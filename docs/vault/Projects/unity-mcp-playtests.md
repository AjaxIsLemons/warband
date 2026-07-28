# Unity MCP playtests

The committed bridge lives in `client/Assets/Editor/RenderShots.cs`. MCP commands should call its
public methods instead of rebuilding project-specific logic in dynamic command scripts.

## Stable bridge

- `McpEnterPlayMode()` — set every existing Game/Simulator view to **Play Unfocused**, then start
  Play Mode without disturbing the active Windows desktop.
- `McpInspect()` — return the current skirmish flow/result and replay ending state.
- `McpAdvanceSkirmish()` — press `BEGIN FIGHT`, or return from Result to Planning.
- `McpSelectWeapon(heroIndex, weaponId)` — select a hero and issue the real Planning loadout
  action.
- `McpPlaceHero(heroIndex, row, col)` — issue the real field/reserve placement action and rebuild
  the snapshot.
- `McpSwapReserve(fieldHeroIndex, reserveHeroIndex)` — exercise the atomic field/bench swap.
- `McpUndoPlanning()` — verify snapshot history through the live controller.
- `McpTogglePlanningDrawer()` — verify the expandable/collapsed presentation without pixel input.
- `McpVerifyBoardPick(row, col)` — verify screen-to-hex projection without requiring the dynamic
  command assembly to reference `Warband.Sim`.
- `McpSetPlaybackSpeed(ticksPerSecond)` — accelerate a resolved replay.
- `McpStepPlayMode()` — advance exactly one frame on the unattended remote editor.
- `McpPreviewEnrage()` — fold the current in-memory battle directly to the Bond activation tick.
- `McpCaptureGameView(label)` — queue a Game View PNG, including UI, under
  `client/McpCaptures/`; call `McpStepPlayMode()` twice to service its end-of-frame coroutine.
- `McpFlushGameViewCapture()` — repaint the Game View for an unpaused capture. Explicit stepping
  is more reliable when the remote editor is paused.

`client/McpCaptures/` is git-ignored but intentionally not Syncthing-ignored, so the homeserv can
inspect full Game View captures. The existing contact-sheet renderer still writes outside the
Syncthing tree because bulk captures should not travel back into the project.

## Reliable sequence

1. Wait for `make sync-status` to report 100%, refresh assets, and confirm zero compile errors.
2. Enter Play Mode, then use only committed bridge calls for the rest of the run.
3. Inspect Planning, select a weapon, place a hero, swap a reserve, undo, and inspect again.
4. Set a high playback speed only for transition verification; advance to Fight and call
   `McpStepPlayMode()` once. Inspect Result.
5. Advance from Result and verify the committed Planning draft is preserved and history is closed.
6. Use `McpPreviewEnrage()` plus the Unity camera capture for the exact relationship tell when
   inspecting Bond.
7. Stop Play Mode and read the console.

## Remote-editor traps

- Dynamic `Unity_RunCommand` assemblies do not reference the external Warband runtime DLLs.
  They cannot name `Hex`, `BattleEvent`, or other `Warband.Sim` types. Put that logic in this
  committed bridge and call a primitive-typed method.
- The unattended Game View does not reliably free-run. Use `McpStepPlayMode()` instead of sleeps
  when a result or end-of-frame capture depends on Update.
- Never foreground, raise, maximize, or focus Unity or its Game View from MCP. Keep the Game View
  on **Play Unfocused**. The old synchronous compositor-pixel fallback is disabled because it
  interrupted the active Windows desktop; if a background Game View capture stalls, use semantic
  inspection or a camera/RenderTexture capture and report the UI screenshot as unverified.
- Never sync/compile scripts during a Play Mode verification run. A domain reload can preserve
  scene objects while wiping code-built, nonserialized UI state, producing convincing but invalid
  partial captures. Stop Play Mode, refresh, and restart from Preview after any source change.
- `Unity_Camera_Capture` is ideal for board/VFX inspection but excludes screen-space UI Toolkit
  overlays. Use the bridge's Game View capture when UI composition matters.
