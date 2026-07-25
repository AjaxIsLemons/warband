using System.Collections.Generic;

namespace Warband.Content
{
    /// <summary>
    /// Which ability a unit actually casts. Composition resolves a signature by letting each
    /// spec node with a SignatureOverride replace the one before it (Loadout.Compose), so the
    /// LAST override in trait order is the cast that ships — every earlier one was overwritten.
    /// Resolving that here, in the DLL the client already loads, means the renderer keys its
    /// cast tells on the same id the sim composed from, with no second copy of the law.
    /// </summary>
    public static class AbilityIdentity
    {
        /// <summary>The resolved ability id — the last override node's id, or the chassis id
        /// for a unit still casting its stock signature. Traits carry node ids (Compose stamps
        /// Traits with node.Name, which Kits.Node sets to the dictionary key); trinket traits
        /// are display names, miss the dictionary, and are skipped.</summary>
        public static string Resolve(string chassisId, IReadOnlyList<string> traits)
        {
            string ability = chassisId;
            foreach (var t in traits)
                if (Kits.Nodes.TryGetValue(t, out var node) && node.SignatureOverride != null)
                    ability = node.Name;
            return ability;
        }

        /// <summary>Player-facing name for a resolved ability id — a node's if it names one,
        /// else the chassis's, else the raw id (un-authored content stays readable).</summary>
        public static string DisplayName(string abilityId)
        {
            if (ContentLexicon.Nodes.TryGetValue(abilityId, out var node)) return node.Name;
            if (ContentLexicon.Chassis_.TryGetValue(abilityId, out var chassis)) return chassis.Name;
            return abilityId;
        }
    }
}
