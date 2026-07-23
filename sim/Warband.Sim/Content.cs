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
        RemoveStatus, // strip ALL instances of Status from the selected target (Detonate consume)
        Recast,       // re-run the owner's signature anchored on the selected target
                      // (Dying Star kill-chains; cascade depth bounds it)
    }

    public sealed class EffectDef
    {
        public EffectKind Kind;
        public Selector Select = new Selector { Kind = SelKind.CurrentTarget };
        public int Amount;
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
    }

    public sealed class Trigger
    {
        public EventKind On;
        public List<Cond> When = new List<Cond>();   // ANDed; empty = always
        public List<EffectDef> Do = new List<EffectDef>();
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
    }
}
