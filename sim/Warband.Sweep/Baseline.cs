using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// The committed balance baseline (`--baseline`, Makefile target `baseline`).
///
/// Every probe prints markdown for a human and then the numbers evaporate: answering "did my change
/// help?" meant having captured a before, by hand, from a git worktree. That is exactly what
/// happened during the 2026-07-26 pathing rework, and it is too much friction to survive contact
/// with a real tuning session.
///
/// So this reduces every instrument to one metric per line, dotted-key, stable order, and writes it
/// to a file in the repo. **The A/B is then `git diff`** — no comparison tool, no stored history, no
/// second format to keep in sync. A change that moves nothing shows an empty diff; a change that
/// moves the game shows exactly which encounters, axes and builds moved and by how much.
///
/// It deliberately does NOT assert or fail. Numbers are supposed to move — the baseline exists so a
/// session can SEE the movement and say whether it intended it, which is the content doctrine's
/// "name what is there, tune nothing" applied to change rather than to state.
///
/// Determinism: every probe is seeded, and crit is the sim's only RNG, so re-running with unchanged
/// content must reproduce this file byte for byte. If it does not, something has picked up an
/// unordered iteration or a wall clock, and that is a bug worth chasing on its own.
/// </summary>
public static class Baseline
{
    /// <summary>Sim-health guards. These are not balance — they are the "is the engine still doing
    /// its job" numbers that a balance change should NOT move, and the only reason the 2026-07-26
    /// pathing bug was measurable at all. Dead time is a living unit with no move, swing or cast
    /// for over 3s while its fight is still going.</summary>
    private const int IdleThreshold = 30;

