# Management Hall polish — The Last Hour instrument

**Status:** approved; reusable polish foundation built, broader pass in progress · **Date:** 2026-07-25
**Scope:** the Hourstone Table, station workspaces, cards/dossiers, transactions, and the
post-fight result gate. This is a polish and reusable-systems pass, not another flow redesign.

## The honest read

ADR 0021 fixed the important structural problems. The Hall now has remembered geography,
explicit actions, exact rules, responsive landscape compositions, and a result gate. Its current
visual layer is clean programmer UI, not yet the game's final identity:

- portraits carry more art quality than the surrounding surfaces;
- the Table reads as a diagram rather than a mysterious place outside time;
- station symbols are provisional text/emoji rather than one authored family;
- workspace navigation still resembles application tabs;
- selection is clear but transactions do not yet feel physical or causal;
- the result gate explains the fight but does not celebrate or mourn it;
- default UI Toolkit scrollbars and some dossier crops expose the scaffolding;
- configured transition timings exist, but there is no reusable recipe system for selection,
  transfer, number changes, attention, success, and failure;
- mobile is responsive in captures, but touch feel, haptics, and real safe areas remain
  device-unverified.

The next pass should preserve the information architecture and replace that scaffolding with one
coherent material, motion, and feedback grammar.

## North star: an instrument cut from the Tower

The Hall is **an obsidian war instrument from outside time**. It is not a medieval tavern,
parchment inventory, or glowing spaceship dashboard. Cold iron gives it structure, bone-white
marks make it readable, and living Sand is the only material that flows.

The player is conducting a war council around the Hourstone:

- **obsidian** is the stable surface and negative space;
- **cold iron** is structure: borders, rails, rivets, sockets, separators;
- **bone/ash ink** is knowledge: names, rules, numbers, diagrams;
- **Sand** is agency and time: affordability, recommendation, progress, transfer, reward;
- portraits and equipment art are the champions and relics placed onto the instrument.

This direction belongs to no recruited era, so bronze shieldmaid, plague cleric, gunslinger, and
far-future pyromancer can all sit inside it without the UI becoming a collage. It also directly
expresses ADR 0010's binding law that everything references time and the Tower is constant.

### Anti-goals

- No parchment page backgrounds, wax seals, or medieval filigree as the global frame.
- No neon rainbow station colors. Station identity comes from symbol, geometry, and motion.
- No permanent ambient movement behind dense rules text.
- No elastic mobile-app bounce, confetti, or casino sparkle.
- No animation that delays a second action or carries information unavailable when reduced
  motion is enabled.
- No visual response before an authoritative run transaction succeeds.

## Visual grammar

### Semantic palette

| Token | Role | Candidate |
|---|---|---|
| Ink | deepest background | `#070B11` |
| Obsidian | primary surface | `#0D141E` |
| Slate | raised panel/card | `#162131` |
| Iron | border/divider | `#40536B` |
| Ash | secondary copy | `#8C9AAF` |
| Bone | primary copy | `#ECE8DF` |
| Sand | agency, price, active route, time | `#D9A43A` |
| Tower blue | preview, focus, selected-but-uncommitted | `#68A5E7` |
| Verdant | confirmed gain or improved value | `#65C99A` |
| Blood | error, danger, loss | `#B44C43` |

Colour is semantic, never decorative. In particular:

- blue means **I am inspecting or choosing this**;
- Sand means **this advances/spends/rewards the run**;
- green means **the committed result improved a value**;
- red means **failure, danger, or a real negative delta**.

Every colour signal also gets shape, icon, text, or motion. Cost is not red merely because it is a
decrease; an affordable cost remains Sand. A deficit or rejected action is Blood.

### Type and density

- Titles remain bold, condensed-feeling, and widely tracked: carved labels on an instrument.
- Body/rules text stays sentence case and quiet. Uppercase is reserved for labels and triggers.
- No meaningful body text below 16 logical px; compact metadata targets 12–13, never 9–10 on
  handheld layouts.
