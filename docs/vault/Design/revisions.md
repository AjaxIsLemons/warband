# Revisions — watched time as the player's combat verb

**Status:** first complete vertical slice · **Decision:** ADR 0028

## Player contract

Each battle begins with one unspent Revision. From one battle-second onward, **REVISE** holds the
board at the currently watched moment. The player chooses a whole-second rewind, studies the board
at that branch, selects a legal unit, and commits. The watched future is then visibly unwritten and
the deterministic battle runs again from the branch.

The player is changing one fact, not directing the warband:

- no attack-clock controls;
- no ability timing buttons;
- no repositioning allied heroes during the fight;
- no forecast of the branch outcome;
- no repeated fishing for a better branch.

Cancelling before commitment resumes the original future and preserves the charge. Once committed,
the Revision is spent even if the revised outcome is worse.

If the original future loses with the charge unspent, playback freezes immediately before the
fatal event. The player may revise from that observed present or **Accept Fate**. This is the same
charge and the same rules, not a second life.

## First two complete lineages

### Borrowed Future

Base: select one allied Mana user alive at both present and branch. Carry the greater of 15 Mana
or its positive present-minus-branch Mana difference into the branch. Mana caps normally; excess
becomes Shield.

| Act | Option A | Option B |
| --- | --- | --- |
| 1 | **Shared Premonition:** nearest other allied Mana user gains half the carried Mana. | **Deep Reserve:** minimum carry becomes 25. |
| 2 | **Clear Intention:** directly revised champions shed Silence and Disarm. | **Long Memory:** maximum rewind becomes six seconds. |
| 3 | **Convergence:** revise up to two allied champions. | **Afterthought:** the first revised signature refunds half the Mana actually added. |

### Recall to Formation

Base: select one enemy alive at both present and branch. Cancel its movement, return it to its
original deployment hex (or the nearest deterministic legal hex), and Disarm it for 15 ticks.
Recall never Silences, so dangerous enemy signatures remain dangerous.

| Act | Option A | Option B |
| --- | --- | --- |
| 1 | **Fixed Point:** Root the primary target for 15 ticks. | **Long Peace:** primary Disarm lasts 25 ticks. |
| 2 | **Roll Call:** recall the nearest second enemy and Disarm it for 10 ticks. | **Empty Hands:** primary target returns with zero Mana. |
| 3 | **General Recall:** return every living enemy; secondary Disarms last 10 ticks. | **Missing Hour:** omit the primary enemy for 20 ticks, then return it Disarmed. |

An Omitted unit remains alive for victory checks but cannot act, occupy a hex, be targeted,
receive effects, or run personal rules. Its status clocks pause. This prevents Missing Hour from
creating a false instant victory or consuming statuses while the unit is outside the Hour.

## Run progression

After Muster, **First Draft** presents both full lineages: base rule plus all six named evolutions.
This is a strategic commitment, not a surprise draft. Each Act's Interlude first blocks on the
next authored two-option Revision evolution, then reveals the existing Treasury / Armory /
Hourstone reward. Revision growth spends no Sand and grants no account-scoped power.

Save data carries the chosen lineage and ordered upgrade IDs. A prepared, uncommitted combat is
deliberately transient: closing during playback resumes at the same pre-fight node rather than
serializing a half-watched future.

## Combat presentation

### Ready

A quiet bottom-center instrument names the lineage and says **ONE SPLIT REMAINS**. It does not
compete with the board. Keyboard `R`, gamepad north, and touch/click open it.

### Held Hour

- The replay playhead stops locally; no global timescale.
- Lighting dims, saturation falls, and the vignette closes around the board.
- The first opening in a battle is a 0.9-second full-screen rupture: combat audio vacates, a sand-
  gold clock arrests over the board, and one art-directed temporal fault separates the world into
  broad slipping plates. Its hierarchy is one black primary chasm with hot sand-gold rims, a small
  set of subordinate hairline fractures, and a short central impact bloom—not an equal-weight
  spiderweb. The fault is screen-composed; small filaments identify selected subjects.
- Cancelling preserves the charge. Reopening uses an abbreviated 0.18-second crack rather than
  replaying the entire reveal; the first rupture is a discovery beat, not a tax on exploration.
- A compact timeline shows the reachable window and significant Cast/Death landmarks.
- Whole-second anchors are explicit buttons, with arrows/d-pad cycling.
- Scrubbing reconstructs the authoritative fold without replaying attack or death FX.
- Legal units receive ground rings; selected units receive the wider ring.

### Rewind and split

Commit is a scored sentence on an unscaled presentation clock. Battle speed never makes the
flagship moment hurried or sluggish:

