# Warband Market UI — Unified Header and Advanced Preview Specification

Status: implementation brief  
Reference commit: `ab6c56f`  
Primary visual target: `warband-market-weapon-preview-inspector.png`

## 1. Outcome

Rebuild the Market screen around two ideas:

1. One compact command ribbon replaces the current stacked run header and station header.
2. The right side becomes a decision-specific preview workspace, not a generic vertically stacked “Selected Card” dossier.

The target experience should let a player answer these questions without scrolling:

- What is this offer?
- What does it cost?
- What does it do?
- Who can use it?
- What exactly changes if I buy and equip it?
- What mechanic do I lose?
- What mechanic do I gain?
- Can I buy it now?

The reference image is a visual direction, not a source of gameplay truth. All mechanics, values, names, eligibility and authored descriptions must continue to come from game data.

---

## 2. Design principles

### 2.1 Decision density over information density

Every visible element should help the player make the current market decision. Remove framing labels and repeated context that do not change the decision.

Examples:

- Remove the `SELECTED CARD` strip. The highlighted stock card and inspector title already communicate selection.
- Do not show balance arithmetic such as `4 - 5 = -1 after`.
- Do show an exact equipment comparison because it changes the buying decision.

### 2.2 One semantic home for every control

- Run progression belongs in the global command ribbon.
- Station navigation belongs in the global command ribbon.
- Market reroll belongs in the stock panel.
- Buying and holding belong in the selected-offer inspector.
- Hero loadout manipulation belongs in the persistent Warband bar or Manage Warband.

### 2.3 Preserve authored asymmetry

The inspector must explain meaningful mechanical differences rather than reducing equipment to a single “better/worse” score.

For example, Officer’s Sabre on Bulwark is not simply an upgrade:

- Faster attacks and higher power are gains.
- Lower mana per hit is a loss.
- `Hold Fast` shield generation is lost.
- `Aftercast Edge` is gained.

The UI should expose those tradeoffs instead of choosing for the player.

### 2.4 Fit common decisions at 1080p

At a 1920×1080 reference resolution:

- Recruit previews should fit without vertical scrolling.
- Weapon and trinket previews should fit without vertical scrolling.
- Scrolling is allowed for unusually long rank-up, inscription or capacity decisions.
- The action dock must never cover the final content row.

---

## 3. Screen composition

### 3.1 Vertical regions

| Region | Target height | Notes |
| --- | ---: | --- |
| Unified command ribbon | 84px | Replaces both existing headers |
| Main workspace | Remaining height | Stock and inspector |
| Compact Warband bar | 96px | Existing persistent roster concept |

Do not render a separate next-wager footer on this screen. Move that action into the unified command ribbon.

### 3.2 Main workspace split

| Pane | Target width |
| --- | ---: |
| Market stock | 42% |
| Selected-offer preview | 58% |

The divider should be visually quiet: a one-pixel cool-gray line or a shallow inset shadow. Avoid heavy independent panel borders that make the screen feel like two unrelated applications.

---

## 4. Unified command ribbon

Replace the current `.management-topbar` / `.hub-run-ribbon` and `.hub-workspace__header` stack with one `hub-command-ribbon`.

### 4.1 Content order

#### Left cluster

- Back button: `TABLE`
- Small context: `WARBAND · ACT 1 / 3`
- Primary page title: `THE MARKET`
- Small beat context: `BEAT 1 / 5`

#### Center cluster

- Route nodes: `1`, `2`, `3`, `4`, `BOSS`
- Current node receives the gold active treatment.
- Completed, available, unavailable and boss states must remain visually distinct.

#### Right cluster

- Market tab — selected
- Warband tab
- Armory tab
- Hourstone tab
- Compact progression action: `NEXT: CHOOSE WAGER`
- Hourstone icon and current balance

### 4.2 Interaction

- The full station tab is clickable, not just its icon.
- Each tab has an icon and visible label.
- Selected state uses a dark blue fill, bright top/side edge and slightly brighter label.
- Hover state should be quieter than selected state.
- `NEXT: CHOOSE WAGER` should use an outlined gold treatment until it becomes the dominant next action.
- Hourstone balance is display-only here; hover may explain the currency.

