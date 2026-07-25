using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public class AuthoredEncounterTests
    {
        [Fact]
        public void BondedPairPublishesTheWholeRuleAndFormation()
        {
            var encounter = Encounters.BondedPair();

            Assert.Equal("bonded-pair", encounter.Id);
            Assert.Equal("BOND", encounter.RuleName);
            Assert.Contains("+100% Attack Speed", encounter.RuleText);
            Assert.Equal(2, encounter.Enemies.Count);
            Assert.All(encounter.Enemies, e =>
            {
                Assert.True(Battle.InBounds(e.Pos));
                Assert.True(e.Pos.Row >= 5);
            });
        }

        [Fact]
        public void FirstBondedDeathEnragesOnlyTheSurvivor()
        {
            var encounter = Encounters.BondedPair();
            var executioner = new UnitDef
            {
                Name = "proof executioner",
                MaxHp = 1000,
                Attack = 1000,
                AttackInterval = 10,
                Range = 8,
                MoveInterval = 5,
            };
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, executioner, Hex.FromRowCol(2, 2)),
                UnitState.Spawn(100, 1, encounter.Enemies[0].Def, encounter.Enemies[0].Pos),
                UnitState.Spawn(101, 1, encounter.Enemies[1].Def, encounter.Enemies[1].Pos),
            };

            var result = new Battle(units, seed: 17).Run();
            var firstEnemyDeath = result.Events.First(e =>
                e.Kind == EventKind.Death && (e.Target == 100 || e.Target == 101));
            int survivor = firstEnemyDeath.Target == 100 ? 101 : 100;

            var enrage = Assert.Single(result.Events, e =>
                e.Tick == firstEnemyDeath.Tick &&
                e.Kind == EventKind.StatusApplied &&
                e.Target == survivor &&
                e.Aux == (int)StatusKind.Haste &&
                e.Amount == Encounters.BondHaste);
            Assert.Equal(Encounters.BondHaste, enrage.Amount);
            Assert.DoesNotContain(result.Events, e =>
                e.Tick == firstEnemyDeath.Tick &&
                e.Kind == EventKind.StatusApplied &&
                e.Target == firstEnemyDeath.Target &&
                e.Aux == (int)StatusKind.Haste);
        }

        [Fact]
        public void PlayableSkeletonLineupResolvesAndShowsTheEnrage()
        {
            var encounter = Encounters.BondedPair();
            var units = new List<UnitState>();
            int unitId = 0;
            foreach (var hero in SkirmishProof.Heroes.Where(h => !h.StartsInReserve))
            {
                var chassis = Kits.Chassis[hero.HeroId];
                var weapon = Weapons.All[hero.WeaponIds[0]];
                var loadout = Loadout.Compose(
                    chassis,
                    weapon,
                    mastered: chassis.Specializations.Contains(weapon.Category));
                units.Add(Loadout.Spawn(unitId++, 0, loadout, hero.DefaultPosition));
            }
            for (int i = 0; i < encounter.Enemies.Count; i++)
                units.Add(UnitState.Spawn(
                    100 + i,
                    1,
                    encounter.Enemies[i].Def,
                    encounter.Enemies[i].Pos));

            var result = new Battle(units, seed: 20260724).Run();

            Assert.True(result.EndTick < Battle.OvertimeStartTick);
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied &&
                (e.Target == 100 || e.Target == 101) &&
                e.Aux == (int)StatusKind.Haste &&
                e.Amount == Encounters.BondHaste);
        }

        [Fact]
        public void PreparationCatalogReferencesRunnableDistinctLoadouts()
        {
            Assert.Equal(4, SkirmishProof.Heroes.Count);
            Assert.Equal(
                SkirmishProof.FieldCapacity,
                SkirmishProof.Heroes.Count(hero => !hero.StartsInReserve));
            Assert.Single(SkirmishProof.Heroes, hero => hero.StartsInReserve);

            foreach (var hero in SkirmishProof.Heroes)
            {
                var chassis = Kits.Chassis[hero.HeroId];
                Assert.Equal(3, hero.WeaponIds.Count);
                Assert.Equal(3, hero.WeaponIds.Distinct().Count());
                Assert.Equal(chassis.StarterWeapon.Category, hero.WeaponIds[0]);
                Assert.True(Battle.InBounds(hero.DefaultPosition));
                Assert.InRange(hero.DefaultPosition.Row, 0, 2);

                var profiles = hero.WeaponIds.Select(id =>
                {
                    var weapon = Weapons.All[id];
                    Assert.True(SkirmishProof.MasteryCopy.ContainsKey(id));
                    var loadout = Loadout.Compose(
                        chassis,
                        weapon,
                        mastered: chassis.Specializations.Contains(weapon.Category));
                    return (
                        loadout.Def.Attack,
                        loadout.Def.AttackInterval,
                        loadout.Def.Range,
                        loadout.Def.HealAutos);
                }).ToList();

                Assert.Equal(profiles.Count, profiles.Distinct().Count());
                Assert.Contains(
                    hero.WeaponIds,
                    id => !chassis.Specializations.Contains(Weapons.All[id].Category));
            }
        }

        [Fact]
        public void PlanningProofStartsWithDataDrivenFieldAndBenchCapacity()
        {
            var draft = SkirmishProof.CreatePlanningDraft(3, 5);
            var rules = new SkirmishPlanningRules();
            var session = new Warband.Run.PlanningSession(draft, rules);

            Assert.Equal(3, session.Current.FieldCount);
            Assert.Equal(1, session.Current.BenchCount);
            Assert.Equal(5, session.Current.BenchCapacity);
            Assert.True(session.ValidateForCommit().IsValid);
        }

        [Fact]
        public void RenamedEncounterUnitsKeepTheirChassisIdentity()
        {
            // The Last Oath renames both enemies ("Oathbound Bulwark"). Display names are authored
            // flavor and will drift; ChassisId is what art and tells key on, so it must survive the
            // rename — and Preview must be able to state each enemy's real reach.
            var encounter = Encounters.BondedPair();
            var byChassis = encounter.Enemies.ToDictionary(e => e.Def.ChassisId, e => e.Def);

            Assert.Equal(new[] { "bulwark", "sharpshot" }, byChassis.Keys.OrderBy(k => k));
            Assert.Equal("Oathbound Bulwark", byChassis["bulwark"].Name);
            Assert.Equal("Oathbound Sharpshot", byChassis["sharpshot"].Name);

            // The threat the formation actually poses: the archer outranges the tank.
            Assert.True(byChassis["sharpshot"].Range > byChassis["bulwark"].Range);
            Assert.All(encounter.Enemies, e => Assert.False(string.IsNullOrEmpty(e.Def.WeaponName)));
        }

        [Fact]
        public void EveryComposedHeroReachesTheSnapshotWithItsIdentity()
        {
            // Nothing in the catalog may reach deployment as an anonymous stat block.
            foreach (var pair in Kits.Chassis)
            {
                var def = Loadout.Compose(pair.Value).Def;
                Assert.Equal(pair.Key, def.ChassisId);
                Assert.False(string.IsNullOrEmpty(def.WeaponName));

                var view = PlaybackUnit.From(Loadout.Spawn(0, 0, new ComposedLoadout { Def = def }, Hex.FromRowCol(0, 0)));
                Assert.Equal(pair.Key, view.ChassisId);
                Assert.True(view.Range >= 1);
                Assert.True(view.MoveInterval >= 1);
            }
        }

        [Fact]
        public void EveryWeaponHasInspectableMasteryCopy()
        {
            Assert.Equal(Weapons.All.Count, SkirmishProof.MasteryCopy.Count);
            Assert.All(Weapons.All, pair =>
            {
                var copy = SkirmishProof.MasteryCopy[pair.Key];
                Assert.False(string.IsNullOrWhiteSpace(copy.Name));
                Assert.False(string.IsNullOrWhiteSpace(copy.Text));
            });
        }
    }
}
