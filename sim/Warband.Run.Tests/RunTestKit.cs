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
        public Hex BossPos = Hex.FromRowCol(Battle.BoardRows - 1, 2);   // enemy half, stated directly

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

        public Rank ForkRank(string chassisId) => Rank.B;

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

        public bool UseBotGhosts;                // legacy knob: bot-generated comps as the boss

        /// <summary>
        /// Authored act boss (ADR 0016). WeakBoss = one body the player beats by default;
        /// otherwise a three-body comp. Positions are enemy-half rows, stated directly now that
        /// nothing mirrors an owner-half snapshot in.
        /// </summary>
        public EncounterBrief EncounterBrief(int act, int nodeIndex, FightTier tier, Rng rng) =>
            new EncounterBrief { Id = "stub", Name = "Stub Encounter", RuleName = "NONE" };

        public EncounterBrief BossBrief(int act, Rng rng) =>
            new EncounterBrief { Id = "stub-boss", Name = "Stub Boss", RuleName = "NONE" };

        public List<(UnitDef Def, Hex Pos)> Boss(int act, Rng rng)
        {
            var list = new List<(UnitDef, Hex)>();
            // The armed bodies carry a weapon, matching the old ghost comp (HeroInstance with
            // WeaponId "greatblade") — without it the boss is too weak to ever beat the player.
            UnitDef Body(string id, string? weaponId = null) =>
                Loadout.Compose(Chassis(id), weaponId == null ? null : Weapon(weaponId)).Def;

            list.Add((Body(WeakBoss ? "ghost-weak" : "ghost"), BossPos));
            if (!WeakBoss)
            {
                list.Add((Body("ghost", "greatblade"), Hex.FromRowCol(Battle.BoardRows - 1, 3)));
                list.Add((Body("ghost", "greatblade"), Hex.FromRowCol(Battle.BoardRows - 2, 2)));
            }
            return list;
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

        /// <summary>Scripted full-run driver for the unified Planning workspace.</summary>
        public static RunState PlayOut(RunController run, FightTier tier = FightTier.Fraying)
        {
            while (!run.State.Over)   // Complete or Defeated
            {
                if (run.State.Phase == RunPhase.Reward)
                {
                    run.ChooseBossReward(0);
                    continue;
                }

                if (run.SlotOfferOpen && run.State.Gold >= run.SlotOfferCost)
                    run.BuySlot();
                switch (run.CurrentNodeKind)
                {
                    case NodeKind.Fight: run.ResolveFight(tier, AutoPlace(run)); break;
                    case NodeKind.Event: run.ResolveEvent(); break;
                    case NodeKind.Boss: run.ResolveBoss(AutoPlace(run)); break;
                }
            }
            return run.State;
        }
    }
}
