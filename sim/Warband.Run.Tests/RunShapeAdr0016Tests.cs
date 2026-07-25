using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;
using Xunit;

namespace Warband.Run.Tests
{
    /// <summary>
    /// The run shape after ADR 0016 superseded the ghost-boss design: the act boss is AUTHORED
    /// content, and the PoC defeat rule (Jake, 2026-07-24) makes any loss terminal. These are the
    /// laws the client shell binds to, so they are pinned here rather than left implicit.
    /// </summary>
    public class RunShapeAdr0016Tests
    {
        /// <summary>
        /// Delegating spy — records what the controller asked for, so we can prove WHICH seam it
        /// used. Must implement the interface rather than subclass the stub: RunController calls
        /// through IRunContent, so a hidden method would never dispatch.
        /// </summary>
        private sealed class SpyContent : IRunContent
        {
            private readonly StubContent _inner = new StubContent();
            public readonly List<int> BossAsks = new List<int>();

            public List<(UnitDef Def, Hex Pos)> Boss(int act, Rng rng)
            {
                BossAsks.Add(act);
                return _inner.Boss(act, rng);
            }

            public ChassisDef Chassis(string id) => _inner.Chassis(id);
            public WeaponDef Weapon(string id) => _inner.Weapon(id);
            public TrinketDef Trinket(string id) => _inner.Trinket(id);
            public SpecNode Node(string id) => _inner.Node(id);
            public BannerDef Banner(string id) => _inner.Banner(id);
            public IReadOnlyList<string> HeroPool(int act) => _inner.HeroPool(act);
            public IReadOnlyList<string> WeaponPool(int act) => _inner.WeaponPool(act);
            public IReadOnlyList<string> TrinketPool(int act) => _inner.TrinketPool(act);
            public IReadOnlyList<string> BannerPool(int act) => _inner.BannerPool(act);
            public (string A, string B) SpecOptions(string chassisId, Rank rank, string? pathId)
                => _inner.SpecOptions(chassisId, rank, pathId);
            public Rank ForkRank(string chassisId) => _inner.ForkRank(chassisId);
            public List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng)
                => _inner.Encounter(act, nodeIndex, tier, rng);
        }

        private static void DriveToBoss(RunController run)
        {
            while (!run.AtBoss)
            {
                if (run.CurrentNodeKind == NodeKind.Fight)
                    run.ResolveFight(FightTier.Fraying, Kit.AutoPlace(run));
                else
                    run.ResolveEvent();
            }
        }

        [Fact]
        public void DefeatIsTerminalAndRefusesFurtherPlay()
        {
            var state = Kit.PlayOut(new RunController(4, new StubContent { WeakBoss = false }, Kit.Warband()));
            Assert.Equal(RunPhase.Defeated, state.Phase);

            // Nothing may resume a dead run — the shell's "back to menu" is the only exit.
            var dead = new RunController(4, new StubContent { WeakBoss = false }, Kit.Warband());
            Kit.PlayOut(dead);
            Assert.Throws<System.InvalidOperationException>(() => dead.LeaveShop());
            Assert.Throws<System.InvalidOperationException>(() => dead.Reroll());
            Assert.Throws<System.InvalidOperationException>(() => _ = dead.CurrentNodeKind);
        }

        [Fact]
        public void VictoryMeansFinishingTheLastActNotAWinRecord()
        {
            var state = Kit.PlayOut(new RunController(9, new StubContent(), Kit.Warband()));
            Assert.True(state.Victory);
            Assert.Equal(RunPhase.Complete, state.Phase);
            // Victory is the phase, not an arithmetic threshold on wins.
            state.BossWins = 0;
            Assert.True(state.Victory);
        }

        [Fact]
        public void ActBossComesFromTheAuthoredContentSeam()
        {
            var content = new SpyContent();
            var run = new RunController(21, content, Kit.Warband());
            DriveToBoss(run);
            run.ResolveBoss(Kit.AutoPlace(run));

            // The controller reached IRunContent.Boss for act 1 — not a generated ghost pool.
            Assert.Equal(new[] { 1 }, content.BossAsks);
        }

        [Fact]
        public void CatalogBossIsTheAuthoredEncounterScaledByAct()
        {
            var cat = new Catalog();
            var act1 = cat.Boss(1, new Rng(1));
            var act3 = cat.Boss(3, new Rng(1));

            // It is The Last Oath's bonded pair, by identity — not random kits-as-monsters.
            var authored = Encounters.BondedPair();
            Assert.Equal(authored.Enemies.Count, act1.Count);
            Assert.Equal(authored.Enemies.Select(e => e.Def.ChassisId).OrderBy(x => x),
                         act1.Select(u => u.Def.ChassisId).OrderBy(x => x));

            // Later acts scale the same authored comp (scaffolding until real per-act bosses exist).
            Assert.True(act3[0].Def.MaxHp > act1[0].Def.MaxHp);
            Assert.Equal(act1[0].Pos, act3[0].Pos);
        }

        [Fact]
        public void BossDifficultyNeverKeysOffThePlayersRecord()
        {
            // ADR 0002's surviving law: act-anchored only. Two runs at the same act must field the
            // same boss regardless of what happened earlier in the run.
            var cat = new Catalog();
            var a = cat.Boss(2, new Rng(7));
            var b = cat.Boss(2, new Rng(7));
            Assert.Equal(a.Select(u => (u.Def.Name, u.Def.MaxHp, u.Pos)),
                         b.Select(u => (u.Def.Name, u.Def.MaxHp, u.Pos)));
        }

        [Fact]
        public void AuthoredBossDefsAreFreshPerCallAndCannotBeMutatedAcrossRuns()
        {
            // Catalog.Boss scales MaxHp in place; if it handed out the catalog's own defs, act 3
            // would compound onto act 1's scaling and every retry would get harder.
            var cat = new Catalog();
            int first = cat.Boss(1, new Rng(1))[0].Def.MaxHp;
            cat.Boss(5, new Rng(1));
            Assert.Equal(first, cat.Boss(1, new Rng(1))[0].Def.MaxHp);
        }
    }
}
