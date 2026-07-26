using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Conversion fidelity for the 12 nodes that moved from SignatureOverride to SignaturePatch
    /// (2026-07-25 systems review §4). Each of those nodes used to restate an entire effect list to
    /// change one number; these tests assert the composed result is the same signature it always
    /// was — and, for the Lancer chain, that it is finally the signature the dive PROMISED.
    /// </summary>
    public class SignatureCompositionTests
    {
        private static List<EffectDef> Sig(string chassis, params string[] nodes) =>
            Loadout.Compose(Kits.Chassis[chassis], nodes: nodes.Select(n => Kits.Nodes[n])).Def.Signature;

        [Fact]
        public void WarPriestGrowsThePyreAndAddsBurn()
        {
            var sig = Sig("cleric", "cleric.warpriest");

            // Same three effects as the old override. The Burn now lands last rather than in the
            // middle — inert, because it targets enemies and the heal it swapped with targets allies.
            Assert.Equal(3, sig.Count);
            Assert.Equal(EffectKind.Damage, sig[0].Kind);
            Assert.Equal(2, sig[0].Select.Range);
            Assert.Equal(12, sig[0].Amount);
            Assert.Equal(EffectKind.Heal, sig[1].Kind);
            Assert.Equal(2, sig[1].Select.Range);
            Assert.False(sig[1].Select.ExcludeSelf);   // the Pyre still heals her
            Assert.Equal(EffectKind.ApplyStatus, sig[2].Kind);
            Assert.Equal(StatusKind.Burn, sig[2].Status);
            Assert.Equal(3, sig[2].Amount);
        }

        [Fact]
        public void GreatChorusFiresThePulseTwice()
        {
            var one = Sig("cleric", "cleric.lifebinder");
            var two = Sig("cleric", "cleric.lifebinder", "cleric.lifebinder.greatchorus");

            Assert.Equal(2, one.Count);
            Assert.Equal(4, two.Count);
            Assert.Equal(new[] { EffectKind.Heal, EffectKind.ApplyStatus, EffectKind.Heal, EffectKind.ApplyStatus },
                         two.Select(e => e.Kind));
            Assert.All(two.Where(e => e.Kind == EffectKind.Heal), e => Assert.Equal(18, e.Amount));
        }

        [Fact]
        public void FaultlineWidensTheSlamByOneRing()
        {
            var jug = Sig("bulwark", "bulwark.juggernaut");
            var fault = Sig("bulwark", "bulwark.juggernaut", "bulwark.juggernaut.faultline");

            Assert.All(jug, e => Assert.Equal(1, e.Select.Range));
            Assert.All(fault, e => Assert.Equal(2, e.Select.Range));
            Assert.Equal(10, fault[0].Amount);                     // damage untouched
            Assert.Equal(15, fault[1].StatusTicks);                // Stun duration untouched
        }

        [Fact]
        public void ChallengeWidensTheTauntAndHeavensTheShield()
        {
            var sig = Sig("bulwark", "bulwark.warden", "bulwark.warden.challenge");

            Assert.Equal(StatusKind.Taunt, sig[0].Status);
            Assert.Equal(4, sig[0].Select.Range);
            Assert.Equal(0, sig[0].Amount);                        // Taunt has no magnitude to scale
            Assert.Equal(EffectKind.GrantShield, sig[1].Kind);
            Assert.Equal(SelKind.Self, sig[1].Select.Kind);        // Self is not a radius selector
            Assert.Equal(35, sig[1].Amount);                       // 20 × 175%
        }

        [Fact]
        public void OverpenetrationEscalatesTheSnipersLine()
        {
            var plain = Sig("sharpshot", "sharpshot.sniper");
            var over = Sig("sharpshot", "sharpshot.sniper", "sharpshot.sniper.overpen");

            Assert.Equal(0, plain[0].EscalatePctPerIndex);
            Assert.Equal(25, over[0].EscalatePctPerIndex);
            Assert.Equal(35, over[0].Amount);
            Assert.Equal(SelKind.EnemiesOnLineThroughFarthest, over[0].Select.Kind);
        }

        [Fact]
        public void InfernoOwnsTheRadiusAndEverburnOwnsTheDuration()
        {
            var inferno = Sig("pyromancer", "pyromancer.inferno");
            Assert.Equal(2, inferno[0].Field!.Radius);
            Assert.Equal(80, inferno[0].Field!.Ticks);

            var ever = Sig("pyromancer", "pyromancer.inferno", "pyromancer.inferno.everburn");
            Assert.Equal(2, ever[0].Field!.Radius);    // inherited, not restated
            Assert.Equal(-1, ever[0].Field!.Ticks);    // rest of the fight

            // And the chassis' own glyph is still the small one — the catalog was not mutated.
            Assert.Equal(1, Sig("pyromancer")[0].Field!.Radius);
        }

        /// <summary>
        /// The headline fix. Sarissa used to REPLACE Deep Thrust's signature, so "Breach the Line"
        /// silently kept the board-length lunge and dropped the escalation the A node was picked
        /// for — a crown that ate its own amplifier.
        /// </summary>
        [Fact]
        public void BreachTheLineIsBoardLengthAndEscalating()
        {
            var lancer = Sig("phalanx", "phalanx.lancer");
            Assert.Equal(4, lancer[0].Select.Range);
            Assert.Equal(0, lancer[0].EscalatePctPerIndex);

            var deep = Sig("phalanx", "phalanx.lancer", "phalanx.lancer.deepthrust");
            Assert.Equal(4, deep[0].Select.Range);
            Assert.Equal(30, deep[0].EscalatePctPerIndex);

            var breach = Sig("phalanx", "phalanx.lancer", "phalanx.lancer.deepthrust", "phalanx.lancer.sarissa");
            Assert.Equal(0, breach[0].Select.Range);            // board-length, from the crown
            Assert.Equal(30, breach[0].EscalatePctPerIndex);    // AND still escalating

            // Sarissa without Deep Thrust is still just the long lunge — the crown adds no
            // escalation of its own.
            var plainSarissa = Sig("phalanx", "phalanx.lancer", "phalanx.lancer.sarissa");
            Assert.Equal(0, plainSarissa[0].Select.Range);
            Assert.Equal(0, plainSarissa[0].EscalatePctPerIndex);
        }

        [Fact]
        public void BannerForksAppendToRallyRatherThanRestatingIt()
        {
            var rally = Sig("banneret");
            Assert.Single(rally);
            Assert.Equal(EffectKind.GrantMana, rally[0].Kind);

            var herald = Sig("banneret", "banneret.herald");
            Assert.Equal(2, herald.Count);
            Assert.Equal(EffectKind.GrantMana, herald[0].Kind);
            Assert.Equal(EffectKind.GrantShield, herald[1].Kind);
            Assert.Equal(10, herald[1].Amount);

            var warcaller = Sig("banneret", "banneret.warcaller");
            Assert.Equal(2, warcaller.Count);
            Assert.Equal(StatusKind.Slow, warcaller[1].Status);
            Assert.Equal(25, warcaller[1].StatusTicks);
        }

        /// <summary>Every node that changes the cast — override or patch — must still own its
        /// ability id, or the renderer's byAbility cast tells fall back to the chassis and a
        /// board-length Sarissa lunge draws itself as a stock Skewer.</summary>
        [Fact]
        public void PatchedNodesStillClaimAbilityIdentity()
        {
            var cases = new (string Chassis, string[] Nodes, string Expected)[]
            {
                ("cleric", new[] { "cleric.warpriest" }, "cleric.warpriest"),
                ("pyromancer", new[] { "pyromancer.inferno", "pyromancer.inferno.everburn" }, "pyromancer.inferno.everburn"),
                ("phalanx", new[] { "phalanx.lancer", "phalanx.lancer.deepthrust", "phalanx.lancer.sarissa" }, "phalanx.lancer.sarissa"),
                ("banneret", new[] { "banneret.warcaller" }, "banneret.warcaller"),
                ("bulwark", new[] { "bulwark.juggernaut" }, "bulwark.juggernaut"),
                ("berserker", System.Array.Empty<string>(), "berserker"),   // no signature change at all
            };

            foreach (var (chassis, nodes, expected) in cases)
            {
                var def = Loadout.Compose(Kits.Chassis[chassis], nodes: nodes.Select(n => Kits.Nodes[n])).Def;
                Assert.Equal(expected, AbilityIdentity.Resolve(def.ChassisId, def.Traits));
            }
        }
    }
}
