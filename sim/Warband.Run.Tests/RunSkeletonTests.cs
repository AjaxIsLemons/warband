using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public class RunSkeletonTests
    {
        private static RunController NewRun(ulong seed = 7, StubContent? content = null,
                                            RunConfig? cfg = null, int heroes = 3) =>
            new RunController(seed, content ?? new StubContent(), Kit.Warband(heroes), cfg);

        private static void ResolveCurrent(RunController run, FightTier tier = FightTier.Even)
        {
            switch (run.CurrentNodeKind)
            {
                case NodeKind.Fight: run.ResolveFight(tier, Kit.AutoPlace(run)); break;
                case NodeKind.Event: run.ResolveEvent(); break;
                case NodeKind.Boss: run.ResolveBoss(Kit.AutoPlace(run)); break;
            }
        }

        private static void DriveToBoss(RunController run)
        {
            while (!run.AtBoss)
            {
                ResolveCurrent(run);
                run.LeaveShop();
            }
        }

        // ---- Structure -------------------------------------------------------------

        [Fact]
        public void FullRunCompletesWithBestOfFiveRecord()
        {
            var state = Kit.PlayOut(NewRun());
            Assert.Equal(RunPhase.Complete, state.Phase);
            Assert.Equal(5, state.BossWins + state.BossLosses);
            Assert.Equal(5, state.CapturedGhosts.Count);
            Assert.Equal(5, state.BossWins);          // weak bot-ghosts: clean sweep
            Assert.True(state.Victory);
            Assert.True(state.Flawless);
        }

        [Fact]
        public void LosingEveryBossStillCompletesTheRun()
        {
            var state = Kit.PlayOut(NewRun(content: new StubContent { WeakBoss = false }));
            Assert.Equal(RunPhase.Complete, state.Phase);
            Assert.Equal(0, state.BossWins);
            Assert.Equal(5, state.BossLosses);
            Assert.False(state.Victory);
        }

        [Fact]
        public void ActMapsHaveExpectedNodeMixAndAreSeedDeterministic()
        {
            var a = NewRun(seed: 42).State.ActMaps;
            var b = NewRun(seed: 42).State.ActMaps;
            Assert.Equal(5, a.Length);
            for (int act = 0; act < 5; act++)
            {
                Assert.Equal(4, a[act].Length);
                Assert.Equal(1, a[act].Count(k => k == NodeKind.Event));
                Assert.Equal(3, a[act].Count(k => k == NodeKind.Fight));
                Assert.Equal(a[act], b[act]);
            }
        }

        [Fact]
        public void BossComesAfterAllNodesAndOnlyThere()
        {
            var run = NewRun();
            Assert.Throws<InvalidOperationException>(() => run.ResolveBoss(Kit.AutoPlace(run)));
            DriveToBoss(run);
            Assert.Equal(NodeKind.Boss, run.CurrentNodeKind);
            Assert.Throws<InvalidOperationException>(
                () => run.ResolveFight(FightTier.Even, Kit.AutoPlace(run)));
        }

        [Fact]
        public void SameSeedSameChoicesSameRun()
        {
            var a = Kit.PlayOut(NewRun(seed: 99));
            var b = Kit.PlayOut(NewRun(seed: 99));
            Assert.Equal(a.Gold, b.Gold);
            Assert.Equal(a.BossWins, b.BossWins);
            Assert.Equal(a.FieldSlots, b.FieldSlots);
            Assert.Equal(a.SlotsBought, b.SlotsBought);
        }

        [Fact]
        public void FightBattlesAreSeedDeterministic()
        {
            var r1 = FirstFightOutcome(NewRun(seed: 5));
            var r2 = FirstFightOutcome(NewRun(seed: 5));
            Assert.Equal(r1.Battle.FinalHash, r2.Battle.FinalHash);
        }

        private static FightOutcome FirstFightOutcome(RunController run)
        {
            while (run.CurrentNodeKind != NodeKind.Fight)
            {
                ResolveCurrent(run);
                run.LeaveShop();
            }
            return run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));
        }

        // ---- Wager payout (ADR 0007) ----------------------------------------------

        [Fact]
        public void WinningFightPaysKillShareAndBonus()
        {
            var run = NewRun();
            var cfg = new RunConfig();
            var o = FirstFightOutcome(run);
            Assert.True(o.Won);
            Assert.Equal(o.EnemyCount, o.EnemiesKilled);
            Assert.Equal(cfg.BaseIncome(1), o.BaseIncome);
            Assert.Equal(cfg.Pot(1, FightTier.Even) * cfg.TierKillSharePct[1] / 100, o.KillPayout);
            Assert.Equal(cfg.Pot(1, FightTier.Even) * (100 - cfg.TierKillSharePct[1]) / 100, o.WinBonus);
            Assert.Equal(o.GoldEarned, run.State.Gold);
        }

        [Fact]
        public void LosingFightStillPaysForKillsButNoBonus()
        {
            var content = new StubContent();
            content.EncounterOverride = (act, node, tier, rng) => new List<(UnitDef, Hex)>
            {
                (new UnitDef { Name = "runt", MaxHp = 10, Attack = 1, AttackInterval = 10,
                               Range = 1, MoveInterval = 5 }, Hex.FromRowCol(6, 0)),
                (new UnitDef { Name = "brute", MaxHp = 1000, Attack = 50, AttackInterval = 10,
                               Range = 1, MoveInterval = 5 }, Hex.FromRowCol(6, 5)),
            };
            var run = NewRun(content: content);
            var cfg = new RunConfig();
            var o = FirstFightOutcome(run);
            Assert.False(o.Won);
            Assert.Equal(1, o.EnemiesKilled);
            Assert.Equal(cfg.Pot(1, FightTier.Even) * cfg.TierKillSharePct[1] * 1 / (100 * 2), o.KillPayout);
            Assert.Equal(0, o.WinBonus);
            Assert.True(o.GoldEarned > 0);        // never zeroed on a loss (ADR 0007)
            Assert.Equal(RunPhase.Shop, run.State.Phase);   // loss consumes the node, run continues
        }

        [Fact]
        public void GreedierTierPaysMoreOnAFullClearWin()
        {
            var safe = FirstFightOutcome(NewRun(seed: 11));
            var greedy = NewRun(seed: 11);
            while (greedy.CurrentNodeKind != NodeKind.Fight) { ResolveCurrent(greedy); greedy.LeaveShop(); }
            var g = greedy.ResolveFight(FightTier.Greedy, Kit.AutoPlace(greedy));
            Assert.True(safe.Won && g.Won);
            Assert.True(g.KillPayout + g.WinBonus > safe.KillPayout + safe.WinBonus);
        }

        [Fact]
        public void EventPaysBaseIncome()
        {
            var run = NewRun();
            var cfg = new RunConfig();
            while (run.CurrentNodeKind != NodeKind.Event) { ResolveCurrent(run); run.LeaveShop(); }
            int before = run.State.Gold;
            int gold = run.ResolveEvent();
            Assert.Equal(cfg.BaseIncome(run.State.Act), gold);
            Assert.Equal(before + gold, run.State.Gold);
        }

        // ---- Slots (ADR 0006) ------------------------------------------------------

        [Fact]
        public void SlotOfferedAtActCloseWithEscalatingCostUpToCap()
        {
            var run = NewRun();
            var cfg = new RunConfig();
            for (int act = 1; act <= 4; act++)
            {
                DriveToBoss(run);
                run.ResolveBoss(Kit.AutoPlace(run));
                if (act <= 3)
                {
                    Assert.True(run.SlotOfferOpen);
                    Assert.Equal(cfg.SlotCosts[act - 1], run.SlotOfferCost);
                    int goldBefore = run.State.Gold;
                    run.BuySlot();
                    Assert.Equal(goldBefore - cfg.SlotCosts[act - 1], run.State.Gold);
                    Assert.Equal(3 + act, run.State.FieldSlots);
                    Assert.False(run.SlotOfferOpen);
                }
                else
                    Assert.False(run.SlotOfferOpen);   // already at cap 6 — no fourth offer
                run.LeaveShop();
            }
            Assert.Equal(6, run.State.FieldSlots);
        }

        [Fact]
        public void DecliningOneOfferStillReachesMaxWidth()
        {
            var run = NewRun();
            for (int act = 1; act <= 4; act++)
            {
                DriveToBoss(run);
                run.ResolveBoss(Kit.AutoPlace(run));
                if (act > 1 && run.SlotOfferOpen) run.BuySlot();   // decline only the first
                run.LeaveShop();                                    // leaving declines act 1's offer
            }
            Assert.Equal(6, run.State.FieldSlots);
        }

        [Fact]
        public void SlotOfferNeverAppearsMidAct()
        {
            var run = NewRun();
            ResolveCurrent(run);
            Assert.False(run.SlotOfferOpen);
        }

        [Fact]
        public void CannotBuySlotWithoutGold()
        {
            var cfg = new RunConfig { SlotCosts = new[] { 9999, 9999, 9999 } };
            var run = NewRun(cfg: cfg);
            DriveToBoss(run);
            run.ResolveBoss(Kit.AutoPlace(run));
            Assert.True(run.SlotOfferOpen);
            Assert.Throws<InvalidOperationException>(() => run.BuySlot());
        }

        // ---- Placement & roster ----------------------------------------------------

        [Fact]
        public void PlacementIsValidated()
        {
            var run = NewRun();
            while (run.CurrentNodeKind != NodeKind.Fight) { ResolveCurrent(run); run.LeaveShop(); }
            var tooFew = new List<Hex> { Hex.FromRowCol(0, 0) };
            var enemyHalf = new List<Hex> { Hex.FromRowCol(0, 0), Hex.FromRowCol(1, 0), Hex.FromRowCol(4, 0) };
            var dupes = new List<Hex> { Hex.FromRowCol(0, 0), Hex.FromRowCol(0, 0), Hex.FromRowCol(1, 0) };
            Assert.Throws<ArgumentException>(() => run.ResolveFight(FightTier.Even, tooFew));
            Assert.Throws<ArgumentException>(() => run.ResolveFight(FightTier.Even, enemyHalf));
            Assert.Throws<ArgumentException>(() => run.ResolveFight(FightTier.Even, dupes));
        }

        [Fact]
        public void StartingWarbandMustFitStartingSlots()
        {
            Assert.Throws<ArgumentException>(() => NewRun(heroes: 4));
            Assert.Throws<ArgumentException>(() => NewRun(heroes: 0));
        }

        [Fact]
        public void BenchStoresHeroesWithinCapsDuringShopOnly()
        {
            var run = NewRun();
            Assert.Throws<InvalidOperationException>(() => run.FieldToBench(0));  // not in shop
            ResolveCurrent(run);                                                   // → shop
            run.FieldToBench(0);
            run.FieldToBench(0);
            Assert.Equal(2, run.State.Bench.Count);
            Assert.Single(run.State.Field);
            Assert.Throws<InvalidOperationException>(() => run.FieldToBench(0));  // bench full
            run.BenchToField(0);
            Assert.Equal(2, run.State.Field.Count);
        }

        // ---- Progression & ghosts --------------------------------------------------

        [Fact]
        public void RunBonusesAccumulateAcrossFights()
        {
            var run = NewRun();
            var hero = run.State.Field[0];
            hero.RunBonuses.Add(new RunBonus
            {
                Per = GrowthMetric.KillsParticipated, Threshold = 1,
                Grant = StatusKind.AttackUp, Mag = 5,
            });
            FirstFightOutcome(run);
            int after1 = hero.Earned.Sum(s => s.Kind == StatusKind.AttackUp ? s.Mag : 0);
            Assert.True(after1 > 0);
            run.LeaveShop();
            while (run.CurrentNodeKind != NodeKind.Fight) { ResolveCurrent(run); run.LeaveShop(); }
            run.ResolveFight(FightTier.Even, Kit.AutoPlace(run));
            int after2 = hero.Earned.Sum(s => s.Kind == StatusKind.AttackUp ? s.Mag : 0);
            Assert.True(after2 > after1);
        }

        [Fact]
        public void BossCapturesGhostSnapshotOfTheBoardGoingIn()
        {
            var run = NewRun();
            DriveToBoss(run);
            var placement = Kit.AutoPlace(run);
            run.ResolveBoss(placement);
            var snap = Assert.Single(run.State.CapturedGhosts);
            Assert.Equal(1, snap.Act);
            Assert.Equal(0, snap.WinsAtCapture);
            Assert.Equal(run.State.Field.Count, snap.Units.Count);
            Assert.Equal(placement[0], snap.Units[0].Pos);
            // Snapshot is a value copy — later run mutation must not leak into the pool.
            run.State.Field[0].Rank = Rank.S;
            Assert.Equal(Rank.C, snap.Units[0].Hero.Rank);
        }
    }
}
