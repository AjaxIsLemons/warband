# UI review: ui-system-foundation-hall-v2

Status: AWAITING_REVIEW  
Created: 2026-07-27

## Brief

- Screen or flow: Shared Last Hour UI foundation, Hourstone Table, and Management Hall Market.
- Primary player decision: Table — what to do before the next beat. Market — which offer changes
  the current build, what the tradeoff is, and whether to buy or hold it.
- Required information and actions: Act/beat route, Hourstone balance, labeled station navigation,
  five Market offers, selected-offer identity and exact rules, recipient comparison where relevant,
  Buy/Hold/Reroll, and the compact fielded Warband.
- Required states: idle, hover/focus, selected, affordable/unaffordable, held, disabled with reason,
  empty detail, comparison target, and keyboard/controller focus distinct from selection.
- Target viewport/aspect ratio: 1920x1080 wide reference; 1280x720 compact desktop contract.
- Must preserve: existing portraits and authored mechanics, deterministic command paths, the
  obsidian/cold-iron/bone/living-Sand identity, current semantic mechanic colors, 3D Table, and
  five-offer Market composition.
- May change: stylesheet ownership, typography, spacing, border density, header/tab composition,
  component boundaries, inspector paging, and compact layout behavior.
- Hard law: zero player scrolling. Overflow becomes named pages, bounded card pagination, or a
  dedicated dossier composition with pinned actions.

## Inputs

| Source | Role |
|---|---|
| `current-market.png` | Current implementation and content-state reference |
| `current-table.png` | Current implementation and 3D Table reference |
| `reference-market-clean.png` | Clean hierarchy and readable sans/mechanic treatment |
| `reference-market-technical.png` | Unified ribbon, component density, and Warband strip |
| `reference-market-ornate.png` | Display serif, exact comparison, and restrained Tower framing |
| `reference-table-cinematic.png` | Cinematic Table hierarchy and dominant next-beat action |

## Assumptions

- Samples are structural and stylistic targets; generated typography and art details are
  illustrative.
- Source Serif-style display roles are paired with Source Sans-style mechanical roles.
- Gold means current choice/commit/time; blue means keyboard/controller focus; mechanic colors
  remain semantic and are never the only signal.
- Desktop is the first implementation target. Phone/tablet are not represented in this review.
- Hero designs, hero content, and gameplay systems are not being redesigned.

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `01-market-evolution.png` | Preserve the current simultaneous browse/decide structure while establishing the new type and token system | Lowest migration risk and closest to current mental model | May retain too much panel density | Layout/material hierarchy is literal; generated copy and art details are illustrative |
| `02-market-decision-workspace.png` | Treat stock, exact decision pages, and actions as one coherent instrument | Strongest zero-scroll component grammar and clearest tradeoffs | Larger component/layout migration | Regions and page model are literal; generated glyphs and text are illustrative |
| `03-table-command-stage.png` | Make the 3D Table the stage and UI the restrained command layer | Strongest world-first identity and clearest next action | Requires disciplined overlay opacity and camera-safe bounds | Composition is literal; rendered environment details are illustrative |
| `02-market-decision-workspace-r2.png` | Combine the low-risk browse/decide structure with one compact Current / Offered comparison, one qualitative rule-swap row, and the persistent roster as the only target selector | Removes duplicate change explanations and makes the decision readable in one scan without tabs or scrolling | Type-specific offer details still need to be proven with worst-case authored content | Region hierarchy, comparison grammar, action placement, and state-color roles are literal; generated portraits, item art, and exact copy are illustrative |
| `03-table-command-stage-r2.png` | Keep the Table world-first, correct the station map, and reduce the wager overlay to a supporting decision surface | Establishes the strongest shared shell while preserving the 3D Table as the identity-bearing element | Recommendation, focus, selection, and commit states need interaction testing in-engine | Station positions, hierarchy, roster persistence, and panel proportions are literal; rendered environment and exact copy are illustrative |

## Independent R&D

Three isolated review agents evaluated the same images without seeing this review log or each
other's recommendations:

- A blind readability review tested scan order, information duplication, typography, and
  comparison comprehension.
- A visual-system review tested type roles, color semantics, framing density, and whether the
  Table and Market felt like one product.
