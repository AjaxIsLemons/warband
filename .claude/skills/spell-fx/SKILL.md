---
name: spell-fx
description: Author or change warband combat visuals (spells, weapon attacks, fields, statuses, deaths) — the tell/recipe/shader workflow, verification gates, and gotchas.
---

You are adding or changing combat FX in warband. **Source of truth:
`docs/vault/Design/authoring-combat-fx.md`** (workflow + next-steps ledger), with
`combat-spectacle.md` (palette lanes, T0-T3 tiers, per-signature direction) and
`fx-runtime.md` (engine laws). Read the authoring doc first; this skill is the
operational checklist.

## Generated source art

Use `.claude/skills/warband-art-pipeline/SKILL.md` for generated sigils, masks, sprites,
decals, flipbooks, status icons, textures, or still effect concepts. Textures, masks, and
spritesheet VFX generate directly through `Unity_AssetGeneration_GenerateAsset`; portraits,
icons, and illustrative concepts queue as a `WAITING_FOR_CODEX` job. Either way, Jake
approves any credit spend first, and unreviewed outputs never go in `Resources`.

Generated art supplies source imagery only. This skill still owns the deterministic runtime
recipe, tell binding, palette/intensity law, and verification gate.

## Decide the change tier first
- **Data** (tuning.json tell rows + F1 knobs): colors, glows, windups, sounds, which
  vfx ids fire. Hot-reload, no recompile. Most spell work lives here.
- **Recipe** (VfxLibrary.cs): new looks composed from Particle/Quad/Light elements.
  Recipes are shapes, tells are paint — author color-neutral, let the tell tint.
- **Primitive** (new shader/element): rare; follow P1 shader conventions exactly.

## Hard laws (violating these breaks reproducibility or the art direction)
1. Everything steps via `bool Step(float dt)` from StepFx — no Update(), coroutines,
   `Time.*`, `Random`, TrailRenderer, or `_Time` in shaders. Seeds derive from
   (tick, unitId, slot).
2. Palette law: one hue lane per effect; gilt = crit only; **defensives never bloom**
   (glow ≤1.0); bloom threshold is 1.1 — tier is authored via glow.
3. Announce/shake are RATIONED (S-crown rows only + cooldowns). Don't add either to
   ordinary rows.
4. Unknown vfx/sound ids are safe no-ops — authoring may lead assets.

## Verification gate (every change, no exceptions)
1. `make check-client` BEFORE syncing conclusions — compiles the client's C# headlessly
   and catches API errors before the Syncthing round-trip and the Unity lock. It
   excludes editor scripts by construction, so it cannot catch an Editor script
   touching an `internal` type in `Assembly-CSharp`.
2. Fixture the content (`sim/Warband.Viewer/scenarios.json` + `make scenarios`), find
   real event ticks by folding the replay headlessly.
3. Iterate recipes in the **VFX Lab** (`Warband/VFX Lab/VFX Lab`, with
   `Warband/VFX Lab/Open Lab Scene` and `Rebuild Lab Scene`) — all recipes are editable
   there; see `docs/vault/Projects/vfx-lab.md`. Use probe captures for evidence, not
   for exploration.
4. Probe captures: write `fixture tick advance` lines → scp to Windows
   `warband-shots/probes.txt` → menu `Warband/Render Probe Shots` → scp PNGs back.
   Same fixture+tick at two advances = same filename — run as separate passes.
5. Determinism: `Warband/Render Contact Sheet` twice, binary-diff **every** PNG
   (`cmp`); byte-identical or the change is rejected. The sheet is
   `RenderShots.Ticks.Length` × the fixture count in
   `client/Assets/StreamingAssets/replays/` — 4 × 14 = 56 today, and it grows with
   every fixture. Count the directory; never trust a number written here.
6. Console reads can return empty falsely — verify via `MonoScript.GetClass()`,
   `ShaderUtil.ShaderHasError`, `Resources.Load` probes instead.
7. A gate is evidence only if it could have failed — break the thing on purpose once
   and confirm it goes red. A capture showing a suspiciously *default* value (`0:00`,
   zero, empty) is the harness lying before it is the feature broken.

## Environment gotchas
- Unity editor is remote (Syncthing + MCP); lease-gated (`unity-warband`) — never
  spin-retry; check `EditorApplication.isPlaying` before frozen captures.
- The MCP relay blocks destructive file ops in RunCommand — do file surgery over SSH
  at `C:\Dev\game\warband`, then `AssetDatabase.Refresh()`.
- Run git from the repo root (cwd stuck in a subdir makes pathspecs fail confusingly).
- Textures via GenerateAsset: mono masks tint best; check for retry-stub/suffixed
  files after every batch; commit assets + .metas together.
- Replay/DLL changes ship atomically (`make scenarios` + `make unity-sim`, one commit).
