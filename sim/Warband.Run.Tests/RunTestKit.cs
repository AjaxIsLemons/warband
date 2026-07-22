using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;

namespace Warband.Run.Tests
{
    /// <summary>Placeholder content stub — exists to exercise the machine, not to be tuned.</summary>
    internal sealed class StubContent : IRunContent
    {
        public int MonstersPerFight = 2;
        public int MonsterHp = 40;
        public int MonsterAttack = 4;
        public bool WeakBoss = true;             // weak ghost = player wins bosses by default
        public Hex GhostPos = Hex.FromRowCol(0, 2);   // owner-half coords, mirrored at fight time

        public ChassisDef Chassis(string id) => new ChassisDef
        {
            Name = id,
            MaxHp = 120,
            ManaMax = 0,
            StarterWeapon = new WeaponDef { Name = "starter", Damage = 12, Interval = 10, Range = 1 },
        };

        public WeaponDef Weapon(string id) => new WeaponDef { Name = id, Damage = 20, Interval = 10, Range = 1 };
        public TrinketDef Trinket(string id) => new TrinketDef { Name = id, HpBonus = 20 };
        public SpecNode Node(string id) => new SpecNode { Name = id, HpBonus = 10 };

        public BannerDef Banner(string id) => new BannerDef
        {
            Name = id,
            TeamTriggers =
            {
                new Trigger
                {
                    On = EventKind.BattleStart,
                    Do =
                    {
                        new EffectDef
                        {
                            Kind = EffectKind.GrantShield, Amount = 30,
                            Select = new Selector { Kind = SelKind.AlliesWithin, Range = 99 },
                        },
                    },
                },
            },
        };

        public List<string> Heroes = new List<string>
            { "hero0", "hero1", "hero2", "alpha", "beta", "gamma", "delta", "epsilon" };
        public List<string> Weapons = new List<string> { "blade", "bow" };
        public List<string> Trinkets = new List<string> { "charm" };
        public List<string> Banners = new List<string> { "warbanner" };

        public IReadOnlyList<string> HeroPool(int act) => Heroes;
        public IReadOnlyList<string> WeaponPool(int act) => Weapons;
        public IReadOnlyList<string> TrinketPool(int act) => Trinkets;
        public IReadOnlyList<string> BannerPool(int act) => Banners;

        public (string A, string B) SpecOptions(string chassisId, Rank rank, string? pathId) =>
            ($"{pathId ?? chassisId}-{rank}-a", $"{pathId ?? chassisId}-{rank}-b");

        public Func<int, int, FightTier, Rng, List<(UnitDef, Hex)>>? EncounterOverride;

        public List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng)
        {
            if (EncounterOverride != null) return EncounterOverride(act, nodeIndex, tier, rng);
            var list = new List<(UnitDef, Hex)>();
            for (int i = 0; i < MonstersPerFight; i++)
                list.Add((new UnitDef
                {
                    Name = "monster",
                    MaxHp = MonsterHp,
                    Attack = MonsterAttack,
                    AttackInterval = 10,
                    Range = 1,
                    MoveInterval = 5,
                }, Hex.FromRowCol(6, i)));
            return list;
        }

        public List<string> GhostBanners = new List<string>();

        public GhostSnapshot BossGhost(int act, int bossWins, Rng rng)
        {
            var hero = new HeroInstance { ChassisId = WeakBoss ? "ghost-weak" : "ghost" };
            var snap = new GhostSnapshot { Act = act };
            snap.BannerIds.AddRange(GhostBanners);
            snap.Units.Add(new GhostUnit { Hero = hero, Pos = GhostPos });
            if (!WeakBoss)
            {
                snap.Units.Add(new GhostUnit
                {
                    Hero = new HeroInstance { ChassisId = "ghost", WeaponId = "greatblade" },
                    Pos = Hex.FromRowCol(0, 3),
                });
                snap.Units.Add(new GhostUnit
                {
                    Hero = new HeroInstance { ChassisId = "ghost", WeaponId = "greatblade" },
                    Pos = Hex.FromRowCol(1, 2),
                });
            }
            return snap;
        }
    }

    internal static class Kit
    {
        public static List<HeroInstance> Warband(int n = 3)
        {
            var list = new List<HeroInstance>();
            for (int i = 0; i < n; i++)
                list.Add(new HeroInstance { ChassisId = $"hero{i}" });
            return list;
        }

        public static List<Hex> AutoPlace(RunController run) =>
            Enumerable.Range(0, run.State.Field.Count).Select(i => Hex.FromRowCol(1, i)).ToList();

        /// <summary>Scripted full-run driver: fixed tier, auto-place, buy every affordable slot.</summary>
        public static RunState PlayOut(RunController run, FightTier tier = FightTier.Even)
        {
            while (run.State.Phase != RunPhase.Complete)
            {
                switch (run.State.Phase)
                {
                    case RunPhase.Node:
                        switch (run.CurrentNodeKind)
                        {
                            case NodeKind.Fight: run.ResolveFight(tier, AutoPlace(run)); break;
                            case NodeKind.Event: run.ResolveEvent(); break;
                            case NodeKind.Boss: run.ResolveBoss(AutoPlace(run)); break;
                        }
                        break;
                    case RunPhase.Shop:
                        if (run.SlotOfferOpen && run.State.Gold >= run.SlotOfferCost)
                            run.BuySlot();
                        run.LeaveShop();
                        break;
                }
            }
            return run.State;
        }
    }
}
