using System;
using System.Collections.Generic;
using System.Text;

namespace Warband.Sim
{
    /// <summary>One unit's line in the post-fight chart. Every number is a fold over the
    /// event log — nothing here re-simulates or reads live battle state.</summary>
    public sealed class UnitSummary
    {
        public int UnitId;
        public int Team;
        public string Name = "";
        public string ChassisId = "";

        public int DamageDealt;          // all causes, damage this unit is the SOURCE of
        public int DamageTaken;          // total incoming, INCLUDING the part shields ate
        public int HealingDone;
        public int HealingReceived;
        public int ShieldAbsorbed;       // of DamageTaken, the part this unit's shields ate
                                         // (DamageDealt.Aux) — HP actually lost is the difference
        public int Kills;                // killing blows credited to this unit (see FightSummary docs)

        public int DeathTick = -1;       // -1 = survived the fight
        public int KilledBy = -1;        // -1 = nobody: survived, or the storm/an ownerless hazard
        public Cause KilledByCause;      // cause of the blow that finished it; None if it survived
        public bool Died => DeathTick >= 0;

        /// <summary>This unit's share of its TEAM's damage, 0..100. A team with no damage
        /// gives every member 0 (not NaN) so a chart can bind to it unguarded.</summary>
        public double DamagePctOfTeam;

        // Indexed by Cause ordinal — Cause is contiguous from 0, so the buckets extend
        // automatically if the enum grows.
        internal int[] ByCause = new int[FightSummary.CauseCount];

        public int DamageBy(Cause cause)
        {
            int i = (int)cause;
            return i >= 0 && i < ByCause.Length ? ByCause[i] : 0;
        }

        /// <summary>The non-empty damage buckets in Cause-enum order — what a stacked bar
        /// renders directly.</summary>
        public List<(Cause Cause, int Amount)> DamageBuckets()
        {
            var list = new List<(Cause, int)>();
            for (int i = 0; i < ByCause.Length; i++)
                if (ByCause[i] > 0)
                    list.Add(((Cause)i, ByCause[i]));
            return list;
        }
    }

    /// <summary>Team roll-up. Deaths + Survivors partition Units.</summary>
    public sealed class TeamSummary
    {
        public int Team;
        public int Units;
        public int DamageDealt;
        public int DamageTaken;
        public int HealingDone;
        public int ShieldAbsorbed;
        public int Kills;
        public int Deaths;
        public int Survivors => Units - Deaths;
    }

    /// <summary>One death, as the post-fight story reads it: who fell, when, to whom, to what,
    /// and by how much the blow overshot.</summary>
    public sealed class DeathBeat
    {
        public int Tick;
        public int Victim;
        public int Killer = -1;       // -1 = the storm or an ownerless field: no unit to credit
        public Cause Cause;           // cause of the finishing blow
        public int Overkill;          // Death.Amount — damage past 0 HP
        public bool KillerInferred;   // the Death event named no killer; Killer came from the
                                      // finishing blow's Source/Root instead
    }

    /// <summary>
    /// The post-fight comprehension fold: (initial snapshot, event log) → per-unit contribution,
    /// team totals and the death story. Consumes exactly what a replay carries, so the client
    /// builds it from bytes it already has and never re-simulates to explain a fight.
    /// Sibling of <see cref="ReplayInspector"/> (which folds the same log into PRESENTATION
    /// signatures) and of <see cref="FightStats"/> (the balance-harness fold).
    ///
    /// KILL ATTRIBUTION — the thing a UI consumer must get right:
    /// <list type="bullet">
    /// <item>Death events carry <c>Source</c> = the victim's last unit-sourced damager, and
    /// <c>Amount</c> = overkill. They do NOT carry a Cause, and their <c>Root</c> is never
    /// set (it stays -1), so Root is not a fallback — the fallback is the finishing blow.</item>
    /// <item>Storm ticks and fields with no owner deal damage with Source = Root = -1, which
    /// deliberately does not overwrite the victim's last damager. So a unit softened by an
    /// enemy and finished by the storm still credits that enemy, while the KilledByCause
    /// reads Storm. That is not a bug: the credit and the finishing blow are two facts.</item>
    /// <item>A unit nobody ever damaged has Death.Source = -1. Killer stays -1 and
    /// <see cref="DeathBeat.KillerInferred"/> is false — the environment killed it, and
    /// its damage lands in <see cref="UnattributedDamage"/>, not in any unit's DamageDealt.</item>
    /// <item>Kills here are KILLING BLOWS (one credit per death), unlike
    /// <see cref="UnitFightStats.Kills"/>, which credits every unit that participated.</item>
    /// </list>
    /// </summary>
    public sealed class FightSummary
    {
        internal static readonly int CauseCount = Enum.GetValues(typeof(Cause)).Length;

        /// <summary>Read off the End event. A log that carries no verdict reads as a Draw —
        /// the same fallback Battle itself uses — never as a win for team 0.</summary>
        public Winner Winner = Winner.Draw;
        public int EndTick;

        /// <summary>Units ordered by damage dealt, descending — a chart binds to this list as
        /// it stands. Ties break on ascending id so the order is stable across runs.</summary>
        public List<UnitSummary> Units = new List<UnitSummary>();

        /// <summary>Team roll-ups in ascending team order.</summary>
        public List<TeamSummary> Teams = new List<TeamSummary>();

        /// <summary>Deaths in log order — the fight's beats.</summary>
        public List<DeathBeat> Beats = new List<DeathBeat>();

        /// <summary>Damage no unit dealt: storm ticks and ownerless field hazards. Team totals
        /// exclude it, so dealt-vs-taken only balances once this is added back.</summary>
        public int UnattributedDamage;