### 4.3 Responsive behavior

At narrower widths:

1. Reduce spacing between station tabs.
2. Shorten `NEXT: CHOOSE WAGER` to `CHOOSE WAGER`.
3. Collapse station labels only below the width at which the main workspace is already switching layouts.
4. Never create a second header row.

---

## 5. Visual styling system

### 5.1 Surfaces

Use a layered near-black/navy system:

| Token | Suggested value | Purpose |
| --- | --- | --- |
| `--wb-bg-deep` | `#070C12` | Screen background |
| `--wb-surface-0` | `#0B121B` | Main panels |
| `--wb-surface-1` | `#101A26` | Cards and stat cells |
| `--wb-surface-2` | `#162333` | Selected/raised surfaces |
| `--wb-line` | `#33465C` | Normal border |
| `--wb-line-quiet` | `#223043` | Section divider |
| `--wb-text` | `#EEF2F7` | Primary text |
| `--wb-text-muted` | `#99A9BD` | Secondary text |
| `--wb-gold` | `#E8AD32` | Currency, current beat, key selection |

Values may be adjusted to existing project tokens, but maintain the hierarchy.

### 5.2 Semantic mechanic colors

Color is an aid, not the only carrier of meaning. Always pair color with an icon and label.

| Mechanic | Color family | Icon concept |
| --- | --- | --- |
| Health / damage received | Coral red | Heart/shield-heart |
| Damage / power | Orange | Crossed weapons |
| Healing | Green | Plus/spark |
| Reach / area | Blue | Hex/range ring |
| Cadence / haste | Purple | Clock |
| Mana / mana per hit | Cyan | Droplet |
| Crit | Gold | Burst/star |
| Shield / formation | Teal | Shield |

Use these colors consistently in:

- Stat cells
- Rule text keywords
- Before/after comparisons
- Tooltips
- Warband-bar equipment and talent summaries

Do not color entire paragraphs. Color only meaningful keywords, values and icons.

### 5.3 Typography

- Use the existing display serif treatment for major titles and offer names.
- Use the existing readable sans treatment for mechanics, labels and controls.
- Uppercase should be reserved for navigation, compact labels and mechanic headings.
- Body descriptions should use normal title/sentence case.
- Numerical columns should align cleanly; use a tabular number style if available.

### 5.4 Borders and selection

- Normal cards: cool gray border.
- Hover/focus: brighter blue-gray border.
- Selected stock card: gold border plus a restrained outer glow.
- Valid recipient preview: blue-gray.
- Selected recipient: gold border.
- Invalid recipient: reduced opacity with a reason tooltip.
- Avoid putting gold borders around every actionable object; gold should retain decision significance.

---

## 6. Market stock pane

### 6.1 Local toolbar

Render one compact row:

- Left: `LIVE STOCK`
- Beside it: `5 OFFERS`
- Right: `REROLL` + Hourstone icon + `1`

Remove the permanent instructional sentence:

> Tap or focus an offer to compare it. Buying is always a separate action.

That copy may appear in onboarding once or in a help tooltip.

Move the stock-refresh explanation to a quiet footer inside this pane:

> Stock refreshes after a resolved beat. Held stock survives refresh.

### 6.2 Offer grid

At the reference resolution, use a three-column, two-row grid.

Recommended behavior:

- Three columns above approximately 1500px.
- Two columns for medium layouts.
- One column only for genuinely narrow layouts.
- Cards stretch within a bounded minimum/maximum width.

Each offer card must show:

- Offer type and subtype
- Offer name
- Portrait or item/rune artwork
- Hourstone icon and cost
- Selected state when focused

Do not show `SHORT 1` or equivalent insufficiency text on the card. The disabled Buy action should explain affordability after the player selects the offer.

### 6.3 Card cost treatment

Use:

`[Hourstone icon] 5`

Do not use:

- `5 SAND`
- `SHORT 1`
- A repeated balance equation

The Hourstone icon should be identical everywhere currency appears.

---

## 7. Selected-offer preview architecture

The preview is driven by `DecisionDetailKind`. It should not be one UXML stack with every possible section appended vertically.

Recommended variant classes:

