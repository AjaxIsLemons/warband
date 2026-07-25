using System.Collections.Generic;
using System.IO;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// THE render-accuracy guardrail (render-contract.md): folding the event log like a
    /// client must reproduce the sim's view-state at EVERY tick. Any mutation that
    /// forgets to emit its absolute post-state fails here and can't ship.
    /// </summary>
    public class PlaybackTests
    {
        private static void AssertLogReconstructsState(BattleResult result)
        {
            var fold = PlaybackState.From(result.InitialUnits);
            for (int t = 0; t < result.TickViewHashes.Count; t++)
            {
                fold.AdvanceToTick(result.Events, t);
                Assert.True(result.TickViewHashes[t] == fold.ViewHash(),
                    $"fold diverged from sim view-state at tick {t}");
            }
        }

        [Fact]
        public void BusyBattleLogReconstructsEveryTick()
        {
            // Kitchen sink: shields, counters, ramps, casts with riders, DoT, regen,
            // timed statuses (expiry path), movement, deaths.
            var tank = BattleTests.Grunt(hp: 220, atk: 8);
            tank.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 60, Select = new Selector { Kind = SelKind.Self } } },
            });
            tank.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When = { new Cond { Kind = CondKind.TargetIsOwner }, new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack } },
                Do = { new EffectDef { Kind = EffectKind.Damage, Amount = 4, Select = new Selector { Kind = SelKind.EventSource } } },
            });

            var ramper = BattleTests.Grunt(hp: 140, atk: 6);
            ramper.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When = { new Cond { Kind = CondKind.SourceIsOwner } },
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.AttackUp, Amount = 2, StatusTicks = -1, Select = new Selector { Kind = SelKind.Self } } },
            });

            var pyro = BattleTests.Grunt(hp: 90, atk: 4);
            pyro.Range = 3;
            pyro.ManaMax = 25;
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 30 });
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Dot, Amount = 4, StatusTicks = 80 });

            var cleric = BattleTests.Grunt(hp: 120, atk: 5);
            cleric.ManaMax = 20;
            cleric.Signature.Add(new EffectDef { Kind = EffectKind.Heal, Amount = 35, Select = new Selector { Kind = SelKind.LowestHpAlly } });
            cleric.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Regen, Amount = 3, StatusTicks = -1, Select = new Selector { Kind = SelKind.Self } } },
            });

            var skirmisher = BattleTests.Grunt(hp: 110, atk: 9);
            skirmisher.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                // Short on purpose: this window only exists to drive the expiry path through the
                // fold, and its holder is the first unit to die (~t60). At 60 ticks the death beat
                // the expiry and the fixture silently stopped covering StatusExpired.
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Haste, Amount = 400, StatusTicks = 40, Select = new Selector { Kind = SelKind.Self } } },
            });

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, tank, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, ramper, Hex.FromRowCol(3, 3)),
                UnitState.Spawn(2, 0, pyro, Hex.FromRowCol(1, 2)),
                UnitState.Spawn(3, 1, cleric, Hex.FromRowCol(6, 3)),
                UnitState.Spawn(4, 1, skirmisher, Hex.FromRowCol(4, 2)),
                UnitState.Spawn(5, 1, BattleTests.Grunt(hp: 130, atk: 7), Hex.FromRowCol(4, 4)),
            };
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e => e.Kind == EventKind.Cast);
            Assert.Contains(result.Events, e => e.Kind == EventKind.StatusExpired); // timed haste ran out
            Assert.Contains(result.Events, e => e.Kind == EventKind.Death);
            AssertLogReconstructsState(result);
        }

        [Fact]
        public void BurnPoolLogReconstructsEveryTick()
        {
            // Burn is the one status that merges into a SINGLE pool per unit and decays
            // silently each pulse. No other guardrail fixture used it, which is how a fold
            // that appends per apply and removes on kind+Mag shipped diverging.
            var pyro = BattleTests.Grunt(hp: 120, atk: 3);
            pyro.Range = 3;
            pyro.ManaMax = 12; // ~2 casts before the pool has drained: stacking AND draining
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Burn, Amount = 5 });

            var units = BattleTests.Duel(pyro, BattleTests.Grunt(hp: 260, atk: 6));
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e => e.Kind == EventKind.StatusApplied && e.Aux == (int)StatusKind.Burn);
            Assert.Contains(result.Events, e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Burn);
            AssertLogReconstructsState(result);
        }

        [Fact]
        public void BurnFoldReplacesThePoolWhileOtherKindsStack()
        {
            var fold = PlaybackState.From(new[] { new PlaybackUnit { Id = 0 } });
            var events = new List<BattleEvent>
            {
                Status(0, EventKind.StatusApplied, StatusKind.Burn, 5),
                Status(0, EventKind.StatusApplied, StatusKind.Burn, 8), // merged pool total, not a second stack
                Status(1, EventKind.StatusApplied, StatusKind.Dot, 3),
                Status(1, EventKind.StatusApplied, StatusKind.Dot, 3),
                Status(2, EventKind.StatusExpired, StatusKind.Burn, 0), // drained: Amount can't identify the pool
            };

            fold.AdvanceToTick(events, 0);
            Assert.Single(fold.Units[0].Statuses);
            Assert.Equal(8, fold.Units[0].Statuses[0].Mag);

            fold.AdvanceToTick(events, 1);
            Assert.Equal(2, fold.Units[0].Statuses.FindAll(s => s.Kind == StatusKind.Dot).Count);

            fold.AdvanceToTick(events, 2);
            Assert.DoesNotContain(fold.Units[0].Statuses, s => s.Kind == StatusKind.Burn);
        }

        private static BattleEvent Status(int tick, EventKind kind, StatusKind status, int amount) =>
            new BattleEvent { Tick = tick, Kind = kind, Target = 0, Amount = amount, Aux = (int)status };

        [Fact]
        public void StatusAppliedCarriesItsDurations()
        {
            var caster = BattleTests.Grunt(hp: 200, atk: 5);
            caster.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do =
                {
                    new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Haste, Amount = 100, StatusTicks = 40, Select = new Selector { Kind = SelKind.Self } },
                    new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.AttackUp, Amount = 2, StatusTicks = -1, Select = new Selector { Kind = SelKind.Self } },
                    new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.NextSwingCrit, StatusSwings = 2, Select = new Selector { Kind = SelKind.Self } },
                },
            });
            var events = new Battle(BattleTests.Duel(caster, BattleTests.Grunt(150))).Run().Events;

            BattleEvent Applied(StatusKind k) =>
                events.Find(e => e.Kind == EventKind.StatusApplied && e.Aux == (int)k);

            Assert.Equal(40, Applied(StatusKind.Haste).Aux2);
            Assert.Equal(-1, Applied(StatusKind.Haste).Aux3);
            Assert.Equal(-1, Applied(StatusKind.AttackUp).Aux2);       // whole fight
            Assert.Equal(-1, Applied(StatusKind.NextSwingCrit).Aux2);  // not on the tick clock
            Assert.Equal(2, Applied(StatusKind.NextSwingCrit).Aux3);
        }

        [Fact]
        public void FoldStampsAnExpiryTickAndLeavesItOutOfTheHash()
        {
            var fold = PlaybackState.From(new[] { new PlaybackUnit { Id = 0 } });
            fold.AdvanceToTick(Timed(5), 5);
            Assert.Equal(45, fold.Units[0].Statuses[0].ExpiryTick); // applied at 5, 40 ticks left
            Assert.Equal(-1, fold.Units[0].Statuses[1].ExpiryTick); // permanent

            // Identical view, countdowns 25 ticks apart: the ring is decoration, so the hash —
            // and with it the replay round-trip check — must not see the difference.
            var later = PlaybackState.From(new[] { new PlaybackUnit { Id = 0 } });
            later.AdvanceToTick(Timed(30), 30);
            Assert.Equal(70, later.Units[0].Statuses[0].ExpiryTick);
            Assert.Equal(fold.ViewHash(), later.ViewHash());
        }

        private static List<BattleEvent> Timed(int tick) => new List<BattleEvent>
        {
            new BattleEvent { Tick = tick, Kind = EventKind.StatusApplied, Target = 0, Amount = 3, Aux = (int)StatusKind.Dot, Aux2 = 40 },
            new BattleEvent { Tick = tick, Kind = EventKind.StatusApplied, Target = 0, Amount = 2, Aux = (int)StatusKind.AttackUp },
        };

        [Fact]
        public void ReplayCarriesStatusExpiryTicks()
        {
            var units = BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt(90));
            units[0].Statuses.Add(new Status { Kind = StatusKind.AttackUp, Mag = 5, TicksLeft = 120, SourceId = 0 });
            var result = new Battle(units).Run();

            var ms = new MemoryStream();
            Replay.Write(ms, result.InitialUnits, result.Events);
            var (initial, _) = Replay.Read(new MemoryStream(ms.ToArray()));

            Assert.Contains(initial[0].Statuses,
                s => s.Kind == StatusKind.AttackUp && s.Mag == 5 && s.ExpiryTick == 120);
        }

        [Fact]
        public void StormBattleLogReconstructsEveryTick()
        {
            var result = new Battle(BattleTests.Duel(BattleTests.Pacifist(120), BattleTests.Pacifist(150))).Run();
            Assert.True(result.EndTick > Battle.OvertimeStartTick);
            AssertLogReconstructsState(result);
        }

        [Fact]
        public void RunScopedSpawnStatusesAreInTheInitialSnapshot()
        {
            var units = BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt(90));
            units[0].Statuses.Add(new Status { Kind = StatusKind.AttackUp, Mag = 5, TicksLeft = -1, SourceId = 0 });
            var result = new Battle(units).Run();
            Assert.Contains(result.InitialUnits[0].Statuses, s => s.Kind == StatusKind.AttackUp && s.Mag == 5);
            AssertLogReconstructsState(result);
        }
    }
}
