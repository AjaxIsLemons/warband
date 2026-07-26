# Unit identity & fight story — "who is anyone, what happened" (spec, 2026-07-24)

Companion slice to [[directed-tells]]. Tells now show WHO acts on WHOM — but every actor is an
identical capsule and the fight ends by silently looping. Jake's verdict: "you can't really tell
what's going on at all." This pass fixes the two remaining comprehension deficits that don't
require any run-loop design: **identity** (silhouettes, nameplates, facing, tooltip) and **story**
(kill feed, win banner, post-fight readout, scenario picker).

**Zero sim changes.** `PlaybackUnit.Name` already carries the chassis name (`Battle.ViewOf` →
`u.Def.Name`; compose sets `Name = chassis.Name`), and `FightStats` (Warband.Sim, tested) already
folds events into per-unit damage/kill totals the client can call directly. Deeper tooltip data
(weapon, range, path picks) would need snapshot enrichment (replay v3) — deliberately deferred.

**Out of scope:** decoupled clock/beats · PrimeTween · shake/hit-stop · run outcomes (gated on
item 5 DESIGN) · replay v3 snapshot enrichment · sound.

## A. Chassis silhouettes (render-polish's silhouette-first law, finally built)

Key on `PlaybackUnit.Name`, matched case-insensitively; unknown names keep today's plain capsule
(enemy scaffolding uses the same 8 chassis, so coverage is total). Silhouette language:
**round/wide = tank · spiky/lean = DPS · tall/thin = caster · tall accessory = support.** All
primitive composites parented to the unit's Body so flash/punch/lunge inherit; accessories in
DESATURATED neutrals (palette law: bright colors stay reserved for VFX) with the body keeping the
team color.

| Chassis | Body proportion | Accessory (primitives) |
|---|---|---|
| Bulwark | wide, squat | front shield slab (flattened cube) |
| Phalanx | wide-ish | long spear (thin cylinder, ~30° forward) |
| Berserker | bulky, mid | two angled shoulder blades (small cubes) |
| Shade | slim, slight forward lean | two dagger prisms (small thin cubes) at hips |
| Sharpshot | slim | bow stave (thin horizontal cylinder held out front) |
| Pyromancer | tall, thin | staff (cylinder) + small orb sphere at its tip |
| Cleric | mid | flat halo disc (squashed cylinder) floating above head |
| Banneret | mid | banner pole (cylinder) + flag (thin cube) — the tallest thing on the board |

Constraint: shapes must read at the iso camera distance where a unit is ~60-90 px tall — verify in
RenderShots captures, not up close. Bar/pip heights may need a small per-chassis Y offset so tall
accessories don't collide with them.

## B. Nameplates + facing

- **Nameplate:** pooled world-space `TextMesh` (FloatingNumber idiom — same font path), chassis
  name, billboarded with `_numberFace`, sitting just above the HP bar. Tunable via new
  `NameplateTune` in TuningData (`show` toggle · size · color) → F1 sliders for free.
- **Facing:** rotate the Body yaw (slerp ~10/s, decorative only — the fold still owns position):
  - while the fold position is changing → face the movement direction;
  - on `Attack`/`Cast` handled by the Director → snap-turn toward the captured target endpoint
    (the Director already has both endpoints; expose a `FaceTarget(uid, worldPos)` hook on the view).
  - No sim TargetId needed — event-driven facing is honest enough for v1.

## C. Hover tooltip v1 (play mode)

