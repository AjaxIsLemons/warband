# ADR 0005 — Items, spec trees, and the loadout composer

**Date:** 2026-07-22 · **Status:** proposed (round 9, awaiting Jake's calls on the ❓s) ·
**Context:** before building more, settle the remaining sim-level data shapes.

## The composition principle
**The battle sim never knows about items, ranks, or spec trees.** The run layer composes a
hero's chassis + rank + spec choices + items + run-scoped bonuses into ONE resolved
`UnitDef` (+ spawn statuses) via a deterministic, unit-tested **loadout composer** that
lives in `Warband.Sim` beside (not inside) `Battle`. The sim stays a pure fight resolver;
item/tree design can iterate forever without touching the engine or its guardrails.

## Items (from heroes.md: 2 slots — Weapon + Trinket)
- **Weapon = an attack-profile overlay**: may override damage / interval / range /
  **attack shape** (single, cleave, pierce-line, splash) and add on-hit triggers
  (expressible today as `On: Attack, SourceIsOwner` triggers). Category-locked by chassis.
- **Trinket = a bundle of the existing primitives**: stat mods, StatRules, Triggers,
  mana mods.
- Chassis always has a baseline attack — a hero with no weapon still fights. Heroes are
  the stars; items are the churn axis (heroes sticky, items swappable — ADR 0001 lineage).

## Spec tree (mechanically)
- Rank C→B→A→S by duplicates (heroes.md). **A spec node = the same primitive bundle as a
  trinket** (stat deltas + triggers + StatRules) **plus an optional signature override.**
- "Fork transforms the signature" is a **content discipline, not a data mechanic**: the
  B-path authors a full replacement signature that *reads* as an upgrade of the base
  (circuit's evolution model). No effect-graph surgery in the engine.
- Per-rank chassis stat scaling = a table on the chassis (content, later).

## Remaining sim-level gaps (the honest list, build order)
1. **Attack shapes** on auto-attacks (cleave/pierce/splash) — weapons need them.
2. **Displacement effects** (Push/Pull/Leap as EffectKinds) + **collision** hooks
   (into wall/unit → damage/stun) — Shade's chassis needs Leap.
3. **Targeting-rule overrides** (nearest is hardwired; Sniper wants farthest,
   assassin wants backline) — enum on UnitDef.
4. **Board bounds** — the board is currently an infinite plane; clamp to 6×8. ❓confirm.
5. **In-combat RNG policy** ❓: the PCG32 is plumbed but NOTHING consumes it — fights are
   100% deterministic given setup. Proposal: **keep zero in-combat RNG for v1** (no crit,
   no proc rolls; every ghost matchup is pure build+placement; seed reserved for run-layer
   generation). Revisit only if fights feel solved/stale in playtests.

## Content doctrine (recorded from Jake, round 9)
All current content is placeholder for system-building. Per-hero deep dives happen later,
each as its own design pass. See CLAUDE.md warning block.
