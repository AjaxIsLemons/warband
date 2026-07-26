using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The per-unit behavior layer (2026-07-25 systems review §1). Before this, every unit in the
    /// game shared one brain: acquire the nearest enemy, walk at it until in range, stop — at an
    /// identical move speed, because no chassis ever set MoveInterval. Range and heal-autos were
    /// the only things that made two units behave differently. These tests pin the three hooks that
    /// changed that, plus the weapon cadence axis (§2) and signature patching (§4).
    /// </summary>
    public class UnitBehaviorTests
    {
        private static UnitDef Fighter(int hp = 100, int atk = 10, int range = 1) => new UnitDef
        {
            Name = "fighter", MaxHp = hp, Attack = atk, AttackInterval = 10,
            Range = range, MoveInterval = 5,
        };

        private static UnitState At(int id, int team, UnitDef def, int row, int col) =>
            UnitState.Spawn(id, team, def, Hex.FromRowCol(row, col));

        // ---- TargetPref: kits override the acquire rule (ADR 0013's long-promised hook) ----

        /// <summary>Three enemies, deliberately arranged so nearest / farthest / weakest are three
        /// DIFFERENT units — otherwise a preference test passes by coincidence. The hunter has NO
        /// reach (and no attack): pure acquisition, with nothing for the engagement law to divert
        /// it to. That law is pinned separately, in PathingTests.</summary>
        private static int AcquiredWith(TargetPref pref)
        {
            var hunter = Fighter(range: 0);
            hunter.TargetPref = pref;
            hunter.Attack = 0;                       // never kills anyone: acquisition only

            var units = new List<UnitState>
            {
                At(0, 0, hunter, 0, 0),
                At(1, 1, Fighter(hp: 100), 1, 0),    // nearest, healthiest
                At(2, 1, Fighter(hp: 40), 3, 0),     // middle distance, weakest
                At(3, 1, Fighter(hp: 60), 6, 0),     // farthest
            };
            foreach (var u in units) u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });

            var battle = new Battle(units);
            battle.Run();
            return units[0].TargetId;
        }

        [Fact]
        public void NearestIsStillTheDefault() => Assert.Equal(1, AcquiredWith(TargetPref.Nearest));

        [Fact]
        public void FarthestReachesPastTheFront() => Assert.Equal(3, AcquiredWith(TargetPref.Farthest));

        [Fact]
        public void LowestHpPicksTheWeakest() => Assert.Equal(2, AcquiredWith(TargetPref.LowestHp));

        [Fact]
        public void HighestHpPicksTheBiggest() => Assert.Equal(1, AcquiredWith(TargetPref.HighestHp));

        /// <summary>Determinism law: equal candidates fall to the lowest id, never to iteration
        /// order or an rng draw. Two enemies at identical distance AND identical HP is the only
        /// case where a preference can be ambiguous, so it is the case worth pinning.</summary>
        [Fact]
        public void TiesFallToTheLowestId()
        {
            foreach (var pref in new[] { TargetPref.Nearest, TargetPref.Farthest,
                                         TargetPref.LowestHp, TargetPref.HighestHp })
            {
                var hunter = Fighter();
                hunter.TargetPref = pref;
                hunter.Attack = 0;
                var units = new List<UnitState>
                {
                    At(0, 0, hunter, 3, 2),
                    At(1, 1, Fighter(hp: 100), 3, 1),
                    At(2, 1, Fighter(hp: 100), 3, 3),   // same distance, same HP
                };
                foreach (var u in units) u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });
                new Battle(units).Run();
                Assert.Equal(1, units[0].TargetId);
            }
        }

        /// <summary>A preference decides who you ACQUIRE, not who you keep: ADR 0013's stickiness
        /// still owns re-acquisition, so a LowestHp unit does not re-aim the instant someone else
        /// drops below its current victim. That is what keeps the board predictable to place into.
        /// </summary>
        [Fact]
        public void PreferenceDoesNotBreakStickiness()
        {
            var hunter = Fighter(range: 4);
            hunter.TargetPref = TargetPref.LowestHp;
            hunter.Attack = 1;

            var units = new List<UnitState>
            {
                At(0, 0, hunter, 0, 0),
                At(1, 1, Fighter(hp: 60), 2, 0),    // starts weakest → acquired
                At(2, 1, Fighter(hp: 200), 3, 0),
            };
            foreach (var u in units) u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });
            units[2].Hp = 5;                        // someone else is now far weaker

            new Battle(units).Run();
            Assert.Equal(1, units[0].TargetId);     // still on the original victim
        }

        // ---- Standoff: the ranged unit that defends its distance ----

        [Fact]
        public void StandoffGivesGroundWhenTheTargetClosesInside()
        {
            var archer = Fighter(hp: 400, atk: 0, range: 4);
            archer.Standoff = 4;
            // Mid-board on purpose: at a corner there is nowhere to give ground to, and the test
            // would pass or fail on geometry rather than on the rule.
            var units = new List<UnitState>
            {
                At(0, 0, archer, 4, 2),
                At(1, 1, Fighter(hp: 400, atk: 0), 6, 2),   // distance 2 — inside standoff
            };
            units[1].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });

            new Battle(units).Run();
            Assert.Equal(4, Hex.Distance(units[0].Pos, units[1].Pos));  // backed off to exactly its distance
        }

        /// <summary>It never retreats out of its own fight: every step must leave the target inside
        /// weapon range, so a standoff shorter than reach still ends with a live firing solution.
        /// </summary>
        [Fact]
        public void StandoffNeverRetreatsOutOfRange()
        {
            var archer = Fighter(hp: 400, atk: 0, range: 3);
            archer.Standoff = 3;
            var units = new List<UnitState>
            {
                At(0, 0, archer, 4, 2),
                At(1, 1, Fighter(hp: 400, atk: 0), 5, 2),
            };
            units[1].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });

            new Battle(units).Run();
            Assert.True(Hex.Distance(units[0].Pos, units[1].Pos) <= archer.Range);
        }

        [Fact]
        public void StandoffZeroStandsItsGround()
        {
            var archer = Fighter(hp: 400, atk: 0, range: 4);   // Standoff defaults to 0
            var units = new List<UnitState>
            {
                At(0, 0, archer, 4, 2),                        // room to retreat, and doesn't
                At(1, 1, Fighter(hp: 400, atk: 0), 6, 2),
            };
            units[1].Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });

            new Battle(units).Run();
            Assert.Equal(Hex.FromRowCol(4, 2), units[0].Pos);
        }

        // ---- ManaPerSwing: the weapon's cast-cadence axis ----

        [Fact]
        public void WeaponManaPerSwingDrivesCastCadence()
        {
            int CastsWith(int manaPerSwing)
            {
                var caster = Fighter(hp: 400, atk: 1, range: 4);
                caster.ManaMax = 40;
                caster.ManaPerSwing = manaPerSwing;
                caster.Signature.Add(new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.Mark, Amount = 1,
                    StatusTicks = 5, Select = new Selector { Kind = SelKind.Self },
                });
                var units = new List<UnitState> { At(0, 0, caster, 0, 0), At(1, 1, Fighter(hp: 400, atk: 0), 2, 0) };
                foreach (var u in units) u.Statuses.Add(new Status { Kind = StatusKind.Root, Mag = 0, TicksLeft = -1 });
                return new Battle(units).Run().Events.Count(e => e.Kind == EventKind.Cast && e.Source == 0);
            }

            // Same swing rate, different mana per swing: the heavy-banking weapon casts more often.
            Assert.True(CastsWith(20) > CastsWith(5),
                "a weapon that banks more mana per swing must fire the signature more often");
        }
    }
}
