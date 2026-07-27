using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// The outlier sanity sweep — the HERO side of the board (board item 3). THE BAR (Jake, verbatim):
/// *"rule out CRAZY outliers or broken things — NOT a detailed balance pass."* Flags only: strict
/// dominance / dead builds · safety-cap or all-overtime fights · mirror asymmetries · per-node
/// catastrophes · tier EV anomalies.
///
/// Split out of `Program.cs` (2026-07-26) so `--baseline` can record the same numbers the report
/// prints without re-running the matrix or, worse, re-implementing it.
/// </summary>
public static class BuildSweep
{
    public sealed class Build
    {
        public string Class = "", Label = "";
        public List<string> Nodes = new List<string>();
        public double WinPct, OvertimePct;
    }

    public sealed class ClassRow
    {
        public string Class = "";
        public double Avg, Best, Worst;
    }

    public sealed class NodeDelta
    {
        public string Node = "", Rival = "";
        public double Delta;
    }

    public sealed class TierRow
    {
        public FightTier Tier;
        public double VictoryPct, AvgBossWins, FightWinPct, AvgGold;
        public int CapHits;
    }

    public sealed class Result
    {
        public List<Build> Builds = new List<Build>();
        public List<ClassRow> Classes = new List<ClassRow>();
        public List<NodeDelta> NodeDeltas = new List<NodeDelta>();
        public List<TierRow> Tiers = new List<TierRow>();
        public List<string> Flags = new List<string>();
        public List<string> MirrorNonDraws = new List<string>();
        public int CapHits, Fights;
    }

    private static string Shorten(string nodeId) => nodeId[(nodeId.LastIndexOf('.') + 1)..];

    /// <summary>Escorts carry mana + a small signature so support builds (mana-grant, cast-count
    /// texture) aren't structurally starved by the harness itself.</summary>
    private static UnitDef Escort() => new UnitDef
    {
        Name = "escort", MaxHp = 120, Attack = 8, AttackInterval = 10, Range = 1, MoveInterval = 5,
        ManaMax = 30,
        Signature = { new EffectDef { Kind = EffectKind.Damage, Amount = 12,
                                      Select = new Selector { Kind = SelKind.CurrentTarget } } },
    };

