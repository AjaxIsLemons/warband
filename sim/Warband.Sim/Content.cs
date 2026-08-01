using System.Collections.Generic;

namespace Warband.Sim
{
    // The content atom (ADR 0004): Trigger{On, When, Do}. Passives, fork riders,
    // banners, item effects, glyph rules and reactions all compile to this shape.
    // Pure data — the engine interprets; content never gets bespoke code.
    // Dive-campaign law (Jake, 2026-07-23): every mechanic must be reachable from this
    // grammar so banners and Relic riders can hook it — no unit-hardcoded specials.

    public enum CondKind
    {
        SourceIsOwner,             // event source is the trigger's owner
        TargetIsOwner,
        SourceIsEnemyOfOwner,
        TargetIsAllyOfOwner,       // event target is on owner's team, not owner itself
        CauseIs,                   // event cause matches Cond.Cause
        OwnerBelowHpPct,           // Amount = percent
        TargetWithinHexesOfOwner,  // Amount = range
        SourceWithinHexesOfOwner,  // Amount = range (zone-punisher reactions)
        IsCrit,                    // event was a critical strike (on-crit passives)

        // --- dive-campaign conditions (2026-07-23) ---
        TargetBelowHpPct,          // event target below Amount% (Execute gates, Scent of Blood)
        TargetAtRangeOfOwner,      // exact distance == Amount (Spearpoint max-reach reward)
        NoEnemyWithinHexesOfOwner, // spacing conditions (Perfect Form) — StatRule-legal
        TargetAdjacentToAllyOfOwner, // pike mastery: enemies engaged with the line
        AnyEnemyTauntedByOwner,    // Give No Ground — StatRule-legal
        OwnerHasStatus,            // Cond.Status (charge gates: CounterCharge present?)
        TargetHasStatus,           // Cond.Status (Burn-gated riders, "enemies with X")
        SourceHasStatus,           // Cond.Status
        EveryNthSwingOfOwner,      // Amount = N; owner's swing counter divisible (Twin Nock, Chorus)
        StatusIs,                  // StatusApplied/Expired events: the status is Cond.Status (Phase re-entry)
        OwnerRecentDamageAbovePct, // damage taken in the last RecentWindow ticks ≥ Amount% max HP (Phase entry)
        IsRootEvent,               // event depth 0 — guards echo-riders (Twin Nock must not
                                   // fire off its own second arrow; counters not counter counters)
        AnyEnemyHasStatus,         // Cond.Status — state cond, StatRule-legal (Undying Zeal)
        TargetInFieldOfOwner,      // event target stands in a field the owner created
                                   // (Choking Smoke, Stoke the Coals; "in your fields" banners)
        EventRuleIsTeamRule,       // TriggerFired events only: the rule that fired is a TEAM rule
                                   // (an Inscription), not a unit passive. The hook Living
                                   // Inscription rides (ADR 0026) — and the guard that keeps it
                                   // from waking itself off its own announcement.
        TargetIsEnemyOfOwner,      // the target-side twin of SourceIsEnemyOfOwner. Negated it means
                                   // "an ally OR the owner" — which TargetIsAllyOfOwner cannot say,
                                   // and team rules about "any ally" need (the rep is somebody).
    }

    public sealed class Cond
    {
        public CondKind Kind;
        public bool Not;           // day-one negation (circuit's Opportunist lesson)
        public int Amount;
        public Cause Cause;
        public StatusKind Status;  // for *HasStatus / StatusIs
    }

    public enum SelKind
    {
        Self, EventSource, EventTarget, CurrentTarget,
        NearestEnemy, FarthestEnemy, LowestHpAlly, AlliesWithin, EnemiesWithin,
        EnemiesOnLineThroughTarget, // the pierce line: owner → through resolved anchor → onward
                                    // (Piercing Bolt, Lancer lunge; Range = max hexes, 0 = board)
        EnemiesOnLineThroughFarthest, // Sniper's law: the bolt aims at the FARTHEST enemy
    }

    public sealed class Selector
    {
        public SelKind Kind;
        public int Range;          // AlliesWithin / EnemiesWithin / line length
        public bool ExcludeSelf;
        public bool AnchorEvent;   // range kinds measure from the event SOURCE's hex, not owner's
                                   // (banner shapes: "when an enemy Leaps: stun around the lander")
        public bool AnchorEventTarget; // …or from the event TARGET's hex (the corpse, the victim);
                                       // wins over AnchorEvent (overkill-carry, Contagion, Splitheads)
        public bool ExcludeAnchorUnit; // drop the unit standing AT the anchor ("around the victim, not the victim")
        public bool SkipCtxTarget;     // line kinds: skip the through-target itself (Overreach's "behind")
        public int BelowHpPct;         // >0: only units under this HP% (Second Wind's triage filter)
        public StatusKind? MustHave; // filter: only units carrying this status ("nearest Burning enemy")
        public bool AdjacentToAlly;  // filter: only units with a living ally on an adjacent hex
                                     // ("mustered beside an ally" — Shoulder to Shoulder, ADR 0026)

        public Selector Clone() => (Selector)MemberwiseClone();
    }

    // Leap: the OWNER moves to a free hex adjacent to the selected target, drops its
    // sticky target, and fights like a normal unit from there (round 10: backline access
    // is a passive, not a targeting rule).
    public enum EffectKind
    {
        Damage, Heal, ApplyStatus, GrantShield, GrantMana, CreateField, Leap,

