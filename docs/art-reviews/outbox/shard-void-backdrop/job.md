# Art job: shard-void-backdrop

Status: VERIFIED — direction approved, imported, dialed, and capture-verified 2026-07-30.
Asset class: environment-concept
Created: 2026-07-30
Roadmap: item 35 Stage 2, second half ("one generated void/backdrop art job"). The first half
(rim dressing) shipped 2026-07-30 without any generated art.

## Approval history

The art-pipeline gate is explicit: **any credit spend needs Jake's approval, every time.** This job
was written and parked before any generation call; Jake approved the spend, then the revised 4-asset
batch, then the `sunken-strata` direction, then the import. Sections below are kept in the order they
were written, so the contract above records what was promised *before* results were known.

## Contract

- **Intended use:** the void the shard floats in. Today `ShardEnvironment.BuildVoidDome` paints a
  440-unit inverted sphere with a procedural 1×256 vertical gradient (`voidTop` → `voidGlow`,
  `voidGlowHeight`, `voidGlowWidth`). This job asks whether authored art beats that gradient.
- **Concept-only or runtime candidate:** concept FIRST. If a direction lands, a second revision
  produces the runtime texture. Do not generate a runtime-resolution asset on round one.
- **Subject:** what is beneath/behind the last coherent shard of a dying era, seen from the
  Hourstone. Not a landscape. Not a sky.