    public static Result Run(Catalog cat)
    {
        var res = new Result();

        // ---- enumerate the 64 builds -------------------------------------------------
        var builds = new List<(string Class, string Label, List<string> Nodes)>();
        foreach (var id in cat.HeroPool(1))
        {
            var fork = cat.ForkRank(id);
            foreach (int pB in new[] { 0, 1 })
            foreach (int pA in new[] { 0, 1 })
            foreach (int pS in new[] { 0, 1 })
            {
                string? path = null;
                var nodes = new List<string>();
                var picks = new Dictionary<Rank, int> { [Rank.B] = pB, [Rank.A] = pA, [Rank.S] = pS };
                foreach (var rank in new[] { Rank.B, Rank.A, Rank.S })
                {
                    var (a, b) = cat.SpecOptions(id, rank, path);
                    string chosen = picks[rank] == 0 ? a : b;
                    nodes.Add(chosen);
                    if (rank == fork) path = chosen;
                }
                builds.Add((id, string.Join("+", nodes.Select(Shorten)), nodes));
            }
        }

        UnitDef HeroDef(int b)
        {
            var (cls, _, nodes) = builds[b];
            return Loadout.Compose(cat.Chassis(cls), nodes: nodes.Select(cat.Node),
                tier: WeaponTier.Honed, mastered: true, rankSteps: 3).Def;
        }

        // ---- the round-robin matrix: build + 2 escorts vs build + 2 escorts -----------
        int n = builds.Count;
        var score = new double[n];      // win 1 / draw 0.5
        var overtime = new int[n];
        var games = new int[n];

        for (int i = 0; i < n; i++)
        for (int j = i; j < n; j++)
        {
            var units = new List<UnitState>
            {
                UnitState.Spawn(0, 0, HeroDef(i), Hex.FromRowCol(1, 2)),
                UnitState.Spawn(1, 0, Escort(), Hex.FromRowCol(2, 1)),
                UnitState.Spawn(2, 0, Escort(), Hex.FromRowCol(2, 3)),
                UnitState.Spawn(3, 1, HeroDef(j), Hex.FromRowCol(6, 2)),
                UnitState.Spawn(4, 1, Escort(), Hex.FromRowCol(5, 1)),
                UnitState.Spawn(5, 1, Escort(), Hex.FromRowCol(5, 3)),
            };
            var r = new Battle(units, seed: (ulong)(i * 1009 + j * 31 + 7)).Run();
            if (r.EndTick >= Battle.SafetyCapTick) res.CapHits++;
            if (r.EndTick >= Battle.OvertimeStartTick) { overtime[i]++; if (i != j) overtime[j]++; }
            games[i]++; if (i != j) games[j]++;
            double s0 = r.Winner == Winner.Team0 ? 1 : r.Winner == Winner.Draw ? 0.5 : 0;
            score[i] += s0; if (i != j) score[j] += 1 - s0;
            if (i == j && r.Winner != Winner.Draw) res.MirrorNonDraws.Add(builds[i].Label);
        }
        res.Fights = n * (n + 1) / 2;

        for (int b = 0; b < n; b++)
        {
            double wr = 100 * score[b] / games[b];
            res.Builds.Add(new Build
            {
                Class = builds[b].Class, Label = builds[b].Label, Nodes = builds[b].Nodes,
                WinPct = wr, OvertimePct = 100.0 * overtime[b] / games[b],
            });
            if (wr >= 90) res.Flags.Add($"DOMINANT {builds[b].Class}:{builds[b].Label} ({wr:F0}%)");
            if (wr <= 10) res.Flags.Add($"DEAD {builds[b].Class}:{builds[b].Label} ({wr:F0}%)");
        }

        // Per-class spread (a class whose best and worst builds diverge wildly is fine;
        // a class ALL of whose builds sit at the bottom is a chassis outlier).
        foreach (var cls in cat.HeroPool(1))
        {
            var rates = res.Builds.Where(b => b.Class == cls).Select(b => b.WinPct).ToList();
            res.Classes.Add(new ClassRow
            {
                Class = cls, Avg = rates.Average(), Best = rates.Max(), Worst = rates.Min(),
            });
            if (rates.Max() < 25) res.Flags.Add($"CHASSIS-DEAD {cls} (best build {rates.Max():F0}%)");
            if (rates.Min() > 75) res.Flags.Add($"CHASSIS-DOMINANT {cls} (worst build {rates.Min():F0}%)");
        }

        // Per-node: average win% of the 4 builds carrying each node vs its 4 counterparts.
        foreach (var (_, pair) in Kits.Offers.Select(kv => (kv.Key, kv.Value)))
        {
            double RateOf(string node)
            {
                var idx = res.Builds.Where(b => b.Nodes.Contains(node)).ToList();
                return idx.Count == 0 ? -1 : idx.Average(b => b.WinPct);
            }
            double ra = RateOf(pair.A), rb = RateOf(pair.B);
            if (ra < 0 || rb < 0) continue;
            res.NodeDeltas.Add(new NodeDelta { Node = pair.A, Rival = pair.B, Delta = ra - rb });
        }
        res.NodeDeltas = res.NodeDeltas.OrderByDescending(x => Math.Abs(x.Delta)).ToList();
        foreach (var d in res.NodeDeltas)
            if (Math.Abs(d.Delta) >= 25)
                res.Flags.Add($"NODE-LOPSIDED {d.Node} vs {d.Rival} (Δ{d.Delta:F0})");

        // ---- run-level: tier EV over full bot runs ------------------------------------
        foreach (FightTier tier in new[] { FightTier.Safe, FightTier.Even, FightTier.Greedy })
        {
            var policy = new RunPolicy { Tier = _ => tier };
            var reports = RunHarness.PlayMany(120, seedBase: (ulong)(42_000 + 1000 * (int)tier), cat, policy: policy);
            var fights = reports.SelectMany(r => r.Fights).ToList();
            int caps = fights.Count(f => f.EndTick >= Battle.SafetyCapTick);
            if (caps > 0) res.Flags.Add($"RUN-CAP {tier}: {caps} capped fights");
            res.Tiers.Add(new TierRow
            {
                Tier = tier,
                VictoryPct = 100.0 * reports.Count(r => r.Final.Victory) / reports.Count,
                AvgBossWins = reports.Average(r => r.Final.BossWins),
                FightWinPct = 100.0 * fights.Count(f => f.Won) / fights.Count,
                AvgGold = reports.Average(r => r.GoldFromNodes),
                CapHits = caps,
            });
        }

        return res;
    }
}
