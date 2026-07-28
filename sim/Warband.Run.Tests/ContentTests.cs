using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>The real content pass (board item 2): catalog integrity, every build
    /// composes and fights, rank stat bumps, the forge, and full-run smoke.</summary>
    public class ContentTests
    {
        private static readonly Catalog Cat = new Catalog();

        [Fact]
        public void CatalogIsClosed()
        {
            // Every pool id resolves; every offer row points at real nodes; every
            // chassis specialization names a real weapon category.
            foreach (var id in Cat.HeroPool(1)) Assert.NotNull(Cat.Chassis(id));
            foreach (var id in Cat.WeaponPool(1)) Assert.NotNull(Cat.Weapon(id));
            foreach (var id in Cat.TrinketPool(1)) Assert.NotNull(Cat.Trinket(id));
            foreach (var id in Cat.InscriptionPool(1)) Assert.NotNull(((IRunContent)Cat).Inscription(id));

            foreach (var (key, pool) in Kits.Offers.Select(kv => (kv.Key, kv.Value)))
            {
                Assert.True(pool.Count >= 2, $"{key} offers fewer than two nodes");
                Assert.Equal(pool.Count, pool.Distinct().Count());
                foreach (string node in pool)
                    Assert.True(Kits.Nodes.ContainsKey(node), $"{key} offers missing node {node}");
            }

            var categories = new HashSet<string>(Weapons.All.Values.Select(w => w.Category));
            foreach (var chassis in Kits.Chassis.Values)
                foreach (var spec in chassis.Specializations)
                    Assert.Contains(spec, categories);
        }

        [Fact]
        public void EveryChassisHasACompleteOfferLadder()
        {
            // C→B→A→S: an offer row must exist for every rank-up a hero can hit,
            // whichever path they took (the "easily changeable" table is TOTAL).
            foreach (var id in Cat.HeroPool(1))
                foreach (var nodes in AllLadders(id))
                    Assert.Equal(3, nodes.Distinct().Count());
        }

        /// <summary>
        /// Every C→B→A→S node sequence a hero can actually reach, branching on the real width of
        /// each rank's authored pool. Widening a pool widens the test rather than leaving the new
        /// options uncovered.
        /// </summary>
        private static List<List<string>> AllLadders(string chassisId)
        {
            var ladders = new List<List<string>>();
            var fork = Cat.ForkRank(chassisId);
            Walk(null, new List<string>(), 0);
            return ladders;

            void Walk(string? path, List<string> nodes, int rankIndex)
            {
                var ranks = new[] { Rank.B, Rank.A, Rank.S };
                if (rankIndex == ranks.Length) { ladders.Add(new List<string>(nodes)); return; }
                var rank = ranks[rankIndex];
                foreach (string choice in Cat.SpecOptions(chassisId, rank, path))
                {
                    nodes.Add(choice);
                    Walk(rank == fork ? choice : path, nodes, rankIndex + 1);
                    nodes.RemoveAt(nodes.Count - 1);
                }
            }
        }

        [Fact]
        public void EveryBuildComposesAndFightsToTermination()
        {
            // The sweep-lite: every chassis × every reachable ladder at rank S, thrown against a
            // fixed enemy pair. Eight two-wide heroes = the same 64 builds this always covered.
            // Broken content = crash or a fight that hits the safety cap.
            foreach (var id in Cat.HeroPool(1))
            {
                foreach (var nodeIds in AllLadders(id))
                {
                    var composed = Loadout.Compose(
                        Cat.Chassis(id),
                        nodes: nodeIds.Select(n => Cat.Node(n)),
                        tier: WeaponTier.Relic, mastered: true, rankSteps: 3);
                    var units = new List<UnitState>
                    {
                        Loadout.Spawn(0, 0, composed, Hex.FromRowCol(2, 2)),
                        UnitState.Spawn(1, 1, BattleTests_Grunt(160, 9), Hex.FromRowCol(5, 2)),
                        UnitState.Spawn(2, 1, BattleTests_Grunt(160, 9), Hex.FromRowCol(5, 3)),
                    };
                    var result = new Battle(units).Run();
                    Assert.True(result.EndTick < Battle.SafetyCapTick,
                        $"{id} build [{string.Join(",", nodeIds)}] hit the safety cap");
                }
            }
        }

        private static UnitDef BattleTests_Grunt(int hp, int atk) => new UnitDef
        {
            Name = "grunt", MaxHp = hp, Attack = atk, AttackInterval = 10, Range = 1, MoveInterval = 5,
        };

        [Fact]
        public void RankUpsCarryFlatStatBumps()
        {
            var recruit = Loadout.Compose(Cat.Chassis("cleric"));
            var sRank = Loadout.Compose(Cat.Chassis("cleric"), rankSteps: 3);
            Assert.Equal(recruit.Def.MaxHp + 3 * 25, sRank.Def.MaxHp);
            Assert.Equal(recruit.Def.Attack + 3 * 2, sRank.Def.Attack);
        }

        [Fact]
        public void ForgeFollowsTheFront()
        {
            var cfg = new RunConfig();
            Assert.Equal(WeaponTier.Worn, cfg.TierCeiling(1));
            Assert.Equal(WeaponTier.Honed, cfg.TierCeiling(2));
            Assert.Equal(WeaponTier.Relic, cfg.TierCeiling(4));

            // Act 1: the forge is closed (ceiling = Worn).
            var run = new RunController(7, Cat, RunHarness.StarterWarband(Cat, cfg), cfg);
            var placement = RunHarness.DefaultPlacement(run, Cat);
            run.ResolveFight(FightTier.Safe, placement);
            Assert.Throws<System.InvalidOperationException>(() => run.Reforge(RosterZone.Field, 0));
        }

        [Fact]
        public void ShadeForksAtA_NotB()
        {
            Assert.Equal(Rank.A, Cat.ForkRank("shade"));
            // The B offer is path-agnostic; the A offer is the fork itself.
            Assert.Equal(new[] { "shade.killerstempo", "shade.opportunist" },
                         Cat.SpecOptions("shade", Rank.B, null));
            Assert.Equal(new[] { "shade.reaper", "shade.phantom" },
                         Cat.SpecOptions("shade", Rank.A, null));
        }

        [Fact]
        public void FullRunsCompleteOnRealContent()
        {
            // The integration smoke: bot policy plays whole runs on the real catalog —
            // shops, rank-ups (incl. Shade's A-fork), wagers, the lot.
            //
            // This used to assert that EVERY run reached victory, which was a sound invariant while
            // monsters were random hero kits at 60% stats. Against authored encounters plus ADR
            // 0016's terminal-loss rule it stopped being one: a greedy bot with a fixed comp, fixed
            // spec picks and default placement winning every time would mean the PvE content poses
            // nothing. So the assertion is now that the MACHINE always completes, that the full arc
            // is reachable, and that it is not free.
            // 48 seeds, not 12: the sweep puts the bot's victory rate at only 2–8% (a greedy bot
            // with default placement against authored encounters), so a dozen seeds made
            // "completed > 0" a coin flip that any pool-size change re-rolls — exactly what
            // ADR 0026's seven new Inscriptions did. At ~4% × 48 the reachability claim is real.
            var reports = RunHarness.PlayMany(48, seedBase: 1000, new Catalog());

            Assert.All(reports, r =>
            {
                Assert.True(r.Final.Phase == RunPhase.Complete || r.Final.Phase == RunPhase.Defeated,
                    $"run ended in {r.Final.Phase} — the run machine got stuck mid-arc");
                Assert.NotEmpty(r.Fights);
            });

            int completed = reports.Count(r => r.Final.Phase == RunPhase.Complete);
            Assert.True(completed > 0, "no seed completed a run — the authored content is unwinnable");
            Assert.True(completed < reports.Count, "every seed won — the authored content poses nothing");

            // Fights actually resolve (no perma-draw stalemates from broken content).
            int decided = reports.SelectMany(r => r.Fights).Count(f => f.EndTick < Battle.OvertimeStartTick);
            Assert.True(decided > 0, "every single fight went to overtime — something is broken");

            // The progression path is exercised too, not just the fight loop: somewhere in these
            // runs a duplicate was bought and a spec choice resolved.
            Assert.Contains(reports, r =>
                r.Final.Field.Concat(r.Final.Bench).Any(h => h.Rank > Rank.C));
        }
    }
}
