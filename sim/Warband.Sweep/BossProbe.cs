using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// The act-boss authoring instrument (`--boss`). Same bar as `--enc` and the oath probe:
/// **NAME what is there, tune nothing.**
///
/// A boss is held to a harder standard than a node encounter, because `pve-encounters.md` says a
/// boss is a **strength exam**: one defining pressure an ordinary warband cannot absorb, which
/// **several qualitatively different strong answers** can overcome. So this probe measures three
/// things a node probe does not:
///
///   1. **Is it an exam at all?** A boss the act-appropriate party beats from every formation with
///      a stock build is not a wall, it is a corridor.
///   2. **Does placement still matter?** Same spread test as `--enc`. A boss that ignores where you
///      stood has thrown away the only order the player gets to give.
///   3. **Does it admit more than one answer?** The same boss is re-run against four deliberately
///      DIFFERENT parties — reach, control, sustain, raw damage. A boss that only one of them
///      clears is prescribing a build, which the encounter law forbids; a boss none of them clear
///      is a stat wall.
///
/// The answer axes are the honest version of "multiple strong answers". They are not balanced
/// against each other and are not meant to be — they exist so the report can say *which* kinds of
/// strength the encounter actually rewards.
/// </summary>
public static class BossProbe
{
    private const int SeedsPerArrangement = 24;

    // Formations, answer axes and the party-size curve are shared with `--enc`
    // (ProbeParties) so the two instruments can never describe two different players.

    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("# Boss probe — the act bosses as strength exams");
        report.AppendLine();
        report.AppendLine($"{ProbeParties.Formations.Length} formations × {SeedsPerArrangement} seeds, " +
                          $"{ProbeParties.Axes.Length} answer-axis parties, each sized to its act " +
                          "(rank C→A, forked from act 2). " +
                          "Crit is the sim's only RNG, so seeds are the whole distribution.");
        report.AppendLine();
        report.AppendLine("**Three bars, not one.** ① it must be an exam (not free) · ② placement " +
                          "must still move the result · ③ **more than one axis must be able to " +
                          "pass it**, or the boss is prescribing a build (pve-encounters.md).");
        report.AppendLine();

        for (int act = 1; act <= Encounters.BossPool.Length; act++)
        {
            var def = Encounters.BossFor(act);
            report.AppendLine($"## Act {act} — {def.Name} · `{def.RuleName}`");
            report.AppendLine();
            report.AppendLine($"> {def.Pressure}");
            report.AppendLine();
            report.AppendLine($"{def.Enemies.Count} bodies: " +
                              string.Join(", ", def.Enemies
                                  .GroupBy(e => e.Def.Name)
                                  .Select(g => g.Count() > 1 ? $"{g.Count()}× {g.Key}" : g.Key)));
            report.AppendLine();
            report.AppendLine("| answer axis | best formation | worst formation | spread | avg ticks | rule fired |");
            report.AppendLine("|---|---|---|---|---|---|");

            var axisBest = Collect(act);
            foreach (var a in axisBest)
                report.AppendLine($"| {a.Axis} | {a.BestFormation} {a.BestWin:F0}% | " +
                                  $"{a.WorstFormation} {a.WorstWin:F0}% | **{a.Spread:F0}** | " +
                                  $"{a.AvgTicks:F0} | {a.RuleFiredPct:F0}% |");

            report.AppendLine();
            foreach (string line in Verdict(axisBest))
                report.AppendLine("> " + line);
            report.AppendLine();
        }

        Console.WriteLine(report.ToString());
    }

    /// <summary>Every axis measured against one act's boss, once — so the markdown report and the
    /// committed baseline render the same numbers instead of re-running the fights.</summary>
    public static List<ProbeParties.AxisResult> Collect(int act) => ProbeParties.Axes
        .Select(a => ProbeParties.Across(a.Axis, slots => Measure(act, a.Party, slots)))
        .ToList();

    /// <summary>The three bars, stated as findings rather than as a pass/fail gate — an author
    /// reads these, a build system does not.</summary>
    public static IEnumerable<string> Verdict(List<ProbeParties.AxisResult> axes)
    {
        var (passing, marginal, bestSpread, bestWin) = ProbeParties.Summarise(axes);

        if (bestWin < 20)
            yield return "**STAT WALL** — no axis clears it from any formation. That is numbers, not design.";
        else if (passing.Count == 0)
            yield return $"**PUNISHING** — nothing passes cleanly; best axis peaks at {bestWin:F0}%. " +
                         (marginal.Count > 0 ? $"Marginal: {string.Join(", ", marginal)}." : "");
        else if (passing.Count == 1)
            yield return $"**PRESCRIBES A BUILD** — only `{passing[0]}` clears it. " +
                         "pve-encounters.md: an exam must admit several qualitatively different answers.";
        else
            yield return $"**ADMITS {passing.Count} ANSWERS** — {string.Join(", ", passing)} all clear it" +
                         (marginal.Count > 0 ? $"; {string.Join(", ", marginal)} marginal." : ".");

        if (bestSpread < 15)
            yield return $"**FLAT** — placement moves the result by at most {bestSpread:F0} points. " +
                         "Placement is the only order the player gives; this boss ignores it.";
        else
            yield return $"Placement swings the result by up to {bestSpread:F0} points.";
    }

    private static ProbeParties.Outcome Measure(int act, (string Chassis, string Node)[] party, Hex[] slots)
    {
        int wins = 0, fired = 0;
        long ticks = 0;

        for (int seed = 0; seed < SeedsPerArrangement; seed++)
        {
            var units = new List<UnitState>();
            int id = ProbeParties.Field(units, act, party, slots);

            // The catalog's OWN boss comp, scaling included — the probe must never measure a
            // different boss than the one that ships.
            var enemyIds = new List<int>();
            var def = Encounters.BossFor(act);
            int pct = Encounters.BossScalePct(act);
            foreach (var e in def.Enemies)
            {
                if (pct != 100)
                {
                    e.Def.MaxHp = e.Def.MaxHp * pct / 100;
                    e.Def.Attack = e.Def.Attack * pct / 100;
                }
                enemyIds.Add(id);
                units.Add(UnitState.Spawn(id++, 1, e.Def, e.Pos));
            }

            var result = new Battle(units, seed: (ulong)(seed + 1)).Run();
            if (result.Winner == Winner.Team0) wins++;
            ticks += result.EndTick;
            if (RuleFired(def.Id, result, enemyIds)) fired++;
        }

        return new ProbeParties.Outcome(
            100.0 * wins / SeedsPerArrangement,
            (double)ticks / SeedsPerArrangement,
            100.0 * fired / SeedsPerArrangement);
    }

    /// <summary>Did the boss's authored mechanic actually happen? A boss whose rule never fires is
    /// a stat block wearing rule text.</summary>
    private static bool RuleFired(string id, BattleResult result, List<int> enemyIds) => id switch
    {
        // BOND: the survivor actually Enraged.
        "bonded-pair" => result.Events.Any(e =>
            e.Kind == EventKind.StatusApplied && e.Aux == (int)StatusKind.Haste
            && e.Amount == Encounters.BondHaste && enemyIds.Contains(e.Target)),
        // BATTERY / WANING: the clock actually reached full and rang at least once.
        "ashfall-battery" => result.Events.Any(e => e.Kind == EventKind.Cast && enemyIds.Contains(e.Source)),
        "waning-crown" => result.Events.Any(e => e.Kind == EventKind.Cast && enemyIds.Contains(e.Source)),
        _ => true,
    };
}
