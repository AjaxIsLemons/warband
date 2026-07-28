# UI review: out-of-combat-zero-base-v1

Status: IMPLEMENTED_UNITY_VERIFIED  
Created: 2026-07-27

## Brief

- Screen or flow: Complete out-of-combat run flow, redesigned from the authoritative actions with
  no structural or visual reference to the current Hall UI or previous concepts.
- Primary player decision: Build and understand the current warband, then make one explicit
  irreversible choice or spatial commitment at a time.
- Required information and actions: Result/replay; all field and reserve heroes; complete owned
  equipment inventory including equipped locations; equip, transfer, unequip, reforge, and sell;
  Market inspect/Buy/Hold/Reroll; capacity; all owned Inscriptions; specialization and reward
  choices; Wager; encounter disclosure; formation; Begin Fight.
- Required states: rest, focus, selected, new/recommended, equipped/location, affordable,
  unaffordable with exact deficit, disabled with reason, empty/full capacity, held offer,
  unresolved blocking choice, positive/negative delta, and committed receipt.
- Target viewport/aspect ratio: 1920×1080 concept target; fixed 1280×720 desktop contract; later
  landscape touch composition without scaling desktop down.
- Must preserve: three-act/five-beat run cadence; Sand economy; three Recruit plus two Workshop
  offers; field three→six and bench two; one Weapon plus one Trinket per hero; all owned
  Inscriptions always active; selection never commits; exact rules; staged Wager → disclosure →
  Deployment; deterministic run commands; no scrolling.
- May change: every current screen boundary, station metaphor, navigation model, information
  hierarchy, layout, component system, visual treatment, and whether named systems are
  destinations at all.
- Zero-base hypothesis: the smallest coherent system is Result + one object-centric Workbench +
  one reusable Choice Gate + Deployment. Market, Owned Gear, Warband, and Laws are sources or
  regions inside the Workbench, not separate screens.

## Inputs

| Source | Role |
|---|---|
| `docs/vault/Design/pitch.md` | Product fantasy and run purpose |
| `docs/vault/Design/heroes.md` | Owned roster, rank/spec, loadout, and commitment laws |
| `docs/vault/Design/weapons.md` | Item physics, temper, transfer, and forge actions |
| `docs/vault/Design/inscriptions.md` | Persistent-law ownership and inspection scale |
| `docs/vault/Decisions/0019-first-playable-run-and-workspace.md` | Run cadence, economy, stock, capacity, and first-playable values |
| `docs/vault/Decisions/0020-run-flow-and-rules-language.md` | Distinct Hall/Wager/Deployment/Combat flow and staged disclosure |
| `docs/vault/Decisions/0021-hourstone-table-and-result-gate.md` | Result/replay and current station responsibilities; content input only |
| `sim/Warband.Run/RunState.cs` | Current authoritative owned state |
| `sim/Warband.Run/RunController.cs` | Current authoritative actions and blocking conditions |
| `work/zero-base-ia.md` | Proposed action-first system and zero-scroll contract |

No current screenshot, current Hall layout, or previous generated UI concept is a visual or
structural input.

## Assumptions

- Desktop mouse/keyboard is the first concept target; every action retains controller/touch
  equivalents.
- Inventory means every owned weapon/trinket, including equipped objects and their current
  location, not merely the unequipped `RunState.Inventory` list.
- Workbench is one selection-driven screen. Selecting a hero, offer, item, or Inscription changes
  the central decision canvas without navigating.
- The bottom hero rail is the permanent Warband representation across out-of-combat surfaces. It
  is equipment-interactive in Workbench, read-only in Choice Gate, and formation-oriented in
  Deployment.
- Market stock remains visible; Owned Gear is a bounded, paginated Armory drawer that is collapsed
  until the player starts an inventory task.
- Hero hover/focus opens one stable full dossier; click/tap pins it. Touch and controller expose
  the same information without relying on pointer hover.
- Equipment preview renders one complete projected hero with restrained inline change markers,
  not current/new unit columns or Lose/Gain sections.
- Explicit pagination is permitted; scrolling is not.
- The existing obsidian/Sand fiction may inform materials, but no current composition,
  station geography, or previous mockup is inherited.
