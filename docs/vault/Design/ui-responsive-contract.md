# Responsive UI — the model, the contract, the SOP

Written 2026-07-27. Covers Unity 6.3 UI Toolkit (the only UI system warband uses). Two halves:
**how the machinery actually works**, then **what warband does today and what to change**.

**Implementation status (2026-07-27):** the foundation in Part 3 is now the shipping contract.
Part 2 remains the measured pre-migration baseline so future regressions have context.

---

## Part 1 — How responsive UI works in UI Toolkit

### The three-layer model

Responsiveness in UI Toolkit is not one feature. It is three independent layers, and confusing
them is the root of most bugs:

| Layer | Owner | Responds to | Unit |
|---|---|---|---|
| **1. Panel scaling** | `PanelSettings` | screen resolution / DPI | multiplies everything |
| **2. Flex layout** | Yoga (USS + UXML) | *container* size | px = reference px, % = of parent |
| **3. Breakpoints** | your C# | screen size *class* | toggles USS classes |

Layer 1 makes the UI *bigger or smaller*. Layer 2 makes it *fit*. Layer 3 makes it a
*different design*. A UI that only has layers 1+2 will be legible but wrong-shaped on a phone;
one that only has 3 will be right-shaped but unreadably small on a 4K monitor.

### Layer 1 — PanelSettings scale modes

There are exactly three, and the choice is per-panel, not per-element
([docs](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Panel-Settings.html)):

- **Constant Pixel Size** — 1 USS px = 1 screen px, always. UI shrinks visually as resolution
  climbs. Correct for *developer* surfaces that want maximum information density (debug cockpits,
  inspectors) and for anything that must line up with raw `Screen.width`/mouse coordinates.
- **Constant Physical Size** — 1 USS px = a fixed physical size, derived from `referenceDpi` /
  `fallbackDpi`. Correct for cross-device shipping UI where a button must be thumb-sized on both a
  phone and a tablet.
- **Scale With Screen Size** — the workhorse. You author at a `referenceResolution` (1920×1080 is
  the sane default) and the panel multiplies by a scale factor derived from the real resolution.

**The scale factor math, because it decides your font floor.** With
`screenMatchMode = MatchWidthOrHeight`, the factor is a *log-weighted* blend, not a linear one:

```
scale = 2 ^ lerp( log2(W / refW), log2(H / refH), match )
```

`match = 0` follows width only, `1` follows height only, `0.5` splits. With ref 1920×1080,
match 0.5:

| Viewport | Scale | A 7px font renders at | A 14px font renders at |
|---|---|---|---|
| 1280×720 | 0.667 | **4.7 px** | 9.3 px |
| 1920×1080 | 1.000 | 7 px | 14 px |
| 2558×1313 | 1.273 | 8.9 px | 17.8 px |
| 2560×1440 | 1.333 | 9.3 px | 18.7 px |
| 2560×1080 (ultrawide) | 1.155 | 8.1 px | 16.2 px |

The other two match modes are `Shrink` (use the *smaller* ratio — nothing ever overflows, but you
get letterboxed empty space) and `Expand` (use the *larger* — fills the screen, guarantees the
short axis overflows). `MatchWidthOrHeight` at 0.5 is the right default for a game whose important
content is centred; **use `match = 1` (height-locked) when the design is a horizontal band that
must never grow taller than the screen**, which is what an autobattler board UI usually is.

**Key consequence:** under Scale With Screen Size, USS `px` are *reference pixels*, not device
pixels. So "px is not responsive, use %" is wrong advice in UI Toolkit — px already scales.
What px does *not* do is **re-proportion**. That's layer 2's job.

### Layer 2 — the flex layout engine (Yoga)

UI Toolkit runs a modified [Yoga](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-LayoutEngine.html),
a Flexbox subset. The differences from web CSS that actually bite:

- **`flex-direction` defaults to `column`**, not `row`. Every container is a vertical stack until
  told otherwise.
- **`flex-grow` defaults to 1 in Unity 6** (0 on the web). Elements eat available space by
  default, which is usually what you want and occasionally a mystery.
- **`flex-basis: auto`** means "size from content." `flex-grow: 1; flex-basis: 0;` is the idiom
  for "divide the space evenly, ignore content size."
- **Percentages resolve against the parent's *resolved* size.** A `height: 100%` child inside a
  parent whose height is `auto` resolves to nothing. Percentage heights need a definite-height
  ancestor chain.
