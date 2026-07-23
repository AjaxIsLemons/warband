using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

// The outlier sanity sweep (board item 3). THE BAR (Jake, verbatim): "rule out CRAZY
// outliers or broken things — NOT a detailed balance pass." Flags only:
//   strict dominance / dead builds · safety-cap or all-overtime fights ·
//   mirror asymmetries · per-node catastrophes · tier EV anomalies.
// Emits markdown on stdout; the session pipes it into the vault.

var cat = new Catalog();
var report = new StringBuilder();
report.AppendLine($"# Outlier sweep — {args.FirstOrDefault() ?? "undated"}");
report.AppendLine();

// ---- enumerate the 64 builds -------------------------------------------------------
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
static string Shorten(string nodeId) => nodeId[(nodeId.LastIndexOf('.') + 1)..];

// Escorts carry mana + a small signature so support builds (mana-grant, cast-count
// texture) aren't structurally starved by the harness itself.
UnitDef Escort() => new UnitDef
{
    Name = "escort", MaxHp = 120, Attack = 8, AttackInterval = 10, Range = 1, MoveInterval = 5,
    ManaMax = 30,
    Signature = { new EffectDef { Kind = EffectKind.Damage, Amount = 12,
                                  Select = new Selector { Kind = SelKind.CurrentTarget } } },
};

UnitDef HeroDef(int b)
{
    var (cls, _, nodes) = builds[b];
    return Loadout.Compose(cat.Chassis(cls), nodes: nodes.Select(cat.Node),
        tier: WeaponTier.Honed, mastered: true, rankSteps: 3).Def;
}

// ---- the round-robin matrix: build + 2 escorts vs build + 2 escorts ----------------
int n = builds.Count;
var score = new double[n];      // win 1 / draw 0.5
var capHits = 0;
var mirrorNonDraws = new List<string>();
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
    if (r.EndTick >= Battle.SafetyCapTick) capHits++;
    if (r.EndTick >= Battle.OvertimeStartTick) { overtime[i]++; if (i != j) overtime[j]++; }
    games[i]++; if (i != j) games[j]++;
    double s0 = r.Winner == Winner.Team0 ? 1 : r.Winner == Winner.Draw ? 0.5 : 0;
    score[i] += s0; if (i != j) score[j] += 1 - s0;
    if (i == j && r.Winner != Winner.Draw) mirrorNonDraws.Add(builds[i].Label);
}

report.AppendLine($"## Build round-robin ({n} builds, hero+2 escorts, rank S, Honed mastered)");
report.AppendLine($"- Fights: {n * (n + 1) / 2} · safety-cap hits: **{capHits}** · non-draw mirrors: **{mirrorNonDraws.Count}**");
if (mirrorNonDraws.Count > 0)
    report.AppendLine($"  - asymmetric mirrors: {string.Join(", ", mirrorNonDraws.Take(8))}");
report.AppendLine();

var ranked = Enumerable.Range(0, n).OrderByDescending(b => score[b] / games[b]).ToList();
report.AppendLine("| # | build | win% | OT% |");
report.AppendLine("|---|-------|------|-----|");
foreach (var b in ranked.Take(8))
    report.AppendLine($"| top | {builds[b].Class}: {builds[b].Label} | {100 * score[b] / games[b]:F0} | {100.0 * overtime[b] / games[b]:F0} |");
foreach (var b in ranked.TakeLast(8))
    report.AppendLine($"| bot | {builds[b].Class}: {builds[b].Label} | {100 * score[b] / games[b]:F0} | {100.0 * overtime[b] / games[b]:F0} |");
report.AppendLine();

// Flags per the bar.
var flagged = new List<string>();
for (int b = 0; b < n; b++)
{
    double wr = 100 * score[b] / games[b];
    if (wr >= 90) flagged.Add($"DOMINANT {builds[b].Class}:{builds[b].Label} ({wr:F0}%)");
    if (wr <= 10) flagged.Add($"DEAD {builds[b].Class}:{builds[b].Label} ({wr:F0}%)");
}

