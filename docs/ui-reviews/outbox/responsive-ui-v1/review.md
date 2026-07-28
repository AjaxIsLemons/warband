# UI review: responsive-ui-v1

Status: IMPLEMENTED
Created: 2026-07-27

## Brief

- **Screen or flow:** Workbench at Jake's 2556×1317 Game View, focused on a selected Rank Up
  offer and the permanent warband rail.
- **Primary player decision:** understand the guaranteed rank gain and both specialization
  options, then Buy or Hold without opening redundant internal pages.
- **Required information and actions:** five Market offers, visible offer art, reroll, selected
  hero/price, rank/health/power gain, both specialization rules, Buy, Hold, Armory drawer,
  permanent field/reserve/equipment targets, and the next-Wager command.
- **Required states:** nominal Rank Up, 130% copy stress, selected traits, unaffordable/held offer,
  3/3 early rail, full 6+2 rail, empty/non-empty Armory.
- **Target viewport/aspect ratio:** Jake's 2556×1317 (~1.94:1), plus 1600×900 authoring,
  1280×720 floor, and 1024×768 squeeze coverage.
- **Must preserve:** approved Workbench object-selection flow; five simultaneous offers; selected
  object dossier; no runtime scrolling; permanent warband/equipment rail; semantic keyword color;
  Market and Armory mutually exclusive; exact rules available before purchase.
- **May change:** Rank Up's internal page model, Market/card height allocation, header command
  width, rail command proportions, and the QA contract that defines "fits."

## Inputs

| Source | Role |
|---|---|
| `inbox/responsive-ui-v1/Screenshot 2026-07-27 160658.png` | Full-flow evidence: header and rail text escape, art-starved Market, redundant Rank Up page |
| `inbox/responsive-ui-v1/whack 1.png` | Rank Up page 1: prose summary of facts already present in header chips |
| `inbox/responsive-ui-v1/whack 2.png` | Rank Up page 2: exact before/after rows repeating the same header gains |
| `inbox/responsive-ui-v1/whack 3.png` | Rank Up page 3: the only page containing the actual 1-of-2 decision |
| `outbox/out-of-combat-zero-base-v1/samples/01-workbench-market-recruit-r5.png` | Approved hierarchy/art authority for Market and full dossier |
| `client/TempCaptures/ui-qa/20260727-160020/` | Previous structural matrix; useful evidence of what the old gate did and did not prove |

## Diagnosis

1. `UiEnvironment` emits the compatibility `layout--compact` class whenever panel height is under
   960. The shipping panel is authored at 1600×900 and height-locked, so effectively every normal
   Workbench capture receives the compact profile. The Market's compact card math reserves
   104 px for 18 px classification + 34 px title + 68 px art; flex shrink makes the art pay the
   impossible 120 px sum.
2. `InspectorPanel.RefreshDetailPagination` explicitly pages every Rank Up with multiple sections,
   regardless of available width. The model supplies a prose gain section, an exact comparison
   section, and the choices section, even though rank/health/power are already visible in the
   overview chips. Navigation exposes implementation sections instead of serving one player
   decision.
3. The layout contract validates the header/rail command **containers**, but text-fit assertions
   only query selected `Label` classes. UI Toolkit `Button` is a `TextElement`, so button text can
   draw outside a passing box. `warband-bar__manage` and `warband-bar__armory-hint` are not covered
   by single-line/wrapped-text assertions either.
4. The previous visual review sampled a few captures rather than every affected state and never
   reviewed Rank Up as a page sequence. The reported 57/57 result was true only for the structural
   selectors that existed; it was not sufficient evidence that the screen was visually sound.

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `samples/01-one-page-choices-r1.png` — **Direct choice dossier (recommended)** | Rank/health/power belong in the overview; the only body content needed is both exact specialization options | Removes all internal arrows and both redundant gain pages; keeps authored rule prose and keyword color; gives Market art a useful window; contains all commands | Long future fork rules need a strict two-card text budget and tooltip fallback for keyword definitions | One-page hierarchy, larger art allocation, command containment, both exact choices, action dock, and rail proportions are literal; font and small ornament are illustrative |
| `samples/02-comparison-matrix-r1.png` — **Comparison matrix** | Forks are easier to compare when their shared trigger and divergent engine are decomposed into rows | Extremely scannable; common behavior is obvious; no internal pages | Requires mechanically decomposing authored prose and can accidentally paraphrase away exact rules; more authoring machinery | Matrix structure is literal; the rewritten ROLE/ON KILL/ENGINE copy is illustrative and would require a new exact-rule projection contract |

