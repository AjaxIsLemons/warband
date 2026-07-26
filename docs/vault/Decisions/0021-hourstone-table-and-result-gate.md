# ADR 0021 — Hourstone Table and result-gated management flow

**Status:** Accepted · **Date:** 2026-07-25

## Context

The first full-screen Management Hall still behaved like a generic admin application: a left
drawer selected four tabs and each tab poured different data into the same card grid. It was
functional, but it did not give the run a place, teach spatial memory, or explain what changed
between a fight and the next wager. Combat playback also exited directly into Management, erasing
the fight summary at the moment it appeared.

## Decision

1. Between fights, the run lives at a spatial **Hourstone Table**. Breach is north, Market west,
   Armory east, Warband south, and Hourstone at the center. This geography is stable.
2. A completed or skipped fight always opens a blocking result report over the frozen battlefield.
   It shows the exact Sand receipt, enemies felled, top player damage, and player death causes.
   Watching again replays the stored `BattleResult`; it never resolves the fight again.
3. Continue is contextual. Terminal runs go to the run result; rank and boss choices block first;
   ordinary victories recommend the refreshed Market.
4. Station sequencing is a client presentation concern. A before/after run snapshot produces a
   prioritized event plan; `RunController` and deterministic simulation remain unchanged.
5. Stations share navigation but not information architecture: Market compares offers, Warband
   separates field/reserve, Armory previews exact equipment deltas, and Hourstone displays authored
   run-wide laws.
6. Desktop mouse/keyboard and **landscape phone/tablet touch** are first-class. Nothing essential
   is hover-only, selection never commits, safe areas are honored, primary touch targets are at
   least 56 logical pixels, and body copy is at least 16 logical pixels. Portrait rotation is not
   supported for the first playable.
7. Motion expresses destination and cause, uses transform/opacity, is cancellation-safe, and has a
   reduced-motion replacement. Semantic cue events remain the seam for later audio/haptics.
8. Meta progression remains no-power and deferred. The client may emit a nonpersistent run
   conclusion receipt, but this decision adds no save system, currency, unlock, or Archive screen.

## Consequences

- The old left drawer is removed.
- The fight result can be read and replayed without mutating the run.
- Hall presentation order and timing can change without changing economy or combat rules.
- Mobile is a responsive composition and interaction contract, not a scaled desktop screenshot.
- A development Flow Lab can preview routes, reduced motion, and the phone composition without
  playing a complete run each time.