```text
wb-inspector
wb-inspector--recruit
wb-inspector--rank-up
wb-inspector--weapon
wb-inspector--trinket
wb-inspector--inscription
wb-inspector--capacity
```

The root enables exactly one detail variant at a time.

### 7.1 Shared shell

Every variant shares:

1. Compact overview
2. Variant-specific decision body
3. Pinned action dock

Remove the standalone `SELECTED CARD` strip.

The action dock is pinned outside the scrolling content. The scrolling content must receive bottom padding equal to the dock height plus normal spacing.

### 7.2 Compact overview

Target height: approximately 190–215px.

#### Artwork region

- Champion portrait, weapon art, trinket art or inscription sigil
- Approximately one-third of inspector width
- Crop with intent; never stretch

#### Identity region

- Name
- Offer type/subtype tags
- One-line role or mechanic description
- Cost in the upper-right
- Semantic stat strip where appropriate

Do not show the balance comparison widget in the overview.

### 7.3 Expand behavior

Do not render a full-width `EXPAND` control.

If a detailed dossier exists:

- Use a small icon button beside the title.
- Tooltip: `Open full dossier`.
- Show it only for variants where additional information exists.

Recruit and ordinary equipment previews should not require expansion to make the purchase decision.

---

## 8. Advanced weapon preview

This is the primary target represented by the second reference image.

### 8.1 Overview content

For Officer’s Sabre:

- Name: `OFFICER’S SABRE`
- Tags: `WEAPON`, `SABRE`
- Description: authored one-line weapon identity
- Cost: Hourstone icon + `4`
- Stat cells:
  - Power
  - Reach
  - Cadence
  - Mana / Hit
  - Crit

Stat values must come from simulation/content data.

### 8.2 Decision body layout

Use two columns.

#### Left: weapon identity

Section: `WEAPON PROFILE`

- Basic attack name
- Exact attack sentence
- Attack icon

Section: `MASTERY`

- Mastery name
- Exact authored description
- Mastery icon

#### Right: preview on Warband

Section: `PREVIEW ON WARBAND`

1. Recipient selector
2. Current weapon → offered weapon heading
3. Exact stat comparison
4. Lost/gained mechanic callout

### 8.3 Recipient selector

Show every currently fielded hero as a compact selectable chip:

- Portrait
- Name
- Rank badge
- Eligibility state

Optional: include reserve heroes after fielded heroes when reserve use is relevant.

Selection rules:

- Default to the currently selected Warband hero if eligible.
- Otherwise choose the first eligible fielded hero.
- Do not silently choose an ineligible recipient.
- Keep the player’s selected recipient while inspecting compatible equipment.
- Highlight the same hero in the persistent Warband bar.

Hover tooltip should include:

- Hero name and rank
- Currently equipped weapon
- Why the offered item is valid or invalid

### 8.4 Exact comparison model

For each comparable stat, provide:

```csharp
public sealed record StatDeltaModel(
    string StatKey,
    string Label,
    string IconKey,
    string BeforeText,
    string AfterText,
    DeltaDirection Direction,
    string? Explanation);
```

`DeltaDirection` should not be inferred solely from numeric sign. Some lower values are better, and some effects are contextual.

```csharp
public enum DeltaDirection
{
    Positive,
    Negative,
    Neutral,
    Contextual
}
```

Examples:

- Power `5 → 7`: positive
- Cadence `1.4s → 0.7s`: positive because lower interval attacks faster
- Reach `1 → 1`: neutral
- Mana per hit `16 → 7`: negative
- Crit `0% → 5%`: positive

The simulation or content layer should supply semantic direction when necessary. The view should only render it.

### 8.5 Lost and gained mechanics

Below the numeric comparison, show two explicit callouts:

- `LOSE: Hold Fast · shield generation`
- `GAIN: Aftercast Edge · guaranteed crit after casting`

Requirements:

- Use authored mastery names.
- Summaries should be short, but the full authored rule appears on hover.
- Do not imply the gained mechanic is universally better.
- Contextual tradeoffs may use gold/blue rather than green/red.

### 8.6 Buy behavior

Recommended first implementation:

- Buying the weapon places it in the Armory.
- The selected recipient is a preview target only.
- Button tooltip clarifies: `Buy to Armory. Previewing on Bulwark.`