- Generated copy and art are illustrative. Screen regions, hierarchy, interaction state, and
  fixed-page behavior are the review target.

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `01-workbench-owned-gear.png` | Market, full inventory, roster, and laws can coexist as sources in one object-centric build surface without becoming separate destinations | Complete owned gear—including equipped locations—is visible and assignable without reconstructing inventory from hero cards | Eight visible gear tiles are comfortable at the concept size but likely become six per page at the 1280×720 floor | Three-region architecture, source model, and selection flow are literal; generated art, copy, numbers, and exact tile count are illustrative |
| `01-workbench-owned-gear-r2.png` | Market and owned inventory stay visible together while one permanent bottom hero rail owns roster context and equipment targeting | Removes both navigation bands and the command footer; swapping a weapon or trinket now happens against the hero's actual socket without a duplicate roster | The permanent rail needs a deterministic compact breakpoint when six active heroes and two reserves are owned | Four-band hierarchy, persistent rail, socket targeting, location mapping, prices, and disabled forge state are literal; art and displayed item stats remain illustrative |
| `01-workbench-hero-preview-r3b.png` | Inventory can collapse by default so one complete, pinnable hero dossier owns the center | The player can understand the actual composed unit—stats, attack, signature, passive, build, and equipment—without opening a separate hero screen or reading a comparison ledger | The dossier needs a deliberate COMBAT/BUILD page contract at the 1280×720 floor for worst-case S-rank rule text | Hero-first hierarchy, hover/focus/tap parity, pinned state, collapsed Armory handle, and permanent rail are literal; portrait and generated copy treatment are illustrative |
| `01-workbench-armory-drawer-r3.png` | A fixed paginated Armory drawer can support equipment work without displacing the pinned hero or adding scrolling | Inventory appears only when relevant; selecting gear shows the complete projected hero and a small subordinate changes line | The open state must be tested with localized copy and the maximum six active plus two reserve heroes before its minimum width is frozen | Drawer behavior, six-item page, projected hero model, inline blue change markers, and compatible rail socket are literal; art and microcopy are illustrative |
| `01-workbench-armory-mode-r4.png` | Expanded Market and Armory should be mutually exclusive while the hero and permanent rail persist | Collapsing Market during inventory work recovers enough height for readable exact rules at 1280×720; Armory still has six fixed items per page and no scroll | Switching work modes must preserve pinned hero, selected item, page, and rail focus so the collapse never feels like navigation | Mutual exclusion, Market summary bar, stable hero/rail, fixed Armory page, and projected hero are literal; generated art is illustrative |
| `01-workbench-market-recruit-r5.png` | A selected live Market Recruit should use the same full center dossier as an owned hero, with context and semantic facts visible before purchase | Makes the offer's complete combat identity, roster result, price, remaining Sand, and exact acquisition action readable without a second screen | The full dossier depends on structured semantic text and a tested page budget rather than generated copy fitting by luck | Market-to-dossier interaction, semantic hierarchy, context lines, keyword chips, acquisition result, and action placement are literal; portrait and generated copy treatment are illustrative |
| `01-workbench-tooltip-keyword-r6.png` | A keyword can disclose a short definition and source context in one bounded runtime tooltip while the full hero dossier remains stable | Clarifies domain and mechanic meaning without turning the tooltip into the only home of the rule | The tooltip must be custom at runtime, cancel stale delayed opens, and place safely at screen edges | Anchor relationship, content budget, glyph + label + color semantics, and pin handoff are literal; exact ornament is illustrative |
| `01-workbench-tooltip-equipment-r6.png` | Hover/focus on an equipped rail socket can show identity, location, compact item profile, mastery, and active status before the item is pinned into the center | Makes the permanent rail useful for inventory comprehension while preserving the center for the current object | This is the maximum acceptable tooltip density; long items must hand off to the full dossier rather than grow or scroll | Equipment tooltip anatomy, focused socket, compact semantic facts, mastery status, and pin handoff are literal; exact ornament is illustrative |
| `02-choice-gate-wager.png` | Wager, Interlude, spec, and boss reward can share one focused irreversible-choice grammar | The run's decision is unmistakable, exact consequences stay visible, and management cannot leak into the gate | Variants need distinct headings/consequences so the shared shell does not make every choice feel identical | Choice hierarchy, focus/selection/commit separation, and read-only context are literal; environment art is illustrative |
| `03-deployment-command.png` | The disclosed board can remain the only spatial screen and shed every economic responsibility | Encounter, formation, inspection, recovery, and Begin Fight form one legible task | Exact post-Wager lineup/loadout permission needs a design ruling before the interaction contract is final | Board dominance, brief/inspector roles, formation tray, and command dock are literal; encounter, units, and generated mechanics are illustrative |

