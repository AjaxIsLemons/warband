using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The win-probability re-sim. A forecast is only worth showing a player if it is
    /// REPRODUCIBLE (same board, same number, forever) and HONEST (a hopeless board reads
    /// hopeless, a coin-flip board reads like a coin flip). These pin both, plus the seam
    /// itself: one trial of a forecast must be the exact battle its derived seed names.
    /// </summary>
    public class BattleForecastTests
    {
        /// <summary>Crit-decided duel: 50% crits vs a slightly harder hitter, tuned so the dice
        /// genuinely pick the winner (all three outcomes occur across seeds).</summary>
        private static List<UnitState> CoinFlip()
        {
            var critter = BattleTests.Grunt(hp: 100, atk: 10);
            critter.CritChance = 50;
            critter.CritMultFp = 2000;
            return BattleTests.Duel(critter, BattleTests.Grunt(hp: 100, atk: 15));
        }

        private static List<UnitState> Hopeless() => new List<UnitState>
        {
            UnitState.Spawn(0, 0, BattleTests.Pacifist(10), Hex.FromRowCol(3, 2)),
            UnitState.Spawn(1, 1, BattleTests.Grunt(), Hex.FromRowCol(4, 2)),
            UnitState.Spawn(2, 1, BattleTests.Grunt(), Hex.FromRowCol(4, 3)),
            UnitState.Spawn(3, 1, BattleTests.Grunt(), Hex.FromRowCol(5, 2)),
        };

        [Fact]
        public void SameInputsGiveAnIdenticalForecast()
        {
            var a = BattleForecast.Run(CoinFlip, 60, baseSeed: 9);
            var b = BattleForecast.Run(CoinFlip, 60, baseSeed: 9);

            Assert.Equal(a.Team0Wins, b.Team0Wins);
            Assert.Equal(a.Team1Wins, b.Team1Wins);
            Assert.Equal(a.Draws, b.Draws);
            Assert.Equal(a.AvgEndTick, b.AvgEndTick);
            Assert.Equal(a.Units.Select(u => (u.UnitId, u.Team, u.Survived)),
                         b.Units.Select(u => (u.UnitId, u.Team, u.Survived)));
        }

        [Fact]
        public void TrialSeedsAreDerivedAndDistinct()
        {
            var seeds = Enumerable.Range(0, 64).Select(i => BattleForecast.SeedFor(7, i)).ToList();
            Assert.Equal(seeds.Count, seeds.Distinct().Count());
            Assert.DoesNotContain(7UL, seeds);                      // derived, never the base seed itself
            Assert.Equal(seeds, Enumerable.Range(0, 64).Select(i => BattleForecast.SeedFor(7, i)));
            Assert.NotEqual(seeds[0], BattleForecast.SeedFor(8, 0)); // a different board's base forks the sample
        }

        [Fact]
        public void OneTrialIsExactlyTheBattleItsSeedNames()
        {
            // The seam: forecasting must not be a different code path from fighting.
            var forecast = BattleForecast.Run(CoinFlip, 1, baseSeed: 4242);
            var direct = new Battle(CoinFlip(), seed: BattleForecast.SeedFor(4242, 0)).Run();

            Assert.Equal(direct.EndTick, forecast.AvgEndTick);
            Assert.Equal(1, forecast.Wins((int)direct.Winner));
        }

        [Fact]
        public void HopelessBoardForecastsAtZero()
        {
            var forecast = BattleForecast.Run(Hopeless, 40, baseSeed: 5);

            Assert.Equal(0, forecast.Team0Wins);
            Assert.Equal(0, forecast.Draws);
            Assert.Equal(40, forecast.Team1Wins);
            Assert.Equal(0.0, forecast.WinPct(0));
            Assert.Equal(0.0, forecast.NotBeatenPct(0));   // not even a draw to hide behind
            Assert.Equal(100.0, forecast.WinPct(1));

            Assert.Equal(0.0, forecast.Unit(0)!.SurvivalPct);
            foreach (var u in forecast.Units.Where(u => u.Team == 1))
                Assert.Equal(100.0, u.SurvivalPct);
        }

        [Fact]
        public void PerfectMirrorForecastsAsAllDraws()
        {
            // Identical adjacent grunts trade simultaneously — never a winner, on any seed.
            var forecast = BattleForecast.Run(() => BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt()),
                                              20, baseSeed: 11);

            Assert.Equal(20, forecast.Draws);
            Assert.Equal(0.0, forecast.WinPct(0));
            Assert.Equal(100.0, forecast.NotBeatenPct(0));   // a draw is not a loss (run-layer law)
            Assert.All(forecast.Units, u => Assert.Equal(0.0, u.SurvivalPct));
        }

        [Fact]
        public void CritDecidedBoardForecastsBetweenTheExtremes()
        {
            var forecast = BattleForecast.Run(CoinFlip, 60, baseSeed: 9);

            Assert.Equal(60, forecast.Team0Wins + forecast.Team1Wins + forecast.Draws);
            Assert.True(forecast.Team0Wins > 0, "the underdog must win on SOME seed");
            Assert.True(forecast.Team1Wins > 0, "the favorite must lose on SOME seed");
            Assert.True(forecast.Draws > 0, "mutual KOs are reachable here too");
            Assert.InRange(forecast.WinPct(0), 0.01, 99.99);
            Assert.True(forecast.NotBeatenPct(0) > forecast.WinPct(0)); // draws move the run-facing number
            Assert.True(forecast.AvgEndTick > 0);

            // Per-unit survival is a real distribution, not a 0/100 flag.
            Assert.InRange(forecast.Unit(0)!.SurvivalPct, 0.01, 99.99);
        }

        [Fact]
        public void TeamTriggersCarryIntoEveryTrial()
        {
            // A mirror is always a draw (both sides trade to zero). Hand team 1 a banner-shaped
            // team trigger and it survives the trade instead — on every trial, or the triggers
            // were dropped after the first re-sim.
            var banner = new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.GrantShield, Amount = 50, Select = new Selector { Kind = SelKind.Self } } },
            };
            List<UnitState> Mirror() => BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt());

            var buffed = BattleForecast.Run(Mirror, 5, baseSeed: 3, teamTriggers: new[] { (1, banner) });
            Assert.Equal(5, buffed.Team1Wins);
            Assert.Equal(0, buffed.Draws);
            Assert.Equal(100.0, buffed.Unit(1)!.SurvivalPct);
            Assert.Equal(0.0, buffed.Unit(0)!.SurvivalPct);

            Assert.Equal(5, BattleForecast.Run(Mirror, 5, baseSeed: 3).Draws);   // same board, no banner
        }

        [Fact]
        public void InitialFieldsCarryIntoEveryTrial()
        {
            // A wall between two rooted archers blocks every shot, every trial — proof the
            // non-unit fight inputs are not dropped after the first re-sim.
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            List<UnitState> Rooted()
            {
                var units = BattleTests.Duel(BattleTests.Grunt(hp: 60), BattleTests.Grunt(hp: 60));
                units[0].Pos = Hex.FromRowCol(0, 2);
                units[1].Pos = Hex.FromRowCol(4, 2);
                foreach (var u in units)
                {
                    u.Def.Range = 4;
                    u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = u.Id });
                }
                return units;
            }

            var blocked = BattleForecast.Run(Rooted, 3, baseSeed: 2,
                initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) });
            var open = BattleForecast.Run(Rooted, 3, baseSeed: 2);

            Assert.Equal(3, blocked.Draws);                        // storm ends a stalemate
            Assert.True(blocked.AvgEndTick > open.AvgEndTick);     // …far later than a real trade
        }

        [Fact]
        public void ForecastNeedsAtLeastOneTrial()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BattleForecast.Run(CoinFlip, 0));
            Assert.Throws<ArgumentNullException>(() => BattleForecast.Run(null!, 4));
        }
    }
}
