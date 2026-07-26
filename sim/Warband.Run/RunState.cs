using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    public enum Rank { C, B, A, S }

    public enum FightTier
    {
        Stable = 0,
        Fraying = 1,
        Collapsing = 2,

        // Source compatibility while the current client shell migrates its labels.
        Safe = Stable,
        Even = Fraying,
        Greedy = Collapsing,
    }

    public enum NodeKind { Fight, Event, Boss }

    /// <summary>
    /// Defeated is terminal, alongside Complete. ADR 0016 gave the run a real ending, and the
    /// PoC rule (Jake, 2026-07-24) is that losing any fight ends the run — so there is no
    /// best-of-5 record to accumulate and no way back to Node from here.
    /// </summary>
    public enum RunPhase
    {
        Planning,
        Reward,
        Complete,
        Defeated,

        // Compatibility values for older snapshots and the shell landing in parallel. New runs
        // never enter them; the client maps both to the unified Planning workspace while loading.
        Node,
        Shop,
    }

    public enum OfferKind
    {
        Hero,
        Weapon,
        Trinket,
        Inscription,
        Banner = Inscription,
    }

    public enum ItemKind { Weapon, Trinket }

    public enum RosterZone { Field, Bench }

    public enum InterludePath { Treasury, Armory, Hourstone }

    public enum PurchaseOutcome
    {
        Recruit,
        RankUp,
        Weapon,
        Trinket,
        Inscription,
        Capacity,
    }

    /// <summary>
    /// Authoritative receipt for a Market purchase. Presentation can animate this result without
    /// diffing mutable state or guessing whether a Hero offer recruited or ranked up.
    /// </summary>
    public sealed class PurchaseResult
    {
        public int OfferIndex = -1;
        public OfferKind OfferKind;
        public PurchaseOutcome Outcome;
        public string ContentId = "";
        public int SandSpent;
        public Rank? PreviousRank;
        public Rank? NewRank;
        public long ItemInstanceId;
        public string PendingOptionA = "";
        public string PendingOptionB = "";
    }

    public sealed class ReforgeResult
    {
        public RosterZone Zone;
        public int HeroIndex;
        public string WeaponId = "";
        public long ItemInstanceId;
        public bool IsStarter;
        public WeaponTier PreviousTier;
        public WeaponTier NewTier;
        public int SandSpent;
        public int TotalWeaponInvestment;
    }

    public sealed class RewardOffer
    {
        public InterludePath Path;
        public OfferKind Kind;
        public string Id = "";
        public int Sand;
    }

    public sealed class InterludePreview
    {
        public int TreasurySand;
        public List<RewardOffer> Armory = new List<RewardOffer>();
        public List<RewardOffer> Hourstone = new List<RewardOffer>();
    }

    public sealed class ShopOffer
    {
        public OfferKind Kind;
        public string Id = "";
        public int Price;
        public bool Frozen;                      // free, persists into the next shop (ADR 0009)
        public WeaponTier Tier = WeaponTier.Worn; // weapon offers: act-gated temper (ADR 0015)
    }

    public sealed class ItemRef
    {
        /// <summary>Stable within one run. UI selections survive list reordering by this id.</summary>
        public long InstanceId;
        public ItemKind Kind;
        public string Id = "";
        public WeaponTier Tier = WeaponTier.Worn; // travels with the weapon (ADR 0015)
        /// <summary>Purchase plus forge spend. Resale refunds a percentage of the actual sink.</summary>
        public int SandInvested;
    }

    /// <summary>A rank-up's 1-of-2 spec choice, blocking the shop until resolved (ADR 0009).</summary>
    public sealed class PendingSpec
    {
        public RosterZone Zone;
        public int Index;
        public Rank ForRank;
        public string OptionA = "";
        public string OptionB = "";
    }

    /// <summary>
    /// A hero the player owns. Content by id only (ADR 0008) — the catalog resolves ids to
    /// defs, the Loadout composer (ADR 0005) turns the whole thing into one UnitDef.
    /// </summary>
    public sealed class HeroInstance
    {
        public string ChassisId = "";
        public Rank Rank = Rank.C;
        public string? PathId;                   // set by the B fork (ADR 0009)
        public int GoldSpent;                    // legacy storage name: Sand sunk into card + dupes
        public string? WeaponId;                 // null = chassis starter weapon
        public WeaponTier WeaponTier = WeaponTier.Worn; // temper of the weapon in hand (starter included)
        public long WeaponInstanceId;            // 0 = implicit starter, otherwise stable item id
        public int WeaponSandInvested;            // currently held weapon's purchase + forge spend
        public WeaponTier StarterWeaponTier = WeaponTier.Worn;
        public int StarterWeaponSandInvested;
        public List<string> TrinketIds = new List<string>();
        public long TrinketInstanceId;
        public int TrinketSandInvested;
        public List<string> SpecNodeIds = new List<string>();
        public List<RunBonus> RunBonuses = new List<RunBonus>();  // growth rules (content-granted)
        public List<Status> Earned = new List<Status>();          // run-scoped statuses earned so far

        public HeroInstance Clone()
        {
            var c = new HeroInstance
            {
                ChassisId = ChassisId, Rank = Rank, WeaponId = WeaponId,
                WeaponTier = WeaponTier, WeaponInstanceId = WeaponInstanceId,
                WeaponSandInvested = WeaponSandInvested,
                StarterWeaponTier = StarterWeaponTier,
                StarterWeaponSandInvested = StarterWeaponSandInvested,
                TrinketInstanceId = TrinketInstanceId,
                TrinketSandInvested = TrinketSandInvested,
                PathId = PathId, GoldSpent = GoldSpent,
            };
            c.TrinketIds.AddRange(TrinketIds);
            c.SpecNodeIds.AddRange(SpecNodeIds);
            c.RunBonuses.AddRange(RunBonuses);
            foreach (var s in Earned)
                c.Earned.Add(new Status { Kind = s.Kind, Mag = s.Mag, TicksLeft = s.TicksLeft, SourceId = s.SourceId });
            return c;
        }
    }

    public sealed class GhostUnit
    {
        public HeroInstance Hero = null!;
        public Hex Pos;                          // in owner-half coordinates (rows 0-3)
    }

    /// <summary>Snapshot format per roadmap 1d: act + record + composed loadouts + placement.
    /// Captured at every act boss regardless of result (pitch); record = going INTO the boss,
    /// which is what same-act pool keying matches on (ADR 0002).</summary>
    public sealed class GhostSnapshot
    {
        /// <summary>Which content build produced this board. A snapshot is the artifact most likely
        /// to cross builds or machines (a stored Echo, a leaderboard entry), and re-simulating one
        /// under different content yields a different outcome with no symptom. Stamp it at capture;
        /// compare before trusting it.</summary>
        public string ContentVersion = "";
        public int Act;
        public int WinsAtCapture;
        public int LossesAtCapture;
        public List<GhostUnit> Units = new List<GhostUnit>();
        public List<string> BannerIds = new List<string>();  // ghost boards keep their team rules
    }

    /// <summary>Pure data, serializable-by-construction (ADR 0008). No content refs, no rng.</summary>
    public sealed class RunState
    {
        public ulong Seed;
        /// <summary>
        /// The content fingerprint this run was CREATED under (ADR 0008's `contentVersion`). Not a
        /// gameplay value — provenance. It is here rather than alongside the save file because the
        /// run genuinely belongs to a content build: its encounters are derived from the seed at
        /// fight time, so resuming under different content silently changes the army it was saved
        /// against. Checked by <see cref="RunController.Resume"/>.
        /// Empty on runs created before this existed, which is treated as "unknown, refuse".
        /// </summary>
        public string ContentVersion = "";
        public int Act = 1;                      // 1-based
        public int NodeIndex;                    // 0..NodesPerAct-1; == NodesPerAct means act boss
        public RunPhase Phase = RunPhase.Planning;
        public int Gold;
        public int FieldSlots;
        public int UnlockedFieldSlots;
        public int SlotsBought;                  // indexes escalating slot cost (ADR 0006)
        public bool SlotOfferPending;
        public bool JustClosedBoss;              // compatibility only; unified Planning advances directly
        public List<HeroInstance> Field = new List<HeroInstance>();
        public List<HeroInstance> Bench = new List<HeroInstance>();
        public List<ShopOffer?> ShopOffers = new List<ShopOffer?>();  // null = bought/empty slot
        public int ShopRolls;                    // generation counter — stateless shop rng (ADR 0008)
        public long NextItemInstanceId = 1;
        public List<ItemRef> Inventory = new List<ItemRef>();
        public List<string> Banners = new List<string>();
        public PendingSpec? PendingSpec;
        public List<string> PendingBossRewards = new List<string>();
        public int PendingBossSand;
        public int BossWins;                     // plain tally now — NOT a best-of-5 record
        public int BossLosses;
        public NodeKind[][] ActMaps = new NodeKind[0][];   // [act-1][nodeIndex], generated at start

        /// <summary>ADR 0016: a completed run is a real PvE victory — every act's boss beaten.
        /// (Was best-of-5 `BossWins >= 3` under the superseded ghost-boss design.)</summary>
        public bool Victory => Phase == RunPhase.Complete;

        public bool Over => Phase == RunPhase.Complete || Phase == RunPhase.Defeated;

        public int Sand { get => Gold; set => Gold = value; }
        public List<string> Inscriptions => Banners;
    }
}
