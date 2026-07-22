# ADR 0005 — Items, spec trees, and the loadout composer

**Date:** 2026-07-22 · **Status:** ACCEPTED (round 10, Jake's calls recorded below) ·
**Context:** before building more, settle the remaining sim-level data shapes.

## Round-10 decisions (Jake)
1. **Hero anatomy:** passives exist from tier 1 (rank C); a unit may carry **multiple
   passives**, exactly **one active ability** (the signature), and **their weapon/attack**.
2. **Crit is in — the only in-combat RNG.** "Could free up ability design" (on-crit
   passives). Nothing else rolls for now. Battle takes a seed; replay = (seed, snapshot).
3. **Board = 6×8, clamped** ("easy to tune as needed").
4. **Weapon-REQUIRED: every chassis ships with a starter weapon, and RANGE LIVES ON THE
   WEAPON** (damage/interval/range/shape = the weapon's attack profile). Jake: a ranger
   who specs into daggers becomes assassin-flavored — "more interesting and tinkerable."
   Chassis keeps HP/move/mana/innate passives/signature.

## The composition principle
**The battle sim never knows about items, ranks, or spec trees.** The run layer composes a
hero's chassis + rank + spec choices + items + run-scoped bonuses into ONE resolved
`UnitDef` (+ spawn statuses) via a deterministic, unit-tested **loadout composer** that
lives in `Warband.Sim` beside (not inside) `Battle`. The sim stays a pure fight resolver;
item/tree design can iterate forever without touching the engine or its guardrails.

## Items (from heroes.md: 2 slots — Weapon + Trinket)
- **Weapon = the attack profile** (round 10): damage / interval / **range** / attack shape
  (single, cleave, pierce-line, splash) / on-hit triggers all live on the weapon.
  Weapon-required; every chassis ships with a starter weapon. Category locks are a
  content decision per chassis (loose enough for ranger→daggers pivots).
- **Trinket = a bundle of the existing primitives**: stat mods, StatRules, Triggers,
  mana mods.
- Heroes are the stars; items are the churn axis (heroes sticky, items swappable).

## Spec tree (mechanically)
- Rank C→B→A→S by duplicates (heroes.md). **A spec node = the same primitive bundle as a
  trinket** (stat deltas + triggers + StatRules) **plus an optional signature override.**
- "Fork transforms the signature" is a **content discipline, not a data mechanic**: the
  B-path authors a full replacement signature that *reads* as an upgrade of the base
  (circuit's evolution model). No effect-graph surgery in the engine.
- Per-rank chassis stat scaling = a table on the chassis (content, later).

## Remaining sim-level gaps — re-scoped round 10 (Jake)
1. **Attack shapes** — DEFERRED to the weapons design pass ("less worried for now").
2. **Displacement (Push/Pull/collisions)** — DEFERRED, nice-to-have. **Leap is BUILT**
   (effect: owner teleports adjacent to selected target, drops sticky target, fights
   normally — backline access is a passive, not a targeting rule).
3. **Targeting** — SETTLED as-is: TFT-simple, nearest-in-range + sticky. No override
   system. "Simple rules for now."
4. **Board bounds** — clamp to 6×8. DECIDED round 10, build now.
5. **In-combat RNG** — DECIDED round 10: **crit only** (chance% + multiplier on the
   attack profile; auto-attacks roll, abilities don't for now), plus an `IsCrit` trigger
   condition for on-crit passives. Battle is seeded; no other rolls exist.

## Content doctrine (recorded from Jake, round 9)
All current content is placeholder for system-building. Per-hero deep dives happen later,
each as its own design pass. See CLAUDE.md warning block.
