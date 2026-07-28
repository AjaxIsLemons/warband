using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The routing + engagement laws (2026-07-26). Movement was a greedy hill-climb on straight-line
    /// hex distance — step only to a strictly-closer FREE neighbour — and targeting had no idea
    /// whether the thing it picked could be reached at all. Together those produced two fights'
    /// worth of units doing literally nothing:
    ///
    /// - A unit whose one closing direction held an ally froze there permanently. On a 6-wide board
    ///   with a front line, that is most of the back rank.
    /// - A diver that leapt into a full backline landed with every neighbour occupied, found no
    ///   strictly-closer hex, and stood still for the rest of the fight while the enemies it was
    ///   standing between killed it — because the target it had re-acquired was on the far side of
    ///   the board.
    ///
    /// These pin the replacement. See <see cref="Pathing"/> for the field itself.
    /// </summary>
    public class PathingTests
    {
        private static UnitDef Body(int hp = 2000, int atk = 1, int range = 1, int move = 5) =>
            new UnitDef
            {
                Name = "body", MaxHp = hp, Attack = atk, AttackInterval = 10,
                Range = range, MoveInterval = move,
            };

        private static UnitState At(int id, int team, UnitDef def, int row, int col) =>
            UnitState.Spawn(id, team, def, Hex.FromRowCol(row, col));

        // ---- routing ----

        [Fact]
        public void AUnitBoxedInByItsOwnAlliesRoutesAroundThem()
        {
            // Four bodies in a clump, one enemy ahead. Only the front rank has a strictly-closer
            // hex to step into, so under the old hill-climb the flank body never moved and never
            // swung once in a 1200-tick fight. Every one of them must reach the fight.
            var units = new List<UnitState>
            {
                At(0, 0, Body(), 3, 2),
                At(1, 0, Body(), 2, 2),
                At(2, 0, Body(), 2, 1),
                At(3, 0, Body(), 2, 3),
                At(9, 1, Body(), 5, 2),
            };
            var result = new Battle(units).Run();

            for (int id = 0; id <= 3; id++)
                Assert.True(result.Events.Any(e => e.Kind == EventKind.Attack && e.Source == id),
                    $"unit {id} never swung — it is stuck behind the line");
        }

        [Fact]
        public void ARouteGoesTheLongWayRoundAWall()
        {
            // A wall straight across the lane. Distance-wise every step round it is a step
            // backwards, so a hill-climb can never take one; a route just goes.
            var walker = Body(hp: 400, atk: 6, move: 3);
            var units = new List<UnitState>
            {
                At(0, 0, walker, 0, 2),
                Rooted(1, 1, Body(hp: 60, atk: 0), 4, 2),
            };
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            var fields = new[]
            {
                (wall, Hex.FromRowCol(2, 1), -1), (wall, Hex.FromRowCol(2, 2), -1),
                (wall, Hex.FromRowCol(2, 3), -1), (wall, Hex.FromRowCol(2, 4), -1),
                (wall, Hex.FromRowCol(2, 5), -1), (wall, Hex.FromRowCol(2, 6), -1),
                (wall, Hex.FromRowCol(2, 7), -1),
            };
            var result = new Battle(units, initialFields: fields).Run();

            Assert.Equal(Winner.Team0, result.Winner);
            Assert.True(result.EndTick < Battle.OvertimeStartTick, "it never got there — the storm decided it");
            // It went round the open end (col 0), which is strictly farther from the target first.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.Move && e.Source == 0 && new Hex(e.Amount, e.Aux).Col == 0);
        }

        [Fact]
        public void UnitsWithNowhereLeftToStandSettleInsteadOfOrbiting()
        {
            // One target, more bodies than it has neighbours. The ones that cannot get a slot must
            // STOP — a field that treats "closer" loosely has them circling the scrum forever.
            var units = new List<UnitState> { Rooted(9, 1, Body(hp: 5000, atk: 0), 4, 2) };
            int id = 0;
            for (int col = 0; col < 4; col++)
            {
                units.Add(At(id++, 0, Body(atk: 0), 0, col));
                units.Add(At(id++, 0, Body(atk: 0), 1, col));
            }
            var result = new Battle(units).Run();

            // Nobody can kill anybody, so any movement after the pack has settled is thrash.
            var late = result.Events
                .Where(e => e.Kind == EventKind.MoveStart && e.Tick > 200 && e.Tick < Battle.OvertimeStartTick)
                .ToList();
            Assert.True(late.Count == 0, $"{late.Count} steps taken after the board settled — units are orbiting");
        }

        [Fact]
        public void RoutingIsDeterministic()
        {
            // The field adds a Dijkstra to every decision tick. Two runs of the same crowded fight
            // must still produce byte-identical logs — no dictionary order, no float, no rng.
            List<UnitState> Build()
            {
                var us = new List<UnitState>();
                int id = 0;
                for (int col = 0; col < 5; col++)
                {
                    us.Add(At(id++, 0, Body(hp: 200, atk: 7), 1, col));
                    us.Add(At(id++, 1, Body(hp: 200, atk: 7), 6, col));
                }
                return us;
            }
            var a = new Battle(Build(), seed: 11).Run();
            var b = new Battle(Build(), seed: 11).Run();

            Assert.Equal(a.Winner, b.Winner);
            Assert.Equal(a.EndTick, b.EndTick);
            Assert.Equal(a.FinalHash, b.FinalHash);
            Assert.Equal(a.Events.Count, b.Events.Count);
            Assert.Equal(a.TickViewHashes, b.TickViewHashes);
        }

        // ---- the engagement law ----

        [Fact]
        public void ABoxedInUnitFightsWhatItCanReach()
        {
            // The diver bug, exactly: leap at the farthest enemy, land sealed into the back corner
            // by five bodies, and want a target on the far side of the board. It cannot move, so it
            // must fight the enemies it is standing between rather than pose until it dies.
            var diver = Body(hp: 3000, atk: 5, move: 3);
            diver.TargetPref = TargetPref.LowestHp;
            diver.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.Leap, Select = new Selector { Kind = SelKind.FarthestEnemy } } },
            });

            var units = new List<UnitState>
            {
                At(0, 0, diver, 0, 2),
                At(1, 1, Body(hp: 300, atk: 4), 2, 2),      // weakest, and unreachable from the pocket
                At(2, 1, Body(atk: 4), 7, 1), At(3, 1, Body(atk: 4), 7, 3),
                At(4, 1, Body(atk: 4), 6, 1), At(5, 1, Body(atk: 4), 6, 3),
                At(6, 1, Body(atk: 4), 6, 2),
            };
            var result = new Battle(units).Run();

            var leapTick = result.Events.First(e => e.Kind == EventKind.Leap && e.Source == 0).Tick;
            var swings = result.Events
                .Where(e => e.Kind == EventKind.Attack && e.Source == 0 && e.Tick < Battle.OvertimeStartTick)
                .ToList();
            Assert.True(swings.Count > 20, $"only {swings.Count} swings — the diver posed instead of fighting");
            // And it fought a neighbour, not the thing it could never get to.
            Assert.All(swings, e => Assert.NotEqual(1, e.Target));
            Assert.True(swings[0].Tick - leapTick < 20, "it took too long to start fighting");
        }

        [Fact]
        public void ATauntSurvivesTheEngagementLaw()
        {
            // The one exemption: a taunted unit does not get to "fight what it can reach" instead.
            // Taunter out of reach, an untaunted enemy adjacent — it must still whiff at the taunt.
            var victim = Body(hp: 600, atk: 9);
            var taunter = Body(hp: 600, atk: 0);
            taunter.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.Taunt, Amount = 0, StatusTicks = -1,
                    Select = new Selector { Kind = SelKind.NearestEnemy },
                } },
            });

            var units = new List<UnitState>
            {
                Rooted(0, 0, victim, 3, 2),                  // rooted: it can never close on the taunter
                Rooted(1, 1, taunter, 6, 2),                 // far away, holds the taunt
                Rooted(2, 1, Body(hp: 600, atk: 0), 3, 3),   // adjacent and free to be hit — but is not the taunt
            };
            var result = new Battle(units).Run();

            Assert.DoesNotContain(result.Events,
                e => e.Kind == EventKind.Attack && e.Source == 0 && e.Target == 2);
            Assert.Equal(1, units[0].TargetId);
        }

        [Fact]
        public void ADiverFightsWhatItLandedOn()
        {
            // A leap used to clear TargetId and re-acquire by preference from the landing hex, which
            // makes a Farthest-seeking stalker turn round and walk back over the line it just
            // jumped: from your backline, the farthest enemy is your front. It fights where it lands.
            var stalker = Body(hp: 400, atk: 20, move: 3);
            stalker.TargetPref = TargetPref.Farthest;
            stalker.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.Leap, Select = new Selector { Kind = SelKind.FarthestEnemy } } },
            });

            var units = new List<UnitState>
            {
                At(0, 0, stalker, 0, 2),
                At(1, 1, Body(hp: 500, atk: 3), 4, 2),   // the front line it jumps over
                At(2, 1, Body(hp: 120, atk: 3), 7, 2),   // the backliner it dives
            };
            var result = new Battle(units).Run();

            var first = result.Events.First(e => e.Kind == EventKind.Attack && e.Source == 0);
            Assert.Equal(2, first.Target);
            // It kills what it landed on before the frontliner ever becomes its problem.
            var deaths = result.Events.Where(e => e.Kind == EventKind.Death).Select(e => e.Target).ToList();
            Assert.Equal(2, deaths[0]);
        }

        private static UnitState Rooted(int id, int team, UnitDef def, int row, int col)
        {
            var u = At(id, team, def, row, col);
            u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });
            return u;
        }
    }
}
