using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The content half. `Traits` now flows to the client on every snapshot, so an un-authored
    /// node id would render as "cleric.warpriest.conflagration" in a tooltip — these tests are
    /// what make that impossible to ship.
    /// </summary>
    public class ContentLexiconTests
    {
        [Fact]
        public void EverySpecNodeHasCopy()
        {
            var missing = Kits.Nodes.Keys.Where(id => !ContentLexicon.Nodes.ContainsKey(id)).ToList();
            Assert.True(missing.Count == 0, $"spec nodes without copy: {string.Join(", ", missing)}");
        }

        [Fact]
        public void EveryChassisHasCopy()
        {
            var missing = Kits.Chassis.Keys.Where(id => !ContentLexicon.Chassis_.ContainsKey(id)).ToList();
            Assert.True(missing.Count == 0, $"chassis without copy: {string.Join(", ", missing)}");
        }

        [Fact]
        public void NoCopyIsOrphaned()
        {
            // The other direction: copy for a node that no longer exists is stale text that will
            // quietly drift out of sync with the catalog.
            var orphans = ContentLexicon.Nodes.Keys.Where(id => !Kits.Nodes.ContainsKey(id)).ToList();
            Assert.True(orphans.Count == 0, $"copy for nonexistent nodes: {string.Join(", ", orphans)}");
        }

        [Fact]
        public void NoDisplayNameLeaksAnId()
        {
            // The whole point: an id is dotted and lowercase; a display name is neither.
            foreach (var pair in ContentLexicon.Nodes)
            {
                Assert.False(string.IsNullOrWhiteSpace(pair.Value.Name));
                Assert.False(string.IsNullOrWhiteSpace(pair.Value.Text));
                Assert.DoesNotContain(".", pair.Value.Name);
                Assert.NotEqual(pair.Key, pair.Value.Name);
                Assert.True(char.IsUpper(pair.Value.Name[0]), $"{pair.Key} name is not title-cased");
                Assert.EndsWith(".", pair.Value.Text);
            }
        }

        [Fact]
        public void NodeNamesAreDistinct()
        {
            var dupes = ContentLexicon.Nodes.Values
                .GroupBy(e => e.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            Assert.True(dupes.Count == 0, $"duplicate node display names: {string.Join(", ", dupes)}");
        }

        [Fact]
        public void EveryTraitOnAComposedUnitResolves()
        {
            // End-to-end: walk every chassis' real offer tree, compose it, and prove that every
            // trait that reaches the snapshot can be hydrated. This is the path the client takes.
            foreach (var chassisId in Kits.Chassis.Keys)
            {
                var nodes = Kits.Nodes
                    .Where(n => n.Key.StartsWith(chassisId + "."))
                    .Select(n => n.Value)
                    .ToList();
                var def = Loadout.Compose(Kits.Chassis[chassisId], nodes: nodes).Def;

                Assert.NotEmpty(def.Traits);
                foreach (var trait in def.Traits)
                {
                    var entry = ContentLexicon.Node(trait);
                    Assert.NotEqual(trait, entry.Name);   // a fallback returns the raw id as the name
                    Assert.False(entry.IsEmpty);
                }
                Assert.False(ContentLexicon.Chassis(def.ChassisId).IsEmpty);
            }
        }

        [Fact]
        public void UnknownIdFallsBackInsteadOfThrowing()
        {
            var entry = ContentLexicon.Node("cleric.doesnotexist");
            Assert.Equal("cleric.doesnotexist", entry.Name);
            Assert.Equal("", entry.Text);
        }
    }
}
