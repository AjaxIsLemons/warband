namespace Warband.Sim
{
    /// <summary>
    /// The render layer keys "tells" (the visual/audio feedback for an event) on more than
    /// <see cref="EventKind"/> alone — a Burn tick should read differently from a sword hit, and
    /// each status wants its own pip burst, and a healing glyph should not be the same color as a
    /// fire glyph. A tell rule declares an EventKind plus OPTIONAL <see cref="Cause"/> /
    /// <see cref="StatusKind"/> / <see cref="FieldFlavor"/> filters; the most specific matching rule
    /// wins (so a generic "Damage" tell is a fallback and "Damage/Burn" overrides it for burns).
    /// This is the client's dispatch brain, kept here so it's headless-testable and shares the
    /// event model with the sim (no drift). The Unity FeedbackDirector is a thin executor over it.
    /// </summary>
    public static class TellMatch
    {
        /// <summary>Does a rule (kind + optional cause/status/field/ranged/chassis filters) match
        /// this event? <paramref name="distance"/> is the hex distance between the event's two unit
        /// endpoints, a VIEW-time fact the client computes from fold positions at dispatch — the
        /// event itself doesn't carry it, so it's optional context (null when the event has no two
        /// endpoints, e.g. a field tick or a status expiry). <paramref name="sourceChassis"/> is the
        /// same kind of view context: the SOURCE unit's ChassisId from the fold, so a rule can give
        /// the Pyromancer's cast a different tell than the Cleric's without the event carrying
        /// ability identity (the flagged growth path in directed-tells). <paramref name="sourceAbility"/>
        /// is the same context one level narrower: the source's RESOLVED ability id
        /// (Content.AbilityIdentity), so the Pyromancer's Starfall can look different from her
        /// stock bolt. <paramref name="sourceWeapon"/> is the last of the same family: the source's
        /// WeaponName off the fold, so a Greataxe swing can hang where Twin Daggers snick — the
        /// per-weapon attack language in combat-spectacle §6, which autos could not reach while
        /// chassis was the narrowest thing an Attack row could name.</summary>
        public static bool Matches(BattleEvent e, EventKind kind, Cause? cause, StatusKind? status,
                                   FieldFlavor? flavor = null, bool? ranged = null, int? distance = null,
                                   string? chassis = null, string? sourceChassis = null,
                                   string? ability = null, string? sourceAbility = null,
                                   string? weapon = null, string? sourceWeapon = null)
        {
            if (e.Kind != kind) return false;
            if (cause.HasValue && e.Cause != cause.Value) return false;
            if (status.HasValue)
            {
                bool isStatusEvent = e.Kind == EventKind.StatusApplied || e.Kind == EventKind.StatusExpired;
                if (!isStatusEvent || (StatusKind)e.Aux != status.Value) return false;
            }
            if (flavor.HasValue)
            {
                // Only FieldCreated carries a flavor (Aux3); FieldHex/FieldExpired reference the
                // zone by id, so a flavor-filtered rule simply doesn't apply to them.
                if (e.Kind != EventKind.FieldCreated || e.Flavor != flavor.Value) return false;
            }
            if (ranged.HasValue)
            {
                // "Ranged" is the sim's own projectile law: an attack over hex distance ≥2 traces
                // the hex line (Battle.cs:254). Keying the same threshold here means the renderer
                // and sim can never disagree about what "ranged" means. A ranged-filtered rule
                // needs distance context and never matches without it (events with no two unit
                // endpoints pass distance=null) — a melee lunge and a projectile tracer must not
                // fall back to each other when we can't tell them apart.
                if (!distance.HasValue || (distance.Value >= 2) != ranged.Value) return false;
            }
            if (!string.IsNullOrEmpty(chassis))
            {
                // A chassis-filtered rule needs source context and never matches without it, same
                // law as ranged: a chassis-specific cast look must not fire for an unknown caster.
                if (string.IsNullOrEmpty(sourceChassis)
                    || !string.Equals(chassis, sourceChassis, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(ability))
            {
                // Same law again: an ability-specific look must not fire for an unresolved caster.
                if (string.IsNullOrEmpty(sourceAbility)
                    || !string.Equals(ability, sourceAbility, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(weapon))
            {
                // And once more: a Musket's smoke line must not fire for a unit whose weapon the
                // view can't name. Matched on the catalog's WeaponName ("Twin Daggers", "Greataxe")
                // — the identity block the fold already carries, so this needs no event change.
                if (string.IsNullOrEmpty(sourceWeapon)
                    || !string.Equals(weapon, sourceWeapon, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>How specific a rule is — more declared filters win ties toward the narrower
        /// rule. Kind is always required so it isn't counted. Equal specificity keeps the FIRST
        /// matching rule in registry order (the dispatcher uses a strict &gt;).
        /// <para>Ability is the one WEIGHTED filter: it counts 2, not 1, so this is no longer a
        /// plain count of declared filters. Every ability belongs to exactly one chassis, so a
        /// byAbility rule is strictly narrower than a byChassis one — at 1 the two would tie and
        /// the winner would fall to registry order, which is not a decision anyone authored.</para>
        /// <para>Weapon counts 1 — a PEER of chassis, deliberately unlike ability. Weapons and
        /// chassis cross freely (any hero may carry any weapon), so neither contains the other and
        /// there is no truthful ordering between them: a byWeapon row TIES a byChassis row and the
        /// tie falls to registry order. If authoring ever needs weapon to outrank chassis, bump this
        /// consciously and say why — do not discover it from a row that mysteriously lost.</para></summary>
        public static int Specificity(Cause? cause, StatusKind? status, FieldFlavor? flavor = null,
                                      bool? ranged = null, string? chassis = null, string? ability = null,
                                      string? weapon = null)
            => (cause.HasValue ? 1 : 0) + (status.HasValue ? 1 : 0) + (flavor.HasValue ? 1 : 0)
               + (ranged.HasValue ? 1 : 0) + (string.IsNullOrEmpty(chassis) ? 0 : 1)
               + (string.IsNullOrEmpty(ability) ? 0 : 2)
               + (string.IsNullOrEmpty(weapon) ? 0 : 1);
    }
}
