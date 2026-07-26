using System.Collections.Generic;
using Warband.Sim;
using static Warband.Content.D;

namespace Warband.Content
{
    /// <summary>
    /// The enemy-role grammar (roadmap item 2; Jake, 2026-07-25: *"enemies should all have their own
    /// designs... we will want to make interesting pve encounters that dont follow all the same
    /// rules"*).
    ///
    /// These are **authored UnitDefs, not composed hero kits**. A monster has no chassis, no rank,
    /// no weapon and no spec tree — it is a stat block plus a role behavior plus at most one
    /// disclosed rule. That is what lets a Swarm body be 40 HP with nothing else going on, and what
    /// stops "difficulty" from meaning "a Cleric with 300% health".
    ///
    /// Five roles, each stressing a DIFFERENT axis of the roster's coverage law (roster.md) so an
    /// act can pose genuinely different problems rather than the same fight at bigger numbers:
    ///
    /// | Role | The problem it poses | What it stresses |
    /// |---|---|---|
    /// | Swarm | more bodies than you have answers | cleave/AoE; punishes single-target |
    /// | Anchor | a wall you cannot burst | go through or go around |
    /// | Artillery | it shoots PAST your front line | reach, dive, protecting the back |
    /// | Ritualist | a clock that beats you if ignored | burst, Silence, Stun |
    /// | Diver | it is already in your backline | placement, anti-dive |
    ///
    /// Every role obeys the shared combat verbs (pve-encounters.md: no blanket boss immunity). The
    /// one authored exception — the Anchor's Ward — names its verb, is disclosed in the encounter
    /// rule text, and leaves several strong answers open.
    ///
    /// **ChassisId is a RENDER KEY, not a claim of identity** (see UnitDef's identity block: the sim
    /// never branches on it). Each role borrows the silhouette that reads closest to its shape so
    /// five roles are five distinct bodies on the board today; bespoke enemy art is a later pass.
    /// </summary>
    public static class Enemies
    {
        public const string Swarm = "swarm";
        public const string Anchor = "anchor";
        public const string Artillery = "artillery";
        public const string Ritualist = "ritualist";
        public const string Diver = "diver";

        /// <summary>Hourling — the era's leavings. Fast, brittle, and never alone.</summary>
        public static UnitDef Hourling() => new UnitDef
        {
            Name = "Hourling", ChassisId = "shade", WeaponName = "Twin Daggers",
            MaxHp = 70, Attack = 12, AttackInterval = 8, Range = 1,
            MoveInterval = 3,                       // it arrives before you are ready
            ManaMax = 0,                            // no signature: the pressure IS the count
        };

        /// <summary>
        /// Ashen Colossus — what is left when an era's largest thing stops moving. Slow enough to
        /// walk around, too heavy to burst, and (when escorted) warded until its escorts fall.
        /// </summary>
        public static UnitDef Colossus(bool warded = true)
        {
            var def = new UnitDef
            {
                Name = "Ashen Colossus", ChassisId = "bulwark", WeaponName = "Greataxe",
                MaxHp = 340, Attack = 34, AttackInterval = 16, Range = 1,
                MoveInterval = 9,                   // the slowest body in the game
                ManaMax = 0,
            };
            if (warded)
            {
                // WARD: 60% damage reduction that its escorts are holding up. Disclosed in the
                // encounter's rule text; the answer is "kill the escorts first", which fights the
                // player's instinct to focus the biggest threat. Not an immunity — every shared
                // verb still lands on it, and burst still beats it once the Ward drops.
                def.Triggers.Add(AtStart(Status(StatusKind.DamageTakenDown, 50, Self)));
                def.Triggers.Add(On(EventKind.Death, W(TgtAlly()),
                    Strip(StatusKind.DamageTakenDown, Self)));
                def.Traits.Add("Ward");
            }
            return def;
        }

        /// <summary>
        /// Sanddrift Gunner — the reason a front line is not a wall. It acquires the FARTHEST
        /// target, so it shoots over your melee at whatever you were protecting, and it gives
        /// ground to keep doing it. Fragile: reaching it is the whole answer.
        /// </summary>
        public static UnitDef Gunner() => new UnitDef
        {
            Name = "Sanddrift Gunner", ChassisId = "sharpshot", WeaponName = "Matchlock Musket",
            MaxHp = 85, Attack = 28, AttackInterval = 15, Range = 5,
            MoveInterval = 6, ManaMax = 0,
            TargetPref = TargetPref.Farthest,       // past the front line, at your backline
            Standoff = 5,                           // and it will not let you close for free
        };

