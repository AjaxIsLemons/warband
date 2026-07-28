using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The post-fight report's fold. A chart fails in arithmetic, not in pixels — shares that do
    /// not sum, a bar normalised to the wrong denominator, a timeline that divides by a zero-tick
    /// fight — so the numbers are pinned here and Unity is left to draw them.
    ///
    /// Most of these author the event log by hand rather than run a battle: the fold's contract is
    /// with the log, and a hand-written log states the case being tested instead of hoping a
    /// simulated fight happens to contain it.
    /// </summary>
    public class CombatRecapTests
    {
        private static PlaybackUnit U(int id, int team, string name, string chassis = "grunt") =>
            new PlaybackUnit { Id = id, Team = team, Name = name, ChassisId = chassis, MaxHp = 100, Hp = 100 };

        private static BattleEvent Dmg(int tick, int src, int tgt, int amount, Cause cause) =>
            new BattleEvent { Tick = tick, Kind = EventKind.DamageDealt, Source = src, Target = tgt, Amount = amount, Cause = cause };

        private static BattleEvent Died(int tick, int victim, int killer, int overkill = 0) =>
            new BattleEvent { Tick = tick, Kind = EventKind.Death, Source = killer, Target = victim, Amount = overkill };

        private static BattleEvent End(int tick, Winner winner) =>
            new BattleEvent { Tick = tick, Kind = EventKind.End, Amount = (int)winner };

        private static CombatRecap Fold(List<PlaybackUnit> units, List<BattleEvent> log, int team = 0) =>
            CombatRecap.Build(FightSummary.Build(units, log), team);

        [Fact]
        public void RowsCoverTheViewingTeamAloneAndLeadWithTheTopContributor()
        {
            var units = new List<PlaybackUnit> { U(0, 0, "ours-small"), U(1, 0, "ours-big"), U(2, 1, "theirs") };
            var log = new List<BattleEvent>
            {
                Dmg(10, 0, 2, 30, Cause.Attack),
                Dmg(20, 1, 2, 70, Cause.Attack),
                Dmg(30, 2, 0, 999, Cause.Attack),   // the enemy out-damages both of ours
                End(40, Winner.Team0),
            };

            var recap = Fold(units, log);

            Assert.Equal(new[] { "ours-big", "ours-small" }, recap.Rows.Select(r => r.Name));
            Assert.True(recap.Victory);
            Assert.Equal(4.0, recap.Seconds);

            // ...and the same log read from the other side is a different report, not a mirror.
            var theirs = Fold(units, log, team: 1);
            Assert.Equal(new[] { "theirs" }, theirs.Rows.Select(r => r.Name));
            Assert.False(theirs.Victory);
        }

        [Fact]
        public void BarFillNormalisesToTheLeaderWhilePctNormalisesToTheTeam()
        {
            // The two denominators are different on purpose, and conflating them is THE chart bug:
            // 70/30 of the team is 100%/43% of the leader.
            var units = new List<PlaybackUnit> { U(0, 0, "a"), U(1, 0, "b"), U(2, 1, "enemy") };
            var log = new List<BattleEvent>
            {
                Dmg(10, 0, 2, 70, Cause.Attack),
                Dmg(20, 1, 2, 30, Cause.Attack),
                End(30, Winner.Team0),
            };

            var recap = Fold(units, log);

            Assert.Equal(70, recap.Rows[0].PctOfTeam, 3);
            Assert.Equal(30, recap.Rows[1].PctOfTeam, 3);
            Assert.Equal(1.0, recap.Rows[0].BarFill, 3);
            Assert.Equal(30.0 / 70.0, recap.Rows[1].BarFill, 3);
            Assert.Equal(100, recap.Rows.Sum(r => r.PctOfTeam), 3);
        }

        [Fact]
        public void CompositionSumsToTheTeamTotalAndCarriesCausesTheHarnessNeverNames()
        {
            // FightStats splits damage five ways. UnitSummary.ByCause carries every Cause, so
            // Burn and Counter — the causes a compounding build actually lives in — get slices.
            var units = new List<PlaybackUnit> { U(0, 0, "pyro"), U(1, 0, "phalanx"), U(2, 1, "enemy") };
            var log = new List<BattleEvent>
            {
                Dmg(10, 0, 2, 40, Cause.Attack),
                Dmg(11, 0, 2, 25, Cause.Ability),
                Dmg(12, 0, 2, 20, Cause.Burn),
                Dmg(13, 1, 2, 15, Cause.Counter),
                End(20, Winner.Team0),
            };

            var recap = Fold(units, log);

            Assert.Equal(100, recap.CompositionTotal);
            Assert.Equal(new[] { "Attack", "Ability", "Burn", "Counter" },
                recap.Composition.Select(s => s.Name));
            Assert.Equal(new[] { 40, 25, 20, 15 }, recap.Composition.Select(s => s.Amount));
            Assert.Equal(100, recap.Composition.Sum(s => s.Pct), 3);
            Assert.Equal(recap.Rows.Sum(r => r.Damage), recap.CompositionTotal);

            // Zero buckets are absent, not drawn flat: a fight with no Decay shows no Decay.
            Assert.DoesNotContain(recap.Composition, s => s.Cause == Cause.Dot);
            Assert.DoesNotContain(recap.Composition, s => s.Amount == 0);
        }

        [Fact]
        public void TheTimelineSeparatesOurLossesFromTheirsAndNamesTheKiller()
        {
            var units = new List<PlaybackUnit> { U(0, 0, "ours"), U(1, 1, "theirs") };
            var log = new List<BattleEvent>
            {
                Dmg(10, 1, 0, 100, Cause.Ability),
                Died(10, victim: 0, killer: 1, overkill: 12),
                Dmg(50, 0, 1, 100, Cause.Attack),
                Died(50, victim: 1, killer: 0),
                End(100, Winner.Team1),
            };

            var recap = Fold(units, log);

            Assert.Equal(2, recap.Beats.Count);
            Assert.Equal(0.1, recap.Beats[0].At, 3);
            Assert.Equal(0.5, recap.Beats[1].At, 3);

            Assert.True(recap.Beats[0].Friendly);
            Assert.Equal("ours", recap.Beats[0].Victim);
            Assert.Equal("theirs", recap.Beats[0].Killer);
            Assert.Equal("Ability", recap.Beats[0].Cause);
            Assert.Equal(12, recap.Beats[0].Overkill);

            Assert.False(recap.Beats[1].Friendly);
            Assert.Equal(0, recap.Survivors);
            Assert.Equal(1, recap.Losses);
        }

        [Fact]
        public void AStormKillNamesTheStormBecauseNoUnitCanBeCredited()
        {
            // Source = -1 is the ownerless hazard. There is no unit to blame, and the beat must
            // read as something rather than as an empty name next to a dead hero.
            var units = new List<PlaybackUnit> { U(0, 0, "ours"), U(1, 1, "theirs") };
            var log = new List<BattleEvent>
            {
                Dmg(950, -1, 0, 200, Cause.Storm),
                Died(950, victim: 0, killer: -1),
                End(950, Winner.Team1),
            };

            var recap = Fold(units, log);

            var beat = Assert.Single(recap.Beats);
            Assert.Equal("ours", beat.Victim);
            Assert.Equal("Storm", beat.Killer);
            Assert.Equal("Storm", beat.Cause);
        }

        [Fact]
        public void TheWaningAppearsOnTheTrackOnlyOnceTheFightOutlivesIt()
        {
            var units = new List<PlaybackUnit> { U(0, 0, "ours"), U(1, 1, "theirs") };

            var quick = Fold(units, new List<BattleEvent> { End(300, Winner.Team0) });
            Assert.False(quick.ReachedWaning);
            Assert.Equal(-1, quick.WaningAt);

            var dragged = Fold(units, new List<BattleEvent> { End(1200, Winner.Team0) });
            Assert.True(dragged.ReachedWaning);
            Assert.Equal(Battle.OvertimeStartTick / 1200.0, dragged.WaningAt, 3);

            // A fight ending exactly ON the threshold already took a storm tick — Battle fires it
            // at `_tick >= OvertimeStartTick` — so the report must not deny the Waning happened.
            // The marker lands on the end cap.
            var onTheLine = Fold(units, new List<BattleEvent> { End(Battle.OvertimeStartTick, Winner.Team0) });
            Assert.True(onTheLine.ReachedWaning);
            Assert.Equal(1.0, onTheLine.WaningAt, 3);
        }

        [Fact]
        public void AFightThatEndsBeforeAnythingHappensDrawsEmptyBarsNotNaN()
        {
            // Every chart binds unguarded, so the degenerate fight must produce finite zeroes:
            // a zero-tick span would otherwise divide by zero and a zero-damage team would
            // produce NaN shares.
            var units = new List<PlaybackUnit> { U(0, 0, "ours"), U(1, 1, "theirs") };
            var recap = Fold(units, new List<BattleEvent>
            {
                Died(0, victim: 0, killer: -1),
                End(0, Winner.Team1),
            });

            Assert.Equal(0, recap.EndTick);
            Assert.Empty(recap.Composition);
            Assert.Equal(0, recap.CompositionTotal);
            foreach (var row in recap.Rows)
            {
                Assert.Equal(0, row.BarFill);
                Assert.Equal(0, row.PctOfTeam);
            }
            Assert.Equal(0, Assert.Single(recap.Beats).At);
            Assert.False(recap.ReachedWaning);
        }

        [Fact]
        public void FoldsARealFightWithoutContradictingItsOwnSummary()
        {
            // The hand-authored cases pin the arithmetic; this one pins that the arithmetic is
            // being applied to a real log, and that recap and summary never disagree.
            var result = new Battle(BattleTests.Duel(BattleTests.Grunt(), BattleTests.Pacifist(25))).Run();
            var summary = FightSummary.Build(result);
            var recap = CombatRecap.Build(result, team: 0);

            Assert.Equal(summary.EndTick, recap.EndTick);
            Assert.True(recap.Victory);
            var row = Assert.Single(recap.Rows);
            Assert.Equal(summary.Unit(0)!.DamageDealt, row.Damage);
            Assert.Equal(1.0, row.BarFill, 3);
            Assert.Equal(100, row.PctOfTeam, 3);
            Assert.False(row.Died);
            Assert.Equal(1, recap.Survivors);
            Assert.Equal(0, recap.Losses);

            var attack = Assert.Single(recap.Composition);
            Assert.Equal(Cause.Attack, attack.Cause);
            Assert.Equal(row.Damage, attack.Amount);

            var beat = Assert.Single(recap.Beats);   // the enemy's death, not ours
            Assert.False(beat.Friendly);
        }
    }
}
