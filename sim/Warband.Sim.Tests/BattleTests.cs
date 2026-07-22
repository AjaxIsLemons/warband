using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class BattleTests
    {
        private static UnitDef Grunt(int hp = 100, int atk = 10) => new UnitDef
        {
            Name = "grunt", MaxHp = hp, Attack = atk, AttackInterval = 10,
            Range = 1, MoveInterval = 5,
        };

        private static UnitDef Pacifist(int hp) => new UnitDef
        {
            Name = "pacifist", MaxHp = hp, Attack = 0, AttackInterval = 10,
            Range = 1, MoveInterval = 5,
        };

        private static List<UnitState> Duel(UnitDef a, UnitDef b) => new List<UnitState>
        {
            UnitState.Spawn(0, 0, a, Hex.FromRowCol(3, 2)),
            UnitState.Spawn(1, 1, b, Hex.FromRowCol(4, 2)), // adjacent across the midline
        };

        [Fact]
        public void SameSetupSameOutcome()
        {
            var r1 = new Battle(Duel(Grunt(), Grunt(90))).Run();
            var r2 = new Battle(Duel(Grunt(), Grunt(90))).Run();
            Assert.Equal(r1.Winner, r2.Winner);
            Assert.Equal(r1.EndTick, r2.EndTick);
            Assert.Equal(r1.FinalHash, r2.FinalHash);
        }

        [Fact]
        public void InputOrderDoesNotMatter()
        {
            var forward = Duel(Grunt(), Grunt(90));
            var reversed = Duel(Grunt(), Grunt(90));
            reversed.Reverse();
            var r1 = new Battle(forward).Run();
            var r2 = new Battle(reversed).Run();
            Assert.Equal(r1.Winner, r2.Winner);
            Assert.Equal(r1.FinalHash, r2.FinalHash);
        }

        [Fact]
        public void PerfectMirrorIsMutualKoDraw()
        {
            // Identical adjacent units swing simultaneously (frozen-read/buffer/apply):
            // both must die on the same tick. Iteration order must never pick a winner.
            var result = new Battle(Duel(Grunt(), Grunt())).Run();
            Assert.Equal(Winner.Draw, result.Winner);
        }

        [Fact]
        public void StrongerUnitWins()
        {
            var result = new Battle(Duel(Grunt(hp: 100), Grunt(hp: 90))).Run();
            Assert.Equal(Winner.Team0, result.Winner);
        }

        [Fact]
        public void MeleeWalksAcrossTheBoardToFight()
        {
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, Grunt(), Hex.FromRowCol(0, 0)),
                UnitState.Spawn(1, 1, Grunt(90), Hex.FromRowCol(7, 5)),
            };
            var result = new Battle(units).Run();
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.Contains(result.Events, e => e.Type == EventType.Move);
        }

        [Fact]
        public void StormResolvesPacifistStalemate()
        {
            // Nobody can deal damage: without the storm this never ends (the beltwars
            // stalemate finding). The storm must produce a Draw after overtime starts.
            var result = new Battle(Duel(Pacifist(100), Pacifist(100))).Run();
            Assert.Equal(Winner.Draw, result.Winner);
            Assert.True(result.EndTick > Battle.OvertimeStartTick);
        }

        [Fact]
        public void StormPicksTheTougherPacifist()
        {
            var result = new Battle(Duel(Pacifist(200), Pacifist(100))).Run();
            Assert.Equal(Winner.Team0, result.Winner);
        }

        [Fact]
        public void FullManaCastsAndResets()
        {
            var caster = new UnitDef
            {
                Name = "caster", MaxHp = 300, Attack = 5, AttackInterval = 10,
                Range = 1, MoveInterval = 5, ManaMax = 30, CastDamage = 50,
            };
            var result = new Battle(Duel(caster, Grunt(hp: 500, atk: 5))).Run();
            Assert.Contains(result.Events, e => e.Type == EventType.Cast && e.Actor == 0 && e.Value == 50);
        }

        [Fact]
        public void RangedKitesNothingButStillShootsFromRange()
        {
            var archer = new UnitDef
            {
                Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10,
                Range = 4, MoveInterval = 5,
            };
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(0, 2)),
                UnitState.Spawn(1, 1, Grunt(hp: 40), Hex.FromRowCol(4, 2)),
            };
            var result = new Battle(units).Run();
            Assert.Equal(Winner.Team0, result.Winner);
            // Archer opened fire without ever moving: distance 4 = already in range.
            Assert.DoesNotContain(result.Events, e => e.Type == EventType.Move && e.Actor == 0);
        }

        [Fact]
        public void BattleEmitsDeathsAndEnd()
        {
            var result = new Battle(Duel(Grunt(), Grunt(90))).Run();
            Assert.Contains(result.Events, e => e.Type == EventType.Death && e.Actor == 1);
            Assert.Equal(EventType.End, result.Events.Last().Type);
        }
    }
}