- Numeric values use tabular-width treatment where available and never shift their container.
- Rules retain ADR 0020's grammar: `TRIGGER` → named rule → exact mechanical sentence.
- Flavour copy is visually separated and never substitutes for the exact sentence.

### Shapes and icon family

Replace provisional symbols with one mono-line, cut-metal family. Each sigil must survive at
20 px and as a black silhouette:

- **Market:** divided coin / ledger notch;
- **Warband:** banner inside a three-position formation;
- **Armory:** crossed tool and blade inside a riveted socket;
- **Hourstone:** split hourglass inside two concentric rings;
- **Breach:** an aperture broken open along a clock hand.

Secondary icons cover inspect, selected, held stock, affordable, deficit, new, equipped,
improved, worsened, and locked. Author them as SVG/VectorImage-compatible source and atlas the
raster fallbacks. Emoji are removed completely.

### Station identity without station colours

| Station | Geometry | Motion verb | Transaction sound family |
|---|---|---|---|
| Market | ledgers, slots, divided coins | slide / deal | tile, coin, dry paper |
| Warband | standards, formation pips | gather / rally | cloth snap, low drum |
| Armory | rails, rivets, chevrons | clamp / seat | metal click, leather |
| Hourstone | rings, notches, inscriptions | orbit / engrave | glass grain, harmonic chime |
| Breach | split aperture, clock hand | unlock / open | stone release, low air |

The same palette and components remain everywhere. These geometric and temporal signatures make
stations recognizable without five unrelated themes.

## Interaction grammar: preview, selection, commitment

The interface needs three visually distinct verbs because a data-first game becomes dangerous
when selecting and committing feel alike.

1. **Preview** — hover/focus/touch-down. Tower-blue edge, 2 px lift, immediate dossier preview,
   quiet tick. No check mark and no economic sound.
2. **Selected** — click/tap release. Blue socket/pin locks in, related comparison values wake,
   action button becomes specific. Selection never mutates the run.
3. **Committed** — explicit Buy/Equip/Bind/Begin action succeeds. Sand/green receipt, physical
   transfer, destination response, success sound/haptic. Failed actions produce no transfer.

### Reusable state table

| State | Visual | Motion | Other feedback |
|---|---|---|---|
| Rest | iron edge | none | none |
| Hover/focus | blue edge + visible focus bracket | 2 px lift, 90 ms | soft tick, no haptic |
| Press | surface darkens | 0.985 scale, 60–80 ms | touch selection haptic |
| Selected | blue socket/check + stable raised state | settle 120 ms | dry token-set sound |
| Attention | Sand notch + named `NEW/READY` chip | one travelling glint, then still | optional quiet cue once |
| Disabled | ash value + explicit reason | none | no hover lift |
| Success | receipt stamp + changed values | causal transfer, 180–320 ms | success sound/haptic |
| Error | Blood edge + exact reason | one 2–3 px lateral knock, 140 ms | hollow knock/error haptic |

Focus must always be more than a subtle glow: bracket/border plus fill or weight change. Modals
trap focus and restore it to the invoking element when dismissed.

## The causal feedback sequences

All authoritative mutations happen immediately. Presentation shows the receipt afterward and is
interruptible; it never owns or delays run truth.

### Buy

1. Buy button compresses and locks against a duplicate press.
2. The Sand counter ticks to its new absolute value; 3–5 pooled grains travel toward the card.
3. The card receives an `ACQUIRED` stamp and moves a short distance toward its destination sigil.
4. Warband, Armory, or Hourstone answers with one pulse and an attention notch.
5. The exact receipt remains readable: `Cracked Hourglass acquired · 3 Sand spent`.

Do not animate one grain per Sand. Magnitude changes sound weight and count-up duration, not
particle spam.

### Reroll

