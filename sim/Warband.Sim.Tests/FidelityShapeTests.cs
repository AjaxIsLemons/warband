using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>The shapes added by the fidelity pass (de-SIMPLIFYing the content):
    /// corpse-pool transfer, escalating lines, in-field conds, shield-scaled rules,
    /// state conds, triage filters, behind-only lines, cleave bonuses.</summary>
    public class FidelityShapeTests
    {
        private static Trigger AtStart(params EffectDef[] effects) => DiveStatusTests.AtStart(effects);
        private static EffectDef Apply(StatusKind kind, int mag, SelKind sel, int ticks = -1, int swings = 0, int range = 0)
            => DiveStatusTests.Apply(kind, mag, sel, ticks, swings, range);

        private static UnitDef Rooted(UnitDef d)
        {
            d.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            return d;
        }

        [Fact]
        public void ContagionPassesTheCorpsePool()
        {
            // Kill a burning enemy: its REMAINING pool lands on the enemy nearest the corpse.
            var pyro = BattleTests.Grunt(hp: 400, atk: 30);
            pyro.Triggers.Add(AtStart(Apply(StatusKind.Burn, 12, SelKind.NearestEnemy)));
            pyro.Triggers.Add(new Trigger
            {
                On = EventKind.Death,
                When = { new Cond { Kind = CondKind.TargetIsAllyOfOwner, Not = true }, new Cond { Kind = CondKind.TargetHasStatus, Status = StatusKind.Burn } },
                Do = { new EffectDef
                {
                    Kind = EffectKind.ApplyStatus, Status = StatusKind.Burn, Amount = 1,
                    ScaleByEventTargetStatus = true, ScaleStatus = StatusKind.Burn,
                    Select = new Selector { Kind = SelKind.NearestEnemy, AnchorEventTarget = true, ExcludeAnchorUnit = true },
                } },
            });
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, pyro, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, Rooted(BattleTests.Pacifist(25)), Hex.FromRowCol(4, 2)),
                UnitState.Spawn(2, 1, Rooted(BattleTests.Pacifist(500)), Hex.FromRowCol(5, 2)),
            };
            var result = new Battle(units).Run();

            var death = result.Events.First(e => e.Kind == EventKind.Death && e.Target == 1);
            var pass = result.Events.First(e =>
                e.Kind == EventKind.StatusApplied && e.Target == 2 && e.Aux == (int)StatusKind.Burn && e.Tick >= death.Tick);
            // The corpse died with 12 minus one decay per elapsed pulse.
            int expected = 12 - death.Tick / Battle.PulseInterval;
            Assert.Equal(expected, pass.Amount);
        }

        [Fact]
        public void EscalatingLineHitsHarderDownTheRay()
        {
            var caster = BattleTests.Pacifist(400);
            caster.ManaMax = 5;
            caster.Signature.Add(new EffectDef
            {
                Kind = EffectKind.Damage, Amount = 10, EscalatePctPerIndex = 50,
                Select = new Selector { Kind = SelKind.EnemiesOnLineThroughTarget },
            });
            var attacker = BattleTests.Grunt(hp: 400, atk: 3);
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, caster, Hex.FromRowCol(3, 0)),
                UnitState.Spawn(1, 1, Rooted(attacker), Hex.FromRowCol(3, 1)),
                UnitState.Spawn(2, 1, Rooted(BattleTests.Pacifist(400)), Hex.FromRowCol(3, 3)),
                UnitState.Spawn(3, 1, Rooted(BattleTests.Pacifist(400)), Hex.FromRowCol(3, 5)),
            };
            var result = new Battle(units).Run();
            int castTick = result.Events.First(e => e.Kind == EventKind.Cast && e.Source == 0).Tick;
            var bolt = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Ability && e.Tick == castTick)
                .OrderBy(e => e.Target).Select(e => e.Amount).ToList();
            Assert.Equal(new List<int> { 10, 15, 20 }, bolt); // +50% per body down the line
        }

        [Fact]
        public void LineThroughFarthestAimsPastTheFrontline()
        {
            var sniper = BattleTests.Pacifist(400);
            sniper.ManaMax = 5;
            sniper.Signature.Add(new EffectDef
            {
                Kind = EffectKind.Damage, Amount = 9,
                Select = new Selector { Kind = SelKind.EnemiesOnLineThroughFarthest },
            });
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, sniper, Hex.FromRowCol(3, 0)),
                UnitState.Spawn(1, 1, Rooted(BattleTests.Grunt(hp: 400, atk: 3)), Hex.FromRowCol(3, 1)), // nearest (feeds mana)
                UnitState.Spawn(2, 1, Rooted(BattleTests.Pacifist(400)), Hex.FromRowCol(3, 5)),          // farthest, same row
            };
            var result = new Battle(units).Run();
            int castTick = result.Events.First(e => e.Kind == EventKind.Cast && e.Source == 0).Tick;
            var victims = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Cause == Cause.Ability && e.Tick == castTick)
                .Select(e => e.Target).OrderBy(x => x).ToList();
            Assert.Equal(new List<int> { 1, 2 }, victims); // through the frontliner, INTO the farthest
        }

        [Fact]
        public void BehindOnlyLineSkipsTheTarget()
        {
            var pike = BattleTests.Grunt(hp: 400, atk: 10);
            pike.Range = 2;
            pike.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            pike.Triggers.Add(new Trigger
            {
                On = EventKind.Attack,
                When = { new Cond { Kind = CondKind.SourceIsOwner }, new Cond { Kind = CondKind.IsRootEvent } },
                Do = { new EffectDef
                {
                    Kind = EffectKind.Damage, Amount = 4,
                    Select = new Selector { Kind = SelKind.EnemiesOnLineThroughTarget, Range = 3, SkipCtxTarget = true },
                } },
            });
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, pike, Hex.FromRowCol(3, 1)),
                UnitState.Spawn(1, 1, Rooted(BattleTests.Pacifist(400)), Hex.FromRowCol(3, 3)), // the target (range 2)
                UnitState.Spawn(2, 1, Rooted(BattleTests.Pacifist(400)), Hex.FromRowCol(3, 4)), // directly behind
            };
            var result = new Battle(units).Run();
            var echoes = result.Events
                .Where(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Amount == 4).ToList();
            Assert.NotEmpty(echoes);
            Assert.All(echoes, e => Assert.Equal(2, e.Target)); // never the target itself
        }

        [Fact]
        public void ShieldScaledRuleSwingsWithTheWall()
        {
            var wall = BattleTests.Grunt(hp: 400, atk: 10);
            wall.StatRules.Add(new StatRule { Stat = StatKind.AttackFlat, Amount = 2, ScaleBy = StatScale.ShieldPer10 });
            wall.Triggers.Add(AtStart(new EffectDef
            {
                Kind = EffectKind.GrantShield, Amount = 50, Select = new Selector { Kind = SelKind.Self },
            }));
            var result = new Battle(BattleTests.Duel(wall, Rooted(BattleTests.Pacifist(600)))).Run();
            var first = result.Events.First(e => e.Kind == EventKind.DamageDealt && e.Source == 0);
            Assert.Equal(10 + 2 * 5, first.Amount); // 50 shield → ×5
        }

        [Fact]
        public void AnyEnemyHasStatusGatesStateRules()
        {
            // Zeal shape: faster only while something burns.
            var zealot = BattleTests.Grunt(hp: 400, atk: 5);
            zealot.StatRules.Add(new StatRule
            {
                Stat = StatKind.AttackSpeed, Amount = 1000,
                When = { new Cond { Kind = CondKind.AnyEnemyHasStatus, Status = StatusKind.Burn } },
            });
            zealot.Triggers.Add(AtStart(Apply(StatusKind.Burn, 3, SelKind.NearestEnemy)));
            var result = new Battle(BattleTests.Duel(zealot, Rooted(BattleTests.Pacifist(600)))).Run();

            var swings = result.Events
                .Where(e => e.Kind == EventKind.Attack && e.Source == 0)
                .Select(e => e.Tick).Take(9).ToList();
            // Burn pool (3) dies at tick 30: doubled speed before (interval 5), base after (10).
            Assert.Contains(5, swings.Select((t, i) => i > 0 ? t - swings[i - 1] : 0).ToList());
            Assert.Contains(10, swings.Select((t, i) => i > 0 ? t - swings[i - 1] : 0).ToList());
        }

        [Fact]
        public void TriageFilterOnlyFeedsTheWounded()
        {
            var medic = BattleTests.Pacifist(300);
            medic.ManaMax = 5;
            medic.Signature.Add(new EffectDef
            {
                Kind = EffectKind.GrantShield, Amount = 10,
                Select = new Selector { Kind = SelKind.AlliesWithin, Range = 3, ExcludeSelf = true, BelowHpPct = 50 },
            });
            var hurt = Rooted(BattleTests.Pacifist(100));
            var healthy = Rooted(BattleTests.Pacifist(100));
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, medic, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 0, hurt, Hex.FromRowCol(3, 3)),
                UnitState.Spawn(2, 0, healthy, Hex.FromRowCol(3, 1)),
                UnitState.Spawn(3, 1, Rooted(BattleTests.Grunt(hp: 600, atk: 3)), Hex.FromRowCol(4, 2)), // pokes the medic → mana
            };
            units[1].Hp = 30; // wounded from the start
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e => e.Kind == EventKind.ShieldChanged && e.Target == 1);
            Assert.DoesNotContain(result.Events, e => e.Kind == EventKind.ShieldChanged && e.Target == 2);
        }

        [Fact]
        public void InFieldConditionSeesFieldStanders()
        {
            // Stoke shape: swings apply extra Burn only to enemies inside the owner's field.
            var pyro = BattleTests.Grunt(hp: 400, atk: 5);
            pyro.Range = 4;
            pyro.Triggers.Add(AtStart(Apply(StatusKind.Root, 0, SelKind.Self)));
            pyro.Triggers.Add(AtStart(new EffectDef
            {
                Kind = EffectKind.CreateField,
                Select = new Selector { Kind = SelKind.NearestEnemy },
                Field = new FieldDef { Radius = 0, Ticks = -1 },     // a bare marker field on the enemy's hex
            }));
            pyro.Triggers.Add(new Trigger
            {
                On = EventKind.DamageDealt,
                When =
                {
                    new Cond { Kind = CondKind.SourceIsOwner },
                    new Cond { Kind = CondKind.CauseIs, Cause = Cause.Attack },
                    new Cond { Kind = CondKind.TargetInFieldOfOwner },
                },
                Do = { Apply(StatusKind.Burn, 2, SelKind.EventTarget) },
            });
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, pyro, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, Rooted(BattleTests.Pacifist(300)), Hex.FromRowCol(5, 2)), // stands in the marker
            };
            var result = new Battle(units).Run();
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Target == 1 && e.Aux == (int)StatusKind.Burn && e.Amount == 2);
        }

        [Fact]
        public void CleaveBonusStacksOntoTheWeapon()
        {
            var chassis = new ChassisDef
            {
                Name = "zerk", MaxHp = 100,
                StarterWeapon = new WeaponDef { Damage = 10, Interval = 10, Range = 1, CleavePct = 25 },
            };
            var noQuarter = new SpecNode { CleaveBonusPct = 75 };
            var composed = Loadout.Compose(chassis, nodes: new[] { noQuarter });
            Assert.Equal(100, composed.Def.CleavePct);
        }
    }
}
