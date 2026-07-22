using System.Collections.Generic;

namespace Warband.Sim
{
    public enum GrowthMetric
    {
        KillsParticipated,   // enemy deaths this hero dealt any damage toward
        DamageDealt,         // total damage dealt (Threshold = per-N)
    }

    /// <summary>
    /// Run-scoped passive (2026-07-22): the battle sim stays pure — after each fight the
    /// run layer folds the log through the hero's RunBonuses and bakes what was earned
    /// into permanent statuses the hero spawns with in later fights. Deterministic
    /// (the log is), replay-safe (growth is derived, never sim-side mutation).
    /// </summary>
    public sealed class RunBonus
    {
        public GrowthMetric Per;
        public int Threshold = 1;   // e.g. 1 kill, or per 50 damage
        public StatusKind Grant;
        public int Mag;             // granted per threshold reached, additive
    }

    public static class ProgressionFold
    {
        public static List<Status> Earned(List<BattleEvent> events, int heroId, IEnumerable<RunBonus> bonuses)
        {
            int damageDealt = 0;
            int killsParticipated = 0;
            var damagedBy = new Dictionary<int, HashSet<int>>(); // victim -> damage sources (lookup only)

            foreach (var e in events)
            {
                if (e.Kind == EventKind.DamageDealt)
                {
                    if (e.Source == heroId) damageDealt += e.Amount;
                    if (!damagedBy.TryGetValue(e.Target, out var sources))
                        damagedBy[e.Target] = sources = new HashSet<int>();
                    sources.Add(e.Source);
                }
                else if (e.Kind == EventKind.Death)
                {
                    // Damage only flows to enemies today; when friendly fire arrives
                    // (symmetric fields), gate this on the victim's team.
                    if (damagedBy.TryGetValue(e.Target, out var sources) && sources.Contains(heroId))
                        killsParticipated++;
                }
            }

            var earned = new List<Status>();
            foreach (var b in bonuses)
            {
                int tally = b.Per == GrowthMetric.KillsParticipated ? killsParticipated : damageDealt;
                int times = b.Threshold > 0 ? tally / b.Threshold : 0;
                if (times > 0)
                    earned.Add(new Status { Kind = b.Grant, Mag = b.Mag * times, TicksLeft = -1, SourceId = heroId });
            }
            return earned;
        }
    }
}