- **`min-width` / `min-height` win over `flex-shrink`.** A row of children that each have an
  intrinsic min size will overflow their container rather than compress — Unity resolves the
  conflict in favour of the child. This is the single most common source of "it looked fine at
  1920 and blew out at 1280."
  **The guard is `min-width: 0` on flex children that contain text**, so the text is allowed to
  wrap/ellipsize instead of forcing the row wider.
- **`position: absolute` removes the element from layout entirely.** Fine for overlays, popovers,
  decorations, badges. Not fine for structure — an absolute element with px offsets is a hardcoded
  design that no breakpoint can rescue.
- **Sizing order**: width/height → available parent space → distribute by grow → compress by
  shrink → clamp by min/max → final. Min/max is applied *last*, which is why it overrules shrink.

**Overflow and text.** `text-overflow: ellipsis` does nothing unless the element also has
`overflow: hidden` and `white-space: nowrap`. Multi-line text with a fixed `height` and
`overflow: hidden` silently truncates — it is invisible in a screenshot at your authoring
resolution and catastrophic at another, or with longer content.

### Layer 3 — breakpoints (there are no media queries)

USS **does not support `@media`**
([confirmed](https://discussions.unity.com/t/does-ui-toolkit-support-css-media-queries/254298)).
The established pattern is:

1. One place in C# reads the *panel's own* `resolvedStyle` size (not `Screen.width` — the panel
   may be scaled or render to a texture).
2. It classifies into named states and calls `EnableInClassList` on the panel root.
3. USS expresses every adaptation as a descendant selector off those classes.

```css
.card__summary          { font-size: 14px; }
.layout--compact .card__summary { font-size: 12px; }
.layout--phone   .card  { width: 100%; }
```

Rules that make this work rather than rot:
- **One classifier for the whole app.** Two classifiers drift; three are undebuggable.
- **Classify on the panel's resolved size, in reference px** — so the thresholds are in the same
  unit as the USS you author.
- **Capability ≠ modality.** `Touchscreen.current != null` means the device *has* a touchscreen
  (every modern Windows laptop does). It does not mean the player is using it. Drive a `--touch`
  class off *the last input actually received*, or off device type, not off capability.
- **Register the classifier on `GeometryChangedEvent` on the panel root.** Never inside per-element
  geometry callbacks that themselves change layout — that's a relayout loop.

### Layer 0 — the things underneath all three

- **Safe area.** `Screen.safeArea` is bottom-left origin; UI Toolkit is top-left. You must flip Y.
  It's defined relative to the *player window*, not the physical screen. One `SafeAreaFrame`
  element directly under the panel root, padding-driven, re-applied on geometry change — never
  per-screen copies.
- **Aspect ratio.** USS supports the `aspect-ratio` property (float, e.g. `1.777778`, or `auto`).
  Use it for portraits/art so images stay proportioned without fixed px.
- **Design tokens.** USS supports custom properties (`--name: value`) and `var(--name)`. This is
  the only mechanism for a shared visual vocabulary; without it, a palette change is a
  find-and-replace across every stylesheet.
- **Cascade.** Stylesheets added to the same root all share one flat namespace, resolved in
  **add order**. Two files defining the same class is a load-order dependency, not an override.

---

## Part 2 — Evaluation of warband's UI

### What's already right (do not regress these)

- **`UiLayoutContract.cs` is genuinely good foundation work.** Asserting on *resolved geometry*
  — escapes, overlap, wrapped-text clipping, minimum font, minimum hit height — rather than on
  screenshots is exactly the correct automation boundary. Most Unity projects have nothing here.
- **`WarbandUiQa.cs` runs a real viewport matrix** (`WarbandUiQa.cs:137-180`): 1280×720,
  1920×1080, 2558×1313, 2560×1440, 2560×1080, crossed with fixtures and an expanded-text variant.
  Testing long-text × small-viewport is the right stress axis.
- **A breakpoint layer exists and is used**: `layout--compact` (96 rules), `layout--short` (45),
  `layout--phone` (218), `input--touch` (7), `motion--reduced` (22).
- **`min-width: 0` appears 54 times** — someone already knows the shrink trap.
- **Panel scaling is correct for the shell**: `ScaleWithScreenSize`, 1920×1080,
  `MatchWidthOrHeight`, `match 0.5` (`RunShell.cs:1402-1408`), matched by `SkirmishController.cs:111-118`.
- **Touch targets are mostly sized**: 242 rules at `min-height >= 44px` vs 68 below.
- **`ConstantPixelSize` on `Tooltip` and `DebugMenu` is deliberate and documented** — dev surfaces
  that want density and 1:1 mouse coordinates. Correct call.

### Findings, ranked

**1 — Body text is authored below the legibility floor.** `.wb-card__ability-summary`
(`PlanningWorkspaceStyles.uss:714`) is `font-size: 7px` with `max-height: 28px; overflow: hidden`.
`.wb-card__subtitle` (:546) and `.planning-track-node__label` (:90) are 8px. These are *base*
selectors, not compact-breakpoint overrides. At the 1280×720 viewport the QA matrix already tests,
7px renders at **4.7 device pixels**. There are **158 declarations at 7–10px** across 10
stylesheets, on a 33-value font scale running 7px→54px.

The project already knows the right number: `RuntimeTooltipSystem.cs:426` enforces a **14px**
floor on tooltip body. That standard is not applied to the cards the player actually reads.
The `max-height: 28px; overflow: hidden` combination is worse than small — it *silently truncates*
ability text, and `RequireWrappedTextFits` is not asserted on that class.

**2 — The breakpoint classifier is forked, and the forks have already drifted.**
`RunShell.ApplyShellLayoutClasses` (`RunShell.cs:1791-1815`) and
`ManagementView.ApplyResponsiveLayout` (`ManagementView.cs:1388-1428`) are near-identical copies
with the same magic numbers (1500 / 820 / 760 / 8-inch diagonal) — but they disagree:

| | RunShell | ManagementView |
|---|---|---|
| `ultrawide` folded into compact | yes (>2.05) | **no** |
| `motion--reduced` | yes | no |
| `layout--tablet` | emitted | emitted |
| safe area | **no** | yes |

So the same viewport produces different classes depending on which view is up. Separately,
**`layout--tablet` is emitted by both and styled by zero USS rules** — dead output.
And folding `ultrawide` into `layout--compact` conflates "not much room" with "too much width,"
which want opposite treatments (compact = shrink; ultrawide = clamp `max-width` and centre).

**3 — `Input.touchSupported` is wrong twice over.** `RunShell.cs:1799` and
`ManagementView.cs:1394` both read it. First, `ProjectSettings.asset:931` sets
`activeInputHandler: 1` — **Input System package only** — under which reading `UnityEngine.Input`
raises `InvalidOperationException`
([issue tracker](https://issuetracker.unity3d.com/issues/error-invalidoperationexception-you-are-trying-to-read-input-using-the-unityengine-dot-input-class-but-you-have-switched-active-input-handling-to-input-system-package-in-player-settings-dot-is-present-when-using-ui-toolkit-and-new-input-system)).
*(Not verified at runtime for `touchSupported` specifically — the throw is documented for the
`Input` class broadly. Worth a Play Mode check.)* The rest of `RunShell.cs` already uses
`UnityEngine.InputSystem`.

Second, and certainly: it answers the wrong question. A touch-capable Windows laptop reports
`true` and gets phone-sized hit targets on a mouse-driven desktop. The intent is "is the player
touching," which is `Touchscreen.current` plus last-input tracking, not device capability.

**4 — Safe area is duplicated and shell-incomplete.** `ResultGateView.cs:302-314` and
`ManagementView.cs:1419-1425` are byte-similar implementations of the same padding math. Every
*other* screen (`MenuView`, `RecruitView`, `WorkbenchView`, `WagerView`, `DeployView`,
`RunOverView`, the fight overlay) has none. On a notched phone — a stated target, given 218
`layout--phone` rules and a `Mobile_Renderer` — those screens run under the notch.

**5 — The design-token layer is ~4% adopted.** 25 custom properties declared against **1541
literal `rgba()` values**; `var(--)` is used 72 times, and 55 of those are inside
`LastHourTokens.uss` itself. The token file's own values are duplicated in `RunShellStyles.uss:951-955`
(`--fact-health` and `--decision-health` are the same colour declared twice). There are no
spacing, radius, or type-scale tokens at all — which is why the type scale has 33 steps.

**6 — Stylesheet namespace collisions.** `RunShell.cs:1414-1424` loads 11 stylesheets into one
root. `.hub-workspace`, `.hub-station`, `.hub-table` and ~15 other `.hub-*` classes are each
defined in **three** files (`HubStyles.uss`, `LastHourTokens.uss`, `HallPhysicalStyles.uss`).
Which wins is the array's order. Reordering that array silently restyles the Hall.

**7 — Fixed heights outnumber min-heights on text containers.** 313 `height: Npx` vs 305
`min-height: Npx`, and 108 `overflow`/`white-space` declarations exist largely to contain the
consequences. Fixed height + `overflow: hidden` is invisible truncation: it passes at the
authoring resolution and fails with longer content, a larger font, or a different language.

**8 — The QA matrix has no vertical-extreme or phone viewport,** and only covers the Workbench
fixture set. All five viewports are landscape ≥1280 wide. Nothing exercises `layout--phone`,
which is the most-styled breakpoint in the codebase (218 rules) and therefore the least verified.

---

## Part 3 — Shipping contract and SOP

### Foundation

**F1. `UiPanelProfile` owns shipping scale.** RunShell and the board use the same
`1600×900`, `ScaleWithScreenSize`, height-locked (`match = 1`) profile. A horizontal decision band
never becomes taller than its viewport. Debug documents may keep constant-pixel profiles.

**F2. `UiEnvironment` is the sole root classifier.** It reads the panel's resolved size and emits
independent axes:

- `layout--narrow`, `layout--short`, and `layout--ultrawide` describe available geometry.
- `layout--phone` / `layout--tablet` describe form factor.
- `input--pointer`, `input--touch`, and `input--navigation` describe the last input received.
- `interaction--coarse` describes target-size capability.
- `motion--reduced` describes presentation preference.

Capability, modality, and size must never be folded into one "mobile" boolean. The temporary
`layout--compact` compatibility alias may only disappear as screens migrate to the independent
axes; no view may create a second classifier.

**F3. One shell-owned safe frame wraps all player-facing content.** It converts
`Screen.safeArea` from screen pixels/bottom-left origin into panel units/top-left origin once.
Screens, fight overlay, and the permanent rail live inside it. Portrait handheld is unsupported
for the first playable and receives a blocking rotate-device guard rather than a crushed layout.

**F4. `UiFoundationTokens.uss` loads first.** The normative semantic scale is display 34, title
26, heading 20, body 16, metadata 13. Spacing, radius, hit-target, and rail-reserve values are
tokens as well. Migration is incremental: an active selector is not considered migrated until its
final cascade rule consumes the semantic token.

**F5. transient presentation state is scoped.** `UiNoticeStore` owns Menu, Muster, Hall, and
Deployment notices. Route exit clears the owning scope, so a combat result or placement error
cannot leak over the next Market decision. Notices are non-picking presentation layers.

### Standing rules

1. **Body text is at least 16 logical px; metadata is at least 13.** Important body copy must also
   pass a rendered-device floor at the 720p target.
2. **No runtime page scroller.** Use hierarchy first, a details/traits disclosure page second,
   bounded pagination for collections third, and ellipsis only for genuinely secondary one-line
   metadata. The permanent rail is a fixed-address row, not a horizontal scroller.
3. **`height` is for boxes; `min-height` is for text.** Any deliberately bounded text must be
   checked with `RequireWrappedTextFits`.
4. **Absolute positioning is reserved for overlays, popovers, badges, and decoration.** Primary
   structure stays in flex layout.
5. **Every flex child containing text gets `min-width: 0`.**
6. **Primary actions and notices never occlude each other.** Feedback uses
   `PickingMode.Ignore`; permanent-rail clearance is asserted.
7. **A stylesheet owns its class prefix.** New collisions across loaded sheets are not accepted.

### Verification gate

`WarbandUiQa` is a deterministic **Play Mode** fixture runner. It does not simulate player
behaviour; it binds presentation-only models to the real retained views, measures resolved
geometry, and captures pixels without focusing the Windows Game View.

The full matrix covers:

- 1024×768, 1280×720, the 1600×900 authoring viewport, and 3440×1440 ultrawide;
- Workbench recruit/rank-up/equipment/tooltip/rail states;
- Wager, Deployment, and Result with the permanent rail;
- expanded-copy stress;
- forced-phone landscape and the 390×844 portrait rotation guard.

`UiLayoutContract` is the automated gate: resolved bounds, no overlap, no `ScrollView`, wrapped
text fit, logical and rendered type floors, minimum hit size, and non-blocking notices. Pixel
captures remain human evidence, not a substitute for those assertions.

**Implementation verification (2026-07-27):** all 57 full-matrix cases pass in Play Mode; the
reviewable report and exact-target captures are under `client/TempCaptures/ui-qa/20260727-160020/`.
