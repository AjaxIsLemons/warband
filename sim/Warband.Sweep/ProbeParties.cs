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
        // 8-wide remap (ADR 0027): centered shapes translated +1, edge shapes re-anchored.
        ("default",    new[] { Hex.FromRowCol(3, 3), Hex.FromRowCol(1, 2), Hex.FromRowCol(1, 5), Hex.FromRowCol(0, 3) }),
        ("forward",    new[] { Hex.FromRowCol(3, 3), Hex.FromRowCol(3, 2), Hex.FromRowCol(3, 5), Hex.FromRowCol(2, 3) }),
        ("turtle",     new[] { Hex.FromRowCol(0, 3), Hex.FromRowCol(0, 2), Hex.FromRowCol(0, 4), Hex.FromRowCol(1, 3) }),
        ("wall-first", new[] { Hex.FromRowCol(3, 4), Hex.FromRowCol(0, 3), Hex.FromRowCol(0, 4), Hex.FromRowCol(1, 4) }),
        ("split",      new[] { Hex.FromRowCol(3, 0), Hex.FromRowCol(1, 3), Hex.FromRowCol(3, 7), Hex.FromRowCol(0, 0) }),
        ("back-line",  new[] { Hex.FromRowCol(2, 3), Hex.FromRowCol(0, 0), Hex.FromRowCol(0, 7), Hex.FromRowCol(1, 7) }),
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
            // Item 32 / ADR 0031: Banneret is already named CHASSIS-DEAD by the build sweep.
            // Including it made every act-1 "control failed" verdict a Banneret verdict. A second
            // Warden is legal draft content and keeps this axis honestly about Stun/Silence/Taunt.
            ("bulwark", "bulwark.warden"),
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
    /// Axes built from CANDIDATE content (Kits.CandidateNodes) — appended only when
    /// <see cref="IncludeCandidates"/> is set, so the default `--enc` report stays byte-identical
    /// and every number already committed to the vault stays comparable.
    ///
    /// `focus` is deliberately `damage` with one hero swapped (pyromancer → Spotter), because the
    /// question is not "does a Spotter party win" — it is "is a Spotter worth a body". At act 1 the
    /// probe takes the first three of each, so the two columns differ by exactly that one hero.
    /// The rest are high-output bodies on purpose: DamageTakenUp multiplies whatever the party
    /// already deals, so a support path is only legible beside allies who can spend it.
    /// </summary>
    public static readonly (string Axis, (string Chassis, string Node)[] Party)[] CandidateAxes =
    {
        ("focus", new[]
        {
            ("sharpshot", "sharpshot.spotter"),
            ("berserker", "berserker.bloodreaver"),
            ("shade", "shade.reaper"),
            ("bulwark", "bulwark.juggernaut"),
        }),
    };

    /// <summary>Set from `--candidates`. Off by default: the safe failure is measuring less.</summary>
    public static bool IncludeCandidates;

    private static (string Axis, (string Chassis, string Node)[] Party)[]? _active;

    /// <summary>The axes a probe actually measures. Read this, never <see cref="Axes"/>, so the
    /// verdict thresholds ("every axis passes") count the columns that were really run.</summary>
    public static (string Axis, (string Chassis, string Node)[] Party)[] Active =>
        _active ??= IncludeCandidates ? Axes.Concat(CandidateAxes).ToArray() : Axes;

    /// <summary>
    /// Party size follows the run's own capacity curve. Measuring an act-1 fight against a
    /// four-hero warband describes a game nobody plays: the player meets act 1 with the three they
    /// drafted.
    ///
    /// **This is the single most powerful difficulty dial in the game, and it is not a stat.**
    /// Measured 2026-07-26: The Long Range at act 2 admits 3 answers with a spread of 100 against
    /// three heroes and is FREE from every formation against four. One extra body deleted the
    /// sharpest encounter in the pool. Anything read off this probe is conditional on this number,
    /// which is why the report prints it in every table.
    ///
    /// **Known conservatism.** `RunController` unlocks `3 + act` slots (capped at 6) after each
    /// act's Interlude, so a player who takes every offer fields up to 4/5/6, and only the
    /// pre-Interlude part of an act matches `act + 2`. This models the player who is one offer
    /// behind, and it is additionally capped at 4 by the size of the authored axis parties. Widening
    /// the axes to 6 heroes would move every act-2/3 number again, so it is a deliberate later
    /// decision, not an oversight.
    /// </summary>
    public static int SizeAt(int act) => Math.Min(act + 2, Axes[0].Party.Length);

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
            // Resolves candidates too, so a proposed path can be probed against the authored
            // encounters without first being promoted into the live offer table.
            var nodes = act >= 2
                ? new[] { Kits.Nodes.TryGetValue(node, out var live) ? live : Kits.CandidateNodes[node] }
                : Array.Empty<SpecNode>();
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
        // OrderByDescending is a documented STABLE sort, which is the tie-break: equal formations
        // stay in declaration order, so a fight everything wins reports `default` as its best
        // rather than whichever name happens to sort first.
        var rows = Formations
            .Select(f => (f.Name, Result: measure(f.Slots)))
            .OrderByDescending(r => r.Result.WinPct)
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
