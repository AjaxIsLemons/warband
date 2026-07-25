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
    }
}
