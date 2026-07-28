using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The copy contract behind the in-fight inspector (roadmap item 21). The card itself only
    /// exists in Play Mode, which no session can reach — but everything it displays is resolved
    /// headlessly here, so the part that can actually rot is the part that is tested.
    ///
    /// The drift this guards: a spec node authored without a ContentLexicon entry would surface its
    /// raw id ("berserker.bloodreaver.redharvest") on a player-facing card. Weapons and trinkets are
    /// the deliberate exception — their Name IS display text.
    /// </summary>
    public class RuleCopyTests
    {
        private static IEnumerable<UnitDef> EveryComposedHero()
        {
            foreach (var chassisId in Kits.Chassis.Keys.OrderBy(k => k))
            {
                var chassis = Kits.Chassis[chassisId];
                // Bare chassis, and the chassis with every node it can legally take, at Relic so the
                // mastery rider is live too.
                yield return Loadout.Compose(chassis).Def;
                var nodes = Kits.Nodes.Values
                    .Where(n => n.Name.StartsWith(chassisId + ".", System.StringComparison.Ordinal))
                    .ToList();
                foreach (var node in nodes)
                    yield return Loadout.Compose(chassis, nodes: new[] { node },
                                                 tier: WeaponTier.Relic, mastered: true).Def;
            }
        }

        [Fact]
        public void EveryComposedRuleHasANameAndNoRawIdEscapes()
        {
            var unnamed = new List<string>();
            foreach (var def in EveryComposedHero())
                foreach (string id in def.Triggers.Select(t => t.RuleId)
                                        .Concat(def.StatRules.Select(r => r.RuleId)))
                {
                    Assert.False(string.IsNullOrEmpty(id), "the composer must stamp every rule");
                    var entry = ContentLexicon.Rule(id);
                    Assert.False(string.IsNullOrEmpty(entry.Name));

                    // A DOTTED id is a spec node or an authored rule — a namespace, never display
                    // text. If one of those reaches the card as its own id, it has no copy.
                    string bare = id.Split('#')[0];
                    if (bare.Contains('.') && entry.Name == bare) unnamed.Add(id);
                }

            Assert.True(unnamed.Count == 0,
                "these rule ids would render as raw ids on the inspector card: " +
                string.Join(", ", unnamed.Distinct()));
        }

        [Fact]
        public void AuthoredEnemyAndBannerRulesAreNamedToo()
        {
            // ADR 0024 promises these to the player by name, so they must not fall back.
            foreach (string id in new[] { "enemy.ward", "enemy.deathfed", "enemy.rooted",
                                          "enemy.emplaced", "enemy.ambush", "oath.bond",
                                          "crown.bell", "crown.emplaced" })
                Assert.NotEqual(id, ContentLexicon.Rule(id).Name);

            foreach (string key in Catalog.Inscriptions.Keys)
                Assert.Equal(Catalog.Inscriptions[key].Name, ContentLexicon.Rule("inscription." + key).Name);
        }

        [Fact]
        public void RuleCopyHandlesTheComposerSIdShapes()
        {
            Assert.Equal("Greataxe mastery", ContentLexicon.Rule("Greataxe/mastery").Name);
            Assert.Equal("Berserker", ContentLexicon.Rule("berserker").Name);
            // A weapon or trinket name is already display text and is passed through untouched.
            Assert.Equal("Twin Daggers", ContentLexicon.Rule("Twin Daggers").Name);
            // The "#2" suffix names WHICH rule from a source that contributes several.
            Assert.Equal("Berserker 2", ContentLexicon.Rule("berserker#2").Name);
            // Never throws, never empty.
            Assert.False(string.IsNullOrEmpty(ContentLexicon.Rule("").Name));
            Assert.False(string.IsNullOrEmpty(ContentLexicon.Rule("nonsense.made.up").Name));
        }

        [Fact]
        public void TheFoldsRuleSpansAddressTheRightRules()
        {
            // The inspector reads a unit's passives as a SPAN of the battle-wide table. If the
            // bases were off by even one, every card would name someone else's engine.
            var units = new List<UnitState>();
            int id = 1;
            foreach (var chassisId in new[] { "berserker", "sharpshot", "bulwark" })
            {
                var def = Loadout.Compose(Kits.Chassis[chassisId], tier: WeaponTier.Relic, mastered: true).Def;
                units.Add(new UnitState
                {
                    Id = id, Team = id % 2, Def = def, Hp = def.MaxHp,
                    Pos = Hex.FromRowCol(id % 2 == 0 ? 1 : 6, id),
                });
                id++;
            }
            var result = new Battle(units, seed: 5).Run();

            foreach (var view in result.InitialUnits)
            {
                var source = units.First(u => u.Id == view.Id);
                Assert.Equal(source.Def.Triggers.Count, view.TriggerRuleCount);
                Assert.Equal(source.Def.StatRules.Count, view.StatRuleCount);
                for (int i = 0; i < view.TriggerRuleCount; i++)
                    Assert.Equal(source.Def.Triggers[i].RuleId, result.RuleIds[view.TriggerRuleBase + i]);
                for (int i = 0; i < view.StatRuleCount; i++)
                    Assert.Equal(source.Def.StatRules[i].RuleId, result.RuleIds[view.StatRuleBase + i]);
            }
        }

        [Fact]
        public void TargetingRuleReachesTheFold()
        {
            // The Gunner's whole design is "acquires FARTHEST, holds standoff 5" — item 12's
            // complaint was that this was previewed as a name and a health number.
            var gunner = Enemies.Gunner();
            var units = new List<UnitState>
            {
                new UnitState { Id = 1, Team = 0, Def = gunner, Hp = gunner.MaxHp, Pos = Hex.FromRowCol(1, 1) },
                new UnitState { Id = 2, Team = 1, Def = Enemies.Hourling(), Hp = 70, Pos = Hex.FromRowCol(6, 1) },
            };
            var view = new Battle(units, seed: 2).Run().InitialUnits.First(u => u.Id == 1);

            Assert.Equal(TargetPref.Farthest, view.TargetPref);
            Assert.Equal(5, view.Standoff);
        }
    }
}
