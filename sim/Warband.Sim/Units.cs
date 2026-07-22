namespace Warband.Sim
{
    /// <summary>Chassis numbers. Content layer fills these; the sim never hardcodes them.</summary>
    public sealed class UnitDef
    {
        public string Name = "unit";
        public int MaxHp;
        public int Attack;
        public int AttackInterval;   // ticks between swings
        public int Range;            // hexes
        public int MoveInterval;     // ticks per 1-hex step
        public int ManaMax;          // 0 = no signature
        public int CastDamage;       // placeholder signature until the effect grammar lands
    }

    public sealed class UnitState
    {
        public int Id;               // stable ordering key — ALL iteration is by ascending Id
        public int Team;             // 0 or 1
        public UnitDef Def = null!;
        public Hex Pos;
        public int Hp;
        public int Mana;
        public int TargetId = -1;
        public int NextAttackTick;
        public int NextMoveTick;

        public bool Alive => Hp > 0;

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
