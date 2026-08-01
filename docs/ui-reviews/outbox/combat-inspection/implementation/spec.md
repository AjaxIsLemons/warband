# combat-inspection — implementation spec

**Approved:** `r4-unit-card.png` + `r4-rank-ladder.png` (Jake, 2026-07-29).
**Supersedes** the r2/r3 samples for anything they disagree on.

## The one-line summary

`InspectorPanel` is already the shared component. This restyles it into the unit card, gives it
two optional bands, deletes the second inspection surface entirely, and routes Deploy through it.

## Information hierarchy — five bands

| Band | Present in | Content |
|---|---|---|
| 0 Portrait banner | Hall only | full-bleed portrait, rank badge overlaid top-right |
| 1 Identity | always | crest/bezel (combat) · eyebrow `KIND · ROLE` · name · subtitle · price (Hall) |
| 2 Facts | always | one compact strip; `current/max` in combat, static in Hall |
| 3 Rules | always | SIGNATURE · WEAPON · PASSIVES · SPECS |
| 4 State | combat only | `TARGETS` row + `LIVE` statuses |
| 5 Decision | Hall only | action row |

Width is the only cross-context variable: **440px** Hall column, **302px** floating in combat.
Portrait: full banner in the Hall, **56px** hex bezel in combat (research legibility floor).

## Section grammar (replaces the current set)

- **SIGNATURE** — trigger chip = mana cost, **Mana teal**. Name + rule.
- **WEAPON** — owns the attack row. `Pike · Honed` hoverable → weapon dossier (profile, temper,
  mastery rider). The damage line is the weapon's line; "BASIC ATTACK" as a concept is retired.
- **PASSIVES** — plural. Name + trigger label + rule.
- **SPECS** — tier-up selections, each with the rank taken at (B/A/S), each hoverable.

`InspectorSectionRole.Deferred` remains a **role** (full text vs one-liner). It is never a heading.

## Colour law

- **Sand/gold = Hourstone cost ONLY.** No other use.
- Signature mana → Mana teal. `WHEN HIT` → Offense orange. Trigger chips inherit the family of
  what they key off.
- `TARGETS` → Space blue (targeting is spatial).
- Team identity → plate spine only. Never on a number (crimson already means "damage you took").
- Rank: C iron / B bronze ◆ / A blue-steel ◆◆ / S gold ◆◆◆ **+ gold spine + sheen** (S only).

## Prose

| Now | Ships as |
|---|---|
| `Acquires the FARTHEST enemy, holds 5 hexes` | `TARGETS  Farthest, held at 5 hexes` |
| `PROTECTION` | `SHIELD` |
| `{Name} · {Role} · {HP} HP · reach {R} · row {N}` | structured enemy row |
| `The Waning begins after 45 seconds.` | magnitude+unit coloured as one run |
| `Formation and rules are final. Combat has no hidden phase.` | deleted |
| `FULL INFO` pill | deleted |
| `CLICK OR TAP A UNIT · OPEN COMBAT CARD` | `Click a unit to inspect` |
| `LIVE COMBAT · 88 HP REMAINING` | deleted (HP is a fact chip) |

`MechanicPresentation.FormatInline` must colour **magnitude + unit as one run** (`45 seconds`,
`3 hexes`, `12 damage`) plus keyword nouns — not every occurrence of ~40 common English words.

## Interaction contract (combat)

The fight **never pauses**. The card must therefore never block the board.

- **Open:** click/tap a body. Picking is `ReplayPlayer.PickUnit` (unchanged).
- **Retarget:** clicking a different body swaps the card's subject in place; it does not stack.
- **Close:** the `×` button · `Esc` · clicking empty board · the unit's fight ending.
- **Never blocks:** the card is the only picking region; the rest of the overlay stays
  `PickingMode.Ignore` so board clicks still reach `PickUnit`. No scrim.
- **Placement:** floats beside the subject's projected screen position, flipped to whichever side
  has room, clamped to the safe-area frame. Tether stem + hex target ring drawn on the body.
- **Follows the fight:** the card re-binds live values on the existing 150ms refresh. If the
  subject dies, the card stays open and shows `DEFEATED` rather than vanishing under the cursor.
- **Keyword tooltips:** the card is pinned, so `RuntimeTooltipService` keyword links are legal on
  it. Hoverable rows (weapon, passives, specs) use the dashed-underline affordance.
- **Focus/keyboard:** hoverable rows are focusable with `tabIndex 0`; `RuntimeTooltipService`
  already opens on `FocusIn`, so keyword disclosure is not pointer-only. `Esc` closes the card
  before it closes anything else on the fight overlay.
- **Reduced motion:** reveal/dismiss transitions collapse to 0ms via the existing
  `motion--reduced` path; the sheen on rank S does not animate.

## Deletions

- `Tooltip.cs` + `.meta`; its spawn in `GameBoot`.
- `.combat-tooltip-*` rules in `MechanicPresentationStyles.uss` (incl. the 9px label that escapes
  the 14px floor).
- World-space text nameplates: `MakeNameplate`, `StyleNameplate`, `UnitView.Nameplate`,
  `TuningData.nameplates`, the `nameplates` block in `tuning.json`. **Bars, status icons and
  stacks are untouched** — they are separate (`UnitView.Icons`).
- The fight inspector scrim + modal wrapper (`fight-inspector-scrim`, `wb-inspector--modal`).

## Acceptance

1. `make check-client` PASS.
2. `make test` PASS (no sim change expected; guards the prose helpers).
3. UI QA captures at 1024×768 / 1280×720 / 1600×900 / 2556×1317 / 3440×1440 for: workbench
   dossier, combat card (ally + enemy), deploy enemy panel.
4. `UiLayoutContract`: no ScrollView in the card, 14px body floor, wrapped text fits, card inside
   the safe-area frame, no overlap with the warband bar or the fight controls.
5. Console 0 errors / 0 warnings in a live fight.
6. Unseen-in-motion items go to `Projects/play-pass.md`, not the board.
