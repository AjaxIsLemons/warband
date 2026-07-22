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
