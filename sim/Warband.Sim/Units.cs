using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>Chassis numbers + declarative kit. Content fills these; the sim never
    /// hardcodes them.</summary>
    public sealed class UnitDef
    {
        public string Name = "unit";
        public int MaxHp;
        public int Attack;
        public int AttackInterval;   // base ticks between swings (modulated by Haste/Slow)
        public int Range;            // hexes
        public int MoveInterval;     // ticks per 1-hex step
        public TargetPref TargetPref;// who this unit acquires (ADR 0013 default = Nearest)
        public int Standoff;         // >0: preferred fighting distance — gives ground to hold it
        public int ManaMax;          // 0 = no signature
        public int ManaPerSwing = Battle.ManaPerAttack;  // the weapon's cast-cadence axis
        /// <summary>Mana earned per hit TAKEN. Authored so a unit can opt out of the
        /// damage-fed channel: a ritual whose clock is pure trickle is a countdown the player can
        /// read off the board and answer with time, Silence or Stun — whereas the global rate makes
        /// any channeller fire the instant it is focused, which inverts the problem it poses.</summary>
        public int ManaPerHitTaken = Battle.ManaPerHitTaken;
        public int CritChance;       // percent; auto-attacks only — the sim's ONLY rng (ADR 0005)
        public int CritMultFp = 1500;
        public bool HealAutos;       // censer law (ADR 0012): swings heal the lowest-HP ally instead
        public int CleavePct;        // >0: swings also hit enemies adjacent to the target at this % (greataxe)
        public int ExtraArrowPct = 50; // MultiShot extras deal this % unless the window says otherwise
        public List<EffectDef> Signature = new List<EffectDef>();
        public List<Trigger> Triggers = new List<Trigger>();   // innate + fork riders
        public List<StatRule> StatRules = new List<StatRule>(); // conditional stats ("+speed below half HP")

        // Identity block — descriptive only. The sim NEVER branches on these; they exist so the
        // renderer can say what a unit IS (placement needs "reach 4, Honed Longbow" before the
        // fight starts). Loadout.Compose fills them; authored content may overwrite Name freely
        // without breaking chassis-keyed art, because ChassisId is the stable key.
        public string ChassisId = "";
        public string WeaponName = "";
        public WeaponTier WeaponTier;
        public List<string> Traits = new List<string>();  // spec nodes + trinkets, in merge order

        /// <summary>The authored enemy role (Enemies.Swarm/Anchor/…), empty for heroes. Unlike
        /// ChassisId — a render key pointing at a hero silhouette — this is what the body IS, so it
        /// is what the board renders a monster AS. The encounter declares it once on its
        /// EncounterUnit; the catalog stamps it here at resolution, which is why every consumer
        /// (preview brief, board, replay) reads one authored value instead of guessing from a name.</summary>
        public string RoleId = "";
    }

    public sealed class UnitState
    {
        public int Id;               // stable ordering key — ALL iteration is by ascending Id
        public int Team;             // 0 or 1
        public UnitDef Def = null!;

        /// <summary>Where this unit's rules start in the battle-wide rule-id table
        /// (<see cref="BattleResult.RuleIds"/>). Assigned once at Battle construction so a firing
        /// rule can name itself with an int on an all-int event. Triggers occupy
        /// [TriggerBase, TriggerBase + Def.Triggers.Count), StatRules follow at StatRuleBase.
        /// -1 outside a battle (deployment previews compose UnitStates with no table).</summary>
        public int TriggerBase = -1;
        public int StatRuleBase = -1;

        /// <summary>Last sampled on/off state per StatRule — the entire memory behind the
        /// transition sweep (Battle.SampleStatRules). Presentation-only: nothing reads it to decide
        /// anything, so it is deliberately outside StateHash.</summary>
        public bool[]? RuleActive;
        public Hex Pos;
        public int Hp;
        public int Shield;
        public int Mana;
        public int TargetId = -1;
        public int BlockedTargetId = -1; // block-then-adapt: enemy id whose shot a wall fizzled. Live-sim state only — never serialized, never hashed (transient decision state, like NextAttackTick).
        public int NextAttackTick;
        public bool Dead;            // set only by the death phase
        public int SwingCount;       // lifetime swings — Nth-swing riders + charge decrement
        public int LastDamagedBy = -1; // killer attribution on the Death event
        public List<(int Tick, int Amount)> RecentDamage = new List<(int, int)>(); // Phase-entry window
        public List<Status> Statuses = new List<Status>();

        // ---- committed step (movement law) ----
        // A unit that decides to move DEPARTS now and ARRIVES StepEnd ticks later. Pos does not
        // change for the whole walk — you are where you were until you get there — and StepTo is
        // reserved, so nothing paths into the hex being walked into. A committed step always
        // completes: Root/Stun gate STARTING one, never finishing one, which is what keeps the
        // renderer free of rubber-banding.
        private Hex _stepTo;
        public int StepStart;
        public int StepEnd;
        public bool Walking => StepEnd > StepStart;

        /// <summary>Where this unit is headed — its own hex whenever it isn't walking. The standing
        /// case is derived, not stored, so "StepTo == Pos when still" holds by construction and no
        /// spawn path can leave a stale destination behind.</summary>
        public Hex StepTo { get => Walking ? _stepTo : Pos; set => _stepTo = value; }

        public bool Alive => !Dead;

        public bool Has(StatusKind kind)
        {
            foreach (var s in Statuses)
                if (s.Kind == kind)
                    return true;
            return false;
        }

        public int Sum(StatusKind kind)
        {
            int total = 0;
            foreach (var s in Statuses)
                if (s.Kind == kind)
                    total += s.Mag;
            return total;
        }

        /// <summary>Read-time attack damage: base + AttackUp − AttackDown, floor 0.
        /// Never cached — ramp passives just stack statuses.</summary>
        public int EffAttack(int ruleBonus = 0)
        {
            int attack = Def.Attack + Sum(StatusKind.AttackUp) - Sum(StatusKind.AttackDown) + ruleBonus;
            return attack < 0 ? 0 : attack;
        }

        /// <summary>Read-time stat evaluation (never cached): interval scaled by
        /// fixed-point attack speed, floor 20% speed, min 1 tick.</summary>
        public int EffAttackInterval(int ruleSpeedBonus = 0)
        {
            int speed = Battle.FP + Sum(StatusKind.Haste) - Sum(StatusKind.Slow) + ruleSpeedBonus;
            if (Has(StatusKind.Frenzied)) speed += Battle.FrenzySpeedFp;
            if (speed < Battle.FP / 5) speed = Battle.FP / 5;
            int interval = Def.AttackInterval * Battle.FP / speed;
            return interval < 1 ? 1 : interval;
        }

        public static UnitState Spawn(int id, int team, UnitDef def, Hex pos) => new UnitState
        {
            Id = id,
            Team = team,
            Def = def,
            Pos = pos,
            Hp = def.MaxHp,
        };
    }
}