## Recommended implementation contract

If `01-one-page-choices-r1.png` is approved:

1. Rank Up never uses detail-section arrows. The overview owns rank and guaranteed stat gains;
   the dossier body owns both specialization cards. Traits are compact hover targets beside the
   Basic Attack, Signature, or Passive rule they modify; they never become a separate page.
2. Remove the always-compact height condition. Geometry classes describe actual width/height; a
   dedicated Workbench density rule allocates at least 86 logical px of visible art at 1600×900
   and Jake's 2556×1317 viewport.
3. Size header/rail commands from their longest real copy, keep their text inside the command,
   and avoid free-floating arrow glyphs outside the button.
4. Extend layout checks from `Label` to `TextElement`; assert full text fit for next Wager,
   Manage Warband, Armory hint, Buy/Hold, page names, and every Market title. Assert a minimum
   resolved Market-art height.
5. Add 2556×1317 to the deterministic matrix and capture Rank Up nominal + 130% copy + traits.
   Review every changed capture before reporting completion. Structural PASS and visual review
   are reported separately.

## Jake review

1. Preferred sample, combination, or reject all:
2. Must keep:
3. Most important next change:

## Approval

- Approved sample: `samples/01-one-page-choices-r1.png`
- Conditions: add a persistent B/A/S selected-tier ladder; preserve preview → buy → blocking
  specialization choice; implement Rank Up plus the reusable responsive/QA foundations first.
- Date: 2026-07-27

## Review log

- 2026-07-27 — Job created from Jake's dated Workbench screenshot and `whack 1–3`.
- 2026-07-27 — Diagnosed the always-compact classifier, impossible Market-card height sum,
  unconditional Rank Up pagination, and container-only text assertions.
- 2026-07-27 — Rendered two 1600×900 structural prototypes with real Warband portraits and rules.
  Recommended the direct one-page choice dossier; status set to `AWAITING_REVIEW`.
- 2026-07-27 — Jake approved `01-one-page-choices-r1` as the implementation target. The focused
  pass adds the B/A/S ladder and keeps the existing two-step rank-up transaction.
- 2026-07-27 — Implemented the typed one-page Rank Up body, B/A/S ladder, exact option cards,
  responsive Market/art correction, button-aware text contracts, semantic QA diagnostics, and
  deterministic B/A/S/tooltip fixtures. Headless smoke is 13/13 structural PASS at 1280×720 and
  1600×900; implementation captures are under `implementation/`.
- 2026-07-27 — Removed the separate Traits page and placed themed hover labels directly beside
  their Basic Attack, Signature, or Passive context. Fixed the live pending-fork duplicate-offer
  crash (`sharpshot|A|-`) with a safe preview API, disabled waiting state, simulation regression,
  and an exact temporary-controller Unity regression.
- 2026-07-27 — Verification complete: 217/217 simulation tests, 15/15 Workbench smoke captures,
  and 82/82 full responsive captures pass. The full matrix covers
  1024/1280/1600/2556/3440, expanded copy, phone, Armory, keyword/equipment/rank-tier tooltips,
  route surfaces, and rotation guard. Final evidence:
  `client/TempCaptures/ui-qa/20260727-191233/report.md`.
- 2026-07-27 — Replaced detached glossary chips with semantic phrases inside authored rule prose:
  `Gain 1 Riposte` now makes **Riposte** the themed hover/focus target and tooltip source without
  splitting the sentence's wrapping layout. Added a Workbench-only full matrix entry point so UI
  iteration can finish independently of unrelated route fixtures. Post-migration verification is
  **65/65 PASS** across all Workbench fixtures at 1024/1280/1600/2556/3440; final evidence:
  `client/TempCaptures/ui-qa/20260727-202843/report.md`.
