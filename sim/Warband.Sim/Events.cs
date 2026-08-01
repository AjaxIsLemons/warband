using System.Collections.Generic;

namespace Warband.Sim
{
    public enum EventKind
    {
        BattleStart, Move, Attack, DamageDealt, Heal, Cast,
        StatusApplied, StatusExpired, ShieldChanged, ManaChanged,
        Death, StormTick, End,
        FieldCreated,   // Target=field id, Amount=IsWall(0/1), Source=creator, Aux=attached unit id (-1 static), Aux2=radius, Aux3=FieldFlavor
        FieldHex,       // Target=field id, Amount=Q, Aux=R (one per covered hex)
        FieldExpired,   // Target=field id
        AttackBlocked,  // Source=attacker, Target=intended victim, Amount=Q, Aux=R of the wall hex
        Leap,           // Source=leaper, Amount=Q, Aux=R of the LANDING hex; Aux2=Q, Aux3=R of the hex it
                        // left (Pikewall punish, Leap banners). Both endpoints ride the event because the
                        // renderer arcs the body between them, and by dispatch time the fold has already
                        // applied the landing — the take-off is not recoverable from view state.
        CheatDeath,     // Target=the unit that refused to die (Deathless — Berserker dive)
        MoveStart,      // Source=walker, Amount=Q, Aux=R of the DESTINATION, Aux2=step duration in ticks.
                        // Movement law: a step is committed here and lands as a Move at Tick+Aux2. The
                        // unit's position stays put until then. A Move with no preceding MoveStart is a
                        // teleport (Leap) — that distinction is the renderer's whole slide-vs-blink rule.
                        // Appended, never inserted: the ordinal is the wire encoding (Replay).
        TriggerFired,   // Source=owner, Aux=index into BattleResult.RuleIds, Target=the unit the
                        // triggering event was about (for the attribution spark-link), Root/Depth
                        // inherited. Emitted when a Trigger's conditions pass and BEFORE its effects
                        // resolve, so drain order reads cause-then-consequence (render-contract §5).
                        // WHY THIS EXISTS: a Trigger's effects were always on the wire, but nothing
                        // said WHICH passive produced them — the engine was invisible and only its
                        // exhaust was visible. See Design/passive-legibility.md.
        RuleChanged,    // Source=owner, Aux=index into BattleResult.RuleIds, Amount=1 came online /
                        // 0 went offline, Aux2=the rule's contribution at that moment.
                        // A StatRule is a read-time predicate with no activation moment to hook, so
                        // the sim SAMPLES every rule once per tick and emits transitions only. The
                        // client may not evaluate a condition itself (render-contract law #1), so
                        // this event is the only way a conditional passive can ever be seen.
        RuleProgress,   // Source=counting rep, Aux=index into BattleResult.RuleIds,
                        // Amount=progress 1..N (N on the firing match), Aux2=N, Target=what the
                        // counted event was about. A counter Inscription's pips (ADR 0026): every
                        // counted match rides the wire because the badge rail may not count for
                        // itself (render-contract law #1). Presentation-only like RuleChanged —
                        // fires no triggers, spends no cascade budget, provably outcome-neutral.
        RevisionApplied,// Target=directly revised unit, Amount=RevisionEffectKind,
                        // Aux=RevisionModifier mask. Emitted before the ordinary Move/Mana/Status
                        // consequences so the renderer can announce the cause without sim logic.
        UnitOmitted,    // Target=unit removed from the active hour, Amount=return tick.
        UnitReturned,   // Target=unit restored to the board at its recalled destination.
    }

    public enum Cause
    {
        None, Attack, Ability, Dot, Storm, Trigger, Field,
        Burn,     // the decay-pool tick (Pyro dive law) — distinct so Burn banners can hook it
        Counter,  // a directional riposte swing (Phalanx dive law) — "when an ally Counters: X"
    }

    /// <summary>
    /// One log entry, tag-change model (ADR 0004): mutating events carry the delta
    /// (Amount) AND the absolute post-state (Post*). A replay client SETS bars to the
    /// absolutes and never accumulates — dropped frame = stale, never drift.
    /// Root = the unit whose trigger chain originated this event (attribution).
    /// Death events: Source = the killer (last damager), Amount = overkill damage —
    /// so on-kill triggers and overkill-carry riders compose from the grammar.
    /// </summary>
    public sealed class BattleEvent
    {
        public int Tick;
        public EventKind Kind;
        public int Source = -1;
        public int Target = -1;
        public int Amount;
        public Cause Cause;
        public int Depth;
        public int Root = -1;
        public int Aux = -1;      // StatusKind for Status* events; absorbed-by-shield for DamageDealt
        public int Aux2 = -1;     // FieldCreated: radius (attached fields); else unused
        public int Aux3 = -1;     // FieldCreated: FieldFlavor (what the zone does — render color); else unused

        /// <summary>FieldCreated's flavor, honoring the -1 "unused" slot default as Neutral so an
        /// event built without one reads as an uncolored zone rather than a bogus enum value.</summary>
        public FieldFlavor Flavor => Aux3 < 0 ? FieldFlavor.Neutral : (FieldFlavor)Aux3;
        public bool Crit;         // DamageDealt from a critical auto-attack

        /// <summary>Which root event's cascade tree this event belongs to — the once-per-root
        /// guard's key (ADR 0026). Engine-internal and NEVER serialized: replay is re-simulation,
        /// and depth-0 events already define root identity on the wire (IsRootEvent).</summary>
        internal int RootSeq;
        public const int Unset = int.MinValue; // Post* sentinel — HP can legitimately go negative
        public int PostHp = Unset;
        public int PostShield = Unset;
        public int PostMana = Unset;
    }
}