- Held stock remains physically pinned.
- Unheld offers wipe toward the Market edge.
- The Sand ledger updates once.
- New offers deal in with a 35 ms stagger, capped at 175 ms.
- The first new offer gains focus only for keyboard/controller input; pointer/touch focus stays
  where the player left it.

This is the clearest opportunity for the Hall to feel like a real instrument rather than a grid
refresh.

### Equip

- Selecting an item wakes compatible champion sockets.
- Selecting a champion draws a thin path between item and portrait and reveals exact deltas.
- On Equip success, a ghost of the item clamps into the hero card, changed rows pulse once, and
  the previous item visibly returns to the rack when applicable.
- Green belongs only to improved values; unchanged values stay Bone/Ash; worse values use Blood.

### Recruit, rank, and specialization

- Recruit cards travel toward the Warband station; capacity increments at arrival.
- Duplicate/rank resolution stacks the two portraits for one beat, then stamps the new rank.
- A specialization choice uses a blocking two-option comparison; the chosen branch engraves into
  the hero dossier rather than merely disappearing.

### Bind an Inscription

- The selected rule orbits once around the Hourstone.
- A ring segment draws clockwise and resolves into the persistent badge.
- Existing badges shift only after the new badge lands.
- The exact run-wide law remains visible throughout; spectacle cannot cover the rule sentence.

### Route through the Table

- Hover/focus lights a thin Sand path from Hourstone to station.
- Selection sends one grain along that path, then the workspace enters from its remembered
  direction.
- Returning reverses spatially to the Table.
- Workspace navigation becomes a compact **Table compass**, not four equal application tabs:
  sigils preserve west/centre/east/south relationships and the current station is a filled socket.

### Result gate

1. Freeze the battlefield and lower it, do not erase it.
2. Result panel resolves quickly; Victory/Defeat is immediately readable.
3. Sand and casualty values count to their exact absolutes in 250–400 ms.
4. Top damage and death causes reveal in causal order.
5. The recommended station sigil lights; Continue inherits that destination.
6. On Continue, the panel closes toward the recommendation and the Hall receives the route.

A boss result may use the longer 750 ms ceremony. Ordinary fight results stay brisk.

## Show rules instead of adding prose

Into the Breach's developers identified animated tooltips as more effective than several
sentences for spatial weapons. Warband should add a reusable **Rule Preview** control rather than
keep increasing card copy.

`RulePreviewModel` is presentation metadata, not inferred game logic:

- source hex/unit;
- target relationship;
- affected hex shape or line;
- ordered beats;
- before/after status or number;
- optional loop duration.

The control renders a tiny 4×4/6×4 hex diagram through `Painter2D`. It is shared by recruit,
Market, dossier, encounter reveal, and future enemy intent. It plays only while focused/open,
loops at most twice, and becomes a labelled start/end diagram under reduced motion. First scope is
shape, reach, direction, and target relationship—not bespoke animation for every rule.

## Reusable implementation architecture

The existing `HubFlowPlanner`, plain screen models, and authoritative `RunShell` boundary remain.
Polish is divided into five small systems.

### 1. Theme tokens

`LastHourTokens.uss`

- palette, type scale, spacing, radii, line weights, focus, elevation;
- semantic component states;
- responsive token overrides;
- no screen-specific hardcoded colour values outside this file.

`UiIconCatalog`

- stable semantic icon ids;
- station SVG/VectorImage references and raster fallback atlas;
- no gameplay ids and no view querying.

### 2. Motion recipes

Evolve `HubPresentation.json` into `UiPresentation.json` with named recipes:

- `press`, `focus`, `select`, `route`, `reveal`, `transfer`, `count`, `attention`,
  `success`, `error`, `result`, `boss`;
- duration, stagger, distance, scale, and supported easing;
- reduced-motion replacement per recipe.

`UiMotionDirector`

