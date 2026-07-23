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
            foreach (var id in Cat.BannerPool(1)) Assert.NotNull(((IRunContent)Cat).Banner(id));

            foreach (var (key, pair) in Kits.Offers.Select(kv => (kv.Key, kv.Value)))
            {
                Assert.True(Kits.Nodes.ContainsKey(pair.A), $"{key} offers missing node {pair.A}");
                Assert.True(Kits.Nodes.ContainsKey(pair.B), $"{key} offers missing node {pair.B}");
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
            {
                var fork = Cat.ForkRank(id);
                // Walk both branches of the ladder.
                foreach (int first in new[] { 0, 1 })
                {
                    string? path = null;
                    var nodes = new List<string>();
                    foreach (var rank in new[] { Rank.B, Rank.A, Rank.S })
                    {
                        var (a, b) = Cat.SpecOptions(id, rank, path);
                        string chosen = first == 0 ? a : b;
                        nodes.Add(chosen);
                        if (rank == fork) path = chosen;
                    }
                    Assert.Equal(3, nodes.Distinct().Count());
                }
            }
        }

        [Fact]
        public void EveryBuildComposesAndFightsToTermination()
        {
            // The 64-build sweep-lite: all 8 chassis × both paths × both A × both S at
            // rank S, thrown against a fixed enemy pair. Broken content = crash or a
            // fight that hits the safety cap.
            foreach (var id in Cat.HeroPool(1))
            {
                var fork = Cat.ForkRank(id);
                foreach (int pB in new[] { 0, 1 })
                foreach (int pA in new[] { 0, 1 })
                foreach (int pS in new[] { 0, 1 })
                {
                    string? path = null;
                    var nodeIds = new List<string>();
                    var picks = new Dictionary<Rank, int> { [Rank.B] = pB, [Rank.A] = pA, [Rank.S] = pS };
                    foreach (var rank in new[] { Rank.B, Rank.A, Rank.S })
                    {
                        var (a, b) = Cat.SpecOptions(id, rank, path);
                        string chosen = picks[rank] == 0 ? a : b;
                        nodeIds.Add(chosen);
                        if (rank == fork) path = chosen;
                    }

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
            var (b1, b2) = Cat.SpecOptions("shade", Rank.B, null);
            Assert.Equal(("shade.killerstempo", "shade.opportunist"), (b1, b2));
            var (a1, a2) = Cat.SpecOptions("shade", Rank.A, null);
            Assert.Equal(("shade.reaper", "shade.phantom"), (a1, a2));
        }

        [Fact]
        public void FullRunsCompleteOnRealContent()
        {
            // The integration smoke: bot policy plays whole runs on the real catalog —
            // shops, rank-ups (incl. Shade's A-fork), bot ghosts, wagers, the lot.
            var reports = RunHarness.PlayMany(6, seedBase: 1000, new Catalog());
            Assert.All(reports, r =>
            {
                Assert.Equal(RunPhase.Complete, r.Final.Phase);
                Assert.NotEmpty(r.Fights);
            });
            // Fights actually resolve (no perma-draw stalemates from broken content).
            int decided = reports.SelectMany(r => r.Fights).Count(f => f.EndTick < Battle.OvertimeStartTick);
            Assert.True(decided > 0, "every single fight went to overtime — something is broken");
        }
    }
}
