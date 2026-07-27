# Warband UI/UX Redesign Brief

Status: proposal for implementation planning  
Scope: starter selection, Hourstone Table, Market, Warband management, and shared run HUD  
Primary objective: make the current game easier to read and more satisfying to operate without changing its visual identity or core systems

## Executive direction

Warband already has the expensive parts of a good game UI:

- strong champion portraits;
- a coherent dark steel / sand-gold visual identity;
- consistent stat colors and icon language;
- an evocative 3D Hourstone hub;
- compact, deterministic combat concepts that can be explained precisely.

The current problem is hierarchy, not aesthetics. Almost every screen presents navigation, roster state, detailed inspection, and the current decision at the same visual weight. That makes the interface feel more complicated than the game actually is.

The redesign should follow three rules:

1. **One screen, one decision.** The largest and brightest element must answer “what am I deciding right now?”
2. **Persistent context, contextual controls.** Act, beat, Sand, and the three fielded champions stay visible. Full roster management, reserve slots, and equipment actions appear only when relevant.
3. **Summary first, exact mechanics on demand.** A player should understand an offer or champion in two seconds, then be able to inspect deterministic details without leaving the screen.

Do not replace the existing art direction. Refine the information architecture around it.

## Product read

The current screens imply the following game:

- a deterministic hex-based autobattler roguelite;
- three champions initially fielded, with reserve and additional field slots unlocked during a run;
- champions defined by health, power/healing, reach, cadence, mana generation, a Signature, and a Passive;
- a run split into three acts and five beats per act, ending in a boss;
- Sand as the primary run currency;
- the Hourstone Table as a diegetic hub connecting the next encounter, Market, Armory, Warband, and run-wide laws;
- rank-ups, recruits, and equipment as the main buildcraft decisions.

The planned progression expansion makes UI clarity more important: each champion will eventually receive three visible choices per rank, following **Deepen / Bridge / Answer**, with up to 27 final builds per hero. The UI therefore needs to communicate build identity and synergy, not merely expose exact stats.

## Highest-priority problems

### 1. The bottom dock behaves like a second application

The current dock permanently shows field count, all field slots, locked slots, reserve slots, Armory state, and manage controls. It consumes substantial height and repeats information already present in the active screen.

Replace it with a collapsed **Warband strip**:

- three portrait chips for fielded champions;
- rank and one build-identity icon on each chip;
- one `Manage Warband` control;
- optional alert badges for unspent rank choices, empty equipment, or a newly opened slot.

Expand the full dock only in Warband, deployment, equipment drag/drop, or a deliberate `Manage` state.

### 2. Navigation is duplicated and under-labeled

The top row mixes an act/beat tracker with four icon-only service buttons, while screens also provide back buttons and the 3D table provides service plaques. The result is repetition without clear location.

Use one navigation model:

- the top HUD contains run state only: game name, act, beat path, Sand;
- the screen header contains a labeled back destination and current title;
- the Table itself is the service selector;
- Market, Warband, Armory, and Hourstone pages may use a compact labeled subnav if direct switching is valuable.

Avoid unlabeled icons as the primary navigation mechanism.

### 3. Inspectors dominate instead of assisting

The Market and Warband inspectors occupy roughly half to two-thirds of the screen. This is appropriate for exact mechanics, but their framing is generic and their content has limited hierarchy. The player sees many equally weighted stat boxes and long ability rows before seeing why a unit matters.

Every inspector header should state:

- **identity:** `Banneret`;
- **role/build tags:** `Recruit · Support`;
- **one-line promise:** `Accelerates nearby allies.`;
- **party relevance:** `Best with Phalanx + Cleric`;
- exact stats and ability rules beneath that.

Exact mechanics remain visible, but the player receives an interpretation layer first.

### 4. Affordability feedback is technically present but cognitively awkward

An offer can show `5 SAND`, `SHORT`, a disabled purchase action, and the player’s `SAND 4` in separate areas. The disabled action currently risks saying what the item costs rather than what the player lacks.

Use:

- card price: `5 SAND`;
- small state beside price: `SHORT 1`;
- disabled CTA: `NEED 1 MORE SAND`.

Never use a red treatment on the entire card unless the card itself is dangerous or invalid. Unaffordable stock should remain desirable.