## System read

The zero-base pass suggests that the current problem is not primarily visual density. It is that
named game systems became peer destinations even when they do not represent separate player jobs.
The proposed system instead organizes by commitment:

- **Workbench:** reversible build, inventory, inspection, and economic actions.
- **Choice Gate:** one irreversible 2–3 option decision.
- **Deployment:** one spatial commitment.
- **Result:** explanation and replay.

The Workbench is the largest change. R4 makes the hero the default object of understanding:
hover/focus reveals a full composed dossier, click/tap pins it, and the permanent bottom rail
remains the set of valid owners/targets. The Armory is collapsed until needed. Opening it keeps
the pinned hero visible and collapses the Market offer shelf to a summary bar. Selecting or
dragging an item wakes compatible Weapon or Trinket sockets, renders the complete projected hero,
and swaps atomically on release/confirm. A compact changes line is supporting evidence, not a
second decision surface. No secondary roster, source navigation, bottom command bar, or scrolling
inventory is required.

The Wager sample is the cleanest expression of the new rule: when the player is making one
irreversible choice, everything unrelated disappears. The Deployment sample applies the same law
to the board.

## Research notes

- [Microsoft tooltip guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips)
  treats tooltips as sparse supplementary explanation, not the sole home of essential
  information. The full hero read therefore occupies a stable focus preview that can be pinned.
