# Responsive Workbench Rank Up — implementation contract

Approved source: `../samples/01-one-page-choices-r1.png` on 2026-07-27.

## Decision hierarchy

Rank Up is one decision surface, never a sequence of dossier pages:

1. Hero identity, current → next rank, price, and guaranteed HP/power gains.
2. A compact B/A/S ladder showing selected, pending, and locked specialization tiers.
3. Both exact deterministic specialization options, side by side.
4. The existing Buy/Hold actions, pinned above the permanent warband rail.

Per-option before/after tables are omitted. Keyword and selected-tier rules remain available
through the shared tooltip system.

## Required behavior

- Buying preserves the existing transaction: guarantee the rank/chassis gain, then enter the
  blocking specialization choice. Resolving it fills the pending ladder tier.
- Completed tier slots show rank, theme glyph, and selected node name. The next tier is visibly
  pending; future tiers are locked. All three slots remain visible.
- Rank Up never enables detail pagination or scrolling.
- Selection may move from a Market offer to an owned hero and back without retaining the wrong
  dossier.
- Pointer hover, keyboard focus, navigation submit, and reduced-motion behavior use the existing
  tooltip and polish systems.

## Responsive contract

- Full Workbench: 1024×768, 1280×720, 1600×900, 2556×1317, and 3440×1440 landscape.
- Phone portrait uses the existing rotation guard.
- Market artwork resolves to at least 96 logical px normally and 72 logical px on genuinely
  narrow/short layouts.
- Every visible command and title fits its own box; all dossier content remains above actions and
  the permanent rail.

## Verification

- Deterministic fixtures cover B/A/S ladder states, nominal and 130% copy, plus tier tooltips.
- Layout checks inspect all `TextElement` implementations, including buttons; assert artwork
  floors, no Rank Up pager, no ScrollView, and action/rail separation.
- The responsive runner outputs structural results, semantic diagnostics, and labeled captures.
- Final captures are compared with the approved sample. Structural PASS and visual review are
  reported separately.
