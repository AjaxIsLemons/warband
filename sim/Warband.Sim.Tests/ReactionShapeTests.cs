using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>The reactive shapes: corpse-field spawns (Inferno spread), the Phase-entry
    /// damage window, status-gated riders, and thorns via PctOfEventAmount.</summary>
    public class ReactionShapeTests
    {
        private static Trigger AtStart(params EffectDef[] effects) => DiveStatusTests.AtStart(effects);
        private static EffectDef Apply(StatusKind kind, int mag, SelKind sel, int ticks = -1, int swings = 0, int range = 0)
            => DiveStatusTests.Apply(kind, mag, sel, ticks, swings, range);

        [Fact]
        public void DeathSpawnsFieldOnTheCorpseHex()
        {
            // Inferno spread: when a Burning enemy dies, the ground under it ignites.
            var pyro = BattleTests.Grunt(hp: 400, atk: 10);
            pyro.Triggers.Add(AtStart(Apply(StatusKind.Burn, 10, SelKind.NearestEnemy))); // pool must outlive the kill
            pyro.Triggers.Add(new Trigger
            {
                On = EventKind.Death,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.TargetHasStatus, Status = StatusKind.Burn },
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.CreateField,
                    Select = new Selector { Kind = SelKind.EventTarget }, // the corpse
                    Field = new FieldDef { Radius = 1, Ticks = 50, Pulse = { new EffectDef { Kind = EffectKind.Damage, Amount = 3 } }, PulseAffects = Affects.Enemies },
                } },
            });
            var result = new Battle(BattleTests.Duel(pyro, BattleTests.Pacifist(30))).Run();

            var death = result.Events.Single(e => e.Kind == EventKind.Death && e.Target == 1);
            var field = result.Events.SingleOrDefault(e => e.Kind == EventKind.FieldCreated && e.Source == 0);
            Assert.NotNull(field);
            Assert.Equal(death.Tick, field!.Tick); // ignites the moment they fall
        }

        [Fact]
        public void PhaseEntersOnRecentDamageThreshold()
        {
            // Phantom shape: burst damage within the window trips the Phase.
            var phantom = BattleTests.Pacifist(100);
            phantom.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsOwner },
                    new Cond { Kind = CondKind.OwnerRecentDamageAbovePct, Amount = 25 },
                    new Cond { Kind = CondKind.OwnerHasStatus, Status = StatusKind.Phase, Not = true },
                },
                Do = { Apply(StatusKind.Phase, 0, SelKind.Self, ticks: 30) },
            });
            var result = new Battle(BattleTests.Duel(phantom, BattleTests.Grunt(hp: 300, atk: 15))).Run();

            // 15-damage swings vs 100 HP: the second swing crosses the 25% window → Phase.
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 0 && e.Aux == (int)StatusKind.Phase);
            // And phasing bought real time: the fight outlives the 7 swings a helpless
            // punchbag would absorb.
            var death = result.Events.Single(e => e.Kind == EventKind.Death && e.Target == 0);
            Assert.True(death.Tick > 70);
        }

        [Fact]
        public void ThornsReflectPctOfDamageTaken()
        {
            // Retribution shape: attackers Taunted by the owner bleed for trying.
            var warden = BattleTests.Pacifist(400);
            warden.Triggers.Add(AtStart(Apply(StatusKind.Taunt, 0, SelKind.NearestEnemy, ticks: -1)));
            warden.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsOwner },
                    new Cond { Kind = CondKind.SourceHasStatus, Status = StatusKind.Taunt },
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Damage, PctOfEventAmount = 50,
                    Select = new Selector { Kind = SelKind.EventSource },
                } },
            });
            var result = new Battle(BattleTests.Duel(warden, BattleTests.Grunt(hp: 300, atk: 10))).Run();

            var thorns = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Target == 1).ToList();
            Assert.NotEmpty(thorns);
            Assert.All(thorns, e => Assert.Equal(5, e.Amount)); // half of every 10-damage swing
        }

        [Fact]
        public void SpearpointRewardsExactMaxReach()
        {
            // Pike rider shape: bonus damage packet only at exactly range 2.
            var pike = BattleTests.Grunt(hp: 400, atk: 10);
            pike.Range = 2;
            pike.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            pike.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                    new Cond { Kind = CondKind.TargetAtRangeOfOwner, Amount = 2 },
                },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Damage, PctOfEventAmount = 50,
                    Select = new Selector { Kind = SelKind.EventTarget },
                } },
            });

            // Scenario A: a pinned enemy at exactly reach 2 → every swing rewarded.
            var pinned = BattleTests.Grunt(hp: 500, atk: 0);
            pinned.Range = 2;
            pinned.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            var atReach = new Battle(new List<UnitState>
            {
                UnitState.Spawn(0, 0, pike, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, pinned, Hex.FromRowCol(5, 2)),
            }).Run();
            Assert.NotEmpty(atReach.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Amount == 5));

            // Scenario B: the same enemy adjacent → no bonus packet, ever.
            var adjacent = new Battle(new List<UnitState>
            {
                UnitState.Spawn(0, 0, pike, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, pinned, Hex.FromRowCol(4, 2)),
            }).Run();
            Assert.Empty(adjacent.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Amount == 5));
        }

        [Fact]
        public void MusterCounterGuard()
        {
            // The Unbroken Line shape: allies PLACED adjacent are marked at muster; the
            // pikeman answers the first root-level attack on any marked ally in his reach.
            var pike = BattleTests.Grunt(hp: 600, atk: 10);
            pike.Range = 2;
            pike.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            pike.Triggers.Add(AtStart(new EffectDef
            {
                Kind = EffectKind.ApplyStatus, Status = StatusKind.CounterCharge, Amount = 1, StatusTicks = -1,
                Select = new Selector { Kind = SelKind.AlliesWithin, Range = 1, ExcludeSelf = true },
            }));
            pike.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When =
                {
                    new Cond { Kind = CondKind.TargetIsAllyOfOwner },
                    new Cond { Kind = CondKind.TargetHasStatus, Status = StatusKind.CounterCharge },
                    new Cond { Kind = CondKind.IsRootEvent },
                },
                Do =
                {
                    new EffectDef { Kind = EffectKind.Swing, AsCounter = true, Select = new Selector { Kind = SelKind.EventSource } },
                    new EffectDef { Kind = EffectKind.RemoveStatus, Status = StatusKind.CounterCharge, Amount = 1, Select = new Selector { Kind = SelKind.EventTarget } },
                },
            });

            var ward = BattleTests.Pacifist(300);
            ward.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, pike, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, ward, Hex.FromRowCol(4, 2)),   // placed adjacent → guarded
                UnitState.Spawn(2, 1, BattleTests.Grunt(hp: 400, atk: 5), Hex.FromRowCol(5, 2)),
            };
            var result = new Battle(units).Run();

            // Exactly one riposte (single charge), thrown by the pikeman on the ward's behalf.
            var counters = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Counter && e.Source == 0).ToList();
            Assert.Single(counters);
            Assert.Equal(2, counters[0].Target);
        }
    }
}
