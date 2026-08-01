using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// The encounter authoring instrument (`--enc`). Same bar as the outlier sweep and the oath probe:
/// **NAME what is there, tune nothing.** It exists so authoring a PvE encounter is a measured act
/// rather than a guess, and it answers three questions an author actually has:
///
///   1. **Is it survivable at all?** Win% for a plausible warband at each act.
///   2. **Does placement matter?** The spread between the best and worst formation. A flat spread
///      means the encounter poses no positional problem, whatever its rule text claims.
///   3. **Does its rule actually fire?** An encounter whose mechanic never triggers before the
///      fight ends is decoration.
///
/// A "good" encounter here is NOT one the player always wins. It is one where the spread is wide —
/// where where-you-stand changed the answer.
///
/// **Four answer axes since 2026-07-26.** This probe used to run one hard-coded party, which made
/// every "poses nothing" verdict conditional on a warband the author never chose: an encounter flat
/// for bulwark/pyro/sharpshot and sharp for a control party read as flat, with nothing in the report
/// to hint otherwise. It now uses the same four axes and formations as `--boss` (see
/// <see cref="ProbeParties"/>), so a flat verdict means flat for every kind of strength — and the
/// two instruments can no longer describe two different games.
/// </summary>
public static class EncounterProbe
{
    private const int SeedsPerArrangement = 24;

    /// <summary>Everything measured for one encounter, once. Rendering — the markdown report and
    /// the committed baseline — reads this rather than re-running the fights.</summary>
    public sealed class Row
    {
        public string Id = "", Name = "", RuleName = "", Pressure = "", Bodies = "";
        public int DebutAct;
        /// <summary>act (1-based index 0..2) → one result per answer axis.</summary>
        public List<ProbeParties.AxisResult>[] ByAct = new List<ProbeParties.AxisResult>[3];
    }

    public sealed class NaiveLine
    {
        public int Completed, Total;
        public int AdaptedPlacements;
        public List<(string Where, int Count)> Deaths = new List<(string, int)>();
        public List<(string Response, int Count)> Responses = new List<(string, int)>();
    }