        /// <summary>
        /// Hour-Scribe — the encounter's clock with a body. It never attacks; it channels. Mana
        /// comes from the trickle ALONE (ManaPerHitTaken = 0), so the countdown is pure time: the
        /// player can read it off the mana bar and answer it four different ways — out-damage it,
        /// Silence it (no mana gain), Stun it (both clocks stop), or simply race the ritual.
        ///
        /// This is the role that needed the sim to bend: on the global hit-fed rate a channeller
        /// fires the instant it is focused, which inverts the problem — punishing the answer.
        /// </summary>
        public static UnitDef Scribe() => new UnitDef
        {
            Name = "Hour-Scribe", ChassisId = "pyromancer", WeaponName = "Ashwood Staff",
            MaxHp = 120, Attack = 0, AttackInterval = 20, Range = 1,
            MoveInterval = 7,
            ManaMax = 10,                           // 1 trickle/second → the ritual lands at ~10s
            ManaPerHitTaken = 0,                    // time advances the ritual. Nothing else does.
            // THE RITUAL: everything you brought takes the hit, wherever it stands.
            Signature = { Dmg(Enemies(99), 38), Status(StatusKind.Slow, 200, Enemies(99), ticks: 30) },
            // It stands where it was placed and channels — a fixed site, not a chaser.
            Triggers = { AtStart(Status(StatusKind.Root, 0, Self)) },
        };

        /// <summary>
        /// Gloamstalker — it opens the fight already behind you. Ambush lifted straight from the
        /// Shade (Jake: lifting kits for simplicity is fine for now), aimed at the farthest body so
        /// it keeps hunting backward instead of collapsing into the scrum.
        /// </summary>
        public static UnitDef Stalker() => new UnitDef
        {
            Name = "Gloamstalker", ChassisId = "berserker", WeaponName = "Twin Daggers",
            MaxHp = 120, Attack = 30, AttackInterval = 9, Range = 1,
            MoveInterval = 3, ManaMax = 0,
            TargetPref = TargetPref.Farthest,
            Triggers = { AtStart(Leap(Farthest)) },  // it is in your backline on tick 0
        };

        /// <summary>Every role, for tests and tooling that need to sweep the grammar.</summary>
        public static readonly Dictionary<string, System.Func<UnitDef>> Roles =
            new Dictionary<string, System.Func<UnitDef>>
            {
                [Swarm] = Hourling,
                [Anchor] = () => Colossus(),
                [Artillery] = Gunner,
                [Ritualist] = Scribe,
                [Diver] = Stalker,
            };

        // ---- boss bodies (2026-07-26, ADR 0024) -------------------------------------
        // Bosses are ENCOUNTERS, not privileged chassis (pve-encounters.md). These two are
        // singular creatures that each serve their encounter's core mechanic; the act-1 boss is
        // the Last Oath, which has no boss body at all — the RELATIONSHIP is the boss.
        //
        // Both obey the shared combat verbs: no immunities, no hidden rules. Both are Rooted and
        // never attack, so the whole threat is a clock the player can read off a mana bar and
        // answer four ways (out-damage it · reach it · Silence it · Stun it). That is deliberate
        // repetition of the Hour-Scribe's grammar: acts teach a verb, bosses examine it.

        public const string Siege = "siege";
        public const string Hour = "hour";

        /// <summary>
        /// Ashfall Bombard — the act-2 exam's gun. It cannot be rushed (Rooted, behind a wall) and
        /// it cannot be starved (`ManaPerHitTaken = 0`: the shell is on a clock, not on your
        /// damage). The shell lands on the FARTHEST body and leaves burning ground around the
        /// crater, so a warband that stands together takes it together — which inverts the swarm
        /// lesson act 1 taught. Reach, spread, control, or sustain; pick one.
        /// </summary>
        public static UnitDef Bombard() => new UnitDef
        {
            Name = "Ashfall Bombard", ChassisId = "pyromancer", WeaponName = "Ashwood Staff",
            MaxHp = 300, Attack = 0, AttackInterval = 20, Range = 1,
            MoveInterval = 9,
            // 9s to the first shell, then every 9s. MEASURED, not guessed: at 14s the probe found
            // the gun fired roughly once before a rank-B party finished the wall, so three of four
            // answer axes cleared it 100% from every formation — the encounter posed nothing.
            ManaMax = 9,
            ManaPerHitTaken = 0,                    // striking it does not hasten it
            Signature =
            {
                Dmg(Farthest, 58),
                Glyph(Farthest, Ashfall(radius: 1, ticks: 80)),
            },
            Triggers = { AtStart(Status(StatusKind.Root, 0, Self)) },
        };

        /// <summary>The crater the Bombard leaves. Factional by default (pve-encounters.md):
        /// enemies of its creator burn, its own court walks through it untouched.</summary>
        private static FieldDef Ashfall(int radius, int ticks) => new FieldDef
        {
            Radius = radius, Ticks = ticks, PulseAffects = Affects.Enemies,
            Pulse = { Dmg(Self /* ignored: hits each occupant */, 9) },
        };

