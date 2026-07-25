using System;
using System.Collections.Generic;
using System.Text;

namespace Warband.Sim
{
    /// <summary>How one unit fared across the sampled seeds.</summary>
    public sealed class UnitOdds
    {
        public int UnitId;
        public int Team;
        public string Name = "";
        public int Survived;          // trials this unit was still standing at the End event
        public double SurvivalPct;    // 0..100
    }

    /// <summary>The answer to "was I actually favored?" — counts first, percentages derived,
    /// so a caller can apply whatever draw convention its screen wants.</summary>
    public sealed class ForecastResult
    {
        public int Trials;
        public ulong BaseSeed;
        public int Team0Wins;
        public int Team1Wins;
        public int Draws;
        public double AvgEndTick;

        /// <summary>Per-unit survival, ascending by unit id.</summary>
        public List<UnitOdds> Units = new List<UnitOdds>();

        public int Wins(int team) => team == 0 ? Team0Wins : Team1Wins;
        public int Losses(int team) => team == 0 ? Team1Wins : Team0Wins;

        /// <summary>Strict win share, 0..100 — draws count for nobody.</summary>
        public double WinPct(int team) => Trials == 0 ? 0 : 100.0 * Wins(team) / Trials;

        /// <summary>Win share under the run layer's law that a draw is not a loss ("your board
        /// wasn't beaten" — RunController scores <c>Winner != Team1</c> as a player win). This is
        /// the number a run-facing "you were N% favored" readout wants.</summary>
        public double NotBeatenPct(int team) => Trials == 0 ? 0 : 100.0 * (Wins(team) + Draws) / Trials;

        public UnitOdds? Unit(int id)
        {
            foreach (var u in Units)
                if (u.UnitId == id)
                    return u;
            return null;
        }

        public string Report(string name)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### {name} — {Trials} re-sims from seed {BaseSeed}");
            sb.AppendLine($"- Team 0: **{WinPct(0):F0}%** favored ({Team0Wins}W / {Draws}D / {Team1Wins}L) " +
                          $"· avg length {AvgEndTick:F0} ticks ({AvgEndTick / 10.0:0.0}s)");
            sb.AppendLine();
            sb.AppendLine("| unit | team | survives |");
            sb.AppendLine("|---|---|---|");
            foreach (var u in Units)
                sb.AppendLine($"| {u.Name} #{u.UnitId} | {u.Team} | {u.SurvivalPct:F0}% |");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Win-probability re-sim: run the same board across N derived seeds and report how often it
    /// actually wins. The sim's only randomness is the crit roll (ADR 0005), so a forecast is a
    /// clean read of how much of an outcome was the board and how much was the dice.
    ///
    /// Why a FACTORY and not a unit list: <see cref="Battle"/> mutates the
    /// <see cref="UnitState"/> objects it is handed (HP, statuses, position), so every trial
    /// needs fresh bodies — the same reason the determinism tests call their <c>Setup()</c>
    /// helper once per battle. The factory must be pure: hand back the same board every call, or
    /// the forecast is sampling different fights and means nothing. <see cref="UnitDef"/>,
    /// <see cref="Trigger"/> and <see cref="FieldDef"/> are never mutated, so those pass by value.
    ///
    /// Lives in Warband.Sim because <see cref="Battle"/>'s own constructor arguments ARE the
    /// fight — nothing in the run layer is needed to describe one. The run layer forecasts by
    /// handing in a factory that rebuilds its composed warband + previewed enemies.
    /// </summary>
    public static class BattleForecast
    {
        public static ForecastResult Run(Func<IEnumerable<UnitState>> spawn, int trials, ulong baseSeed = 1,
                                         IEnumerable<(int Team, Trigger T)>? teamTriggers = null,
                                         IEnumerable<(FieldDef Def, Hex Center, int OwnerTeam)>? initialFields = null)
        {
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            if (trials < 1) throw new ArgumentOutOfRangeException(nameof(trials), "a forecast needs at least one trial");

            // Materialize once: Battle copies them into its own lists and never writes back.
            var triggers = teamTriggers == null ? null : new List<(int, Trigger)>(teamTriggers);
            var fields = initialFields == null ? null : new List<(FieldDef, Hex, int)>(initialFields);

            var forecast = new ForecastResult { Trials = trials, BaseSeed = baseSeed };
            var odds = new Dictionary<int, UnitOdds>();
            long totalTicks = 0;

            for (int trial = 0; trial < trials; trial++)
            {
                var result = new Battle(spawn(), triggers, fields, SeedFor(baseSeed, trial)).Run();
                totalTicks += result.EndTick;
                switch (result.Winner)
                {
                    case Winner.Team0: forecast.Team0Wins++; break;
                    case Winner.Team1: forecast.Team1Wins++; break;
                    default: forecast.Draws++; break;
                }

                var dead = new HashSet<int>();
                foreach (var e in result.Events)
                    if (e.Kind == EventKind.Death)
                        dead.Add(e.Target);

                foreach (var u in result.InitialUnits)
                {
                    if (!odds.TryGetValue(u.Id, out var o))
                        odds[u.Id] = o = new UnitOdds { UnitId = u.Id, Team = u.Team, Name = u.Name };
                    if (!dead.Contains(u.Id)) o.Survived++;
                }
            }

            forecast.AvgEndTick = (double)totalTicks / trials;
            var ids = new List<int>(odds.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                var o = odds[id];
                o.SurvivalPct = 100.0 * o.Survived / trials;
                forecast.Units.Add(o);
            }
            return forecast;
        }

        /// <summary>The trial's seed. Stateless splitmix64 derivation (ADR 0008): no ordering
        /// coupling between trials, and the same (base, index) always names the same fight — which
        /// is what makes a forecast reproducible rather than merely repeatable.</summary>
        public static ulong SeedFor(ulong baseSeed, int trial) => Split(Split(baseSeed) + (ulong)trial);

        private static ulong Split(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
