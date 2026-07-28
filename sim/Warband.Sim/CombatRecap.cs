using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>One hero's line in the contribution chart.</summary>
    public sealed class RecapRow
    {
        public int UnitId;
        public string Name = "";
        public string ChassisId = "";

        public int Damage;
        public int Healing;
        public int Absorbed;
        public int Taken;
        public int Kills;

        /// <summary>Share of the TEAM's damage, 0..100 — the number printed on the row.</summary>
        public double PctOfTeam;

        /// <summary>Share of the BEST row's damage, 0..1 — the width the bar is drawn at.
        /// Normalising to the leader instead of the team is what makes the chart readable:
        /// six even contributors would otherwise each draw a 17%-wide stub.</summary>
        public double BarFill;

        public bool Died;
        public int DeathTick = -1;
    }

    /// <summary>One slice of the damage-composition bar — the "why did my build work" chart.</summary>
    public sealed class RecapSegment
    {
        public Cause Cause;
        public string Name = "";
        public int Amount;
        public double Pct;        // of the team's total damage, 0..100
    }

    /// <summary>One death on the fight's clock.</summary>
    public sealed class RecapBeat
    {
        public int Tick;
        /// <summary>Position on the timeline track, 0..1.</summary>
        public double At;
        public string Victim = "";
        /// <summary>The killer's name, or the cause's name when no unit can be credited
        /// (the storm, an ownerless field).</summary>
        public string Killer = "";
        public string Cause = "";
        public int Overkill;
        /// <summary>The victim was on the viewing team — the timeline draws our losses and
        /// theirs on opposite sides of the track.</summary>
        public bool Friendly;
    }

    /// <summary>
    /// The post-fight REPORT, folded from <see cref="FightSummary"/> for one viewing team:
    /// contribution rows, the damage composition, and the death timeline.
    ///
    /// This is the view-model, and it lives here rather than in the client for the same reason
    /// every other fold does — a chart's bugs are arithmetic bugs (shares that do not sum,
    /// bars normalised to the wrong denominator, a timeline that divides by a zero-tick fight),
    /// and arithmetic is testable headlessly while a Unity panel is not. Unity draws these
    /// numbers and computes none of them.
    ///
    /// Everything is derived from (initial snapshot, event log) by way of FightSummary, so a
    /// replay explains itself without re-simulating.
    /// </summary>
    public sealed class CombatRecap
    {
        private const double TicksPerSecond = 10.0;

        /// <summary>The team this report is written for — rows and composition cover it alone.</summary>
        public int Team;

        public Winner Winner = Winner.Draw;
        public bool Victory;
        public int EndTick;
        public double Seconds;

        /// <summary>Viewing team, damage descending. Ties break on ascending id (inherited from
        /// FightSummary), so the chart is stable across runs of the same replay.</summary>
        public List<RecapRow> Rows = new List<RecapRow>();

        /// <summary>The viewing team's damage split by cause, descending by amount. Non-zero
        /// buckets only — a fight with no Burn draws no Burn slice rather than a zero one.</summary>
        public List<RecapSegment> Composition = new List<RecapSegment>();

        /// <summary>Denominator of <see cref="Composition"/>: the team's total damage dealt.</summary>
        public int CompositionTotal;

        /// <summary>Every death in the fight, both teams, chronological.</summary>
        public List<RecapBeat> Beats = new List<RecapBeat>();

        public int Survivors;
        public int Losses;
        public int HealingDone;
        public int ShieldAbsorbed;

        /// <summary>Where the Waning opened on the timeline, 0..1 — or -1 when the fight ended
        /// before overtime ever started. Gives item 11's clock a place in the report.</summary>
        public double WaningAt = -1;
        public bool ReachedWaning => WaningAt >= 0;

        public static CombatRecap Build(BattleResult result, int team) =>
            Build(FightSummary.Build(result), team);

        public static CombatRecap Build(FightSummary summary, int team)
        {
            var recap = new CombatRecap
            {
                Team = team,
                Winner = summary.Winner,
                EndTick = summary.EndTick,
                Seconds = summary.EndTick / TicksPerSecond,
            };
            recap.Victory = summary.Winner == (team == 0 ? Winner.Team0 : Winner.Team1);

            // ① Contribution. FightSummary is already sorted by damage descending, so filtering
            //    preserves that order and the first surviving row is the leader.
            int best = 0;
            var byCause = new int[FightSummary.CauseCount];
            foreach (var unit in summary.Units)
            {
                if (unit.Team != team) continue;
                if (unit.DamageDealt > best) best = unit.DamageDealt;
                for (int c = 0; c < byCause.Length; c++) byCause[c] += unit.DamageBy((Cause)c);

                recap.Rows.Add(new RecapRow
                {
                    UnitId = unit.UnitId,
                    Name = unit.Name,
                    ChassisId = unit.ChassisId,
                    Damage = unit.DamageDealt,
                    Healing = unit.HealingDone,
                    Absorbed = unit.ShieldAbsorbed,
                    Taken = unit.DamageTaken,
                    Kills = unit.Kills,
                    PctOfTeam = unit.DamagePctOfTeam,
                    Died = unit.Died,
                    DeathTick = unit.DeathTick,
                });

                recap.HealingDone += unit.HealingDone;
                recap.ShieldAbsorbed += unit.ShieldAbsorbed;
                if (unit.Died) recap.Losses++; else recap.Survivors++;
            }
            foreach (var row in recap.Rows)
                row.BarFill = best > 0 ? (double)row.Damage / best : 0;

            // ② Composition. Summing the team's per-cause buckets rather than re-folding the log:
            //    UnitSummary.ByCause already carries every Cause, not just the five the balance
            //    harness names, so Burn and Counter get their own slices for free.
            for (int c = 0; c < byCause.Length; c++) recap.CompositionTotal += byCause[c];
            for (int c = 0; c < byCause.Length; c++)
            {
                if (byCause[c] <= 0) continue;
                var cause = (Cause)c;
                recap.Composition.Add(new RecapSegment
                {
                    Cause = cause,
                    Name = Lexicon.Of(cause).Name,
                    Amount = byCause[c],
                    Pct = recap.CompositionTotal > 0
                        ? 100.0 * byCause[c] / recap.CompositionTotal
                        : 0,
                });
            }
            recap.Composition.Sort((a, b) =>
                a.Amount != b.Amount ? b.Amount.CompareTo(a.Amount)
                                     : ((int)a.Cause).CompareTo((int)b.Cause));

            // ③ Timeline. A fight that ends on tick 0 would divide by zero; everything then
            //    stacks at the start of the track, which is exactly where it happened.
            double span = summary.EndTick > 0 ? summary.EndTick : 1;
            foreach (var beat in summary.Beats)
            {
                var victim = summary.Unit(beat.Victim);
                var killer = summary.Unit(beat.Killer);
                string cause = Lexicon.Of(beat.Cause).Name;
                recap.Beats.Add(new RecapBeat
                {
                    Tick = beat.Tick,
                    At = Clamp01(beat.Tick / span),
                    Victim = victim?.Name ?? "",
                    Killer = killer?.Name ?? cause,
                    Cause = cause,
                    Overkill = beat.Overkill,
                    Friendly = victim != null && victim.Team == team,
                });
            }

            // `>=`, not `>`: Battle's storm fires on `_tick >= OvertimeStartTick`, so a fight
            // ending exactly on the threshold already took a storm tick. Reporting no Waning
            // there would deny damage the log contains — the marker sits on the end cap instead.
            if (summary.EndTick >= Battle.OvertimeStartTick)
                recap.WaningAt = Clamp01(Battle.OvertimeStartTick / span);

            return recap;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
