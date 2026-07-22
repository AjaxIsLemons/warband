namespace Warband.Sim
{
    public enum EventType { Move, Attack, Cast, Death, StormTick, End }

    /// <summary>
    /// One entry of the battle log. The log is the replay: the client renders battles
    /// purely from these plus re-simulation — no combat logic client-side.
    /// </summary>
    public readonly struct BattleEvent
    {
        public readonly int Tick;
        public readonly EventType Type;
        public readonly int Actor;
        public readonly int Target;   // -1 when n/a
        public readonly int Value;    // damage, or packed destination for Move

        public BattleEvent(int tick, EventType type, int actor, int target, int value)
        {
            Tick = tick;
            Type = type;
            Actor = actor;
            Target = target;
            Value = value;
        }
    }
}
