# Reusable UI foundation: implementation and iteration guide

Status: IMPLEMENTED_UNITY_VERIFIED  
Date: 2026-07-27

## What shipped

The Workbench is the first route on the reusable out-of-combat UI foundation:

- one fixed, zero-scroll surface for Market, full selected-object dossier, Armory, and the
  permanent six-field/two-reserve unit rail;
- one shell-owned runtime tooltip service for keyword, equipment, semantic fact, disabled-reason,
  and receipt disclosure;
- semantic mechanic glyph/color/label rendering and project-owned Inter/Barlow Condensed fonts;
- centrally tunable interaction recipes for tooltip reveal/dismiss, pin/unpin, drawer
  expand/collapse, compatible-socket wake, and projected-target change;
- semantic cue routing into the existing bounded FX, audio, haptic, and reduced-motion systems;
- deterministic fixtures, resolved-layout contracts, exact-size captures, reports, and contact
  sheets.

This is intentionally a consolidation of the existing presentation stack. `WorkbenchView`,
`InspectorPanel`, `RuntimeTooltipService`, `WarbandBarView`, `MechanicPresentation`,
`UiPolishSignals`, `UiFeedbackDirector`, and `UiFxLayer` are the current extension seams. A later
pure `InspectableModel` factory can replace remaining route-specific projections without
discarding the controls or verification harness.

## 2026-07-27 correction pass

The first live review established five additional invariants:

- the dossier starts with the selected object's identity; it has no redundant
  `INSPECTION DOSSIER` chrome row;
- clicking any visible Market offer makes Market the authoritative inspection context, even after
  the permanent rail moved focus to an owned unit;
- ability cost is semantic context (`SIGNATURE` + Mana glyph + value), while passive headings omit
  the meaningless `ALWAYS` suffix and retain only informative triggers;
- already selected traits occupy one bounded ribbon beside keyword chips and expose their exact
  rules through the shared runtime tooltip service;
- Wager navigation and commitment live in the top command band, while the whole Wager surface
  explicitly reserves the permanent rail's height.

The correction pass was exercised through the real owned-unit → Market selection path and direct
resolved-layout captures at 1280×720 and 2558×1313. The long-copy Rank Up dossier and Wager/rail
non-overlap contracts pass without a `ScrollView`. Current visual evidence is in
`client/McpCaptures/workbench-rankup-traits-1280x720.png`,
`client/McpCaptures/workbench-rankup-traits-2558x1313-settled.png`,
`client/McpCaptures/workbench-selected-traits-2558x1313.png`, and
`client/McpCaptures/wager-rail-safe-2558x1313.png`.

## Fast iteration loop

Use the Editor menu:

- `Warband/UI QA/Run Workbench Smoke Matrix` — five representative states at 1280×720 and the
  live 2558×1313 reference;
- `Warband/UI QA/Run Workbench Full Matrix` — 50 cases: nine states at five viewports plus Recruit
  copy expansion at every viewport;
- `Warband/UI QA/Cancel Active Run` — cancel without changing run/save authority.

The full fixture catalog is:

`market-recruit`, `market-rankup-long`, `market-weapon`, `market-inscription`, `armory-empty`,
`armory-full`, `rail-full`, `tooltip-keyword`, and `tooltip-equipment`.

The full viewport set is 1280×720, 1920×1080, 2558×1313, 2560×1440, and 2560×1080. The foundation
full-matrix run is `client/TempCaptures/ui-qa/20260727-141414/`; the correction-pass evidence is
listed above.

Each case binds a deterministic projection directly to the retained Workbench and rail. It does
not mutate the run controller, save, economy, or command history. The report fails on:

- any `ScrollView` under Workbench;
- unresolved or escaped fixed regions;
- dossier content behind pinned actions;
- Market or Armory items outside their fixed grids;
- undersized rule sections, columns, or contract type;
- wrapped body/title/subtitle text that does not fit;
- tooltip escape, clipping, or undersized body type.

Pixel captures remain a human review artifact. Structural geometry is the automated gate.

## Unity workflow gotchas

The remote Editor may have no Game View window. The harness first requests the exact Game View
size; when no window exists, it renders the same `UIDocument` to an exact-size temporary target
texture and labels the result `offscreen-panel-fallback`.

After changing a runtime `.uss`, import it and restart Play Mode before trusting captures. An
already constructed `UIDocument` can retain the previous `StyleSheet` object even after the asset
imports. C# changes also require a clean Edit Mode compile before starting the matrix; compiling
inside Play Mode can invalidate retained scene references.

For remote automation, `WarbandUiQa.FinalizeIfComplete()` closes a run when the Editor idles
immediately after its final capture.

## Layout policy

The desktop floor is 1280×720. Normal 16:9 preserves the full offer art and dossier identity.
Ultrawide is an explicit compact composition selected by aspect ratio, not a uniformly scaled
desktop canvas:

- the Market yields art height;
- the dossier and permanent rail retain readable type;
- redundant Rank Up and compact weapon subtitle copy yields before exact rules;
- the Armory remains six fixed items per page;
- projected-hero rules stack beside the equipment projection instead of truncating horizontally.

No supported desktop composition introduces a runtime scroller.

## Motion, VFX, and audio extension rules

Controls emit semantic `UiPolishCue` values after state changes. Presentation recipes in
`HubPresentation.json` own duration, easing, travel, scale, intensity, priority, and cooldown.
The existing director maps those cues to:

1. USS transitions for local opacity, translate, border, tint, and small scale;
2. the retained `UiFxLayer` for bounded selection, compatibility, seat/transfer, purchase, and
   commitment flourishes;
3. pooled audio/haptic outputs by semantic family.

UI audio remains disabled by configuration until authored cue identity and mix are reviewed.
Reduced motion, reduced flash, mute, and haptics never alter interaction truth.

## Next-screen migration

Migrate by player commitment, not by old destination:

1. **Choice Gate** — reuse semantic facts, fixed option regions, tooltip layer, action dock,
   polish cues, fixtures, and layout contracts for Wager, specialization, Interlude, and boss
   reward. Keep the permanent rail read-only.
2. **Deployment** — reuse full hero/equipment inspection, tooltip placement, permanent rail, and
   layout contracts around the disclosed board. Only formation is spatial.
3. **Result** — reuse dossier sections, receipt facts, action hierarchy, and capture fixtures for
   explanation/replay before the route returns to Workbench.
4. **Combat inspection** — consume the same hero/item/keyword projections in a bounded overlay;
   do not fork another tooltip or mechanic-theme system.

Each migration should add deterministic fixtures first, define fixed-region and copy contracts,
then build its composition and semantic feedback. Do not remove the prior route until the new
surface passes its matrix and is watched with real pointer/keyboard/controller interaction.
