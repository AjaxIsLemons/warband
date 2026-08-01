---
name: warband-ui-review
description: Create and implement review-first UI directions for Warband through paired inbox/outbox jobs. Use for UI samples, HTML mockups, screenshot redesigns, visual comparisons, or approved-concept implementation. Requires a named concept approval before Unity work and Jake's acceptance of matched Unity evidence before the job is done.
---

# Warband UI Review

Turn a UI idea or screenshot into comparable visual directions, an implementation-ready contract,
and matched Unity evidence. Concept approval authorizes implementation; it does not certify the
implementation.

## Read first

Always read `CLAUDE.md` and the relevant design sources. When implementation feasibility,
specification, or Unity verification is in scope, also read:

- [implementation fidelity](references/implementation-fidelity.md);
- `docs/vault/Design/ui-responsive-contract.md`;
- `.claude/skills/unity-ui/SKILL.md`;
- `.claude/skills/unity-ui-patterns/SKILL.md` only when screen architecture is relevant.

## The two approval gates

Concept work and implementation are distinct phases:

1. **Direction gate:** Jake explicitly approves an exact sample or revision. This authorizes a
   fidelity spec and, when requested, Unity implementation.
2. **Implementation gate:** Jake reviews the matched Unity evidence and accepts the actual result.
   Only this closes the job.

Hard rules:

- Generate, compare, and revise without changing `client/`.
- A preference is not an approval; ask for approval by exact filename.
- If Jake asks to generate and implement in one prompt, generate first and stop at the direction
  gate.
- After implementation, an agent may report `IMPLEMENTATION_CANDIDATE` or
  `VISUAL_VERIFICATION_BLOCKED`. It may not self-promote the job to `ACCEPTED`.
- Passing layout tests is not visual acceptance.

## Use the shared inbox/outbox

Every request is a job with the same lowercase hyphenated slug on both sides:

```text
docs/ui-reviews/
├── inbox/<job>/                 # Jake's source material; never overwritten
└── outbox/<job>/
    ├── review.md                # brief, status, decisions, and review log
    ├── samples/                 # reviewable PNG/JPG references
    ├── work/                    # optional HTML/SVG source
    └── implementation/          # fidelity spec, Unity captures, overlays, and diffs
```

Create the paired folders and review sheet with:

```bash
bash .claude/skills/warband-ui-review/scripts/new-review-job.sh <job>
```

Keep agent-authored material in the matching outbox. Preserve every prior sample and use revision
suffixes such as `02-compact-r2.png`. Commit approved concepts only when Jake asks for a durable
project reference.

## Establish the brief

1. Inspect every inbox file and the closest current Unity capture.
2. Record the screen, one primary player decision, required information/actions/states, design
   laws, inputs, and what may change.
3. Record resolution explicitly:
   - logical authoring resolution;
   - physical review resolution;
   - panel scale factor;
   - acceptance tier.
4. Ask only about a choice that would materially split the result. Otherwise record assumptions
   and continue.

Do not invent mechanics, currencies, progression, copy, or state. Illustrative text must be
labelled.

## Generate useful comparisons

Produce two or three meaningfully different directions at the same target size:

1. **Evolution** — retain structure and improve hierarchy, density, and focus.
2. **Structural alternative** — reorganize around the primary decision.
3. **Wildcard** — optional, when a bolder hypothesis is genuinely useful.

### Choose the right medium

- Use **HTML/CSS or SVG** for implementation-grade hierarchy, typography, density, and exact
  sizing. Author Warband desktop mockups in the `1600×900` logical coordinate system. Render a
  QHD review image by scaling the page to `2560×1440`; do not copy 2560 device pixels into USS.
- Use **image generation** for mood, material, ornament, lighting, or broad art direction. Treat
  generated typography and geometry as illustrative unless rebuilt in a measured coded mockup.
- Use existing approved art and representative real data whenever possible.

For coded mockups:

- create one self-contained source per direction under `work/`;
- render with headless Chromium at the declared target;
- constrain CSS to Unity-feasible features or record an explicit Unity implementation path for
  every must-match exception;
- label the output as a structural prototype, not a running-game capture.

When Claude cannot generate required raster concepts, prepare exact prompts, set
`WAITING_FOR_CODEX`, and hand off:

```text
Codex: use $warband-ui-review to process <job>.
```

Do not assume programmatic access to Jake's subscriptions. If no suitable rendering lane exists,
preserve the brief, set `GENERATION_BLOCKED`, and report the missing capability.

## Prepare the direction review

Set `review.md` to `AWAITING_CONCEPT_REVIEW`. For every sample record:

- filename, direction, and hypothesis;
- what changed and the strongest benefit;
- main risk or implementation cost;
- which details are literal versus illustrative;
- any feature whose Unity feasibility is not yet established.

Present samples inline when possible. Ask for a preferred exact sample, what must be kept, and the
single most important revision.

Record feedback in the review log. Revise the selected direction without erasing history, change
one major dimension at a time, and return to `AWAITING_CONCEPT_REVIEW`.

## Convert approval into a fidelity contract

After Jake approves a named sample:

1. Set `APPROVED_DIRECTION` and record the filename, date, and conditions.
2. Write `implementation/spec.md`.
3. Define required component regions, exact actions/states, focus/input/motion behavior, resolution
   contract, must-match details, illustrative details, feasibility path, fixture data, and required
   evidence.
4. Preserve the approved sample unchanged.
5. Set `SPEC_READY` only when the feasibility map has no unresolved must-match item.

Implementation begins only in the approval turn or a later request that clearly asks for it.

## Implement and prove fidelity

Use `$warband-unity-workflow` for all client work and take the Unity lease before MCP operations.
Capture the approved fixture and state at the declared physical review resolution.

The ordinary acceptance set is:

- **primary visual:** QHD `2560×1440`, corresponding to Warband's `1600×900` logical panel;
- **containment smoke:** `1920×1080`;
- broader matrix only for responsive-shell, breakpoint, or target-specific work.

Put the approved reference, Unity capture, overlay/diff, and deviation ledger in
`implementation/`. The helper below builds deterministic comparison evidence when the inputs have
matching dimensions:

```bash
bash .claude/skills/warband-ui-review/scripts/build-visual-comparison.sh \
  approved.png unity.png implementation/comparison.png
```

Run structural layout assertions separately. Verify interaction or motion with a focused play
check or a short capture/filmstrip.

Set:

- `IMPLEMENTATION_CANDIDATE` when implementation, proportional tests, and matched evidence are
  ready for Jake;
- `VISUAL_VERIFICATION_BLOCKED` when the actual Unity evidence cannot be produced, naming the
  missing check;
- `ACCEPTED` only after Jake accepts the Unity result.

Never use `IMPLEMENTED_UNVERIFIED`, and never describe code-complete work as visually done.
Generated concept art is not promoted into runtime `Resources` without a separate curation
decision.