- **Picking: screen-space nearest, NO colliders** (MakePrimitive strips them; don't re-add).
  Project live unit positions via `Camera.main.WorldToScreenPoint`, pick the nearest within
  ~48 px of `Mouse.current.position` (new Input System, same as DebugMenu).
- **Panel:** small runtime UI Toolkit card (own `UIDocument`, sortingOrder below DebugMenu's 1000),
  built in code like DebugMenu (no UXML assets): Name · team chip · HP x/max · Shield · Mana x/max ·
  one line per live status (`Kind ×Mag`). Data read straight from the fold each frame while hovered.
- Known verify limit (memory: runtime UI isn't self-capturable over MCP): I verify data + compile;
  Jake eyeballs the card. Keep the card's data path trivially thin so static review suffices.

## D. Fight story: kill feed · win banner · readout

All world-space pooled `TextMesh` (NOT screen-space UI) so RenderShots captures can verify them —
anchored to board-edge world positions, billboarded like numbers.

- **Kill feed:** on `Death` events (Director already handles them) push "«Killer» felled «Victim»"
  (+" — overkill N" when Amount > 0); show the last ~4 lines, fading out after a tunable lifetime.
  Names from the fold; killer may be -1 (storm) → "The storm claimed «Victim»".
- **Win banner + readout:** when the playhead reaches the end tick and `loop` is on, **hold** for a
  tunable `endHoldSeconds` (default ~4 s) before wrapping: show "BLUE/RED WINS" (surviving team; a
  draw shows DRAW) plus a compact readout — top 3 damage dealers and each side's kills — computed
  via the existing `Warband.Sim.FightStats` fold (no client math; check its actual API surface
  first and use what's there rather than re-deriving).
- New `StoryTune` in TuningData: feed lifetime/size · banner size · endHoldSeconds → F1 for free.

## E. Scenario picker + walls fixture

- **Picker:** a row in the DebugMenu toolbar (next to battle speed — this IS a menu feature, so
  editing DebugMenu.cs is in scope here, unlike tell work): dropdown of
  `StreamingAssets/replays/*.bytes` + current selection; choosing one sets `ReplayPlayer.replayFile`,
  reloads, restarts playback. Also `[` / `]` keys cycle scenarios (Input System, ignored while the
  search field is focused).
- **Walls fixture:** new `wallfort` scenario so `AttackBlocked` actually fires (today NO fixture
  emits it and the blocked-shot fizzle tell is unexercised). *Corrected 2026-07-24 (agent finding):*
  **no content path creates `IsWall` fields** — pikewall/faultline are thematic names for
  Counter/AoE effects; only tests build walls, via `Battle`'s `initialFields` parameter. So walls
  become a Viewer scenario feature: optional `"walls": [{"row","col"},…]` on ScenarioDef
  (Scenarios.cs), mapped to `initialFields` with the FieldTests wall shape. Dev fixture tool, not
  game content — content doctrine untouched. `make scenarios` round-trip + `make coverage
  F=.../wallfort.bytes` listing AttackBlocked ≥ 1 are the success gates.

## Build order (2 sequential agents, shared-file conflict avoided)

1. **Agent "identity-board":** silhouettes (A) · nameplates + facing (B) · walls fixture (E2).
   Files: ReplayPlayer.cs (SpawnView/UnitView/Update), TuningData.cs (NameplateTune), new
   Nameplate helper if warranted, scenarios.json. Verify: `make scenarios` clean + coverage shows
   AttackBlocked · compile · RenderShots sheet shows 8 distinct silhouettes + nameplates + facing.
2. **Agent "fight-story":** kill feed + banner/readout via FightStats (D) · tooltip (C) ·
   scenario picker (E1). Files: ReplayPlayer.cs, TuningData.cs (StoryTune), new Tooltip.cs,
   DebugMenu.cs (toolbar row only). Verify: compile · targeted capture at a Death tick shows the
   feed line · end-hold capture shows the banner · tooltip/picker = static review + Jake's F1 pass.

Both: re-read every file before editing (Syncthing drift) · no .meta/scene edits · no commits ·
captures outside the tree · existing 186 sim tests stay green (`make test` — the scenarios change
rebuilds fixtures, so run it).

## Shipped 2026-07-24 (deltas from spec)

Built same-day by two Opus agents (identity-board → fight-story) + orchestrator review; verified
via contact sheet + targeted captures. Deltas that matter:
- **Walls correction** (§E, already folded above): no content creates IsWall — `walls` is a Viewer
  scenario feature into `Battle.initialFields`. wallfort emits AttackBlocked ×12.
- **Sharpshot bow is a vertical stave** (agent's call — reads better at iso than the spec'd
  horizontal); flip after Jake's eyeball if it reads wrong.
- **Body is a container** (localPos 0, scale 1): torso capsule + accessories are its children;
  punch scales the container, facing yaws it, flash tints only the torso, lunge stays on Root —
  all four compose.
- **Kills in the readout** = Death events grouped by killer team (matches the feed 1:1);
  `UnitFightStats.Kills` is participation-counted and deliberately not used.
- **One compile error total**: Tooltip had a `Position(Vector2)` method shadowing the UIElements
  `Position` enum (CS0119) — renamed to `PlaceAt` by the orchestrator.
- The win banner shows whenever the playhead reaches the LAST EVENT tick (e.g. stomp ends ~t24);
  fights whose events stop early hold the banner from there — correct, but reads oddly in
  mid-number captures.
- **Not eyes-verified:** tooltip card + picker (runtime UI, needs Jake's focused editor) ·
  blocked-shot fizzle (stills never landed on a block tick; same executor as verified tracers) ·
  nameplate F1 sliders. Cosmetics logged: nameplate clutter at density · center pile-up at fight
  end (banner/readout/numbers overlap) — sizes all F1-tunable.
