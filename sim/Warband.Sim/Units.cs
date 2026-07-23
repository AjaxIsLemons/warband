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
        public int ManaMax;          // 0 = no signature
        public int CritChance;       // percent; auto-attacks only — the sim's ONLY rng (ADR 0005)
        public int CritMultFp = 1500;
        public bool HealAutos;       // censer law (ADR 0012): swings heal the lowest-HP ally instead
        public int CleavePct;        // >0: swings also hit enemies adjacent to the target at this % (greataxe)
        public int ExtraArrowPct = 50; // MultiShot extras deal this % unless the window says otherwise
        public List<EffectDef> Signature = new List<EffectDef>();
        public List<Trigger> Triggers = new List<Trigger>();   // innate + fork riders
        public List<StatRule> StatRules = new List<StatRule>(); // conditional stats ("+speed below half HP")
    }

    public sealed class UnitState
    {
        public int Id;               // stable ordering key — ALL iteration is by ascending Id
        public int Team;             // 0 or 1
        public UnitDef Def = null!;
        public Hex Pos;
        public int Hp;
        public int Shield;
        public int Mana;
        public int TargetId = -1;
        public int NextAttackTick;
        public int NextMoveTick;
        public bool Dead;            // set only by the death phase
        public int SwingCount;       // lifetime swings — Nth-swing riders + charge decrement
        public int LastDamagedBy = -1; // killer attribution on the Death event
        public List<(int Tick, int Amount)> RecentDamage = new List<(int, int)>(); // Phase-entry window
        public List<Status> Statuses = new List<Status>();

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
