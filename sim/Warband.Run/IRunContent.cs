using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    /// <summary>
    /// The content boundary (ADR 0008): RunState stores ids, this resolves them. Also the
    /// seam where encounters and ghost bosses come from — bot-ghost generation (roadmap 1d)
    /// and the server-backed pool (roadmap 5) plug in here without touching the machine.
    /// </summary>
    /// <summary>A law in the Hourstone: whole-team rules that ride into battle as team triggers
    /// (ADR 0017/0026 — the layer legacy code called Banners).</summary>
    public sealed class InscriptionDef
    {
        public string Name = "inscription";
        public List<Trigger> TeamTriggers = new List<Trigger>();

        /// <summary>Rule-rewrites with a real drawback (ADR 0017 family six). Paradoxes are
        /// reachable ONLY through boss rewards — the Workshop and the Hourstone Interlude filter
        /// them out — so the commitment always arrives as a considered choice, never shop filler.</summary>
        public bool Paradox;
    }

    /// <summary>
    /// One previewed enemy body, exactly as it will spawn. Numbers are POST-scaling: the brief is
    /// built from the same scaled comp the fight uses, so the health the player reads is the
    /// health they fight.
    ///
    /// <see cref="ChassisId"/> is a RENDER KEY and nothing else — authored monsters borrow a hero
    /// silhouette because bespoke enemy art does not exist yet. A presentation layer may use it to
    /// pick a mesh or an icon; it may NOT use it to name the unit or to describe what it does. That
    /// mistake shipped: the shell titled every enemy card with the hero whose silhouette it
    /// borrowed, so an Hourling previewed as "Shade" with the Shade's ability text.
    /// </summary>
    public sealed class EncounterUnitBrief
    {
        public string Name = "";
        public string Role = "";
        public string RoleId = "";
        public string Accent = "";
        public string ChassisId = "";
        public string WeaponName = "";
        public int MaxHp;
        public int Attack;
        public int AttackIntervalTicks;
        public int Range;
        public int Row;
        /// <summary>Targeting, triggers and signature in one plain sentence — the part of
        /// "know the rules" that a stat line cannot carry.</summary>
        public string Behavior = "";
    }

    /// <summary>
    /// What the player is owed before they lock deployment (pve-encounters.md, "know the rules, not
    /// the result"): the encounter's identity, its one-sentence pressure, the plain-language rule
    /// that makes it dangerous, and every body it will field. Text and public stats only — the run
    /// layer never forecasts an outcome.
    /// </summary>
    public sealed class EncounterBrief
    {
        public string Id = "";
        public string Name = "";
        public string Pressure = "";
        public string RuleName = "";
        public string RuleText = "";
        public List<EncounterUnitBrief> Units = new List<EncounterUnitBrief>();
    }

    public interface IRunContent
    {
        /// <summary>
        /// A stable fingerprint of every piece of content that can change a fight's outcome
        /// (ADR 0008's long-promised `contentVersion`). Stamped into a run at creation and checked
        /// on resume, because a run's encounters are derived from its seed AT FIGHT TIME — so the
        /// same save resumed on a retuned build fights a different army than it was saved against,
        /// and no id check can see it (the ids are identical; the numbers moved).
        ///
        /// It exists on this interface rather than being read off the catalog directly so the run
        /// layer keeps knowing nothing about content, and so a future server-side or shared-artifact
        /// feature has one obvious thing to compare.
        /// </summary>
        string ContentVersion { get; }

        ChassisDef Chassis(string id);
        WeaponDef Weapon(string id);
        TrinketDef Trinket(string id);
        SpecNode Node(string id);
        InscriptionDef Inscription(string id);

        /// <summary>Shop pools (ADR 0009): infinite weighted draws, act-anchored.</summary>
        IReadOnlyList<string> HeroPool(int act);
        IReadOnlyList<string> WeaponPool(int act);
        IReadOnlyList<string> TrinketPool(int act);
        IReadOnlyList<string> InscriptionPool(int act);

        /// <summary>The authored POOL a rank-up draws from, in authored order. The choice made at
        /// ForkRank(chassis) is the hero's path; other ranks offer in-path (or
        /// path-agnostic) nodes. One flat content table = trivially retunable offers.
        /// This is the pool, NOT the offer — the run layer decides how many of it the player
        /// sees (RunController.SpecPick).</summary>
        IReadOnlyList<string> SpecOptions(string chassisId, Rank rank, string? pathId);

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

        /// <summary>The disclosure for a node fight. MUST resolve the same encounter as
        /// <see cref="Encounter"/> given the same arguments, or the player reads one brief and
        /// fights another.</summary>
        EncounterBrief EncounterBrief(int act, int nodeIndex, FightTier tier, Rng rng);

        /// <summary>The disclosure for the act boss (revealed at act start — it is a build target,
        /// not a knowledge check).</summary>
        EncounterBrief BossBrief(int act, Rng rng);
    }
}