- An interaction-feasibility review tested the zero-scroll contract, keyboard/controller focus,
  bounded content, and 1280x720 implementation risk.

All three independently reached the same core conclusion: the first Market workspace explained
the same change too many times. The `Change` column, numeric `Lose` / `Gain` boxes, repeated stat
summary, and second hero selector should collapse into one exact comparison and one qualitative
rule-swap row. They also converged on the cinematic Table as the stronger visual foundation,
provided its station map was corrected and the wager panel was reduced.

The R2 direction therefore uses:

- One row per comparable stat, with `Current` and `Offered`; the offered value carries its delta
  inline, such as `7 (+2)` or `0.7s (0.7s faster)`.
- One `Rule swap` row only when the offer changes a nonnumeric rule.
- The persistent Warband strip as the sole target selector.
- No ordinary weapon tabs. Named pages are reserved for genuinely different semantic views or
  worst-case content that cannot fit the fixed workspace.
- Serif for locations, item names, and major decisions; sans for mechanics, labels, numbers,
  roster information, and actions.
- Amber for Sand, progression, recommendation, and commit; steel blue for focus/selection; text
  and shape keep these states distinguishable without color alone.
- The exact Table map: Breach up, Market left, Armory right, Hourstone center, Warband down.
- A fixed page contract with no scroll containers at 1280x720 and larger desktop targets.

This is an independent heuristic review, not a substitute for player testing. Before the system
is considered complete, the implementation should be exercised at 1280x720, 1920x1080,
1920x1200, 2560x1080, and 3840x2160 with longest titles/rules, keyboard/controller navigation,
focus restoration, and unaffordable/disabled states.

## Research notes

- Carbon's tab guidance says tabs reduce cognitive load for related categories, but explicitly
  advises against tabs when users need to compare information across groups. That supports a
  single visible weapon comparison instead of splitting the decision across Overview, Rules,
  and Compare.
- Fluent and the WAI-ARIA tabs pattern reinforce short labels, bounded width, clear focus versus
  selection, and predictable left/right navigation when tabs are genuinely needed.
- GOV.UK's table guidance supports rows and columns for direct comparison.
- Baymard's comparison research recommends removing identical attributes, grouping related
  differences, and reducing duplicated comparison data.
- UI postmortems for *Into the Breach* and *Beastieball* reinforce sacrificing decorative ideas
  for clarity and maintaining a consistent visual grammar across the product.

## Jake review

1. Preferred sample, combination, or reject all:
2. Must keep:
3. Most important next change:

## Approval

- Approved sample:
- Conditions:
- Date:

## Review log

- 2026-07-27 — Job created.
- 2026-07-27 — Generated the first comparison set with Codex native image generation.
- 2026-07-27 — `01-market-evolution.png` keeps the simultaneous browse/decide structure and
  tests the shared serif/sans, token, stat, action-dock, and Warband-strip language.
- 2026-07-27 — `02-market-decision-workspace.png` makes Overview / Rules / Compare explicit,
  with a bounded comparison page replacing vertical dossier overflow.
- 2026-07-27 — `03-table-command-stage.png` applies the same language to a world-first Table
  overview with one dominant next-beat action.
- 2026-07-27 — Jake: the set is an okay start and the overall direction is promising, but asked
  for more R&D and independent review. The `Change` column plus separate `Lose` / `Gain` sections
  feels redundant and overbuilt. No sample approved yet.
- 2026-07-27 — Ran three isolated reviews covering blind readability, visual-system coherence,
  and interaction/zero-scroll feasibility. All three independently confirmed the duplicated
  comparison problem and recommended a simplified Market plus the cinematic Table foundation.
- 2026-07-27 — Reviewed established tab, table, comparison, and game-UI guidance. The sources
  support keeping a comparison visible together and reserving tabs for semantic categories.
- 2026-07-27 — Generated `02-market-decision-workspace-r2.png` with one Current / Offered table,
  one rule-swap row, and the Warband strip as target selector.
- 2026-07-27 — Generated `03-table-command-stage-r2.png` with the corrected station map, reduced
  wager panel, shared state/type grammar, and no duplicate Hourstone.
