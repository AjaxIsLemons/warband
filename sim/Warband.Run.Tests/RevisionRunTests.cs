using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public class RevisionRunTests
    {
        private static RunController NewRun(string revision = RevisionCatalog.BorrowedFutureId) =>
            new RunController(191, new StubContent(), Kit.Warband(), revisionId: revision);

        [Fact]
        public void StartingLineagesAndEveryTierAreComplete()
        {
            Assert.Equal(2, RevisionCatalog.Starting.Count);
            foreach (var revision in RevisionCatalog.Starting)
            {
                Assert.Equal(3, revision.Tiers.Count);
                Assert.All(revision.Tiers, tier => Assert.Equal(2, tier.Length));
            }
        }

        [Fact]
        public void InterludeEvolutionIsBlockingSeparateAndSaved()
        {
            var run = NewRun();
            while (run.CurrentNodeKind != NodeKind.Event)
                run.ResolveFight(FightTier.Stable, Kit.AutoPlace(run));

            Assert.Equal(2, run.PreviewRevisionUpgrades().Count);
            Assert.Throws<InvalidOperationException>(
                () => run.ResolveInterlude(InterludePath.Treasury));
            var picked = run.ChooseRevisionUpgrade(1);
            int sand = run.State.Sand;
            run.ResolveInterlude(InterludePath.Treasury);

            Assert.Contains(picked.Id, run.State.Revision.UpgradeIds);
            Assert.True(run.State.Sand > sand);
            var restored = RunSave.Read(RunSave.Write(run.State));
            Assert.Equal(run.State.Revision.RevisionId, restored.Revision.RevisionId);
            Assert.Equal(run.State.Revision.UpgradeIds, restored.Revision.UpgradeIds);
        }

        [Fact]
        public void PreparingDoesNotMutateAndOnlyOneFutureCanCommit()
        {
            var content = new StubContent
            {
                EncounterOverride = (_, _, _, _) => new List<(UnitDef, Hex)>
                {
                    (new UnitDef
                    {
                        Name = "revision anchor",
                        MaxHp = 2000,
                        Attack = 1,
                        AttackInterval = 20,
                        MoveInterval = 3,
                        Range = 1,
                    }, Hex.FromRowCol(6, 2)),
                },
            };
            var run = new RunController(191, content, Kit.Warband(),
                revisionId: RevisionCatalog.RecallToFormationId);
            int node = run.State.NodeIndex;
            int sand = run.State.Sand;
            var prepared = run.PrepareFight(FightTier.Stable, Kit.AutoPlace(run));

            Assert.Equal(node, run.State.NodeIndex);
            Assert.Equal(sand, run.State.Sand);
            Assert.Equal(RunPhase.Planning, run.State.Phase);

            int present = Math.Min(20, prepared.Original.Battle.EndTick);
            Assert.True(present >= 10);
            var revised = run.CommitRevision(prepared, new RevisionChoice
            {
                PresentTick = present,
                BranchTick = present - 10,
                TargetIds = { 100 },
            });

            Assert.True(revised.Revised);
            Assert.Contains(revised.Battle.Events, e => e.Kind == EventKind.RevisionApplied);
            Assert.Throws<InvalidOperationException>(() => run.CommitOriginal(prepared));
        }

        [Fact]
        public void OriginalAndRevisionUseTheSamePreparedOpening()
        {
            var originalRun = NewRun(RevisionCatalog.RecallToFormationId);
            var revisedRun = NewRun(RevisionCatalog.RecallToFormationId);
            var originalPrepared = originalRun.PrepareFight(
                FightTier.Stable, Kit.AutoPlace(originalRun));
            var revisedPrepared = revisedRun.PrepareFight(
                FightTier.Stable, Kit.AutoPlace(revisedRun));

            Assert.Equal(
                originalPrepared.Original.Battle.Events
                    .Where(e => e.Tick < 10)
                    .Select(e => (e.Tick, e.Kind, e.Source, e.Target, e.Amount)),
                revisedPrepared.Original.Battle.Events
                    .Where(e => e.Tick < 10)
                    .Select(e => (e.Tick, e.Kind, e.Source, e.Target, e.Amount)));
        }
    }
}