- cancellation/generation token per target;
- transform/opacity/colour fast paths only during animation;
- `UsageHints.DynamicTransform` on moving elements and `GroupTransform` on moving containers;
- interrupted recipes settle into the newest model state;
- never blocks interaction while decorative cleanup finishes;
- hides inactive trees with `DisplayStyle.None` after transitions complete.

`InteractableFeedbackManipulator`

- one reusable hover/focus/press implementation for buttons, cards, station sockets, and choices;
- input-aware: mouse hover, controller focus, and touch press share semantics without pretending
  touch has hover;
- emits preview/selected cues but never gameplay actions.

### 3. Semantic feedback bus

Replace the payload-free cue with:

```text
UiFeedbackEvent
  Kind        Select | Spend | Acquire | Transfer | Equip | Bind | Reroll | Route |
              Attention | Result | Success | Error
  SourceId    stable presentation target
  TargetId    optional destination target
  Amount      optional exact delta
  Tone        Neutral | Sand | Positive | Negative | Major
  Receipt     already-hydrated player-facing sentence
```

The pipeline is:

```text
authoritative run command
  → before/after receipt
  → semantic UiFeedbackEvent
  → UiFeedbackDirector
      ├─ motion recipe
      ├─ UiFxLayer
      ├─ UiAudioDirector
      └─ IUiHaptics
```

`UiTargetRegistry` maps stable presentation ids to live `VisualElement`s. Dynamic cards register
when bound and unregister when pooled. If a target is absent, the receipt still displays and
audio/haptics can still respond; feedback can never make the transaction fail.

### 4. One UI FX layer

`UiFxLayer` is one custom VisualElement at the document root:

- `Painter2D` draws Table paths, ring traces, focus brackets, and transfer arcs;
- a small fixed pool handles Sand grains, stamps, and value-change ticks;
- no GameObject per particle and no per-card particle component;
- animation budget: 24 live grains desktop/tablet, 12 phone, 0 in reduced-motion mode;
- ambient Table motion updates at a capped rate and stops while a dense dossier is being read.

Use two small shared textures at most: obsidian grain and soft Sand noise. Put static icons in one
atlas to preserve UI Toolkit batching.

### 5. Audio and haptics as optional sinks

`UiAudioDirector`

- one 2D UI bus and a small `AudioSource` pool;
- clip manifest keyed by semantic cue, with 2–4 variants, small pitch range, volume, priority,
  and minimum repeat interval;
- frequent hover/focus cues are aggressively deduplicated;
- errors, purchases, binds, and results outrank selection sounds;
- UI bus remains audible when combat is paused and gets its own settings slider.

First sound family: soft ceramic/stone tick, Sand pour, dry stamp, card sweep, cloth snap, metal
seat, glass-grain chime, aperture release, hollow error knock, result chord.

`IUiHaptics`

- `None`, `Selection`, `LightImpact`, `Success`, `Warning`, `Error`;
- platform implementation plus no-op desktop/editor implementation;
- respects OS capability, player setting, and reduced-feedback preference;
- never uses a long legacy buzz as button feedback.

## Responsive and accessibility contract

- Landscape remains the first-playable orientation.
- Touch targets remain at least 56 logical px, exceeding common 44/48 guidance.
- No gesture is required: swipe shelves also have tap/select actions and visible close/back.
- Touch-down gives immediate press response; transaction feedback begins only on successful
  release/command.
- Phone dossiers remain side sheets, with a visible Close and Escape/Back equivalence.
- Focus is always visible, trapped inside blocking modals, and restored on close.
- No information is communicated by colour, motion, sound, or haptics alone.
- Reduced motion removes travel, shake, looping ambience, and count-up; it substitutes immediate
  state, one opacity change, and the same receipt.
- Add a separate `Ambient UI motion` toggle so a player can keep brief causal feedback while
  disabling background activity.
- Important text sits on opaque or near-opaque surfaces; the battlefield cannot reduce result
  readability.