        // --- dive-campaign effects (2026-07-23) ---
        Swing,        // the owner performs a free auto-attack swing at the selected target.
                      // Amount = % of normal swing damage (0 = 100). AsCounter = directional
                      // law (ADR: strike the attacker if in reach, else the first enemy on
                      // the line toward them within reach; clear line = air).
        Execute,      // kill outright: damage equal to target's HP + Shield (Reaper)
        RemoveStatus, // Amount > 0 spends that many matching status instances; <= 0 strips all
        Recast,       // re-run the owner's signature anchored on the selected target
                      // (Dying Star kill-chains; cascade depth bounds it)
    }

    public sealed class EffectDef
    {
        public EffectKind Kind;
        public Selector Select = new Selector { Kind = SelKind.CurrentTarget };
        public int Amount;         // RemoveStatus: instances to spend; <= 0 removes every instance
        public StatusKind Status;
        public int StatusTicks;    // duration for ApplyStatus; <0 = whole fight
        public int StatusSwings;   // >0: ApplyStatus expires after owner's Nth swing instead
        public FieldDef? Field;    // CreateField: glyph spec, centered on the resolved target's hex
        public int PctOfEventAmount; // >0: Amount is replaced by % of the triggering event's Amount
                                     // (Lifesteal, thorns, overkill-carry — Death.Amount = overkill)
        public bool ScaleByTargetStatus; // Amount is multiplied by the target's Sum(ScaleStatus)
        public StatusKind ScaleStatus;   // (Detonate: +Z per Burn stack consumed)
        public bool ScaleByEventTargetStatus; // …by the EVENT target's Sum instead — the corpse's
                                              // pool (Contagion passes on what actually remained)
        public int EscalatePctPerIndex;  // multi-target effects: +% per resolved-target index —
                                         // enemies farther down the line take more (Overpenetration)
        public bool AsCounter;     // Swing only: apply the directional Counter law + Cause.Counter

        /// <summary>Deep copy down to the Selector and FieldDef. The composer patches signatures
        /// in place (SignaturePatch), and every UnitDef's signature starts life pointing at the
        /// STATIC content catalog — so a patch that mutated in place would rewrite the kit for
        /// every later composition in the process. Clone first, always.</summary>
        public EffectDef Clone()
        {
            var c = (EffectDef)MemberwiseClone();
            c.Select = Select.Clone();
            c.Field = Field?.Clone();
            return c;
        }
    }

    public sealed class Trigger
    {
        public EventKind On;
        public List<Cond> When = new List<Cond>();   // ANDed; empty = always
        public List<EffectDef> Do = new List<EffectDef>();

        /// <summary>ADR 0026 cascade law: at most one activation per root event (one depth-0
        /// event's whole cascade tree). Effects are always child events, so this also bans a rule
        /// waking itself. Stamped by the CONTENT layer on Inscription team triggers — the engine
        /// never assumes it, so kit passives and encounter rules keep their exact behavior.</summary>
        public bool OncePerRoot;

        /// <summary>&gt;1: the trigger fires on every Nth matching event instead of every one
        /// ("every third allied cast" — the Counters family). Each counted match goes on the wire
        /// as RuleProgress so the badge rail can show pips without counting for itself.</summary>
        public int EveryN;

        /// <summary>Render identity — WHICH passive this is. Stamped automatically by
        /// <see cref="Loadout.Compose"/> from the content that contributed the rule (chassis id,
        /// spec-node name, weapon name, trinket name), so no rule is ever hand-authored with one and
        /// new content is identified the day it is written. Empty on the static catalog instances;
        /// only composed clones carry it. Deliberately NOT part of <see cref="ContentHash"/>: the
        /// fingerprint exists to catch a retune, and an id changes no simulation outcome, so hashing
        /// it would invalidate every save for a presentation-only edit.</summary>
        public string RuleId = "";

        /// <summary>Shallow by design: When/Do are read-only to the engine, and the composer only
        /// ever writes <see cref="RuleId"/>. What must not be shared is the Trigger OBJECT — the
        /// catalog hands out the same instance to every composition, so stamping in place would
        /// rewrite the kit for every later one (the bug EffectDef.Clone already exists to prevent).</summary>
        public Trigger CloneForComposition() => (Trigger)MemberwiseClone();
    }

    public enum StatKind { AttackFlat, AttackSpeed }

    /// <summary>Multiplies a StatRule's Amount by live state — the gradient innates:
    /// Full Draw (per hex to target), Burning Hours (per 10% missing HP),
    /// Grudgekeeper (per 10 Shield held).</summary>
    public enum StatScale { None, DistanceToTarget, MissingHpPct10, ShieldPer10 }

    /// <summary>Read-time conditional stat: "while <conds>: ±Amount". Evaluated fresh at
    /// every stat read, never cached (circuit's missing primitive, ADR 0004 wall #2).
    /// Conds must be owner-state kinds (e.g. OwnerBelowHpPct) — there is no event.</summary>
    public sealed class StatRule
    {
        public StatKind Stat;
        public List<Cond> When = new List<Cond>();
        public int Amount;
        public StatScale ScaleBy;  // Amount × distance-to-target / missing-HP decades

        /// <summary>Render identity, same contract as <see cref="Trigger.RuleId"/>. This is the one
        /// that matters most: a StatRule has no activation moment to hook (it is re-evaluated at
        /// every stat read and never cached), so without an id AND the per-tick transition sweep in
        /// Battle, a conditional passive is structurally invisible — see Design/passive-legibility.md.</summary>
        public string RuleId = "";

        public StatRule CloneForComposition() => (StatRule)MemberwiseClone();
    }
}
