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
/// </summary>
public static class EncounterProbe
{
    private const int SeedsPerArrangement = 24;

    /// <summary>Blue rows are 0-3. Named shapes, because a player thinks in shapes.</summary>
    private static readonly (string Name, Hex[] Slots)[] Formations =
    {
        ("default",    new[] { Hex.FromRowCol(3, 2), Hex.FromRowCol(1, 1), Hex.FromRowCol(1, 4) }),
        ("forward",    new[] { Hex.FromRowCol(3, 2), Hex.FromRowCol(3, 1), Hex.FromRowCol(3, 4) }),
        ("turtle",     new[] { Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 1), Hex.FromRowCol(0, 3) }),
        ("wall-first", new[] { Hex.FromRowCol(3, 3), Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 3) }),
        ("split",      new[] { Hex.FromRowCol(3, 0), Hex.FromRowCol(1, 2), Hex.FromRowCol(3, 5) }),
        ("back-line",  new[] { Hex.FromRowCol(2, 2), Hex.FromRowCol(0, 0), Hex.FromRowCol(0, 5) }),
    };

    /// <summary>
    /// A wall, a caster, a shooter — and it GROWS with the act, because that is the only honest
    /// comparison. Measuring a rank-B forked party against act-1 enemies says nothing about act 1:
    /// the player meets act 1 with three rank-C recruits and no fork at all.
    /// </summary>
    private static readonly (string Chassis, string Node)[] Party =
    {
        ("bulwark", "bulwark.juggernaut"),
        ("pyromancer", "pyromancer.inferno"),
        ("sharpshot", "sharpshot.volleyer"),
    };

    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("# Encounter probe — authored PvE node pool");
        report.AppendLine();
        report.AppendLine($"{Formations.Length} formations × {SeedsPerArrangement} seeds per act, " +
                          "vs an act-appropriate Bulwark/Pyromancer/Sharpshot party " +
                          "(rank C→A as the act climbs). Crit is the sim's only RNG, so seeds are the " +
                          "whole distribution.");
        report.AppendLine();
        report.AppendLine("**The bar is not win%.** It is the SPREAD: if every formation scores the " +
                          "same, the encounter poses no placement problem no matter what its rule says.");
        report.AppendLine();

        foreach (var factory in Encounters.NodePool)
        {
            var def = factory(1);
            report.AppendLine($"## {def.Name} — `{def.RuleName}`");
            report.AppendLine();
            report.AppendLine($"> {def.Pressure}");
            report.AppendLine();
            report.AppendLine("| act | best formation | worst formation | spread | avg ticks | rule fired |");
            report.AppendLine("|---|---|---|---|---|---|");

            for (int act = 1; act <= 3; act++)
            {
                var rows = Formations
                    .Select(f => (f.Name, Result: Measure(factory, act, f.Slots)))
                    .OrderByDescending(r => r.Result.WinPct)
                    .ToList();
                var best = rows.First();
                var worst = rows.Last();
                double spread = best.Result.WinPct - worst.Result.WinPct;
                double firedPct = rows.Average(r => r.Result.RuleFiredPct);
                report.AppendLine($"| {act} | {best.Name} {best.Result.WinPct:F0}% | " +
                                  $"{worst.Name} {worst.Result.WinPct:F0}% | **{spread:F0}** | " +
                                  $"{rows.Average(r => r.Result.AvgTicks):F0} | {firedPct:F0}% |");
            }

            report.AppendLine();
            // Judge an encounter at the FIRST act it can actually appear in — an act-2 encounter
            // measured against a rank-C opening warband is a fact about the pool, not a flaw.
            int debutAct = Enumerable.Range(1, 3)
                .First(a => Encounters.PoolFor(a).Any(f => f(a).Id == def.Id));
            var debut = Formations.Select(f => Measure(factory, debutAct, f.Slots).WinPct).ToList();
            report.AppendLine($"> Debuts in act {debutAct}. " +
                              Verdict(def, debut.Max(), debut.Max() - debut.Min()).Substring(2));
            report.AppendLine();
        }

        // ---- the naive line: can the bot's fixed comp survive the pool at all? ----------
        // The integration smoke test plays Cleric/Bulwark/Shade with front/back placement and no
        // draft choice. It is the WEAKEST legal answer, so it is the floor check: an act-1 pool a
        // legal comp cannot clear is prescribing a build, which the encounter law forbids.
        report.AppendLine("## The naive line (bot: fixed comp, default placement)");
        report.AppendLine();
        var reports = RunHarness.PlayMany(12, seedBase: 4000, new Catalog());
        int victories = reports.Count(r => r.Final.Phase == RunPhase.Complete);
        report.AppendLine($"- Runs completed: **{victories}/12**");
        var deaths = reports.Where(r => r.Final.Phase != RunPhase.Complete)
            .Select(r => r.Fights.LastOrDefault())
            .Where(f => f != null)
            .GroupBy(f => $"act {f!.Act} node {f.Node}{(f.IsBoss ? " (boss)" : "")}")
            .OrderByDescending(g => g.Count());
        foreach (var g in deaths)
            report.AppendLine($"- died at {g.Key}: {g.Count()}×");
        report.AppendLine();

        Console.WriteLine(report.ToString());
    }

    private static string Verdict(EncounterDef def, double bestWin, double spread)
    {
        if (bestWin < 20) return "> **UNSURVIVABLE at act 1** — no formation clears it. Stats, not design.";
        if (bestWin > 95 && spread < 10) return "> **FREE** — every formation wins. It poses nothing yet.";
        if (spread < 15) return "> **FLAT** — winnable, but placement barely moves the result.";
        return $"> **POSES A PROBLEM** — placement swings the result by {spread:F0} points.";
    }

    private readonly struct Outcome
    {
        public readonly double WinPct, AvgTicks, RuleFiredPct;
        public Outcome(double win, double ticks, double fired)
        { WinPct = win; AvgTicks = ticks; RuleFiredPct = fired; }
    }

    private static Outcome Measure(Func<int, EncounterDef> factory, int act, Hex[] slots)
    {
        int wins = 0, fired = 0;
        long ticks = 0;

        for (int seed = 0; seed < SeedsPerArrangement; seed++)
        {
            var units = new List<UnitState>();
            int id = 0;
            for (int i = 0; i < Party.Length; i++)
            {
                var (chassis, node) = Party[i];
                // Act 1 = rank C, unforked. Act 2 = rank B with its fork. Act 3 = rank A.
                var nodes = act >= 2 ? new[] { Kits.Nodes[node] } : System.Array.Empty<SpecNode>();
                var composed = Loadout.Compose(
                    Kits.Chassis[chassis],
                    nodes: nodes,
                    mastered: true,
                    rankSteps: act - 1);
                units.Add(Loadout.Spawn(id++, 0, composed, slots[i]));
            }

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

        return new Outcome(
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
        // The swarm has no rule to fire — it is honest about that.
        _ => true,
    };
}
