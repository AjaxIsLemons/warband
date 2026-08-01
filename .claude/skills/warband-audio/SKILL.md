---
name: warband-audio
description: Author, bake, promote, and verify warband SFX — the sfx.py contract, the audition sheet, the voice/bus budget, and the promotion traps that fail silently.
---

You are touching warband sound. **Source of truth: `docs/vault/Design/audio.md`** — §5 the two
policy layers and bus tree, §6 the tooling, §7 the numbered build order (steps 0–6 DONE
2026-07-27; step 7 = options sliders, owned by roadmap item 9). The contract as data is
`tools/sfx/families.json`; the CLI is `tools/sfx/sfx.py`. This skill is the checklist.

## ⚠ Know which lint state you are looking at

The `make sfx-lint` help text says it is **RED by design until audio.md step 5 promotes the
bake** — written when it was. Step 5 and step 6 have since landed, and lint **PASSES today: 0
violations, no dangling ids, no silent weapons** (re-verified 2026-07-29). So do not read a red
run as "expected"; it is now a real regression. Its `tuning.json` cross-reference also lists
the 11 `revision_*` clips as "no tell row names" — that is informational, not a violation.

## The flow

`docs/audio/src/{ui,board}` → `make sfx-bake` → `docs/audio/baked/{ui,board}` → audition
(`make sfx-sheet`, then `make sfx-serve` → http://127.0.0.1:8091; browsers block `file://`
media) → **promote** into `client/Assets/Resources/{UI,Board}/SFX` (audio.md step 5) →
`make sfx-lint` against the shipped set. `make sfx` = bake + sheet + lint-the-bake. Raw
numbers with no contract: `python3 tools/sfx/sfx.py measure <dir|file>`. Working files sit
outside `client/Assets/` on purpose so Unity never imports them. (§6.3 names
`client/Assets/ArtSource/SFX` as the source tree — that directory does not exist; `docs/audio/src`
is real.)

## Measure, don't listen-and-guess

- `sfx.py` measures **onset / audible length / peak / crest**; those four numbers decide
  whether a cue feels good. `make sfx-density` gives onsets/sec **and per-bus pressure vs cap**
  per replay fixture — the combat voice budget input.
- The worst density is **`overtime`** (~9.4 onsets/s sustained for 3.6 min), **not** the
  `castfest` burst the audit assumed. Judge board audio against `overtime`.
- An older doc's claim that all 18 clips "passed structural validation" only checked that files
  imported. That is why a regenerated batch had no reason to be better, and why three fresh
  ElevenLabs clips came back with **identical pathology** — all padded to 1.045 s, 23 dB level
  spread. **Root cause was process, not the model.** Never accept "it imported" as a gate.

## Traps that fail silently

- **`_1` variant shadowing.** `SfxPlayer.Load` tries `{id}_1..4` and only falls back to bare
  `{id}` when none exist, and the shipped set names every single-variant clip `_1`. Promoting a
  bake as `error.wav`/`major.wav` left stale 1.04 s `error_1`/`major_1` **shadowing** them — both
  files exist, both import, both play, no warning. Fix: `variants: 1` in `families.json` (or
  delete the `_1`). Verify *resolution*, not that the copy succeeded.
- **`Resources/` ships everything it contains, referenced or not.** 3.49 MB of dead clips
  shipped in every build; `hall_ambience` alone was 1.4 MB for a bed a design call had already
  cut. Audit the folder after any supersession. (Now 11 UI / 31 board clips.)
- **Routing depends on which event kind content is authored on.** Per-weapon hits are authored
  on `EventKind.Attack` (the swing) — `DamageDealt` carries no sound row — so `BusFor`
  (`ReplayPlayer.cs`) originally filed **every weapon hit** under `State`: lowest priority,
  smallest cap, first stolen under load. `Attack` now routes to `Impact`. Found by pricing caps
  against fixtures, not by reading code. Generalise: price the budget against real fixtures and
  never assume which kind a sound hangs off.

## Rules

- Unknown sound ids are **silent no-ops** (one warning, then cached) — authoring may lead
  assets, and presentation must never break a transaction. Preserve that in any change.
- Tell rows fire the sounds: see `.claude/skills/spell-fx/SKILL.md` for the row/`tuning.json`
  workflow. `sound`/`critSound`/`castSound` on a row is permission, not obligation (§5.2).
- Generated audio goes through ElevenLabs in the Unity MCP asset-generation stack into the
  gitignored `client/Assets/GeneratedAssets/`, then through the same bake + lint as everything
  else. **Explicit Jake approval before any credit spend, every time.**
- Both surfaces are unmuted via data, not code: `tuning.json` (board, live under F1) and
  `HubPresentation.json` (Hall UI) — those two values are the mute until step 7 ships sliders.
