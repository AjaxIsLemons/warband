using System.Collections.Generic;
using Warband.Run;
using Warband.Sim;

namespace Warband.Content
{
    /// <summary>
    /// Player-facing preparation data for one hero in the first PvE proof. Combat truth still
    /// lives in Kits/Weapons; this layer owns the small authored choice set and the concise copy
    /// a preparation screen needs. It is intentionally separate from Unity presentation.
    /// </summary>
    public sealed class SkirmishHeroDef
    {
        public string HeroId { get; }
        public string ChampionName { get; }
        public string Role { get; }
        public string PassiveName { get; }
        public string PassiveText { get; }
        public string SignatureName { get; }
        public string SignatureText { get; }
        public Hex DefaultPosition { get; }
        public bool StartsInReserve { get; }
        public IReadOnlyList<string> WeaponIds { get; }

        public SkirmishHeroDef(
            string heroId,
            string championName,
            string role,
            string passiveName,
            string passiveText,
            string signatureName,
            string signatureText,
            Hex defaultPosition,
            bool startsInReserve,
            params string[] weaponIds)
        {
            HeroId = heroId;
            ChampionName = championName;
            Role = role;
            PassiveName = passiveName;
            PassiveText = passiveText;
            SignatureName = signatureName;
            SignatureText = signatureText;
            DefaultPosition = defaultPosition;
            StartsInReserve = startsInReserve;
            WeaponIds = weaponIds;
        }
    }

    /// <summary>
    /// Presentation copy for a weapon's latent mastery rider. The runnable effect remains on
    /// WeaponDef; authored copy prevents the client from trying to reverse-engineer triggers.
    /// </summary>
    public sealed class WeaponMasteryCopy
    {
        public string Name { get; }
        public string Text { get; }

        public WeaponMasteryCopy(string name, string text)
        {
            Name = name;
            Text = text;
        }
    }

    /// <summary>
    /// The smallest useful preparation catalog for The Last Oath. Each hero gets their starter,
    /// a second natural specialization, and one deliberately off-label wardrobe experiment.
    /// These are free testing choices, not a claim about run inventory or shop ownership.
    /// </summary>
    public static class SkirmishProof
    {
        public const int FieldCapacity = 3;
        public const int BenchCapacity = 2;

        public static readonly IReadOnlyList<SkirmishHeroDef> Heroes =
            new List<SkirmishHeroDef>
            {
                new SkirmishHeroDef(
                    "bulwark",
                    "Brakka, Shieldmaid of the Bronze Hour",
                    "Frontline control",
                    "Bastion",
                    "Starts combat with 30 Shield.",
                    "Shield Slam",
                    "Deals 10 damage to the nearest enemy and Stuns it for 1.0s.",
                    Hex.FromRowCol(2, 2),
                    false,
                    "towershield", "mace", "censer"),

                new SkirmishHeroDef(
                    "pyromancer",
                    "Ilion-7, Cinder of a Dead Star",
                    "Burn and field pressure",
                    "Firebrand",
                    "Damaging auto attacks apply 1 Burn.",
                    "Fire Glyph",
                    "Ignites the target hex and its neighbors for 8.0s; enemies inside build Burn.",
                    Hex.FromRowCol(0, 1),
                    false,
                    "staff", "censer", "towershield"),

                new SkirmishHeroDef(
                    "sharpshot",
                    "Calamity Vance, the Last Deadeye",
                    "Long-range damage",
                    "Full Draw",
                    "Gains +2 Attack per hex to the current target.",
                    "Piercing Bolt",
                    "Deals 14 damage through the current target and every enemy behind it.",
                    Hex.FromRowCol(0, 4),
                    false,
                    "bow", "musket", "daggers"),

                new SkirmishHeroDef(
                    "cleric",
                    "Sister Maren of the Waning Bell",
                    "Sustain and formation support",
                    "Mercy Aura",
                    "Nearby allies start combat with Regen.",
                    "Sanctified Pyre",
                    "Damages nearby enemies and heals nearby allies.",
                    Hex.FromRowCol(1, 3),
                    true,
                    "censer", "staff", "mace"),
            };

