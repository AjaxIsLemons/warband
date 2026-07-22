using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>
    /// Per-unit contribution stats — a pure fold over the battle log (ADR 0004 §6).
    /// The same accounting later feeds the balance harness and the post-fight screen.
    /// Storm damage lands in the -1 bucket (source-less, excluded from contribution).
    /// </summary>
    public sealed class UnitFightStats
    {
        public int UnitId;
        public int DamageDealt;                 // all causes
        public int AttackDamage;
        public int AbilityDamage;
        public int DotDamage;
        public int FieldDamage;
        public int TriggerDamage;
        public int DamageTaken;                 // total incl. shield-absorbed
        public int ShieldAbsorbed;              // portion eaten by shields
        public int HealingDone;
        public int HealingReceived;
        public int Casts;
        public int FirstCastTick = -1;
        public int CcTicksSuffered;             // stun/silence/disarm/root uptime
        public int Steps;
        public int ShotsBlocked;
        public int Kills;                       // enemy deaths this unit damaged (participation)
    }

    public static class FightStats
    {
        public static Dictionary<int, UnitFightStats> Compute(BattleResult result)
        {
            var stats = new Dictionary<int, UnitFightStats>();
            UnitFightStats S(int id)
            {
                if (!stats.TryGetValue(id, out var s))
                    stats[id] = s = new UnitFightStats { UnitId = id };
                return s;
            }

            var damagedBy = new Dictionary<int, HashSet<int>>();          // victim -> sources
            var ccActive = new Dictionary<(int Unit, StatusKind Kind), int>(); // -> count
            var ccSince = new Dictionary<int, int>();                     // unit -> tick CC started
            var ccTotal = new Dictionary<int, int>();

            static bool IsCc(StatusKind k) =>
                k == StatusKind.Stun || k == StatusKind.Silence || k == StatusKind.Disarm || k == StatusKind.Root;

            int CcCount(int unit)
            {
                int n = 0;
                foreach (StatusKind k in new[] { StatusKind.Stun, StatusKind.Silence, StatusKind.Disarm, StatusKind.Root })
                    if (ccActive.TryGetValue((unit, k), out int c))
                        n += c;
                return n;
            }

            foreach (var e in result.Events)
            {
                switch (e.Kind)
                {
                    case EventKind.DamageDealt:
                        S(e.Source).DamageDealt += e.Amount;
                        switch (e.Cause)
                        {
                            case Cause.Attack: S(e.Source).AttackDamage += e.Amount; break;
                            case Cause.Ability: S(e.Source).AbilityDamage += e.Amount; break;
                            case Cause.Dot: S(e.Source).DotDamage += e.Amount; break;
                            case Cause.Field: S(e.Source).FieldDamage += e.Amount; break;
                            case Cause.Trigger: S(e.Source).TriggerDamage += e.Amount; break;
                        }
                        S(e.Target).DamageTaken += e.Amount;
                        if (e.Aux > 0) S(e.Target).ShieldAbsorbed += e.Aux;
                        if (e.Source >= 0)
                        {
                            if (!damagedBy.TryGetValue(e.Target, out var set))
                                damagedBy[e.Target] = set = new HashSet<int>();
                            set.Add(e.Source);
                        }
                        break;
                    case EventKind.Heal:
                        S(e.Source).HealingDone += e.Amount;
                        S(e.Target).HealingReceived += e.Amount;
                        break;
                    case EventKind.Cast:
                        var c = S(e.Source);
                        c.Casts++;
                        if (c.FirstCastTick < 0) c.FirstCastTick = e.Tick;
                        break;
                    case EventKind.Move:
                        S(e.Source).Steps++;
                        break;
                    case EventKind.AttackBlocked:
                        S(e.Source).ShotsBlocked++;
                        break;
                    case EventKind.StatusApplied when IsCc((StatusKind)e.Aux):
                    {
                        int unit = e.Target;
                        if (CcCount(unit) == 0) ccSince[unit] = e.Tick;
                        var key = (unit, (StatusKind)e.Aux);
                        ccActive[key] = ccActive.TryGetValue(key, out int n) ? n + 1 : 1;
                        break;
                    }
                    case EventKind.StatusExpired when IsCc((StatusKind)e.Aux):
                    {
                        int unit = e.Target;
                        var key = (unit, (StatusKind)e.Aux);
                        if (ccActive.TryGetValue(key, out int n) && n > 0)
                        {
                            ccActive[key] = n - 1;
                            if (CcCount(unit) == 0 && ccSince.TryGetValue(unit, out int since))
                                ccTotal[unit] = (ccTotal.TryGetValue(unit, out int t) ? t : 0) + (e.Tick - since);
                        }
                        break;
                    }
                    case EventKind.Death:
                        if (damagedBy.TryGetValue(e.Target, out var sources))
                            foreach (var srcId in sources)
                                if (srcId >= 0)
                                    S(srcId).Kills++;
                        break;
                }
            }

            // Close out CC still active at battle end.
            foreach (var kv in ccSince)
                if (CcCount(kv.Key) > 0)
                    ccTotal[kv.Key] = (ccTotal.TryGetValue(kv.Key, out int t) ? t : 0) + (result.EndTick - kv.Value);
            foreach (var kv in ccTotal)
                S(kv.Key).CcTicksSuffered = kv.Value;

            return stats;
        }
    }
}