- [Microsoft mouse interaction guidance](https://learn.microsoft.com/en-us/windows/apps/develop/input/mouse-interactions)
  notes that touch has no hover. Hero inspection is consequently triggered by hover, controller
  focus, or tap, with pinning available through click/tap.
- [Xbox XAG 112](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/112)
  recommends consistent component placement and predictable focus across screens, plus more than
  one way to locate content in a complex inventory. The permanent hero rail, focus restoration,
  filters, and paged Armory grid follow that guidance.
- [Into the Breach's contextual explanation work](https://www.gamedeveloper.com/design/-i-into-the-breach-i-dev-on-ui-design-sacrifice-cool-ideas-for-the-sake-of-clarity-every-time-)
  favored short, object-bound explanations over multiple paragraphs of detached help. The dossier
  groups exact rules around their Basic Attack, Signature, Passive, and Build objects instead of
  producing a generic prose tooltip.
- [Darkest Dungeon](https://darkestdungeon.wiki.gg/wiki/Inventory) exposes equipped trinkets
  through the selected hero, while
  [Backpack Battles](https://steamcommunity.com/app/2427700/announcements/?l=english) added a
  grid-storage view as an alternate inventory mode. Both support separating persistent hero
  context from an on-demand inventory browsing state.
- UI Toolkit feasibility favors a small navigation stack with composable views and immutable
  view models. The hero dossier and Armory drawer are modules, not screens; explicit pages remove
  hidden focusable content and avoid ScrollView behavior.
- Unity's runtime
  [tooltip event is Editor-only](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Tooltip-Events.html),
  so the game requires one custom overlay/controller rather than per-card Editor tooltip strings.
- UI Toolkit
  [custom controls](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-create-custom-controls.html),
  [pointer events](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Pointer-Events.html),
  [focus events](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Focus-Events.html), and
  [USS transitions](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Transitions.html)
  support one reusable hover/focus/pin control path with class-driven local motion.
- The existing Warband client already contains a semantic feedback bus, tunable motion recipes,
  a retained Painter2D effect layer, reduced-motion handling, pooled UI audio/haptic outputs,
  mechanic-family presentation, and exact-rule popovers. The implementation should consolidate
  those behind the reusable inspection layer rather than creating a second animation or tooltip
  framework. See `work/reusable-ui-system-rnd.md`.

## Review focus

This review is about the architecture, not whether the generated decoration is final:

1. Approve `01-workbench-market-recruit-r5.png` as the Workbench visual/semantic treatment and
   live-Market full-dossier behavior.
2. Approve `01-workbench-armory-mode-r4.png` as the mutually exclusive Armory work mode, with R5's
   semantic treatment applied during implementation.
3. Approve `01-workbench-tooltip-keyword-r6.png` and
   `01-workbench-tooltip-equipment-r6.png` as the two bounds of the reusable runtime tooltip.
4. Is a reusable Choice Gate the right way to unify Wager, Interlude, spec, and boss rewards?
5. Should Deployment permit lineup/loadout changes after exact encounter disclosure, or only
   formation changes?

Exact approval of the four named Workbench references authorizes the deterministic 1280×720
foundation and coded Workbench prototype described in `work/reusable-ui-system-rnd.md`. It does
not approve the generated portraits as shipping art or authorize replacing Choice Gate or
Deployment before their separate review.

## Jake review

1. Preferred sample, combination, or reject all: Workbench direction is solid; revision requested.
2. Must keep: Permanent bottom unit rail; allow Weapon and Trinket assignment/swapping there.
3. Most important next change: Remove the bottom buttons/command strip and unnecessary top-right
   navigation.
4. Latest concern: Give the preview enough room for complete unit information; reduce the emphasis
   on old/new differences; explore a hover/focus full hero card and a collapsible inventory drawer.
5. Latest direction: A selected live Market item must also get the full center read. Restore
   context/keyword theme and color coding. Research and build a reusable Unity system with
   tooltip, animation, effects, and sound hooks; wait for exact sample approval before changing
   the client.

## Approval

- Approved samples:
  - `01-workbench-market-recruit-r5.png`
  - `01-workbench-armory-mode-r4.png`
  - `01-workbench-tooltip-keyword-r6.png`
  - `01-workbench-tooltip-equipment-r6.png`
- Conditions:
  - Build the Workbench as a functional Unity UI, not a static visual recreation.
  - Preserve reusable tooltip, semantic theme, animation, audio, and flair hooks.
  - Keep animation values centrally tunable.
  - Replace visible `SAND` labels with the Sand currency icon plus numeric value.
  - Do not show a redundant `cost and remains` sentence; price and balance are sufficient.
  - Generated concept art remains reference-only unless separately curated for runtime import.
- Date: 2026-07-27

## Review log

- 2026-07-27 — Job created.
- 2026-07-27 — Inventoried the authoritative run actions and current design laws without using
  existing UI captures or previous generated concepts as references.
- 2026-07-27 — Proposed a four-primitive system: Result, Workbench, Choice Gate, and Deployment.
- 2026-07-27 — Made complete owned inventory a first-class Workbench source, including equipped
  item locations, direct assignment/transfer, forge, sell, and explicit fixed pagination.
- 2026-07-27 — Generated `01-workbench-owned-gear.png` from a blank composition. No current UI or
  earlier concept image was used.
- 2026-07-27 — Generated `02-choice-gate-wager.png` using only the new Workbench's visual grammar,
  not its layout.
- 2026-07-27 — Generated `03-deployment-command.png` using only the new system grammar and an
  action-first Deployment brief.
- 2026-07-27 — Set the review to `AWAITING_REVIEW`. No Unity/client changes were made.
- 2026-07-27 — Jake: the Workbench concept is solid. Remove the bottom buttons/command strip and
  unnecessary top-right navigation. Keep the unit rail permanently and make its Weapon/Trinket
  sockets direct assignment and swap targets. This is preference and revision feedback, not
  implementation approval.
- 2026-07-27 — Generated `01-workbench-owned-gear-r2.png`: Market and inventory are simultaneously
  visible; the command footer and navigation bands are gone; the full-width bottom hero rail owns
  equipment preview, assignment, transfer, and swap.
- 2026-07-27 — Corrected the R2 illustrative state to use the real Market prices and one coherent
  eight-item ownership/location mapping. Stored Ashwood Staff correctly disables forge until
  equipped.
- 2026-07-27 — Jake: inventory may be over-allocated, and the unit preview needs to disclose the
  whole composed hero rather than emphasizing old/new gear accounting. Requested a tooltip/full
  hero card and collapsible inventory drawer exploration.
- 2026-07-27 — Researched tooltip disclosure, hover/touch parity, predictable focus, contextual
  game explanation, hero-bound equipment inspection, and alternate grid-storage modes.
- 2026-07-27 — Generated `01-workbench-hero-preview-r3.png`: the Armory is collapsed and one
  hover/focus/pinnable Bulwark dossier owns the center.
- 2026-07-27 — Generated `01-workbench-hero-preview-r3b.png` to correct the illustrative
  Pyromancer loadout: Twin Daggers remain equipped and the Trinket socket is empty.
- 2026-07-27 — Generated `01-workbench-armory-drawer-r3.png`: the same hero remains pinned while a
  fixed six-item, two-page Armory drawer opens; item selection renders one projected hero with
  restrained inline change markers and no old/new columns.
- 2026-07-27 — A 1280×720 downscale check found that keeping both the expanded Market and Armory
  visible made exact hero rules too small. Revised the system so the two expanded work modes are
  mutually exclusive while the hero and permanent rail persist.
- 2026-07-27 — Generated `01-workbench-armory-mode-r4.png`: opening Armory collapses Market to a
  one-line summary, giving the projected hero and six-item Armory page comfortable fixed space.
- 2026-07-27 — Generated `01-workbench-market-recruit-r5.png`: selecting the live Banneret offer
  now opens its complete Recruit dossier, roster result, price/resulting Sand, exact context
  lines, mechanic-colored facts, and keyword chips.
- 2026-07-27 — Audited the existing client presentation stack. `MechanicPresentation`,
  `CardRulesPopover`, `MusterCard`, `InspectorPanel`, `UiPolishSignals`, `UiFeedbackDirector`,
  `UiFxLayer`, `UiAudioDirector`, and haptic outputs provide a strong base to consolidate rather
  than replace.
- 2026-07-27 — Confirmed through Unity 6 documentation that runtime game tooltips require a custom
  overlay/controller; custom controls, pointer/focus parity, USS transitions, Painter2D, safe-area
  placement, and scoped usage hints support the proposed reusable architecture.
- 2026-07-27 — Generated `01-workbench-tooltip-keyword-r6.png` and
  `01-workbench-tooltip-equipment-r6.png`. Both passed a 1280×720 downscale inspection without
  adding scroll; equipment represents the maximum normal tooltip density.
- 2026-07-27 — Wrote `work/reusable-ui-system-rnd.md`, including semantic/text contracts,
  projection and inspection boundaries, runtime tooltip placement/input behavior, feedback and
  audio hooks, performance limits, capture matrix, tests, and a staged delivery plan.
- 2026-07-27 — Jake explicitly approved Market Recruit R5, Armory Mode R4, Keyword Tooltip R6,
  and Equipment Tooltip R6 for Unity implementation. Additional conditions: use Sand icons
  instead of the visible word `SAND`, remove the redundant `cost and remains` line, keep motion
  tunable, and build the complete functional tooltip/flair system.
- 2026-07-27 — Implemented the approved Workbench in Unity as one fixed, zero-scroll surface:
  five-offer Market shelf, complete selected-object dossier, mutually exclusive paged Armory,
  and permanent six-field/two-reserve equipment rail. Existing run actions drive selection,
  purchase, hold, reroll, inventory arming, equip/transfer/unequip, and progression.
- 2026-07-27 — Added one shell-owned runtime tooltip service for pointer and focus disclosure,
  stale delayed-show cancellation, mechanic-family glyph/color theming, keyword definitions,
  equipment profiles, disabled reasons, final-geometry safe-edge placement, and pin handoff.
  Tooltip timing and size, drawer motion, semantic FX, audio hooks, haptics, and reduced motion
  remain centrally tunable through `HubPresentation.json`.
- 2026-07-27 — Unity verification passed a clean compile/console check, Workbench semantic
  contracts, Market and Armory containment, explicit absence of `ScrollView`, permanent 6+2 rail
  geometry, functional keyword/equipment tooltip seams, and 1280×720 offscreen captures for
  Market, Armory, Keyword Tooltip, and Equipment Tooltip.
- 2026-07-27 — Added deterministic Workbench fixtures, reusable resolved-layout contracts, and a
  remote-safe smoke/full QA runner. The full matrix covers nine states plus expanded-copy Recruit
  at five viewports, including 21:9, and produces a report plus contact sheet.
- 2026-07-27 — The matrix exposed and fixed real composition failures: the live dossier bottom
  cutoff, ultrawide market over-allocation, Rank Up header collision, weapon comparison overflow,
  Armory row rounding, projected-hero rule truncation, and UI Toolkit font-leading tolerances.
- 2026-07-27 — Final structural result: 50/50 PASS in
  `client/TempCaptures/ui-qa/20260727-141414/report.md`. Human review also checked the contact
  sheet and exact 1280×720/21:9 Market, Rank Up, Weapon, Armory, and tooltip captures. The remote
  Editor had no Game View window, so every capture is explicitly labelled as an exact-size
  offscreen panel fallback.
