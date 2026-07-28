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

    private static readonly Rank[] SpecRanks = { Rank.B, Rank.A, Rank.S };

    /// <summary>Re-resolve an offer key through the catalog, so the merged (live + candidate)
    /// pool is what gets compared. Keys are "chassis|rank|path", path "-" meaning none.</summary>
    private static List<string> PoolFor(Catalog cat, string key)
    {
        var parts = key.Split('|');
        string? path = parts[2] == "-" ? null : parts[2];
        return new List<string>(cat.SpecOptions(parts[0], Enum.Parse<Rank>(parts[1]), path));
    }

    /// <summary>Depth-first over C/B/A/S, branching on each rank's authored pool width.</summary>
    private static void Walk(Catalog cat, string chassis, Rank fork, string? path,
                             List<string> nodes, int rankIndex,
                             List<(string Class, string Label, List<string> Nodes)> builds)
    {
        if (rankIndex == SpecRanks.Length)
        {
            builds.Add((chassis, string.Join("+", nodes.Select(Shorten)), new List<string>(nodes)));
            return;
        }
        var rank = SpecRanks[rankIndex];
        foreach (string choice in cat.SpecOptions(chassis, rank, path))
        {
            nodes.Add(choice);
            Walk(cat, chassis, fork, rank == fork ? choice : path, nodes, rankIndex + 1, builds);
            nodes.RemoveAt(nodes.Count - 1);
        }
    }

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

        // ---- enumerate every reachable build ------------------------------------------
        // Walks the ACTUAL pool at each rank rather than a hardcoded 0/1, so widening a pool
        // widens the sweep instead of silently leaving the extra options untested. Eight
        // two-wide heroes still enumerate the same 64 builds.
        var builds = new List<(string Class, string Label, List<string> Nodes)>();
        foreach (var id in cat.HeroPool(1))
            Walk(cat, id, cat.ForkRank(id), null, new List<string>(), 0, builds);

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

        // Per-node: average win% of the builds carrying each node vs each of its pool rivals.
        // Every unordered pair inside a pool is reported, so a two-wide pool yields the single
        // A-vs-B delta it always did and a wider pool reports each rivalry rather than only
        // comparing against whichever option happened to be authored first.
        // Read the pools THROUGH the catalog so candidate paths are compared against their
        // siblings too — reading Kits.Offers directly would silently drop exactly the content
        // --candidates exists to measure.
        var pools = new List<List<string>>();
        foreach (var key in Kits.Offers.Keys) pools.Add(PoolFor(cat, key));
        if (cat.IncludeCandidates)
            foreach (var key in Kits.CandidateOffers.Keys)
                if (!Kits.Offers.ContainsKey(key)) pools.Add(PoolFor(cat, key));

        foreach (var pool in pools)
        {
            double RateOf(string node)
            {
                var idx = res.Builds.Where(b => b.Nodes.Contains(node)).ToList();
                return idx.Count == 0 ? -1 : idx.Average(b => b.WinPct);
            }
            for (int i = 0; i < pool.Count; i++)
            for (int j = i + 1; j < pool.Count; j++)
            {
                double ra = RateOf(pool[i]), rb = RateOf(pool[j]);
                if (ra < 0 || rb < 0) continue;
                res.NodeDeltas.Add(new NodeDelta { Node = pool[i], Rival = pool[j], Delta = ra - rb });
            }
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
