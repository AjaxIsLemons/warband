namespace Warband.Sim
{
    /// <summary>
    /// The v1 status set (combat-grammar.md). One generic Dot — flavor/riders live on
    /// heroes, never as status typing. Stacking is additive-by-default: instances
    /// append, effective values are summed at read time, never cached (circuit model).
    /// </summary>
    public enum StatusKind
    {
        Haste,    // Mag = attack-speed bonus, fixed-point vs FP
        Slow,     // Mag = attack-speed malus
        Stun,     // both clocks stop
        Silence,  // no casting, no mana gain; autos continue
        Disarm,   // no autos; casting continues
        Root,     // no movement
        Regen,    // Mag healed per pulse (1s)
        Dot,      // Mag damage per pulse (1s)
    }

    public sealed class Status
    {
        public StatusKind Kind;
        public int Mag;
        public int TicksLeft;      // <0 = permanent for the fight
        public int SourceId = -1;  // applier — attribution + rider hooks
    }
}