## Asset budget

This pass needs a small, deliberate kit rather than a UI asset pack:

- 5 station sigils;
- 10–12 semantic control/receipt icons;
- one icon atlas;
- one obsidian-grain texture and one Sand-noise texture, 256 px;
- optional five station edge-pattern strips;
- 10–12 short UI sound families with variants.

The core shapes should be authored as SVG/code-native vectors so they remain crisp at phone and
4K sizes. Texture generation is reserved for grain/noise, not icons or text.

### Confirmed focused generation batch

The 2026-07-25 authored batch is deliberately split between **reference**, **runtime candidate**,
and **runtime-ready** assets. Generated work is never accepted merely because it exists.

| Class | Output | Intended use |
|---|---|---|
| Concept | `ArtSource/HallConcepts/hall_overview_concept` | composition/material/lighting reference for the Table |
| Concept | `ArtSource/HallConcepts/hall_market_concept` | Market workspace integration reference |
| Concept | `ArtSource/HallConcepts/hall_purchase_concept` | Buy receipt and causal transfer reference |
| Concept | `ArtSource/HallConcepts/hall_mobile_concept` | wide-phone composition and touch-density reference |
| Material | `Resources/UI/Hall/Materials/hall_obsidian` | Table/floor primary surface |
| Material | `Resources/UI/Hall/Materials/hall_slate` | raised Table bed and secondary structure |
| Material | `Resources/UI/Hall/Materials/hall_iron` | cold-iron rim, braces, and Tower blades |
| Material | `Resources/UI/Hall/Materials/hall_dark_iron` | sockets, piers, and deep structure |
| Material | `Resources/UI/Hall/Materials/hall_living_sand` | emissive Hourstone material |
| FX sprite | `Resources/UI/Hall/FX/sand_grain_soft` | soft transfer particle |
| FX sprite | `Resources/UI/Hall/FX/sand_grain_shard` | sharp commit particle |
| FX sprite | `Resources/UI/Hall/FX/acquisition_stamp` | acquired receipt mark |
| FX sprite | `Resources/UI/Hall/FX/receipt_bracket` | focus/receipt corner motif |
| FX sprite | `Resources/UI/Hall/FX/route_glint` | Table-path travelling highlight |
| FX sprite | `Resources/UI/Hall/FX/station_notch` | station attention marker |
| FX sprite | `Resources/UI/Hall/FX/card_wipe` | Market deal/reroll mask |
| FX sprite | `Resources/UI/Hall/FX/hourstone_halo` | restrained Hourstone pulse mask |
| Mesh candidate | `GeneratedAssets/Hall/Meshes/hourstone_core_a` | geometric split-hourglass candidate |
| Mesh candidate | `GeneratedAssets/Hall/Meshes/hourstone_core_b` | fractured mineral candidate |
| Mesh candidate | `GeneratedAssets/Hall/Meshes/hourstone_core_c` | restrained Tower-relic candidate |
| Audio | `Resources/UI/SFX/{preview,select,route,deal,purchase,seat,bind}_1..2` | two variants for frequent semantic cues |
| Audio | `Resources/UI/SFX/{commit,major,error}_1` | one high-priority commit/result/error cue |
| Audio | `Resources/UI/SFX/hall_ambience` | seamless low Tower/Hall bed |

This is 38 generated assets: 4 concepts, 5 materials, 8 FX sprites, 3 mesh candidates, and
18 audio clips. The three meshes are normalized and inspected side by side; only the chosen
candidate moves to `Resources/UI/Hall/Meshes/hourstone_core`. Sprite background removal is a
second import operation, not an additional deliverable. Generated concepts never ship in
`Resources`.

### 2026-07-25 curation outcome

Generation success is not acceptance:

- the four composition concepts passed as source reference;
- `hall_iron` and the refined `hall_living_sand_v2` passed for runtime use;
- generated obsidian, slate, and dark-iron surfaces were rejected because their large seams,
  masonry, and plate borders reveal the repeat. The Hall deliberately retains its clean
  procedural versions instead of loading those candidates;
- `card_wipe_v2`, `hourstone_halo_v2`, and `station_notch_v2` replace the three rejected first
  passes. The remaining five FX sprites passed as candidates;
- all 18 authored audio clips passed structural validation and replace synthesized fallbacks by
  resource name;
- Hourstone B had the best distant silhouette, but every raw provider mesh was approximately
  1.45–1.49 million triangles. Retopology is unavailable until the project deliberately adopts
  glTFast, so none of the generated meshes ships in `Resources`. The bounded procedural
  Hourstone remains the runtime implementation.

Rejected candidates move non-destructively to `GeneratedAssets/Hall/RejectedMaterials` and
`GeneratedAssets/Hall/RejectedFX` for comparison. They do not remain under `Resources`, silently
override a known-good procedural surface, or enter a mobile build.

All visual prompts share one invariant block:

> Obsidian Tower instrument outside time; cold iron structure; bone-white readable marks; living
> Sand is the only flowing or warm material; restrained stylized realism; strong silhouette and
> practical game readability. No parchment, tavern, medieval filigree, rainbow neon, sci-fi
> hologram chrome, characters, readable text, logos, or watermark.

Materials must be seamless and scale-neutral, with no hero crack or central focal feature. FX
sprites must be isolated on a removable flat background, avoid smoke/glass complexity, and remain
legible at 24–64 px. Meshes must be a single centered watertight prop with a stable base, compact
silhouette, no environment, no floating secondary pieces, and no text. Audio prompts specify the
physical source and action—not emotion alone—and keep frequent cues dry, short, and low-fatigue:
stone/ceramic ticks, Sand grains, iron seats, glass-mineral binding, dry stamp, stone release, and
one low seamless Tower bed. No voice, melody, UI beeps, casino sparkle, trailer boom, or combat
impact.

## Build order

### P0 — defects and tokens

- capture desktop, 16:10, wide phone, tablet, and safe-area baselines;
- create `LastHourTokens.uss`; migrate Hall colours/type/line weights;
- restyle scrollbars; fix dossier crop rules and every sub-13 handheld label;
- replace emoji with temporary vector glyphs using final dimensions;
- add highly visible focus brackets.

**Gate:** no overflow/clipping, no default Unity chrome, selected/focused/disabled never confused.

### P1 — reusable feedback foundation

- payload-bearing feedback events and target registry;
- motion recipe config/director and interaction manipulator;
- UI FX layer skeleton;
- audio/haptic interfaces with no-op implementations;
- expand Flow Lab to fire every recipe under mouse, keyboard/controller, touch, and reduced motion.

**Gate:** rapid route/select spam never leaves an element offset, invisible, double-focused, or
unresponsive.

### P2 — Table identity

- final station sigils and Table compass;
- obsidian/iron/bone surface pass;
- `Painter2D` rings, paths, focus brackets, and restrained Sand ambience;
- station hover/focus/attention/route sequences.

**Gate:** grayscale silhouette test still distinguishes every station; static reduced-motion
composition remains complete.

### P3 — transaction feel

- Buy, Reroll, Equip, Recruit/rank, Bind, and rejected-action recipes;
- exact number/delta transitions and persistent receipts;
- Rule Preview foundation with the first four spatial shapes;
- UI sound family and mobile haptic provider.

**Gate:** a muted capture communicates preview vs selection vs commitment and shows where the
acquired object went.

### P4 — result and route payoff

- result count/reveal choreography;
- station recommendation handoff;
- boss/terminal variants and fight-ender transition;
- Watch Again/Skip regression checks: no mutation and no duplicate reward feedback.

**Gate:** result is readable in under one second, skippable immediately, and replay-pure.

