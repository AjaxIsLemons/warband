using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The authored enemy-role grammar and encounter pool (roadmap item 2). These pin the laws that
    /// make an encounter legal — placement, disclosure, and the two authored rules that bend the
    /// shared combat model on purpose.
    /// </summary>
    public class AuthoredEncounterGrammarTests
    {
        private static readonly Catalog Cat = new Catalog();

        private static IEnumerable<EncounterDef> AllEncounters()
        {
            for (int act = 1; act <= 3; act++)
                foreach (var factory in Encounters.PoolFor(act))
                    yield return factory(act);
        }

        [Fact]
        public void EveryRoleIsALegalFightableUnit()
        {
            foreach (var (id, factory) in Enemies.Roles)
            {
                var def = factory();
                Assert.True(def.MaxHp > 0, $"{id} has no health");
                Assert.True(def.MoveInterval > 0, $"{id} cannot move");
                Assert.False(string.IsNullOrWhiteSpace(def.Name), $"{id} has no name");
                // ChassisId is the render key — without one the client falls back to a bare capsule
                // and five distinct roles read as five identical blobs.
                Assert.False(string.IsNullOrWhiteSpace(def.ChassisId), $"{id} has no render key");
                // A body with neither an attack nor a signature cannot do anything at all.
                Assert.True(def.Attack > 0 || def.Signature.Count > 0, $"{id} is inert");
            }
        }

        [Fact]
        public void EncountersDeployOnlyInTheEnemyHalfAndNeverStack()
        {
            foreach (var enc in AllEncounters())
            {
                var seen = new HashSet<Hex>();
                foreach (var e in enc.Enemies)
                {
                    Assert.True(Battle.InBounds(e.Pos), $"{enc.Id}: {e.Pos} is off the board");
                    Assert.True(e.Pos.Row >= Battle.BoardRows / 2,
                        $"{enc.Id}: {e.Def.Name} deploys at row {e.Pos.Row}, inside the player half");
                    Assert.True(seen.Add(e.Pos), $"{enc.Id}: two enemies stacked on {e.Pos}");
                    Assert.False(string.IsNullOrWhiteSpace(e.Role), $"{enc.Id}: {e.Def.Name} has no previewed role");
                }
                Assert.NotEmpty(enc.Enemies);
            }
        }

        /// <summary>pve-encounters.md: "no surprise mechanics after the player presses Play".</summary>
        [Fact]
        public void EveryEncounterDisclosesItsRule()
        {
            foreach (var enc in AllEncounters())
            {
                Assert.False(string.IsNullOrWhiteSpace(enc.Id));
                Assert.False(string.IsNullOrWhiteSpace(enc.Name), $"{enc.Id} has no name");
                Assert.False(string.IsNullOrWhiteSpace(enc.Pressure), $"{enc.Id} states no pressure");
                Assert.False(string.IsNullOrWhiteSpace(enc.RuleName), $"{enc.Id} names no rule");
                Assert.False(string.IsNullOrWhiteSpace(enc.RuleText), $"{enc.Id} explains no rule");
            }
        }

        /// <summary>
        /// The preview law, at the catalog seam: a brief that describes a different encounter than
        /// the one that spawns is worse than no brief. Both derive from the same salted draw, so
        /// this pins that they stay in step.
        /// </summary>
        [Fact]
        public void TheBriefDescribesTheEncounterThatActuallySpawns()
        {
            for (int act = 1; act <= 3; act++)
                for (int node = 0; node < 4; node++)
                    foreach (var tier in new[] { FightTier.Stable, FightTier.Fraying, FightTier.Collapsing })
                    {
                        var brief = Cat.EncounterBrief(act, node, tier, new Rng(99));
                        var spawn = Cat.Encounter(act, node, tier, new Rng(99));
                        var named = Encounters.PoolFor(act)
                            .Select(f => f(act))
                            .Single(e => e.Id == brief.Id);
                        Assert.Equal(named.Enemies.Count, spawn.Count);
                        for (int i = 0; i < spawn.Count; i++)
                            Assert.Equal(named.Enemies[i].Def.Name, spawn[i].Def.Name);
                    }
        }

        [Fact]
        public void TheLongRangeIsGatedOutOfActOne()
        {
            Assert.DoesNotContain(Encounters.PoolFor(1).Select(f => f(1).Id), id => id == "the-long-range");
            Assert.Contains(Encounters.PoolFor(2).Select(f => f(2).Id), id => id == "the-long-range");
        }

        /// <summary>
        /// Roadmap item 14: acts 2 and 3 used to draw an IDENTICAL pool, so a three-act run was one
        /// act played three times at rising numbers.
        ///
        /// The law is NOT "every act owns a unique encounter" — act 1 deliberately owns none. Its
        /// job is to teach pieces cleanly that later acts recombine (pve-encounters.md), so all
        /// three of its encounters recurring later is the through-line working. What must hold is
        /// that the run keeps introducing problems and that the two acts item 14 names stop being
        /// the same fight.
        /// </summary>
        [Fact]
        public void ActsDrawDifferentPoolsAndKeepIntroducingProblems()
        {
            var pools = Enumerable.Range(1, 3)
                .Select(a => Encounters.PoolFor(a).Select(f => f(a).Id).ToList())
                .ToList();

            foreach (var pool in pools)
                Assert.Equal(pool.Count, pool.Distinct().Count());   // no act repeats itself

            // The literal defect: acts 2 and 3 drawing the same pool. They are now disjoint.
            for (int a = 0; a < 3; a++)
                for (int b = a + 1; b < 3; b++)
                    Assert.False(pools[a].ToHashSet().SetEquals(pools[b]),
                        $"acts {a + 1} and {b + 1} draw an identical pool");
            Assert.True(pools[1].Intersect(pools[2]).Count() <= 1,
                "acts 2 and 3 overlap by more than one encounter — the item-14 defect in miniature");

            // Every act after the first must bring a problem the player has not met yet, or it is
            // the previous act with a bigger multiplier.
            var seen = pools[0].ToHashSet();
            for (int a = 1; a < 3; a++)
            {
                Assert.True(pools[a].Any(id => !seen.Contains(id)),
                    $"act {a + 1} introduces nothing new — every encounter was already met earlier");
                seen.UnionWith(pools[a]);
            }
        }

        /// <summary>Every act's pool must be big enough to fill its node fights without forcing a
        /// repeat: an act with fewer encounters than beats is an act you see twice.</summary>
        [Fact]
        public void EveryActPoolCoversItsNodeFights()
        {
            int nodeFights = new RunConfig().NodesPerAct - 1;   // one beat is the Interlude
            for (int act = 1; act <= 3; act++)
                Assert.True(Encounters.PoolFor(act).Length >= nodeFights,
                    $"act {act} has {Encounters.PoolFor(act).Length} encounters for {nodeFights} fights");
        }

        /// <summary>Composition is the act's primary difficulty lever (ADR 0016), so a later act
        /// must field a genuinely different problem, not the same one with bigger numbers.</summary>
        [Fact]
        public void ActsChangeCompositionNotJustStats()
        {
            Assert.True(Encounters.GnawingHour(3).Enemies.Count > Encounters.GnawingHour(1).Enemies.Count);
            Assert.True(Encounters.TheDrop(2).Enemies.Count > Encounters.TheDrop(1).Enemies.Count);
            // The Ninth Bell gains its wall at act 2 — the clock is taught alone first.
            Assert.DoesNotContain(Encounters.NinthBell(1).Enemies, e => e.Def.Name == "Ashen Colossus");
            Assert.Contains(Encounters.NinthBell(2).Enemies, e => e.Def.Name == "Ashen Colossus");
        }

        // ---- the two authored rules that bend the shared model on purpose ----

        /// <summary>
        /// The Scribe's whole design: its ritual is a clock the player can read, so hitting it must
        /// NOT advance it. On the global hit-fed mana rate a channeller fires the instant it is
        /// focused, which punishes the obvious answer instead of rewarding it.
        /// </summary>
        [Fact]
        public void TheRitualAdvancesOnTimeAloneNotOnBeingStruck()
        {
            int CastTick(int attackers)
            {
                // Survival is not what is under test — pin the Scribe's HP so the only variable is
                // how much incoming damage its mana bar sees.
                var scribe = Enemies.Scribe();
                scribe.MaxHp = 100_000;
                var units = new List<UnitState> { UnitState.Spawn(0, 1, scribe, Hex.FromRowCol(7, 2)) };
                for (int i = 0; i < attackers; i++)
                {
                    var hitter = new UnitDef
                    {
                        Name = "hitter", MaxHp = 4000, Attack = 3, AttackInterval = 4,
                        Range = 5, MoveInterval = 5,
                    };
                    units.Add(UnitState.Spawn(i + 1, 0, hitter, Hex.FromRowCol(3, i)));
                }
                var cast = new Battle(units).Run().Events
                    .FirstOrDefault(e => e.Kind == EventKind.Cast && e.Source == 0);
                return cast?.Tick ?? -1;
            }

            int lightPressure = CastTick(1);
            int heavyPressure = CastTick(3);
            Assert.True(lightPressure > 0, "the ritual never fired at all");
            Assert.Equal(lightPressure, heavyPressure);
        }

        /// <summary>The Ward names its verb and its off-switch: kill an escort and the wall becomes
        /// killable. Disclosed in the rule text, and the reason focusing the biggest threat is the
        /// wrong opening.</summary>
        [Fact]
        public void TheWardDropsWhenAnEscortDies()
        {
            var colossus = Enemies.Colossus();
            var escort = Enemies.Hourling();
            escort.MaxHp = 1;                       // dies to the first swing

            var killer = new UnitDef
            {
                Name = "killer", MaxHp = 4000, Attack = 30, AttackInterval = 5,
                Range = 6, MoveInterval = 5, TargetPref = TargetPref.LowestHp,
            };

            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, killer, Hex.FromRowCol(3, 2)),
                UnitState.Spawn(1, 1, colossus, Hex.FromRowCol(5, 2)),
                UnitState.Spawn(2, 1, escort, Hex.FromRowCol(5, 4)),
            };
            var result = new Battle(units).Run();

            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusApplied && e.Aux == (int)StatusKind.DamageTakenDown && e.Target == 1);
            Assert.Contains(result.Events, e =>
                e.Kind == EventKind.StatusExpired && e.Aux == (int)StatusKind.DamageTakenDown && e.Target == 1);
        }

        /// <summary>An unescorted Colossus is authored without the Ward — the reduction is something
        /// its escorts hold up, so a lone wall must not carry it for free.</summary>
        [Fact]
        public void AnUnwardedColossusCarriesNoReduction()
        {
            var lone = Enemies.Colossus(warded: false);
            Assert.Empty(lone.Triggers);
            Assert.DoesNotContain("Ward", lone.Traits);
        }

        /// <summary>The roles that need to reach past a front line say so in data, which is the ADR
        /// 0022 behavior layer doing the work that used to need bespoke sim code.</summary>
        [Fact]
        public void ReachRolesAcquirePastTheFrontLine()
        {
            Assert.Equal(TargetPref.Farthest, Enemies.Gunner().TargetPref);
            Assert.Equal(TargetPref.Farthest, Enemies.Stalker().TargetPref);
            Assert.True(Enemies.Gunner().Standoff > 0, "artillery that will not give ground is just melee");
            Assert.True(Enemies.Hourling().MoveInterval < Enemies.Colossus().MoveInterval,
                "the swarm must arrive before the wall does");
        }
    }
}
