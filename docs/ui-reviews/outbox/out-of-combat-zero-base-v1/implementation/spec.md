# Approved Workbench implementation contract

Status: BUILT_AND_UNITY_VERIFIED  
Approval date: 2026-07-27

## Implementation result

- `WorkbenchView` replaces the old Management destination with one retained, bounded Workbench.
- `RuntimeTooltipService` is shell-owned and shared by the Workbench dossier, permanent hero
  rail, equipment sockets, specializations, semantic facts, keywords, and disabled actions.
- Market and Armory are mutually exclusive expanded modes. Armory uses a fixed six-item page;
  the permanent rail exposes six field and two reserve addresses without a scroll container.
- Prices and the run balance use the Hourstone glyph plus value. Redundant post-purchase
  arithmetic and `NEED N MORE` commerce labels are absent.
- Existing curated portraits and code-native glyphs were used. Generated concept art remains
  reference-only, per the approval boundary.
- The existing semantic feedback bus, bounded FX layer, audio/haptic services, reduced-motion
  handling, and central presentation config are reused; no parallel animation/audio framework
  was introduced.
- Deterministic fixtures and a reusable layout contract cover Workbench containment, fixed
  collections, minimum type/region sizes, text fit, pinned actions, the permanent rail, and both
  approved tooltip bounds. The final 50-case matrix passed at 1280×720, 1920×1080, 2558×1313,
  2560×1440, and 2560×1080, including 130% copy stress.
- The connected Editor had no Game View window open, so the harness used its labelled exact-size
  offscreen `UIDocument` target fallback. Captures and the structural report are in
  `client/TempCaptures/ui-qa/20260727-141414/`.
- The reusable iteration workflow and next-surface migration order are documented in
  `foundation.md`.

## Visual authority

- `../samples/01-workbench-market-recruit-r5.png` owns the default Workbench hierarchy, Market
  offer shelf, full live-offer dossier, permanent hero rail, and semantic treatment.
- `../samples/01-workbench-armory-mode-r4.png` owns the mutually exclusive Armory work mode and
  full projected-hero behavior. R5's semantic treatment supersedes R4's generated color details.
- `../samples/01-workbench-tooltip-keyword-r6.png` owns the compact keyword tooltip bound.
- `../samples/01-workbench-tooltip-equipment-r6.png` owns the maximum normal equipment-tooltip
  density.
- Generated portraits, item paintings, typography errors, and ornamental details are
  illustrative. Runtime uses existing curated project art and code-native decoration.

## Required composition

At the 1280×720 desktop reference:

1. A compact run ribbon shows act, beat, the Sand icon and current balance, `WORKBENCH`, and the
   next run commitment.
2. Market mode shows all five live offers and reroll in one fixed shelf.
3. Selecting or focusing an offer opens its complete center dossier. A Recruit uses the same
   combat-rule grammar as an owned hero and includes acquisition result and explicit action.
4. The center dossier prioritizes identity, semantic facts, Basic Attack, Signature, Passive,
   keywords, context, equipment, and mastery. It never displays duplicated old/new hero columns
   or Lose/Gain sections.
5. The permanent bottom rail shows active heroes, reserves, legal empty slots, and Weapon/Trinket
   sockets. Hero and socket hover/focus feed the same inspection system.
6. Armory is closed by default. Opening it collapses Market to a one-line summary and exposes a
   fixed paged item grid. Market and Armory are never expanded together.
7. There is no `ScrollView` under the Workbench root. Dossiers and collections use bounded pages.

## Commerce copy

- Visible currency presentation is the Sand icon followed by the value.
- Do not render the word `SAND` in the run ribbon, offer price, action label, or receipt.
- Accessible labels and source descriptions may still say “Sand” so the icon has a spoken name.
- Do not render `COST … · … REMAINS`. The player already sees price and current balance.
- Affordability remains explicit through enabled state, price tone, and a tooltip/disabled reason
  when the player cannot act.

## Inspection and tooltip behavior

- Pointer hover and keyboard/controller focus produce equivalent preview content.
- Click, tap, or Submit pins a full dossier. Cancel restores the exact invoking focus.
- One custom runtime tooltip overlay serves every screen. It is not Unity's Editor-only tooltip.
- Tooltip kinds initially cover keyword, item/equipment, semantic fact, disabled reason, and
  compact receipt.
- Keyword tooltip: glyph, written keyword, written domain, concise definition, source context.
- Equipment tooltip: identity, equipped/stored location, up to four semantic facts, mastery rule,
  active/inactive state.
- A transient tooltip has no interactive children, nested targets, or scrolling. Overflow hands
  off to the pin action.
- Placement tries above, below, right, and left, then clamps inside the safe area.
- Delayed shows are generation-guarded so recycled or rapidly crossed elements cannot open stale
  content.

## Semantic theme

- Steel blue: focus, selection, compatible target, and projected state only.
- Sand gold: currency and irreversible commit.
- Mechanic families retain their existing glyph/color pair:
  Durability coral, Offense orange, Restoration green, Space blue, Time violet, Mana cyan,
  Protection teal, Currency gold.
- Rule-domain badges retain written domain plus glyph. Domain and mechanic colors may coexist but
  are never substituted for one another.
- Every semantic color has a glyph and written label; color is never the sole carrier.
- Rule copy uses structured semantic spans where available and the existing formatter only as a
  migration bridge.

## Motion, effects, audio, and tuning

- Reuse `UiPolishSignals`, `UiFeedbackDirector`, `UiFxLayer`, `UiAudioDirector`, and platform
  haptics. Do not introduce a parallel feedback bus.
- Controls emit semantic events; they never play audio or own transaction truth directly.
- Local hover/focus/pin/drawer transitions use USS opacity, translate, tint, and restrained scale.
- Painter2D is reserved for bounded selection traces, compatibility wake, transfer/item-seat,
  purchase, and commit flourishes.
- Tooltip reveal/dismiss, pin/unpin, drawer expand/collapse, compatible socket wake, projection
  change, selection, purchase, equip, route, and error all have tuneable feedback recipes.
- Timing, easing, offset, scale, effect intensity, sound family, cooldown, and haptic family live
  in the existing presentation configuration.
- Reduced motion, reduced flash, UI audio enable/volume, and haptics do not change interaction
  state or block input.

## Functional actions

The coded slice must route existing authoritative commands for:

- select/inspect offer;
- buy Recruit, Weapon, Trinket, or Inscription when legal;
- hold/unhold offer;
- reroll;
- select/inspect owned hero and equipped item;
- open/close Armory and page it;
- select owned item and reveal compatible sockets;
- equip, transfer, swap, unequip, and reforge where the current run API permits;
- continue to the next run commitment.

Selection and projection never commit. Disabled actions expose their exact reason.

## Acceptance

- Clean Unity compile and no unexpected console errors.
- Relevant Edit Mode tests for tooltip placement, stale-show cancellation, semantic models,
  no-scroll composition, paging, and inspection state.
- Play Mode or deterministic UI harness covers hover/focus parity, pin/unpin, drawer mode,
  offer selection, and socket targeting.
- Captures at 1280×720 and 1920×1080 for Market Recruit, Armory, keyword tooltip, and equipment
  tooltip.
- Full deterministic capture matrix at 1280×720, 1920×1080, 2558×1313, 2560×1440, and 2560×1080,
  with nominal and 130% copy fixtures.
- Worst-case real rule text, six active plus two reserves, last Armory page, and safe-area edge
  anchors fit without scrolling or clipped essential copy.
- Intentional differences from approved samples are listed beside verification captures.
