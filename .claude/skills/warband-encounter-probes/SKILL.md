---
name: warband-encounter-probes
description: Measure warband's PvE content — balance tuning, enemy composition, act difficulty, boss tuning, run length, "does this encounter pose a decision?" — with the baseline/enc/boss/oath probes.
---

You are measuring warband's content: enemy composition, act difficulty, a boss's tuning, run
length, or whether an encounter poses a decision at all. **Source of truth:
`docs/vault/Design/pve-encounters.md`**, with ADR 0023 (enemies are authored; composition is the
act's difficulty lever) and ADR 0024 (bosses are strength exams + the disclosure contract).
Cite those, don't re-derive them. Every instrument obeys one bar: **NAME what is there, tune
nothing.**

## The doctrine boundary — read before changing a number
- CLAUDE.md forbids a detailed balance pass before playtest #1. Probes MEASURE and NAME
  problems; they do not authorize a pass. Fix only broken machinery, unreadable behavior, or
  outliers that invalidate the test.
- ADR 0016's balance law: do not flatten a spectacular engine merely because it is powerful.
- Legitimate output of a probe session: a named finding, a report, an ADR, a composition
  change, a roadmap item. Not a sweep of magnitudes.

## The instruments (headless, `sim/Warband.Sweep/`, markdown on stdout)
- `make baseline` → writes `docs/vault/Projects/balance-baseline.md`. **The A/B is `git diff`** —
  run it before AND after your change, then read the diff. It never asserts. Deterministic:
  unchanged content must reproduce it byte for byte, or something picked up a wall clock.
- `make enc` — the authored node pool: 4 answer axes × 6 formations × 24 seeds per act, plus the
  naive line (12 bot runs, fixed comp, default placement — the weakest legal answer, so the floor).
- `make boss` — each act boss at the harder bar: how many kinds of strength can pass it.
- `make oath` — the Bonded Pair what-if: does the Bond pose a decision?
- `make content-version` — the ADR 0008 fingerprint, to compare against a build's manifest.
- `--candidates` (on `--enc`, `--boss`, or the default sweep) appends `Kits.CandidateNodes` axes
  so a proposed spec path is measured against its siblings before anyone argues about promoting
  it. Off by default; it cannot leak into a run.
- Probes call the catalog's own `Encounters.Scale` / `BossScalePct`, and share `ProbeParties`, so
  no instrument can measure a different game than the one that ships.

## The bar is SPREAD, not win%
`ProbeParties.Summarise`: an axis passes at best-formation win ≥55%, marginal 30–55. Verdicts are
UNSURVIVABLE / PUNISHING / FREE / PRESCRIBES A BUILD / ADMITS n ANSWERS, then FLAT when the
placement spread is under 15 points. A boss every formation beats identically is not a boss: act
2's showed 3/4 axes at 100% from every formation, spread 0. Retuning it (bell 14s→9s, shell
46→58, longer crater) brought the spread to 100.

## Authoring loop
1. **Answer the encounter's own pitch, not generic stats.** If the rule text promises "choose
   which threat you leave enraged", the report needs a section stating whether placement chose
   the survivor. `OathProbe` grew its §4 for exactly that reason — the report could not answer
   the encounter's own pitch before. Result: placement chose the survivor in 4/4 lineups, Δ84 win%.
2. **Check geometry before numbers.** "THE CHOICE DOES NOT EXIST" traced to asymmetric standing
   positions: Bulwark at (5,2), Sharpshot tucked at (6,4) and therefore structurally unreachable
   first. Fixed with a data change only — same rank, opposite edges, (5,0) and (5,5).
3. **Measure several candidate arrangements; let the run-completion test arbitrate.** Re-runs are
   ~3 s. Four placements were measured; two inner-symmetric variants also posed the decision but
   made act 1 hard enough that the bot lost 4/6 seeded runs, caught by
   `ContentTests.FullRunsCompleteOnRealContent` (`sim/Warband.Run.Tests/ContentTests.cs`). Only
   the edge mirror passed both gates. Run that test on every composition change.
4. **Composition is the act lever; stats are secondary.** The Gnawing Hour ships 5 bodies at act
   1, 8 at act 2, 10 at act 3 — same Hourling, scaled comp. Enemies get their own designs: propose
   rule-bending disclosed mechanics (WARD, RITUAL) over stat-scaled player kits.
5. **Report the honesty gap explicitly.** A choice with a strongly correct answer is **a lesson,
   not yet a dilemma** — flag it as such rather than calling it a decision. Same for a mechanic
   the sim fires that nothing in the UI names.

## Traps
- **The tune-to-the-metric trap.** A probe number is a symptom, not a target. Chasing a non-flat
  spread with a third Slagworks wall made act 2's pool uniformly hard and drove the naive line to
  0/12 completed runs. Slagworks should escalate 2→3, not be maximal in both.
- **Negative controls.** Before trusting a probe or a test, confirm it CAN fail. A Reward-phase
  save test passed for free — no seed in 1..40 reached a boss reward via the naive line — and was
  rewritten to construct the state directly.
- **Reports supersede, they do not append.** A new probe report replaces the old one (see the two
  dated `Projects/oath-probe-*` files) and gets registered in `docs/vault/index.md`.
- Probe parties are deliberately one offer behind the real curve: `ProbeParties.SizeAt` is act+2
  capped at 4, while `RunController` unlocks up to 6. Party size is the strongest difficulty dial
  in the game and it is not a stat — one extra body turned The Long Range, the sharpest encounter
  in the pool, FREE from every formation. Every number read off a probe is conditional on it.
