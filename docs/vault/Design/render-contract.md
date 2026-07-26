# Render contract — "the sim owns time; the renderer decorates it" (2026-07-22)

How the Unity client is guaranteed to show what the sim actually decided. Modeled on
Hearthstone's tag-change protocol, TFT's server-truth timing, and circuit's shipped
replay viewer + guardrail.

## The contract
1. **Single source of truth.** The client consumes ONLY (initial snapshot, event log).
   It runs zero combat logic — ever. (Circuit's law, kept.)
2. **Tick → clock mapping.** 1 tick = 100 ms presentation (fast-forward = multiplier).
   Every event carries its tick; every state-visible change (bars, deaths, positions)
   is presented AT its tick-time. Within-tick cascades play as ordered micro-stagger
   inside the 100 ms window (drain order is the animation order).
3. **Bars are SET, never accumulated.** HP/Shield/Mana displays snap/tween to the
   absolute post-state carried on each event (PostHp/PostShield/PostMana). A dropped
   or lagged animation self-corrects on the next event — stale for a beat, never drift.
4. **Gameplay time lives in sim ticks only.** Attack cadence is real (Attack events at
   exact ticks — a Hasted unit's events are simply denser; the renderer fits its swing
   animation into the gap). If cast wind-up or projectile flight ever affects outcomes,
   it becomes a scheduled sim effect (deferred-effect queue) — the renderer may
   *decorate* time (sparks, shake, squash), never *create* it.
   **v1 decision: projectiles are instant in sim** — drawn as fast tracers inside the
   tick window. Post-v1 lever (flagged, real design space): sim-modeled flight time on
   a movement board = dodge-by-repositioning gameplay, TFT-style impact-time damage.
   *Movement pulled this lever 2026-07-24 (ADR 0018).* A step is no longer applied the tick
   it is decided: a unit **departs** (`MoveStart`) and **arrives** `MoveInterval` ticks later
   (`Move`), standing on its origin the whole way with its destination reserved. Travel time
   changes when a unit arrives, so it had to become sim truth — the renderer only interpolates
   across the window the sim declares, and lands exactly on the arrival tick.
   **A `Move` with no preceding `MoveStart` is a teleport** (Leap): slide vs blink, one rule.
5. **Causality grouping.** Depth + Root + contiguous drain order = Hearthstone's
   BLOCK nesting: the renderer can sequence "the counter flash follows the hit that
   caused it" without guessing.
6. **One tell per event SIGNATURE.** Every distinguishable event gets exactly one canonical
   visual signature (autobattle's readability lesson). No silent mechanics.
   *Refined 2026-07-24:* the unit is the signature, not the bare kind — a tell declares an
   EventKind plus optional `Cause` / `StatusKind` / `FieldFlavor`, and the most specific rule
   wins (filterless = fallback). `DamageDealt` alone was too coarse: a burn tick, a sword hit
   and a crit must read differently, and a healing glyph must not be colored like a fire one.
   Matching lives in `Warband.Sim.TellMatch` (tested); tells are authored in `tuning.json`.

## The load-bearing trick: the fold IS the view-model
The log-reconstruction fold (`PlaybackState`: events → per-tick unit states) lives in
**Warband.Sim**, pure C#. Three consumers, one implementation:
- the sim-side guardrail test (fold == live sim state, every tick — circuit's
  TestLogReconstructsState),
- **the Unity render driver** (the client renders FROM the fold's output),
- FightStats/metrics.
Accuracy becomes tautological: the state on screen is the tested fold's state.

## Verification ladder
1. Sim-side: reconstruction guardrail test (fold vs live state per tick). CI, forever.
2. Client-side: golden replay corpus checked into the repo; Unity edit-mode tests run
   the same fold + playback scheduling headlessly and assert final bars == truth.
3. Feel/visual: Unity MCP native captures at fixed replay timestamps (Shoota SOP) —
   screenshot diffing for regressions, eyes for juice.

## How others do it (research 2026-07-22)
- **Hearthstone:** server resolves instantly; client receives TAG_CHANGE packets
  (absolute values), BLOCK_START/END nesting for causality, META_DATA packets that are
  pure animation guidance. Animations pace themselves; state is already decided.
- **TFT/League:** real-time server-truth; wind-ups/projectiles are champion-kit
  cosmetics anticipating server-timed damage application.
- **Circuit (in-house):** event log with delta+absolute, pure replay-viewer client,
  reconstruction guardrail — deployed and it worked; we inherit the model.
