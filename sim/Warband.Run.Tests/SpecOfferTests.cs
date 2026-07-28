using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// Variable-arity spec offers: content authors a POOL, the run layer draws the OFFER.
    ///
    /// The rule under test is the fork-rank law — the rank that decides what a hero IS always
    /// shows its whole pool, every other rank draws a seeded subset. That is what buys
    /// run-to-run variety without adding a card to read, and it is why a wider pool must never
    /// silently hide a path at the fork.
    /// </summary>
    public class SpecOfferTests
    {
        private static RunController ShopRun(ulong seed, StubContent content)
        {
            var run = new RunController(seed, content, Kit.Warband(), null);
            switch (run.CurrentNodeKind)
            {
                case NodeKind.Fight: run.ResolveFight(FightTier.Even, Kit.AutoPlace(run)); break;
                case NodeKind.Event: run.ResolveEvent(); break;
            }
            run.State.Gold = 200;
            return run;
        }

        private static StubContent Wide(int width) =>
            new StubContent { Heroes = new List<string> { "hero0" }, SpecPoolWidth = width };

        /// <summary>Rank a hero to <paramref name="upTo"/>, returning the offer shown at that rank.</summary>
        private static List<string> OfferAtRank(RunController run, Rank upTo, int pick = 0)
        {
            var hero = run.State.Field.First(h => h.ChassisId == "hero0");
            for (int slot = 0; ; slot++)
            {
                run.BuyOffer(slot);
                var options = new List<string>(run.State.PendingSpec!.Options);
                if (hero.Rank == upTo) return options;
                run.ChooseSpec(pick);
            }
        }

        [Fact]
        public void ForkRankOffersTheWholePool()
        {
            // Identity is never withheld: all three paths, in authored order.
            var run = ShopRun(7, Wide(3));
            Assert.Equal(new[] { "hero0-B-a", "hero0-B-b", "hero0-B-c" }, OfferAtRank(run, Rank.B));
        }

        [Fact]
        public void NonForkRankDrawsASubsetInAuthoredOrder()
        {
            var content = Wide(3);
            var run = ShopRun(7, content);
            var offer = OfferAtRank(run, Rank.A);

            Assert.Equal(2, offer.Count);
            var pool = content.SpecOptions("hero0", Rank.A, "hero0-B-a");
            Assert.All(offer, o => Assert.Contains(o, pool));
            // Authored order survives the draw — a pool never reorders between runs.
            Assert.Equal(pool.Where(offer.Contains), offer);
        }

        [Fact]
        public void TwoWidePoolIsOfferedWhole()
        {
            // The pre-variable-arity shape: drawing 2 of 2 is the identity function, which is
            // why today's content behaves exactly as it did before the pool existed.
            var run = ShopRun(7, Wide(2));
            Assert.Equal(new[] { "hero0-B-a-A-a", "hero0-B-a-A-b" }, OfferAtRank(run, Rank.A));
        }

        [Fact]
        public void FollowingRankPreviewWaitsForTheForkChoice()
        {
            var run = ShopRun(7, Wide(2));
            OfferAtRank(run, Rank.B);
            var hero = run.State.Field.First(h => h.ChassisId == "hero0");
            Assert.Null(hero.PathId);

            HeroInstance next = hero.Clone();
            next.Rank++;
            Assert.False(run.TryPeekSpecOffer(next, out var blocked));
            Assert.Empty(blocked);

            run.ChooseSpec(0);
            HeroInstance resolved = hero.Clone();
            resolved.Rank++;
            Assert.True(run.TryPeekSpecOffer(resolved, out var preview));
            Assert.Equal(new[] { "hero0-B-a-A-a", "hero0-B-a-A-b" }, preview);
        }

        [Fact]
        public void DrawIsStableForTheSameSeed()
        {
            // Stateless-by-salt (ADR 0008): no rng to persist, no ordering coupling with the
            // shop or encounter rolls, so the same run always shows the same menu.
            Assert.Equal(OfferAtRank(ShopRun(11, Wide(3)), Rank.A),
                         OfferAtRank(ShopRun(11, Wide(3)), Rank.A));
        }

        [Fact]
        public void DrawVariesAcrossSeeds()
        {
            // The whole point of the pool: the A-rank menu is not the same every run.
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 24; seed++)
                seen.Add(string.Join(",", OfferAtRank(ShopRun(seed, Wide(3)), Rank.A)));

            Assert.True(seen.Count > 1, $"pool draw never varied across 24 seeds: {seen.First()}");
        }

        [Fact]
        public void DrawnOfferSurvivesSaveAndResume()
        {
            // Re-drawing on resume would hand the player a different menu than the one they were
            // looking at when they saved.
            var run = ShopRun(5, Wide(3));
            var offer = OfferAtRank(run, Rank.A);

            var resumed = RunController.Resume(RunSave.Read(RunSave.Write(run.State)), Wide(3));
            Assert.Equal(offer, resumed.State.PendingSpec!.Options);
        }

        [Fact]
        public void LegacyPairSaveMigratesToOptions()
        {
            // Saves written before variable arity store the offer as optionA/optionB.
            var state = ShopRun(5, Wide(2)).State;
            OfferAtRank(ShopRun(5, Wide(2)), Rank.B);   // sanity: the shape exists
            state.PendingSpec = new PendingSpec
            {
                Zone = RosterZone.Field, Index = 0, ForRank = Rank.B,
                Options = { "hero0-B-a", "hero0-B-b" },
            };

            string legacy = RunSave.Write(state)
                .Replace("pendingSpec.options=hero0-B-a|hero0-B-b\n",
                         "pendingSpec.optionA=hero0-B-a\npendingSpec.optionB=hero0-B-b\n");
            Assert.DoesNotContain("pendingSpec.options=", legacy);

            var loaded = RunSave.Read(legacy);
            Assert.Equal(new[] { "hero0-B-a", "hero0-B-b" }, loaded.PendingSpec!.Options);
        }

        [Fact]
        public void ChooseSpecRejectsAnIndexOutsideTheOffer()
        {
            var run = ShopRun(7, Wide(3));
            OfferAtRank(run, Rank.A);                  // a 2-wide offer drawn from a 3-wide pool
            Assert.Throws<System.InvalidOperationException>(() => run.ChooseSpec(2));
            Assert.Throws<System.InvalidOperationException>(() => run.ChooseSpec(-1));
        }

        [Fact]
        public void RealContentStillOffersEveryAuthoredPoolWhole()
        {
            // Today every LIVE pool is two wide, so no live offer is narrowed yet. This is
            // the canary for the first widened pool: it should fail, and the failure should be
            // read as "the draw is now live", not as a regression.
            foreach (var pool in Kits.Offers.Values)
                Assert.Equal(2, pool.Count);
        }

        // ---- candidate containment ---------------------------------------------------
        // Authored-but-unreachable content is only safe if it is provably unreachable. These
        // pin the three ways it could leak: an offer, the fingerprint, or a live catalog.

        [Fact]
        public void CandidateNodesAreNeverOfferedByALiveCatalog()
        {
            var cat = new Catalog();                       // IncludeCandidates defaults to false
            Assert.NotEmpty(Kits.CandidateNodes);          // guard: this test means nothing if empty

            foreach (var key in Kits.Offers.Keys)
            {
                var parts = key.Split('|');
                var pool = cat.SpecOptions(parts[0], System.Enum.Parse<Rank>(parts[1]),
                                           parts[2] == "-" ? null : parts[2]);
                foreach (string id in pool)
                    Assert.False(Kits.CandidateNodes.ContainsKey(id),
                                 $"candidate {id} leaked into live offer {key}");
            }
        }

        [Fact]
        public void CandidateOffersAppearOnlyWhenExplicitlyEnabled()
        {
            var live = new Catalog();
            var withCandidates = new Catalog { IncludeCandidates = true };

            Assert.Equal(new[] { "sharpshot.sniper", "sharpshot.volleyer" },
                         live.SpecOptions("sharpshot", Rank.B, null));
            Assert.Equal(new[] { "sharpshot.sniper", "sharpshot.volleyer", "sharpshot.spotter" },
                         withCandidates.SpecOptions("sharpshot", Rank.B, null));
        }

        [Fact]
        public void CandidateContentDoesNotMoveTheContentFingerprint()
        {
            // The load-bearing claim: authoring a candidate cannot invalidate a save or a replay,
            // because content that cannot be reached is not part of a run's content identity.
            // Recomputed from the live registries only — if this ever folds CandidateNodes,
            // every existing save stops loading and the symptom looks like save corruption.
            Assert.NotEmpty(Kits.CandidateNodes);
            foreach (string id in Kits.CandidateNodes.Keys)
                Assert.False(Kits.Nodes.ContainsKey(id), $"{id} is both live and candidate");
            Assert.Equal("3dba11673c26e858", new Catalog().ContentVersion);
        }

        [Fact]
        public void CandidatePathsCompleteALadderAndCompose()
        {
            // Spotter must be a real ladder, not a dangling B node: every rank it can reach has
            // an offer, and every resulting build composes.
            var cat = new Catalog { IncludeCandidates = true };
            Assert.Contains("sharpshot.spotter", cat.SpecOptions("sharpshot", Rank.B, null));

            foreach (string a in cat.SpecOptions("sharpshot", Rank.A, "sharpshot.spotter"))
                foreach (string s in cat.SpecOptions("sharpshot", Rank.S, "sharpshot.spotter"))
                {
                    var composed = Loadout.Compose(
                        cat.Chassis("sharpshot"),
                        nodes: new[] { cat.Node("sharpshot.spotter"), cat.Node(a), cat.Node(s) },
                        tier: WeaponTier.Relic, mastered: true, rankSteps: 3);
                    Assert.NotNull(composed.Def);
                }
        }

        [Fact]
        public void SpotterAmplifiesForTheWholeParty_NotJustHerself()
        {
            // The reason this path exists: DamageTakenUp is a debuff on the ENEMY, so every ally
            // collects it for free. If this ever became a self-buff the path would be pointless.
            var spotter = new Catalog().Node("sharpshot.spotter");
            Assert.NotNull(spotter.SignatureOverride);

            bool amplifiesEnemy = false;
            foreach (var eff in spotter.SignatureOverride!)
                if (eff.Status == StatusKind.DamageTakenUp &&
                    eff.Select.Kind == SelKind.FarthestEnemy) amplifiesEnemy = true;
            Assert.True(amplifiesEnemy, "Spotter no longer amplifies damage taken by its target");
        }
    }
}
