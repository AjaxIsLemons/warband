using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// The player side of every authoring probe, defined ONCE (2026-07-26).
///
/// `--enc` used to measure node encounters against a single hard-coded party while `--boss`
/// measured bosses against four. That made half the encounter report untrustworthy in a way the
/// report itself could not show: an encounter that is flat for a bulwark/pyro/sharpshot warband and
/// sharp for a control warband reported as "poses nothing", and the author had no way to know. It
/// also meant the two instruments were quietly describing two different games — the exact drift
/// `Encounters.Scale` exists to prevent on the enemy side.
///
/// So formations, answer axes and the party-size curve all live here, and both probes read them.
/// </summary>
public static class ProbeParties
{
    /// <summary>
    /// Blue rows are 0-3. Named shapes, because a player thinks in shapes. Four slots; a probe
    /// takes the first <see cref="Size"/> of them, so the three-hero and four-hero versions of a
    /// formation are the same shape rather than two unrelated arrangements.
    /// </summary>
    public static readonly (string Name, Hex[] Slots)[] Formations =
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
    /// plausibly own at that act — NOT an optimised solution. If an encounter can only be answered
    /// by one column, the report says so and the author has a decision to make.
    ///
    /// **Significance-ordered.** A probe truncates to the act's capacity, so the heroes that carry
    /// an axis's identity come first: dropping the last entry must never turn `control` into
    /// something else. `balanced`'s first three are deliberately the party `--enc` measured against
    /// for its whole life, so act-1 numbers stay comparable with every figure already in the vault.
    /// </summary>
    public static readonly (string Axis, (string Chassis, string Node)[] Party)[] Axes =
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

    /// <summary>
    /// Party size follows the run's own capacity curve (ADR 0019 / RunConfig: 3 field slots at the
    /// start). Measuring an act-1 fight against a four-hero warband describes a game nobody plays:
    /// the player meets act 1 with the three they drafted.
    /// </summary>
    public static int Size(int act, (string Chassis, string Node)[] party, Hex[] slots) =>
        Math.Min(Math.Min(act + 2, party.Length), slots.Length);

    /// <summary>
    /// Compose and place the player side: act-appropriate rank (C→A) and forks only from act 2,
    /// exactly as the run hands them over. Returns the next free unit id.
    /// </summary>
    public static int Field(List<UnitState> units, int act,
                            (string Chassis, string Node)[] party, Hex[] slots, int id = 0)
    {
        int size = Size(act, party, slots);
        for (int i = 0; i < size; i++)
        {
            var (chassis, node) = party[i];
            var nodes = act >= 2 ? new[] { Kits.Nodes[node] } : Array.Empty<SpecNode>();
            var composed = Loadout.Compose(
                Kits.Chassis[chassis], nodes: nodes, mastered: true, rankSteps: act - 1);
            units.Add(Loadout.Spawn(id++, 0, composed, slots[i]));
        }
        return id;
    }

    /// <summary>One arrangement's result. Shared so the markdown report and the committed
    /// baseline can never render different numbers from the same run.</summary>
    public readonly struct Outcome
    {
        public readonly double WinPct, AvgTicks, RuleFiredPct;
        public Outcome(double win, double ticks, double fired)
        { WinPct = win; AvgTicks = ticks; RuleFiredPct = fired; }
    }

    /// <summary>How one axis did across every formation: its best and worst showing, and the
    /// spread between them — which is the number that says whether placement mattered.</summary>
    public readonly struct AxisResult
    {
        public readonly string Axis, BestFormation, WorstFormation;
        public readonly double BestWin, WorstWin, AvgTicks, RuleFiredPct;
        public double Spread => BestWin - WorstWin;

        public AxisResult(string axis, string bestF, double bestWin, string worstF, double worstWin,
                          double avgTicks, double ruleFired)
        {
            Axis = axis; BestFormation = bestF; BestWin = bestWin;
            WorstFormation = worstF; WorstWin = worstWin;
            AvgTicks = avgTicks; RuleFiredPct = ruleFired;
        }
    }

    /// <summary>Run one axis against every formation and reduce to best/worst/spread.</summary>
    public static AxisResult Across(string axis, Func<Hex[], Outcome> measure)
    {
        var rows = Formations
            .Select(f => (f.Name, Result: measure(f.Slots)))
            .OrderByDescending(r => r.Result.WinPct)
            .ThenBy(r => r.Name, StringComparer.Ordinal)   // deterministic ties
            .ToList();
        return new AxisResult(
            axis, rows.First().Name, rows.First().Result.WinPct,
            rows.Last().Name, rows.Last().Result.WinPct,
            rows.Average(r => r.Result.AvgTicks),
            rows.Average(r => r.Result.RuleFiredPct));
    }

    /// <summary>An axis "passes" if it clears the fight more often than not from its BEST
    /// formation — a real answer, not a coin flip. Anything between is named marginal rather
    /// than rounded away.</summary>
    public static (List<string> Passing, List<string> Marginal, double BestSpread, double BestWin)
        Summarise(IEnumerable<AxisResult> axes)
    {
        var list = axes.ToList();
        return (list.Where(a => a.BestWin >= 55).Select(a => a.Axis).ToList(),
                list.Where(a => a.BestWin >= 30 && a.BestWin < 55).Select(a => a.Axis).ToList(),
                list.Max(a => a.Spread),
                list.Max(a => a.BestWin));
    }
}
