using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class CritBoundsTests
    {
        [Fact]
        public void CritIsSeededDeterministicAndMultiplies()
        {
            UnitDef Critter()
            {
                var d = BattleTests.Grunt(hp: 400, atk: 10);
                d.CritChance = 50;
                d.CritMultFp = 2000;
                return d;
            }
            var r1 = new Battle(BattleTests.Duel(Critter(), BattleTests.Grunt(hp: 400, atk: 5)), seed: 42).Run();
            var r2 = new Battle(BattleTests.Duel(Critter(), BattleTests.Grunt(hp: 400, atk: 5)), seed: 42).Run();
            Assert.Equal(r1.FinalHash, r2.FinalHash);
            Assert.Equal(r1.EndTick, r2.EndTick);

            var swings = r1.Events.Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack).ToList();
            Assert.Contains(swings, e => e.Crit && e.Amount == 20);
            Assert.Contains(swings, e => !e.Crit && e.Amount == 10);

            // Different seed → different crit pattern (2^-N odds of collision).
            var r3 = new Battle(BattleTests.Duel(Critter(), BattleTests.Grunt(hp: 400, atk: 5)), seed: 1337).Run();
            string Pattern(BattleResult r) => string.Concat(r.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack)
                .Select(e => e.Crit ? '1' : '0'));
            Assert.NotEqual(Pattern(r1), Pattern(r3));
        }

        [Fact]
        public void OnCritPassiveFires()
        {
            var d = BattleTests.Grunt(hp: 400, atk: 10);
            d.CritChance = 50;
            d.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When = { new Cond { Kind = CondKind.SourceIsOwner }, new Cond { Kind = CondKind.IsCrit } },
                Do = { new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.AttackUp, Amount = 3, StatusTicks = -1,
                    Select = new Selector { Kind = SelKind.Self },
                } },
            });
            var result = new Battle(BattleTests.Duel(d, BattleTests.Grunt(hp: 400, atk: 5)), seed: 7).Run();
            int crits = result.Events.Count(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Crit);
            int stacks = result.Events.Count(e => e.Kind == EventKind.StatusApplied && e.Target == 0 && e.Aux == (int)StatusKind.AttackUp);
            Assert.True(crits > 0);
            Assert.Equal(crits, stacks); // exactly one stack per crit
        }

        [Fact]
        public void CritlessMirrorStillDrawsRegardlessOfSeed()
        {
            // No crit chance = no RNG consumption: the mirror guarantee survives seeding.
            var a = new Battle(BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt()), seed: 1).Run();
            var b = new Battle(BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt()), seed: 999).Run();
            Assert.Equal(Winner.Draw, a.Winner);
            Assert.Equal(a.FinalHash, b.FinalHash);
        }

        [Fact]
        public void UnitsNeverLeaveTheBoard()
        {
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, BattleTests.Grunt(), Hex.FromRowCol(0, 0)),
                UnitState.Spawn(1, 0, BattleTests.Grunt(), Hex.FromRowCol(0, 5)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 90), Hex.FromRowCol(7, 0)),
                UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 90), Hex.FromRowCol(7, 5)),
            };
            var result = new Battle(units).Run();
            foreach (var e in result.Events.Where(e => e.Kind == EventKind.Move))
            {
                var h = new Hex(e.Amount, e.Aux);
                Assert.True(Battle.InBounds(h), $"unit {e.Source} stepped off-board to {h}");
            }
        }
    }
}