        /// <summary>
        /// The Waning Crown — the Hour made manifest (theme.md), and the run's final exam. Its bell
        /// advances on time, AND on every death in its court **including the ones the player
        /// causes**. That is the whole encounter: clearing the escorts is both the obvious answer
        /// and the thing that rings the bell sooner, so "kill everything in front of you" — the
        /// habit three acts of node fights have trained — is the losing line.
        ///
        /// Multiple strong answers survive, which is the boss law's real bar: burst the Crown past
        /// its court · reach it · Silence it (GainMana is gated on Silence, so the bell stops
        /// COMPLETELY) · Stun it · or simply out-sustain the bells. Nothing here is immune to
        /// anything.
        /// </summary>
        public static UnitDef Crown() => new UnitDef
        {
            Name = "The Waning Crown", ChassisId = "banneret", WeaponName = "Company Standard",
            MaxHp = 460, Attack = 0, AttackInterval = 20, Range = 1,
            MoveInterval = 9,
            ManaMax = 22,                           // ~22s to the first bell on time alone
            ManaPerHitTaken = 0,
            Signature =
            {
                Dmg(Enemies(99), 44),
                Status(StatusKind.Slow, 250, Enemies(99), ticks: 50),
            },
            Triggers =
            {
                AtStart(Status(StatusKind.Root, 0, Self)),
                // Every death in its court rings the bell closer. Same shape as the Bond's
                // On(Death, ally) — proven grammar, new meaning.
                On(EventKind.Death, W(TgtAlly()), Mana(Self, 4)),
            },
        };

        /// <summary>
        /// The one-line behavior note every previewed body owes the player. `pve-encounters.md`
        /// requires attacks, signatures, passives, triggers **and targeting rules** to be
        /// inspectable before deployment — and a Sanddrift Gunner whose entire design is "acquires
        /// FARTHEST, holds standoff 5" was previously previewed as a name and a health number.
        ///
        /// Keyed on the authored `UnitDef.Name`, which is the enemy's real identity. It is
        /// deliberately NOT keyed on ChassisId: that is a render key pointing at a hero, and a
        /// preview that reads a hero's ability text off a monster is worse than no preview.
        /// </summary>
        public static string Behavior(string unitName) =>
            BehaviorNotes.TryGetValue(unitName, out string note) ? note : "";

        private static readonly Dictionary<string, string> BehaviorNotes =
            new Dictionary<string, string>
            {
                ["Hourling"] =
                    "Fast and fragile, with no signature. The pressure is the count, not the body.",
                ["Ashen Colossus"] =
                    "The slowest body on the board and the heaviest swing. Walk around it or go through it.",
                ["Sanddrift Gunner"] =
                    "Fires at your FARTHEST unit from reach 5, and gives ground to hold that range.",
                ["Hour-Scribe"] =
                    "Rooted, and never attacks. Its mana advances on time alone; at full it damages "
                    + "and Slows your entire warband, then begins again.",
                ["Gloamstalker"] =
                    "Leaps adjacent to your FARTHEST unit the moment the fight begins, then keeps "
                    + "hunting backward.",
                ["Ashfall Bombard"] =
                    "Rooted, and never attacks. Its clock runs on time alone; at full it shells your "
                    + "FARTHEST unit and leaves burning ground around the crater.",
                ["The Waning Crown"] =
                    "Rooted, and never attacks. Its bell advances on time AND on every death in its "
                    + "court; at full it damages and Slows your entire warband.",
                ["Oathbound Bulwark"] =
                    "Frontline body. Enrages permanently (+100% Attack Speed) if its Oathbound partner dies.",
                ["Oathbound Sharpshot"] =
                    "Backline damage at reach. Enrages permanently (+100% Attack Speed) if its Oathbound partner dies.",
            };

        /// <summary>Display label for a role id — the eyebrow a preview card wears.</summary>
        public static string RoleLabel(string roleId) =>
            RoleLabels.TryGetValue(roleId, out string label) ? label : roleId;

        private static readonly Dictionary<string, string> RoleLabels =
            new Dictionary<string, string>
            {
                [Swarm] = "SWARM", [Anchor] = "ANCHOR", [Artillery] = "ARTILLERY",
                [Ritualist] = "RITUAL CLOCK", [Diver] = "DIVER",
                [Siege] = "SIEGE CLOCK", [Hour] = "THE HOUR",
            };

        /// <summary>
        /// Role → the shell's existing accent vocabulary. Reuses the eight authored accents rather
        /// than inventing an enemy palette, so no stylesheet has to learn a new word.
        /// </summary>
        public static string RoleAccent(string roleId) =>
            RoleAccents.TryGetValue(roleId, out string accent) ? accent : "utility";

        private static readonly Dictionary<string, string> RoleAccents =
            new Dictionary<string, string>
            {
                [Swarm] = "tempo", [Anchor] = "ward", [Artillery] = "precision",
                [Ritualist] = "affliction", [Diver] = "reaction",
                [Siege] = "affliction", [Hour] = "power",
            };
    }
}