Optional later implementation:

- Provide `BUY` and `BUY & EQUIP`.
- Only add this after confirming that immediate equipping does not bypass intended Armory or capacity rules.

Do not make the preview recipient selection itself mutate the loadout.

---

## 9. Other preview variants

### 9.1 Recruit

Use:

- Compact portrait/identity/stats overview
- Two-column body
  - Left: basic attack and signature
  - Right: passive and build signals
- Party compatibility signals derived from actual keywords/tags

All three core rules should be visible without scrolling.

Avoid unsupported recommendations such as `BEST WITH X` unless the relationship is derived from explicit mechanics. Prefer factual signals:

- `FORMATION`
- `MANA`
- `AREA`
- `LOW-HP`
- `ON-CAST`

### 9.2 Trinket

Use the same recipient preview framework as weapons.

Compare:

- Slot occupancy
- Replaced trinket
- Trigger and effect
- Numeric changes where exact comparison exists
- Lost/gained rule hooks

### 9.3 Rank-up

Use:

- Guaranteed rank gains at top
- Talent choices in two or three equal columns
- Exact changed rule text
- Current choice highlighted if revisiting

Rank-up is a legitimate case for scrolling or a dossier mode.

### 9.4 Inscription

Use:

- Large law/sigil identity
- Exact run-law text
- Affected mechanic tags
- Current Warband interactions derived from tags or deterministic analysis

Do not pretend to calculate value when the inscription creates combinatorial effects. Explain affected systems clearly.

### 9.5 Capacity

Use a slot-oriented visualization:

- Current field and reserve capacity
- Locked/unlocked slots
- What the purchase unlocks
- Current heroes/items affected

---

## 10. Persistent Warband bar

Retain the compact 96px roster bar.

Each fielded hero card should expose:

- Portrait and name
- Current rank/tier
- Equipped weapon
- Equipped trinket slots
- Selected talent icons

Every equipment and talent icon is hoverable.

Drag behavior:

- Weapon and trinket icons may be dragged between eligible heroes.
- Valid targets highlight before drop.
- Invalid targets remain visible but show the rejection reason.
- Dragging must not conflict with clicking a hero to select them as the comparison recipient.

When previewing an equipment offer:

- Highlight the preview recipient in both the inspector and Warband bar.
- Do not visually replace the equipped icon until a real equip action occurs.
- A temporary ghost/outline may show the prospective item.

---

## 11. Model and state changes

The view should receive complete presentation models. Avoid reconstructing gameplay semantics in USS/UXML binding code.

Suggested additions:

```csharp
public sealed record OfferPreviewModel(
    DecisionDetailKind Kind,
    string OfferKey,
    string Title,
    string Description,
    HourstoneAmount Cost,
    IReadOnlyList<SemanticStatModel> Stats,
    IReadOnlyList<RuleSectionModel> Sections,
    EquipmentPreviewModel? EquipmentPreview,
    bool CanPurchase,
    string? DisabledPurchaseReason);

public sealed record EquipmentPreviewModel(
    string OfferedItemKey,
    string? SelectedRecipientHeroKey,
    IReadOnlyList<RecipientPreviewModel> Recipients,
    string? CurrentItemName,
    string OfferedItemName,
    IReadOnlyList<StatDeltaModel> StatDeltas,
    RuleDeltaModel? LostRule,
    RuleDeltaModel? GainedRule);

public sealed record RecipientPreviewModel(
    string HeroKey,
    string DisplayName,
    string PortraitKey,
    string RankText,
    bool IsEligible,
    string? IneligibleReason,
    bool IsSelected);

public sealed record RuleDeltaModel(
    string RuleName,
    string ShortSummary,
    string FullDescription,
    string IconKey);
```

Planning/view state should retain:

```csharp
string? SelectedOfferKey;
string? ComparisonTargetHeroKey;
```

Selecting a recipient updates preview state only. Purchasing follows existing authoritative command paths.

---

## 12. Reuse existing comparison logic

`RunShell.cs` already contains `BuildEquipmentComparison`.

Refactor toward one shared comparison builder that can be called from:

