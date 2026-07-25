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
                ResolveCurrent(run);
        }

        // ---- Structure -------------------------------------------------------------

        [Fact]
        public void FullRunCompletesByBeatingEveryActBoss()
        {
            // ADR 0016: victory is reaching the end of the last act alive, not a best-of-5 record.
            var state = Kit.PlayOut(NewRun());
            Assert.Equal(RunPhase.Complete, state.Phase);
            Assert.Equal(3, state.BossWins);          // one authored boss per act, all beaten
            Assert.Equal(0, state.BossLosses);
            Assert.True(state.Victory);
            Assert.True(state.Over);
        }

        [Fact]
        public void LosingABossEndsTheRunImmediately()
        {
            // PoC defeat rule (Jake, 2026-07-24): lose any fight and the run is over. The old
            // behaviour — lose all five bosses and still "complete" the run — was best-of-5.
            var state = Kit.PlayOut(NewRun(content: new StubContent { WeakBoss = false }));
            Assert.Equal(RunPhase.Defeated, state.Phase);
            Assert.Equal(0, state.BossWins);
            Assert.Equal(1, state.BossLosses);        // it ends at the FIRST loss
            Assert.Equal(1, state.Act);
            Assert.False(state.Victory);
            Assert.True(state.Over);
        }

        [Fact]
        public void ActMapsHaveExpectedNodeMixAndAreSeedDeterministic()
        {
            var a = NewRun(seed: 42).State.ActMaps;
            var b = NewRun(seed: 42).State.ActMaps;
            Assert.Equal(3, a.Length);
            for (int act = 0; act < 3; act++)
            {
                Assert.Equal(4, a[act].Length);
                Assert.Equal(1, a[act].Count(k => k == NodeKind.Event));
                Assert.Equal(3, a[act].Count(k => k == NodeKind.Fight));
                Assert.Equal(NodeKind.Event, a[act][2]);
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
                ResolveCurrent(run);
            return run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
        }

        // ---- Visible terminal-risk rewards -----------------------------------------

        [Fact]
        public void WinningFightPaysTheVisibleTierReward()
        {
            var run = NewRun();
            var cfg = new RunConfig();
            int before = run.State.Sand;
            var o = FirstFightOutcome(run);
            Assert.True(o.Won);
            Assert.Equal(o.EnemyCount, o.EnemiesKilled);
            Assert.Equal(cfg.FightReward(1, FightTier.Fraying), o.SandEarned);
            Assert.Equal(0, o.KillPayout);
            Assert.Equal(0, o.WinBonus);
            Assert.Equal(before + o.SandEarned, run.State.Sand);
        }

        [Fact]
        public void LosingFightEndsTheRunAndPaysNothing()
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
            int before = run.State.Sand;
            var o = FirstFightOutcome(run);
            Assert.False(o.Won);
            Assert.Equal(1, o.EnemiesKilled);
            Assert.Equal(0, o.KillPayout);
            Assert.Equal(0, o.WinBonus);
            Assert.Equal(0, o.SandEarned);
            Assert.Equal(before, run.State.Sand);
            Assert.Equal(RunPhase.Defeated, run.State.Phase);
        }

        [Fact]
        public void CollapsingPaysMoreOnAFullClearWin()
        {
            var safe = FirstFightOutcome(NewRun(seed: 11));
            var greedy = NewRun(seed: 11);
            while (greedy.CurrentNodeKind != NodeKind.Fight) ResolveCurrent(greedy);
            var g = greedy.ResolveFight(FightTier.Collapsing, Kit.AutoPlace(greedy));
            Assert.True(safe.Won && g.Won);
            Assert.True(g.SandEarned > safe.SandEarned);
        }

        [Fact]
        public void InterludeTreasuryPaysSandAndUnlocksCapacity()
        {
            var run = NewRun();
            var cfg = new RunConfig();
            while (run.CurrentNodeKind != NodeKind.Event) ResolveCurrent(run);
            int before = run.State.Sand;
            var reward = run.ResolveInterlude(InterludePath.Treasury);
            Assert.Equal(cfg.InterludeTreasurySand, reward.Sand);
            Assert.Equal(before + reward.Sand, run.State.Sand);
            Assert.Equal(4, run.State.UnlockedFieldSlots);
            Assert.True(run.SlotOfferOpen);
        }

        // ---- Slots (ADR 0006) ------------------------------------------------------

        [Fact]
        public void InterludesUnlockEscalatingSlotsBeforeEachBoss()
        {
            var run = NewRun();
            var cfg = new RunConfig();
            for (int act = 1; act <= 3; act++)
            {
                while (run.CurrentNodeKind != NodeKind.Event)
                    ResolveCurrent(run);
                run.ResolveInterlude(InterludePath.Treasury);
                Assert.Equal(3 + act, run.State.UnlockedFieldSlots);
                Assert.True(run.SlotOfferOpen);
                Assert.Equal(cfg.SlotCosts[act - 1], run.SlotOfferCost);
                int sandBefore = run.State.Sand;
                run.BuySlot();
                Assert.Equal(sandBefore - cfg.SlotCosts[act - 1], run.State.Sand);
                Assert.Equal(3 + act, run.State.FieldSlots);
                Assert.False(run.SlotOfferOpen);

                DriveToBoss(run);
                run.ResolveBoss(Kit.AutoPlace(run));
                if (run.State.Phase == RunPhase.Reward)
                    run.ChooseBossReward(0);
            }
            Assert.Equal(6, run.State.FieldSlots);
        }

        [Fact]
        public void SkippedCapacityCanBeBoughtLaterInSequence()
        {
            var run = NewRun();
            while (run.CurrentNodeKind != NodeKind.Event) ResolveCurrent(run);
            run.ResolveInterlude(InterludePath.Treasury);          // unlock 4, skip purchase
            DriveToBoss(run);
            run.ResolveBoss(Kit.AutoPlace(run));
            run.ChooseBossReward(0);

            while (run.CurrentNodeKind != NodeKind.Event) ResolveCurrent(run);
            run.ResolveInterlude(InterludePath.Treasury);          // cap now 5
            run.State.Sand = 100;
            run.BuySlot();
            Assert.True(run.SlotOfferOpen);                         // fifth remains available
            run.BuySlot();
            Assert.Equal(5, run.State.FieldSlots);
        }

        [Fact]
        public void CapacityIsLockedBeforeTheInterlude()
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
            while (run.CurrentNodeKind != NodeKind.Event) ResolveCurrent(run);
            run.ResolveInterlude(InterludePath.Treasury);
            Assert.True(run.SlotOfferOpen);
            Assert.Throws<InvalidOperationException>(() => run.BuySlot());
        }

        // ---- Placement & roster ----------------------------------------------------

        [Fact]
        public void PlacementIsValidated()
        {
            var run = NewRun();
            while (run.CurrentNodeKind != NodeKind.Fight) ResolveCurrent(run);
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
        public void PlanningAllowsRosterChangesWithinCaps()
        {
            var run = NewRun();
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
            while (run.CurrentNodeKind != NodeKind.Fight) ResolveCurrent(run);
            run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
            int after2 = hero.Earned.Sum(s => s.Kind == StatusKind.AttackUp ? s.Mag : 0);
            Assert.True(after2 > after1);
        }

        [Fact]
        public void DrawnBossFightCountsAsAWin()
        {
            // 1 hero vs 1 identical boss body, adjacent across the midline: the sim's proven
            // mutual-KO draw. Your board wasn't beaten, so the run continues.
            var content = new StubContent { BossPos = Hex.FromRowCol(4, 2) };
            var run = NewRun(content: content, heroes: 1);
            DriveToBoss(run);
            var o = run.ResolveBoss(new List<Hex> { Hex.FromRowCol(3, 2) });
            Assert.Equal(Winner.Draw, o.Battle.Winner);
            Assert.True(o.Won);
            Assert.Equal(1, run.State.BossWins);
            Assert.Equal(0, run.State.BossLosses);
        }

    }
}