- **THE GEOMETRY LAW (item 35 Stage 1's reusable finding — this is the whole trap):** at pitch 42 /
  fov 34 the visible backdrop band is **25°–59° BELOW horizontal**. The camera looks DOWN into the
  void. Backdrop art therefore lives *beneath* the shard, and **a horizon is never in frame** —
  art authored on a horizon line is 100% wasted. Verified again on 2026-07-30's captures.
- **Must preserve:**
  - The environment stays **desaturated**. Saturation belongs to gameplay VFX (standing law).
    Desaturated is not the same as dark — Stage 2's rim dressing failed its first probe by being
    too dark to read at all.
  - The playfield reads first. This is backdrop; it must lose every contrast fight with the board.
  - The Tower already exists as a procedural silhouette (`towerYawDeg` -12, `towerDistance` 150,
    `towerTopY` -38). ADR 0010 makes it the constant. Generated art must not duplicate, occlude,
    or contradict it.
  - Determinism: a static texture is fine; nothing may animate on `_Time` (the byte-identical
    contact-sheet run is the commit gate).
- **Palette:** `voidTop` `#080A0F` → `voidGlow` `#1E2536`. Sand `#D9A43A` is the single permitted
  warmth (it means time), and only as a rim/glow accent, never as fill.
- **Avoid:** horizon lines · stars/nebulae (this is outside time, not space) · literal clouds ·
  any readable text or symbol · a focal point that pulls the eye off the board.
- **Target dimensions:** concept round at 1536×1536 for evaluation only. Runtime mapping is an open
  question — the dome currently samples a 1×256 strip, so real art needs either a proper spherical
  UV pass or a restricted lower-band decal. **Decide the direction before solving the mapping.**
- **Candidate count:** 3 distinct directions, one image each.
- **Required proofs:** contact sheet; plus each candidate composited under a real board capture at
  the 25°–59° band before any judgement.

## Three directions to generate (one image each)

1. **`sunken-strata`** — the shard is the top of a drowned structure. Layered rock/masonry strata
   fall away into black beneath, lit only by the Sand rim from behind. Reads as depth.
2. **`the-unmade`** — the void beneath is unfinished world: geometry dissolving into flat untextured
   planes and gaps, as if the era stopped being rendered. Reads as "outside time."
3. **`hour-drift`** — slow drifts of pale sand/ash suspended in the dark below, catching the one
   warm light. Reads as time itself pooling under the shard.

My pick if you want one: **`the-unmade`** — it is the only one that says something the procedural
gradient cannot already say, and it matches the Hourstone fiction rather than decorating it.

## Inputs

| Path | Role |
|---|---|
| `/tmp/item35s2c/enc-the-drop_A_rim-on.png` | Composition reference — the exact band the art occupies |
| `/tmp/item35s2c/boss-waning-crown_A_rim-on.png` | Composition reference, boss framing |
| `docs/vault/Design/theme.md` "salvage spine" | Content reference |
| `docs/vault/Decisions/0010-*` | The Tower is the constant |

## Codex generation request

- **Use case:** illustrative environment concepts — Codex native imagegen is the right route
  (the Unity asset-generation path is for textures/masks/spritesheets, not concept art).
- **Prompt files:** to be written into `prompts/` once Jake approves the spend.
- **Output names:** `sunken-strata_v1.png`, `the-unmade_v1.png`, `hour-drift_v1.png`.

## Route change (recorded before generating)

The contract originally routed this to Codex native imagegen. `Unity_AssetGeneration_GetModels`
showed the Unity path now carries general image models (`gemini-3.0-pro`, `gpt-image-1-5`,
`flux-2-*`, `seedream-4-5`) **and** a Cubemap modality (`skybox-cinematic`/`skybox-standard`), so
the whole job ran in-editor. Jake approved the revised 4-asset batch before any call.

## Candidates

All four generated 2026-07-30 into `Assets/GeneratedAssets/shard-void-backdrop/` (never into
`Resources/`), copied to `candidates/`. No retry stubs or suffixed duplicates. Concepts are
1024×1024; the cubemap came back as a 1536×768 equirectangular panorama.

| Candidate | Model | Result | Keep/reject reason |
|---|---|---|---|
| `the-unmade_v1` | gemini-3.0-pro | Kept — strong | Obeys the geometry law exactly (steep downward view, no horizon). Dissolving grey architecture + hard-edged gaps say "the era stopped being rendered" better than the gradient ever could. **But** it is the brightest of the three (mean 68, peaks 201) and the amber cracks form a focal point that competes. |
| `sunken-strata_v1` | gemini-3.0-pro | Kept — strongest | Unprompted, it composed the floating shard at top with the drowned world receding beneath — our exact staging. Best depth read of the three; values closest to target (mean 54). **Caveat:** its top third draws the shard we already render in 3D, so a runtime crop must remove it. |
| `hour-drift_v1` | gemini-3.0-pro | Kept — weakest | Best tonal restraint and the ash veils are lovely, but the motion reads as RISING (geyser) rather than time falling, the composition is centred where the board sits, and it painted a **mossy green rock** at the bottom — a literal landscape element the contract forbade. |
| `void-cubemap-probe` | skybox-cinematic | **Rejected — but valuable** | See below. |

## The cubemap probe's negative result (the reusable finding)

The prompt explicitly said NO horizon, NO ground plane, NO bright zenith, all interest BELOW the
viewer. The model returned a hard horizon across the middle, a bright cloud-lit zenith band, and a
**solid rocky seabed floor filling the entire lower half** — precisely the band our camera occupies
(25°–59° below horizontal), where the fiction requires bottomless nothing.

**Conclusion: the cubemap/skybox route is structurally wrong for this game.** Skybox models are
trained on ground + horizon + sky and cannot express "nothing beneath you." Do not re-attempt a
skybox backdrop for the battle environment. The existing architecture — procedural gradient dome
(`BuildVoidDome`) with authored art confined to a lower band — is correct. This cost one generation
to learn and saves the Stage 3 era-dressing pass from re-discovering it.

## Measured value check (why none of these can ship as generated)

Sampled from the real 2026-07-30 board capture, mean luminance 0–255:

| Region | Mean |
|---|---|
| Void today | **29** |
| Board playfield (tiles) | **83–99** |
| `the-unmade_v1` | 68 (peak 201) |
| `sunken-strata_v1` | 54 (peak 163) |
| `hour-drift_v1` | 54 (peak 145) |

Every candidate's bright passages out-value the playfield, so as generated all three break the
"backdrop must lose every contrast fight" law. A flat **×0.5 value multiply** lands them at 27/34/27
— on top of the void's 29 — and is the baseline runtime correction. Proof: `proofs/value_test.png`.

## Candidate approval

- Approved source: **PENDING — Jake to pick a direction.**
- Recommendation: **`sunken-strata_v1`**, at ×0.5 value with the top third cropped away. It is the
  only one that composed our actual staging unprompted, it has the best depth read, and its
  failure mode (crop the shard it drew) is mechanical rather than generative.
- Conditions:
- Date:

## Processing

Approved source: **`sunken-strata_v1`** (Jake, 2026-07-30). Mechanical only — no regeneration.

1. `-crop 1024x666+0+358` — removes the top third, which drew the floating shard we already
   render in 3D.
2. `-evaluate multiply 0.5` — the measured value correction (mean 54 → 27).
3. Feathered alpha (sides 18%, top 22%, bottom 30%, smoothstep) so it dissolves into the void
   with no boundary.

- **Output:** `processed/void-sunken-strata_billboard.png`, 1024×666 RGBA.
- **Measured:** mean luminance **22** — below the void's own 29, and far below the playfield's 99.
  It cannot win a contrast fight with the board.
- **Proofs:** `proofs/import_proof.png` (board today vs the processed plate),
  `proofs/value_test.png` (all three candidates, original vs ×0.5).

### Rejected intermediate: the equirect dome band

First attempt mapped the art into the dome's texture as a latitude band (`v` 0.08–0.45, mirror-tiled
3×) — `processed/void-sunken-strata_2048.png`, kept as the record. **Rejected on sight:** squashing a
1024×666 vertical composition into a 379px horizontal band destroys the receding-depth read that was
the entire reason this candidate won, and mirror-tiling produced obvious Rorschach seams.