### P5 — real-device and performance tune

- actual phone/tablet safe-area and finger pass;
- mouse, keyboard/controller, touch, and rapid-input matrix;
- reduced motion and ambient-motion-off matrix;
- UI Toolkit Debugger/Frame Debugger pass: no animated layout properties, bounded overdraw,
  atlas healthy, no avoidable batch explosion;
- tune in Flow Lab with Jake; move only values, not code.

**Gate:** stable frame time, no accidental double actions, no unreadable physical text, and every
frequent interaction still feels good after fifty repetitions.

## Research-derived laws

- Motion should convey status and cause, stay brief, remain cancellable, and have a non-motion
  equivalent. Apple HIG: <https://developer.apple.com/design/human-interface-guidelines/motion>
- UI Toolkit animations should prefer translate/scale/rotate and use dynamic/group transform
  hints rather than animating layout. Unity:
  <https://docs.unity3d.com/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html>
- `Painter2D` is the supported retained-mode route for reusable vector geometry:
  <https://docs.unity3d.com/ScriptReference/UIElements.Painter2D.html>
- Focus must remain unmistakable and on-screen, including inside modals. Xbox XAG 113:
  <https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/113>
- Background motion around important text must be suppressible. Xbox XAG 117:
  <https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/117>
- Android recommends 48 dp touch targets and short, action-oriented haptics; long buzzy feedback is
  worse than none:
  <https://developer.android.com/develop/ui/views/haptics/haptics-principles>
- Into the Breach's animated spatial tooltips replaced explanations players did not understand:
  <https://www.gamedeveloper.com/design/-i-into-the-breach-i-dev-on-ui-design-sacrifice-cool-ideas-for-the-sake-of-clarity-every-time->
- Balatro's card lift/jiggle and synchronized audio demonstrate that a repeated mechanical action
  can remain pleasurable without hiding its numbers. The lesson is causal layering, not its
  casino aesthetic.
- The Bazaar is a useful density and tactile-shopping reference; Warband should borrow physical
  selection/transfer feedback, not its bright merchant theme:
  <https://interfaceingame.com/games/the-bazaar/>

## Implementation state — 2026-07-25

Jake approved the **obsidian Tower instrument / living Sand** direction and explicitly asked for a
deep reusable system with live debug tuning.

Built:

- shared Last Hour semantic theme tokens and dark cold-iron scrollbar treatment;
- one code-native `Painter2D` station-sigil family, replacing platform emoji in the Table;
- payload-bearing feedback events with source, target, amount, tone, and receipt;
- interruption-safe reveal, hover/focus, press, selection, attention, route, commit/transfer, and
  error recipes;
- identity-aware staggered card and blocking-choice reveals that do not replay on ordinary binds;
- one bounded `Painter2D` FX plane for pulses, route arcs, and travelling Sand grains;
- stable target registry and reusable interaction manipulator;
- reduced-motion substitutions, plus audio/haptic interfaces with no-op providers;
- dedicated F1 `UI FX` tuning tab with live nested recipe controls, Save/Reload, and six previews;
- corresponding F2 Flow Lab previews and a presentation contract check;
- a runtime 2.5D Hall world on an isolated camera layer: Tower chamber, obsidian Table, iron
  structure, living Hourstone, station sockets/channels, bounded Sand motes, interruptible
  station camera poses, and reduced-motion/quality controls;
- real optional outputs: a pooled semantic UI audio director, authored-clip resource hooks,
  synthesized development fallbacks, Hall ambience with commit ducking, Android short-pulse
  haptics, and conservative iOS fallback;
- authoritative purchase/reroll receipts with Sand ledger count transitions, causal transfer and
  acquisition paths, destination response, offer wipe, and bounded world/UI particles;
- a pinned dossier command dock so Buy/Equip/Bind never hides below scroll content on desktop or
  the phone side sheet;
