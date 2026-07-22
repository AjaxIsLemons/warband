using System.Collections.Generic;

namespace Warband.Sim
{
    // The content atom (ADR 0004): Trigger{On, When, Do}. Passives, fork riders,
    // banners, item effects, glyph rules and reactions all compile to this shape.
    // Pure data — the engine interprets; content never gets bespoke code.

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
    }

    public sealed class Cond
    {
        public CondKind Kind;
        public bool Not;           // day-one negation (circuit's Opportunist lesson)
        public int Amount;
        public Cause Cause;
    }

    public enum SelKind
    {
        Self, EventSource, EventTarget, CurrentTarget,
        NearestEnemy, LowestHpAlly, AlliesWithin, EnemiesWithin,
    }

    public sealed class Selector
    {
        public SelKind Kind;
        public int Range;          // AlliesWithin / EnemiesWithin
        public bool ExcludeSelf;
    }

    public enum EffectKind { Damage, Heal, ApplyStatus, GrantShield, GrantMana, CreateField }

    public sealed class EffectDef
    {
        public EffectKind Kind;
        public Selector Select = new Selector { Kind = SelKind.CurrentTarget };
        public int Amount;
        public StatusKind Status;
        public int StatusTicks;    // duration for ApplyStatus; <0 = whole fight
        public FieldDef? Field;    // CreateField: glyph spec, centered on the resolved target's hex
    }

    public sealed class Trigger
    {
        public EventKind On;
        public List<Cond> When = new List<Cond>();   // ANDed; empty = always
        public List<EffectDef> Do = new List<EffectDef>();
    }

    public enum StatKind { AttackFlat, AttackSpeed }

    /// <summary>Read-time conditional stat: "while <conds>: ±Amount". Evaluated fresh at
    /// every stat read, never cached (circuit's missing primitive, ADR 0004 wall #2).
    /// Conds must be owner-state kinds (e.g. OwnerBelowHpPct) — there is no event.</summary>
    public sealed class StatRule
    {
        public StatKind Stat;
        public List<Cond> When = new List<Cond>();
        public int Amount;
    }
}
