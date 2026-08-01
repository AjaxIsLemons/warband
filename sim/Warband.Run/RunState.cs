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
        /// <summary>
        /// The authored run is won, but the player has not yet chosen whether to retire or carry
        /// the same warband Beyond the Hour. This is saveable and non-terminal.
        /// </summary>
        VictoryChoice,

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
        public List<string> PendingOptions = new List<string>();
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

    /// <summary>
    /// A rank-up's spec choice, blocking the shop until resolved (ADR 0009). Options is the
    /// drawn offer in presentation order — already narrowed from the authored pool, so nothing
    /// downstream needs the seed or the pool to render or resolve it.
    /// </summary>
    public sealed class PendingSpec
    {
        public RosterZone Zone;
        public int Index;
        public Rank ForRank;
        public List<string> Options = new List<string>();
    }

    /// <summary>
    /// A hero the player owns. Content by id only (ADR 0008) — the catalog resolves ids to
    /// defs, the Loadout composer (ADR 0005) turns the whole thing into one UnitDef.
    /// </summary>
    public sealed class HeroInstance
    {
        /// <summary>
        /// Stable within one run. Roster addresses may change as heroes move between field and
        /// bench; UI commands must use this identity instead of a transient list index.
        /// </summary>
        public long InstanceId;
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
                InstanceId = InstanceId,
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
        public int Act = 1;                      // 1-based; 4+ are virtual endless acts
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
        public long NextHeroInstanceId = 1;
        public long NextItemInstanceId = 1;
        public List<ItemRef> Inventory = new List<ItemRef>();
        public List<string> Banners = new List<string>();
        public PendingSpec? PendingSpec;
        public List<string> PendingBossRewards = new List<string>();
        public int PendingBossSand;
        public int BossWins;                     // plain tally now — NOT a best-of-5 record
        public int BossLosses;
        public NodeKind[][] ActMaps = new NodeKind[0][];   // [act-1][nodeIndex], generated at start
        public RevisionState Revision = new RevisionState();

        /// <summary>
        /// ADR 0030: the standard victory is banked the instant the Waning Crown falls. It remains
        /// true through the continuation choice and an eventual endless defeat. `Complete` stays
        /// accepted for saves written before the explicit banked-victory field existed.
        /// </summary>
        public bool VictoryBanked;
        public bool InEndless;
        public int EndlessCycles;                // completed three-fight-plus-Crown cycles
        public int EndlessBeat;                  // combat wins in the current cycle, 0..3

        public bool Victory => VictoryBanked || Phase == RunPhase.Complete;
        public bool AwaitingEndlessChoice => Phase == RunPhase.VictoryChoice;
        public bool EndlessDefeat => InEndless && Victory && Phase == RunPhase.Defeated;

        public bool Over => Phase == RunPhase.Complete || Phase == RunPhase.Defeated;

        public int Sand { get => Gold; set => Gold = value; }

        /// <summary>PERMANENT inscriptions. Additions still land here; timed ones live in
        /// <see cref="Timed"/> so this list keeps its exact save format and semantics.</summary>
        public List<string> Inscriptions => Banners;

        /// <summary>Inscriptions with a combat countdown running. Separate from
        /// <see cref="Banners"/> on purpose: a `List&lt;string&gt;` cannot carry a remaining count,
        /// and keeping the permanent list untouched means every existing save still loads.</summary>
        public List<TimedInscription> Timed = new List<TimedInscription>();

        /// <summary>What actually rides into battle: permanent + every timed one still breathing.
        /// Battle prep and the "do you already own this" checks both read THIS, never
        /// <see cref="Banners"/> alone.</summary>
        public IEnumerable<string> ActiveInscriptionIds
        {
            get
            {
                foreach (string id in Banners) yield return id;
                foreach (var t in Timed) if (t.FightsRemaining > 0) yield return t.Id;
            }
        }

        public bool HasInscription(string id) =>
            Banners.Contains(id) || Timed.Exists(t => t.Id == id && t.FightsRemaining > 0);
    }

    /// <summary>One inscription counting down in COMBATS. `FightsRemaining` is the number the tray
    /// shows the player — the remaining count, never the rule (the rule is already on the card).</summary>
    public sealed class TimedInscription
    {
        public string Id = "";
        public int FightsRemaining;

        /// <summary>The combat index this was taken at, purely so telemetry can answer "did the
        /// player take timed costs early or late?" without reconstructing it from the trail.</summary>
        public int TakenAtCombat;
    }
}
