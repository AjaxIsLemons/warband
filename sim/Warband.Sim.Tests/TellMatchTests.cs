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
        public void SpecificityCountsDeclaredFilters()
        {
            Assert.Equal(0, TellMatch.Specificity(null, null));
            Assert.Equal(1, TellMatch.Specificity(Cause.Burn, null));
            Assert.Equal(1, TellMatch.Specificity(null, StatusKind.Taunt));
            Assert.Equal(2, TellMatch.Specificity(Cause.Dot, StatusKind.Burn));
        }
    }
}
