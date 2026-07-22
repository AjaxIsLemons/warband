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
        public bool AttachToOwner;         // aura: field follows the creating unit, dies with it
        public Affects PulseAffects = Affects.All;
        public List<EffectDef> Pulse = new List<EffectDef>();            // Select ignored: hits each occupant
        public Affects PresenceAffects = Affects.Allies;
        public List<(StatusKind Kind, int Mag)> Presence = new List<(StatusKind, int)>(); // statuses while standing inside
        public Affects ProjectileAffects = Affects.All;
        public int ProjectileBonus;                                       // flat damage added to crossing shots
        public List<EffectDef> ProjectileRiders = new List<EffectDef>(); // applied to the hit target
    }

    public sealed class Field
    {
        public int Id;
        public int OwnerId = -1;
        public int OwnerTeam = -1;         // -1 = environmental
        public int AttachedUnitId = -1;    // aura anchor; -1 = static
        public int TicksLeft;              // <0 permanent
        public FieldDef Def = null!;
        public List<Hex> StaticHexes = new List<Hex>();  // static fields only, emission order

        public static bool TeamMatches(Affects affects, int ownerTeam, int otherTeam) =>
            affects == Affects.All ||
            (affects == Affects.Allies ? otherTeam == ownerTeam : otherTeam != ownerTeam);
    }

    /// <summary>Renderer/fold view of a field (tracked from Field* events).</summary>
    public sealed class PlaybackField
    {
        public int Id;
        public bool IsWall;
        public int AttachedTo = -1;
        public int Radius;
        public List<Hex> Hexes = new List<Hex>();   // static fields only; attached derive from the anchor
    }
}
