using System.Collections.Generic;
using System.IO;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The movement law (2026-07-24): a move is a COMMITTED ONE-HEX STEP. The unit departs now
    /// (MoveStart) and arrives MoveInterval ticks later (Move); its position does not change in
    /// between; the destination is reserved the whole way; a committed step always completes.
    ///
    /// These are the rules the renderer's interpolation is entitled to trust. If one of them
    /// breaks, the client is drawing a unit somewhere the sim says it is not.
    /// </summary>
    public class MovementTests
    {
        /// <summary>A walker and a target far enough apart that closing takes several steps.</summary>
        private static List<UnitState> Chase(int moveInterval = 5)
        {
            var walker = BattleTests.Grunt(hp: 400, atk: 1);
            walker.MoveInterval = moveInterval;
            var quarry = BattleTests.Pacifist(400);          // never moves toward us, never kills us
            return new List<UnitState>
            {
                UnitState.Spawn(0, 0, walker, Hex.FromRowCol(0, 2)),
                UnitState.Spawn(1, 1, quarry, Hex.FromRowCol(7, 2)),
            };
        }

        [Fact]
        public void AStepDepartsNowAndArrivesMoveIntervalTicksLater()
        {
            var result = new Battle(Chase()).Run();

            var start = result.Events.First(e => e.Kind == EventKind.MoveStart && e.Source == 0);
            Assert.Equal(0, start.Tick);
            Assert.Equal(5, start.Aux2);                     // the step's declared duration

            var arrive = result.Events.First(e => e.Kind == EventKind.Move && e.Source == 0);
            Assert.Equal(start.Tick + start.Aux2, arrive.Tick);
            Assert.Equal(start.Amount, arrive.Amount);       // …at the hex it committed to
            Assert.Equal(start.Aux, arrive.Aux);
        }

        [Fact]
        public void PositionDoesNotChangeUntilTheStepLands()
        {
            var result = new Battle(Chase()).Run();
            var origin = Hex.FromRowCol(0, 2);
            var dest = new Hex(result.Events.First(e => e.Kind == EventKind.MoveStart && e.Source == 0).Amount,
                               result.Events.First(e => e.Kind == EventKind.MoveStart && e.Source == 0).Aux);

            // Mid-walk the unit is still standing on its origin — and visibly committed to the dest.
            for (int t = 0; t < 5; t++)
            {
                var fold = PlaybackState.From(result.InitialUnits);
                fold.AdvanceToTick(result.Events, t);
                var u = fold.ById(0)!;
                Assert.Equal(origin, u.Pos);
                Assert.True(u.Walking);
                Assert.Equal(dest, u.StepTo);
                Assert.Equal(0, u.StepStart);
                Assert.Equal(5, u.StepEnd);
            }

            var landed = PlaybackState.From(result.InitialUnits);
            landed.AdvanceToTick(result.Events, 5);
            Assert.Equal(dest, landed.ById(0)!.Pos);
        }

        [Fact]
        public void ChasingIsContinuous_ArrivalsAreExactlyMoveIntervalApart()
        {
            // The point of landing and re-departing on the same tick: a pursuit reads as one slide,
            // not a hop every MoveInterval. Cadence must stay 1 hex per MoveInterval, as before.
            var result = new Battle(Chase(moveInterval: 4)).Run();
            var arrivals = result.Events.Where(e => e.Kind == EventKind.Move && e.Source == 0)
                                        .Select(e => e.Tick).ToList();
            Assert.True(arrivals.Count >= 3, $"expected a real chase, got {arrivals.Count} steps");
            for (int i = 1; i < arrivals.Count; i++)
                Assert.Equal(4, arrivals[i] - arrivals[i - 1]);
        }

        [Fact]
        public void EveryDepartureIsFollowedByItsArrival()
        {
            var result = new Battle(Chase()).Run();
            var starts = result.Events.Where(e => e.Kind == EventKind.MoveStart && e.Source == 0).ToList();
            var arrivals = result.Events.Where(e => e.Kind == EventKind.Move && e.Source == 0).ToList();
            Assert.Equal(starts.Count, arrivals.Count);      // nobody survives with a step still in flight
            for (int i = 0; i < starts.Count; i++)
                Assert.Equal(starts[i].Tick + starts[i].Aux2, arrivals[i].Tick);
        }

        [Fact]
        public void NoTwoUnitsEverShareAHexOrAReservation()
        {
            // The reservation rule, checked the only way that matters: fold the whole fight and look
            // at the board every tick. Both the hex a unit stands on and the one it is walking into
            // are exclusive — otherwise two bodies slide through each other on screen.
            var result = new Battle(PlaybackTestsCrowd()).Run();
            var fold = PlaybackState.From(result.InitialUnits);
            for (int t = 0; t <= result.EndTick; t++)
            {
                fold.AdvanceToTick(result.Events, t);
                var claimed = new Dictionary<Hex, string>();
                foreach (var u in fold.Units)
                {
                    if (u.Dead) continue;
                    var claims = u.Walking
                        ? new[] { (u.Pos, "stands on"), (u.StepTo, "walks into") }
                        : new[] { (u.Pos, "stands on") };
                    foreach (var (hex, what) in claims)
                    {
                        if (claimed.TryGetValue(hex, out var other))
                            Assert.Fail($"t{t}: unit {u.Id} {what} {hex}, already claimed by {other}");
                        claimed[hex] = $"unit {u.Id}";
                    }
                }
            }
        }

        [Fact]
        public void ControlGatesStartingAStepNeverFinishingOne()
        {
            // Root landing mid-walk must NOT rubber-band the unit back to its origin: the step it
            // already committed to completes, and only the NEXT one is denied.
            var rooter = BattleTests.Grunt(hp: 300, atk: 1);
            rooter.Range = 8;                 // reaches across the board, so it never needs to move
            rooter.AttackInterval = 10_000;   // exactly one shot, on tick 0
            rooter.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When = { new Cond { Kind = CondKind.SourceIsOwner } },
                Do = { new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.Root, Amount = 1,
                    StatusTicks = -1, Select = new Selector { Kind = SelKind.EventTarget },
                } },
            });
            var walker = BattleTests.Grunt(hp: 400, atk: 1);

            var result = new Battle(new List<UnitState>
            {
                UnitState.Spawn(0, 0, walker, Hex.FromRowCol(0, 2)),
                UnitState.Spawn(1, 1, rooter, Hex.FromRowCol(7, 2)),
            }).Run();

            var starts = result.Events.Where(e => e.Kind == EventKind.MoveStart && e.Source == 0).ToList();
            var arrivals = result.Events.Where(e => e.Kind == EventKind.Move && e.Source == 0).ToList();
            Assert.Single(starts);            // rooted on tick 0 → it never commits a second step
            Assert.Equal(0, starts[0].Tick);
            Assert.Single(arrivals);          // …but the one already committed still lands
            Assert.Equal(5, arrivals[0].Tick);
        }

        [Fact]
        public void ALeapIsATeleport_NoDepartureAndNoWalkLeftBehind()
        {
            // The renderer's whole slide-vs-blink rule is "a Move with no MoveStart is a teleport".
            var shade = BattleTests.Grunt(hp: 120, atk: 12);
            shade.Triggers.Add(new Trigger
            {
                On = EventKind.BattleStart,
                Do = { new EffectDef { Kind = EffectKind.Leap, Select = new Selector { Kind = SelKind.FarthestEnemy } } },
            });

            var result = new Battle(new List<UnitState>
            {
                UnitState.Spawn(0, 0, shade, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, BattleTests.Grunt(hp: 200), Hex.FromRowCol(4, 3)),
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 80, atk: 15), Hex.FromRowCol(7, 2)),
            }).Run();

            var leap = result.Events.First(e => e.Kind == EventKind.Leap && e.Source == 0);
            Assert.DoesNotContain(result.Events,
                e => e.Kind == EventKind.MoveStart && e.Source == 0 && e.Tick <= leap.Tick);

            // Both endpoints ride the event. The renderer arcs the body between them, and by the
            // time it sees this the fold has already applied the landing — so the hex the leaper
            // left is unrecoverable from view state and has to be carried here.
            Assert.Equal(Hex.FromRowCol(3, 2), new Hex(leap.Aux2, leap.Aux3));
            Assert.NotEqual(new Hex(leap.Aux2, leap.Aux3), new Hex(leap.Amount, leap.Aux));

            var fold = PlaybackState.From(result.InitialUnits);
            fold.AdvanceToTick(result.Events, leap.Tick);
            var u = fold.ById(0)!;
            Assert.Equal(new Hex(leap.Amount, leap.Aux), u.Pos);
            Assert.False(u.Walking);          // the landing carries no phantom step
        }

        [Fact]
        public void ACorpseIsNeverWalking()
        {
            // Death cancels the commitment: a dead unit holds no reservation and never arrives,
            // so the renderer can retire its view without stranding a half-finished slide.
            var result = new Battle(PlaybackTestsCrowd()).Run();
            var fold = PlaybackState.From(result.InitialUnits);
            for (int t = 0; t <= result.EndTick; t++)
            {
                fold.AdvanceToTick(result.Events, t);
                foreach (var u in fold.Units)
                    if (u.Dead)
                    {
                        Assert.False(u.Walking, $"t{t}: dead unit {u.Id} still walking");
                        Assert.Equal(u.Pos, u.StepTo);
                    }
            }
        }

        [Fact]
        public void ReplayCarriesTheCommittedStep()
        {
            var result = new Battle(Chase()).Run();
            result.InitialUnits[0].StepTo = Hex.FromRowCol(1, 2);
            result.InitialUnits[0].StepStart = 3;
            result.InitialUnits[0].StepEnd = 8;

            var ms = new MemoryStream();
            Replay.Write(ms, result.InitialUnits, result.Events);
            var (initial, events) = Replay.Read(new MemoryStream(ms.ToArray()));

            Assert.Equal(Hex.FromRowCol(1, 2), initial[0].StepTo);
            Assert.Equal(3, initial[0].StepStart);
            Assert.Equal(8, initial[0].StepEnd);
            Assert.True(initial[0].Walking);
            Assert.Contains(events, e => e.Kind == EventKind.MoveStart && e.Aux2 == 5);
        }

        /// <summary>Six units in a scrum — enough contention that reservations actually get tested.</summary>
        private static List<UnitState> PlaybackTestsCrowd() => new List<UnitState>
        {
            UnitState.Spawn(0, 0, BattleTests.Grunt(hp: 220, atk: 8), Hex.FromRowCol(1, 2)),
            UnitState.Spawn(1, 0, BattleTests.Grunt(hp: 140, atk: 6), Hex.FromRowCol(1, 3)),
            UnitState.Spawn(2, 0, BattleTests.Grunt(hp: 160, atk: 5), Hex.FromRowCol(0, 2)),
            UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 150, atk: 7), Hex.FromRowCol(6, 3)),
            UnitState.Spawn(4, 1, BattleTests.Grunt(hp: 110, atk: 9), Hex.FromRowCol(7, 2)),
            UnitState.Spawn(5, 1, BattleTests.Grunt(hp: 130, atk: 7), Hex.FromRowCol(6, 4)),
        };
    }
}
