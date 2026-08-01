using System.Collections.Generic;
using System.Linq;
using System;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    public sealed class MechanicalRulePresenterTests
    {
        [Fact]
        public void EveryNodeHasExactNonFallbackMechanicalCopy()
        {
            foreach (var pair in Kits.Nodes)
            {
                MechanicalRule rule = MechanicalRulePresenter.Node(pair.Value);
                Assert.False(string.IsNullOrWhiteSpace(rule.Compact), pair.Key);
                Assert.False(string.IsNullOrWhiteSpace(rule.Full), pair.Key);
                Assert.DoesNotContain("unsupported", rule.Full.ToLowerInvariant());
            }
        }

        [Fact]
        public void EveryTrinketAndInscriptionHasExactMechanicalCopy()
        {
            foreach (var pair in Catalog.Trinkets)
            {
                MechanicalRule rule = MechanicalRulePresenter.Trinket(pair.Value);
                Assert.False(string.IsNullOrWhiteSpace(rule.Full), pair.Key);
            }
            foreach (var pair in Catalog.Inscriptions)
            {
                MechanicalRule rule = MechanicalRulePresenter.Inscription(pair.Value);
                Assert.False(string.IsNullOrWhiteSpace(rule.Full), pair.Key);
            }
        }

        [Fact]
        public void EveryWeaponShowsManaAndHasMasteryCopy()
        {
            foreach (var pair in Weapons.All)
            {
                Assert.True(pair.Value.ManaPerSwing > 0, pair.Key);
                MechanicalRule mastery = MechanicalRulePresenter.WeaponMastery(pair.Value);
                Assert.False(string.IsNullOrWhiteSpace(mastery.Full), pair.Key);
            }
        }

        [Fact]
        public void SelfBuffGrammarIsShortAndNamesTheAffectedAttacks()
        {
            string copy =
                MechanicalRulePresenter.WeaponMastery(Weapons.All["sabre"]).Full;

            Assert.Equal(
                "After this champion casts their signature: " +
                "Gain Sure Strike for the next basic attack.",
                copy);
        }

        [Fact]
        public void GreataxeMasteryNamesTheExactOverkillDestination()
        {
            Assert.Equal(
                "On kill: Deal excess damage to the enemy nearest the corpse.",
                MechanicalRulePresenter.WeaponMastery(Weapons.All["greataxe"]).Full);
        }

        [Fact]
        public void GreataxeMasteryCopyTracksItsAuthoredPercentage()
        {
            Trigger source = Weapons.All["greataxe"].MasteryTriggers.Single();
            var changed = new Trigger { On = source.On };
            changed.When.AddRange(source.When);
            EffectDef effect = source.Do.Single().Clone();
            effect.PctOfEventAmount = 60;
            changed.Do.Add(effect);
            var weapon = new WeaponDef { Name = "Test Greataxe" };
            weapon.MasteryTriggers.Add(changed);

            Assert.Equal(
                "On kill: Deal 60% of excess damage to the enemy nearest the corpse.",
                MechanicalRulePresenter.WeaponMastery(weapon).Full);
        }

        [Fact]
        public void PikeMasteryStatesTheTotalAttackBonus()
        {
            Assert.Equal(
                "Basic attacks deal +30% damage to enemies adjacent to an ally.",
                MechanicalRulePresenter.WeaponMastery(Weapons.All["pike"]).Full);
        }

        [Fact]
        public void PhalanxRiposteIsOneRuleInsteadOfThreeEngineTriggers()
        {
            UnitDef phalanx = Loadout.Compose(Kits.Chassis["phalanx"]).Def;

            Assert.Equal(
                "Combat start or Signature cast: Gain 1 Riposte.",
                PlayerRuleProjection.Champion(phalanx).PassiveText);
            Assert.Equal(
                "Combat start or Signature cast: Allies within 1 hex gain 1 Riposte.",
                MechanicalRulePresenter.Node(
                    Kits.Nodes["phalanx.pikewall.unbrokenline"]).Full);
            Assert.Contains(
                "RIPOSTE · Spend 1 to Counter an incoming basic attack.",
                PlayerRuleProjection.Keywords(phalanx));
        }

        [Fact]
        public void RiposteCopyTracksTheAuthoredSpendCount()
        {
            var changed = Kits.Chassis["phalanx"].Passives.Select(source =>
            {
                var trigger = new Trigger
                {
                    On = source.On,
                    EveryN = source.EveryN,
                    OncePerRoot = source.OncePerRoot,
                    RuleId = source.RuleId,
                };
                trigger.When.AddRange(source.When);
                trigger.Do.AddRange(source.Do.Select(effect => effect.Clone()));
                return trigger;
            }).ToList();
            changed.Single(trigger => trigger.On == EventKind.Attack)
                .Do.Single(effect => effect.Kind == EffectKind.RemoveStatus)
                .Amount = 2;

            string copy = MechanicalRulePresenter.Passives(changed);

            Assert.NotEqual(
                "Combat start or Signature cast: Gain 1 Riposte.",
                copy);
            Assert.Contains("Spend 2 Riposte", copy);
        }

        [Fact]
        public void MechanicalCopyChangesWhenMagnitudeChanges()
        {
            var weak = new TrinketDef { Name = "Test", HpBonus = 10 };
            var strong = new TrinketDef { Name = "Test", HpBonus = 20 };
            Assert.NotEqual(
                MechanicalRulePresenter.Trinket(weak).Full,
                MechanicalRulePresenter.Trinket(strong).Full);
        }

        [Theory]
        [InlineData("cleric", 4)]
        [InlineData("bulwark", 2)]
        [InlineData("shade", 7)]
        [InlineData("sharpshot", 5)]
        [InlineData("pyromancer", 4)]
        [InlineData("berserker", 3)]
        [InlineData("phalanx", 4)]
        [InlineData("banneret", 3)]
        public void OpeningDraftCastCycleComesFromComposedWeaponMana(
            string chassisId, int expectedAttacks)
        {
            UnitDef unit = Loadout.Compose(Kits.Chassis[chassisId]).Def;

            Assert.Equal(expectedAttacks,
                MechanicalRulePresenter.BasicAttacksToSignature(unit));
        }

        [Fact]
        public void EveryOpeningChampionHasExactSignatureAndPassiveLanguage()
        {
            foreach (var pair in Kits.Chassis)
            {
                UnitDef unit = Loadout.Compose(pair.Value).Def;
                string signature = MechanicalRulePresenter.Signature(unit.Signature);
                string passives = MechanicalRulePresenter.Passives(
                    pair.Value.Passives, pair.Value.StatRules);

                Assert.False(string.IsNullOrWhiteSpace(signature), pair.Key);
                Assert.False(string.IsNullOrWhiteSpace(passives), pair.Key);
                Assert.DoesNotContain("unsupported", signature.ToLowerInvariant());
                Assert.DoesNotContain("unsupported", passives.ToLowerInvariant());

                // Berserker's cast is authored as an on-cast innate rather than a direct effect.
                // The opening-draft projection moves that exact trigger into the Signature lens.
                if (pair.Key != "berserker")
                    Assert.NotEqual("No signature effect.", signature);
                Assert.NotEqual("No passive rule.", passives);
            }
        }

        [Fact]
        public void TriggerGrammarUsesChampionFacingLanguage()
        {
            string copy =
                MechanicalRulePresenter.Node(Kits.Nodes["berserker.bloodreaver"]).Full;

            Assert.Contains("After this champion deals basic-attack damage", copy);
            Assert.Contains("this champion has Frenzy", copy);
            Assert.Contains("60% of that damage", copy);
            Assert.DoesNotContain("source is the owner", copy);
            Assert.DoesNotContain("cause is Attack", copy);
        }

        [Fact]
        public void FieldPulseGrammarTargetsOccupantsInsteadOfItsAuthoringSelector()
        {
            var field = new FieldDef
            {
                Radius = 1,
                Ticks = 60,
                PulseAffects = Affects.Enemies,
            };
            field.Pulse.Add(new EffectDef
            {
                Kind = EffectKind.ApplyStatus,
                Status = StatusKind.Burn,
                Amount = 2,
                Select = new Selector { Kind = SelKind.Self },
                StatusTicks = -1,
            });
            var node = new SpecNode
            {
                Name = "Field",
                SignatureOverride = new List<EffectDef>
                {
                    new EffectDef
                    {
                        Kind = EffectKind.CreateField,
                        Select = new Selector { Kind = SelKind.CurrentTarget },
                        Field = field,
                    },
                },
            };

            string copy = MechanicalRulePresenter.Node(node).Full;

            Assert.Contains("field centered on the target for 6s; each second:", copy);
            Assert.Contains("Apply Burn 2 to every enemy in the field", copy);
            Assert.DoesNotContain("Apply Burn 2 to this champion", copy);
            Assert.DoesNotContain(". centered on", copy);
        }

        [Fact]
        public void ChoiceClassificationDistinguishesAddSwapAndDeepen()
        {
            var add = new SpecNode { Name = "Add", HpBonus = 10 };
            var swap = new SpecNode { Name = "Swap", TargetPref = TargetPref.Farthest };
            var deepen = new SpecNode
            {
                Name = "Deepen",
                SignaturePatch = new SignaturePatch { AmountPct = 125 },
            };

            Assert.Equal(MechanicalChangeKind.Add, MechanicalRulePresenter.Node(add).Change);
            Assert.Equal(MechanicalChangeKind.Swap, MechanicalRulePresenter.Node(swap).Change);
            Assert.Equal(MechanicalChangeKind.Deepen,
                MechanicalRulePresenter.Node(deepen).Change);
        }

        [Fact]
        public void AllAuthoredEnumsUsedByContentHaveGrammar()
        {
            // Walking the real catalog exercises every authored selector, condition, effect,
            // field, trigger, status and stat rule. This also catches future primitives that
            // content starts using without adding player-facing language.
            var output = Kits.Nodes.Values.Select(n => MechanicalRulePresenter.Node(n).Full)
                .Concat(Catalog.Trinkets.Values.Select(
                    t => MechanicalRulePresenter.Trinket(t).Full))
                .Concat(Catalog.Inscriptions.Values.Select(
                    b => MechanicalRulePresenter.Inscription(b).Full))
                .Concat(Weapons.All.Values.Select(
                    w => MechanicalRulePresenter.WeaponMastery(w).Full))
                .ToList();
            Assert.NotEmpty(output);
            Assert.All(output, text => Assert.EndsWith(".", text));
        }

        [Fact]
        public void EveryPlayerFacingRulePassesTheConciseLanguageAudit()
        {
            var failures = new List<string>();
            foreach (var pair in Kits.Chassis)
            {
                ChampionRuleProjection champion = PlayerRuleProjection.Champion(
                    Loadout.Compose(pair.Value).Def);
                failures.AddRange(Audit(
                    $"champion/{pair.Key}/signature", champion.SignatureText, 160));
                failures.AddRange(Audit(
                    $"champion/{pair.Key}/passive", champion.PassiveText, 160));
            }
            foreach (var pair in Weapons.All)
                failures.AddRange(Audit(
                    $"weapon/{pair.Key}",
                    MechanicalRulePresenter.WeaponMastery(pair.Value).Full, 140));
            foreach (var pair in Catalog.Trinkets)
                failures.AddRange(Audit(
                    $"trinket/{pair.Key}",
                    MechanicalRulePresenter.Trinket(pair.Value).Full, 180));
            foreach (var pair in Catalog.Inscriptions)
                failures.AddRange(Audit(
                    $"inscription/{pair.Key}",
                    MechanicalRulePresenter.Inscription(pair.Value).Full, 180));
            foreach (var pair in Kits.Nodes)
                failures.AddRange(Audit(
                    $"node/{pair.Key}",
                    MechanicalRulePresenter.Node(pair.Value).Full, 240));

            Assert.True(
                failures.Count == 0,
                "Player rule copy audit failed:\n" + string.Join("\n", failures));
        }

        [Fact]
        public void RuleCopyAuditRejectsEngineLanguageAndOversizeCopy()
        {
            string bad = "Deal 100% of the recorded overkill to the nearest enemy, " +
                         "measured from the triggering target, excluding the anchor unit.";

            List<string> failures = Audit("negative-control", bad, 40);

            Assert.Contains(failures, failure => failure.Contains("recorded overkill"));
            Assert.Contains(failures, failure => failure.Contains("triggering target"));
            Assert.Contains(failures, failure => failure.Contains("anchor unit"));
            Assert.Contains(failures, failure => failure.Contains("120 > 40"));
        }

        private static List<string> Audit(string id, string text, int maxLength)
        {
            var failures = new List<string>();
            foreach (string banned in new[]
                     {
                         "recorded overkill",
                         "triggering amount",
                         "triggering target",
                         "triggering source",
                         "anchor unit",
                         "99 hex",
                         "Deepen the inherited",
                     })
                if (text.IndexOf(banned, StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add($"{id}: contains '{banned}' · {text}");
            if (text.Length > maxLength)
                failures.Add($"{id}: {text.Length} > {maxLength} · {text}");
            return failures;
        }

    }
}
