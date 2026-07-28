# Warband reusable UI system — Unity 6 R&D and implementation contract

Status: foundation implemented and Unity-verified; Workbench is the first migrated surface  
Date: 2026-07-27  
Applies first to: Workbench  
Intended reuse: Choice Gate, Deployment inspectors, Result, combat inspection, and later events

Implementation and iteration details now live in
`../implementation/foundation.md`. The immutable inspection boundary remains a migration target;
the shipped first slice consolidates the existing `InspectorPanel`, mechanic presentation,
runtime tooltip, rail, and feedback systems behind the Workbench instead of delaying the route on
a wholesale namespace rewrite.

## Outcome

Build one object-inspection and presentation system, not another collection of screen-specific
widgets. The Workbench is its first composition:

- selecting any live Market offer opens that object's complete dossier in the center;
- hovering or focusing a compact fact opens one bounded contextual tooltip;
- clicking, tapping, or submitting pins the appropriate full read;
- the permanent War Band rail remains the owner and equipment-target surface;
- Market and Armory are mutually exclusive expanded work modes;
- state changes are immediate and deterministic, while motion, sound, haptics, and decorative
  effects observe semantic feedback events;
- every supported composition is fixed, paged, or swapped—never scrolled.

This is a consolidation, not a blank technical rewrite. Warband already has most of the difficult
low-level pieces: semantic mechanic colors, exact-rule models, a hover/focus rules popover,
progressive Inspector models, a semantic feedback bus, tunable motion recipes, a Painter2D effect
layer, reduced-motion handling, audio pooling, haptic output, and transaction receipts. The new
system gives those pieces one boundary and removes screen-owned variants.

## Non-negotiable contracts

1. **Selection never commits.** Authoritative run commands happen only from explicit verbs.
2. **Essential information is stable.** Tooltips supplement a dossier; they do not contain the
   only copy of a combat rule, price, deficit, or disabled reason.
3. **One object, one full read.** Recruit, hero, item, Inscription, encounter, and reward all use
   the same center dossier grammar with kind-specific pages.
4. **No runtime `ScrollView` in the Workbench route.** Long collections page; long dossiers have
   named fixed pages; narrow compositions swap authored regions.
5. **Hover and focus are peers.** Pointer enter/leave and focus in/out produce the same preview.
   Touch uses inspect/pin gestures without assuming hover.
6. **Color is never the only carrier.** Every semantic color has a glyph and a written label.
7. **Presentation cannot own simulation truth.** Views consume immutable projections and emit
   typed intent. They do not calculate price, legality, result, mastery, or composed stats.
8. **Feedback cannot own interaction state.** Motion, particles, sound, and haptics may lag,
   shorten, or be disabled without changing the result.
9. **The supported floor is authored at 1280×720.** Larger 16:9 viewports scale cleanly. Other
   supported aspect ratios use explicit composition breakpoints and safe-area insets, not a
   scaled-down desktop canvas or a hidden scroll fallback.

## Visual and semantic layers

The theme must keep four jobs separate:

| Layer | Meaning | Examples |
|---|---|---|
| Material | Fictional surface and hierarchy | obsidian, iron, parchment, muted brass |
| Interaction | focus, selection, compatibility, projected target | steel blue outline/brackets |
| Economy and commitment | Sand, price, irreversible commit, receipt | Sand gold |
| Rules and facts | mechanic family and rule domain | HP coral, Power orange, Reach blue, Time violet, Mana cyan; Tempo, Ward, Mending, etc. |

The implementation should retain `MechanicPresentation` as the source for mechanic-family facts
and `LexKind` as the source for rule-domain identity. They are related but not interchangeable:
`HASTE` may use the violet Time fact color while also carrying a written `TEMPO` domain badge.
Focus blue must not be reused as a generic “positive” color, and Sand gold must not become a
selection color.

The existing project values remain authoritative:

- Durability: `rgb(232, 111, 116)`
- Offense: `rgb(239, 151, 78)`
- Restoration: `rgb(101, 211, 154)`
- Space: `rgb(104, 174, 238)`
- Time: `rgb(181, 139, 235)`
- Mana: `rgb(83, 211, 213)`
- Protection: `rgb(91, 190, 203)`
- Currency: `rgb(228, 177, 64)`

USS custom properties should expose these values, but C# semantic models remain the source that
chooses which token applies. View-specific USS may arrange components; it must not redefine the
meaning of blue, gold, or a mechanic family.

## Text rendering contract

Text is part of the layout system, not decoration applied after the geometry is frozen.

- Use project-owned TextCore font assets and explicit fallback assets. Do not depend on operating
  system fonts or assume that ornamental Unicode glyphs exist in the primary face.
- Keep a display face for object identity and a highly legible interface face for rules, values,
  actions, and metadata. Exact rules do not use small caps or wide tracking.
- Define type tokens for `display`, `title`, `section`, `body`, `fact`, `label`, `micro`, and
  `button`. Each has a tested minimum size and line height at 1280×720.
- Do not scale text-bearing elements to solve layout pressure. Use fixed pages, alternate
  composition, or shorter authored labels. Transform scale is permitted only as a brief feedback
  accent on a parent and must settle at `1`.
- Render inline rule meaning from structured spans such as `SemanticTextSegment`, not by applying
  regex color to a finished prose string. Each segment carries text, mechanic family, optional
  icon, and accessible label. `MechanicPresentation.FormatInline` can remain as a compatibility
  bridge while content moves to structured segments.
- Rule blocks reserve lines by model contract. Overflow is a failed projection/test, not a reason
  to reduce the font or introduce a local scroller.
- Test worst-case real English copy, 130% expansion fixtures, missing-glyph fixtures, and all
  numeric extremes that can occur in the first playable.

## Architecture

```text
Run state / catalog / exact-rule projections
                    │
                    ▼
        InspectableModel factory layer
                    │
          ┌─────────┴─────────┐
          ▼                   ▼
 InspectionCoordinator   SemanticTokenCatalog
          │                   │
          └─────────┬─────────┘
                    ▼
      Reusable UI Toolkit custom controls
                    │
        typed intent + UiPolishSignals
          ┌─────────┴──────────────┐
          ▼                        ▼
 authoritative command       UiFeedbackDirector
                                     │
                          motion / Painter2D / audio /
                          haptics / accessibility modes
```

### 1. Pure projection models

Introduce immutable, Unity-independent presentation records:

```text
InspectableModel
  Id
  Kind
  Identity
  Context
  Pages[]
  Actions[]
  Attention[]

DossierPageModel
  Id
  Label
  Sections[]

TooltipContentModel
  Kind
  Identity
  Domain
  Body
  Facts[]
  Context
  PinIntent
```

`InspectableKind` begins with `Hero`, `RecruitOffer`, `Weapon`, `Trinket`, `Inscription`,
`Encounter`, `Reward`, and `RuleReference`. Sections use typed fact, rule, equipment, receipt,
warning, and action models rather than view-ready strings.

Factories consume the authoritative run snapshot and catalog. A hero factory produces the same
composed dossier whether invoked from the bottom rail, a recruit result, Deployment, or combat
inspection. A projected-equip factory produces one complete future hero plus restrained change
markers. It does not produce current/new hero columns or Lose/Gain boxes.

The model layer owns no `VisualElement`, callbacks, coroutines, audio clips, colors, or USS class
names. Pure Edit Mode tests can therefore prove price, legality, source location, rule context,
page count, and worst-case line budgets without opening a scene.

### 2. Inspection coordinator

`InspectionCoordinator` is the single state owner for full reads:

```text
Rest(object)
  └─ focus/hover ──> Preview(object)
       ├─ leave/blur ──> Rest(previous pinned object)
       └─ click/tap/submit ──> Pinned(object)

Pinned(hero)
  └─ select item + focus compatible socket ──> Projected(hero, item)
       ├─ cancel/leave ──> Pinned(hero)
       └─ explicit Equip/Swap ──> command → Pinned(updated hero)
```

