using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warband.Content;
using Warband.Sim;

/// <summary>
/// The Last Oath what-if probe (board item 5 gate). It answers ONE machine question ahead of
/// Jake's playtest: does the Bond pose a decision at all?
///
/// It does NOT answer whether the decision feels good — that is the playtest, and this exists
/// to make that playtest cheaper, not to replace it. Same bar as the outlier sweep: NAME what
/// is there, tune nothing.
///
/// Three ways the encounter could be posing no decision, each measured separately:
///   1. Enrage never fires before the fight ends       → the Bond is invisible.
///   2. Enrage fires with the fight already over       → the Bond is cosmetic.
///   3. Every arrangement wins anyway                  → the choice is free.
/// </summary>
public static class OathProbe
{
    private const int SeedsPerArrangement = 40;

    /// <summary>Named formations, not random placements — a player thinks in shapes, and a
    /// report keyed to shapes is one they can act on. Blue rows are 0-2 (enemies sit at 5-6).</summary>
    private static readonly (string Name, Hex[] Slots)[] Formations =
    {
        ("default",     new[] { Hex.FromRowCol(2, 2), Hex.FromRowCol(0, 1), Hex.FromRowCol(0, 4) }),
        ("forward",     new[] { Hex.FromRowCol(2, 2), Hex.FromRowCol(2, 1), Hex.FromRowCol(2, 4) }),
        ("turtle",      new[] { Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 1), Hex.FromRowCol(0, 3) }),
        ("wall-first",  new[] { Hex.FromRowCol(2, 3), Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 3) }),
        ("split",       new[] { Hex.FromRowCol(2, 0), Hex.FromRowCol(1, 2), Hex.FromRowCol(2, 5) }),
        ("left-stack",  new[] { Hex.FromRowCol(2, 0), Hex.FromRowCol(1, 0), Hex.FromRowCol(0, 1) }),
        ("right-stack", new[] { Hex.FromRowCol(2, 5), Hex.FromRowCol(1, 5), Hex.FromRowCol(0, 4) }),
        ("bait-left",   new[] { Hex.FromRowCol(2, 0), Hex.FromRowCol(0, 4), Hex.FromRowCol(0, 5) }),
    };

    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("# The Last Oath — what-if probe");
        report.AppendLine();
        report.AppendLine($"{Formations.Length} formations × lineups × {SeedsPerArrangement} seeds. " +
                          "Crit is the sim's only RNG, so seeds are the whole distribution.");
        report.AppendLine();

        // Every legal lineup: FieldCapacity of the roster, in every combination.
        var roster = SkirmishProof.Heroes.Select(h => h.HeroId).ToList();
        var lineups = Combinations(roster, SkirmishProof.FieldCapacity).ToList();

        var rows = new List<Row>();
        foreach (var lineup in lineups)
            foreach (var (fname, slots) in Formations)
                rows.Add(Probe(lineup, fname, slots));

        // ---- 1. outcome spread: is the choice free? ------------------------------------
        report.AppendLine("## Does the choice change the outcome?");
        report.AppendLine();
        report.AppendLine("| lineup | formation | win% | loss% | draw% | avg ticks | enrage% | ticks after | swings after |");
        report.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in rows.OrderByDescending(r => r.WinPct).ThenBy(r => r.DrawPct))
            report.AppendLine($"| {r.Lineup} | {r.Formation} | {r.WinPct:F0}% | {r.LossPct:F0}% | {r.DrawPct:F0}% | " +
                              $"{r.AvgTicks:F0} | {r.EnragePct:F0}% | {r.AvgTicksAfterEnrage:F1} | {r.AvgSwingsAfterEnrage:F1} |");
        report.AppendLine();

        // A 0%/100% split with no middle means crit variance never changes the result — worth
        // saying out loud, because it means one playthrough per arrangement IS the whole story.
        bool binary = rows.All(r => r.WinPct == 0 || r.WinPct == 100);
        if (binary)
            report.AppendLine($"**Every arrangement is seed-invariant** — {SeedsPerArrangement} seeds, " +
                              "no arrangement ever split. Crit variance does not decide this fight; " +
                              "one playthrough per arrangement is the whole distribution.");
        report.AppendLine();

        double best = rows.Max(r => r.WinPct), worst = rows.Min(r => r.WinPct);
        report.AppendLine($"**Win-rate spread: {worst:F0}% – {best:F0}% (Δ{best - worst:F0}).**");
        report.AppendLine(best - worst < 10
            ? "> FLAT — every arrangement resolves the same. On this evidence the encounter is not "
              + "posing a placement decision; the fight is being decided by something other than the player's choice."
            : "> The arrangement matters. The spread is the size of the decision the player is actually making.");
        report.AppendLine();

        // ---- 2. is the Bond even visible? ---------------------------------------------
        var fired = rows.Where(r => r.EnragePct > 0).ToList();
        double overallEnrage = rows.Average(r => r.EnragePct);
        double avgAfter = fired.Count == 0 ? 0 : fired.Average(r => r.AvgTicksAfterEnrage);
        double avgSwings = fired.Count == 0 ? 0 : fired.Average(r => r.AvgSwingsAfterEnrage);

        report.AppendLine("## Is the Bond real?");
        report.AppendLine();
        report.AppendLine($"- Enrage fires in **{overallEnrage:F0}%** of fights.");
        report.AppendLine($"- When it fires, the fight runs **{avgAfter:F1} more ticks** and the survivor " +
                          $"throws **{avgSwings:F1} more swings**.");
        report.AppendLine();
        if (overallEnrage < 50)
            report.AppendLine("> INVISIBLE — the fight usually ends before the Bond can happen at all.");
        else if (avgSwings < 3)
            report.AppendLine("> COSMETIC — Enrage fires, but the survivor barely swings again. " +
                              "+100% Attack Speed on a unit that gets 1-2 more swings is a status burst, not a threat.");
        else
            report.AppendLine("> LIVE — the survivor gets real swings out of the Enrage. " +
                              "The 'which one do I leave standing' question has teeth.");
        report.AppendLine();

        // ---- 3. which one you leave standing -------------------------------------------
        report.AppendLine("## Which Oathbound is worse to leave enraged?");
        report.AppendLine();
        var survivorGroups = rows.SelectMany(r => r.BySurvivor).GroupBy(x => x.Role)
                                 .OrderBy(g => g.Key).ToList();
        report.AppendLine("| survivor | fights | win% |");
        report.AppendLine("|---|---|---|");
        foreach (var g in survivorGroups)
            report.AppendLine($"| {g.Key} | {g.Count()} | {100.0 * g.Count(x => x.Won) / g.Count():F0}% |");
        report.AppendLine();

        var never = Encounters.BondedPair().Enemies
            .Select(e => e.Def.ChassisId)
            .Where(id => survivorGroups.All(g => g.Key != id))
            .ToList();
        if (never.Count > 0)
            report.AppendLine($"> **THE CHOICE DOES NOT EXIST.** `{string.Join("`, `", never)}` never survives " +
                              "its partner in any arrangement — it always dies first, so the player always faces " +
                              "the same enraged threat. The encounter's own pitch (*\"choose which threat you are " +
                              "willing to leave enraged\"*) is not yet expressible: there is only one outcome to choose.");
        else if (survivorGroups.Count > 1)
        {
            double spread = survivorGroups.Max(g => 100.0 * g.Count(x => x.Won) / g.Count())
                          - survivorGroups.Min(g => 100.0 * g.Count(x => x.Won) / g.Count());
            report.AppendLine(spread < 10
                ? $"> Both survivors cost about the same (Δ{spread:F0}) — the choice exists but carries no weight yet."
                : $"> Leaving one alive costs Δ{spread:F0} more than the other. That gap IS the decision.");
        }

        Console.Write(report.ToString());
    }

    private readonly struct Outcome
    {
        public readonly string Role;
        public readonly bool Won;
        public Outcome(string role, bool won) { Role = role; Won = won; }
    }

    private sealed class Row
    {
        public string Lineup = "", Formation = "";
        public double WinPct, LossPct, DrawPct, AvgTicks, EnragePct, AvgTicksAfterEnrage, AvgSwingsAfterEnrage;
        public List<Outcome> BySurvivor = new List<Outcome>();
    }

    private static Row Probe(List<string> lineup, string formationName, Hex[] slots)
    {
        int wins = 0, losses = 0, draws = 0, enrages = 0;
        long ticks = 0, ticksAfter = 0, swingsAfter = 0;
        var bySurvivor = new List<Outcome>();

        for (int seed = 0; seed < SeedsPerArrangement; seed++)
        {
            var encounter = Encounters.BondedPair();
            var units = new List<UnitState>();

            for (int i = 0; i < lineup.Count; i++)
            {
                var chassis = Kits.Chassis[lineup[i]];
                var composed = Loadout.Compose(chassis, mastered: true);
                units.Add(Loadout.Spawn(i, 0, composed, slots[i]));
            }

            var enemyIds = new List<int>();
            for (int i = 0; i < encounter.Enemies.Count; i++)
            {
                units.Add(UnitState.Spawn(100 + i, 1, encounter.Enemies[i].Def, encounter.Enemies[i].Pos));
                enemyIds.Add(100 + i);
            }

            var result = new Battle(units, seed: (ulong)seed).Run();
            bool won = result.Winner == Winner.Team0;
            if (won) wins++;
            else if (result.Winner == Winner.Team1) losses++;
            else draws++;
            ticks += result.EndTick;

            var bond = Encounters.ReadBond(result, enemyIds);
            if (bond.EnrageFired)
            {
                enrages++;
                ticksAfter += bond.TicksAfterEnrage;
                swingsAfter += bond.SurvivorSwingsAfterEnrage;
                int idx = enemyIds.IndexOf(bond.SurvivorId);
                bySurvivor.Add(new Outcome(encounter.Enemies[idx].Def.ChassisId, won));
            }
        }

        return new Row
        {
            Lineup = string.Join("+", lineup.Select(l => l.Substring(0, Math.Min(4, l.Length)))),
            Formation = formationName,
            WinPct = 100.0 * wins / SeedsPerArrangement,
            LossPct = 100.0 * losses / SeedsPerArrangement,
            DrawPct = 100.0 * draws / SeedsPerArrangement,
            AvgTicks = (double)ticks / SeedsPerArrangement,
            EnragePct = 100.0 * enrages / SeedsPerArrangement,
            AvgTicksAfterEnrage = enrages == 0 ? 0 : (double)ticksAfter / enrages,
            AvgSwingsAfterEnrage = enrages == 0 ? 0 : (double)swingsAfter / enrages,
            BySurvivor = bySurvivor,
        };
    }

    private static IEnumerable<List<string>> Combinations(List<string> pool, int take)
    {
        if (take == 0) { yield return new List<string>(); yield break; }
        for (int i = 0; i <= pool.Count - take; i++)
            foreach (var rest in Combinations(pool.GetRange(i + 1, pool.Count - i - 1), take - 1))
            {
                var combo = new List<string> { pool[i] };
                combo.AddRange(rest);
                yield return combo;
            }
    }
}
