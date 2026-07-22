# ADR 0008 — Run layer architecture: pure library, host-agnostic state

**Date:** 2026-07-22 · **Status:** accepted · **Participants:** Jake + Claude

## Context
Jake, kicking off the run-layer build: where does run data live — shouldn't the server drive
most things? Client-first-then-server sounded like a rewrite headache. The end state is right:
anything touching other players (ghost pool, records, ladder) must become server-authoritative.
But the rewrite headache only exists when game logic is tangled into client code — which our
architecture already forbids for combat. This ADR extends the same law to the run layer so the
storage/hosting question stays a deployment detail, not a design fork.

## Decisions
1. **The run layer is a second pure C# assembly (`Warband.Run`)** under the same laws as
   `Warband.Sim`: deterministic, seeded, headless-testable, zero Unity / network / filesystem
   references. The client is placement UI + renderer; it contains no run rules.
2. **`RunState` is serializable-by-construction:** plain data, content referenced **by id**
   through an `IRunContent` catalog — state never holds object graphs into content. Save =
   serialized state; replay = (seed, choice log, contentVersion), same law as combat.
3. **Stateless randomness:** every run-layer roll uses a fresh PCG32 seeded by
   mix(runSeed, purpose, act, node). No RNG state to persist, no ordering coupling between
   decisions.
4. **Hosting is deferred, not decided by code shape.** P0: state lives in a local save file —
   friends playtest with bot-ghosts needs zero server. The ghost pool (roadmap 5) is the first
   real server surface: snapshot store + matchmaking. If/when ladder integrity demands it, the
   same assembly rehosts server-side ("server drives") with no rewrite — that door is the
   point of this ADR.

## Consequences
- The Unity client may never import run rules; it feeds choices in and renders state out.
- Cheating is possible while runs are client-hosted — irrelevant for friends playtest #1
  (ADR 0001 priority); hash-verified re-sim + server rehosting are the ladder-era answers.
- The full-run harness (roadmap 1e) and archetype sweep drive `RunController` directly.