The coordinator tracks a stable `SelectionOrigin` so closing a drawer, unpinning a dossier, or
canceling a projected equip restores focus to the exact invoking element. Work mode
(`MarketExpanded` or `ArmoryExpanded`), Armory page/filter, and pinned object are orthogonal state;
opening a drawer must not feel like navigation or destroy selection.

Transient tooltips are a parallel overlay state and never replace the coordinator's current
dossier. Their source declares a `PinIntent`:

- equipment or offer source → pin the object's full center dossier;
- hero source → pin the full hero dossier;
- keyword or stat source → pin a stable rule-reference card until Cancel/Back;
- disabled action source → retain the current dossier and pin the exact reason.

### 3. Reusable UI Toolkit controls

Use public partial custom controls with `[UxmlElement]` and focused, explicit `Bind(model)`
methods. Prefer UXML templates for structure and USS for visual states.

Initial controls:

- `WarbandDossierElement`
- `DossierRuleBlockElement`
- `SemanticFactTileElement`
- `KeywordChipElement`
- `ContextChipElement`
- `EquipmentSocketElement`
- `MarketOfferElement`
- `MarketShelfElement`
- `ArmoryDrawerElement`
- `WarbandRailElement`
- `RuntimeTooltipLayerElement`
- `PagedRegionElement`
- `DecisionActionElement`

Controls register callbacks in `AttachToPanelEvent`, unregister them in `DetachFromPanelEvent`,
and expose typed intents rather than reaching into the run controller. Binding is explicit and
event-driven; Unity's runtime data binding is useful for editor-style data, but this gameplay UI
benefits from predictable rebinding, low churn, and testable snapshots.

Do not create one `UIDocument`, panel, or GameObject per tooltip/card. The route uses one retained
tree with one overlay child so focus, panel coordinates, sorting, safe area, and feedback remain
coherent.

### 4. Runtime tooltip service

Unity's `TooltipEvent` is Editor-only, so the game needs a custom runtime service. One
`RuntimeTooltipLayerElement` lives at the route root with `pickingMode = Ignore`; source elements
attach a lightweight `TooltipAnchorManipulator`.

The manipulator handles:

- `PointerEnterEvent` / `PointerLeaveEvent`
- `FocusInEvent` / `FocusOutEvent`
- pointer press and movement cancellation
- controller Submit/Cancel through the normal UI event system
- touch long press only where a first tap already performs another action
- detach and recycle without leaving stale scheduled callbacks

Timing defaults:

| Input | Open | Close / reshow |
|---|---:|---:|
| Mouse/stylus hover | 280 ms | 100 ms close; 80 ms sibling reshow |
| Keyboard/controller focus | 220 ms | close on focus out |
| Touch long press | 520 ms | close on release/outside tap |

These values live in presentation config, not controls. Every pending show carries a monotonically
increasing request generation so a recycled card or rapid focus move cannot open stale content.

Placement is a pure function of anchor `worldBound`, tooltip measured size, panel bounds, safe
area, and input exclusion zone:

1. try above, below, right, then left;
2. choose the first candidate that fits;
3. otherwise choose the candidate with the greatest visible area;
4. clamp to the safe region with a 12 px margin;
5. keep the pointer/connector aligned to the anchor within its legal range;
6. recompute on `GeometryChangedEvent`, page swap, and resolution/safe-area change.

Tooltip kinds stay bounded:

- **Keyword:** glyph, name, written domain, 1–3 sentence definition, source context.
- **Equipment:** identity/location, four-item fact budget, one mastery/rule line, active status.
- **Fact:** definition, formula or cap only when it materially answers the label.
- **Disabled reason:** exact blocker and the legal recovery action.
- **Receipt:** paid, gained, resulting Sand/location; disappears only after acknowledged.

The default width range is 280–360 px at the 1280 reference resolution. A transient tooltip has no
scrolling, no nested tooltip targets, and no interactive children. If content exceeds its budget,
it ends with `Inspect for full details` and the source's pin action opens the dossier/reference
card. The equipment R6 sample is the maximum normal density, not a target for every hover.

