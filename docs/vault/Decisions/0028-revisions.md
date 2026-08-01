# ADR 0028 — One watched timeline split is the player's combat intervention

**Date:** 2026-07-28 · **Status:** accepted (Jake, in chat)

## Context

Warband's build and placement decisions are strong, but combat previously crossed a hard
boundary: once deployed, the player only watched. That made time the theme of the world and the
combat grammar without making it the player's verb. A reactive micro layer would erase the
autobattler promise; a loss-only rewind would hide the feature and make it feel like pity.

## Decision

Every battle offers **one Revision**: while watching, the player may pause the local replay,
return one to four whole battle-seconds (six with Long Memory), change one authored fact, and
commit a deterministic branch. If an unused Revision reaches a losing terminal beat, the battle
holds immediately before that beat and offers the same tool once more. There is no Time.timeScale
mutation and no free retry: accepting the original future commits the loss.

A new run chooses one of two lineages:

- **Borrowed Future:** carry at least 15 Mana from one living allied Mana user at the watched
  present into the earlier branch; overflow becomes Shield.
- **Recall to Formation:** return one living enemy to its deployment position and Disarm it for
  15 ticks. It never Silences.

At each Act's Interlude, before its normal reward, the chosen lineage gains one authored
two-option tier. This progression is run-bound and separate from Sand, ranks, equipment, and the
Workbench. The complete upgrade trees live in `Design/revisions.md`.

## Laws

1. **Watch, then revise.** The present and branch must both have been observed in the current
   deterministic battle. Legal targets must be alive and active in both moments.
2. **One split, one authored fact.** No ability buttons, attack-clock manipulation, command queue,
   arbitrary state editing, or second Revision charge.
3. **A branch is real simulation.** The run prepares one immutable opening, simulates the original
   future provisionally, then commits either it or a re-simulation with one
   `TimelineIntervention`. Rewards, earned statuses, node advance, and defeat happen only on
   commit.
4. **The wire stays truthful.** Revision consequences use ordinary Move, Mana, Shield, and Status
   events. `RevisionApplied`, `UnitOmitted`, and `UnitReturned` announce presentation boundaries;
   they do not ask the client to run combat logic.
5. **Local time, not global time.** Pause, scrub, rewind, and branch playback control only the
   replay playhead. Unity's global clock remains untouched.
6. **Readable, not omniscient.** The first timeline shows significant Cast and Death landmarks.
   It does not expose hidden forecasts or promise a perfect outcome preview.

## Consequences

- Amends ADR 0016/0003: placement remains the ordinary order, with one explicit temporal
  intervention after observation.
- Makes time a player-owned differentiator while preserving autonomous combat.
- Requires provisional encounter lifecycle, deterministic branch re-simulation, target validation
  against two folds, save support for lineage/upgrades, and a fight overlay across mouse,
  keyboard, gamepad, and touch.
- The Presentation owns a held-Hour dress, reverse reconstruction, split landing beat, and
  reduced-motion crossfade. Bespoke VFX/audio assets may replace the current native hooks without
  changing simulation.
- Outcome-flip rate, final-chance use, anchors, targets, and lineage/upgrades join run telemetry;
  those results decide future tuning and whether more Revision lineages are justified.

