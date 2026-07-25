using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The comprehension layer against REAL content: a composed catalog warband versus an
    /// authored encounter. The sim-side unit tests pin the folds; these pin that the two tools
    /// are actually usable at the layer that builds fights — real chassis identity survives into
    /// the chart, and a forecast of an authored encounter reproduces.
    /// </summary>
    public class FightComprehensionTests
    {
        private static readonly Catalog Cat = new Catalog();

        /// <summary>Three real heroes at rank S with Honed gear, facing the Bonded Pair. Rebuilt
        /// from scratch on every call — Battle mutates the bodies it is given.</summary>
        private static List<UnitState> BondedPairFight()
        {
            var pool = Cat.HeroPool(1);
            var units = new List<UnitState>();
            for (int i = 0; i < 3; i++)
            {
                var composed = Loadout.Compose(Cat.Chassis(pool[i]), tier: WeaponTier.Honed,
                                               mastered: true, rankSteps: 3);
                units.Add(Loadout.Spawn(i, 0, composed, Hex.FromRowCol(2, 1 + i)));
            }
            var encounter = Encounters.BondedPair();
            for (int i = 0; i < encounter.Enemies.Count; i++)
                units.Add(UnitState.Spawn(100 + i, 1, encounter.Enemies[i].Def, encounter.Enemies[i].Pos));
            return units;
        }

        [Fact]
        public void SummaryOfARealFightKeepsChassisIdentityAndBalances()
        {
            var result = new Battle(BondedPairFight(), seed: 20260725).Run();
            var summary = FightSummary.Build(result);

            // The chart can label rows with real content keys, not display names.
            foreach (var u in summary.Units.Where(u => u.Team == 0))
                Assert.False(string.IsNullOrEmpty(u.ChassisId));

            // Every point dealt is a point taken, once the storm's share is added back.
            Assert.Equal(summary.Teams.Sum(t => t.DamageTaken),
                         summary.Teams.Sum(t => t.DamageDealt) + summary.UnattributedDamage);

            // One kill credit per death, and every credited kill names a unit in the fight.
            Assert.Equal(summary.Beats.Count, summary.Units.Count(u => u.Died));
            foreach (var beat in summary.Beats.Where(b => b.Killer >= 0))
                Assert.NotNull(summary.Unit(beat.Killer));

            // Shares are a real distribution over a real board.
            foreach (var team in summary.Teams.Where(t => t.DamageDealt > 0))
                Assert.Equal(100.0, summary.Units.Where(u => u.Team == team.Team).Sum(u => u.DamagePctOfTeam), 6);

            Assert.NotEmpty(summary.Report("bonded pair"));
        }

        [Fact]
        public void ForecastOfAnAuthoredEncounterReproduces()
        {
            var a = BattleForecast.Run(BondedPairFight, 20, baseSeed: 20260725);
            var b = BattleForecast.Run(BondedPairFight, 20, baseSeed: 20260725);

            Assert.Equal(a.Team0Wins, b.Team0Wins);
            Assert.Equal(a.Team1Wins, b.Team1Wins);
            Assert.Equal(a.Draws, b.Draws);
            Assert.Equal(a.AvgEndTick, b.AvgEndTick);
            Assert.Equal(a.Units.Select(u => (u.UnitId, u.Survived)), b.Units.Select(u => (u.UnitId, u.Survived)));

            Assert.Equal(20, a.Team0Wins + a.Team1Wins + a.Draws);
            Assert.InRange(a.WinPct(0), 0.0, 100.0);
            Assert.True(a.AvgEndTick > 0 && a.AvgEndTick < Battle.SafetyCapTick);
            Assert.Equal(5, a.Units.Count);                         // 3 heroes + the bonded pair
        }
    }
}