### 5. Workbench composition

The route composes the shared controls without changing their contracts:

- **Market is open by default.** Any live Recruit, Weapon, Trinket, or Inscription offer selects
  into the center full dossier. Recruit shows exact composed hero rules and acquisition result.
- **Armory is a mutually exclusive work mode.** Opening its fixed six-item page collapses Market
  to a summary bar. Closing it returns focus to the handle.
- **The hero rail is permanent.** Weapon and Trinket sockets are inspectable, pin-capable, and
  typed drop/selection targets. Only compatible sockets wake.
- **The center is never a comparison ledger.** Equipment targeting shows the full projected hero;
  changed facts receive quiet blue preview marks and at most one subordinate compact summary.
- **Every collection is paged.** Market is a fixed five; Armory uses a fixed grid and explicit
  page controls; dossier uses named fixed pages only when the combat-critical first page cannot
  legally contain secondary build provenance.

### 6. Feedback, flair, audio, and haptics

Extend the existing `UiPolishSignals` → `UiFeedbackDirector` path. Controls emit semantic cues;
they do not play clips or start arbitrary animations themselves.

Add or distinguish cues only where the response is meaningfully different:

- tooltip reveal/dismiss;
- preview pin/unpin;
- drawer expand/collapse;
- compatible socket wake;
- projected target change;
- existing select, confirm, purchase, equip, route, attention, result, and error cues.

`HubPresentationConfig` remains the tunable recipe source. Recipes describe duration, easing,
offset, scale, opacity, intensity, sound family, haptic family, priority, and cooldown. State is
applied before the recipe begins.

Use three rendering tiers:

1. **USS transitions** for opacity, translate, border/tint, and small scale accents.
2. **Retained Painter2D overlay** for selection traces, transfer arcs, item seating, purchase
   receipts, and rare commitment bursts.
3. **Authored VFX/animation only for route-level moments** where a UI-local effect cannot express
   the fantasy cleanly.

The existing `UiFxLayer` remains the one overlay. Effects are pooled/bounded, ignore picking, and
stop repainting when idle. `DynamicTransform` or `GroupTransform` usage hints apply only to
elements that actually animate and are set before attachment.

Audio rules:

- quiet hover/focus tick with a family-wide cooldown;
- slightly firmer pin/selection cue;
- short stone/metal drawer motion;
- distinct item-seat, purchase, bind, major commit, and error families;
- no hover haptic; haptics begin at selection, valid seat/commit, or error;
- ambience ducking only for important commit/result cues;
- master UI volume, mute, reduced-flash, and reduced-motion operate independently;
- synthesized clips remain a development fallback, not the shipping sound identity.

This lets flair become richer without coupling it to a card class or duplicating one-off
coroutines across screens.

## Suggested source boundaries

Names are provisional; responsibilities are not:

```text
client/Assets/Scripts/Warband/UI/Inspection/
  InspectableModel.cs
  InspectableModelFactory.cs
  InspectionCoordinator.cs
  WarbandDossierElement.cs

client/Assets/Scripts/Warband/UI/Tooltip/
  TooltipContentModel.cs
  TooltipAnchorManipulator.cs
  TooltipPlacement.cs
  RuntimeTooltipLayerElement.cs

client/Assets/Scripts/Warband/UI/Presentation/
  SemanticText.cs
  SemanticTokenCatalog.cs
  PagedRegionElement.cs

client/Assets/Scripts/Warband/UI/Workbench/
  WorkbenchView.cs
  MarketShelfElement.cs
  ArmoryDrawerElement.cs
  WarbandRailElement.cs

client/Assets/Resources/UI/
  Workbench.uxml
  WorkbenchStyles.uss
  RuntimeTooltip.uxml
  RuntimeTooltipStyles.uss
```

The implementation should reuse or migrate logic from `InspectorPanel`, `CardRulesPopover`,
`MusterCard`, `MechanicPresentation`, `UiPresentationSystem`, and `UiFeedbackOutputs`; it should
not strand parallel versions of the same policy. Existing UI continues to compile behind the
current route until the Workbench slice passes its contract.

