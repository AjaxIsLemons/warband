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

    public enum EffectKind { Damage, Heal, ApplyStatus, GrantShield, GrantMana }

    public sealed class EffectDef
    {
        public EffectKind Kind;
        public Selector Select = new Selector { Kind = SelKind.CurrentTarget };
        public int Amount;
        public StatusKind Status;
        public int StatusTicks;    // duration for ApplyStatus; <0 = whole fight
    }

    public sealed class Trigger
    {
        public EventKind On;
        public List<Cond> When = new List<Cond>();   // ANDed; empty = always
        public List<EffectDef> Do = new List<EffectDef>();
    }
}
