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

        /// <summary>The 1-of-2 spec choice a rank-up presents. At B the chosen id is also
        /// the hero's path; A/S options are scoped by that path (heroes.md).</summary>
        (string A, string B) SpecOptions(string chassisId, Rank rank, string? pathId);

        /// <summary>Monster comp for a fight node. Positions in enemy half (rows 4-7).
        /// Difficulty must anchor to act + tier, never W/L (ADR 0002 law).</summary>
        List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng);

        /// <summary>Act-boss ghost from the pool keyed by act + record (ADR 0002).</summary>
        GhostSnapshot BossGhost(int act, int bossWins, Rng rng);
    }
}
