using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>Swing-scoped charges (the 4-vote next-N-swings shape), MultiShot,
    /// Frenzied, forced crits, cleave, Nth-swing riders, the directional Counter law,
    /// heal-autos, muster snapshots, gradient StatRules, pierce lines, Recast.</summary>
    public class SwingMechanicsTests
    {
        private static Trigger AtStart(params EffectDef[] effects) => DiveStatusTests.AtStart(effects);

        private static EffectDef Apply(StatusKind kind, int mag, SelKind sel, int ticks = -1, int swings = 0, int range = 0)
            => DiveStatusTests.Apply(kind, mag, sel, ticks, swings, range);

        /// <summary>Ranged pacifist punchbag that stays put.</summary>
        private static UnitDef Dummy(int hp = 500)
        {
            var d = BattleTests.Pacifist(hp);
            d.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            return d;
        }

        // ---- Frenzied: a 4× attack-speed window while charges last ----

        [Fact]
        public void FrenziedMultipliesAttackSpeedThenNormalCadenceResumes()
        {
            var zerk = BattleTests.Grunt(hp: 400, atk: 1);   // AttackInterval 10
            zerk.Triggers.Add(AtStart(Apply(StatusKind.Frenzied, 0, SelKind.Self, ticks: -1, swings: 3)));
            var result = new Battle(BattleTests.Duel(zerk, Dummy())).Run();

            var swings = result.Events
                .Where(e => e.Kind == EventKind.Attack && e.Source == 0)
                .Select(e => e.Tick).Take(5).ToList();
            // +300% speed → interval 10 becomes 2 while charges last. The swing that spends the
            // last charge has already scheduled its successor (NextAttackTick is set before charges
            // decrement), so tick 6 rides the window out; the normal 10-tick interval resumes after.
            Assert.Equal(new List<int> { 0, 2, 4, 6, 16 }, swings);
        }

        /// <summary>
        /// The regression this pins: Frenzy used to bypass AttackInterval outright ("a swing every
        /// tick"), so a window was worth 4 × weapon Damage regardless of how slow the weapon was.
        /// That made the heaviest weapon in the game always the correct Frenzy weapon — musket 64
        /// vs the Berserker's own specialized daggers at 24 — and quietly turned his dagger
        /// specialization into a trap. As a speed multiplier the heavy weapon still hits harder per
        /// swing, but pays for it in ticks.
        /// </summary>
        [Fact]
        public void FrenzyScalesWithAttackSpeedNotWeaponWeight()
        {
            List<int> Window(int interval)
            {
                var u = new UnitDef
                {
                    Name = "zerk", MaxHp = 400, Attack = 1,
                    AttackInterval = interval, Range = 1, MoveInterval = 5,
                };
                u.Triggers.Add(AtStart(Apply(StatusKind.Frenzied, 0, SelKind.Self, ticks: -1, swings: 4)));
                return new Battle(BattleTests.Duel(u, Dummy())).Run().Events
                    .Where(e => e.Kind == EventKind.Attack && e.Source == 0)
                    .Select(e => e.Tick).Take(4).ToList();
            }

            Assert.Equal(new List<int> { 0, 1, 2, 3 }, Window(4));    // light blade: 4 swings, 3 ticks
            Assert.Equal(new List<int> { 0, 4, 8, 12 }, Window(16));  // musket: the same 4 cost 12
        }

        // ---- NextSwingCrit + SwingAmpPct (musket opening shot, sabre mastery) ----

        [Fact]
        public void NextSwingCritForcesExactlyOneCrit()
        {
            var sniper = BattleTests.Grunt(hp: 400, atk: 10); // CritChance 0
            sniper.Triggers.Add(AtStart(Apply(StatusKind.NextSwingCrit, 0, SelKind.Self, ticks: -1, swings: 1)));
            var result = new Battle(BattleTests.Duel(sniper, Dummy())).Run();

            var hits = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack)
                .Take(2).ToList();
            Assert.True(hits[0].Crit);
            Assert.Equal(15, hits[0].Amount);  // 10 × 1.5 default crit mult
            Assert.False(hits[1].Crit);
            Assert.Equal(10, hits[1].Amount);
        }

        [Fact]
        public void SwingAmpDoublesTheOpeningShot()
        {
            var musket = BattleTests.Grunt(hp: 400, atk: 10);
            musket.Triggers.Add(AtStart(Apply(StatusKind.SwingAmpPct, 100, SelKind.Self, ticks: -1, swings: 1)));
            var result = new Battle(BattleTests.Duel(musket, Dummy())).Run();

            var hits = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack)
                .Take(2).ToList();
            Assert.Equal(20, hits[0].Amount);
            Assert.Equal(10, hits[1].Amount);
        }

        // ---- MultiShot (Volleyer): window + ramp, overflow to primary ----

        [Fact]
        public void MultiShotFiresExtrasAtNearestToTarget()
        {
            var volleyer = BattleTests.Grunt(hp: 400, atk: 10);
            volleyer.Range = 4;
            volleyer.Triggers.Add(AtStart(
                Apply(StatusKind.MultiShotRamp, 2, SelKind.Self),
                Apply(StatusKind.MultiShotWindow, 50, SelKind.Self, ticks: -1, swings: 2)));

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, volleyer, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, Dummy(), Hex.FromRowCol(5, 2)),   // primary (nearest)
                UnitState.Spawn(2, 1, Dummy(), Hex.FromRowCol(5, 3)),
                UnitState.Spawn(3, 1, Dummy(), Hex.FromRowCol(6, 2)),
            };
            var result = new Battle(units).Run();

            var t0 = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Tick == 0)
                .ToList();
            Assert.Equal(3, t0.Count);                       // main + 2 extras
            Assert.Equal(10, t0[0].Amount);                  // the swing
            Assert.All(t0.Skip(1), e => Assert.Equal(5, e.Amount)); // extras at 50%
            Assert.Equal(2, t0.Skip(1).Select(e => e.Target).Distinct().Count()); // two different victims

            // Window is 2 swings: extras exist on swing 2, gone on swing 3.
            var extraCount = result.Events.Count(e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Amount == 5);
            Assert.Equal(4, extraCount); // 2 swings × 2 arrows
        }

        [Fact]
        public void MultiShotOverflowRestrikesPrimary()
        {
            var volleyer = BattleTests.Grunt(hp: 400, atk: 10);
            volleyer.Range = 4;
            volleyer.Triggers.Add(AtStart(
                Apply(StatusKind.MultiShotRamp, 3, SelKind.Self),
                Apply(StatusKind.MultiShotWindow, 50, SelKind.Self, ticks: -1, swings: 1)));
            // Only ONE enemy: all 3 extras re-strike it.
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, volleyer, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, Dummy(), Hex.FromRowCol(5, 2)),
            };
            var result = new Battle(units).Run();
            var t0 = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Tick == 0).ToList();
            Assert.Equal(4, t0.Count);
            Assert.All(t0, e => Assert.Equal(1, e.Target));
        }

        // ---- Cleave (greataxe shape) ----

        [Fact]
        public void CleaveHitsEnemiesAdjacentToTarget()
        {
            var axe = BattleTests.Grunt(hp: 400, atk: 10);
            axe.CleavePct = 50;
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, axe, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, Dummy(), Hex.FromRowCol(4, 2)),          // primary
                UnitState.Spawn(2, 1, Dummy(), Hex.FromRowCol(5, 2)),          // adjacent to primary
                UnitState.Spawn(3, 1, Dummy(), Hex.FromRowCol(7, 5)),          // far — untouched
            };
            var result = new Battle(units).Run();
            var t0 = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Tick == 0).ToList();
            Assert.Contains(t0, e => e.Target == 1 && e.Amount == 10);
            Assert.Contains(t0, e => e.Target == 2 && e.Amount == 5);
            Assert.DoesNotContain(t0, e => e.Target == 3);
        }

        // ---- Nth-swing rider (Twin Nock): every 3rd swing fires a 50% echo ----

        [Fact]
        public void EveryThirdSwingFiresTwice()
        {
            var archer = BattleTests.Grunt(hp: 400, atk: 10);
            archer.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.EveryNthSwingOfOwner, Amount = 3 },
                    new Cond { Kind = CondKind.IsRootEvent }, // never chain off the echo
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Swing, Amount = 50,
                    Select = new Selector { Kind = SelKind.EventTarget },
                } },
            });
            var result = new Battle(BattleTests.Duel(archer, Dummy(2000))).Run();

            var echoes = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Amount == 5).ToList();
            var swings = result.Events
                .Count(e => e.Kind == EventKind.Attack && e.Source == 0 && e.Depth == 0);
            Assert.Equal(swings / 3, echoes.Count); // one echo per completed triple
        }

        // ---- The directional Counter law (Phalanx dive) ----

        private static UnitDef Pikeman(int range = 2)
        {
            var pike = BattleTests.Grunt(hp: 800, atk: 10);
            pike.Range = range;
            pike.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            pike.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsOwner },
                    new Cond { Kind = CondKind.IsRootEvent }, // counters don't counter counters
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Swing, AsCounter = true,
                    Select = new Selector { Kind = SelKind.EventSource },
                } },
            });
            return pike;
        }

        [Fact]
        public void CounterStrikesAttackerInReach()
        {
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, Pikeman(), Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, BattleTests.Grunt(hp: 300, atk: 5), Hex.FromRowCol(4, 2)),
            };
            var result = new Battle(units).Run();
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter && e.Source == 0 && e.Target == 1);
        }

        [Fact]
        public void CounterVsRangedRipostesIntoTheFrontline()
        {
            // Same row = a true hex line: pike at col 1, enemy frontliner at col 2,
            // enemy archer at col 5 (distance 4, outside pike reach 2).
            var archer = BattleTests.Grunt(hp: 300, atk: 5);
            archer.Range = 4;
            archer.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            var frontliner = Dummy(300);

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, Pikeman(), Hex.FromRowCol(3, 1)),
                UnitState.Spawn(1, 1, frontliner, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(2, 1, archer, Hex.FromRowCol(3, 5)),
            };
            var result = new Battle(units).Run();

            // The archer's pokes get answered — but the spear can only reach the
            // frontliner standing on the line. The archer is never struck.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter && e.Target == 1);
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter && e.Target == 2);
        }

        [Fact]
        public void CounterOnClearLineCutsAir()
        {
            var archer = BattleTests.Grunt(hp: 300, atk: 5);
            archer.Range = 4;
            archer.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, Pikeman(), Hex.FromRowCol(3, 1)),
                UnitState.Spawn(1, 1, archer, Hex.FromRowCol(3, 5)),
            };
            var result = new Battle(units).Run();
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter);
        }

        [Fact]
        public void CounterChargeGatesToFirstAttackOnly()
        {
            // Riposte shape: one charge, consumed on use, no cast to refresh it.
            var pike = BattleTests.Grunt(hp: 800, atk: 10);
            pike.Triggers.Add(AtStart(Apply(StatusKind.CounterCharge, 1, SelKind.Self)));
            pike.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsOwner },
                    new Cond { Kind = CondKind.OwnerHasStatus, Status = StatusKind.CounterCharge },
                    new Cond { Kind = CondKind.IsRootEvent },
                },
                Do =
                {
                    new EffectDef { Kind = EffectKind.Swing, AsCounter = true, Select = new Selector { Kind = SelKind.EventSource } },
                    new EffectDef { Kind = EffectKind.RemoveStatus, Status = StatusKind.CounterCharge, Amount = 1, Select = new Selector { Kind = SelKind.Self } },
                },
            });
            var result = new Battle(BattleTests.Duel(pike, BattleTests.Grunt(hp: 500, atk: 5))).Run();
            Assert.Equal(1, result.Events.Count(e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter));
        }

        [Fact]
        public void RiposteSpendsOneStackPerIncomingAttack()
        {
            var pike = BattleTests.Grunt(hp: 800, atk: 10);
            pike.Triggers.Add(AtStart(
                Apply(StatusKind.CounterCharge, 1, SelKind.Self),
                Apply(StatusKind.CounterCharge, 1, SelKind.Self)));
            pike.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsOwner },
                    new Cond { Kind = CondKind.OwnerHasStatus, Status = StatusKind.CounterCharge },
                    new Cond { Kind = CondKind.IsRootEvent },
                },
                Do =
                {
                    new EffectDef { Kind = EffectKind.Swing, AsCounter = true, Select = new Selector { Kind = SelKind.EventSource } },
                    new EffectDef { Kind = EffectKind.RemoveStatus, Status = StatusKind.CounterCharge, Amount = 1, Select = new Selector { Kind = SelKind.Self } },
                },
            });

            var result =
                new Battle(BattleTests.Duel(pike, BattleTests.Grunt(hp: 500, atk: 5))).Run();

            Assert.Equal(2, result.Events.Count(e =>
                e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter));
        }

        // ---- Heal-autos (censer law) ----

        [Fact]
        public void CenserSwingsHealTheLowestAllyAndBuildMana()
        {
            var censer = BattleTests.Pacifist(200);
            censer.HealAutos = true;
            censer.Attack = 6;      // heal per swing
            censer.Range = 3;
            censer.ManaMax = 100;

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, censer, Hex.FromRowCol(2, 2)),
                UnitState.Spawn(1, 0, BattleTests.Grunt(hp: 200, atk: 4), Hex.FromRowCol(3, 2)), // front ally
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 200, atk: 8), Hex.FromRowCol(4, 2)),
            };
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.Heal && e.Source == 0 && e.Target == 1 && e.Amount == 6);
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.ManaChanged && e.Target == 0 && e.Amount == Battle.ManaPerAttack);
            // She never attacks the enemy.
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.DamageDealt && e.Source == 0);
        }

        // ---- Muster snapshot (ADR 0014): BattleStart + AlliesWithin IS the Company ----

        [Fact]
        public void MusterAuraLocksAtPlacementAndFollowsTheDrift()
        {
            var banneret = BattleTests.Pacifist(300);
            banneret.Triggers.Add(AtStart(new EffectDef
            {
                Kind = EffectKind.ApplyStatus, Status = StatusKind.Haste, Amount = 500, StatusTicks = -1,
                Select = new Selector { Kind = SelKind.AlliesWithin, Range = 1, ExcludeSelf = true },
            }));
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, banneret, Hex.FromRowCol(1, 2)),
                UnitState.Spawn(1, 0, BattleTests.Grunt(hp: 300), Hex.FromRowCol(2, 2)),  // placed adjacent → Company
                UnitState.Spawn(2, 0, BattleTests.Grunt(hp: 300), Hex.FromRowCol(1, 5)),  // placed far → not
                UnitState.Spawn(3, 1, BattleTests.Grunt(hp: 900, atk: 2), Hex.FromRowCol(7, 3)),
            };
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 1 && e.Aux == (int)StatusKind.Haste);
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 2 && e.Aux == (int)StatusKind.Haste);
            // The Company member marches across the board and never loses the blessing.
            Assert.DoesNotContain(result.Events, e =>
                e.Kind == EventKind.StatusExpired && e.Target == 1 && e.Aux == (int)StatusKind.Haste);
        }

        // ---- Gradient StatRules: Full Draw + Burning Hours ----

        [Fact]
        public void FullDrawScalesDamagePerHexOfDistance()
        {
            var archer = BattleTests.Grunt(hp: 300, atk: 10);
            archer.Range = 5;
            archer.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackFlat, Amount = 2, ScaleBy = StatScale.DistanceToTarget,
            });
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, archer, Hex.FromRowCol(1, 2)),
                UnitState.Spawn(1, 1, Dummy(), Hex.FromRowCol(5, 2)),
            };
            int dist = Hex.Distance(units[0].Pos, units[1].Pos);
            var result = new Battle(units).Run();
            var first = result.Events.First(e => e.Kind == EventKind.DamageDealt && e.Source == 0);
            Assert.Equal(10 + 2 * dist, first.Amount);
        }

        [Fact]
        public void BurningHoursSpeedsUpAsHpFalls()
        {
            var zerk = BattleTests.Grunt(hp: 100, atk: 1);
            zerk.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackSpeed, Amount = 100, ScaleBy = StatScale.MissingHpPct10,
            });
            var units = BattleTests.Duel(zerk, Dummy());
            units[0].Hp = 50; // half dead → +500 speed → interval 10×1000/1500 = 6
            var result = new Battle(units).Run();
            var swings = result.Events
                .Where(e => e.Kind == EventKind.Attack && e.Source == 0)
                .Select(e => e.Tick).Take(2).ToList();
            Assert.Equal(6, swings[1] - swings[0]);
        }

        // ---- The pierce line (Piercing Bolt / Lancer lunge) ----

        [Fact]
        public void LineSelectorHitsEverythingOnTheRay()
        {
            var caster = BattleTests.Pacifist(400);
            caster.ManaMax = 5;
            caster.Signature.Add(new EffectDef
            {
                Kind = EffectKind.Damage, Amount = 7,
                Select = new Selector { Kind = SelKind.EnemiesOnLineThroughTarget },
            });
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, caster, Hex.FromRowCol(3, 0)),
                UnitState.Spawn(1, 1, Dummy(300), Hex.FromRowCol(3, 1)),   // adjacent — feeds mana
                UnitState.Spawn(2, 1, Dummy(300), Hex.FromRowCol(3, 3)),   // on the ray
                UnitState.Spawn(3, 1, Dummy(300), Hex.FromRowCol(3, 5)),   // on the ray
                UnitState.Spawn(4, 1, Dummy(300), Hex.FromRowCol(6, 4)),   // off the ray
            };
            // The adjacent dummy must actually hurt the caster to build mana:
            var attacker = BattleTests.Grunt(hp: 300, atk: 3);
            attacker.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            units[1] = UnitState.Spawn(1, 1, attacker, Hex.FromRowCol(3, 1));

            var result = new Battle(units).Run();
            // Only the FIRST cast: later casts re-aim the ray at whoever is left.
            int firstCastTick = result.Events.First(e => e.Kind == EventKind.Cast && e.Source == 0).Tick;
            var bolt = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Ability
                            && e.Source == 0 && e.Tick == firstCastTick)
                .ToList();
            Assert.NotEmpty(bolt);
            var victims = bolt.Select(e => e.Target).Distinct().OrderBy(x => x).ToList();
            Assert.Contains(1, victims);
            Assert.Contains(2, victims);
            Assert.Contains(3, victims);
            Assert.DoesNotContain(4, victims);
        }

        // ---- Recast (Dying Star): kill-gated chain onto the next Burning enemy ----

        [Fact]
        public void RecastChainsOntoNearestBurningEnemy()
        {
            var star = BattleTests.Pacifist(400);
            star.ManaMax = 5;
            star.Triggers.Add(AtStart(
                Apply(StatusKind.Burn, 30, SelKind.EnemiesWithin, range: 10)));
            star.Signature.Add(new EffectDef
            {
                Kind = EffectKind.Damage, Amount = 100,
                Select = new Selector { Kind = SelKind.CurrentTarget },
            });
            star.Triggers.Add(new Trigger
            {
                On = EventKind.Death,
                When = { new Cond { Kind = CondKind.SourceIsOwner } },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Recast,
                    Select = new Selector { Kind = SelKind.NearestEnemy, MustHave = StatusKind.Burn },
                } },
            });

            var hitter = BattleTests.Grunt(hp: 90, atk: 3);
            hitter.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, star, Hex.FromRowCol(3, 1)),
                UnitState.Spawn(1, 1, hitter, Hex.FromRowCol(3, 2)),      // primary target, feeds mana
                UnitState.Spawn(2, 1, Dummy(90), Hex.FromRowCol(3, 4)),   // burning — the chain victim
            };
            var result = new Battle(units).Run();

            var deaths = result.Events.Where(e => e.Kind == EventKind.Death).Select(e => e.Target).ToList();
            Assert.Contains(1, deaths);
            Assert.Contains(2, deaths); // killed by the recast chain, same cast
            var abilityHits = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Ability && e.Source == 0)
                .Select(e => e.Target).Distinct().ToList();
            Assert.Equal(new List<int> { 1, 2 }, abilityHits.OrderBy(x => x).ToList());
        }

        // ---- Leap emits its own event (Pikewall punish hook) ----

        [Fact]
        public void LeapEmitsLeapEvent()
        {
            var shade = BattleTests.Grunt(hp: 300, atk: 10);
            shade.Triggers.Add(AtStart(new EffectDef
            {
                Kind = EffectKind.Leap, Select = new Selector { Kind = SelKind.FarthestEnemy },
            }));
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, shade, Hex.FromRowCol(0, 0)),
                UnitState.Spawn(1, 1, Dummy(), Hex.FromRowCol(5, 2)),
                UnitState.Spawn(2, 1, Dummy(), Hex.FromRowCol(7, 5)),
            };
            var result = new Battle(units).Run();
            var leap = result.Events.Single(e => e.Kind == EventKind.Leap);
            Assert.Equal(0, leap.Source);
            Assert.Equal(2, leap.Target); // farthest enemy
        }

        // ---- Composer: temper tiers + the Relic rule (ADR 0015) ----

        [Fact]
        public void TierScalesStatsAndRelicUnlocksTheRider()
        {
            var chassis = new ChassisDef { Name = "hero", MaxHp = 100, StarterWeapon = new WeaponDef { Damage = 10, Interval = 10, Range = 1 } };
            var bow = new WeaponDef
            {
                Name = "bow", Category = "bow", Damage = 20, Interval = 10, Range = 4,
                MasteryRangeBonus = 1,
                MasteryStatRules = { new StatRule { Stat = StatKind.AttackFlat, Amount = 3 } },
            };

            var worn = Loadout.Compose(chassis, bow, tier: WeaponTier.Worn, mastered: false);
            Assert.Equal(20, worn.Def.Attack);
            Assert.Equal(4, worn.Def.Range);
            Assert.Empty(worn.Def.StatRules);

            var honedMastered = Loadout.Compose(chassis, bow, tier: WeaponTier.Honed, mastered: true);
            Assert.Equal(25, honedMastered.Def.Attack);       // +25%
            Assert.Equal(5, honedMastered.Def.Range);         // rider on
            Assert.Single(honedMastered.Def.StatRules);

            var relicUnmastered = Loadout.Compose(chassis, bow, tier: WeaponTier.Relic, mastered: false);
            Assert.Equal(30, relicUnmastered.Def.Attack);     // +50%
            Assert.Equal(5, relicUnmastered.Def.Range);       // Relic rule: rider live for anyone
            Assert.Single(relicUnmastered.Def.StatRules);

            var relicMastered = Loadout.Compose(chassis, bow, tier: WeaponTier.Relic, mastered: true);
            Assert.Equal(6, relicMastered.Def.Range);         // doubled for the master
            Assert.Equal(2, relicMastered.Def.StatRules.Count);
        }
    }
}
