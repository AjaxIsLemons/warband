using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The preview must be the fight. Every encounter is shown before deployment, so a preview
    /// that differs from what actually spawns is worse than no preview at all — the player would
    /// plan a formation against an army that never arrives.
    /// </summary>
    public class PreviewEnemiesTests
    {
        private static RunController AtFight(StubContent content, out FightTier tier)
        {
            tier = FightTier.Fraying;
            var run = new RunController(31, content, Kit.Warband());
            while (run.CurrentNodeKind != NodeKind.Fight)
                run.ResolveEvent();
            return run;
        }

        [Fact]
        public void PreviewMatchesTheEnemiesTheFightActuallySpawns()
        {
            var content = new StubContent();
            var run = AtFight(content, out var tier);

            var preview = run.PreviewEnemies(tier);
            var outcome = run.ResolveFight(tier, Kit.AutoPlace(run));

            // Enemy ids start at 100 in the battle; compare the spawned bodies to the preview.
            var spawned = outcome.Battle.InitialUnits.Where(u => u.Team == 1).ToList();
            Assert.Equal(preview.Count, spawned.Count);
            for (int i = 0; i < preview.Count; i++)
            {
                Assert.Equal(preview[i].Def.Name, spawned[i].Name);
                Assert.Equal(preview[i].Def.MaxHp, spawned[i].MaxHp);
                Assert.Equal(preview[i].Pos, spawned[i].Pos);
            }
        }

        [Fact]
        public void PreviewIsStableAcrossRepeatedCalls()
        {
            var content = new StubContent();
            var run = AtFight(content, out var tier);

            var a = run.PreviewEnemies(tier);
            var b = run.PreviewEnemies(tier);
            Assert.Equal(a.Select(x => (x.Def.Name, x.Def.MaxHp, x.Pos)),
                         b.Select(x => (x.Def.Name, x.Def.MaxHp, x.Pos)));
        }

        [Fact]
        public void BossPreviewMatchesTheBossFight()
        {
            var content = new StubContent();
            var run = new RunController(44, content, Kit.Warband());
            while (!run.AtBoss)
            {
                if (run.CurrentNodeKind == NodeKind.Fight)
                    run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
                else
                    run.ResolveEvent();
            }

            var preview = run.PreviewEnemies(FightTier.Fraying);
            var outcome = run.ResolveBoss(Kit.AutoPlace(run));
            var spawned = outcome.Battle.InitialUnits.Where(u => u.Team == 1).ToList();

            Assert.Equal(preview.Count, spawned.Count);
            for (int i = 0; i < preview.Count; i++)
                Assert.Equal(preview[i].Pos, spawned[i].Pos);
        }

        [Fact]
        public void PreviewRefusesWhenThereIsNothingToPreview()
        {
            var content = new StubContent();
            var run = new RunController(5, content, Kit.Warband());
            while (run.CurrentNodeKind != NodeKind.Event)
                run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
            Assert.Throws<System.InvalidOperationException>(() => run.PreviewEnemies(FightTier.Fraying));

            run.ResolveEvent();
            Assert.NotEmpty(run.PreviewEnemies(FightTier.Fraying)); // next fight is immediately inspectable

            while (!run.AtBoss)
            {
                if (run.CurrentNodeKind == NodeKind.Fight)
                    run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
                else
                    run.ResolveEvent();
            }
            run.ResolveBoss(Kit.AutoPlace(run));
            Assert.Equal(RunPhase.Reward, run.State.Phase);
            Assert.Throws<System.InvalidOperationException>(() => run.PreviewEnemies(FightTier.Fraying));
        }
    }
}
