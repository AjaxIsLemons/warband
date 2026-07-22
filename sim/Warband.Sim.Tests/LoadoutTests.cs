using System.Collections.Generic;
using System.Linq;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class LoadoutTests
    {
        private static ChassisDef Ranger() => new ChassisDef
        {
            Name = "Ranger", MaxHp = 100, ManaMax = 20,
            StarterWeapon = new WeaponDef { Name = "Shortbow", Damage = 9, Interval = 10, Range = 4 },
            Signature = { new EffectDef { Kind = EffectKind.Damage, Amount = 20 } },
        };

        [Fact]
        public void StarterWeaponAppliesByDefault()
        {
            var composed = Loadout.Compose(Ranger());
            Assert.Equal(9, composed.Def.Attack);
            Assert.Equal(4, composed.Def.Range);
        }

        [Fact]
        public void DaggerSwapTurnsRangerIntoAssassinFlavor()
        {
            // Jake's round-10 example: range is a tinkerable axis on the weapon.
            var daggers = new WeaponDef { Name = "Daggers", Damage = 7, Interval = 6, Range = 1, CritChance = 25 };
            var composed = Loadout.Compose(Ranger(), daggers);
            Assert.Equal(1, composed.Def.Range);
            Assert.Equal(6, composed.Def.AttackInterval);
            Assert.Equal(25, composed.Def.CritChance);
            Assert.Equal("Ranger", composed.Def.Name); // hero stays the hero
        }

        [Fact]
        public void NodesTrinketsAndOverridesMergeInOrder()
        {
            var trinket = new TrinketDef
            {
                Name = "Vitality Charm", HpBonus = 30,
                SpawnStatuses = { (StatusKind.Regen, 2) },
            };
            var forkA = new SpecNode
            {
                Name = "Sniper",
                SignatureOverride = new List<EffectDef>
                {
                    new EffectDef { Kind = EffectKind.Damage, Amount = 60, Select = new Selector { Kind = SelKind.FarthestEnemy } },
                },
            };
            var nodeB = new SpecNode
            {
                Name = "Deadeye",
                StatRules = { new StatRule { Stat = StatKind.AttackFlat, Amount = 5, When = { new Cond { Kind = CondKind.OwnerBelowHpPct, Amount = 101 } } } },
            };

            var composed = Loadout.Compose(Ranger(), trinkets: new[] { trinket }, nodes: new[] { forkA, nodeB });
            Assert.Equal(130, composed.Def.MaxHp);
            Assert.Single(composed.Def.Signature);
            Assert.Equal(60, composed.Def.Signature[0].Amount);          // fork transformed the signature
            Assert.Single(composed.SpawnStatuses, s => s.Kind == StatusKind.Regen);
            Assert.Single(composed.Def.StatRules);
        }

        [Fact]
        public void ComposedUnitsFightAndEverythingFires()
        {
            var composed = Loadout.Compose(Ranger(),
                trinkets: new[] { new TrinketDef { Name = "Charm", SpawnStatuses = { (StatusKind.Regen, 3) } } });
            var runBonus = new[] { new Status { Kind = StatusKind.AttackUp, Mag = 5, TicksLeft = -1 } };

            var units = new List<UnitState>
            {
                Loadout.Spawn(0, 0, composed, Hex.FromRowCol(0, 2), runBonus),
                UnitState.Spawn(1, 1, BattleTests.Grunt(hp: 200, atk: 6), Hex.FromRowCol(4, 2)),
            };
            var result = new Battle(units).Run();

            var firstShot = result.Events.First(e => e.Kind == EventKind.DamageDealt && e.Source == 0 && e.Cause == Cause.Attack);
            Assert.Equal(14, firstShot.Amount);      // 9 weapon + 5 run-earned
            Assert.Contains(result.Events, e => e.Kind == EventKind.Heal && e.Target == 0); // trinket regen ticked
            Assert.Contains(result.Events, e => e.Kind == EventKind.Cast && e.Source == 0); // signature intact
        }

        [Fact]
        public void CompositionIsDeterministic()
        {
            ComposedLoadout Make() => Loadout.Compose(Ranger(),
                new WeaponDef { Name = "Daggers", Damage = 7, Interval = 6, Range = 1 },
                new[] { new TrinketDef { HpBonus = 10 } },
                new[] { new SpecNode { HpBonus = 5 } });
            List<UnitState> Setup(ComposedLoadout c) => new List<UnitState>
            {
                Loadout.Spawn(0, 0, c, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, BattleTests.Grunt(hp: 120), Hex.FromRowCol(4, 2)),
            };
            var r1 = new Battle(Setup(Make())).Run();
            var r2 = new Battle(Setup(Make())).Run();
            Assert.Equal(r1.FinalHash, r2.FinalHash);
        }
    }
}
