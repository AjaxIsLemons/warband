using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>The client's tell-dispatch brain: rules match on kind + optional cause/status,
    /// and the most specific rule wins (generic tell is the fallback).</summary>
    public class TellMatchTests
    {
        private static BattleEvent Dmg(Cause cause, bool crit = false)
            => new BattleEvent { Kind = EventKind.DamageDealt, Cause = cause, Crit = crit };
        private static BattleEvent Status(EventKind kind, StatusKind k)
            => new BattleEvent { Kind = kind, Aux = (int)k };
        private static BattleEvent Field(FieldFlavor flavor, bool wall = false)
            => new BattleEvent { Kind = EventKind.FieldCreated, Amount = wall ? 1 : 0, Aux3 = (int)flavor };
        private static BattleEvent Atk()
            => new BattleEvent { Kind = EventKind.Attack };

        [Fact]
        public void KindOnlyRuleMatchesAnyCause()
        {
            Assert.True(TellMatch.Matches(Dmg(Cause.Attack), EventKind.DamageDealt, null, null));
            Assert.True(TellMatch.Matches(Dmg(Cause.Burn), EventKind.DamageDealt, null, null));
            Assert.False(TellMatch.Matches(Dmg(Cause.Attack), EventKind.Heal, null, null));
        }

        [Fact]
        public void CauseFilterNarrows()
        {
            Assert.True(TellMatch.Matches(Dmg(Cause.Burn), EventKind.DamageDealt, Cause.Burn, null));
            Assert.False(TellMatch.Matches(Dmg(Cause.Attack), EventKind.DamageDealt, Cause.Burn, null));
        }

        [Fact]
        public void StatusFilterMatchesTheAppliedKind()
        {
            Assert.True(TellMatch.Matches(Status(EventKind.StatusApplied, StatusKind.Taunt), EventKind.StatusApplied, null, StatusKind.Taunt));
            Assert.False(TellMatch.Matches(Status(EventKind.StatusApplied, StatusKind.Burn), EventKind.StatusApplied, null, StatusKind.Taunt));
            // a status filter on a non-status event never matches
            Assert.False(TellMatch.Matches(Dmg(Cause.Attack), EventKind.DamageDealt, null, StatusKind.Taunt));
        }

        [Fact]
        public void FieldFilterNarrowsByFlavor()
        {
            // the generic Field tell still catches every zone…
            Assert.True(TellMatch.Matches(Field(FieldFlavor.Hazard), EventKind.FieldCreated, null, null));
            Assert.True(TellMatch.Matches(Field(FieldFlavor.Boon), EventKind.FieldCreated, null, null));
            // …while a flavored rule takes only its own
            Assert.True(TellMatch.Matches(Field(FieldFlavor.Boon), EventKind.FieldCreated, null, null, FieldFlavor.Boon));
            Assert.False(TellMatch.Matches(Field(FieldFlavor.Hazard), EventKind.FieldCreated, null, null, FieldFlavor.Boon));
        }

        [Fact]
        public void FieldFilterNeverMatchesNonFieldEvents()
        {
            // FieldHex/FieldExpired reference a zone by id and carry no flavor of their own
            var hex = new BattleEvent { Kind = EventKind.FieldHex, Target = 3 };
            Assert.False(TellMatch.Matches(hex, EventKind.FieldHex, null, null, FieldFlavor.Hazard));
            Assert.False(TellMatch.Matches(Dmg(Cause.Attack), EventKind.DamageDealt, null, null, FieldFlavor.Hazard));
        }

        [Fact]
        public void SpecificityCountsDeclaredFilters()
        {
            Assert.Equal(0, TellMatch.Specificity(null, null));
            Assert.Equal(1, TellMatch.Specificity(Cause.Burn, null));
            Assert.Equal(1, TellMatch.Specificity(null, StatusKind.Taunt));
            Assert.Equal(2, TellMatch.Specificity(Cause.Dot, StatusKind.Burn));
            // a flavored field rule outranks the bare Field fallback
            Assert.Equal(0, TellMatch.Specificity(null, null, null));
            Assert.Equal(1, TellMatch.Specificity(null, null, FieldFlavor.Debuff));
        }

        [Fact]
        public void RangedFilterMatchesAtDistanceTwoOrMore()
        {
            // ranged := hex distance ≥2 (the sim's projectile law, Battle.cs:254)
            Assert.True(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: true, distance: 2));
            Assert.True(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: true, distance: 5));
            Assert.False(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: true, distance: 1));
        }

        [Fact]
        public void MeleeFilterMatchesAdjacentOnly()
        {
            Assert.True(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: false, distance: 1));
            Assert.False(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: false, distance: 3));
        }

        [Fact]
        public void RangedFilterNeverMatchesWithoutDistanceContext()
        {
            // events with no two unit endpoints pass distance=null; a ranged rule can't apply
            Assert.False(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: true, distance: null));
            Assert.False(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: false, distance: null));
        }

        [Fact]
        public void FilterlessRuleIgnoresDistance()
        {
            // fallback behavior unchanged: a rule with no ranged filter matches at any distance
            Assert.True(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: null, distance: 1));
            Assert.True(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: null, distance: 5));
            Assert.True(TellMatch.Matches(Atk(), EventKind.Attack, null, null, null, ranged: null, distance: null));
        }

        [Fact]
        public void SpecificityCountsRanged()
        {
            // a ranged rule outranks the bare fallback…
            Assert.Equal(0, TellMatch.Specificity(null, null));
            Assert.Equal(1, TellMatch.Specificity(null, null, null, ranged: true));
            // …and stacks with other filters, so cause+ranged beats ranged alone
            Assert.Equal(2, TellMatch.Specificity(Cause.Attack, null, null, ranged: true));
        }

        private static BattleEvent Cast() => new BattleEvent { Kind = EventKind.Cast, Cause = Cause.Ability };

        [Fact]
        public void ChassisFilterKeysOnTheSourceChassis()
        {
            // the caster's ChassisId is view context from the fold, exactly like distance
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                chassis: "pyromancer", sourceChassis: "pyromancer"));
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                chassis: "pyromancer", sourceChassis: "cleric"));
            // case-insensitive: content ids are lowercase but authored JSON shouldn't die on case
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                chassis: "Pyromancer", sourceChassis: "pyromancer"));
        }

        [Fact]
        public void ChassisFilterNeverMatchesWithoutSourceContext()
        {
            // same law as ranged: a chassis-specific look must not fire for an unknown caster
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                chassis: "pyromancer", sourceChassis: null));
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                chassis: "pyromancer", sourceChassis: ""));
        }

        [Fact]
        public void FilterlessRuleIgnoresChassis()
        {
            // the fallback Cast tell still catches every caster
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                sourceChassis: "pyromancer"));
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                chassis: ""));
        }

        [Fact]
        public void SpecificityCountsChassis()
        {
            // a chassis cast tell outranks the filterless Cast fallback
            Assert.Equal(0, TellMatch.Specificity(null, null, null, null, null));
            Assert.Equal(1, TellMatch.Specificity(null, null, null, null, "pyromancer"));
            Assert.Equal(0, TellMatch.Specificity(null, null, null, null, ""));
        }

        [Fact]
        public void AbilityFilterKeysOnTheResolvedSourceAbility()
        {
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                ability: "pyro.starfall", sourceAbility: "pyro.starfall"));
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                ability: "pyro.starfall", sourceAbility: "pyromancer"));
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                ability: "Pyro.Starfall", sourceAbility: "pyro.starfall"));
        }

        [Fact]
        public void AbilityFilterNeverMatchesWithoutSourceContext()
        {
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                ability: "pyro.starfall", sourceAbility: null));
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                ability: "pyro.starfall", sourceAbility: ""));
            // the filterless fallback still catches an event that HAS ability context
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, null, null,
                sourceAbility: "pyro.starfall"));
        }

        [Fact]
        public void AbilityAndChassisFiltersCompose()
        {
            // both declared: both must hold
            Assert.True(TellMatch.Matches(Cast(), EventKind.Cast, Cause.Ability, null,
                chassis: "pyromancer", sourceChassis: "pyromancer",
                ability: "pyro.starfall", sourceAbility: "pyro.starfall"));
            Assert.False(TellMatch.Matches(Cast(), EventKind.Cast, Cause.Ability, null,
                chassis: "cleric", sourceChassis: "pyromancer",
                ability: "pyro.starfall", sourceAbility: "pyro.starfall"));
        }

        [Fact]
        public void AbilityOutranksChassisInSpecificity()
        {
            // the whole point of the +2: an override's tell must beat its own chassis tell
            // rather than tying it and losing to whichever row was registered first.
            Assert.Equal(2, TellMatch.Specificity(null, null, null, null, null, "pyro.starfall"));
            Assert.True(TellMatch.Specificity(null, null, null, null, null, "pyro.starfall")
                      > TellMatch.Specificity(null, null, null, null, "pyromancer"));
            Assert.Equal(3, TellMatch.Specificity(null, null, null, null, "pyromancer", "pyro.starfall"));
            Assert.Equal(0, TellMatch.Specificity(null, null, null, null, null, ""));
        }

        private static BattleEvent Swing() => new BattleEvent { Kind = EventKind.Attack, Cause = Cause.Attack };

        [Fact]
        public void WeaponFilterKeysOnTheSourceWeapon()
        {
            // WeaponName off the fold's identity block — same class of view context as chassis
            Assert.True(TellMatch.Matches(Swing(), EventKind.Attack, null, null,
                weapon: "Greataxe", sourceWeapon: "Greataxe"));
            Assert.False(TellMatch.Matches(Swing(), EventKind.Attack, null, null,
                weapon: "Greataxe", sourceWeapon: "Twin Daggers"));
            // case-insensitive, like every other string filter: authored JSON shouldn't die on case
            Assert.True(TellMatch.Matches(Swing(), EventKind.Attack, null, null,
                weapon: "greataxe", sourceWeapon: "Greataxe"));
        }

        [Fact]
        public void WeaponFilterNeverMatchesWithoutSourceContext()
        {
            // the view-context law: a Musket's smoke line must not fire for an unarmed unknown
            Assert.False(TellMatch.Matches(Swing(), EventKind.Attack, null, null,
                weapon: "Matchlock Musket", sourceWeapon: null));
            Assert.False(TellMatch.Matches(Swing(), EventKind.Attack, null, null,
                weapon: "Matchlock Musket", sourceWeapon: ""));
            // the filterless fallback still catches an event that HAS weapon context
            Assert.True(TellMatch.Matches(Swing(), EventKind.Attack, null, null,
                sourceWeapon: "Matchlock Musket"));
        }

        [Fact]
        public void WeaponComposesWithCauseAndChassis()
        {
            // every declared filter must hold — a cause mismatch kills a matching weapon
            Assert.True(TellMatch.Matches(Swing(), EventKind.Attack, Cause.Attack, null,
                chassis: "berserker", sourceChassis: "berserker",
                weapon: "Greataxe", sourceWeapon: "Greataxe"));
            Assert.False(TellMatch.Matches(Swing(), EventKind.Attack, Cause.Trigger, null,
                weapon: "Greataxe", sourceWeapon: "Greataxe"));
            Assert.False(TellMatch.Matches(Swing(), EventKind.Attack, Cause.Attack, null,
                chassis: "shade", sourceChassis: "berserker",
                weapon: "Greataxe", sourceWeapon: "Greataxe"));
        }

        [Fact]
        public void WeaponTiesChassisInSpecificity()
        {
            // weapon is a PEER of chassis at +1, not a narrower filter like ability: any hero may
            // carry any weapon, so neither contains the other. The tie falling to registry order is
            // the documented, deliberate contract — assert it so a future bump is a decision.
            Assert.Equal(1, TellMatch.Specificity(null, null, null, null, null, null, "Greataxe"));
            Assert.Equal(TellMatch.Specificity(null, null, null, null, "berserker"),
                         TellMatch.Specificity(null, null, null, null, null, null, "Greataxe"));
            // and it still stacks, so weapon+chassis beats either alone
            Assert.Equal(2, TellMatch.Specificity(null, null, null, null, "berserker", null, "Greataxe"));
            // ability remains strictly above a bare weapon row
            Assert.True(TellMatch.Specificity(null, null, null, null, null, "pyro.starfall")
                      > TellMatch.Specificity(null, null, null, null, null, null, "Greataxe"));
            Assert.Equal(0, TellMatch.Specificity(null, null, null, null, null, null, ""));
        }
    }
}
