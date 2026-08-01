# VFX Lab

PC editor tool for tuning Warband combat and Revision presentation against the shipping runtime.
Open **Warband → VFX Lab → Open Lab Scene** (or open `Assets/Scenes/VfxLab.unity` and then
**Warband → VFX Lab → VFX Lab**).

## What is integrated

- Every `VfxLibrary` recipe appears automatically (38 at launch), including code fallbacks,
  asset overrides, asset-only recipes, and Revision landings.
- Every replay fixture under `StreamingAssets/replays` appears automatically. Its
  `ReplayInspector` signatures become jump-to-first-occurrence bookmarks, so fields, statuses,
  deaths, casts, attacks, passives, and other special systems use their real replay path.
- The complete Revision sequence is scrubbed as one sentence: witnessed-future capture → opening
  split → held Hour → tear → 1–2 second rewind → vacuum → landing → receipt. Both lineages and
  Reduced Motion are available.
- Current `TellDef` recipe bindings are shown beside each recipe. Selecting a binding opens only
  its relevant motion, recipe, audio, and impact fields.
- Board/UI SFX are discoverable and auditionable. Edit Mode is a raw clip preview; Play Mode uses
  the shipping voice pool, priority, bus routing, and mixer.
- The viewport can switch between the production shard, a neutral value studio, and isolation.
  Environments remain authored by their own subsystem; the Lab only supplies comparison contexts.
- Optional `VfxLabScenarioAsset` bookmarks capture a recipe/fixture/Revision setup that needs a
  named review case.

## Draft and Apply contract

All recipe, tell, and Revision tuning edits are drafts until their explicit Apply button is
pressed. Closing the window discards draft state.

Recipe resolution order:

1. active Lab draft;
2. enabled `VfxRecipeAsset` under `Resources/VFX/Recipes`;
3. the C# recipe in `VfxLibrary`;
4. existing primitive fallback for an unknown id.

`APPLY RECIPE` creates or updates one override asset. `APPLY TELL` and
`APPLY REVISION TUNE` write through the existing `TuningConfig`/`TuningIO` path to canonical
`StreamingAssets/tuning.json`. Revert never writes.

## Adding content

- Add a C# recipe to `VfxLibrary`: it appears on the next domain reload.
- Press `+` in Recipes to create an asset-only recipe; or Apply a built-in draft to override it.
- Add a `.bytes` fixture under `StreamingAssets/replays`: Combat discovers and indexes it.
- Create **Assets → Create → Warband → VFX → Lab Scenario** for a durable review bookmark.

The Lab scene is editor-only and is intentionally absent from Build Settings. It is a tuning
workbench, not a new runtime screen.

## Verification seam

`VfxLabSceneTools.McpVerifyContract()` validates recipe resolution and asset round-tripping.
`VfxLabSceneTools.McpSmokeModes()` exercises a direct recipe, all three environment contexts, a
real fixture scrub, and Revision rewind/receipt phases.
