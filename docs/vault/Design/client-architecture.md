# Client — architecture, and where the bodies are buried

Moved off the roadmap 2026-07-28 (actionable-only re-cut); this is REFERENCE, kept current by
whoever changes the client. History: `Projects/roadmap-done-archive.md` + `Daily/` notes.

Bring-up (item 4), render polish (4b) and the PoC shell (4c) are built and verified; full history in
`Daily/2026-07-23` and `Daily/2026-07-24`. What a future session actually needs:

- **Architecture:** `GameBoot` owns startup order (add a line there — **never** another
  `RuntimeInitializeOnLoadMethod`; four competing ones is why two UIDocuments once raced for input).
  Scenes are **Boot(0) → Game(1)**; Boot holds one `~BootLoader` and nothing else.
  `ReplayPlayer.autoPlayOnStart` is **OFF** by default — the board is driven, never self-starting,
  and the shell parks it on any transition to a non-board screen.
- **Shell pattern (extend, don't fork):** `RunShellModel` (plain view-models) · `IRunScreenView` +
  `RunShellActions` (render out, intent in) · `RunShell` (router, and the ONLY place content ids
  become words). Flow is `ManagementView` → `WagerView` → `DeployView` → board-only Fight;
  management still shares one plain `PlanningModel`.
  `PresentationCatalog` owns art/copy/icon references; composed `UnitDef` owns every mechanical
  number. `WarbandCard` and `InspectorPanel` are shared UXML-backed renderers;
  `PlanningWorkspaceStyles.uss` owns layout/motion tokens.
  **Views may not reference `Warband.*`, so a raw id physically cannot reach the UI.**
  The opening draft is the deliberate exception to universal-card reuse: `MusterCard` accepts only
  three facts and two rules, and exact mechanics disclose inside its portrait.
- **Hydration:** `Warband.Sim.Lexicon` (27 StatusKind + 9 Cause) and `Warband.Content.ContentLexicon`
  (78 spec nodes + 8 chassis). `LexKind` is a **domain, not a valence and not a colour** —
  battlefield colour has one owner, the signature-matched tells in `tuning.json`.
- **Tuning:** `StreamingAssets/tuning.json` + F1 debug cockpit (auto-generates sliders by
  reflection). Hot-reload, no recompile. F2 is the Flow Lab preview.
- **Reused USS across shell and Planning:** inherited *position* is context, inherited *paint* is
  language — override the first, keep the second.

### Client gotchas — these have each cost real time
- **PLAY MODE IS UNREACHABLE FROM A SESSION (found 2026-07-26).** `EditorApplication.EnterPlaymode()`
  inside `Unity_RunCommand` is refused outright: *"User interactions are not supported for MCP tool
  calls."* So **no agent can ever click through the runtime UI** — anything existing only in Play Mode
  (button wiring, shell state transitions, frame-driven feel) is **Jake-only verification, full
  stop.** The workable substitute is a **committed edit-mode Editor script + `ExecuteMenuItem`**
  exercising the real DLLs (`Assets/Editor/RunSaveCheck.cs`, `RenderShots.cs`).
- **`Unity_RunCommand` rejects `System.Reflection`** and its dynamic assembly cannot reference
  Warband plugin types — which is *why* the harness must be a real Editor script. Editor scripts live
  in Assembly-CSharp-**Editor**, so they cannot see `internal` types in Assembly-CSharp either.
- **Refreshing scripts mid-Play-Mode** leaves GameObjects alive but **wipes the code-built UI tree**
  (root has 0 children, console clean). Exit and re-enter Play after every source change.
- **An unfocused Editor idles the player loop**, so `Start()` may simply never have run — same
  symptom, different cause. Pump `QueuePlayerLoopUpdate` before believing a probe. **Sometimes it
  does not recover at all** (2026-07-24: `frameCount` stuck at 1, 40 queued pumps, `isPaused=false`).
  **Verify anything frame-driven in EDIT mode** via `BuildPreview(tick)`, measuring against the
  generated tile transforms as a hex-centre yardstick. Never `Thread.Sleep` in a
  RunCommand to wait for frames — it holds the main thread, so nothing ticks and every sample reads
  identical, mimicking the very bug you are chasing.
- **Exiting Play returns the Editor to the Boot scene**, which holds only `~BootLoader` — so an
  edit-mode probe finds no `ReplayPlayer`. Open `Assets/Scenes/Game.unity` first.
- **Game View capture stalls unattended** (`WaitForEndOfFrame` never completes). Driving the live UI
  tree over MCP is the reliable verification; screenshots need Jake's focused Editor.
- **The Editor and the built player share `Application.persistentDataPath`** — a dev Play Mode session
  and a friend build read/write the SAME `run.save`. That is why a fresh build can appear to "already
  have a run."
- **A check that can silently return "nothing found" needs a positive control.** Cost real time twice:
  a shader grep for invented names reported all six missing, and a `dig` that wasn't installed.
- **Syncthing ignores are PER-DEVICE**, and `sync-status: 100%` can predate the local scan — confirm
  over SSH. **Never write captures inside the Syncthing tree** (they sync back into the repo).
- **`.unity`/`.prefab`/`.asset` edits are guard-hook blocked** — scene work goes through Unity MCP.
