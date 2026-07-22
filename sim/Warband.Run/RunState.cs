using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    public enum Rank { C, B, A, S }

    /// <summary>Placeholder tier names (ADR 0007) — rename with theme/lore.</summary>
    public enum FightTier { Safe, Even, Greedy }

    public enum NodeKind { Fight, Event, Boss }

    public enum RunPhase { Node, Shop, Complete }

    public enum OfferKind { Hero, Weapon, Trinket, Banner }

    public enum ItemKind { Weapon, Trinket }

    public enum RosterZone { Field, Bench }

    public sealed class ShopOffer
    {
        public OfferKind Kind;
        public string Id = "";
        public int Price;
        public bool Frozen;                      // free, persists into the next shop (ADR 0009)
    }

    public sealed class ItemRef
    {
        public ItemKind Kind;
        public string Id = "";
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
        public int GoldSpent;                    // card + dupes — sell-back refunds 50% of this
        public string? WeaponId;                 // null = chassis starter weapon
        public List<string> TrinketIds = new List<string>();
        public List<string> SpecNodeIds = new List<string>();
        public List<RunBonus> RunBonuses = new List<RunBonus>();  // growth rules (content-granted)
        public List<Status> Earned = new List<Status>();          // run-scoped statuses earned so far

        public HeroInstance Clone()
        {
            var c = new HeroInstance
            {
                ChassisId = ChassisId, Rank = Rank, WeaponId = WeaponId,
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
        public int Act = 1;                      // 1-based
        public int NodeIndex;                    // 0..NodesPerAct-1; == NodesPerAct means act boss
        public RunPhase Phase = RunPhase.Node;
        public int Gold;
        public int FieldSlots;
        public int SlotsBought;                  // indexes escalating slot cost (ADR 0006)
        public bool SlotOfferPending;            // open during the post-boss shop while under cap
        public bool JustClosedBoss;              // shop-exit routing: next act vs next node
        public List<HeroInstance> Field = new List<HeroInstance>();
        public List<HeroInstance> Bench = new List<HeroInstance>();
        public List<ShopOffer?> ShopOffers = new List<ShopOffer?>();  // null = bought/empty slot
        public int ShopRolls;                    // generation counter — stateless shop rng (ADR 0008)
        public List<ItemRef> Inventory = new List<ItemRef>();
        public List<string> Banners = new List<string>();
        public PendingSpec? PendingSpec;
        public int BossWins;
        public int BossLosses;
        public NodeKind[][] ActMaps = new NodeKind[0][];   // [act-1][nodeIndex], generated at start
        public List<GhostSnapshot> CapturedGhosts = new List<GhostSnapshot>();

        public bool Victory => BossWins >= 3;    // best-of-5 (ADR 0002)
        public bool Flawless => BossWins == 5;
    }
}
