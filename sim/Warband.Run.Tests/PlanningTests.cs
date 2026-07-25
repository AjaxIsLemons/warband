using Warband.Run;
using Warband.Sim;

namespace Warband.Run.Tests
{
    public class PlanningTests
    {
        private sealed class TestRules : PlanningRules
        {
            public override bool IsLegalPosition(Hex position) =>
                Battle.InBounds(position) && position.Row <= 2;

            public override bool CanSetLoadoutOption(
                PlanningDraft draft,
                PlanningHeroState hero,
                string slotId,
                string optionId,
                out string reason)
            {
                bool allowed = slotId == "weapon" && optionId.StartsWith("weapon-");
                reason = allowed ? "" : "That proof loadout option is not allowed.";
                return allowed;
            }

            public override bool CanUseResource(
                PlanningDraft draft,
                PlanningResourceState resource,
                string intentKind,
                string targetId,
                out string reason)
            {
                bool allowed =
                    resource.ContentId == "tonic" &&
                    intentKind == "empower-next-fight" &&
                    draft.FindHero(targetId) != null;
                reason = allowed ? "" : "That consumable cannot affect that target.";
                return allowed;
            }

            public override void ValidateContent(
                PlanningDraft draft,
                PlanningValidationMode mode,
                PlanningValidation validation)
            {
                if (mode != PlanningValidationMode.Commit) return;
                foreach (var hero in draft.Heroes)
                    if (hero.Zone == PlanningZone.Field &&
                        !hero.Loadout.ContainsKey("weapon"))
                        validation.Error(
                            "weapon-required",
                            "Every active hero needs a weapon.",
                            hero.Id);
            }
        }

        private static PlanningHeroState Hero(
            string id,
            PlanningZone zone,
            int row,
            int col,
            int benchSlot = -1,
            bool withWeapon = true)
        {
            var hero = new PlanningHeroState
            {
                Id = id,
                ContentId = id,
                Zone = zone,
                BenchSlot = benchSlot,
                Position = Hex.FromRowCol(row, col),
            };
            if (withWeapon)
                hero.Loadout["weapon"] = $"weapon-{id}";
            return hero;
        }

        private static PlanningDraft StandardDraft()
        {
            var draft = new PlanningDraft
            {
                FieldCapacity = 3,
                BenchCapacity = 2,
            };
            draft.Heroes.Add(Hero("a", PlanningZone.Field, 2, 1));
            draft.Heroes.Add(Hero("b", PlanningZone.Field, 1, 2));
            draft.Heroes.Add(Hero("c", PlanningZone.Field, 0, 3));
            draft.Heroes.Add(Hero("d", PlanningZone.Bench, 1, 4, benchSlot: 0));
            return draft;
        }

        [Fact]
        public void FieldBenchSwapInheritsHexAndUndoRedoRestoresWholeDraft()
        {
            var session = new PlanningSession(StandardDraft(), new TestRules());
            Hex inherited = session.Current.FindHero("b")!.Position;

            var result = session.Execute(new SwapFieldBenchPlanningAction("b", "d"));

            Assert.True(result.Succeeded);
            Assert.Equal(PlanningZone.Bench, session.Current.FindHero("b")!.Zone);
            Assert.Equal(0, session.Current.FindHero("b")!.BenchSlot);
            Assert.Equal(PlanningZone.Field, session.Current.FindHero("d")!.Zone);
            Assert.Equal(inherited, session.Current.FindHero("d")!.Position);

            Assert.True(session.Undo());
            Assert.Equal(PlanningZone.Field, session.Current.FindHero("b")!.Zone);
            Assert.Equal(PlanningZone.Bench, session.Current.FindHero("d")!.Zone);

            Assert.True(session.Redo());
            Assert.Equal(PlanningZone.Bench, session.Current.FindHero("b")!.Zone);
            Assert.Equal(PlanningZone.Field, session.Current.FindHero("d")!.Zone);
        }

        [Fact]
        public void MovingOntoAFieldedHeroSwapsPositionsAtomically()
        {
            var session = new PlanningSession(StandardDraft(), new TestRules());
            Hex a = session.Current.FindHero("a")!.Position;
            Hex c = session.Current.FindHero("c")!.Position;

            var result = session.Execute(new MovePlanningHeroAction("a", c));

            Assert.True(result.Succeeded);
            Assert.Equal(c, session.Current.FindHero("a")!.Position);
            Assert.Equal(a, session.Current.FindHero("c")!.Position);
            Assert.True(session.ValidateForCommit().IsValid);
        }

        [Fact]
        public void InvalidActionCannotPartiallyMutateOrCreateHistory()
        {
            var session = new PlanningSession(StandardDraft(), new TestRules());
            Hex before = session.Current.FindHero("a")!.Position;

            var result = session.Execute(
                new MovePlanningHeroAction("a", Hex.FromRowCol(6, 2)));

            Assert.False(result.Succeeded);
            Assert.Equal(before, session.Current.FindHero("a")!.Position);
            Assert.False(session.CanUndo);
        }

