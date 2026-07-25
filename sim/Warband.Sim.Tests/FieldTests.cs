using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class FieldTests
    {
        [Fact]
        public void HexLineIsConnectedDeterministicAndEndsCorrectly()
        {
            var pairs = new[]
            {
                (Hex.FromRowCol(0, 2), Hex.FromRowCol(4, 2)),
                (Hex.FromRowCol(7, 5), Hex.FromRowCol(0, 0)),
                (new Hex(-3, 2), new Hex(4, -1)),
                (new Hex(0, 0), new Hex(0, 0)),
            };
            foreach (var (a, b) in pairs)
            {
                var line = Hex.Line(a, b);
                Assert.Equal(a, line.First());
                Assert.Equal(b, line.Last());
                Assert.Equal(Hex.Distance(a, b) + 1, line.Count);
                for (int i = 1; i < line.Count; i++)
                    Assert.Equal(1, Hex.Distance(line[i - 1], line[i]));
                Assert.Equal(line, Hex.Line(a, b)); // deterministic
            }
        }

        private static UnitState Rooted(int id, int team, UnitDef def, Hex pos)
        {
            var u = UnitState.Spawn(id, team, def, pos);
            u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1, SourceId = id });
            return u;
        }

        [Fact]
        public void WallBlocksProjectilesCompletely()
        {
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                Rooted(0, 0, archer, Hex.FromRowCol(0, 2)),
                Rooted(1, 1, BattleTests.Pacifist(40), Hex.FromRowCol(4, 2)),
            };
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            var result = new Battle(units, initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }).Run();

            Assert.Contains(result.Events, e => e.Kind == EventKind.AttackBlocked && e.Source == 0);
            // Every point of damage the target ever took came from the storm, never an arrow.
            Assert.All(result.Events.Where(e => e.Kind == EventKind.DamageDealt && e.Target == 1),
                e => Assert.Equal(Cause.Storm, e.Cause));
            Assert.True(result.EndTick > Battle.OvertimeStartTick);
        }

        [Fact]
        public void BlockedShooterAdvancesUntilLineClearsThenFires()
        {
            // Block-then-adapt: a wall on the shot line makes the FIRST shot fizzle (the fizzle
            // is information), then the free-to-move shooter advances instead of whiffing forever;
            // once its line clears it lands a real arrow and the fight resolves.
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(0, 2)),        // free to reposition
                Rooted(1, 1, BattleTests.Pacifist(40), Hex.FromRowCol(4, 2)),
            };
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            var result = new Battle(units, initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }).Run();

            // Exactly one fizzle: it learns, it does not re-whiff.
            Assert.Equal(1, result.Events.Count(e => e.Kind == EventKind.AttackBlocked && e.Source == 0));
            // It advanced off its spawn...
            Assert.Contains(result.Events, e => e.Kind == EventKind.Move && e.Source == 0);
            // ...and once the line cleared, an arrow (not the storm) landed.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Target == 1 && e.Cause == Cause.Attack);
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.True(result.EndTick < Battle.OvertimeStartTick); // killed by arrows, well before the storm
            AssertReconstructs(result);
        }

        [Fact]
        public void ExpiringWallLetsBlockedShooterResumeInPlace()
        {
            // A time-limited wall: the rooted shooter (can't reposition) fizzles once, holds, and
            // the instant the wall expires its line re-traces clear and it resumes firing from the
            // very hex it stood on — the flag clears from wherever it is, no advance required.
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                Rooted(0, 0, archer, Hex.FromRowCol(0, 2)),
                Rooted(1, 1, BattleTests.Pacifist(100), Hex.FromRowCol(4, 2)),
            };
            var wall = new FieldDef { Radius = 0, Ticks = 30, IsWall = true };
            var result = new Battle(units, initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }).Run();

            int expireTick = result.Events.First(e => e.Kind == EventKind.FieldExpired).Tick;
            // One fizzle, all of it before the wall fell; rooted → it neither re-whiffs nor moves.
            Assert.Equal(1, result.Events.Count(e => e.Kind == EventKind.AttackBlocked && e.Source == 0));
            Assert.All(result.Events.Where(e => e.Kind == EventKind.AttackBlocked && e.Source == 0),
                e => Assert.True(e.Tick < expireTick));
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.Move && e.Source == 0);
            // Resumed after the wall came down, from the same hex.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Target == 1
                && e.Cause == Cause.Attack && e.Tick >= expireTick);
            Assert.Equal(Winner.Team0, result.Winner);
            AssertReconstructs(result);
        }

        [Fact]
        public void RetargetClearsBlockedFlagForTheNewTarget()
        {
            // The flag is keyed to the blocked target's id: when T1 dies the rooted shooter
            // re-acquires T2, and the stale T1 flag must not suppress the new (clear-line) shot.
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                Rooted(0, 0, archer, Hex.FromRowCol(0, 2)),
                Rooted(1, 1, BattleTests.Pacifist(10),  Hex.FromRowCol(4, 2)), // T1: nearest, behind the wall
                Rooted(2, 1, BattleTests.Pacifist(100), Hex.FromRowCol(4, 4)), // T2: same range, clear line
            };
            var fields = new (FieldDef, Hex, int)[]
            {
                (new FieldDef { Radius = 0, Ticks = -1, IsWall = true }, Hex.FromRowCol(2, 2), -1),
                // A hazard on T1's hex that kills it on the first pulse — forces the retarget with
                // zero movement, so the test isolates flag invalidation.
                (new FieldDef { Radius = 0, Ticks = -1, PulseAffects = Affects.Enemies,
                                Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 50 } } }, Hex.FromRowCol(4, 2), 0),
            };
            var result = new Battle(units, initialFields: fields).Run();

            Assert.Contains(result.Events, e => e.Kind == EventKind.AttackBlocked && e.Source == 0 && e.Target == 1);
            Assert.Contains(result.Events, e => e.Kind == EventKind.Death && e.Target == 1);
            // The stale T1 flag does NOT gag the T2 shot — a clear-line arrow lands on T2.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Target == 2 && e.Cause == Cause.Attack);
            // T2's line was always clear: it must never be recorded as blocked.
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.AttackBlocked && e.Target == 2);
            AssertReconstructs(result);
        }

        [Fact]
        public void BlockedShooterSidestepsToAFiringAngleInsteadOfFreezing()
        {
            // The wallfort freeze: shooter and target 2 apart with a wall dead on the line between
            // them. The only distance-reducing hex is the wall itself, so "advance until clear"
            // finds no step and the shooter would stand whiffing forever. Firing-angle seek steps
            // it off the line to a hex it can FIRE from — one sidestep, then real arrows.
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var targetPos = Hex.FromRowCol(3, 2);
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(1, 1)),  // free to reposition
                Rooted(1, 1, BattleTests.Pacifist(40), targetPos),   // rooted + passive: the shooter must solve it alone
            };
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            var result = new Battle(units, initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }).Run();

            // Learns once (the fizzle is information), never re-whiffs.
            Assert.Equal(1, result.Events.Count(e => e.Kind == EventKind.AttackBlocked && e.Source == 0));
            var moves = result.Events.Where(e => e.Kind == EventKind.Move && e.Source == 0).ToList();
            Assert.NotEmpty(moves); // it sidestepped
            // Every step is a sidestep to an equal-or-closer hex, never a walk into melee.
            int startDist = Hex.Distance(Hex.FromRowCol(1, 1), targetPos);
            Assert.All(moves, e =>
            {
                int d = Hex.Distance(new Hex(e.Amount, e.Aux), targetPos);
                Assert.True(d >= 2 && d <= startDist, $"move to dist {d} was a melee-close or a backstep");
            });
            // A real arrow lands (not the storm), and it decides the fight before overtime.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Target == 1 && e.Cause == Cause.Attack);
            Assert.Equal(Winner.Team0, result.Winner);
            Assert.True(result.EndTick < Battle.OvertimeStartTick);
            AssertReconstructs(result);
        }

        [Fact]
        public void PocketedShooterStandsInsteadOfOscillating()
        {
            // No firing angle exists: walls on the line AND on the one sidestep hex that would have
            // a clear shot. With nothing closer and nothing clear, the shooter must simply stand —
            // it does not thrash. (The rooted pacifist can never fire back, so the storm resolves it.)
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(1, 1)),  // free to move, but pocketed
                Rooted(1, 1, BattleTests.Pacifist(40), Hex.FromRowCol(3, 2)),
            };
            var fields = new (FieldDef, Hex, int)[]
            {
                (new FieldDef { Radius = 0, Ticks = -1, IsWall = true }, Hex.FromRowCol(2, 2), -1), // on the line
                (new FieldDef { Radius = 0, Ticks = -1, IsWall = true }, Hex.FromRowCol(2, 1), -1), // the only clear-angle hex
            };
            var result = new Battle(units, initialFields: fields).Run();

            Assert.Equal(1, result.Events.Count(e => e.Kind == EventKind.AttackBlocked && e.Source == 0));
            // It stands: zero moves across the whole (well over 20-tick) fight — no oscillation.
            Assert.Empty(result.Events.Where(e => e.Kind == EventKind.Move && e.Source == 0));
            Assert.True(result.EndTick > Battle.OvertimeStartTick); // nobody could shoot; the storm ended it
            // It never lands an arrow (no clear line ever opened).
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack);
            AssertReconstructs(result);
        }

        [Fact]
        public void AngleSeekIsDeterministic()
        {
            // The firing-angle seek adds per-neighbor traces to the decision phase; two runs of the
            // same blocked-shooter fight from the same seed must produce byte-identical event logs.
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            List<UnitState> Build() => new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(1, 1)),
                Rooted(1, 1, BattleTests.Pacifist(40), Hex.FromRowCol(3, 2)),
            };
            var wall = new FieldDef { Radius = 0, Ticks = -1, IsWall = true };
            var a = new Battle(Build(), initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }, seed: 7).Run();
            var b = new Battle(Build(), initialFields: new[] { (wall, Hex.FromRowCol(2, 2), -1) }, seed: 7).Run();

            Assert.Equal(a.Winner, b.Winner);
            Assert.Equal(a.EndTick, b.EndTick);
            Assert.Equal(a.FinalHash, b.FinalHash);
            Assert.Equal(a.Events.Count, b.Events.Count);
            for (int i = 0; i < a.Events.Count; i++)
            {
                var (x, y) = (a.Events[i], b.Events[i]);
                Assert.True(x.Kind == y.Kind && x.Tick == y.Tick && x.Source == y.Source
                    && x.Target == y.Target && x.Amount == y.Amount && x.Aux == y.Aux
                    && x.Cause == y.Cause && x.Depth == y.Depth, $"event {i} diverged");
            }
        }

        [Fact]
        public void BlessingFieldEnhancesCrossingProjectiles()
        {
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 12, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                Rooted(0, 0, archer, Hex.FromRowCol(0, 2)),
                Rooted(1, 1, BattleTests.Pacifist(100), Hex.FromRowCol(4, 2)),
            };
            var blessing = new FieldDef { Radius = 0, Ticks = -1, ProjectileAffects = Affects.Allies, ProjectileBonus = 10 };
            var result = new Battle(units, initialFields: new[] { (blessing, Hex.FromRowCol(2, 2), 0) }).Run();

            var firstShot = result.Events.First(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Attack && e.Source == 0);
            Assert.Equal(22, firstShot.Amount); // 12 base + 10 blessing
        }

        [Fact]
        public void FireFieldIgnitesCrossingProjectiles()
        {
            // Rider: arrows through fire apply the generic DoT — Pyromancer × Sharpshot.
            var archer = new UnitDef { Name = "archer", MaxHp = 60, Attack = 6, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                Rooted(0, 0, archer, Hex.FromRowCol(0, 2)),
                Rooted(1, 1, BattleTests.Pacifist(500), Hex.FromRowCol(4, 2)),
            };
            var fire = new FieldDef
            {
                Radius = 0, Ticks = -1,
                ProjectileRiders = { new EffectDef { Kind = EffectKind.ApplyStatus, Status = StatusKind.Dot, Amount = 3, StatusTicks = 50 } },
            };
            var result = new Battle(units, initialFields: new[] { (fire, Hex.FromRowCol(2, 2), 0) }).Run();

            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 1 && e.Aux == (int)StatusKind.Dot);
            Assert.Contains(result.Events, e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Dot);
        }

        [Fact]
        public void FireFieldPulsesOnOccupants()
        {
            var units = new List<UnitState>
            {
                Rooted(0, 0, BattleTests.Pacifist(200), Hex.FromRowCol(0, 0)),
                Rooted(1, 1, BattleTests.Pacifist(30), Hex.FromRowCol(4, 2)), // standing in fire
            };
            var fire = new FieldDef { Radius = 1, Ticks = -1, Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 6 } } };
            var result = new Battle(units, initialFields: new[] { (fire, Hex.FromRowCol(4, 2), -1) }).Run();

            Assert.Equal(Winner.Team0, result.Winner);
            Assert.True(result.EndTick < Battle.OvertimeStartTick); // the fire killed, not the storm
            Assert.Contains(result.Events, e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Field && e.Target == 1);
        }

        [Fact]
        public void CastCreatesGlyphThatThenPulses()
        {
            var pyro = new UnitDef { Name = "pyro", MaxHp = 300, Attack = 5, AttackInterval = 10, Range = 3, MoveInterval = 5, ManaMax = 20 };
            pyro.Signature.Add(new EffectDef
            {
                Kind = EffectKind.CreateField,
                Field = new FieldDef { Radius = 1, Ticks = 300, Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 5 } }, PulseAffects = Affects.Enemies },
            });
            var units = new List<UnitState>
            {
                Rooted(0, 0, pyro, Hex.FromRowCol(2, 2)),
                Rooted(1, 1, BattleTests.Grunt(hp: 150, atk: 2), Hex.FromRowCol(4, 2)),
            };
            // Rooted pyro at range 3... target at distance 2: in range, shoots and casts.
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e => e.Kind == EventKind.FieldCreated && e.Source == 0);
            Assert.Contains(result.Events, e => e.Kind == EventKind.FieldHex);
            Assert.Contains(result.Events, e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Field && e.Target == 1);
            AssertReconstructs(result);
        }

        [Fact]
        public void FieldBattlesReconstructEveryTick()
        {
            // Wall + blessing + fire pulse all at once, through the fold guardrail.
            var archer = new UnitDef { Name = "archer", MaxHp = 90, Attack = 9, AttackInterval = 10, Range = 4, MoveInterval = 5 };
            var units = new List<UnitState>
            {
                Rooted(0, 0, archer, Hex.FromRowCol(0, 2)),
                Rooted(1, 1, BattleTests.Pacifist(80), Hex.FromRowCol(4, 2)),
                Rooted(2, 1, BattleTests.Pacifist(60), Hex.FromRowCol(4, 4)),
            };
            var fields = new (FieldDef, Hex, int)[]
            {
                (new FieldDef { Radius = 0, Ticks = 200, IsWall = true }, Hex.FromRowCol(2, 2), -1),
                (new FieldDef { Radius = 1, Ticks = 400, Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 4 } } }, Hex.FromRowCol(4, 4), -1),
            };
            var result = new Battle(units, initialFields: fields).Run();
            Assert.Contains(result.Events, e => e.Kind == EventKind.FieldExpired); // wall came down
            AssertReconstructs(result);
        }

        private static void AssertReconstructs(BattleResult result)
        {
            var fold = PlaybackState.From(result.InitialUnits);
            for (int t = 0; t < result.TickViewHashes.Count; t++)
            {
                fold.AdvanceToTick(result.Events, t);
                Assert.True(result.TickViewHashes[t] == fold.ViewHash(), $"fold diverged at tick {t}");
            }
        }
    }
}
