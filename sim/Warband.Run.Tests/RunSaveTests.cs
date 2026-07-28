using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Roadmap item 7. The bar is not "a file appears" — it is **a resumed run is the same run**.
    /// A save system that loses the player's earned statuses, forgets which offers were frozen, or
    /// re-rolls the encounter they were looking at is worse than no save, because it destroys the
    /// run quietly instead of loudly.
    /// </summary>
    public class RunSaveTests
    {
        /// <summary>A run driven several beats in on the REAL catalog, so the state under test has
        /// actual heroes, actual offers, actual inventory and actual earned growth in it.</summary>
        private static RunController MidRun(ulong seed = 41)
        {
            var cat = new Catalog();
            var run = new RunController(seed, cat, RunHarness.StarterWarband(cat, new RunConfig()));
            for (int beat = 0; beat < 3 && !run.State.Over; beat++)
            {
                if (run.State.Phase != RunPhase.Planning) break;
                switch (run.CurrentNodeKind)
                {
                    case NodeKind.Event: run.ResolveInterlude(InterludePath.Treasury); break;
                    case NodeKind.Fight: run.ResolveFight(FightTier.Stable, Kit.AutoPlace(run)); break;
                    case NodeKind.Boss: run.ResolveBoss(Kit.AutoPlace(run)); break;
                }
            }
            return run;
        }

        // ---- round trip -------------------------------------------------------------

        [Fact]
        public void EveryFieldOfAMidRunStateSurvivesARoundTrip()
        {
            var before = MidRun().State;
            var after = RunSave.Read(RunSave.Write(before));

            Assert.Equal(before.Seed, after.Seed);
            Assert.Equal(before.Act, after.Act);
            Assert.Equal(before.NodeIndex, after.NodeIndex);
            Assert.Equal(before.Phase, after.Phase);
            Assert.Equal(before.Gold, after.Gold);
            Assert.Equal(before.FieldSlots, after.FieldSlots);
            Assert.Equal(before.UnlockedFieldSlots, after.UnlockedFieldSlots);
            Assert.Equal(before.SlotsBought, after.SlotsBought);
            Assert.Equal(before.SlotOfferPending, after.SlotOfferPending);
            Assert.Equal(before.ShopRolls, after.ShopRolls);
            Assert.Equal(before.NextHeroInstanceId, after.NextHeroInstanceId);
            Assert.Equal(before.NextItemInstanceId, after.NextItemInstanceId);
            Assert.Equal(before.BossWins, after.BossWins);
            Assert.Equal(before.BossLosses, after.BossLosses);
            Assert.Equal(before.PendingBossSand, after.PendingBossSand);
            Assert.Equal(before.Banners, after.Banners);
            Assert.Equal(before.PendingBossRewards, after.PendingBossRewards);

            Assert.Equal(before.ActMaps.Length, after.ActMaps.Length);
            for (int a = 0; a < before.ActMaps.Length; a++)
                Assert.Equal(before.ActMaps[a], after.ActMaps[a]);

            AssertHeroesEqual(before.Field, after.Field);
            AssertHeroesEqual(before.Bench, after.Bench);

            Assert.Equal(
                before.ShopOffers.Select(o => o == null
                    ? "null"
                    : $"{o.Kind}/{o.Id}/{o.Price}/{o.Frozen}/{o.Tier}"),
                after.ShopOffers.Select(o => o == null
                    ? "null"
                    : $"{o.Kind}/{o.Id}/{o.Price}/{o.Frozen}/{o.Tier}"));

            Assert.Equal(
                before.Inventory.Select(i => (i.InstanceId, i.Kind, i.Id, i.Tier, i.SandInvested)),
                after.Inventory.Select(i => (i.InstanceId, i.Kind, i.Id, i.Tier, i.SandInvested)));
        }

        private static void AssertHeroesEqual(List<HeroInstance> a, List<HeroInstance> b)
        {
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].InstanceId, b[i].InstanceId);
                Assert.Equal(a[i].ChassisId, b[i].ChassisId);
                Assert.Equal(a[i].Rank, b[i].Rank);
                Assert.Equal(a[i].PathId, b[i].PathId);
                Assert.Equal(a[i].GoldSpent, b[i].GoldSpent);
                Assert.Equal(a[i].WeaponId, b[i].WeaponId);
                Assert.Equal(a[i].WeaponTier, b[i].WeaponTier);
                Assert.Equal(a[i].WeaponInstanceId, b[i].WeaponInstanceId);
                Assert.Equal(a[i].WeaponSandInvested, b[i].WeaponSandInvested);
                Assert.Equal(a[i].StarterWeaponTier, b[i].StarterWeaponTier);
                Assert.Equal(a[i].StarterWeaponSandInvested, b[i].StarterWeaponSandInvested);
                Assert.Equal(a[i].TrinketIds, b[i].TrinketIds);
                Assert.Equal(a[i].TrinketInstanceId, b[i].TrinketInstanceId);
                Assert.Equal(a[i].TrinketSandInvested, b[i].TrinketSandInvested);
                Assert.Equal(a[i].SpecNodeIds, b[i].SpecNodeIds);
                Assert.Equal(a[i].RunBonuses.Select(r => (r.Per, r.Threshold, r.Grant, r.Mag)),
                             b[i].RunBonuses.Select(r => (r.Per, r.Threshold, r.Grant, r.Mag)));
                Assert.Equal(a[i].Earned.Select(e => (e.Kind, e.Mag, e.TicksLeft, e.SwingsLeft, e.SourceId)),
                             b[i].Earned.Select(e => (e.Kind, e.Mag, e.TicksLeft, e.SwingsLeft, e.SourceId)));
            }
        }

        [Fact]
        public void WritingIsStableSoASaveOnlyChangesWhenTheRunDoes()
        {
            var state = MidRun().State;
            Assert.Equal(RunSave.Write(state), RunSave.Write(state));
            Assert.Equal(RunSave.Write(state), RunSave.Write(RunSave.Read(RunSave.Write(state))));
        }

        [Fact]
        public void EarnedGrowthAndFrozenOffersSurvive()
        {
            // The two things a naive save loses first: run-scoped earned statuses (the hero quietly
            // resumes weaker) and per-offer freezes (the player paid attention for nothing).
            var state = MidRun().State;
            state.Field[0].Earned.Add(new Status
            { Kind = StatusKind.AttackUp, Mag = 7, TicksLeft = -1, SwingsLeft = 0, SourceId = 3 });
            state.Field[0].RunBonuses.Add(new RunBonus
            { Per = GrowthMetric.DamageDealt, Threshold = 50, Grant = StatusKind.CritUp, Mag = 2 });
            int frozenAt = state.ShopOffers.FindIndex(o => o != null);
            state.ShopOffers[frozenAt]!.Frozen = true;

            var after = RunSave.Read(RunSave.Write(state));
            var earned = after.Field[0].Earned.Single(e => e.Kind == StatusKind.AttackUp && e.Mag == 7);
            Assert.Equal(-1, earned.TicksLeft);
            Assert.Equal(3, earned.SourceId);
            Assert.Contains(after.Field[0].RunBonuses,
                r => r.Per == GrowthMetric.DamageDealt && r.Threshold == 50 && r.Mag == 2);
            Assert.True(after.ShopOffers[frozenAt]!.Frozen);
        }

        [Fact]
        public void SoldOutOfferSlotsStayEmptyRatherThanRefilling()
        {
            var state = MidRun().State;
            state.ShopOffers[0] = null;                     // bought
            var after = RunSave.Read(RunSave.Write(state));
            Assert.Null(after.ShopOffers[0]);
            Assert.Equal(state.ShopOffers.Count, after.ShopOffers.Count);
        }

        [Fact]
        public void AHeroWithNoTrinketsResumesWithNoTrinkets()
        {
            // Empty list vs list-of-one-empty-string is the classic delimiter bug.
            var state = MidRun().State;
            state.Field[0].TrinketIds.Clear();
            var after = RunSave.Read(RunSave.Write(state));
            Assert.Empty(after.Field[0].TrinketIds);
        }

        [Fact]
        public void AnImplicitStarterWeaponStaysImplicit()
        {
            // WeaponId null means "chassis starter". If null round-trips as "", the composer would
            // look up a weapon called "" and the run would fail to resume.
            var state = MidRun().State;
            state.Field[0].WeaponId = null;
            state.Field[0].PathId = null;
            var after = RunSave.Read(RunSave.Write(state));
            Assert.Null(after.Field[0].WeaponId);
            Assert.Null(after.Field[0].PathId);
        }

        [Fact]
        public void LegacySaveWithoutHeroIdsMigratesDeterministically()
        {
            string legacy = RunSave.Write(MidRun().State);
            var lines = legacy.Split('\n')
                .Where(line => !line.StartsWith("nextHeroInstanceId=", StringComparison.Ordinal) &&
                               !((line.StartsWith("field.", StringComparison.Ordinal) ||
                                  line.StartsWith("bench.", StringComparison.Ordinal)) &&
                                 line.Contains(".instanceId=", StringComparison.Ordinal)));

            RunState migrated = RunSave.Read(string.Join("\n", lines));
            long[] ids = migrated.Field.Concat(migrated.Bench)
                .Select(hero => hero.InstanceId)
                .ToArray();

            Assert.Equal(Enumerable.Range(1, ids.Length).Select(value => (long)value), ids);
            Assert.Equal(ids.Length + 1, migrated.NextHeroInstanceId);
            RunState roundTripped = RunSave.Read(RunSave.Write(migrated));
            Assert.Equal(ids, roundTripped.Field.Concat(roundTripped.Bench)
                .Select(hero => hero.InstanceId));
        }

        // ---- refusing what it cannot trust ------------------------------------------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a save at all")]
        [InlineData("warband-run-save v99\nact=1\n")]
        public void GarbageAndFutureFormatsAreRefused(string text)
        {
            Assert.Throws<RunSaveException>(() => RunSave.Read(text));
        }

        [Fact]
        public void ATruncatedSaveIsRefusedRatherThanLoadedAsAStrangeRun()
        {
            string full = RunSave.Write(MidRun().State);
            string half = full.Substring(0, full.Length / 2);
            Assert.Throws<RunSaveException>(() => RunSave.Read(half));
        }

        [Fact]
        public void CorruptValuesAreNamedNotSwallowed()
        {
            string save = RunSave.Write(MidRun().State).Replace("\nact=", "\nact=banana");
            var ex = Assert.Throws<RunSaveException>(() => RunSave.Read(save));
            Assert.Contains("act", ex.Message);
        }

        [Fact]
        public void AnIdContainingAReservedCharacterFailsAtWriteTimeNotLoadTime()
        {
            var state = MidRun().State;
            state.Field[0].SpecNodeIds.Add("cleric.warpriest|smuggled");
            var ex = Assert.Throws<RunSaveException>(() => RunSave.Write(state));
            Assert.Contains("reserved character", ex.Message);
        }

        [Fact]
        public void ASaveReferencingContentThisBuildLacksIsRefusedOnResume()
        {
            var state = MidRun().State;
            state.Field[0].ChassisId = "chassis-from-a-future-build";
            var ex = Assert.Throws<RunSaveException>(
                () => RunController.Resume(RunSave.Read(RunSave.Write(state)), new Catalog()));
            Assert.Contains("chassis-from-a-future-build", ex.Message);
        }

        // ---- the one that matters ---------------------------------------------------

        [Fact]
        public void AResumedRunPlaysOutIDENTICALLYToOneThatWasNeverSaved()
        {
            // The whole point. Two controllers from the same mid-run position — one carried on, one
            // was saved, serialized, and rebuilt from text — must produce the same encounters, the
            // same battles event-for-event, and the same economy to the last grain of Sand.
            var original = MidRun(seed: 77);
            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(original.State)), new Catalog());

            var a = PlayOut(original);
            var b = PlayOut(resumed);

            Assert.Equal(a.Log, b.Log);
            Assert.Equal(a.Phase, b.Phase);
            Assert.Equal(a.Sand, b.Sand);
            Assert.True(a.Log.Count > 0, "the run was already over — this proves nothing");
        }

        [Fact]
        public void ResumingDoesNotRerollTheEncounterThePlayerWasLookingAt()
        {
            var original = MidRun(seed: 5);
            if (original.State.Phase != RunPhase.Planning || original.CurrentNodeKind == NodeKind.Event)
                original = MidRun(seed: 12);

            var before = original.PreviewEnemies(FightTier.Fraying);
            var beforeBrief = original.PreviewBrief(FightTier.Fraying);

            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(original.State)), new Catalog());
            var after = resumed.PreviewEnemies(FightTier.Fraying);
            var afterBrief = resumed.PreviewBrief(FightTier.Fraying);

            Assert.Equal(beforeBrief.Id, afterBrief.Id);
            Assert.Equal(before.Select(e => (e.Def.Name, e.Def.MaxHp, e.Pos.Row, e.Pos.Q)),
                         after.Select(e => (e.Def.Name, e.Def.MaxHp, e.Pos.Row, e.Pos.Q)));
        }

        [Fact]
        public void ResumingDoesNotRerollTheShopStock()
        {
            var original = MidRun(seed: 9);
            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(original.State)), new Catalog());
            Assert.Equal(
                original.State.ShopOffers.Select(o => o == null ? "null" : $"{o.Kind}/{o.Id}/{o.Price}"),
                resumed.State.ShopOffers.Select(o => o == null ? "null" : $"{o.Kind}/{o.Id}/{o.Price}"));
        }

        // ---- content provenance (ADR 0008's contentVersion) -------------------------

        [Fact]
        public void ANewRunIsStampedWithTheContentItWasCreatedUnder()
        {
            var cat = new Catalog();
            var run = new RunController(1, cat, RunHarness.StarterWarband(cat, new RunConfig()));
            Assert.False(string.IsNullOrEmpty(run.State.ContentVersion));
            Assert.Equal(cat.ContentVersion, run.State.ContentVersion);
        }

        [Fact]
        public void TheContentVersionIsStableAcrossCatalogInstances()
        {
            // Two catalogs in one process must agree, or every save refuses to load.
            Assert.Equal(new Catalog().ContentVersion, new Catalog().ContentVersion);
        }

        [Fact]
        public void TheStampSurvivesTheSaveRoundTrip()
        {
            var state = MidRun().State;
            Assert.Equal(state.ContentVersion, RunSave.Read(RunSave.Write(state)).ContentVersion);
        }

        [Fact]
        public void ASaveFromRETUNEDContentIsRefusedEvenThoughEveryIdStillResolves()
        {
            // THE case this exists for. Every id is valid, so the eager id check passes clean — but
            // the run's encounters are derived from its seed at FIGHT time, so resuming under
            // different numbers fights a different army than the save was made against.
            var state = MidRun().State;
            state.ContentVersion = "0123456789abcdef";      // as if a number had moved
            var ex = Assert.Throws<RunSaveException>(
                () => RunController.Resume(RunSave.Read(RunSave.Write(state)), new Catalog()));
            Assert.Contains("different content", ex.Message);
            Assert.Contains("0123456789abcdef", ex.Message);   // names the save's stamp
        }

        [Fact]
        public void ASavePredatingContentStampingIsRefusedAndSaysSo()
        {
            var state = MidRun().State;
            state.ContentVersion = "";
            var ex = Assert.Throws<RunSaveException>(
                () => RunController.Resume(RunSave.Read(RunSave.Write(state)), new Catalog()));
            Assert.Contains("unversioned", ex.Message);
        }

        [Fact]
        public void AMatchingStampResumesNormally()
        {
            // The guard must not be so eager that it blocks the ordinary case.
            var run = MidRun();
            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(run.State)), new Catalog());
            Assert.Equal(run.State.Act, resumed.State.Act);
        }

        // ---- the awkward phases the shell can be quit in ----------------------------

        [Fact]
        public void ARunSavedWithABossRewardPendingResumesStillOwingTheChoice()
        {
            // Quitting on the reward screen is a real thing a player does, and Reward is the one
            // non-Planning phase a live run can sit in. If it resumed as Planning the player would
            // silently lose an Inscription.
            // The Reward phase is constructed rather than played into: with terminal loss and
            // authored encounters, the naive Stable-tier line that buys nothing never beats a boss
            // (checked — no seed in 1..40 gets there), and what is under test is the RESUME, not the
            // bot's win rate. Playing into it would only make the test skip itself.
            var cat = new Catalog();
            var cfg = new RunConfig();
            var state = MidRun().State;
            state.Act = 1;
            state.NodeIndex = cfg.NodesPerAct;               // the boss beat
            state.Phase = RunPhase.Reward;
            state.PendingBossSand = 6;
            state.PendingBossRewards.Clear();
            state.PendingBossRewards.AddRange(new[] { "firstblood", "chorus" });

            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(state)), cat, cfg);
            Assert.Equal(RunPhase.Reward, resumed.State.Phase);
            Assert.Equal(new[] { "firstblood", "chorus" }, resumed.PreviewBossRewards());

            resumed.ChooseBossReward(0);
            Assert.Equal(RunPhase.Planning, resumed.State.Phase);
            Assert.Contains("firstblood", resumed.State.Banners);
            Assert.Equal(2, resumed.State.Act);              // the reward closed the act
        }

        [Fact]
        public void ARunSavedWithARankUpPendingResumesStillOwingTheSpecChoice()
        {
            var cat = new Catalog();
            var state = MidRun().State;
            state.PendingSpec = new PendingSpec
            {
                Zone = RosterZone.Field,
                Index = 0,
                ForRank = Rank.B,
                Options = { "cleric.warpriest", "cleric.lifebinder" },
            };
            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(state)), cat);

            Assert.NotNull(resumed.State.PendingSpec);
            Assert.Equal(new[] { "cleric.warpriest", "cleric.lifebinder" },
                         resumed.State.PendingSpec!.Options);
            Assert.Equal(Rank.B, resumed.State.PendingSpec.ForRank);
        }

        [Fact]
        public void AFinishedRunIsStillReadableSoTheShellCanSeeItIsOverAndDropIt()
        {
            // The shell deletes the file when State.Over, but it has to be able to LOAD one first
            // (e.g. a crash between the run ending and the delete). Refusing it would strand the
            // player on a CONTINUE button that never works.
            var state = MidRun().State;
            state.Phase = RunPhase.Defeated;
            var after = RunSave.Read(RunSave.Write(state));
            Assert.True(after.Over);
            Assert.False(after.Victory);
        }

        private static (List<string> Log, RunPhase Phase, int Sand) PlayOut(RunController run)
        {
            var log = new List<string>();
            int guard = 0;
            while (!run.State.Over && run.State.Phase == RunPhase.Planning && guard++ < 40)
            {
                if (run.State.PendingSpec != null) { run.ChooseSpec(0); continue; }
                switch (run.CurrentNodeKind)
                {
                    case NodeKind.Event:
                        log.Add($"interlude a{run.State.Act} n{run.State.NodeIndex} " +
                                $"+{run.ResolveInterlude(InterludePath.Treasury).Sand}");
                        break;
                    case NodeKind.Fight:
                    {
                        var o = run.ResolveFight(FightTier.Stable, Kit.AutoPlace(run));
                        log.Add($"fight a{run.State.Act} won={o.Won} sand={o.SandEarned} " +
                                $"ticks={o.Battle.EndTick} hash={Hash(o.Battle)}");
                        break;
                    }
                    case NodeKind.Boss:
                    {
                        var o = run.ResolveBoss(Kit.AutoPlace(run));
                        log.Add($"boss a{run.State.Act} won={o.Won} ticks={o.Battle.EndTick} " +
                                $"hash={Hash(o.Battle)}");
                        if (run.State.Phase == RunPhase.Reward) run.ChooseBossReward(0);
                        break;
                    }
                }
            }
            return (log, run.State.Phase, run.State.Sand);
        }

        /// <summary>Cheap order-sensitive digest of a whole battle log — if resume perturbed the
        /// simulation anywhere, this diverges.</summary>
        private static int Hash(BattleResult r)
        {
            int h = 17;
            foreach (var e in r.Events)
                h = unchecked(h * 31 + (int)e.Kind * 7 + e.Tick * 13 + e.Source * 3 + e.Target * 5 + e.Amount);
            return h;
        }
    }
}