        [Fact]
        public void BenchCapacityIsDataNotACommandAssumption()
        {
            var draft = StandardDraft();
            draft.BenchCapacity = 5;
            var session = new PlanningSession(draft, new TestRules());

            var result = session.Execute(new MovePlanningHeroToBenchAction("a", 4));

            Assert.True(result.Succeeded);
            Assert.Equal(PlanningZone.Bench, session.Current.FindHero("a")!.Zone);
            Assert.Equal(4, session.Current.FindHero("a")!.BenchSlot);
            Assert.Equal(2, session.Current.FieldCount);
        }

        [Fact]
        public void BenchSlotsCanReorderWithoutSpecialCasingCapacity()
        {
            var draft = StandardDraft();
            draft.BenchCapacity = 5;
            draft.Heroes.Add(Hero("e", PlanningZone.Bench, 0, 0, benchSlot: 4));
            var session = new PlanningSession(draft, new TestRules());

            var result = session.Execute(new MovePlanningBenchHeroAction("d", 4));

            Assert.True(result.Succeeded);
            Assert.Equal(4, session.Current.FindHero("d")!.BenchSlot);
            Assert.Equal(0, session.Current.FindHero("e")!.BenchSlot);
        }

        [Fact]
        public void ConsumableUseQueuesTypedIntentAndUndoRestoresCharge()
        {
            var draft = StandardDraft();
            draft.Resources.Add(new PlanningResourceState
            {
                Id = "tonic-17",
                ContentId = "tonic",
                Quantity = 2,
            });
            var session = new PlanningSession(draft, new TestRules());

            var result = session.Execute(
                new UsePlanningResourceAction(
                    "tonic-17",
                    "empower-next-fight",
                    "c",
                    new Dictionary<string, string> { ["stacks"] = "2" }));

            Assert.True(result.Succeeded);
            Assert.Equal(1, session.Current.FindResource("tonic-17")!.Quantity);
            var intent = Assert.Single(session.Current.Intents);
            Assert.Equal("empower-next-fight", intent.Kind);
            Assert.Equal("c", intent.TargetId);
            Assert.Equal("2", intent.Parameters["stacks"]);

            Assert.True(session.Undo());
            Assert.Equal(2, session.Current.FindResource("tonic-17")!.Quantity);
            Assert.Empty(session.Current.Intents);
        }

        [Fact]
        public void EditCanRemainIncompleteButFightCommitReportsContentProblem()
        {
            var draft = StandardDraft();
            draft.FindHero("c")!.Loadout.Clear();
            var session = new PlanningSession(draft, new TestRules());

            var validation = session.ValidateForCommit();

            Assert.False(validation.IsValid);
            Assert.Contains(validation.Issues, issue =>
                issue.Code == "weapon-required" && issue.SubjectId == "c");
        }

        [Fact]
        public void LoadoutOptionUsesContentRulesAndParticipatesInHistory()
        {
            var session = new PlanningSession(StandardDraft(), new TestRules());
            string before = session.Current.FindHero("a")!.Loadout["weapon"];

            var accepted = session.Execute(
                new SetPlanningLoadoutOptionAction("a", "weapon", "weapon-new"));
            var rejected = session.Execute(
                new SetPlanningLoadoutOptionAction("a", "trinket", "forbidden"));

            Assert.True(accepted.Succeeded);
            Assert.False(rejected.Succeeded);
            Assert.Equal("weapon-new", session.Current.FindHero("a")!.Loadout["weapon"]);
            Assert.True(session.Undo());
            Assert.Equal(before, session.Current.FindHero("a")!.Loadout["weapon"]);
        }

        [Fact]
        public void CurrentIsAnIsolatedSnapshotAndCannotBypassActions()
        {
            var session = new PlanningSession(StandardDraft(), new TestRules());

            session.Current.FindHero("a")!.Position = Hex.FromRowCol(0, 0);
            session.Current.Heroes.Clear();

            Assert.Equal(4, session.Current.Heroes.Count);
            Assert.Equal(Hex.FromRowCol(2, 1), session.Current.FindHero("a")!.Position);
            Assert.False(session.CanUndo);
        }

        [Fact]
        public void CommitReturnsExactIsolatedDraftAndClosesPlanningHistory()
        {
            var session = new PlanningSession(StandardDraft(), new TestRules());
            Assert.True(session.Execute(
                new SetPlanningLoadoutOptionAction("a", "weapon", "weapon-new")).Succeeded);

            var committed = session.Commit();

            Assert.True(committed.Succeeded);
            Assert.NotNull(committed.Draft);
            Assert.Equal("weapon-new", committed.Draft!.FindHero("a")!.Loadout["weapon"]);
            Assert.False(session.CanUndo);
            Assert.False(session.CanRedo);

            committed.Draft.FindHero("a")!.Loadout["weapon"] = "tampered";
            Assert.Equal("weapon-new", session.Current.FindHero("a")!.Loadout["weapon"]);
        }
    }
}
