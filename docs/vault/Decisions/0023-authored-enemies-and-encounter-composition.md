# ADR 0023 — Authored enemies, composition as the act lever, and the disclosure contract

**Date:** 2026-07-25 · **Status:** accepted (Jake: *"enemies should all have their own designs. its
ok to lift kits for simplicity for now, but im sure we will want to make interesting pve encounters
that dont follow all the same rules"*) · **Participants:** Jake + Claude

## Context

Roadmap item 2 was the biggest gameplay gap: **deployment worked but did not MATTER**, because
`Catalog.Encounter` returned random hero kits at 60% stats and `Catalog.Boss` returned the same
bonded pair for all three acts. Five different fights all posed the same problem, so there was
nothing for placement to answer. ADR 0022's behavior layer removed the technical blocker — an enemy
that reaches past a front line, holds distance, or hunts the weakest is now pure data.

## Decisions

### 1. Enemies are authored UnitDefs, not composed hero kits

A monster has no chassis, no rank, no weapon and no spec tree. It is a stat block + a role behavior
+ at most one disclosed rule. This is what lets a swarm body be 70 HP with nothing else going on,
and it stops "difficulty" from meaning "a Cleric at 300% health".

Five roles, each stressing a different axis of roster.md's coverage law:

| Role | Unit | The problem it poses |
|---|---|---|
| Swarm | Hourling | more bodies than you have answers |
| Anchor | Ashen Colossus | a wall you cannot burst |
| Artillery | Sanddrift Gunner | it shoots **past** your front line |
| Ritualist | Hour-Scribe | a clock that beats you if ignored |
| Diver | Gloamstalker | it is already in your backline |

Kit-lifting stays legal where it is simply cheaper (the Gloamstalker's opening Leap is the Shade's
Ambush) — the rule is that the *design* is authored, not that the grammar must be novel.

**`ChassisId` on an enemy is a RENDER KEY, not a claim of identity** — the sim never branches on it
(UnitDef's identity block). Roles borrow the silhouette that reads closest to their shape so five
roles are five distinct bodies today; bespoke enemy art is a later pass.

### 2. Composition is the act's primary difficulty lever; stats are secondary

ADR 0016 says difficulty adds new pressure before it adds raw stats. Encounter factories therefore
take the act and size themselves: The Gnawing Hour is 5 bodies at act 1 and 10 at act 3; The Ninth
Bell teaches its ritual alone before putting a wall in front of it; The Drop gains a third knife.
`Encounters.Scale` only tilts the same fight, and lives in ONE place so the shipping catalog and the
authoring probe can never measure different games.

**An act's pool is its identity** (theme.md: acts are eras). The Long Range is act 2+ because
measured against a rank-C opening warband it is unwinnable from every formation, and from act 2 it
is the sharpest encounter in the pool. That is an act-placement fact, not a number to flatten.

### 3. One authored rule may bend the shared model — and it must be disclosed

pve-encounters.md already allowed an inspectable passive that names its verb. Two exist:

- **WARD** (Colossus): 50% damage reduction while any escort lives, stripped on the first escort
  death. The answer is "kill the escorts first", which fights the instinct to focus the biggest
  threat. Not an immunity — every shared verb still lands.
- **RITUAL** (Scribe): its mana is fed by the trickle **alone**. This needed a real sim change —
  `UnitDef.ManaPerHitTaken`, per-unit, mirroring ADR 0022's `ManaPerSwing`. On the global hit-fed
  rate a channeller fires the instant it is focused, which *inverts* the problem by punishing the
  obvious answer. As pure time it is a countdown the player reads off the mana bar and answers four
  ways: out-damage it, reach it, Silence it (no mana gain), or Stun it (both clocks stop).

### 4. The brief is a run-layer contract

`IRunContent.EncounterBrief` / `BossBrief` + `RunController.PreviewBrief`, derived from the **same
private salt** as `PreviewEnemies` for the same reason: a brief describing a different encounter
than the one that spawns is worse than no brief. Text only — the run layer never forecasts an
outcome ("know the rules, not the result").

## Consequences

- **Measured, not guessed.** New `Warband.Sweep --enc` probe: win% and formation SPREAD per act per
  encounter, whether each rule actually fires, and how the naive bot line fares. It immediately
  caught three of four encounters being decoration and the Ninth Bell's ritual never firing at all
  (the countdown was longer than a fight). Verdict bar is spread, not win rate.
- All four encounters now pose a placement problem at their debut act (spread 100 for The Long Range
  and The Drop; the two act-1 teachers are gentler by design).
- **The game got much harder.** Bot tier EV moved from 88/92/79 victory to **35/48/39** — and
  Fraying now beats Stable, because Sand buys survival. Roadmap item 4 should re-read that before
  its DESIGN pass; nothing here targeted tiers.
- `FullRunsCompleteOnRealContent` no longer asserts the bot always wins. Against authored content
  plus terminal loss that would mean the PvE poses nothing; it now asserts the machine always
  completes, the arc is reachable, and it is not free.
- `RunHarness.StarterWarband` drafts a plausible comp instead of `pool[0..2]`. The arbitrary one it
  had landed on (Cleric + Bulwark + Shade) has a heal-auto and a Tower Shield — one real damage
  source in the whole warband — and lost the first fight of every run, which said nothing about the
  encounters.

## Still open

- **Client disclosure UI.** The brief reaches the run layer; no shell screen renders it yet. Until
  it does, the "know the rules" law is only half kept.
- **Bosses.** `Catalog.Boss` still returns the act-scaled Last Oath for every act. The role grammar
  now makes a real act boss cheap to author.
- **Bespoke enemy art**, and per-role cast/attack tells.
- Risk-tier mutation of authored encounters (tiers currently only scale stats).
