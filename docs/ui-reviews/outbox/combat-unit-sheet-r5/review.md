# UI review: combat-unit-sheet-r5

Status: IMPLEMENTED  
Created: 2026-07-29

## Brief

- **Screen or flow:** click/tap inspection while combat or the sim viewer keeps running.
- **Primary player question:** what is this unit doing right now, and which parts of its build
  explain that behavior?
- **Required information:** identity; live Health, Shield, and Mana; targeting; the complete
  Weapon profile; Signature; Passives; selected Specs; active statuses.
- **Required states:** allied hero, authored enemy, defeated unit, absent Shield/Mana/Signature,
  selected Specs, long Passive copy, and narrow/short landscape viewports.
- **Target viewport:** 1600×900 baseline; 1024×768 through 3440×1440 acceptance matrix.
- **Must preserve:** the approved R5 ownership/order; 16px rule floor; fight never pauses; one
  pinned click surface; card stays inside the safe-area frame; exact rules remain available from
  hover, focus, and touch focus.
- **May change:** combat card width, portrait treatment, placement of combat-only state, and which
  absent regions collapse.

## Inputs

| Source | Role |
|---|---|
| `../unit-dossier-density/samples/07-weapon-glyph-row-r5.png` | Approved information hierarchy and Weapon treatment |
| `../unit-dossier-density/implementation/verification-20260729-173426/ui-qa-20260729-173426-2556x1317-workbench-market-recruit.png` | Verified Unity rendering and density |
| `../combat-inspection/samples/r4-unit-card.png` | Previously approved floating-card placement and no-pause interaction |
| `../../../../client/McpCaptures/board-live-t46.png` | Real combat backdrop |
| `client/Assets/Scripts/Warband/InspectorPanel.cs` | Shared renderer; R5 currently gated to Workbench Recruit/Champion |
| `client/Assets/Scripts/Warband/RunShell.cs:6680` | Live combat adapter (`PlaybackInspector`) |

## What the code says

This is not a second component. Workbench and combat already bind the same `InspectorPanel`.
Combat misses R5 because `IsWorkbenchUnitSheet` explicitly requires
`wb-inspector--workbench`, and the R5 USS block is double-scoped the same way.

Simply widening that condition is unsafe:

1. R5 currently filters the top fact strip down to Health. Combat would lose live Shield and Mana.
2. The Weapon glyph row reads `ManaPerSwing` and `Cleave`, but `PlaybackUnit` does not currently
   carry either fact.
3. Combat selected Specs have names/rules but no glyph in `PathTiers`; the R5 slot would render
   blank.
4. `KeywordNotes` feeds rule tooltips; it does not render active statuses as a visible state row.
5. Enemy bodies borrow a hero chassis for their temporary model. The renderer must not turn that
   borrowed face/spec/mastery into enemy identity.

The correct seam is therefore **one unit-sheet renderer with two data adapters**, not copied markup:

- Workbench adapter = static Health + full B/A/S addresses + purchase/manage actions.
- Combat adapter = live Health/Shield/Mana + selected Spec icons only + targeting/status state.
- Enemy adapter = authored role crest and behavior; omit absent Signature/Specs and never invent
  player mastery from the borrowed chassis.

## Combat anatomy

| Region | Workbench | Combat |
|---|---|---|
| Identity | vertical portrait when wide; banner when narrow | same responsive rule; compact banner at normal combat width |
| Core | authored Health | live Health current/max; Shield and Mana only when present |
| Weapon | Power/Healing, interval, Range, Mana/hit, Crit/Cleave, property | same row using live composed Power/interval/Range and weapon-owned Mana/hit/Crit/Cleave |
| Signature | cost + name + exact rule | same; live Mana remains in Core, never repeated here |
| Passives | self-contained rules | same; online state may tint the row but does not create another sentence |
| Specs | B/A/S addresses, selected icon replaces letter | selected icons only; no invented rank letters |
| Combat state | absent | one compact targeting row plus visible status chips; no `LIVE` mini-header |

## Samples

