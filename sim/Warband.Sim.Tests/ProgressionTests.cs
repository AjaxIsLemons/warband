using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>Run-scoped bonuses: fold the log → bake permanent statuses → next fight.</summary>
    public class ProgressionTests
    {
        [Fact]
        public void KillParticipationCountsAssistsNotBystanding()
        {
            // Two separated 1v1 lanes: hero kills its own opponent; the ally solo-kills
            // the other. Hero participated in exactly ONE kill.
            var hero = BattleTests.Grunt(hp: 200, atk: 20);
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, hero, Hex.FromRowCol(3, 0)),
                UnitState.Spawn(1, 0, BattleTests.Grunt(hp: 200, atk: 30), Hex.FromRowCol(3, 5)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 60, atk: 5), Hex.FromRowCol(4, 0)),
                UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 60, atk: 5), Hex.FromRowCol(4, 5)),
            };
            var result = new Battle(units).Run();
            Assert.Equal(Winner.Team0, result.Winner);

            var bonuses = new[]
            {
                new RunBonus { Per = GrowthMetric.KillsParticipated, Grant = StatusKind.AttackUp, Mag = 5 },
                new RunBonus { Per = GrowthMetric.DamageDealt, Threshold = 50, Grant = StatusKind.Haste, Mag = 100 },
            };
            var earned = ProgressionFold.Earned(result.Events, heroId: 0, bonuses);

            var attackUp = earned.Single(s => s.Kind == StatusKind.AttackUp);
            Assert.Equal(5, attackUp.Mag);          // 1 kill participated, not 2
            var haste = earned.Single(s => s.Kind == StatusKind.Haste);
            Assert.Equal(100, haste.Mag);           // 60 damage dealt = one 50-threshold
        }

        [Fact]
        public void EarnedBonusesRideIntoTheNextFight()
        {
            // Spawn the "next fight" with the earned permanent status pre-applied:
            // first swing already carries the run-scoped bonus.
            var units = BattleTests.Duel(BattleTests.Grunt(hp: 100, atk: 10), BattleTests.Grunt(hp: 100, atk: 10));
            units[0].Statuses.Add(new Status { Kind = StatusKind.AttackUp, Mag = 5, TicksLeft = -1, SourceId = 0 });

            var result = new Battle(units).Run();
            var firstSwing = result.Events.First(e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack);
            Assert.Equal(15, firstSwing.Amount);
            Assert.Equal(Winner.Team0, result.Winner); // the run-earned edge decides the mirror
        }
    }
}