- Owned-equipment management
- Market weapon preview
- Market trinket preview
- Optional Armory hover preview

The shared builder should accept:

```text
recipient hero
currently equipped item
candidate item
current planning state
```

It should return presentation-ready semantic deltas and rule changes.

Do not duplicate weapon math inside `ManagementView` or `InspectorPanel`.

---

## 13. File-by-file implementation plan

### `client/Assets/Resources/UI/ManagementHall.uxml`

- Replace the two header siblings with one `.hub-command-ribbon`.
- Move `overview-back`, page identity, progress track, anchor rail, next-wager action and Hourstone balance into it.
- Remove `.hub-inspector-pane__top`.
- Remove the full-width `SELECTED CARD` label.
- Remove or relocate the workspace footer.
- Keep the inspector action dock outside its scrollable region.

### `client/Assets/Resources/UI/HallPhysicalStyles.uss`

- Add unified command ribbon geometry.
- Remove obsolete 64px + 72px header assumptions.
- Set market collection to approximately 42%.
- Set inspector to approximately 58%.
- Define a three-column stock grid at the reference resolution.
- Reduce inspector overview height from the current approximately 236px treatment to approximately 205px.
- Add two-column decision-body styles.
- Guarantee action-dock clearance.
- Add responsive breakpoints without creating a second header row.

### `client/Assets/Resources/UI/InspectorPanel.uxml`

- Keep a shared overview and action dock.
- Add named containers for:
  - Rule/profile column
  - Recipient selector
  - Comparison table
  - Rule delta callout
  - Rank-up choices
  - Inscription impact
- Keep unused variant containers hidden.

### `client/Assets/Scripts/Warband/InspectorPanel.cs`

- Enable exactly one inspector variant class.
- Bind semantic stats from models.
- Render recipient selection without mutating equipment.
- Render comparison direction from the model rather than guessing from strings.
- Hide dossier/expand when unnecessary.
- Remove default balance arithmetic from ordinary previews.

### `client/Assets/Scripts/Warband/ManagementView.cs`

- Bind the unified command ribbon.
- Bind market-local offer count and reroll.
- Retain selected offer and selected comparison recipient.
- Forward recipient selection to planning state.
- Remove permanent tutorial copy from normal presentation.
- Ensure Buy and Hold remain separate explicit actions.

### `client/Assets/Scripts/Warband/RunShell.cs`

- Extract/reuse `BuildEquipmentComparison`.
- Build pre-purchase comparison data for weapon and trinket offers.
- Resolve eligible recipient models.
- Preserve authoritative purchase/equip behavior.

### `client/Assets/Resources/UI/WarbandBarStyles.uss`

- Preserve the 96px compact height.
- Add preview-recipient highlight.
- Add hover and drag states for equipment/talent icons.
- Ensure hero cards remain readable with rank, weapon, trinkets and talent indicators.

---

## 14. Interaction states

### Nothing selected

- Inspector shows a quiet prompt and a short explanation of offer types.
- Do not leave a blank framed dossier.

### Affordable offer selected

- Buy action enabled.
- Cost visible in overview and action.
- Recipient comparison shown when relevant.

### Unaffordable offer selected

- Buy action disabled.
- Button text: `NEED [Hourstone icon] 1 MORE`.
- Full cost remains visible in overview.
- Do not show `SHORT 1` on the stock card.

### Held offer

- Card receives a clear held marker.
- Secondary action becomes `RELEASE HOLD`.
- Held state survives refresh according to existing rules.

### Invalid equipment recipient

- Recipient is visibly disabled.
- Hover explains the exact reason.
- The comparison area does not fabricate a result.

### Keyboard/controller focus

- Focus and selection must be visually distinct.
- Focusing an offer may preview it.
- Buying remains a separate confirmation action.
- Recipient chips must be reachable after the selected offer and before action buttons.

---

## 15. Tooltips

Tooltips should be available for:

- Every semantic stat
- Hourstone currency
- Equipment mastery
- Lost/gained rules
- Recipient eligibility
- Warband-bar weapon, trinket and talent icons
- Rank/tier badge

Tooltip content should use authored or model-provided descriptions. Avoid maintaining a second set of mechanic explanations in UXML.

---

