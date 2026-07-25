using System.Collections.Generic;

namespace Warband.Sim
{
    public enum Affects { All, Allies, Enemies }

    /// <summary>
    /// What a field DOES to whoever stands in it, in render terms. The client colors zones by
    /// meaning, not by which hero made them (render-polish.md color language: green=heal ·
    /// cyan=buff · purple=debuff · red=damage), so this is the field half of that vocabulary.
    /// Orthogonal to <see cref="FieldDef.IsWall"/> — a burning wall is a wall AND a Hazard.
    /// </summary>
    public enum FieldFlavor { Neutral, Hazard, Boon, Buff, Debuff }

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

        /// <summary>Derived, not authored: the renderer's read on this glyph. Pulse is the loud
        /// channel (it acts on occupants every pulse) so it wins; Presence is the quiet one.
        /// <see cref="Affects"/> already states the author's intent — a glyph aimed at Enemies is
        /// hostile whatever it applies — so only the ambiguous <see cref="Affects.All"/> case has
        /// to look at the effects themselves, and no status good/bad table is needed anywhere.</summary>
        public FieldFlavor Flavor()
        {
            if (Pulse.Count > 0)
            {
                if (PulseAffects == Affects.Enemies) return FieldFlavor.Hazard;
                if (PulseAffects == Affects.Allies) return FieldFlavor.Boon;
                foreach (var e in Pulse)
                    if (e.Kind == EffectKind.Damage || e.Kind == EffectKind.Execute || e.Kind == EffectKind.Swing)
                        return FieldFlavor.Hazard;
                foreach (var e in Pulse)
                    if (e.Kind == EffectKind.Heal || e.Kind == EffectKind.GrantShield || e.Kind == EffectKind.GrantMana)
                        return FieldFlavor.Boon;
                // An everyone-field that only applies statuses: whether that's a gift or a
                // punishment needs a status good/bad table we deliberately don't keep. Stay
                // Neutral (grey) rather than confidently coloring it wrong.
                return FieldFlavor.Neutral;
            }
            if (Presence.Count > 0)
                return PresenceAffects == Affects.Enemies ? FieldFlavor.Debuff : FieldFlavor.Buff;
            return FieldFlavor.Neutral;   // a bare wall, a projectile-only blessing, a marker
        }
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
        public FieldFlavor Flavor;         // what it does to occupants — drives the zone's color
        public int AttachedTo = -1;
        public int Radius;
        public List<Hex> Hexes = new List<Hex>();   // static fields only; attached derive from the anchor
    }
}
