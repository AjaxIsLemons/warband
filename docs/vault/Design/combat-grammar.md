# Combat grammar — the soul — DRAFT v0.1 (2026-07-22, round 5)

**The soul in one line: spatial tempo warfare.** Three pillars every kit draws from:
**the Clock** (attack & mana rhythms and the tools that bend them), **the Ground** (hexes
that matter), **the Push** (bodies get moved). Placement is the input; these three make
the fight *about* the placement.

## Pillar 1 — the Clock
Two clocks per unit: **attack interval** and **mana** (fills from attacks, damage taken,
small trickle; full = auto-cast). The tempo toolset, in mirrored pairs:

| Tool | Effect | Counter-shape |
|---|---|---|
| **Haste** | attack speed up (stacking, capped) | Slow |
| **Slow** | attack speed down | Haste / cleanse-rider |
| **Stun** | both clocks stop (short, premium) | tenacity-riders post-v1 |
| **Silence** | no casting, no mana gain; autos continue | — kills casters |
| **Disarm** | no autos (and no mana-from-attacks); casting continues | — kills carries |
| **Mana-grant** | ally mana forward (Banneret Rally) | Mana-burn (enemy mana back) |

Silence/Disarm symmetry is the tempo counter-web: you read the ghost's threat and bring
the right denial. Stun is both at once — shorter and rarer.

## Pillar 2 — the Ground
Tiles are state. Both sides place blind but **the map is known** — terrain is part of the
puzzle each act. v1 tile set (❓ scope with Jake):

| Tile | Effect |
|---|---|
| **Obstacle** | impassable; shapes paths, blocks lines — the choke-maker |
| **Hazard** | standing/entering hurts (generic DoT application) |
| **Font** | mana trickle bonus while standing on it — the hill worth holding |

Kits can **create** ground at runtime (scorched trails, consecrated tiles, summoned
walls) — that's where ground gets personal. High-ground/range bonuses: post-v1 candidate.

## Pillar 3 — the Push
Displacement verbs (all respect Obstacles; colliding into one = bonus effect hook):

| Verb | Effect |
|---|---|
| **Push** | shove target N hexes away |
| **Pull** | drag target N hexes closer (the hook) |
| **Leap** | self-move to a distant hex (Shade's innate) |
| **Charge** | self-move along a line through enemies |
| **Root** | target can't move; everything else still runs |

Push × Ground is the marquee interaction: shove into Hazard, off a Font, out of a
Banneret aura, into your Phalanx's reach. Formations break open gaps; assassins exploit them.

## Sustain & damage layer
- **Shield** (absorb pool), **Heal** (instant), **Regen** (heal over time).
- **One generic DoT** (stacking damage/tick). No burn/poison typing — flavor and mechanics
  personalize via **riders on the hero**, not the status: Pyromancer's DoT spreads on kill;
  another hero's DoT Slows; a fork makes your DoT tick faster vs Rooted targets. (Jake's
  call, round 5 — the support-gem philosophy applied to statuses.)
- One damage number, as decided (heroes.md).

## Shapes (targeting geometries)
Single · Adjacent-ring (cleave) · Line (pierce) · Ring-splash (target + neighbors).
Row/cone: post-v1.

## Trigger grammar (the engine vocabulary)
Events kits can hook: combat-start · attack-fired · hit-taken · cast · kill · death ·
ally-death · displacement-suffered/dealt · tile-entered · HP-threshold · overtime-start.
An innate/fork/banner = **when <event> [condition] → <effect>** from the tables above.
This is circuit's proven grammar with ground + displacement added.

## The kit formula
**Interesting kit = crossing two pillars.** Examples:
- Clock × Ground: caster whose consecrated tiles Haste allies standing on them.
- Push × Clock: a hook that also Disarms — drag the carry in *and* shut it off.
- Ground × Push: a wall-summoner who Pushes enemies into her own Obstacles for Stuns.
- Clock × Sustain: a Regen engine that converts overhealing into Mana-grant.
Single-pillar kits are the vanilla floor (Berserker); doubles are where builds sing.

## v1 scope line
In: all statuses above, 3 tile types, Push/Pull/Leap/Root, 4 shapes, trigger set as listed.
Out (post-v1): Charge, high-ground, cones/rows, tenacity, cleanse, summons, morale/rout.

❓ Open with Jake: v1 tile set right? DoT-with-riders confirmed? Anything in "out" he
can't live without? Roster v0.1 predates this grammar — revise kits to use Ground/Push
once grammar settles.
