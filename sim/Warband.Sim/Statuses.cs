namespace Warband.Sim
{
    /// <summary>
    /// The v1 status set (combat-grammar.md + the dive campaign). One generic Dot — flavor/
    /// riders live on heroes, never as status typing. Stacking is additive-by-default:
    /// instances append, effective values are summed at read time, never cached (circuit
    /// model). Exception: Burn is a single merged pool per unit (Pyro dive law).
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
        AttackUp,   // Mag added to attack damage at read time (ramp passives)
        AttackDown, // Mag subtracted from attack damage at read time

        // --- dive-campaign vocabulary (2026-07-23) ---
        Burn,       // decay pool: each pulse deals Mag damage, then Mag-1 (Bazaar law, Pyro dive).
                    // SINGLE instance per unit — all sources merge into one pool.
        BurnAmp,    // Mag = +% to Burn tick damage taken by this unit (White Heat crown)
        Taunt,      // target forced to SourceId + silenced while held (Bulwark dive, ADR 0013)
        Phase,      // untargetable + immune; attackers re-acquire (Shade dive)
        CheatDeath, // death phase: consumed instead of dying — survive at 1 HP (Berserker dive)
        OverhealToShield, // while held, overflow healing converts to Shield (censer mastery, Crimson Tide)
        MultiShotRamp,    // Mag = extra arrows per swing while a window is open (Volleyer ramp; permanent)
        MultiShotWindow,  // swing-scoped window; Mag = % damage per extra arrow (Volley cast)
        Frenzied,         // swing-scoped: +300% attack speed for the window's swings (Berserker
                          // Frenzy). Was "a swing every tick", which ignored AttackInterval and so
                          // paid out purely in weapon weight — see Battle.FrenzySpeedFp.
        NextSwingCrit,    // swing-scoped: the next swing is an automatic crit (Cold Return, sabre mastery)
        SwingAmpPct,      // swing-scoped: +Mag% swing damage (musket mastery opening shot)
        CounterCharge,    // ammo for Counter-law content: consumed by the riposte trigger (Phalanx)
        DamageTakenDown,  // Mag = % less damage taken (Steady the Line, Give No Ground) — armor is a STATUS, not a stat
        DamageTakenUp,    // Mag = % more damage taken (Reckless Swing's risk dial)
        CritUp,           // Mag = flat crit-chance % added at the roll (Reaper, dagger mastery)
        CritMultUp,       // Mag = fixed-point added to the crit multiplier (Widowmaker)
        Mark,             // inert tag — pure trigger fuel (Twist the Knife's crit-memory;
                          // "marked enemies" banner space). The engine never reads it.
    }

    public sealed class Status
    {
        public StatusKind Kind;
        public int Mag;
        public int TicksLeft;      // <0 = permanent for the fight
        public int SwingsLeft;     // >0 = expires after the owner's Nth swing (charge statuses,
                                   // next-N-swings shape — 4 votes: Volley/RollingFire/Frenzy/Drumbeat)
        public int SourceId = -1;  // applier — attribution + rider hooks
    }
}
