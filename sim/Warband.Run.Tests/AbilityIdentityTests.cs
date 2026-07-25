using System.Collections.Generic;
using Warband.Content;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>Which ability a composed unit actually casts: the LAST spec node with a
    /// SignatureOverride, mirroring Loadout.Compose's replace-the-signature law.</summary>
    public class AbilityIdentityTests
    {
        [Fact]
        public void StockKitResolvesToItsChassis()
        {
            Assert.Equal("pyromancer", AbilityIdentity.Resolve("pyromancer", new List<string>()));
        }

        [Fact]
        public void AnOverrideNodeBecomesTheAbility()
        {
            Assert.Equal("pyromancer.inferno",
                AbilityIdentity.Resolve("pyromancer", new List<string> { "pyromancer.inferno" }));
        }

        [Fact]
        public void TheLastOverrideWins()
        {
            // Compose clears and replaces the signature per override node, so a later one
            // erases the earlier — the resolved identity has to agree.
            Assert.Equal("pyromancer.inferno.everburn", AbilityIdentity.Resolve("pyromancer",
                new List<string> { "pyromancer.inferno", "pyromancer.inferno.everburn" }));
        }

        [Fact]
        public void PassiveNodesAndTrinketNamesAreIgnored()
        {
            // Trinket traits are display names, not node ids, so they miss the dictionary;
            // chokingsmoke IS a node but overrides nothing, so neither moves the answer.
            Assert.Equal("pyromancer.inferno", AbilityIdentity.Resolve("pyromancer", new List<string>
            {
                "Cracked Hourglass", "pyromancer.inferno", "pyromancer.inferno.chokingsmoke",
            }));
        }

        [Fact]
        public void DisplayNameReadsTheContentLexicon()
        {
            Assert.Equal("Pyromancer", AbilityIdentity.DisplayName("pyromancer"));
            Assert.Equal(ContentLexicon.Node("pyromancer.inferno").Name,
                AbilityIdentity.DisplayName("pyromancer.inferno"));
            Assert.Equal("mystery.ability", AbilityIdentity.DisplayName("mystery.ability"));
        }
    }
}
