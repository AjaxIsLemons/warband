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

    /// <summary>Blue rows are 0-3. Same named shapes as `--enc`, so the two reports compare.</summary>
    private static readonly (string Name, Hex[] Slots)[] Formations =
    {
        ("default",    new[] { Hex.FromRowCol(3, 2), Hex.FromRowCol(1, 1), Hex.FromRowCol(1, 4), Hex.FromRowCol(0, 2) }),
        ("forward",    new[] { Hex.FromRowCol(3, 2), Hex.FromRowCol(3, 1), Hex.FromRowCol(3, 4), Hex.FromRowCol(2, 2) }),
        ("turtle",     new[] { Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 1), Hex.FromRowCol(0, 3), Hex.FromRowCol(1, 2) }),
        ("wall-first", new[] { Hex.FromRowCol(3, 3), Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 3), Hex.FromRowCol(1, 3) }),
        ("split",      new[] { Hex.FromRowCol(3, 0), Hex.FromRowCol(1, 2), Hex.FromRowCol(3, 5), Hex.FromRowCol(0, 0) }),
        ("back-line",  new[] { Hex.FromRowCol(2, 2), Hex.FromRowCol(0, 0), Hex.FromRowCol(0, 5), Hex.FromRowCol(1, 5) }),
    };

    /// <summary>
    /// Four parties, four kinds of strength. Each is a legal, unremarkable build a player could
    /// plausibly own at that act — NOT an optimised solution. If a boss can only be answered by one
    /// column, the report says so and the author has a decision to make.
    /// </summary>
    private static readonly (string Axis, (string Chassis, string Node)[] Party)[] Answers =
    {
        ("balanced", new[]
        {
            ("bulwark", "bulwark.juggernaut"),
            ("pyromancer", "pyromancer.inferno"),
            ("sharpshot", "sharpshot.volleyer"),
            ("cleric", "cleric.lifebinder"),
        }),
        ("reach", new[]
        {
            ("sharpshot", "sharpshot.sniper"),
            ("sharpshot", "sharpshot.volleyer"),
            ("shade", "shade.phantom"),
            ("bulwark", "bulwark.warden"),
        }),
        ("control", new[]
        {
            ("bulwark", "bulwark.warden"),
            ("phalanx", "phalanx.pikewall"),
            ("banneret", "banneret.warcaller"),
            ("cleric", "cleric.lifebinder"),
        }),
        ("damage", new[]
        {
            ("berserker", "berserker.bloodreaver"),
            ("shade", "shade.reaper"),
            ("pyromancer", "pyromancer.inferno"),
            ("bulwark", "bulwark.juggernaut"),
        }),
    };

    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("# Boss probe — the act bosses as strength exams");
        report.AppendLine();
        report.AppendLine($"{Formations.Length} formations × {SeedsPerArrangement} seeds, four " +
                          "answer-axis parties, each sized to its act (rank C→A, forked from act 2). " +
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

            var axisBest = new List<(string Axis, double Win, double Spread)>();
            foreach (var (axis, party) in Answers)
            {
                var rows = Formations
                    .Select(f => (f.Name, Result: Measure(act, party, f.Slots)))
                    .OrderByDescending(r => r.Result.WinPct)
                    .ToList();
                var best = rows.First();
                var worst = rows.Last();
                double spread = best.Result.WinPct - worst.Result.WinPct;
                axisBest.Add((axis, best.Result.WinPct, spread));
                report.AppendLine($"| {axis} | {best.Name} {best.Result.WinPct:F0}% | " +
                                  $"{worst.Name} {worst.Result.WinPct:F0}% | **{spread:F0}** | " +
                                  $"{rows.Average(r => r.Result.AvgTicks):F0} | " +
                                  $"{rows.Average(r => r.Result.RuleFiredPct):F0}% |");
            }

            report.AppendLine();
            foreach (string line in Verdict(axisBest))
                report.AppendLine("> " + line);
            report.AppendLine();
        }

        Console.WriteLine(report.ToString());
    }

    /// <summary>The three bars, stated as findings rather than as a pass/fail gate — an author
    /// reads these, a build system does not.</summary>
    private static IEnumerable<string> Verdict(List<(string Axis, double Win, double Spread)> axes)
    {
        // A "pass" is a real answer, not a coin flip: the axis clears it more often than not from
        // its best formation. Anything between is named as marginal rather than rounded away.
        var passing = axes.Where(a => a.Win >= 55).Select(a => a.Axis).ToList();
        var marginal = axes.Where(a => a.Win >= 30 && a.Win < 55).Select(a => a.Axis).ToList();
        double bestSpread = axes.Max(a => a.Spread);
        double bestWin = axes.Max(a => a.Win);

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

    private readonly struct Outcome
    {
        public readonly double WinPct, AvgTicks, RuleFiredPct;
        public Outcome(double win, double ticks, double fired)
        { WinPct = win; AvgTicks = ticks; RuleFiredPct = fired; }
    }

    private static Outcome Measure(int act, (string Chassis, string Node)[] party, Hex[] slots)
    {
        int wins = 0, fired = 0;
        long ticks = 0;

        for (int seed = 0; seed < SeedsPerArrangement; seed++)
        {
            var units = new List<UnitState>();
            int id = 0;
            // Party size follows the run's own capacity curve (ADR 0019: 3 → 6 across the run), not
            // a fixed four. Measuring the act-1 boss against a four-hero warband describes a game
            // nobody plays: the player meets it with the three they drafted.
            int size = Math.Min(Math.Min(act + 2, party.Length), slots.Length);
            for (int i = 0; i < size; i++)
            {
                var (chassis, node) = party[i];
                var nodes = act >= 2 ? new[] { Kits.Nodes[node] } : Array.Empty<SpecNode>();
                var composed = Loadout.Compose(
                    Kits.Chassis[chassis], nodes: nodes, mastered: true, rankSteps: act - 1);
                units.Add(Loadout.Spawn(id++, 0, composed, slots[i]));
            }

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

        return new Outcome(
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