    public static void Run(string? path)
    {
        path ??= "docs/vault/Projects/balance-baseline.md";
        var cat = new Catalog();
        var keys = new List<(string Key, string Value)>();
        void Put(string key, string value) => keys.Add((key, value));
        void Num(string key, double v) => Put(key, v.ToString("F0", CultureInfo.InvariantCulture));

        // ---- content identity ---------------------------------------------------------
        Put("content.fingerprint", cat.ContentVersion);

        // ---- node encounters ----------------------------------------------------------
        foreach (var row in EncounterProbe.Collect())
        {
            Num($"enc.{row.Id}.debut", row.DebutAct);
            for (int act = 1; act <= 3; act++)
                foreach (var a in row.ByAct[act - 1])
                    Put($"enc.{row.Id}.a{act}.{a.Axis}",
                        $"win={a.BestWin:F0} spread={a.Spread:F0} rule={a.RuleFiredPct:F0} ticks={a.AvgTicks:F0}");
        }

        var naive = EncounterProbe.CollectNaiveLine();
        Put("enc.naive.completed", $"{naive.Completed}/{naive.Total}");
        foreach (var (where, count) in naive.Deaths)
            Put($"enc.naive.died.{where.Replace(' ', '-').Replace("(", "").Replace(")", "")}", count.ToString());
        var responsive = EncounterProbe.CollectResponsiveLine();
        Put("enc.responsive.completed", $"{responsive.Completed}/{responsive.Total}");
        Put("enc.responsive.adapted", responsive.AdaptedPlacements.ToString());
        foreach (var (response, count) in responsive.Responses)
            Put($"enc.responsive.response.{response.ToLowerInvariant().Replace(' ', '-')}",
                count.ToString());
        foreach (var (where, count) in responsive.Deaths)
            Put($"enc.responsive.died.{where.Replace(' ', '-').Replace("(", "").Replace(")", "")}",
                count.ToString());

        // ---- act bosses ---------------------------------------------------------------
        for (int act = 1; act <= Encounters.BossPool.Length; act++)
        {
            var axes = BossProbe.Collect(act);
            var (passing, _, _, _) = ProbeParties.Summarise(axes);
            Put($"boss.a{act}.axes-passing", string.Join("+", passing) is { Length: > 0 } s ? s : "none");
            foreach (var a in axes)
                Put($"boss.a{act}.{a.Axis}",
                    $"win={a.BestWin:F0} spread={a.Spread:F0} rule={a.RuleFiredPct:F0} ticks={a.AvgTicks:F0}");
        }

        // ---- hero builds --------------------------------------------------------------
        var sweep = BuildSweep.Run(cat);
        Num("build.caphits", sweep.CapHits);
        Num("build.mirror-nondraws", sweep.MirrorNonDraws.Count);
        foreach (var c in sweep.Classes)
            Put($"build.class.{c.Class}", $"avg={c.Avg:F0} best={c.Best:F0} worst={c.Worst:F0}");
        // Only the deltas the sweep itself considers worth printing — a full 32-row node table
        // would churn the diff on noise below the threshold anyone acts on.
        foreach (var d in sweep.NodeDeltas.Where(d => Math.Abs(d.Delta) >= 15))
            Put($"build.node.{d.Node}-vs-{d.Rival}", $"delta={d.Delta:F0}");
        foreach (var t in sweep.Tiers)
            Put($"run.{t.Tier}".ToLowerInvariant(),
                $"victory={t.VictoryPct:F0} fightwin={t.FightWinPct:F0} boss={t.AvgBossWins:F2} " +
                $"gold={t.AvgGold:F0} caps={t.CapHits}");
        Num("build.flags", sweep.Flags.Count);
        foreach (var f in sweep.Flags.OrderBy(f => f, StringComparer.Ordinal))
            Put("build.flag", f);

        // ---- sim health ---------------------------------------------------------------
        var health = Health();
        Put("health.deadtime-pct", health.DeadTimePct.ToString("F2", CultureInfo.InvariantCulture));
        Put("health.never-swung-pct", health.NeverSwungPct.ToString("F2", CultureInfo.InvariantCulture));
        Put("health.frozen-pct", health.FrozenPct.ToString("F2", CultureInfo.InvariantCulture));

        // ---- render -------------------------------------------------------------------
        int width = keys.Max(k => k.Key.Length);
        var body = new StringBuilder();
        body.AppendLine("# Balance baseline — committed golden numbers");
        body.AppendLine();
        body.AppendLine("**Regenerate with `make baseline`. The A/B is `git diff`.**");
        body.AppendLine();
        body.AppendLine("Every authoring instrument (`--enc`, `--boss`, the outlier sweep, run EV, sim");
        body.AppendLine("health) reduced to one metric per line. This file is not an assertion and nothing");
        body.AppendLine("fails when it moves — it exists so a session can SEE what a change did to the game");
        body.AppendLine("instead of reconstructing a before from a worktree. Regenerate it as part of any");
        body.AppendLine("change to content, the sim, or the probes, and read the diff before committing.");
        body.AppendLine();
        body.AppendLine("`win` is the best result across the six formations; `spread` is best − worst, which");
        body.AppendLine("is the number that says whether placement mattered. Encounter and boss rows are per");
        body.AppendLine("answer axis (balanced / reach / control / damage). Party size follows the act:");
        body.AppendLine($"{string.Join(", ", Enumerable.Range(1, 3).Select(a => $"act {a} = {ProbeParties.SizeAt(a)} heroes"))} —");
        body.AppendLine("the strongest difficulty dial in the game, so every number here is conditional on it.");
        body.AppendLine();
        body.AppendLine("Byte-stable: the sim is deterministic and every probe is seeded, so an unchanged");
        body.AppendLine("game must reproduce this file exactly.");
        body.AppendLine();
        body.AppendLine("```");
        foreach (var (key, value) in keys)
            body.AppendLine($"{key.PadRight(width)}  {value}");
        body.AppendLine("```");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, body.ToString());
        Console.WriteLine($"wrote baseline: {path} — {keys.Count} metrics, content {cat.ContentVersion}");
    }

    private readonly struct HealthResult
    {
        public readonly double DeadTimePct, NeverSwungPct, FrozenPct;
        public HealthResult(double dead, double never, double frozen)
        { DeadTimePct = dead; NeverSwungPct = never; FrozenPct = frozen; }
    }

    /// <summary>
    /// Is the engine actually letting units fight? Sweeps every authored encounter and counts the
    /// time living units spend doing nothing at all. Before the 2026-07-26 routing rework this sat
    /// at 5.23% dead time with 4.71% of units never swinging once in a whole fight — a unit in
    /// twenty spent an entire authored encounter frozen behind an ally. A balance change must not
    /// move these; if it does, something structural broke.
    /// </summary>
    private static HealthResult Health()
    {
        long idle = 0, live = 0, frozen = 0, total = 0, never = 0;

        for (int act = 1; act <= 3; act++)
            foreach (var factory in Encounters.PoolFor(act))
                foreach (var (_, party) in ProbeParties.Axes)
                    for (int seed = 0; seed < 4; seed++)
                    {
                        var units = new List<UnitState>();
                        int id = ProbeParties.Field(units, act, party, ProbeParties.Formations[0].Slots);
                        foreach (var e in factory(act).Enemies)
                        {
                            Encounters.Scale(e.Def, act, FightTier.Stable);
                            units.Add(UnitState.Spawn(id++, 1, e.Def, e.Pos));
                        }
                        var r = new Battle(units, seed: (ulong)(seed + 1)).Run();
                        Tally(r, id, ref idle, ref live, ref frozen, ref total, ref never);
                    }

        return new HealthResult(100.0 * idle / live, 100.0 * never / total, 100.0 * frozen / total);
    }

    private static void Tally(BattleResult r, int unitCount, ref long idle, ref long live,
                              ref long frozen, ref long total, ref long never)
    {
        int cap = Math.Min(r.EndTick, Battle.OvertimeStartTick);
        var acted = new Dictionary<int, List<int>>();
        var died = new Dictionary<int, int>();
        var swung = new HashSet<int>();
        foreach (var e in r.Events)
        {
            if (e.Kind == EventKind.Death) died[e.Target] = e.Tick;
            if (e.Kind == EventKind.Attack) swung.Add(e.Source);
            bool active = e.Kind == EventKind.MoveStart || e.Kind == EventKind.Move
                       || e.Kind == EventKind.Attack || e.Kind == EventKind.Cast
                       || e.Kind == EventKind.Leap;
            if (active && e.Source >= 0)
            {
                if (!acted.TryGetValue(e.Source, out var l)) acted[e.Source] = l = new List<int>();
                l.Add(e.Tick);
            }
        }

        for (int u = 0; u < unitCount; u++)
        {
            total++;
            if (!swung.Contains(u)) never++;
            int end = died.TryGetValue(u, out var d) ? Math.Min(d, cap) : cap;
            if (end <= 0) continue;
            var ticks = acted.TryGetValue(u, out var l) ? l : new List<int>();
            int cursor = 0, worst = 0;
            foreach (int t in ticks)
            {
                if (t > end) break;
                int gap = t - cursor;
                if (gap > worst) worst = gap;
                if (gap > IdleThreshold) idle += gap - IdleThreshold;
                cursor = t;
            }
            int tail = end - cursor;
            if (tail > worst) worst = tail;
            if (tail > IdleThreshold) idle += tail - IdleThreshold;
            live += end;
            if (worst >= IdleThreshold) frozen++;
        }
    }
}
