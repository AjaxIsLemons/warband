using System.Collections.Generic;

namespace Warband.Sim
{
    public enum EventKind
    {
        BattleStart, Move, Attack, DamageDealt, Heal, Cast,
        StatusApplied, StatusExpired, ShieldChanged, ManaChanged,
        Death, StormTick, End,
    }

    public enum Cause { None, Attack, Ability, Dot, Storm, Trigger }

    /// <summary>
    /// One log entry, tag-change model (ADR 0004): mutating events carry the delta
    /// (Amount) AND the absolute post-state (Post*). A replay client SETS bars to the
    /// absolutes and never accumulates — dropped frame = stale, never drift.
    /// Root = the unit whose trigger chain originated this event (attribution).
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
        public int PostHp = -1;
        public int PostShield = -1;
        public int PostMana = -1;
    }
}
