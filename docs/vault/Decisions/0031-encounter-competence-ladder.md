# ADR 0031 — Encounter competence ladder before encounter pressure

**Date:** 2026-07-30 · **Status:** accepted · **Participants:** Jake + Codex

## Context

Item 32 begins from a contradictory measurement:

- The Gnawing Hour, The Long Range at its intended act, and The Long Procession are FREE + FLAT
  for every purpose-built answer-axis party.
- The full-run bot completes only 1/12 runs.
- The Last Oath fell from three passing answer axes to two after the board widened to 8×8, but
  its `control` party includes the known chassis-dead Banneret.

Authoring encounters directly between those two extremes previously failed. Adding enough bodies
to move a competent party's formation spread drove the no-response bot to 0/12, so the bot became
an accidental content ceiling. Conversely, tuning to keep that bot alive left encounters that
ignored placement, the player's only ordinary order.

Jake chose option A: fix the measuring ladder before changing content.

## Decisions

### 1. Three measurements have three different jobs

1. **No-response line:** the existing greedy full-run bot, fixed default formation. It remains a
   diagnostic floor and run-machine smoke. Its completion rate is not an encounter target.
2. **Responsive floor:** the same starter, shop policy, and available build power, but placement
   changes deterministically from the disclosed encounter rule. It may move the warband forward,
   turtle it, or split its lanes; it never reads a simulated outcome or searches formations.
3. **Answer axes:** balanced, reach, control, and damage parties still test how several kinds of
   competent strength interact with every standard formation.

The responsive floor must report both the disclosed response it chose and its full-run outcome.
It sits between refusing to answer the encounter and oracle-like best-of-six placement.

### 2. The control axis may not contain a known dead chassis

At act 1, the current control probe fields Warden + Pikewall + Warcaller. Banneret is already named
`CHASSIS-DEAD` by the build sweep, so its failure cannot diagnose the boss. Replace Warcaller with
a second Warden: duplicates are legal, and Warden + Warden + Pikewall is an actual Stun/Silence/
Taunt answer rather than a disguised Banneret test. Lifebinder remains the fourth act-2+ body.

The Last Oath changes only if this deconfounded party still leaves the boss below three credible
answer axes. Probe correction is not permission to retune the boss.

### 3. Sharpen composition, not player kits or the simulation vocabulary

The three flat encounter families may change only through the existing five enemy roles, authored
formation, and body count:

| encounter | authoring hypothesis |
|---|---|
| The Gnawing Hour | split the swarm into visible attacking wedges so a formation can be overrun from one side rather than every Hourling collapsing into the same center scrum |
| The Long Range | turn one center wall into two disclosed wall/gunner lanes, teaching the Ashfall Battery's reach problem without copying its clock |
| The Long Procession | divide a larger court around the Scribe so clearing bodies visibly accelerates the ritual and committing toward the Scribe becomes a formation decision |

Geometry is tested before body count. No new enemy role, rule, status, spawn system, risk-tier
mutation, hero tuning, item tuning, or economy change belongs to this item.

### 4. The acceptance bar is problem shape, not uniform win rate

- Each targeted family must stop being both FREE and FLAT at its debut act.
- At least two answer axes must remain viable; one correct build is not differentiation.
- Ninth Bell, The Drop, Slagworks, Ashfall Battery, and Waning Crown keep their existing strong
  answer count and formation sensitivity.
- The responsive floor is reported honestly, but no fixed completion percentage becomes a target.
- `ContentTests.FullRunsCompleteOnRealContent` must remain green after every composition change.
- `make baseline` runs before and after; the diff is reviewed, and unchanged probes reproduce
  byte-for-byte.

## Consequences

This pass may make the no-response line look worse without being a regression: refusing to respond
is allowed to lose. It may not make the run machine unreachable, prescribe one build, or flatten a
legitimate broken engine. Human telemetry still decides eventual tuning; these probes only ensure
the authored problem exists.

## Implemented outcome — 2026-07-30

- The responsive floor records its chosen response and adapted-placement count in both `--enc`
  and the committed baseline. A negative control mapped every encounter to `DEFAULT`; adapted
  placements fell to zero and its outcomes exactly matched the no-response line, proving this rung
  is not a second label on the same policy.
- The control axis now fields Warden + Pikewall + Warden at act 1. The Last Oath returned from two
  passing axes to three (balanced, control, damage) without changing the encounter.
- Gnawing Hour fields seven act-1 Hourlings in two visible wedges. Its debut went from four
  100/0-flat axes to three viable answers and a 100-point placement spread.
- Long Range fields one warded wall, one ordinary wall, and three guns across two lanes. Its act-2
  debut went from four flat answers to three viable answers and a 100-point placement spread.
- Long Procession's act-3 court grew from six to eight Hourlings split into two wings around the
  Scribe. It retains all four answers and gained a 17-point placement spread.
- Ninth Bell, The Drop, Slagworks, Ashfall Battery, and Waning Crown retained their protected
  multi-answer / formation-sensitive reads.
- The final no-response line completed 0/12; the responsive line completed 1/12 with 42 disclosed
  adaptations. These are diagnostics, not pass thresholds. The committed default-policy run rows
  also became harder, especially Collapsing (fight win 73% → 55%); human run telemetry owns any
  later numeric tuning.
- Engine health remained structural: frozen units improved from 5.00% to 4.32%. The new 0.31%
  never-swing row was traced to one front Hourling in Long Procession being killed by the reach
  party before its first basic attack in four seeds, not a routing stall.
- The 143-metric baseline reproduced with SHA-256
  `97418fd415e7cebb12f3baa277ed949f2fdb38e3f5db50a189dd8a8d9c5786d5`.
