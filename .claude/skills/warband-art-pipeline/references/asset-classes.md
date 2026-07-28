# Warband art asset classes

Choose one primary class per job. Split mixed deliverables into separate jobs when their proof or
import contracts differ.

## Portrait

Use for hero, class, enemy, or NPC portrait art.

- Read `Design/theme.md`, `Design/heroes.md`, `Design/roster.md`, and the relevant character dive.
- Use the current portrait atlas and approved bridge portraits as style references.
- Establish one family anchor before generating a roster.
- Source at 1024 square or larger; runtime placeholder target is currently 512 square.
- Keep face, headgear, weapon cue, era, lighting, camera, and crop consistent across the family.
- Prove as a full-size grid and at 128, 64, and 32 px crops.

Avoid pose drift, inconsistent camera distance, busy backgrounds, edge-cut weapons, and eight
independently invented art styles.

## Icon

Use for class, talent, weapon, status, Inscription, currency, or mechanic icons.

- Read the mechanic's design source. Status icons also read `Design/combat-spectacle.md` §5.
- Prefer code-native SVG/Painter2D when extending an established exact icon family.
- Use generated bitmaps for placeholders, engraved emblems, or a new family exploration.
- Establish one icon-family anchor before batching.
- Generate a simple centered emblem on a flat removable background with generous padding.
- Prove at 128, 64, 32, and the smallest intended size; status icons must survive at 24 px.
- Validate in grayscale and against both near-black and raised-slate surfaces.

Avoid letters, numerals, microscopic filigree, multiple unrelated symbols, soft photographic
backgrounds, and color as the only semantic distinction.

## Weapon art

Use for inventory/card illustrations or isolated weapon cutouts. Use the Icon class for small
weapon sockets.

- Read `Design/weapons.md` and the weapon's current mechanical contract.
- Keep one camera, scale, lighting rig, material treatment, and padding across the family.
- Show the silhouette and category immediately; do not add unattested magical effects.
- Generate on a removable flat background when runtime alpha is required.
- Prove as full card art and at the smallest inventory presentation.

## VFX concept

Use for a still visual target for a spell, attack, field, status, or death.

- Read `Design/combat-spectacle.md`, `Design/authoring-combat-fx.md`, and
  `.claude/skills/spell-fx/SKILL.md`.
- Treat the output as direction for shape, timing beats, palette lane, and intensity—not as a
  shippable effect.
- Show telegraph, release, impact, and aftermath separately when timing matters.
- Preserve one hue lane and the authored T0–T3 intensity.

## VFX source

Use for mono masks, sprites, decals, flipbooks, skyboxes, and other raster inputs consumed by a
deterministic runtime recipe.

- Read the VFX Concept sources plus `Design/fx-runtime.md`.
- Mono sigil/mask: white on black, centered, clean silhouette, normally 512 square.
- Flipbook: declare grid, frame order, background, and loop/non-loop behavior before generation.
- Decal: top-down, no perspective, exact center/pivot, removable background.
- Texture: prefer 256–512 unless the source doc requires more.
- Prove alpha edges, individual frames, and the asset inside the real recipe.

Never use a generated animation/video as the runtime clock.

## Material texture

Use for tileable surface maps or bounded texture detail.

- Read the owning visual design source, such as `Design/hall-polish.md`.
- Request seamless, scale-neutral material with no border, hero crack, central object, lighting
  hotspot, or baked perspective.
- Prove a 3×3 tile, rotation, mip-sized reduction, and actual material use.
- Reject visible repeat structure even when the single tile looks attractive.

## Environment concept

Use for composition, lighting, material, or mood references.

- Mark `concept-only` unless a separate job promotes an isolated source asset.
- Match the real camera and aspect ratio when the concept guides implementation.
- Do not import the concept into `Resources`.
- Record which elements are literal constraints and which are mood reference.