        public static readonly IReadOnlyDictionary<string, WeaponMasteryCopy> MasteryCopy =
            new Dictionary<string, WeaponMasteryCopy>
            {
                ["daggers"] = new WeaponMasteryCopy(
                    "Keen Pair", "Starts combat with +15% Crit Chance."),
                ["sabre"] = new WeaponMasteryCopy(
                    "Aftercast Edge", "The first auto after each cast is a guaranteed crit."),
                ["mace"] = new WeaponMasteryCopy(
                    "Tempered Rhythm", "Damaging autos gain the normal attack Mana a second time."),
                ["greataxe"] = new WeaponMasteryCopy(
                    "Carry the Blow", "Overkill damages the next enemy nearest the corpse."),
                ["towershield"] = new WeaponMasteryCopy(
                    "Hold Fast", "Damaging autos grant 3 Shield."),
                ["pike"] = new WeaponMasteryCopy(
                    "Brace", "Deals +30% auto damage to enemies engaged with an ally."),
                ["censer"] = new WeaponMasteryCopy(
                    "Sanctuary Smoke", "Overhealing becomes Shield."),
                ["staff"] = new WeaponMasteryCopy(
                    "Afterburn", "Casting grants +30% Attack Speed for 2.0s."),
                ["bow"] = new WeaponMasteryCopy(
                    "Long Sight", "Adds +1 attack range."),
                ["musket"] = new WeaponMasteryCopy(
                    "Opening Volley", "The first auto of combat deals double damage."),
                ["standard"] = new WeaponMasteryCopy(
                    "Company Muster", "Adjacent allies start with +10% Attack Speed."),
            };

        /// <summary>
        /// Capacity-driven draft for the live Planning proof. Content owns which heroes and
        /// loadout options exist; Warband.Run owns transactional editing and history.
        /// </summary>
        public static PlanningDraft CreatePlanningDraft(
            int fieldCapacity = FieldCapacity,
            int benchCapacity = BenchCapacity)
        {
            var draft = new PlanningDraft
            {
                FieldCapacity = fieldCapacity,
                BenchCapacity = benchCapacity,
            };

            int benchSlot = 0;
            foreach (var hero in Heroes)
            {
                var state = new PlanningHeroState
                {
                    Id = hero.HeroId,
                    ContentId = hero.HeroId,
                    Zone = hero.StartsInReserve ? PlanningZone.Bench : PlanningZone.Field,
                    BenchSlot = hero.StartsInReserve ? benchSlot++ : -1,
                    Position = hero.DefaultPosition,
                };
                state.Loadout["weapon"] = hero.WeaponIds[0];
                draft.Heroes.Add(state);
            }
            return draft;
        }

        public static SkirmishHeroDef? Hero(string heroId)
        {
            foreach (var hero in Heroes)
                if (hero.HeroId == heroId)
                    return hero;
            return null;
        }
    }

    /// <summary>Pure content legality adapter for the generic Planning session.</summary>
    public sealed class SkirmishPlanningRules : PlanningRules
    {
        public override bool IsLegalPosition(Hex position) =>
            Battle.InBounds(position) && position.Row <= 2;

        public override bool CanSetLoadoutOption(
            PlanningDraft draft,
            PlanningHeroState hero,
            string slotId,
            string optionId,
            out string reason)
        {
            var definition = SkirmishProof.Hero(hero.ContentId);
            if (slotId != "weapon" || definition == null)
            {
                reason = "That loadout slot is not available.";
                return false;
            }

            foreach (var weaponId in definition.WeaponIds)
                if (weaponId == optionId)
                {
                    reason = "";
                    return true;
                }

            reason = "That weapon is not owned in this proof.";
            return false;
        }

        public override void ValidateContent(
            PlanningDraft draft,
            PlanningValidationMode mode,
            PlanningValidation validation)
        {
            foreach (var hero in draft.Heroes)
            {
                var definition = SkirmishProof.Hero(hero.ContentId);
                if (definition == null)
                {
                    validation.Error(
                        "unknown-proof-hero",
                        "A Planning hero is missing from the proof catalog.",
                        hero.Id);
                    continue;
                }

                if (!hero.Loadout.TryGetValue("weapon", out string weaponId) ||
                    !CanSetLoadoutOption(draft, hero, "weapon", weaponId, out _))
                    validation.Error(
                        "proof-weapon-required",
                        $"{definition.HeroId} needs one owned proof weapon.",
                        hero.Id);
            }
        }
    }
}