### 5. Important information is available, but the first read is too dense

The Muster screen exposes five huge portraits, three stats, Signature, Passive, mechanic tags, cadence, and mana costs simultaneously. This is comprehensive but makes the initial “pick three champions” decision harder.

The first read should be:

- portrait;
- name and role;
- concise combat promise;
- health, output, and reach;
- one Signature summary;
- synergy or complexity tag.

Passive details, exact cadence, mana generation, and glossary definitions should appear in a focused state or tooltip. Do not make combat-critical facts hover-only, but avoid showing every exact number on every unselected card.

## Target information architecture

### Persistent run HUD

Always show:

- `ACT 1 / 3`;
- beat path with current beat, known event type, and boss;
- `SAND 4`;
- optional settings/help access.

Do not show:

- service navigation icons;
- Armory inventory count;
- all locked field/reserve slots.

### Context header

Each page shows:

- labeled back destination (`TABLE`);
- current page title (`THE MARKET`);
- one page-level action (`REROLL · 1 SAND`);
- optional concise stock/progress state (`5 OFFERS`).

### Collapsed Warband strip

Show:

- the three fielded champion portrait chips;
- rank;
- current weapon icon;
- actionable badge only when necessary;
- `Manage Warband`.

Hide:

- locked future slots;
- empty reserves;
- duplicated `FIELD 3 / 3`;
- Armory drop target outside equipment interactions.

## Screen specifications

### A. Muster Your Warband

Primary question: **Which three champions define this run?**

Recommended layout:

- five champion cards in a responsive horizontal rail or 3+2 grid;
- selected cards gain a strong sand-gold edge and a numbered pick marker;
- a sticky party tray shows picks 1–3 with role icons and formation summary;
- the CTA reads `BEGIN WITH THIS WARBAND`, not `SELECT 3 CHAMPIONS`;
- the selected card expands or opens a side inspector for exact mechanics;
- unselected cards stay concise.

Add a lightweight composition summary:

- `Frontline: 2`;
- `Sustain: 1`;
- `Ranged: 0`;
- contextual warning such as `No ranged pressure` without blocking the selection.

Do not score parties as objectively good/bad. Use descriptive coverage because surprising compositions are part of roguelite discovery.

Acceptance criteria:

- player can identify role and combat promise without hovering;
- selected count and selection order are unmistakable;
- CTA is visible at 16:9 without scrolling;
- exact mechanics remain one focus/click away;
- cards can scale from five to eight current heroes without becoming narrower than readable;
- the layout can later display three rank choices using the same card grammar.

### B. Hourstone Table

Primary question: **What do I do before the next beat?**

Recommended layout:

- preserve the central 3D Table and glowing Hourstone;
- make the next encounter card the dominant overlay;
- give the encounter card one verb-based action such as `SET WAGER`, `DEPLOY`, or `ENTER BREACH`;
- place Market, Armory, and Hourstone as subordinate plaques around the table;
- show counts as secondary text: `5 offers`, `0 stored`, `Run laws`;
- collapse the roster to the narrow Warband strip.

The current upper-left lore panel is atmospheric but competes with the encounter. Move lore into:

- a short subtitle beneath the screen title;
- a codex/lore affordance;
- or a brief entrance animation that collapses after first viewing.

Acceptance criteria:

- a five-second test consistently identifies the next encounter as the primary action;
- the 3D hub occupies most of the canvas;
- no service plaque competes with the primary CTA;
- all major services remain reachable in one click;
- keyboard/controller focus order follows encounter, Market, Armory, Hourstone, Warband.

### C. Market

Primary question: **Which offer improves my current build, and can I afford it?**

Recommended layout:

- left 55–60%: five offers in a stable grid;
- right 40–45%: selected-offer inspector;
- collapse the persistent roster into three portrait chips;
- use the recovered vertical space for clear buttons and comparison;
- keep stock cards visually consistent across recruits, rank-ups, weapons, and future Inscriptions.

Offer card anatomy:

1. offer type (`RECRUIT`, `RANK UP`, `WEAPON`);
2. name and portrait/icon;
3. one-line outcome or role tag;
4. price;
5. shortage state, if any;
6. held/frozen state, if any.

Inspector anatomy:

1. identity and one-line promise;
2. relevant tags;
3. compact exact stats;
4. three ability/effect rows;
5. party impact or comparison;
6. purchase and hold actions.

Comparison should be contextual:

- recruit: compare role coverage and field/reserve capacity;
- rank-up: show changed rules and deltas from current rank;
- weapon: compare directly against the selected compatible champion’s worn weapon;
- law/inscription: show affected champions and systems.

Add build-language tags aligned to future progression:

- `DEEPEN` for strengthening the current engine;
- `BRIDGE` for connecting systems such as Shield, Mana, Burn, movement, or Fields;
- `ANSWER` for solving an encounter problem at a cost.

These are interpretive tags, not rarity tiers.

Acceptance criteria:

- all five offers and both actions fit at 16:9 without scrolling;
- selected state is visible without relying on color alone;
- shortage is expressed as the missing amount;
- a recruit explains party impact;
- a rank-up shows before/after changes;
- a weapon provides a direct comparison target;
- hold/freeze state remains visible on the card after leaving the Market.

### D. Warband management

Primary question: **How is my current team built, and what do I want to change?**

Recommended layout:

- use the full page for roster and build inspection; hide the redundant bottom dock;
- left column shows field and reserve cards, with locked capacity summarized in a single progression row;
- right inspector uses the same header/stat/ability grammar as Market;
- group actions by intent:
  - `MOVE TO RESERVE`;
  - `CHANGE EQUIPMENT`;
  - `DISMISS`;
- visually separate destructive actions;
- show equipment slots as part of the champion card and inspector rather than as tiny icons in two places.

The large unused area below three field cards should become one of:

- a formation preview;
- team coverage summary;
- reserve and recruit capacity;
- synergy/interaction list;
- or simply a narrower roster column.

Recommended team summary:

- `Phalanx protects the front`;
- `Cleric sustains adjacent allies`;
- `Berserker gains pressure at low health`;
- conflicts or opportunities such as `Cleric sustain may delay Burning Hours`.

The summary should be rules-derived and deterministic, not an opaque power score.

Acceptance criteria:

- no champion appears twice on the same screen;
- all champion actions are visible without being covered by a persistent dock;
- destructive action requires confirmation and is visually separated;
- field/reserve movement supports mouse, keyboard, and controller;
- exact equipment compatibility and resulting stat changes are visible before confirming.

## Shared component plan

Build or refactor these shared primitives before rewriting screens:

- `RunHud`
- `BeatPath`
- `ContextHeader`
- `CollapsedWarbandStrip`
- `OfferCard`
- `ChampionCard` with `compact`, `choice`, and `management` variants
- `EntityInspector`
- `StatRow`
- `RuleRow`
- `SynergyStrip`
- `CurrencyPrice`
- `PrimaryActionBar`
- `TooltipGlossary`

Do not create separate stat/ability markup for Muster, Market, and Warband. The same entity presentation grammar should appear everywhere, with density controlled by variant.

## Visual-system adjustments

Keep:

- near-black canvas;
- blue-gray surface hierarchy;
- sand-gold primary/action accent;
- current stat colors;
- uppercase condensed labels;
- portrait-forward champion cards.

Adjust:

- reduce borders: one panel should usually have one border, not nested boxes around every line;
- use gold only for current selection, progress, and primary action;
- reserve bright blue for keyboard/controller focus;
- increase body text size and contrast slightly;
- use spacing and typography before dividers;
- keep decorative bevels on major panels, not every micro-component;
- add a subtle translucent scrim behind overlay UI so the 3D scene remains legible.

Recommended semantic states:

- selected: gold edge + check/marker;
- focused: blue outer ring;
- affordable: neutral price;
- unaffordable: neutral card + red `SHORT N`;
- disabled: reduced contrast, never the only explanation;
- dangerous: red only for dismiss/sell/irreversible choices;
- new/actionable: small gold badge.

## Interaction and accessibility

- Do not require hover for essential mechanics; focus/click must expose the same content.
- Maintain a visible focus ring separate from selected state.
- Add controller shoulder-button switching only where page tabs are visible.
- Provide a reduced-motion setting for hub camera and Hourstone effects.
- Use text plus icon for role, currency, and destructive states.
- Minimum practical body size at the target resolution should be tested at 1080p, not only ultrawide/high-DPI captures.
- Support UI scaling at 80%, 100%, 120%, and 140%.
- Keep common actions in stable locations across Market and Warband.

