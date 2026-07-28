---
name: warband-ui-review
description: Generate review-first UI concepts for Warband through paired inbox/outbox jobs. Use when Jake asks Claude or Codex for UI samples, mockups, alternate visual directions, screenshot-based redesigns, or a UI review before implementation. Prefer native image generation in Codex, use rendered coded mockups as the fallback, and require explicit approval of a named sample before changing the Unity client.
---

# Warband UI Review

Turn a UI idea or screenshot into comparable visual samples, put everything Jake should inspect
in one outbox job, and stop for review before implementation.

## Hard boundary

Concept work and implementation are separate phases.

- Generate, compare, and revise samples without changing `client/`.
- Do not treat an attractive generated image as an approved design.
- Do not implement until Jake explicitly approves a named sample or revision.
- If Jake asks to generate and implement in one prompt, generate first and stop at the review gate.

Generated concepts are hierarchy, composition, density, and mood references. They are not proof
that text, layout, accessibility, interaction, or Unity UI Toolkit behavior works.

## Use the shared inbox/outbox

Every request is a job with the same lowercase hyphenated slug on both sides:

```text
docs/ui-reviews/
├── inbox/<job>/                 # Jake's raw source material
└── outbox/<job>/
    ├── review.md                # brief, comparison, status, and review log
    ├── samples/                 # reviewable PNG/JPG samples
    ├── work/                    # optional HTML/SVG sources for rendered mockups
    └── implementation/          # approved spec and later verification captures
```

Create the paired folders and review sheet with:

```bash
bash .claude/skills/warband-ui-review/scripts/new-review-job.sh <job>
```

Keep inbox files as source material:

- Never overwrite or delete an input.
- When Jake asks to process loose files in `docs/ui-reviews/inbox/`, move only files clearly
  belonging to the current request into its job folder. Leave ambiguous files untouched.
- Put agent-authored material only in the matching outbox folder.
- Preserve previous samples. Use revision suffixes such as `02-compact-r2.png`.
- Treat unapproved samples as working artifacts. Commit an approved concept only when Jake asks
  for it to become durable project reference.

## Establish the brief

1. Read `CLAUDE.md`, `docs/vault/index.md`, `docs/vault/Design/pitch.md`, and the current design
   sources relevant to the requested screen. For the Hall, include
   `docs/vault/Design/hall-polish.md` and its active ADRs.
2. Inspect every inbox file. For a redesign, also inspect the closest current capture under
   `client/McpCaptures/` when one is not already in the inbox.
3. Read `.claude/skills/unity-ui/SKILL.md` and
   `.claude/skills/unity-ui-patterns/SKILL.md` when feasibility or later implementation matters.
4. Fill the outbox `review.md` with:
   - the screen and the one player decision it serves;
   - required information, actions, states, and target aspect ratios;
   - established visual and interaction laws;
   - source screenshots and references;
   - what may change and what must remain true.
5. Ask Jake only about a missing choice that would materially split the result. Otherwise state
   assumptions in `review.md` and generate.

Do not invent mechanics, currencies, progression, copy, or state. Generated text is illustrative
unless it was supplied by the game or brief.

## Generate a comparison set

Produce two or three meaningfully different samples at the same target size:

1. **Evolution** — preserve the current structure and improve hierarchy, density, and focus.
2. **Structural alternative** — reorganize the screen around the primary player decision.
3. **Wildcard** — optional, only when a bolder direction could reveal a useful idea.

Vary one major design hypothesis per sample. Do not generate three cosmetic reskins of the same
layout.

### Codex lane — preferred

Use Codex's native `$imagegen` capability.

- Attach the current screenshot and any approved visual references.
- Identify which image controls content and which controls style.
- Request a full-screen game UI concept at the real target aspect ratio.
- Preserve Warband's established identity and semantic colors.
- State the required regions, priority, and interaction state precisely.
- Keep in-image text short; judge structure rather than trusting generated typography.
- Generate each direction separately so feedback can target one hypothesis.

Save returned assets to `outbox/<job>/samples/` when the surface provides a local artifact. If an
image is inline-only, present it inline and tell Jake the exact intended filename; do not claim it
was written locally.

### Claude lane — prefer a Codex handoff

When raster concepts are the desired output, prepare the brief and exact prompts, set
`review.md` to `WAITING_FOR_CODEX`, and tell Jake:

```text
Codex: use $warband-ui-review to process <job>.
```

Resume review after Codex returns candidates. Claude must not assume it can invoke Codex's native
image generator or use Jake's ChatGPT subscription programmatically.

When Jake wants an immediate structural prototype instead of a raster concept, create a coded
mockup:

- Build one self-contained HTML/CSS or SVG mockup per direction under `outbox/<job>/work/`.
- Use copies or references to existing portraits and approved source art; do not modify originals.
- Render each direction to `outbox/<job>/samples/` with headless Chromium at the target viewport.
- Prefer real text and representative data so density and hierarchy can be evaluated.
- Label coded mockups as structural prototypes, not final art.

If neither the Codex handoff nor a render-capable browser is available, keep the Codex-ready
prompt in `review.md`, mark the job `GENERATION_BLOCKED`, and report the missing capability.

## Prepare Jake's review

Set `review.md` to `AWAITING_REVIEW`. For each sample, record:

- filename and short direction name;
- the hypothesis it tests;
- what changed from the current UI;
- strongest benefit;
- main risk or implementation cost;
- elements that are illustrative rather than literal.

Present the samples inline when possible and link the outbox files. Ask for:

1. a preferred sample, a combination, or rejection of all;
2. what must be kept;
3. the single most important change for the next revision.

Do not bury the review request inside an implementation report.

## Revise without erasing history

Record Jake's feedback in the review log, then revise only the selected direction unless he asks
for another comparison set. Change one major dimension at a time. Write a new file with an `r2`,
`r3`, and so on suffix, update the comparison, and return to `AWAITING_REVIEW`.

Ask for approval by exact sample name. A preference is not approval.

## Convert approval into an implementation contract

After Jake explicitly approves a named sample:

1. Set `review.md` to `APPROVED_FOR_IMPLEMENTATION`.
2. Record the approved filename, date, and any conditions.
3. Write `outbox/<job>/implementation/spec.md` with:
   - the chosen information hierarchy and component regions;
   - exact actions and required UI states;
   - responsive, focus, input, and reduced-motion behavior;
   - which visual details are must-match versus illustrative;
   - acceptance captures and tests.
4. Preserve the approved sample unchanged.

Implementation begins only in the approval turn or a later request that clearly asks for it.

## Implement and close the loop

For Unity work, follow the Warband Unity workflow, take the `unity-warband` lease before any MCP
operation, and use the smallest proportional verification loop.

Put implementation captures in `outbox/<job>/implementation/` and compare them against the
approved concept. Report intentional differences. Set the review status to `IMPLEMENTED` only
after the requested verification completes; otherwise use `IMPLEMENTED_UNVERIFIED` and name the
missing check.

Do not move generated concept images into runtime `Resources`. Promotion of any generated asset
is a separate curation decision.