- result-gate number counts and staggered death-cause reveals, with immediate reduced-motion
  substitutes;
- resource hooks for authored Hall materials and a normalized Hourstone mesh, retaining procedural
  fallbacks when assets are absent.
- the second-pass **physical station composition**: the old 96 px topbar + 118 px title band +
  54 px application-tab rail + permanent 42% dossier + 78 px footer are retired in favour of a
  60–64 px run ribbon, world-first station stage, compact spatial compass, and one pinned
  contextual action surface;
- data-first station presentation definitions (`hall-stations.json`) now own compass geography,
  motion verb, audio family, and camera pose without owning gameplay legality or economy;
- Market is the quality-bar slice: five dealt offers, one exact rule on-card, stable
  preview/selected/commit grammar, resulting-Sand action state, optional dossier sheet, centered
  ultrawide rail, and horizontal touch composition;
- Armory preserves a selected item as a distinct pinned state while a champion takes focus, then
  presents exact before/after deltas and Equip in the shared action tray;
- Warband and Hourstone use station-specific card proportions over the visible Hall rather than a
  generic split admin page; Breach remains a route into stakes-first Wager, not another workspace;
- full dossiers are progressive-disclosure blocking sheets, so their scroll is the only active
  scroll while open; card rails hide default scrollers and commits never move below content;
- route controls now play a short departing-Hall prelude, lock duplicate navigation, cancel by
  generation, and hand off only after the response begins, preventing effects from trailing over
  the destination;
- typed Hall action ids and exact disabled reasons replace the selection tray's stringly-typed
  command seam;
- landscape phone gets its own compact composition and portrait touch layouts receive a
  non-destructive rotate-device interstitial.
- the offer system now has typed fact ids and one centrally ordered four-fact profile per
  presentation kind, so adding a fact such as Mana per hit is a data/profile change rather than a
  card-layout fork;
- duplicate champions render as dedicated Rank Up offers: guaranteed rank/HP/power are separated
  from the blocking 1-of-2 specialization, and both exact paths show ADD / SWAP / DEEPEN plus
  their changed combat values in the dossier before purchase;
- weapons expose power, reach, cadence, Mana per hit, temper track, mastery audience, and exact
  mastery rule; trinkets and Inscriptions use the same generated mechanical language rather than
  flavour copy;
- purchases now return typed receipts. Inventory items have stable instance ids and retain all
  purchase/forge Sand investment through equip, unequip, resale, and starter-weapon swaps;
- the Armory exposes explicit Worn → Honed → Relic forge actions, act ceilings, exact costs, and
  full equipment deltas including worsened values;
- one semantic transaction vocabulary selects independently tunable Recruit, Rank, Weapon,
  Trinket, Inscription, Capacity, Rank Choice, Equip, and Reforge recipes. The F1 UI FX cockpit
  reflects every recipe and F2 Flow Lab can preview the important commits without mutating a run;
- exact mechanical copy is generated headlessly from authored selectors, conditions, effects,
  fields, status durations, stat rules, and signature patches. Unsupported primitives fail the
  contract instead of falling back to vague prose, while player-facing sentences say “this
  champion,” “basic-attack damage,” and explicit targets rather than engine terms such as owner,
  source, and cause.

Verification in this pass includes clean Unity compilation and runtime presentation-contract
validation plus full-size captures of the physical overview, Market, rank dossier, Armory item
pinning, Warband comparison tray, and forced landscape-phone Market. The offer contract measures
rule/footer containment, minimum mechanical type, title containment, metric-label containment, and
selection-overlay collisions. Captures caught and removed both inline card-detail overflow and the
phone Selected pill covering the fourth metric. The unrelated Unity AI PBR package can still emit
its pre-existing missing-`Serializable` warning.

Still in the broader P3–P5 pass: animated Rule Preview diagrams, real-device safe-area/finger
testing, final Inscription Bind staging, and final timing/audio feel tune from repeated live play.