// Per-class spread (a class whose best and worst builds diverge wildly is fine;
// a class ALL of whose builds sit at the bottom is a chassis outlier).
report.AppendLine("## Per-class win% (avg over its 8 builds)");
report.AppendLine("| class | avg | best | worst |");
report.AppendLine("|-------|-----|------|-------|");
foreach (var cls in cat.HeroPool(1))
{
    var idx = Enumerable.Range(0, n).Where(b => builds[b].Class == cls).ToList();
    var rates = idx.Select(b => 100 * score[b] / games[b]).ToList();
    report.AppendLine($"| {cls} | {rates.Average():F0} | {rates.Max():F0} | {rates.Min():F0} |");
    if (rates.Max() < 25) flagged.Add($"CHASSIS-DEAD {cls} (best build {rates.Max():F0}%)");
    if (rates.Min() > 75) flagged.Add($"CHASSIS-DOMINANT {cls} (worst build {rates.Min():F0}%)");
}
report.AppendLine();

// Per-node: average win% of the 4 builds carrying each node vs its 4 counterparts.
report.AppendLine("## Node deltas (win% with node − win% with its rival) — |Δ|≥25 flagged");
var nodeDeltas = new List<(string Node, string Rival, double Delta)>();
foreach (var (key, pair) in Kits.Offers.Select(kv => (kv.Key, kv.Value)))
{
    double RateOf(string node)
    {
        var idx = Enumerable.Range(0, n).Where(b => builds[b].Nodes.Contains(node)).ToList();
        return idx.Count == 0 ? -1 : idx.Average(b => 100 * score[b] / games[b]);
    }
    double ra = RateOf(pair.A), rb = RateOf(pair.B);
    if (ra < 0 || rb < 0) continue;
    nodeDeltas.Add((pair.A, pair.B, ra - rb));
}
foreach (var (nodeA, rival, delta) in nodeDeltas.OrderByDescending(x => Math.Abs(x.Delta)))
{
    if (Math.Abs(delta) >= 25) flagged.Add($"NODE-LOPSIDED {nodeA} vs {rival} (Δ{delta:F0})");
    if (Math.Abs(delta) >= 15)
        report.AppendLine($"- {nodeA} vs {rival}: Δ{delta:F0}");
}
report.AppendLine();

// ---- run-level: tier EV over full bot runs -----------------------------------------
report.AppendLine("## Tier EV (120 full bot runs per fixed-tier policy, real catalog)");
report.AppendLine("| policy | victory% | avg boss wins | fight win% | avg gold earned | cap hits |");
report.AppendLine("|--------|----------|---------------|------------|-----------------|----------|");
foreach (FightTier tier in new[] { FightTier.Safe, FightTier.Even, FightTier.Greedy })
{
    var policy = new RunPolicy { Tier = _ => tier };
    var reports = RunHarness.PlayMany(120, seedBase: (ulong)(42_000 + 1000 * (int)tier), cat, policy: policy);
    var fights = reports.SelectMany(r => r.Fights).ToList();
    int caps = fights.Count(f => f.EndTick >= Battle.SafetyCapTick);
    if (caps > 0) flagged.Add($"RUN-CAP {tier}: {caps} capped fights");
    report.AppendLine(
        $"| {tier} | {100.0 * reports.Count(r => r.Final.Victory) / reports.Count:F0} " +
        $"| {reports.Average(r => r.Final.BossWins):F2} " +
        $"| {100.0 * fights.Count(f => f.Won) / fights.Count:F0} " +
        $"| {reports.Average(r => r.GoldFromNodes):F0} " +
        $"| {caps} |");
}
report.AppendLine();

report.AppendLine("## FLAGS (the bar: dominance, death, breakage — nothing else)");
if (flagged.Count == 0) report.AppendLine("- none 🎉");
foreach (var f in flagged) report.AppendLine($"- {f}");

Console.Write(report.ToString());