## 16. Animation and feedback

Keep motion restrained:

- Selected card: 100–150ms border/glow transition
- Inspector content swap: short opacity/translate transition
- Recipient comparison changes: values cross-fade rather than rebuilding the entire panel visibly
- Buy success: currency count ticks down, offer leaves stock, Armory count updates
- Invalid action: small button shake or red edge pulse; no full-screen interruption

Do not animate the persistent header or resize the entire layout when switching offers.

---

## 17. Acceptance criteria

### Layout

- Only one top command ribbon is visible.
- No separate next-wager footer appears.
- The Warband bar remains 96px tall.
- Five stock offers render as a readable 3+2 grid at 1920×1080.
- Recruit and weapon previews do not require scrolling at 1920×1080.
- Inspector content is never hidden behind its action dock.

### Weapon preview

- Selecting a weapon shows all eligible fielded recipients.
- Selecting a recipient does not equip or purchase anything.
- Before/after values match simulation data.
- Cadence direction is semantically correct.
- Mana-per-hit direction is semantically correct.
- Lost and gained masteries are both visible.
- Buying uses the existing authoritative purchase path.

### Currency

- Every Hourstone price uses the same icon.
- The word `SAND` is absent from price labels.
- `SHORT X` is absent.
- The disabled Buy action states the missing amount.
- Balance arithmetic is absent from the ordinary inspector.

### Accessibility and input

- Mechanic meaning does not depend on color alone.
- Focus is distinct from selection.
- All recipient choices and actions are keyboard/controller reachable.
- All compact loadout icons have tooltips.

### Regression safety

- Market reroll behavior is unchanged.
- Hold-stock behavior is unchanged.
- Purchases remain deterministic.
- Equipment eligibility still comes from authoritative state.
- Station navigation and run progression remain functional.

---

## 18. Recommended implementation order

### Phase 1 — Recover space

1. Merge headers.
2. Move next-wager into the command ribbon.
3. Remove the selected-card framing strip.
4. Remove the global market footer.
5. Convert stock to a three-column grid.

### Phase 2 — Variant inspector

1. Add `DecisionDetailKind` variant classes.
2. Implement recruit and weapon two-column layouts.
3. Remove default balance arithmetic.
4. Make ordinary recruit/weapon previews fit without scrolling.

### Phase 3 — Advanced comparison

1. Extract shared equipment-comparison builder.
2. Add comparison recipient state.
3. Render exact stat deltas.
4. Render lost/gained masteries.
5. Synchronize recipient highlighting with the Warband bar.

### Phase 4 — Complete decision variants

1. Trinket preview
2. Rank-up choices
3. Inscription impact view
4. Capacity view
5. Drag/drop and richer Warband-bar tooltips

---

## 19. Prompt for an implementation agent

```text
Implement the Warband Market UI redesign described in
warband-market-advanced-preview-spec.md.

Work in phases and preserve deterministic game behavior. Begin with Phase 1 and
Phase 2, then implement the weapon recipient preview from Phase 3.

Important constraints:
- Unity 6.3 UI Toolkit.
- Treat simulation/content data as authoritative.
- Reuse the existing BuildEquipmentComparison logic rather than duplicating
  equipment math in the view.
- Recipient selection is preview-only and must not equip or purchase.
- Keep Buy and Hold as explicit separate actions.
- Maintain keyboard/controller focus behavior.
- The ordinary recruit and weapon inspector must fit at 1920x1080 without
  scrolling or content being covered by the action dock.
- Preserve unrelated local changes.

Before editing, inspect:
- client/Assets/Resources/UI/ManagementHall.uxml
- client/Assets/Resources/UI/HallPhysicalStyles.uss
- client/Assets/Resources/UI/InspectorPanel.uxml
- client/Assets/Resources/UI/WarbandBarStyles.uss
- client/Assets/Scripts/Warband/ManagementView.cs
- client/Assets/Scripts/Warband/InspectorPanel.cs
- client/Assets/Scripts/Warband/RunShell.cs

Validate each phase using existing UI contract/model tests and add tests for:
- weapon preview recipient selection
- semantic comparison direction
- lost/gained mastery presentation
- no preview-side loadout mutation
- affordability text
```

