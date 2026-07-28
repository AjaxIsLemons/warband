---
name: warband-art-pipeline
description: Generate, curate, process, import, and verify Warband placeholder art through shared Claude/Codex inbox-outbox jobs. Use for portraits, class or talent icons, weapon art and icons, spell/VFX source images, decals, masks, flipbooks, materials, textures, and environment concepts. Prefer Codex native image generation, let Claude prepare and review Codex jobs, and require explicit approval before importing generated assets into Unity.
---

# Warband Art Pipeline

Use generated art to improve placeholder quality quickly without confusing provider output with a
game-ready asset.

## Non-negotiable gates

- Keep raw input, generated candidates, processed candidates, and imported runtime assets distinct.
- Never generate directly into `client/Assets/Resources`.
- Never let provider success imply artistic or technical acceptance.
- Require Jake to approve a named candidate before processing it for runtime.
- Require Jake to approve the processed proof before importing it into Unity.
- Preserve rejected candidates outside runtime asset paths.
- Keep first-playable content limits intact; this pipeline improves assets, not content scope.

Concept generation and outbox processing do not need the Unity lease. Import, `.meta` generation,
and in-client verification do.

## Use paired jobs

Use the same lowercase hyphenated job name on both sides:

```text
docs/art-reviews/
├── inbox/<job>/                 # immutable notes, screenshots, sketches, references
└── outbox/<job>/
    ├── job.md                   # request, status, prompts, comparison, approvals
    ├── prompts/                 # exact generation prompts
    ├── candidates/              # raw generated outputs
    ├── proofs/                  # contact sheets and target-size checks
    ├── processed/               # cropped/keyed/resized import candidates
    └── implementation/          # import record and Unity verification captures
```

Create a job with:

```bash
bash .claude/skills/warband-art-pipeline/scripts/new-art-job.sh <job> <asset-class>
```

Supported classes are documented in [asset classes](references/asset-classes.md). Never overwrite
inbox files or earlier candidate revisions. Put agent-authored work only in the matching outbox.

## Establish the asset contract

1. Read `CLAUDE.md`, `docs/vault/index.md`, `docs/vault/Design/pitch.md`, and
   `docs/vault/Decisions/0016-pve-first-asymmetric-endless.md`.
2. Read [asset classes](references/asset-classes.md), then the current design sources it routes to.
3. Inspect every input image. Label each as an edit target, content reference, style reference, or
   composition reference.
4. Inspect the current in-game asset and a real capture showing its use when one exists.
5. Record in `job.md`:
   - intended use and whether the output is concept-only or a runtime candidate;
   - subject, identity, mechanical meaning, and required silhouette;
   - target dimensions, crop, alpha, smallest display size, and background behavior;
   - style anchor, palette lane, invariants, and avoid list;
   - required candidate count and proof views.

Do not invent a mechanic to make an icon interesting. The authored mechanic owns the image.

## Hand work from Claude to Codex

Claude cannot assume access to Codex's native image generator or Jake's ChatGPT subscription.
Use the shared filesystem as the queue:

1. Complete the contract and exact prompts in `job.md` and `prompts/`.
2. Set `Status: WAITING_FOR_CODEX`.
3. Tell Jake:

   ```text
   Codex: use $warband-art-pipeline to process <job>.
   ```

4. Stop image generation work without substituting unrelated stock or procedural art.
5. Resume curation when Codex writes the candidates and changes the status.

Do not launch a nested Codex CLI process unless Jake explicitly requests that experiment. Native
Codex image generation in an interactive Codex session is the supported default.

## Generate with Codex

When Codex receives a named job or is asked to process pending art:

1. Read this skill and the native `$imagegen` skill.
2. Find pending work with:

   ```bash
   bash .claude/skills/warband-art-pipeline/scripts/pending-art-jobs.sh
   ```

3. Inspect all referenced local images before the first generation.
4. Use built-in image generation by default; do not require an API key.
5. Issue one image-generation call per distinct asset or variant.
6. Copy project-bound outputs from Codex's generated-image location into
   `outbox/<job>/candidates/` with stable, versioned names.
7. Save every exact prompt and identify each reference image's role.
8. Inspect each output before keeping it. Reject obvious failures immediately but record why.
9. Set `Status: CANDIDATES_READY`.

For a family such as eight portraits or sixteen status icons, generate two or three **style
anchors first**. Obtain approval for one anchor before producing the full family. Do not spend a
batch discovering the style eight times.

For simple opaque assets needing alpha, generate on a flat chroma key and use the imagegen
skill's removal helper. Do not silently switch to API/CLI-native transparency.

## Build proofs and curate

Generate a named contact sheet:

```bash
bash .claude/skills/warband-art-pipeline/scripts/make-contact-sheet.sh \
  docs/art-reviews/outbox/<job>/proofs/candidates.png \
  docs/art-reviews/outbox/<job>/candidates/*.png
```

Also produce the class-specific proofs from [asset classes](references/asset-classes.md). At
minimum validate:

- subject and mechanical meaning;
- family consistency;
- silhouette and value structure at the smallest real size;
- crop, safe padding, and alpha edge quality;
- absence of text, watermark, accidental symbols, and unintended focal details;
- tiling, frame-grid, or palette constraints where applicable.

Set `Status: AWAITING_CANDIDATE_REVIEW`. Present the proofs and ask Jake to approve a named
candidate, combine specified traits, or reject all.

## Process the selected candidate

After candidate approval:

1. Record the exact approved source filename and conditions.
2. Crop, key, resize, atlas, or color-normalize mechanically into `processed/`.
3. Never generatively alter the approved candidate without creating a new revision.
4. Record tool commands and output dimensions in `job.md`.
5. Produce target-size and in-context proofs.
6. Set `Status: AWAITING_IMPORT_APPROVAL`.

Only `APPROVED_FOR_IMPORT` authorizes copying the processed file into `client/`.

## Import through Unity

After import approval:

1. Use the Warband Unity workflow and select the relevant detailed Unity skills.
2. Copy the durable source master to `client/Assets/ArtSource/<family>/` when it has continuing
   reference value.
3. Copy only accepted runtime outputs to their intended runtime path.
4. Never edit `.meta` files. Acquire `unity-warband`, refresh/import through Unity, and let Unity
   create and save importer metadata.
5. Validate importer settings, console state, actual resource lookup, and the smallest real
   presentation.
6. Put captures and import notes in `implementation/`.

Use `IMPORTED_UNVERIFIED` until the in-context capture passes. Use `VERIFIED` only after the
relevant Unity check succeeds.

## Keep combat FX honest

Generated spell art supplies masks, sprites, decals, flipbooks, textures, or visual references.
It does not define the runtime effect. For combat FX:

- follow `.claude/skills/spell-fx/SKILL.md`;
- keep recipes as shapes and tells as paint;
- preserve deterministic `Step(float dt)` playback;
- verify with targeted probes and two byte-identical contact-sheet runs.

Never replace a deterministic recipe with an opaque video or wall-clock animation.