**The finding:** the shard's backdrop art is inherently a *vertical depth* composition, and the dome's
UV band is inherently *horizontal*. They are incompatible. Hang backdrop art as a billboard in the
deep instead — the camera is fixed, so it only ever needs to exist in one arc. This is why
`BuildVoidArt` builds a quad rather than extending `GradientMat`.

## Runtime wiring (code landed, compiles, awaiting import)

`ShardEnvironment.BuildVoidArt` + `VoidArtMat`, driven entirely by `tuning.json`:
`voidArt` (Resources path — empty restores the Stage 1 gradient dome exactly), `voidArtYawDeg`,
`voidArtDistance`, `voidArtCenterY`, `voidArtWidth`, `voidArtOpacity`. Default distance **180**
places it BEYOND the Tower (150) so the Tower still silhouettes against it, honouring ADR 0010.
Unlit transparent, cull off, ZWrite off, `RenderQueue.Transparent`. `make check-client` PASS.

## Import approval

- **Approved processed file:** `void-sunken-strata_billboard.png` (Jake, 2026-07-30).
- **Runtime destination:** `client/Assets/Resources/Board/Void/void_sunken_strata.png`
- **Source-master destination:** `client/Assets/ArtSource/VoidBackdrop/sunken-strata_v1_master.png`
- **Conditions:** none stated.

## Unity verification — VERIFIED, with a framing caveat that matters more than the art

- **Import settings:** Unity's default `npotScale = ToNearest` silently crushed the 1024×666 plate to
  **1024×512**, squashing it 24% vertically (caught by logging the loaded dimensions, not by eye).
  Set `npotScale = None` + `alphaIsTransparency` + `Clamp` through `TextureImporter`; Unity wrote its
  own `.meta`. Re-verified as 1024×666. **Reusable: any non-power-of-two plate needs this or it ships
  distorted.**
- **Resource/load check:** `Resources.Load` PASS, quad built at the intended transform.
- **Kill-switches, all negative-controlled in one matrix on two fixtures:** `voidArt:""` → quad
  absent, shard intact · `rim.enabled:false` → 16 props → 0, frame intact · `environment.enabled:false`
  → Shard root gone entirely.
- **Tower occlusion:** art centre sits ~209 units from camera vs the Tower's ~205, and the quad is
  ZWrite-off in the transparent queue, so the opaque Tower still silhouettes in front. Confirmed in
  the capture. ADR 0010 intact.
- **Console:** 0 errors / 0 warnings after a cleared-console hold.
- **Determinism:** boss fixture byte-identical on repeat. `enc-the-drop` differs by **86 pixels at
  <1/255** — and an off/off vs on/on attribution run showed *both* pairs differ by exactly 85.67, so
  this is **pre-existing sub-visual render nondeterminism in that fixture, NOT caused by this work.**
  Worth its own look sometime: it means "byte-identical contact sheet" is not strictly true for
  every fixture/tick.

### THE RESULT — read this before spending anything else on backdrops

Measured against a gradient-only control: the backdrop registers **only in a 1202×136 strip along the
top edge**, with a **max pixel change of 34/255**. At the dialed play camera (item 22 / ADR 0027:
fov 34, pitch 42, aimBias 0.2), **the 8×8 board fills the frame and leaves almost no visible void.**

First import shipped a **murky green-brown smear** across that strip — the source's moss/vegetation
tones survived the ×0.5 value multiply. Corrected with `-modulate 100,18,100` + a 22% `#1E2536`
colorize (`processed/void-sunken-strata_billboard_cold.png`, now the runtime plate). Post-fix it
reads as faint cold haze.

**Honest verdict: the backdrop is in, correct, and cheap — but it delivers atmosphere, not "the void
the shard floats in," because there is nowhere for it to live.** Shipped at `voidArtOpacity 0.55`.
The blocker is framing, not art: no further backdrop generation is worth funding unless the camera
opens up. Disabling is one string (`voidArt: ""`).

## Log

- 2026-07-30 — Imported, dialed over three probe rounds (placement sweep → colour correction →
  shipped-value gate), verified, and closed. Status → VERIFIED.

## Log

- 2026-07-30 — Job created and contract written during item 35 Stage 2. Parked at
  AWAITING_JAKE_APPROVAL: three directions specified, zero generation calls made, zero credits
  spent. Rim dressing (Stage 2's other half) shipped independently and needs no generated art.
- 2026-07-30 — Jake approved the credit spend. Route switched from Codex to in-editor Unity
  generation after `GetModels` showed image + cubemap modalities. Four assets generated in one
  parallel batch: three concepts kept for review, the cubemap probe rejected with a reusable
  architectural finding. Value measured against a real board capture; all three need ×0.5 before
  they can ship. Status → AWAITING_CANDIDATE_REVIEW. **No asset has been imported to a runtime
  path** — `Assets/GeneratedAssets/` is a staging folder, not `Resources/`.