    public static List<Row> Collect()
    {
        var rows = new List<Row>();
        foreach (var factory in Encounters.NodePool)
        {
            var def = factory(1);
            var row = new Row
            {
                Id = def.Id, Name = def.Name, RuleName = def.RuleName, Pressure = def.Pressure,
                // Judge an encounter at the FIRST act it can actually appear in — an act-2
                // encounter measured against a rank-C opening warband is a fact about the pool,
                // not a flaw.
                DebutAct = Enumerable.Range(1, 3)
                    .First(a => Encounters.PoolFor(a).Any(f => f(a).Id == def.Id)),
            };
            for (int act = 1; act <= 3; act++)
            {
                var atAct = factory(act);
                if (act == row.DebutAct)
                    row.Bodies = string.Join(", ", atAct.Enemies
                        .GroupBy(e => e.Def.Name)
                        .Select(g => g.Count() > 1 ? $"{g.Count()}× {g.Key}" : g.Key));
                row.ByAct[act - 1] = ProbeParties.Active
                    .Select(a => ProbeParties.Across(a.Axis,
                        slots => Measure(factory, act, a.Party, slots)))
                    .ToList();
            }
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// The naive line: can the bot's fixed comp survive the pool at all? The integration smoke test
    /// plays Cleric/Bulwark/Shade with front/back placement and no draft choice. It is the WEAKEST
    /// legal answer, so it is the floor check: an act-1 pool a legal comp cannot clear is
    /// prescribing a build, which the encounter law forbids.
    /// </summary>
    public static NaiveLine CollectNaiveLine()
    {
        return CollectFullRunLine(responsive: false);
    }

    /// <summary>
    /// The same fixed starter, greedy shop, Fraying wager and seeds as the no-response line, but
    /// it reads the exact disclosed encounter id and makes one plain-language formation response.
    /// It never simulates ahead or searches the six probe formations. This is ADR 0031's missing
    /// rung between "ignore the fight" and purpose-built parties choosing their oracle-best shape.
    /// </summary>
    public static NaiveLine CollectResponsiveLine()
    {
        return CollectFullRunLine(responsive: true);
    }

    private static NaiveLine CollectFullRunLine(bool responsive)
    {
        var content = new Catalog();
        var responseCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        RunPolicy? policy = responsive
            ? new RunPolicy
            {
                Place = run => ResponsivePlacement(run, content, responseCounts),
            }
            : null;
        var reports = RunHarness.PlayMany(
            12, seedBase: 4000, content, policy: policy);
        var line = new NaiveLine
        {
            Total = 12,
            Completed = reports.Count(r => r.Final.Phase == RunPhase.Complete),
            AdaptedPlacements = responseCounts
                .Where(pair => pair.Key != "DEFAULT")
                .Sum(pair => pair.Value),
        };
        line.Deaths = reports.Where(r => r.Final.Phase != RunPhase.Complete)
            .Select(r => r.Fights.LastOrDefault())
            .Where(f => f != null)
            .GroupBy(f => $"act {f!.Act} node {f.Node}{(f.IsBoss ? " (boss)" : "")}")
            .Select(g => (Where: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count).ThenBy(g => g.Where, StringComparer.Ordinal)
            .ToList();
        line.Responses = responseCounts
            .Select(pair => (Response: pair.Key, Count: pair.Value))
            .OrderByDescending(pair => pair.Count)
            .ThenBy(pair => pair.Response, StringComparer.Ordinal)
            .ToList();
        return line;
    }

    private static IReadOnlyList<Hex> ResponsivePlacement(
        RunController run, IRunContent content, Dictionary<string, int> responseCounts)
    {
        string encounterId = run.PreviewBrief(FightTier.Fraying).Id;
        string response = ResponseFor(encounterId);
        responseCounts.TryGetValue(response, out int previous);
        responseCounts[response] = previous + 1;

        List<Hex> baseline = RunHarness.DefaultPlacement(run, content).ToList();
        int[] splitColumns = { 0, 7, 1, 6, 2, 5, 3, 4 };
        int[] rightColumns = { 6, 5, 7, 4, 3, 2, 1, 0 };
        for (int i = 0; i < baseline.Count; i++)
        {
            Hex slot = baseline[i];
            baseline[i] = response switch
            {
                "ADVANCE" => Hex.FromRowCol(Math.Min(3, slot.Row + 2), slot.Col),
                "TURTLE" => Hex.FromRowCol(Math.Max(0, slot.Row - 2), slot.Col),
                "SPLIT" => Hex.FromRowCol(slot.Row, splitColumns[i]),
                "RIGHT STACK" => Hex.FromRowCol(slot.Row, rightColumns[i]),
                _ => slot,
            };
        }
        return baseline;
    }

    /// <summary>
    /// An authored response to the disclosed problem, not an outcome lookup. New encounter ids
    /// fall back safely to the default range-aware formation and appear as DEFAULT in the report.
    /// </summary>
    private static string ResponseFor(string encounterId) => encounterId switch
    {
        "gnawing-hour" => "TURTLE",
        "the-long-range" => "ADVANCE",
        "ninth-bell" => "ADVANCE",
        "the-drop" => "ADVANCE",
        "slagworks" => "SPLIT",
        "long-procession" => "ADVANCE",
        "bonded-pair" => "RIGHT STACK",
        "ashfall-battery" => "SPLIT",
        "waning-crown" => "ADVANCE",
        _ => "DEFAULT",
    };

    public static void Run(bool includeCandidates = false)
    {
        ProbeParties.IncludeCandidates = includeCandidates;
        var report = new StringBuilder();
        report.AppendLine("# Encounter probe — authored PvE node pool");
        report.AppendLine();
        report.AppendLine($"{ProbeParties.Formations.Length} formations × {ProbeParties.Active.Length} " +
                          $"answer axes × {SeedsPerArrangement} seeds per act, each party sized and " +
                          "ranked to its act (3 heroes rank C at act 1; forked from act 2). Crit is " +
                          "the sim's only RNG, so seeds are the whole distribution.");
        report.AppendLine();
        report.AppendLine("**The bar is not win%.** It is the SPREAD: if every formation scores the " +
                          "same, the encounter poses no placement problem no matter what its rule says. " +
                          "Cells are `best win% (spread across formations)`.");
        report.AppendLine();

        foreach (var row in Collect())
        {
            report.AppendLine($"## {row.Name} — `{row.RuleName}`");
            report.AppendLine();
            report.AppendLine($"> {row.Pressure}");
            report.AppendLine();
            report.AppendLine($"Debuts in act {row.DebutAct} — {row.Bodies}.");
            report.AppendLine();
            report.AppendLine("| act | heroes | " + string.Join(" | ", ProbeParties.Active.Select(a => a.Axis)) +
                              " | axes | rule |");
            report.AppendLine("|---|---|" + string.Concat(ProbeParties.Active.Select(_ => "---|")) + "---|---|");
            for (int act = 1; act <= 3; act++)
            {
                var axes = row.ByAct[act - 1];
                var (passing, _, _, _) = ProbeParties.Summarise(axes);
                report.AppendLine($"| {act} | {ProbeParties.SizeAt(act)} | " +
                    string.Join(" | ", axes.Select(a => $"{a.BestWin:F0} ({a.Spread:F0})")) +
                    $" | {passing.Count}/{axes.Count} | {axes.Average(a => a.RuleFiredPct):F0}% |");
            }
            report.AppendLine();
            foreach (var line in Verdict(row))
                report.AppendLine("> " + line);
            report.AppendLine();
        }

        var naive = CollectNaiveLine();
        report.AppendLine("## Full-run floor 1 — no response");
        report.AppendLine();
        report.AppendLine("Same plausible starter and greedy-legal shop policy as the harness; " +
                          "range-aware default placement never changes for the disclosed rule.");
        report.AppendLine($"- Runs completed: **{naive.Completed}/{naive.Total}**");
        foreach (var (where, count) in naive.Deaths)
            report.AppendLine($"- died at {where}: {count}×");
        report.AppendLine();

        var responsive = CollectResponsiveLine();
        report.AppendLine("## Full-run floor 2 — disclosed-rule response");
        report.AppendLine();
        report.AppendLine("Same seeds, starter, shop policy and wagers; placement makes one " +
                          "deterministic response to the published encounter id. It does not " +
                          "simulate ahead or search for the best formation.");
        report.AppendLine($"- Runs completed: **{responsive.Completed}/{responsive.Total}**");
        report.AppendLine($"- adapted placements: **{responsive.AdaptedPlacements}**");
        foreach (var (response, count) in responsive.Responses)
            report.AppendLine($"- {response}: {count}×");
        foreach (var (where, count) in responsive.Deaths)
            report.AppendLine($"- died at {where}: {count}×");
        report.AppendLine();

        Console.WriteLine(report.ToString());
    }

    /// <summary>The findings, at the encounter's debut act. Stated as observations for an author
    /// to read, not as a pass/fail gate for a build system.</summary>
    public static IEnumerable<string> Verdict(Row row)
    {
        var axes = row.ByAct[row.DebutAct - 1];
        var (passing, marginal, bestSpread, bestWin) = ProbeParties.Summarise(axes);

        if (bestWin < 20)
            yield return $"**UNSURVIVABLE at act {row.DebutAct}** — no axis clears it from any " +
                         "formation. That is numbers, not design.";
        else if (passing.Count == 0)
            yield return $"**PUNISHING** — nothing passes cleanly; best axis peaks at {bestWin:F0}%." +
                         (marginal.Count > 0 ? $" Marginal: {string.Join(", ", marginal)}." : "");
        else if (passing.Count == ProbeParties.Active.Length && bestWin > 95 && bestSpread < 10)
            yield return "**FREE** — every axis wins from every formation. It poses nothing yet.";
        else if (passing.Count == 1)
            yield return $"**PRESCRIBES A BUILD** — only `{passing[0]}` clears it. " +
                         "pve-encounters.md: an encounter must admit several kinds of answer.";
        else
            yield return $"**ADMITS {passing.Count} ANSWERS** — {string.Join(", ", passing)} clear it" +
                         (marginal.Count > 0 ? $"; {string.Join(", ", marginal)} marginal." : ".");

        if (bestSpread < 15)
            yield return $"**FLAT** — placement moves the result by at most {bestSpread:F0} points. " +
                         "Placement is the only order the player gives; this encounter ignores it.";
        else
        {
            var sharpest = axes.OrderByDescending(a => a.Spread).First();
            yield return $"Placement swings the result by up to {bestSpread:F0} points " +
                         $"(`{sharpest.Axis}`: {sharpest.BestFormation} {sharpest.BestWin:F0}% vs " +
                         $"{sharpest.WorstFormation} {sharpest.WorstWin:F0}%).";
        }
    }

    private static ProbeParties.Outcome Measure(Func<int, EncounterDef> factory, int act,
                                                (string Chassis, string Node)[] party, Hex[] slots)
    {
        int wins = 0, fired = 0;
        long ticks = 0;

        for (int seed = 0; seed < SeedsPerArrangement; seed++)
        {
            var units = new List<UnitState>();
            int id = ProbeParties.Field(units, act, party, slots);

            // The catalog's own scaling function — shared, so the probe can never measure a
            // different game than the one that ships.
            var enemyIds = new List<int>();
            foreach (var e in factory(act).Enemies)
            {
                Encounters.Scale(e.Def, act, FightTier.Stable);
                enemyIds.Add(id);
                units.Add(UnitState.Spawn(id++, 1, e.Def, e.Pos));
            }

            var result = new Battle(units, seed: (ulong)(seed + 1)).Run();
            if (result.Winner == Winner.Team0) wins++;
            ticks += result.EndTick;
            if (RuleFired(factory(act).Id, result, enemyIds)) fired++;
        }

        return new ProbeParties.Outcome(
            100.0 * wins / SeedsPerArrangement,
            (double)ticks / SeedsPerArrangement,
            100.0 * fired / SeedsPerArrangement);
    }

    /// <summary>Did the encounter's authored mechanic actually happen? Keyed per encounter, because
    /// "the rule fired" means something different for a ward than for a ritual.</summary>
    private static bool RuleFired(string id, BattleResult result, List<int> enemyIds) => id switch
    {
        // The ward is real if it ever came OFF — i.e. an escort died and the wall became killable.
        "the-long-range" => result.Events.Any(e =>
            e.Kind == EventKind.StatusExpired && e.Aux == (int)StatusKind.DamageTakenDown),
        // The ritual is real if the Scribe ever completed a cast.
        "ninth-bell" => result.Events.Any(e => e.Kind == EventKind.Cast && enemyIds.Contains(e.Source)),
        // The ambush is real if the stalkers actually leapt.
        "the-drop" => result.Events.Any(e => e.Kind == EventKind.Leap && enemyIds.Contains(e.Source)),
        // The procession is real if the death-fed ritual actually completed a cast.
        "long-procession" => result.Events.Any(e => e.Kind == EventKind.Cast && enemyIds.Contains(e.Source)),
        // The swarm and the lane have no rule to fire — they are honest about that.
        _ => true,
    };
}
