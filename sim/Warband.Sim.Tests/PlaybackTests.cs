using System.Collections.Generic;
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
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Haste, Amount = 400, StatusTicks = 60, Select = new Selector { Kind = SelKind.Self } } },
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
