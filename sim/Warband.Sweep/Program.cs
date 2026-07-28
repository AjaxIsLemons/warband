using System;
using System.Linq;
using System.Text;
using Warband.Content;

// The authoring instruments. Every one obeys the same bar: **NAME what is there, tune nothing.**
//   (default)   the outlier sanity sweep — hero builds, classes, nodes, tier EV (board item 3)
//   --enc       authored node encounters, four answer axes × six formations
//   --boss      the act bosses, held to the harder "how many kinds of strength pass" bar
//   --oath      the Bonded Pair what-if
//   --baseline  all of the above reduced to committed golden numbers (Makefile target `baseline`)
//   --version   the content fingerprint (ADR 0008)
// Emits markdown on stdout; the session pipes it into the vault.

switch (args.FirstOrDefault())
{
    case "--oath": OathProbe.Run(); return;
    case "--enc": EncounterProbe.Run(args.Contains("--candidates")); return;
    case "--boss": BossProbe.Run(args.Contains("--candidates")); return;
    case "--baseline": Baseline.Run(args.Skip(1).FirstOrDefault()); return;
    // Printable so the value computed here can be compared against the one the Unity build
    // computes — if those ever differ, every save made on one machine refuses to load on the
    // other, and the symptom looks like save corruption.
    case "--version": Console.WriteLine(new Catalog().ContentVersion); return;
}

// --candidates includes authored-but-unreachable content (Kits.CandidateNodes) so a proposed
// path can be measured against its siblings BEFORE anyone argues about promoting it. It cannot
// leak into a run: RunController is handed a Catalog with this off.
bool includeCandidates = args.Contains("--candidates");

var cat = new Catalog { IncludeCandidates = includeCandidates };
var sweep = BuildSweep.Run(cat);
var report = new StringBuilder();
report.AppendLine($"# Outlier sweep — {args.FirstOrDefault() ?? "undated"}");
if (includeCandidates)
    report.AppendLine("\n**Candidate content INCLUDED** — unreachable in a real run; measured here only.");
report.AppendLine();

report.AppendLine($"## Build round-robin ({sweep.Builds.Count} builds, hero+2 escorts, rank S, Honed mastered)");
report.AppendLine($"- Fights: {sweep.Fights} · safety-cap hits: **{sweep.CapHits}** · non-draw mirrors: **{sweep.MirrorNonDraws.Count}**");
if (sweep.MirrorNonDraws.Count > 0)
    report.AppendLine($"  - asymmetric mirrors: {string.Join(", ", sweep.MirrorNonDraws.Take(8))}");
report.AppendLine();

var ranked = sweep.Builds.OrderByDescending(b => b.WinPct).ToList();
report.AppendLine("| # | build | win% | OT% |");
report.AppendLine("|---|-------|------|-----|");
foreach (var b in ranked.Take(8))
    report.AppendLine($"| top | {b.Class}: {b.Label} | {b.WinPct:F0} | {b.OvertimePct:F0} |");
foreach (var b in ranked.TakeLast(8))
    report.AppendLine($"| bot | {b.Class}: {b.Label} | {b.WinPct:F0} | {b.OvertimePct:F0} |");
report.AppendLine();

report.AppendLine("## Per-class win% (avg over its 8 builds)");
report.AppendLine("| class | avg | best | worst |");
report.AppendLine("|-------|-----|------|-------|");
foreach (var c in sweep.Classes)
    report.AppendLine($"| {c.Class} | {c.Avg:F0} | {c.Best:F0} | {c.Worst:F0} |");
report.AppendLine();

report.AppendLine("## Node deltas (win% with node − win% with its rival) — |Δ|≥25 flagged");
foreach (var d in sweep.NodeDeltas)
    if (Math.Abs(d.Delta) >= 15)
        report.AppendLine($"- {d.Node} vs {d.Rival}: Δ{d.Delta:F0}");
report.AppendLine();

report.AppendLine("## Tier EV (120 full bot runs per fixed-tier policy, real catalog)");
report.AppendLine("| policy | victory% | avg boss wins | fight win% | avg gold earned | cap hits |");
report.AppendLine("|--------|----------|---------------|------------|-----------------|----------|");
foreach (var t in sweep.Tiers)
    report.AppendLine($"| {t.Tier} | {t.VictoryPct:F0} | {t.AvgBossWins:F2} | {t.FightWinPct:F0} " +
                      $"| {t.AvgGold:F0} | {t.CapHits} |");
report.AppendLine();

report.AppendLine("## FLAGS (the bar: dominance, death, breakage — nothing else)");
if (sweep.Flags.Count == 0) report.AppendLine("- none 🎉");
foreach (var f in sweep.Flags) report.AppendLine($"- {f}");

Console.Write(report.ToString());
