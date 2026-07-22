using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class FightStatsTests
    {
        [Fact]
        public void ConservationAndAttribution()
        {
            // Shielded tank + caster with DoT rider vs grunts: multiple causes in play.
            var tank = BattleTests.Grunt(hp: 200, atk: 8);
            tank.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 70, Select = new Selector { Kind = SelKind.Self } } },
            });
            var pyro = BattleTests.Grunt(hp: 90, atk: 4);
            pyro.Range = 3;
            pyro.ManaMax = 25;
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.Damage, Amount = 25 });
            pyro.Signature.Add(new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Dot, Amount = 4, StatusTicks = 60 });

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, tank, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, pyro, Hex.FromRowCol(1, 2)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 160, atk: 9), Hex.FromRowCol(4, 2)),
                UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 160, atk: 9), Hex.FromRowCol(4, 3)),
            };
            var result = new Battle(units).Run();
            var stats = FightStats.Compute(result);

            // Conservation: every point dealt is a point taken (circuit's honesty test).
            Assert.Equal(stats.Values.Sum(s => s.DamageDealt), stats.Values.Sum(s => s.DamageTaken));

            Assert.True(stats[1].AbilityDamage > 0);   // pyro nuked
            Assert.True(stats[1].DotDamage > 0);       // pyro's DoT ticked (source-attributed)
            Assert.True(stats[0].ShieldAbsorbed > 0);  // tank's shield ate hits
            Assert.True(stats[1].Casts > 0 && stats[1].FirstCastTick >= 0);
            var winners = result.Winner == Winner.Team0 ? new[] { 0, 1 } : new[] { 2, 3 };
            Assert.True(winners.Sum(id => stats[id].Kills) > 0); // participation credited
        }

        [Fact]
        public void CcUptimeAndBlockedShotsAreCounted()
        {
            var stunner = BattleTests.Grunt(hp: 100);
            stunner.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Stun, Amount = 0, StatusTicks = -1, Select = new Selector { Kind = SelKind.NearestEnemy } } },
            });
            var r1 = new Battle(BattleTests.Duel(stunner, BattleTests.Grunt())).Run();
            var s1 = FightStats.Compute(r1);
            Assert.True(s1[1].CcTicksSuffered >= r1.EndTick - 1); // stunned the whole fight

            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(0, 2)),
                UnitState.Spawn(1, 1, BattleTests.Pacifist(40), Hex.FromRowCol(4, 2)),
            };
            units[0].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = 0 });
            units[1].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = 1 });
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            var r2 = new Battle(units, initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }).Run();
            var s2 = FightStats.Compute(r2);
            Assert.True(s2[0].ShotsBlocked > 0);
        }
    }
}
