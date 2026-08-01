---
name: warband-ship
description: Build, publish, and verify the warband Windows client, launcher, and download site — the release targets, the confirm-first gate, and the player-only traps that never show up in the Editor.
---

You are building, publishing, or reporting on a warband release. **Source of truth:
`deploy/README.md`** (pipeline diagram, verified ledger, site setup); scripts are
`deploy/ship-release.sh` (`ship|preflight|status`), `release-all.sh`, `unity-mcp-build.py`.

## ⚠ Confirm-first gate — read before running anything

`make release`, `ship`, `launcher-release` and `site-deploy` are **outward-facing publishes**,
which `~/CLAUDE.md` puts in "confirm first". Ask Jake **every time**; approval for one release
is never standing approval for the next. `make release` also drives the **already-open**
Windows Editor over the MCP relay — it takes the `unity-warband` lease, refuses Play Mode or a
busy Editor, and collides with any parallel session. Never fire it to "see if it works".

**Safe without asking, and they answer most release questions:** `make ship-preflight` (pull +
verify + stage, publishes nothing), `release-status`, `content-version`, `Warband/Build
Preflight`.

## The six targets

| target | what |
|---|---|
| `release` | all-in-one: test → `unity-sim` → wait for sync → build in the open Editor → publish → verify the public manifest. `SYNC_TIMEOUT=` / `UNITY_BUILD_TIMEOUT=` override the waits. |
| `ship` | publish zip + launcher manifest atomically from an existing build. `EXPECTED_VERSION=0.1.x` refuses a stale one. |
| `ship-preflight` | pull + verify + stage. Publishes nothing. |
| `release-status` | what is published now: version, content fingerprint, size, hash. |
| `launcher-release` | cross-compile `WarbandLauncher.exe` into `$(RELEASES_DIR)`. |
| `site-deploy` | rebuild `site/` + `systemctl --user restart warband-site`. |

Builds land in `C:/Users/jwjwi/warband-builds` — **outside the Syncthing tree** — so a
few-hundred-MB player build never syncs back into `git status`. Never redirect into `client/`.

## The `Shader.Find` stripping trap

A shader reached only via `Shader.Find` has no serialized reference, so it is **stripped from
the player build while working perfectly in the Editor** — build succeeds, visuals break in the
player only. Warband closes this with a build step, not a wiki note:
`WarbandBuild.RuntimeShaders` (`client/Assets/Editor/WarbandBuild.cs`) lists eight names (URP
Lit/Unlit + `Warband/Ring GroundFill Sigil Glow Particle Dissolve`) and
`EnsureRuntimeShadersAreIncluded()` runs on **every** build, appending any missing one to
`m_AlwaysIncludedShaders`, warning about what it saved, and throwing if a listed shader is gone.

**A new runtime shader is therefore one line: add its name to `RuntimeShaders`** — never
hand-edit `GraphicsSettings.asset`. Dynamic-resolution callers to re-check: `VfxLibrary` (recipe
names, URP/Unlit fallback), `Tracer`, `Burst`, `ShardEnvironment`, `HallEnvironmentController`,
`DeathSequence`. `Warband/Build Preflight` reports stripping in seconds, not a 20-minute build.

Second, independent player-only trap: in Unity 6000.3 `GameObject.CreatePrimitive` can return
`Hidden/InternalErrorShader` in a player (UUM-136536), so *including* URP/Lit is not enough —
`ReplayPlayer` ignores the primitive's default material and builds its own from the registered
shader. New primitive-cloning code must do the same.

## Saves, fingerprints, secrets

- **Editor and player share the save path.** `RunSaveFile` and `RunTelemetryWriter` both use
  `Application.persistentDataPath` — the same `InhouseBoyz/Warband` folder for both, so a player
  run can overwrite Editor state and vice versa. Establish which wrote last before blaming
  either for a save bug.
- `make ship` refuses on a `contentVersion` mismatch against `make content-version` (ADR 0008)
  — almost always stale `Assets/Plugins/Warband/*.dll` on Windows: `make sync-status`,
  `make unity-sim`, rebuild. Shipping one makes a cross-machine save look corrupted.
  `release.json` is written last and only on success, so a failed build cannot be shipped.
- Launcher token is optional (`WARBAND_LAUNCHER_TOKEN`, baked via `-ldflags`). Per `~/CLAUDE.md`
  never read or print secrets from `.env*` or `~/.config/warband-site/env` — ask Jake for it.
- Pipeline copied in shape from `~/Work/Shoota/Shoota/deploy/` + `.../launcher/`, proven and
  running here. Jake: *"look at what we did for shoota too... we can piggy back off that infra
  if needed."* Check there before inventing pipeline machinery.
