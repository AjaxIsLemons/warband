using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public class BotGhostTests
    {
        private static GhostSnapshot Gen(int act, int wins, ulong seed = 3,
                                         StubContent? content = null) =>
            BotGhosts.Generate(content ?? new StubContent(), new RunConfig(), act, wins, new Rng(seed));

        [Fact]
        public void GenerationIsDeterministic()
        {
            var a = Gen(3, 1);
            var b = Gen(3, 1);
            Assert.Equal(a.Units.Select(u => u.Hero.ChassisId), b.Units.Select(u => u.Hero.ChassisId));
            Assert.Equal(a.Units.Select(u => u.Hero.Rank), b.Units.Select(u => u.Hero.Rank));
            Assert.Equal(a.Units.Select(u => u.Pos), b.Units.Select(u => u.Pos));
            Assert.Equal(a.BannerIds, b.BannerIds);
        }

        [Fact]
        public void BoardSizeTracksActSlotGrowth()
        {
            Assert.Equal(3, Gen(1, 0).Units.Count);
            Assert.Equal(4, Gen(2, 1).Units.Count);
            Assert.Equal(6, Gen(4, 0).Units.Count);
            Assert.Equal(6, Gen(5, 3).Units.Count);
        }

        [Fact]
        public void RecordDeepensTheBoard()
        {
            int Depth(GhostSnapshot s) => s.Units.Sum(u => (int)u.Hero.Rank);
            Assert.Equal(2, Depth(Gen(3, 0)));    // act-1 rank-ups baseline
            Assert.Equal(4, Depth(Gen(3, 2)));    // + record keying (ADR 0002)
        }

        [Fact]
        public void SpecNodesMatchRanksAndForksSetPaths()
        {
            foreach (var u in Gen(5, 4).Units)
            {
                Assert.Equal((int)u.Hero.Rank, u.Hero.SpecNodeIds.Count);
                if (u.Hero.Rank >= Rank.B) Assert.NotNull(u.Hero.PathId);
            }
        }

        [Fact]
        public void PlacementsAreValidOwnerHalfAndDistinct()
        {
            var seen = new HashSet<Hex>();
            foreach (var u in Gen(5, 5).Units)
            {
                Assert.True(Battle.InBounds(u.Pos));
                Assert.True(u.Pos.Row <= 3);
                Assert.True(seen.Add(u.Pos));
            }
        }

        [Fact]
        public void RecordFieldsDescribeTheRecordGoingIn()
        {
            var s = Gen(4, 2);
            Assert.Equal(2, s.WinsAtCapture);
            Assert.Equal(1, s.LossesAtCapture);   // 3 bosses fought before act 4
        }

        [Fact]
        public void FullRunAgainstBotGhostsCompletes()
        {
            var state = Kit.PlayOut(new RunController(
                21, new StubContent { UseBotGhosts = true }, Kit.Warband()));
            Assert.Equal(RunPhase.Complete, state.Phase);
            Assert.Equal(3, state.BossWins + state.BossLosses);
        }
    }

    public class HarnessTests
    {
        [Fact]
        public void PlayCompletesAndRecordsEveryFight()
        {
            var report = RunHarness.Play(9, new StubContent { UseBotGhosts = true });
            Assert.Equal(RunPhase.Complete, report.Final.Phase);
            Assert.Equal(12, report.Fights.Count);            // 9 risk fights + 3 bosses
            Assert.Equal(3, report.Fights.Count(f => f.IsBoss));
            Assert.True(report.GoldFromNodes > 0);
        }

        [Fact]
        public void PlayIsDeterministic()
        {
            var a = RunHarness.Play(14, new StubContent { UseBotGhosts = true });
            var b = RunHarness.Play(14, new StubContent { UseBotGhosts = true });
            Assert.Equal(a.Final.Gold, b.Final.Gold);
            Assert.Equal(a.Final.BossWins, b.Final.BossWins);
            Assert.Equal(a.GoldSpentInShops, b.GoldSpentInShops);
            Assert.Equal(a.Fights.Select(f => f.EndTick), b.Fights.Select(f => f.EndTick));
        }

        [Fact]
        public void DefaultBotActuallyShops()
        {
            var report = RunHarness.Play(9, new StubContent { UseBotGhosts = true });
            Assert.True(report.GoldSpentInShops > 0);
            bool progressed = report.Final.SlotsBought > 0
                || report.Final.Field.Concat(report.Final.Bench).Any(h => h.Rank > Rank.C)
                || report.Final.Field.Count + report.Final.Bench.Count > 3;
            Assert.True(progressed);
        }

        [Fact]
        public void AggregateFoldsAcrossRuns()
        {
            var reports = RunHarness.PlayMany(5, 100, new StubContent { UseBotGhosts = true });
            var agg = AggregateReport.From(reports);
            Assert.Equal(5, agg.Runs);
            Assert.InRange(agg.AvgBossWins, 0, 3);
            Assert.Equal(45, agg.Tiers[FightTier.Fraying].Chosen); // 9 risk fights × 5 runs
            Assert.True(agg.AvgFightTicks > 0);
        }

        [Fact]
        public void PolicyHooksOverrideTheDefaults()
        {
            var policy = new RunPolicy { Tier = _ => FightTier.Collapsing };
            var report = RunHarness.Play(9, new StubContent { UseBotGhosts = true }, policy: policy);
            Assert.All(report.Fights.Where(f => !f.IsBoss),
                       f => Assert.Equal(FightTier.Collapsing, f.Tier));
        }
    }
}