## Implementation sequence

### Phase 1: hierarchy correction

1. Replace the permanent bottom dock with `CollapsedWarbandStrip`.
2. Introduce the shared `RunHud` and `ContextHeader`.
3. Remove duplicated top service navigation.
4. Ensure all primary actions fit at 1080p.

Expected result: the game immediately feels calmer without altering mechanics.

### Phase 2: shared inspectors

1. Build `EntityInspector`, `StatRow`, and `RuleRow`.
2. Add identity sentence, build tags, and party-impact strip.
3. Use the inspector in Market and Warband.
4. Correct shortage copy and action states.

Expected result: players understand why an option matters before reading every exact rule.

### Phase 3: decision-specific screens

1. Redesign Market stock grid and contextual comparison.
2. Redesign Warband management without duplicated roster.
3. Simplify Muster cards and add party coverage summary.
4. Refine the Hourstone Table encounter CTA and service hierarchy.

### Phase 4: progression-ready grammar

1. Add `DEEPEN / BRIDGE / ANSWER` choice tags.
2. Reuse `ChampionCard choice` for three-choice rank offers.
3. Add before/after rule diffs.
4. Test long names, three choices, all eight heroes, and 27-outcome progression data.

## Agent-ready implementation prompt

Copy the following into Claude Code or Codex from the Warband repository root:

> Implement the first UI hierarchy slice described in `warband-ui-ux-redesign-brief.md`.
>
> Scope:
>
> 1. Create a compact shared run HUD containing only Warband title, Act, beat path, and Sand.
> 2. Replace the permanent full bottom roster dock on Table, Market, and inspection screens with a collapsed strip containing the three fielded champion portrait chips and a `Manage Warband` action.
> 3. Remove duplicated service-navigation icons where the screen already has a labeled context header or the Table provides navigation.
> 4. Refactor the Market into a stable stock grid plus selected-offer inspector. Keep all five offers, the purchase action, and hold action visible at 1920×1080.
> 5. Express affordability as price `N SAND`, optional shortage `SHORT N`, and disabled CTA `NEED N MORE SAND`.
>
> Constraints:
>
> - Preserve all current game logic, deterministic rules, save data, assets, portraits, and service routes.
> - Preserve the dark steel / blue-gray / sand-gold visual identity and current stat-color semantics.
> - Do not invent new currencies, rarity, mechanics, or progression.
> - Do not remove exact mechanical details; put them behind selected/focused inspection where necessary.
> - Essential information must work with hover, keyboard focus, and click/tap.
> - Selected and focused states must be visually distinct.
> - Reuse shared components rather than duplicating champion/stat/ability markup.
> - Keep changes incremental and compatible with the current UI architecture.
>
> Before editing:
>
> - identify the components/styles responsible for the top HUD, bottom roster dock, Market stock cards, and selected-card inspector;
> - summarize the planned component changes and list touched files;
> - identify any screenshot-test, PlayMode-test, or visual-test infrastructure already present.
>
> Verification:
>
> - run existing tests and lint/build checks;
> - capture 1920×1080 screenshots of Table, Market with affordable selection, Market with unaffordable selection, and Warband management;
> - verify no primary button is obscured;
> - verify five Market offers fit without scrolling;
> - verify keyboard/controller focus order;
> - report any behavior intentionally deferred.

## Evaluation checklist

Ask five playtesters to view each screen for five seconds, then answer:

1. What is the primary action?
2. How much Sand do you have?
3. Which champion or offer is selected?
4. Why might the selected option help your party?
5. Where would you go to manage the Warband?

The redesign succeeds when at least four of five answer each question correctly without being coached.

## Mockup notes

Two generated direction mockups accompany this brief:

- Hourstone hub: prioritizes the next beat, preserves the 3D table, and collapses roster management.
- Market: turns the screen into a clear offer grid plus contextual inspector, with explicit party relevance and shortage copy.

Treat them as hierarchy and layout targets, not pixel-perfect art specifications. Existing assets, typography, iconography, and engine constraints should determine the final implementation.
