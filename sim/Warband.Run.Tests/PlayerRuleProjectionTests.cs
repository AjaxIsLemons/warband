using System.Linq;
using System.Collections.Generic;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public sealed class PlayerRuleProjectionTests
    {
        private static UnitDef Compose(string chassisId, params string[] nodeIds) =>
            Loadout.Compose(
                Kits.Chassis[chassisId],
                nodes: nodeIds.Select(id => Kits.Nodes[id])).Def;

        [Fact]
        public void EveryChampionProjectsNamedExactRulesFromItsComposedDefinition()
        {
            foreach (var pair in Kits.Chassis)
            {
                ChampionRuleProjection rules =
                    PlayerRuleProjection.Champion(Compose(pair.Key));

                Assert.False(string.IsNullOrWhiteSpace(rules.SignatureName), pair.Key);
                Assert.False(string.IsNullOrWhiteSpace(rules.SignatureText), pair.Key);
                Assert.False(string.IsNullOrWhiteSpace(rules.PassiveName), pair.Key);
                Assert.False(string.IsNullOrWhiteSpace(rules.PassiveText), pair.Key);
                Assert.DoesNotContain("unsupported", rules.SignatureText.ToLowerInvariant());
                Assert.DoesNotContain("unsupported", rules.PassiveText.ToLowerInvariant());
            }
        }

        [Fact]
        public void ProjectionChangesWhenSignatureDataChanges()
        {
            UnitDef baseline = Compose("bulwark");
            UnitDef retuned = Compose("bulwark");
            retuned.Signature[0].Amount += 7;

            Assert.NotEqual(
                PlayerRuleProjection.Champion(baseline).SignatureText,
                PlayerRuleProjection.Champion(retuned).SignatureText);
        }

        [Fact]
        public void SharedTargetSignatureEffectsReadAsOneDecisionRule()
        {
            Assert.Equal(
                "Deal 10 damage to the nearest enemy, then apply Stun to it for 1s.",
                PlayerRuleProjection.Champion(Compose("bulwark")).SignatureText);
        }

        [Fact]
        public void PhalanxForkUsesExactDecisionCopy()
        {
            UnitDef before = Compose("phalanx");
            UnitDef pikewall = Compose("phalanx", "phalanx.pikewall");
            UnitDef lancer = Compose("phalanx", "phalanx.lancer");

            SpecializationRuleProjection wall = PlayerRuleProjection.Specialization(
                "phalanx", "phalanx.pikewall", before, pikewall);
            SpecializationRuleProjection line = PlayerRuleProjection.Specialization(
                "phalanx", "phalanx.lancer", before, lancer);

            Assert.Equal(
                "Gain an extra Counter against every basic attack targeting them. " +
                "When an enemy Leaps within 2 hexes, Counter and Taunt it for 4s.",
                wall.Choice);
            Assert.Equal("Skewer's line extends from 3 to 4 hexes.", line.Choice);
        }

        [Fact]
        public void TierIdentityComesFromAuthoredOfferRowsNotSelectionOrder()
        {
            Assert.Equal(
                Rank.B,
                PlayerRuleProjection.RankOf("shade", "shade.killerstempo"));
            Assert.Equal(
                Rank.A,
                PlayerRuleProjection.RankOf("shade", "shade.reaper"));

            var tiers = PlayerRuleProjection.Tiers("shade", "shade.reaper");
            Assert.Equal(new[] { Rank.B, Rank.A, Rank.S }, tiers.Select(tier => tier.Rank));
            Assert.False(tiers[0].IsFork);
            Assert.True(tiers[1].IsFork);
            Assert.All(tiers, tier => Assert.Equal(2, tier.OptionIds.Count));
        }

        [Fact]
        public void EveryLiveNodeHasExactlyOneAuthoredTier()
        {
            foreach (var pair in Kits.Nodes)
                Assert.InRange(
                    (int)PlayerRuleProjection.RankOf(
                        pair.Key.Substring(0, pair.Key.IndexOf('.')), pair.Key),
                    (int)Rank.B,
                    (int)Rank.S);
        }

        [Fact]
        public void EveryLiveChoiceIsDecisionSized()
        {
            var tooLong = new List<string>();
            foreach (var pair in Kits.Nodes)
            {
                string chassisId = pair.Key.Substring(0, pair.Key.IndexOf('.'));
                UnitDef before = Compose(chassisId);
                UnitDef after = Compose(chassisId, pair.Key);
                string choice = PlayerRuleProjection.Specialization(
                    chassisId, pair.Key, before, after).Choice;
                if (choice.Length > 180)
                    tooLong.Add($"{pair.Key} ({choice.Length}): {choice}");
            }

            Assert.True(
                tooLong.Count == 0,
                "Choice copy exceeded 180 characters:\n" + string.Join("\n", tooLong));
        }
    }
}
