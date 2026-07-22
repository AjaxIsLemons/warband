using System.Collections.Generic;

namespace Warband.Sim
{
    public enum EventKind
    {
        BattleStart, Move, Attack, DamageDealt, Heal, Cast,
        StatusApplied, StatusExpired, ShieldChanged, ManaChanged,
        Death, StormTick, End,
        FieldCreated,   // Target=field id, Amount=IsWall(0/1), Source=creator, Aux=attached unit id (-1 static), Aux2=radius
        FieldHex,       // Target=field id, Amount=Q, Aux=R (one per covered hex)
        FieldExpired,   // Target=field id
        AttackBlocked,  // Source=attacker, Target=intended victim, Amount=Q, Aux=R of the wall hex
    }

    public enum Cause { None, Attack, Ability, Dot, Storm, Trigger, Field }

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
        public int Aux2 = -1;     // FieldCreated: radius (attached fields); else unused
        public const int Unset = int.MinValue; // Post* sentinel — HP can legitimately go negative
        public int PostHp = Unset;
        public int PostShield = Unset;
        public int PostMana = Unset;
    }
}
