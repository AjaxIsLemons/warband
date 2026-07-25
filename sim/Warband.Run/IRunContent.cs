using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>
    /// The content boundary (ADR 0008): RunState stores ids, this resolves them. Also the
    /// seam where encounters and ghost bosses come from — bot-ghost generation (roadmap 1d)
    /// and the server-backed pool (roadmap 5) plug in here without touching the machine.
    /// </summary>
    /// <summary>A team relic: whole-team rules that ride into battle as team triggers.</summary>
    public sealed class BannerDef
    {
        public string Name = "banner";
        public List<Trigger> TeamTriggers = new List<Trigger>();
    }

    public interface IRunContent
    {
        ChassisDef Chassis(string id);
        WeaponDef Weapon(string id);
        TrinketDef Trinket(string id);
        SpecNode Node(string id);
        BannerDef Banner(string id);

        /// <summary>Shop pools (ADR 0009): infinite weighted draws, act-anchored.</summary>
        IReadOnlyList<string> HeroPool(int act);
        IReadOnlyList<string> WeaponPool(int act);
        IReadOnlyList<string> TrinketPool(int act);
        IReadOnlyList<string> BannerPool(int act);

        /// <summary>The 1-of-2 spec choice a rank-up presents. The choice made at
        /// ForkRank(chassis) is the hero's path; other ranks offer in-path (or
        /// path-agnostic) nodes. One flat content table = trivially retunable offers.</summary>
        (string A, string B) SpecOptions(string chassisId, Rank rank, string? pathId);

        /// <summary>Which rank-up IS the fork. B for most classes; A for late-bloomers
        /// (Shade — ADR 0011 late-bloomer law).</summary>
        Rank ForkRank(string chassisId);

        /// <summary>Monster comp for a fight node. Positions in enemy half (rows 4-7).
        /// Difficulty must anchor to act + tier, never W/L (ADR 0002 law).</summary>
        List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng);

        /// <summary>
        /// The act boss: an AUTHORED comp, same shape as a node encounter (ADR 0016 replaced
        /// mandatory ghost bosses — the encounter itself is the boss). Anchored to the act and
        /// nothing else: difficulty must never key off the player's record (ADR 0002 law), and
        /// with no best-of-5 there is no record to key off anyway.
        /// </summary>
        List<(UnitDef Def, Hex Pos)> Boss(int act, Rng rng);
    }
}
