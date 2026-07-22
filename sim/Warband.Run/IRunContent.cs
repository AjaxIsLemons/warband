using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>
    /// The content boundary (ADR 0008): RunState stores ids, this resolves them. Also the
    /// seam where encounters and ghost bosses come from — bot-ghost generation (roadmap 1d)
    /// and the server-backed pool (roadmap 5) plug in here without touching the machine.
    /// </summary>
    public interface IRunContent
    {
        ChassisDef Chassis(string id);
        WeaponDef Weapon(string id);
        TrinketDef Trinket(string id);
        SpecNode Node(string id);

        /// <summary>Monster comp for a fight node. Positions in enemy half (rows 4-7).
        /// Difficulty must anchor to act + tier, never W/L (ADR 0002 law).</summary>
        List<(UnitDef Def, Hex Pos)> Encounter(int act, int nodeIndex, FightTier tier, Rng rng);

        /// <summary>Act-boss ghost from the pool keyed by act + record (ADR 0002).</summary>
        GhostSnapshot BossGhost(int act, int bossWins, Rng rng);
    }
}
