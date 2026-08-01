# UI review: beyond-the-hour

Status: ACCEPTED  
Created: 2026-07-30

## Brief

- Screen or flow: post-Waning-Crown blocking choice inside the existing Workbench frame
- Primary player decision: bank the completed run now or continue the same warband into endless
- Required information and actions: standard victory is already safe; Retire; Continue; the
  three-fight-plus-Crown cycle; escalation and score language
- Required states: `RunPhase.VictoryChoice`; reduced motion; QHD primary and 1080p containment
- Must preserve: one-frame/one-card law, actual Workbench behind the scrim, existing blocking-choice
  component, existing visual tokens, no new currency or progression promise
- May change: equal versus forward-weighted option emphasis, exact iconography, card copy density

## Resolution contract

- Logical authoring resolution: 1600×900
- Physical review resolution: 2560×1440
- Panel scale factor: 1.6
- Acceptance tier: Primary QHD
- Additional target-specific viewport, if required:

## Inputs

| Source | Role |
|---|---|
| `docs/vault/Decisions/0030-beyond-the-hour.md` | Approved mechanics and state contract |
| `../workbench-refactor/implementation/impl-1600x900-workbench-market-recruit.png` | Actual current Workbench backdrop |
| `../workbench-frame/implementation/spec.md` | Existing blocking-choice and one-frame contract |

## Assumptions

- The final choice is a blocking scrim over the Workbench, not the terminal RunOver view.
- The background remains visible enough to read as the same persistent place but is inert.
- Sample facts use the approved v1 structure. Final live counts come from the run state.

## Samples

| Sample | Hypothesis | Benefit | Risk/cost | Literal vs illustrative |
|---|---|---|---|---|
| `01-banked-victory.png` | Equal, explicit fork after a strong “victory banked” line | Both choices read as honest peers; closest to the existing choice scrim | Slightly menu-like; endless fantasy is quieter | Layout/copy literal; seals illustrative |
| `02-forward-into-the-hour.png` | Treat victory as a compact receipt and make endless the dramatic forward path | Stronger payoff and teaches the cycle visually | More bespoke layout inside the scrim; greater implementation cost | Layout/copy literal; rift/seal ornament illustrative |

## Unity feasibility

| Must-match feature | Unity path | Proven or unresolved |
|---|---|---|
| Blocking Workbench scrim | Existing `_choiceScrim` + USS | Proven |
| Two actions and exact state copy | Existing `InterludeChoiceModel`/choice button path, extended with an endless action | Proven |
| Equal-card direction | Existing flex choice row and card styling | Proven |
| Forward-weighted receipt/cycle direction | New nested flex layout inside existing scrim | Feasible, not yet implemented |
| Reduced motion | Existing Workbench model flag; no required motion | Proven |
| Rift/seal ornament | Nested elements / opacity only; illustrative and may simplify | Feasible |

## Jake direction review

1. Preferred exact sample, combination, or reject all: `01-banked-victory.png`
2. Must keep: equal two-card fork, explicit banked-victory promise, existing Workbench backdrop
3. Most important next change: implement the approved direction in the existing blocking-choice
   component and prove QHD/1080 containment in Unity

## Direction approval

- Approved sample: `samples/01-banked-victory.png`
- Conditions: none
- Date: 2026-07-30

## Implementation acceptance

- Fixture/state: deterministic `beyond-the-hour` Workbench fixture; live runtime path helper uses
  temporary controllers and never touches the player save
- Unity QHD capture: `implementation/unity-2560x1440-beyond-the-hour-r2.png`
- Visual comparison: `implementation/comparison-approved-vs-unity-r2.png`
- 1080p smoke: `implementation/unity-1920x1080-beyond-the-hour-r2.png`
- Interaction/motion evidence: Unity runtime PASS — saved `VictoryChoice` resumed and continued
  into Act 4; retirement completed with banked victory; the live `BuildPlanning` path projected
  both Workbench actions. Static choice has no required motion and uses the same composition under
  reduced motion.
- Deviations: the approved hourglass/rift seals remain illustrative and are omitted; live cards
  use the existing Workbench type tokens instead of the HTML sample's browser font. Modal geometry,
  two-card hierarchy, copy, fact chips, action footers, colors, and Workbench backdrop match. The
  first implementation pass exposed footer overlap with the permanent rail; the final 604px modal
  is anchored at logical y=108 and clears the rail at QHD and 1080p. R1 then retained a 390px card
  minimum inside a roughly 365px clipped row, which cut off each action footer's lower edge. R2
  sizes each card to its row and preserves the authored 20px card-bottom inset; full button borders
  and breathing room are visible at both review targets.
- Jake accepted actual Unity result: R2 approved without conditions
- Date: 2026-07-30

## Review log

- 2026-07-30 — Job created.
- 2026-07-30 — ADR 0030 translated into two measured QHD structural directions over the actual
  Workbench. Mechanics are approved; exact visual direction remains at the direction gate.
- 2026-07-30 — Jake approved `samples/01-banked-victory.png` without conditions. The live
  implementation spec is frozen in `implementation/spec.md`.
- 2026-07-30 — Unity implementation candidate packaged. QHD and 1080p layout matrices pass,
  the live runtime save/continue/retire seam passes, and the final Unity console is clean. Awaiting
  Jake's acceptance of the actual Unity result.
- 2026-07-30 — Jake denied the first Unity candidate: “The buttons look like they are being cut
  off?” Root cause: the endless cards retained a 390px minimum inside a roughly 365px resolved
  choice row, so the row's overflow clip removed the footer's lower edge and bottom inset.
  The rejected evidence remains preserved as revision 1.
- 2026-07-30 — R2 removes the stale minimum, preserves the 20px bottom inset, and adds a structural
  gate for card containment, action containment, action inset, touch height, and label fit. The
  390px negative control fails both cards at QHD; restoring source styles returns the complete
  Workbench contract to PASS. Fresh QHD/1080 matrices pass, client compile is clean, and the final
  Unity console has 0 warnings/errors.
- 2026-07-30 — Jake accepted the R2 actual-Unity result without conditions. Job closed.
