using System.Collections.Generic;

namespace Warband.Sim
{
    public enum Affects { All, Allies, Enemies }

    /// <summary>
    /// Content-side glyph spec (combat-grammar.md pillar 2). A field is area + rule +
    /// duration. Three rule channels, any combination:
    /// - Pulse: effects applied to occupants every pulse (fire, healing ground, font)
    /// - Wall: impassable + blocks projectile paths (the only obstacle in the game)
    /// - Projectile: modifies ranged attacks whose path crosses the field
    /// </summary>
    public sealed class FieldDef
    {
        public int Radius;                 // hexes around the cast center (0 = single hex)
        public int Ticks = -1;             // <0 = rest of the fight
        public bool IsWall;
        public Affects PulseAffects = Affects.All;
        public List<EffectDef> Pulse = new List<EffectDef>();            // Select ignored: hits each occupant
        public Affects ProjectileAffects = Affects.All;
        public int ProjectileBonus;                                       // flat damage added to crossing shots
        public List<EffectDef> ProjectileRiders = new List<EffectDef>(); // applied to the hit target
    }

    public sealed class Field
    {
        public int Id;
        public int OwnerId = -1;
        public int OwnerTeam = -1;         // -1 = environmental
        public int TicksLeft;              // <0 permanent
        public FieldDef Def = null!;
        public HashSet<Hex> Hexes = new HashSet<Hex>();  // membership only — never iterated
        public List<Hex> HexList = new List<Hex>();      // deterministic order for emission/hashing

        public bool Covers(Hex h) => Hexes.Contains(h);

        public static bool TeamMatches(Affects affects, int ownerTeam, int otherTeam) =>
            affects == Affects.All ||
            (affects == Affects.Allies ? otherTeam == ownerTeam : otherTeam != ownerTeam);
    }

    /// <summary>Renderer/fold view of a field (tracked from Field* events).</summary>
    public sealed class PlaybackField
    {
        public int Id;
        public bool IsWall;
        public List<Hex> Hexes = new List<Hex>();
    }
}