## Verification contract

### Pure Edit Mode tests

- every inspectable kind produces the expected identity, context, facts, pages, actions, and
  exact disabled reasons;
- Recruit and owned-hero projections use the same class-rule source;
- item projection shows correct location, compatibility, mastery activity, and resulting hero;
- semantic segments retain glyph + label + family/domain;
- every transient tooltip satisfies its fact/line budget;
- placement resolver fits or deterministically clamps for anchors at every panel edge;
- rapid request generation prevents stale tooltip reveal;
- pagination and work-mode transitions preserve pinned object and focus origin.

### UI Toolkit Edit/Play Mode tests

- pointer hover and focus produce equivalent content;
- click/tap/Submit pins the correct dossier/reference;
- Cancel/Back restores the exact invoking focus;
- hidden drawer/pages contain no focusable elements;
- tooltip overlay does not intercept picking;
- compatible sockets are typed and incompatible sockets cannot commit;
- reduced motion reaches the same final state without delayed input;
- audio/haptic spies receive the expected semantic cue exactly once.

### Capture matrix

- 1280×720: minimum authored desktop contract;
- 1920×1080: primary visual target;
- 2560×1440: scale and text raster check;
- 21:9 landscape: composition/safe-area check;
- worst-case real hero/rule text and six-active/two-reserve rail;
- Armory page one and last page;
- Market Recruit, item, and Inscription full dossiers;
- keyword and equipped-item tooltip at all four edges;
- 130% text-expansion fixture;
- reduced-motion and high-contrast/focus-visible fixtures.

Automated route checks should fail if a `ScrollView` appears under the Workbench root, if any
required region clips outside the safe-area root, if rule labels ellipsize, or if a hidden page
remains focusable. Final acceptance still includes an actual 1280×720 Unity capture and keyboard,
controller, pointer, and touch-equivalent interaction pass.

## Delivery sequence after visual approval

1. **Foundation slice:** pure inspection models, semantic spans/tokens, placement resolver,
   runtime tooltip layer, and tests.
2. **Coded Workbench shell:** fixed 1280×720 route, full Recruit/hero dossier, Market shelf,
   permanent rail, and exact focus restoration.
3. **Armory task:** mutual-exclusion state, fixed pagination, item dossier, compatible sockets,
   and full projected-hero flow.
4. **Feedback pass:** route every component through existing polish signals; add configured
   motion, Painter2D flourishes, authored audio hooks, haptics, reduced-motion/flash modes.
5. **Hardening:** worst-case copy/content fixtures, capture matrix, input parity, performance
   profile, and removal of superseded Hall UI only after the new route is proven.

Each slice is independently reviewable and keeps the current route available. The visual sample
defines composition and hierarchy; authoritative projections and tests define truth.

## Unity 6 research basis

- [Custom controls and lifecycle](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-create-custom-controls.html)
  support reusable `VisualElement` subclasses, UXML traits/elements, and attach/detach cleanup.
- [Runtime data binding](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-runtime-binding.html)
  is available, but explicit immutable binding remains the safer fit for this deterministic
  gameplay route.
- [Pointer events](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Pointer-Events.html),
  [focus events](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Focus-Events.html), and
  the [runtime event system](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Event-System.html)
  provide the input parity needed by the shared anchor manipulator.
- [USS transitions](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Transitions.html) and
  transition events handle local state motion without per-control coroutines.
- [Painter2D](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.Painter2D.html)
  and
  [generated visual content](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-generate-2d-visual-content.html)
  are appropriate for pooled vector flourishes in the retained effect layer.
- [Usage hints](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-use-usage-hints-to-reduce-draw-calls-and-geometry-regeneration.html)
  can reduce regeneration cost for known transform-animated layers.
- [Screen safe area](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen-safeArea.html)
  supplies the placement/clamping inset.
- Unity's documented
  [tooltip event](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Tooltip-Events.html) is
  Editor-only, which is why the runtime game UI needs its own overlay/controller.