All samples use real 1600×900 combat geometry, exact R5 hierarchy, and representative live
Phalanx/Sanddrift data. They are coded structural prototypes: typography, wording, card width,
region order, and omission rules are literal; the tether line, portrait crop, and enemy crest are
illustrative.

| Sample | Hypothesis | Benefit | Risk |
|---|---|---|---|
| `01-compact-live-sheet.png` — **recommended baseline** | The R5 narrow mode is the honest default in combat: same anatomy, portrait banner, 388px floating plate | 16px rules and the full Weapon row fit without returning to tiny type; smallest stable footprint | Taller than the old 302px card; long multi-Passive builds need short-viewport compression |
| `02-wide-hero-sheet.png` — adaptive wide | At ≥2560px, the approved vertical hero can return because the viewport can pay for it | Closest literal match to Workbench; strongest identity; shorter card | At 1600px it covers too much battlefield; must be a viewport-locked mode, never reflow as the unit moves |
| `03-enemy-compact-sheet.png` — enemy adapter | Same renderer should simplify honestly when authored enemy regions are absent | No fake hero face, rank, Specs, or mastery; targeting and behavior become the enemy's readable identity | Requires enemy presentation data/role crest to mature beyond current scaffolding |

## Recommendation

Ship the **compact sheet at ordinary combat widths**, and unlock the vertical-hero variant only
from a stable viewport breakpoint where the card remains under roughly one quarter of the safe
width. Decide the mode when the card opens; do not let a moving unit trigger live reflow.

The baseline combat width should move from 302px to roughly **380–400px**. The old width can fit
12px rules or complete information, but not both. R5's 16px floor makes that trade explicit.

## Smallest implementation path

1. Replace `IsWorkbenchUnitSheet` with a unit-sheet context (`Workbench`, `Combat`, `Enemy`) while
   keeping Rank Up/items/equipment preview excluded.
2. Make the section grouping shared: Weapon → Signature → Passives; keep Core and Specs in their
   existing bands.
3. Extend `PlaybackUnit` with the two missing immutable Weapon facts (`ManaPerSwing`, `CleavePct`)
   and project/clone them with the other base stat facts.
4. Populate the combat adapter with visible status chips, selected Spec glyphs, and a
   combat-safe Weapon property. Skip player mastery for authored enemies.
5. Add combat R5 USS scoped to `.wb-inspector--combat.wb-inspector--unit-sheet`, preserving the
   current floating placement, tether, 150ms live refresh, click-through outside the card, and
   Escape/empty-board close behavior.
6. QA allied/enemy/defeated/long-copy states across the existing viewport matrix.

## Jake review

1. Preferred baseline: `01-compact-live-sheet`, `02-wide-hero-sheet`, a breakpoint combination,
   or reject all.
2. Must keep:
3. Most important next change:

## Approval

- Approved sample: `samples/01-compact-live-sheet.png`
- Conditions:
  - Use the honest enemy omission rules shown by `samples/03-enemy-compact-sheet.png`.
  - Keep Workbench and combat on one list-driven unit-sheet renderer. New Weapon facts,
    Weapon properties, and Passives must append through the model without renderer changes.
  - Combat remains live and non-pausing while the sheet is open.
- Date: 2026-07-29

## Review log

- 2026-07-29 — Inspected the shared renderer, live combat adapter, R5 implementation, prior combat
  contract, and real fight geometry. Produced three R5 combat adaptations. AWAITING_REVIEW.
- 2026-07-29 — Jake approved `01-compact-live-sheet` for implementation, with the explicit
  requirement that future Weapon changes and additional Passives flow through the shared sheet
  rather than requiring a second combat-specific layout. APPROVED_FOR_IMPLEMENTATION.
- 2026-07-29 — Implemented one structured unit-sheet renderer for Workbench and live combat,
  added honest allied/enemy adapters, projected the missing immutable Weapon facts through replay
  format v10, and added list-growth stress coverage. Verified 534 model tests, the client source
  check, golden plus 14 scenario replay round-trips, an 8-capture Unity responsive matrix, and a
  clean Unity console. Evidence:
  `implementation/verification-20260729-184037/`. IMPLEMENTED.
