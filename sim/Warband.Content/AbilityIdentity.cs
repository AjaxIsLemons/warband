using System.Collections.Generic;

namespace Warband.Content
{
    /// <summary>
    /// Which ability a unit actually casts. Composition resolves a signature by walking the spec
    /// nodes in trait order (Loadout.Compose): an override replaces the signature, a patch reshapes
    /// it. Either way the node CHANGED the cast, so the last such node is the ability that ships.
    /// Resolving that here, in the DLL the client already loads, means the renderer keys its cast
    /// tells on the same id the sim composed from, with no second copy of the law.
    /// </summary>
    public static class AbilityIdentity
    {
        /// <summary>The resolved ability id — the last signature-changing node's id, or the chassis
        /// id for a unit still casting its stock signature. Traits carry node ids (Compose stamps
        /// Traits with node.Name, which Kits.Node sets to the dictionary key); trinket traits are
        /// display names, miss the dictionary, and are skipped.
        /// Patches count for exactly the same reason overrides do: Everburn's fire and Sarissa's
        /// board-length lunge are different casts from the ones they inherited, and a tell keyed on
        /// the chassis would say otherwise.</summary>
        public static string Resolve(string chassisId, IReadOnlyList<string> traits)
        {
            string ability = chassisId;
            foreach (var t in traits)
                if (Kits.Nodes.TryGetValue(t, out var node) &&
                    (node.SignatureOverride != null || node.SignaturePatch != null))
                    ability = node.Name;
            return ability;
        }

        /// <summary>Player-facing name for a resolved ability id — a node's if it names one,
        /// else the base Signature's, else the raw id (un-authored content stays readable).</summary>
        public static string DisplayName(string abilityId) =>
            ContentLexicon.Signature(abilityId).Name;
    }
}