| Beat | Default | Read |
| --- | ---: | --- |
| tear | 0.25 s | restore the watched present; **THE WITNESSED HOUR IS NO LONGER TRUE** |
| rewind | 1.35 s + 0.10 s per extra selected second, cap 1.85 s | the rejected witnessed future remains on one side of the fault while the board folds continuously backward on the other; sand runs against the clock |
| vacuum | 0.10 s | remove motion and most copy immediately before the new branch |
| landing | 0.30 s | dispatch the revised branch once; target flash, T3 world VFX, camera punch, lineage sting |
| receipt | 0.55 s + 0.35 s tail | freeze long enough to read only the state differences the two authoritative folds prove |

The landing has shared sand/glass time language and a lineage-specific answer:

- **Borrowed Future:** sand resolves into mana-blue bloom, rising glass, and the actual Mana,
  Shield, Silence, or Disarm delta.
- **Recall to Formation:** sand contracts into ash-violet sigils and falling grit; the receipt names
  returned position, Disarm, Root, Omission, or removed Mana as applicable.

Both landings snap lineage-colored rings onto the affected subjects before the fault seals. Those
rings and the world landing recipe are the loud answer to “what changed”; the fullscreen fault
remains shared time grammar rather than pretending to prove a combat consequence.

The receipt is not predictive copy and does not infer combat rules. It compares original and
revised `PlaybackState` folds at the branch, restricted to units carrying `RevisionApplied`, and
prints at most four proven changes. Ordinary consequence events still tell everything after the
landing. `UnitReturned` has its own smaller sand/violet return punctuation.

Reduced Motion replaces spatial reverse playback with a 0.55-second crossfade to the branch,
removes camera shake, shortens the rupture/landing, and holds the receipt for 0.70 seconds. It keeps
the same information and sound hierarchy.

### Presentation implementation boundaries

- `RunShell` owns the unscaled ceremony state machine and input/chrome lock.
- `RevisionCombatOverlay` owns the clock, cinematic copy, timeline instrument, light UI scrim, and
  branch receipt. It never paints the world fracture.
- `RevisionScreenEffect` bridges the ceremony to `RevisionFractureRendererFeature`: the URP pass
  captures the restored witnessed present, composites it against the live rewind after
  transparents/before post-processing, and releases the texture at Receipt.
- `ReplayPlayer` owns fractional fold rendering, exact pre-branch staging, once-only branch
  dispatch, selected-target viewport projection, world landing recipes, camera dress, and resume.
- `SfxPlayer` owns a highest-priority, non-ducked `Revision` bus plus one dedicated looping bed.
  Opening stops ordinary board voices, not UI or Revision voices.
- All durations and intensity controls live under `tuning.json → revision`; edge width/glow,
  refraction, plate slip, chromatic split, future opacity, held seam, and sand flow hot-reload
  independently. No VFX timing is tied to ticks or `Time.timeScale`.

## Deterministic lifecycle

1. `PrepareFight` captures composed players, earned statuses, enemy definitions/positions,
   team triggers, stable instance IDs, and battle seed.
2. It simulates but does not commit the original future.
3. The client plays that result and may build arbitrary read-only `PlaybackState` folds.
4. A Revision choice supplies present tick, branch tick, and target IDs.
5. The run layer independently validates distance, sides, liveness, target count, and Mana use;
   derives carried Mana from the original present fold; and re-simulates the same opening with one
   `TimelineIntervention`.
6. Exactly one of `CommitOriginal` or `CommitRevision` applies earned progression, income, node
   advance, boss flow, or defeat.

The simulation injects the intervention before normal arrivals, decisions, and actions at the
branch tick. Event consequences remain absolute fold facts. This keeps replay, telemetry, combat
recap, and later server verification on the same deterministic contract.

## Initial tuning and telemetry

- charge: one per battle;
- normal rewind: one to four seconds;
- Long Memory: one to six seconds;
- Recall Disarm: 15 ticks; Long Peace 25;
- Missing Hour: 20 ticks;
- no attack-clock values or attack-timing language.

Telemetry records lineage, upgrade choices, proactive versus final-chance use, present/branch
ticks, target IDs, original/revised outcome, and outcome flips. The first questions are:

1. Do players use Revision proactively, or hoard it for the terminal hold?
2. Which anchors create understandable changes rather than noise?
3. Does either lineage flip outcomes far more often, or merely feel more legible?
4. Do upgrade pairs change target/anchor decisions?
5. Does the 1.35–1.85 second reverse remain readable at 0.5×/1×/2× battle speed, and does Reduced
   Motion preserve the same causal understanding without spatial reverse playback?
