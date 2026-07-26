# ADR 0025 — Routing and the engagement law

**Date:** 2026-07-26 · **Status:** accepted (Jake's bug report) · **Participants:** Jake + Claude

## Context
Jake, watching fights in the standalone build: *"Units will just sit behind others in line — I think
they only take the shortest path when another one is available that they could take to get in range
of an enemy. I think I've seen the jump units jump, get stuck between two enemy units doing nothing
because they are 'targeting' another unit."*

Both were real, both reproduced headlessly, and both were worse than reported.

**Cause 1 — movement was a greedy hill-climb, not a path.** The decide phase looked at the unit's
six neighbours and stepped to a **strictly closer, unoccupied** one. That is a hill-climb on
straight-line hex distance, and hill-climbs have local minima. On a 6-wide board with a front line
there are minima everywhere: a body in the one or two closing directions removes every
distance-reducing hex, the search returns nothing, and the unit stands there **for the rest of the
fight**. Repro: four bodies in a clump against one enemy — the flank body logged **0 swings across
1200 ticks**. It was never a movement bug the renderer could show as such; the unit was correctly
drawn standing still, because the sim had decided to stand still.

**Cause 2 — targeting had no idea what it could reach, and nothing to fall back on.** A unit whose
target is unreachable had no behaviour at all: it could not step (Cause 1), and it would not swing at
anything else, because attacks only ever go to `TargetId`. Repro: a `LowestHp` diver leapt into a
full enemy backline, landed with all six neighbours occupied, and stood **completely motionless for
~1000 ticks** while five enemies killed it — it "wanted" the weakest enemy, which was across the
board behind them.

**Cause 3 — a leap threw away the target it had just chosen.** `LeapTo` cleared `TargetId` so the
leaper would "re-acquire nearest from the new position". That comment pre-dates `TargetPref`
(ADR 0022). For a `Farthest`-seeking diver it inverts the whole kit: from your backline the farthest
enemy is your **front line**, i.e. the rank it just jumped over — so the Gloamstalker dived and then
walked all the way back. The Drop encounter, whose entire authored premise is *"your backline is the
front line"*, was measuring as **FREE — every formation wins. It poses nothing yet.**

## Decision

### 1. Routing law — units follow a field, not a gradient
A unit's destination is chosen by a **flow field**, not by comparing its six neighbours.

- Dijkstra runs outward from the **engage ring**: every in-bounds hex the unit could *attack the
  target from* — inside weapon reach, not inside a wall, and with a projectile line that actually
  arrives. The goal is a firing position, not the target's hex, so closing, walking round a crowd
  and hunting a firing angle are one behaviour instead of three special cases.
- **A wall is impassable. A body is a detour, not a wall** — entering an occupied hex costs
  `Pathing.BodyCost` (6). The field therefore keeps a gradient straight through a crowd, and that
  constant *is* a unit's patience for a queue: it goes round whenever going round is no dearer than
  waiting out one body, and holds position when it is worse than that. Queueing still looks like
  queueing; it just stops being permanent.
- A goal hex is seeded at its own occupancy toll, not at 0. This is load-bearing and was the one
  subtle bug in the build: a melee engage ring is the target's own neighbourhood, which is exactly
  where your allies already stand. Seed those at 0 and everyone queueing behind them believes they
  have arrived, so the whole team stops one hex short of a target with free slots on its far side.
- Step gate: move to the free neighbour lowest in the field, **iff its remaining route is no dearer
  than the unit's own**. Strictly-cheaper would refuse the last step into a ring (a ring hex and the
  route through it cost the same); anything looser makes a unit orbit a full scrum forever.
- Determinism is unchanged: integer costs, Dial's algorithm with circular buckets, ascending board
  index, fixed direction order for ties. No floats, no rng, no unordered iteration.

### 2. The engagement law — you fight what you can reach
> If a unit can neither strike its target nor take a step toward it, it fights the best enemy it
> **can** strike from where it stands, chosen by its own `TargetPref`.

It **retargets** rather than taking a free swing elsewhere, so one unit keeps one intent: the
signature, `DistanceToTarget`, and the renderer all read the enemy it is really fighting. Heal-autos
get the same law against the lowest-HP ally in reach (ADR 0012). **Taunt is exempt** — a taunt is not
negotiable.

This is also the honest resolution of the subtlety ADR 0013 flagged and deliberately left open
("the target leaves your attack range → re-acquire", which for walking melee collapses into
retargeting every tick). Reachability, not range, is the trigger — so stickiness survives intact and
only genuinely stuck units re-aim.

### 3. A leap fights what it landed on
`LeapTo` sets `TargetId` to the unit it jumped at. The selector already chose the victim; landing on
someone and then hunting someone else is not a dive.

## What this does NOT change
`TargetPref` still decides acquisition, and stickiness (ADR 0013) still owns re-acquisition —
a Shade still crosses the board for the weakest body when it *can*. The committed-step movement law
(ADR 0018) is untouched: this ADR changes only how the destination of a step is chosen, never how
the step is taken, reserved or completed. No content, stat or kit numbers changed.

## Measured on shipping content
264 real encounter fights (every node encounter × 3 acts × 4 formations × 6 seeds, 2040 units),
before vs after, from a HEAD worktree so the comparison is an A/B and not a memory:

| | before | after |
|---|---|---|
| units that **never swing once** | 96 (4.71%) | **18 (0.88%)** |
| units frozen ≥3s (no move/swing/cast) | 390 (19.1%) | **198 (9.7%)** |
| dead time, share of living unit-ticks | 5.23% | **2.82%** |
| mean time to a unit's first swing | 14t | **10t** |

One in twenty units used to spend an entire authored fight without swinging at all. It is now
one in a hundred, and fights reach their decision 4.4% sooner in unit-ticks.

## Consequences
- **Encounters got harder, because the enemy AI now works.** The Drop went FREE → **POSES A PROBLEM
  (placement swings the result 100 points)** — its authored premise finally happens. The naive-line
  bot still completes 3/12 runs but now dies in **act 1** rather than act 2. Per the content
  doctrine this is a measurement, not a licence to rebalance: nothing should be retuned against it
  until the interactive playtest.
- Bosses still admit 3–4 answer axes each; act 3 reach went 100% → 33% ("marginal").
- ~27% slower sim (`--enc` sweep 1.18 s → 1.50 s): one Dijkstra per moving unit per decision tick,
  cached per (target, range) per tick, only on ticks where a unit is actually free to step. A
  48-hex board makes this cheap; if it ever matters, the field arrays are poolable.
- **Watch for at playtest:** whether `BodyCost = 6` reads as the right patience. Too low looks like
  skittish units abandoning a queue; too high looks like the old jam. It is one constant.
