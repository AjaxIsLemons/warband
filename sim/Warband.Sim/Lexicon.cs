using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>
    /// Semantic domain of a rule — the grouping a UI hangs an icon on.
    ///
    /// It is a DOMAIN, not a valence: Power holds both Empowered and Weakened, Ward holds both
    /// Fortified and Exposed. Encoding good/bad here would rebuild the status good/bad table that
    /// FieldFlavor deliberately avoided, and it would be wrong as often as right (Exposed on an
    /// enemy is good news).
    ///
    /// It is also NOT a color. Battlefield color already has one owner — the signature-matched
    /// tells in tuning.json — and a second source would drift from it. LexKind drives text-side
    /// icon prefixes only.
    /// </summary>
    public enum LexKind
    {
        Tempo,       // rate of action: haste, slow, frenzy, extra arrows
        Control,     // denial of action: stun, silence, disarm, root, taunt
        Power,       // how hard a swing lands
        Precision,   // critical chance and multiplier
        Affliction,  // damage over time: burn, decay
        Ward,        // damage taken, shields, mitigation
        Mending,     // healing
        Evasion,     // absence and survival: phase, cheat-death
        Reaction,    // counter/riposte machinery
        Mark,        // inert tags that exist purely as trigger fuel
        Utility,     // mana, movement, placement, everything structural
    }

    /// <summary>One hydrated entry: what to call a thing, what it does, and how to badge it.</summary>
    public readonly struct LexEntry
    {
        public readonly string Name;
        public readonly string Text;
        public readonly LexKind Kind;

        public LexEntry(string name, string text, LexKind kind)
        {
            Name = name; Text = text; Kind = kind;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Name);
    }

    /// <summary>
    /// Display copy for the SIM's own closed vocabulary (StatusKind, Cause). Content ids —
    /// spec nodes, chassis, weapons — live in Warband.Content.ContentLexicon, because Sim
    /// must not know content exists (ADR 0005) and the assembly reference only runs one way.
    ///
    /// Follows the EventText/TellMatch law: the lookup is headless and tested here, and Unity
    /// only renders the result. A miss returns a safe fallback rather than throwing — a new
    /// enum value must never crash a live fight — and a test asserts no fallback is ever
    /// actually needed, so the gap gets caught in CI instead of on screen.
    /// </summary>
    public static class Lexicon
    {
        public static LexEntry Of(StatusKind kind) =>
            Statuses.TryGetValue(kind, out var e) ? e : Fallback(kind.ToString());

        public static LexEntry Of(Cause cause) =>
            Causes.TryGetValue(cause, out var e) ? e : Fallback(cause.ToString());

        /// <summary>The un-authored path: readable, obviously placeholder, never an exception.</summary>
        public static LexEntry Fallback(string raw) => new LexEntry(raw, "", LexKind.Utility);

        public static readonly IReadOnlyDictionary<StatusKind, LexEntry> Statuses =
            new Dictionary<StatusKind, LexEntry>
            {
                [StatusKind.Haste] = new LexEntry("Haste", "Swings faster.", LexKind.Tempo),
                [StatusKind.Slow] = new LexEntry("Slow", "Swings slower.", LexKind.Tempo),
                [StatusKind.Frenzied] = new LexEntry("Frenzy", "Swings every single tick while it holds.", LexKind.Tempo),
                [StatusKind.MultiShotRamp] = new LexEntry("Volley", "Looses extra arrows with every swing.", LexKind.Tempo),
                [StatusKind.MultiShotWindow] = new LexEntry("Volley Window", "This swing's extra arrows land for a share of full damage.", LexKind.Tempo),

                [StatusKind.Stun] = new LexEntry("Stun", "Cannot act — both the swing and move clocks stop.", LexKind.Control),
                [StatusKind.Silence] = new LexEntry("Silence", "Cannot cast and gains no mana. Swings continue.", LexKind.Control),
                [StatusKind.Disarm] = new LexEntry("Disarm", "Cannot swing. Casting continues.", LexKind.Control),
                [StatusKind.Root] = new LexEntry("Root", "Cannot move.", LexKind.Control),
                [StatusKind.Taunt] = new LexEntry("Taunt", "Forced to attack the taunter, and silenced while it holds.", LexKind.Control),

                [StatusKind.AttackUp] = new LexEntry("Empowered", "Swings deal more damage.", LexKind.Power),
                [StatusKind.AttackDown] = new LexEntry("Weakened", "Swings deal less damage.", LexKind.Power),
                [StatusKind.SwingAmpPct] = new LexEntry("Charged Swing", "The next swing lands for bonus damage.", LexKind.Power),

                [StatusKind.CritUp] = new LexEntry("Keen", "Higher chance to land a critical hit.", LexKind.Precision),
                [StatusKind.CritMultUp] = new LexEntry("Lethal", "Critical hits strike harder.", LexKind.Precision),
                [StatusKind.NextSwingCrit] = new LexEntry("Sure Strike", "The next swing is a guaranteed critical hit.", LexKind.Precision),

                [StatusKind.Burn] = new LexEntry("Burn", "Burns each second, then burns for one less — a pool that spends itself. Every source merges into one.", LexKind.Affliction),
                [StatusKind.BurnAmp] = new LexEntry("Kindled", "Every Burn tick lands harder on this unit.", LexKind.Affliction),
                [StatusKind.Dot] = new LexEntry("Decay", "Takes damage each second.", LexKind.Affliction),

                [StatusKind.DamageTakenDown] = new LexEntry("Fortified", "Takes less damage from every source.", LexKind.Ward),
                [StatusKind.DamageTakenUp] = new LexEntry("Exposed", "Takes more damage from every source.", LexKind.Ward),
                [StatusKind.OverhealToShield] = new LexEntry("Overflow Ward", "Healing past full becomes Shield instead of being wasted.", LexKind.Ward),

                [StatusKind.Regen] = new LexEntry("Regeneration", "Heals each second.", LexKind.Mending),

                [StatusKind.Phase] = new LexEntry("Phase", "Untargetable and immune. Attackers must find someone else.", LexKind.Evasion),
                [StatusKind.CheatDeath] = new LexEntry("Deathless", "The next lethal blow leaves it standing at 1 HP.", LexKind.Evasion),

                [StatusKind.CounterCharge] = new LexEntry("Riposte", "Ammunition for a counter-swing — spent when one fires.", LexKind.Reaction),

                [StatusKind.Mark] = new LexEntry("Marked", "A tag with no effect of its own. Other rules read it.", LexKind.Mark),
                [StatusKind.Mustered] = new LexEntry("Mustered", "Sworn to a captain's Company at placement — its rallies reach this unit anywhere on the field.", LexKind.Mark),

                [StatusKind.HealToShield] = new LexEntry("Bloodless", "Healing restores no HP — it becomes Shield instead.", LexKind.Ward),
            };

        public static readonly IReadOnlyDictionary<Cause, LexEntry> Causes =
            new Dictionary<Cause, LexEntry>
            {
                [Cause.None] = new LexEntry("Unattributed", "No specific source.", LexKind.Utility),
                [Cause.Attack] = new LexEntry("Attack", "A weapon swing.", LexKind.Power),
                [Cause.Ability] = new LexEntry("Ability", "A cast signature.", LexKind.Utility),
                [Cause.Dot] = new LexEntry("Decay", "A damage-over-time tick.", LexKind.Affliction),
                [Cause.Storm] = new LexEntry("Storm", "Overtime pressure — the clock closing the fight.", LexKind.Utility),
                [Cause.Trigger] = new LexEntry("Trigger", "A rule firing off another event.", LexKind.Utility),
                [Cause.Field] = new LexEntry("Field", "Ground that does something to whoever stands on it.", LexKind.Utility),
                [Cause.Burn] = new LexEntry("Burn", "A Burn pool ticking down.", LexKind.Affliction),
                [Cause.Counter] = new LexEntry("Counter", "A riposte — a swing thrown back at an attacker.", LexKind.Reaction),
            };
    }
}