        public UnitSummary? Unit(int id)
        {
            foreach (var u in Units)
                if (u.UnitId == id)
                    return u;
            return null;
        }

        public TeamSummary? TeamTotals(int team)
        {
            foreach (var t in Teams)
                if (t.Team == team)
                    return t;
            return null;
        }

        public static FightSummary Build(BattleResult result) =>
            Build(result.InitialUnits, result.Events);

        /// <summary>Fold a replay into the post-fight screen. <paramref name="initial"/> supplies
        /// team and identity (the event log alone carries neither); everything else is derived
        /// from the events.</summary>
        public static FightSummary Build(IReadOnlyList<PlaybackUnit> initial, IReadOnlyList<BattleEvent> events)
        {
            var summary = new FightSummary();
            var byId = new Dictionary<int, UnitSummary>();
            foreach (var u in initial)
            {
                var s = new UnitSummary
                {
                    UnitId = u.Id, Team = u.Team, Name = u.Name, ChassisId = u.ChassisId,
                };
                byId[u.Id] = s;
                summary.Units.Add(s);
            }

            // Finishing blow per victim: the last damage that landed on them at or before they
            // died. Tracked live because the log is chronological — by the time the Death event
            // arrives, this holds exactly the blow that ended them.
            var lastHit = new Dictionary<int, BattleEvent>();

            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case EventKind.DamageDealt:
                    {
                        if (byId.TryGetValue(e.Source, out var src))
                        {
                            src.DamageDealt += e.Amount;
                            int c = (int)e.Cause;
                            if (c >= 0 && c < src.ByCause.Length) src.ByCause[c] += e.Amount;
                        }
                        else summary.UnattributedDamage += e.Amount;

                        if (byId.TryGetValue(e.Target, out var tgt))
                        {
                            tgt.DamageTaken += e.Amount;
                            if (e.Aux > 0) tgt.ShieldAbsorbed += e.Aux;
                        }
                        lastHit[e.Target] = e;
                        break;
                    }
                    case EventKind.Heal:
                        if (byId.TryGetValue(e.Source, out var healer)) healer.HealingDone += e.Amount;
                        if (byId.TryGetValue(e.Target, out var healed)) healed.HealingReceived += e.Amount;
                        break;
                    case EventKind.Death:
                    {
                        lastHit.TryGetValue(e.Target, out var blow);
                        int killer = e.Source;
                        bool inferred = false;
                        if (killer < 0 && blow != null)
                        {
                            // Death named nobody. The finishing blow is the only other witness:
                            // prefer its Root (the unit whose chain originated it) over its Source.
                            killer = blow.Root >= 0 ? blow.Root : blow.Source;
                            inferred = killer >= 0;
                        }

                        var beat = new DeathBeat
                        {
                            Tick = e.Tick, Victim = e.Target, Killer = killer,
                            Cause = blow?.Cause ?? Cause.None, Overkill = e.Amount,
                            KillerInferred = inferred,
                        };
                        summary.Beats.Add(beat);

                        if (byId.TryGetValue(e.Target, out var victim))
                        {
                            victim.DeathTick = e.Tick;
                            victim.KilledBy = killer;
                            victim.KilledByCause = beat.Cause;
                        }
                        if (byId.TryGetValue(killer, out var slayer)) slayer.Kills++;
                        break;
                    }
                    case EventKind.End:
                        summary.Winner = (Winner)e.Amount;
                        break;
                }
                if (e.Tick > summary.EndTick) summary.EndTick = e.Tick;
            }

            // Team roll-ups, then each unit's share of its own team's damage.
            var teams = new Dictionary<int, TeamSummary>();
            foreach (var u in summary.Units)
            {
                if (!teams.TryGetValue(u.Team, out var t))
                    teams[u.Team] = t = new TeamSummary { Team = u.Team };
                t.Units++;
                t.DamageDealt += u.DamageDealt;
                t.DamageTaken += u.DamageTaken;
                t.HealingDone += u.HealingDone;
                t.ShieldAbsorbed += u.ShieldAbsorbed;
                t.Kills += u.Kills;
                if (u.Died) t.Deaths++;
            }
            foreach (var u in summary.Units)
            {
                int total = teams[u.Team].DamageDealt;
                u.DamagePctOfTeam = total > 0 ? 100.0 * u.DamageDealt / total : 0;
            }

            foreach (var team in Sorted(teams.Keys))
                summary.Teams.Add(teams[team]);

            summary.Units.Sort((a, b) =>
                a.DamageDealt != b.DamageDealt ? b.DamageDealt.CompareTo(a.DamageDealt)
                                               : a.UnitId.CompareTo(b.UnitId));
            return summary;
        }

        private static List<int> Sorted(IEnumerable<int> keys)
        {
            var list = new List<int>(keys);
            list.Sort();
            return list;
        }

        /// <summary>A compact markdown post-fight readout — the terminal-side twin of the
        /// screen the client will draw.</summary>
        public string Report(string name)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### {name} — {Winner} at tick {EndTick} ({EndTick / 10.0:0.0}s)");
            sb.AppendLine("| unit | team | dealt | % | taken | absorbed | healed | kills | died |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var u in Units)
                sb.AppendLine($"| {u.Name} #{u.UnitId} | {u.Team} | {u.DamageDealt} | {u.DamagePctOfTeam:F0} " +
                              $"| {u.DamageTaken} | {u.ShieldAbsorbed} | {u.HealingDone} | {u.Kills} " +
                              $"| {(u.Died ? $"t{u.DeathTick} by #{u.KilledBy} ({u.KilledByCause})" : "-")} |");
            if (UnattributedDamage > 0)
                sb.AppendLine($"\nUnattributed (storm / ownerless fields): {UnattributedDamage}");
            return sb.ToString();
        }
    }
}
