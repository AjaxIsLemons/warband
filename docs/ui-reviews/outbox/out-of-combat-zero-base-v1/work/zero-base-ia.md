# Warband out-of-combat UI — zero-base information architecture

Status: concept for review  
Date: 2026-07-27

## Premise

Design from the actions and commitments in the run, not from the existing Hall, station map, tabs,
or screenshots.

The player does four kinds of work outside combat:

1. **Understand** what just happened and what comes next.
2. **Build** the warband from owned heroes, equipment, and persistent laws.
3. **Choose** one irreversible offer, reward, specialization, or pressure level.
4. **Deploy** the chosen warband against a disclosed encounter.

Those become four reusable UI primitives. A system does not earn its own screen merely because it
has a name in the fiction or data model.

## Proposed run flow

```text
COMBAT
  ↓
RESULT
  ↓
WORKBENCH ── normal fight ──→ CHOICE GATE: WAGER ──→ DEPLOYMENT ──→ COMBAT
  │
  ├──────── interlude ──────→ CHOICE GATE: INTERLUDE ────────────→ WORKBENCH
  │
  └──────── boss ───────────────────────────────────→ DEPLOYMENT ──→ COMBAT

Boss victory:
RESULT → CHOICE GATE: INSCRIPTION REWARD → WORKBENCH
```

The Workbench is the only between-fight home. There is no separate Table, Market screen, Armory
screen, Warband screen, or Hourstone screen.

## Primitive 1 — Result

**Question:** What happened, and what changed?

Show:

- victory/defeat and exact Sand receipt;
- enemies defeated, top damage, and death causes;
- newly unlocked capacity or blocking reward;
- `Watch Again` and one contextual `Continue`.

Result is a blocking report over the frozen battlefield. It does not contain build tools.
`Continue` always names its destination: `Continue to Workbench`, `Choose Inscription`, or
`End Run`.

## Primitive 2 — Workbench

**Question:** What engine am I carrying into the next commitment?

This is one hero-first workspace with four stable horizontal bands:

```text
┌──────────────────────────────── RUN RIBBON ────────────────────────────────┐
│ Act / beat · Sand                     WORKBENCH                    WAGER → │
├──────────────────────────────── LIVE MARKET ───────────────────────────────┤
│ Five offers visible together · Hold · Reroll                              │
├──────────────────────────────────────────────────────────────────────┬──────┤
│ HERO FOCUS PREVIEW · full composed combat/build dossier              │ARMORY│
│ hover/focus reveals · click/tap pins · projected state while testing │ · 8  │
├──────────────────────────────────────────────────────────────────────┴──────┤
│ PERMANENT WAR BAND RAIL · hero portraits · WEAPON / TRINKET sockets       │
└─────────────────────────────────────────────────────────────────────────────┘
```

The R4 composition removes the command-hint footer, giant forward button, top-right law chips,
and Market / Owned Gear / Laws navigation. The expanded Market and Armory are mutually exclusive
work modes: Market is open by default; opening the bounded Armory drawer collapses Market to one
summary bar. The hero preview and permanent rail remain in both modes. The only global action is
the compact, specific next commitment in the run ribbon.

### Permanent War Band rail

The bottom rail is the one persistent representation of owned heroes across the out-of-combat
system. It always shows:

- active lineup first, reserves second;
- field capacity and two reserve sockets;
- rank, path, weapon, trinket, and unresolved-choice markers;
- an empty field slot when capacity exceeds the active lineup;
- one selected hero at a time.

Its permissions change with the run task:

- **Workbench:** fully interactive equipment target. An owned weapon/trinket may be dragged to a
  compatible socket or selected and followed by a socket selection. Occupied sockets swap or
  return the displaced item to the Armory atomically.
- **Choice Gate:** read-only context. It never permits management during an irreversible choice.
- **Deployment:** lineup/formation target. Equipment remains read-only or editable according to
  the final post-Wager gameplay law.
- **Result:** read-only snapshot when shown; the result itself remains the primary task.

The rail is not a second inventory. Equipment sockets show current identity and location, while
the Armory drawer provides the complete item collection on demand. It replaces the old left
Warband column and every duplicate roster summary.

Hovering or focusing a hero opens that hero's complete dossier in the stable center preview.
Clicking or tapping pins it so the player can inspect rules or operate the Armory without losing
context. Moving pointer focus alone must never be required: controller focus opens the same
preview, and touch uses first tap to inspect and a deliberate second action to commit.

### Live Market shelf

All five current offers are visible simultaneously in one shallow shelf:

- three Recruit offers and two Workshop offers;
- exact price and Hold state;
- one 1-Sand Reroll action; and
- selection populates the same selected-object canvas used by owned objects.

There is no Market destination or source-navigation control.

Opening the Armory collapses this shelf to a single `MARKET · 5 OFFERS` summary bar. Reopening the
Market closes the Armory and restores all five offers. This task-level exclusivity preserves
comfortable exact-rule type at 1280×720 without introducing another screen.

### Collapsible Armory drawer

Collapsed is the default. A narrow `ARMORY · 8` handle keeps item count, new-item attention, and
the affordance to open visible without taking the center workspace away from the hero. Opening
the drawer reveals every unequipped and equipped weapon/trinket with a location badge such as
`Bulwark`, `Reserve`, or `Stored`.

The open drawer is an opaque, bounded right-side region rather than a modal. At first-playable
scale it uses a fixed 2 × 3 grid. Growth uses explicit `‹ PAGE 1 / 3 ›` controls and filters,
never a scroll container. Page changes restore focus predictably; closing the drawer returns
focus to its handle; hidden item pages and the closed drawer are removed from focus order.

Selecting an item wakes only compatible sockets in the permanent hero rail:

1. select or drag an owned item;
2. compatible sockets gain quiet blue target brackets;
3. focusing a socket renders that hero's complete projected post-equip dossier;
4. release/confirm performs the authoritative equip, transfer, or swap; and
5. the displaced item remains visible in the Armory with its new location.

Selection never commits, and a weapon can never target a Trinket socket.

### Hero focus preview and full hero card

The default center is a stable full hero card, not a transient tooltip and not a side-by-side
comparison. It answers “what is this unit, as currently composed?” in one place:

- identity, portrait, class, rank, path, role, active/reserve state;
- full composed combat stats, including movement/targeting facts when they differ by chassis;
- Weapon and Trinket;
- exact Basic Attack, Signature with Mana threshold, Passive, and mastery grammar;
- selected specialization nodes and run modifiers that alter this hero; and
- unresolved choices or illegal-loadout reasons.

At the 1280×720 floor, the first page contains every combat-critical fact. A second fixed
`BUILD` page may hold the complete specialization lineage and secondary provenance. There is no
scrolling inside either page. Hover/focus opens the card, click/tap pins it, and closing or
unpinning restores focus to the invoking hero in the permanent rail.

Equipment preview renders the complete projected hero after the proposed equip. Fields that
would change receive restrained blue preview markers and inline deltas; the old and new heroes
are never duplicated. A subordinate `CHANGES` strip is limited to three to five facts and one
qualitative rule swap. This keeps the player oriented around the resulting hero rather than
making comparison accounting the primary object.

The same center remains adaptive when the player is not inspecting a hero:

- **Market offer selected:** identity, exact rule, price, resulting Sand, and acquisition result.
  Equipment offers may project onto the pinned hero.
- **Owned item selected:** current location, compatible hero targets, exact before/offered
  projected hero, Equip/Transfer/Unequip/Sell, and Reforge when legal.
- **Inscription selected:** exact run-wide law, trigger family, counter state if any, and owned
  synergies. It has no Equip action because all owned Inscriptions are always active.
- **Nothing selected:** a compact engine summary and attention queue.

The canvas never shows the same facts in a stat strip, a comparison column, and Lose/Gain boxes.

Owned Inscriptions do not need a permanent top-right navigation rail. The selected-object canvas
can open an Hourstone/Laws dossier from an acquisition receipt or a compact run-summary object;
because all owned laws are always active, this remains inspection rather than another management
mode.

### Acquisition handoff

Buying does not navigate:

- Recruit → appears in the first legal War Band rail socket, or Buy is disabled with the exact
  capacity reason.
- Weapon/Trinket → appears in the Armory with `NEW`; compatible sockets wake in the permanent
  hero rail.
- Inscription → binds to the Hourstone and opens its selected-object receipt; it does not create
  another navigation destination.
- Duplicate Recruit → opens the blocking specialization Choice Gate in place; no other transaction
  is available until it resolves.

### What this removes

- No station navigation.
- No duplicate Warband presentation alongside the permanent hero rail.
- No separate Armory screen; inventory is a bounded Workbench drawer.
- No separate Hourstone destination for a read-only collection.
- No top-right system navigation or bottom command-hint/action bar.
- No ordinary tabs inside an item decision.
- No scrolling page or dossier. Long dossiers use named fixed pages with a pinned Close/Back.

## Primitive 3 — Choice Gate

**Question:** Which irreversible branch am I taking?

One shared full-screen grammar handles:

- Stable / Fraying / Collapsing wager;
- Treasury / Armory / Hourstone Interlude;
- rank specialization;
- boss Inscription reward; and
- any later one-of-three event.

The shell contains:

- a short decision title and consequence statement;
- two or three equal choices;
- exact differences that are currently knowable;
- one selected choice;
- a specific commit verb such as `LOCK WAGER`, `TAKE REWARD`, or `CHOOSE PATH`;
- Cancel/Back only when the run rules allow it.

The gate does not carry inventory, roster management, or station navigation. It preserves context
with the run ribbon and a small read-only warband summary.

## Primitive 4 — Deployment

**Question:** Where does this already-built warband begin the disclosed fight?

Show only:

- exact enemy formation and encounter rule;
- the 3D board;
- active lineup and two reserves;
- selected friendly/enemy inspector;
- legal placement, range, and relationship overlays;
- restore/reset/undo/redo; and
- `BEGIN FIGHT`.

Economic actions are absent. Selection never commits. The presentation may allow only the
lineup/loadout edits the final gameplay law explicitly permits after Wager; it must not silently
create a second Armory.

## Opening draft

The opening `choose three of five` uses the Choice Gate grammar in a multi-pick variant:

- five fixed cards;
- three numbered selection sockets;
- exact rules on selection;
- `BEGIN EXPEDITION` after exactly three legal picks.

It is onboarding, not a fifth permanent navigation surface.

## Information and action inventory

| Player intent | Authoritative action | Surface |
|---|---|---|
| Inspect full owned hero roster | Read field + bench | Workbench |
| Inspect every owned item, including equipped | Read inventory + hero loadouts | Workbench / Armory drawer |
| Compare and equip an item | Equip, transfer, unequip | Workbench |
| Reforge an equipped weapon | Reforge | Workbench |
| Sell an item or hero | Sell | Workbench with confirmation |
| Recruit or rank up | Buy offer, then resolve spec | Workbench → Choice Gate |
| Buy equipment or an Inscription | Buy offer | Workbench |
| Hold or reroll stock | Toggle Hold, Reroll | Workbench / Market |
| Buy field capacity | Buy capacity when unlocked | Workbench |
| Inspect all persistent laws | Read Inscriptions | Workbench / Laws |
| Choose an Interlude path | Resolve Interlude | Choice Gate |
| Choose a boss reward | Choose reward | Choice Gate |
| Choose encounter pressure | Lock Wager | Choice Gate |
| Inspect disclosed encounter | Read brief/enemies | Deployment |
| Change formation | Planning actions | Deployment |
| Commit the fight | Begin Fight | Deployment |
| Understand/replay outcome | Read stored BattleResult | Result |

## Fixed-page and zero-scroll contract

- Baseline: 1280×720. Also verify 1920×1080, 1920×1200, 2560×1080, and 3840×2160.
- No `ScrollView`, mouse-wheel dependency, clipped overflow, shrinking text, or hidden action dock.
- The Armory drawer uses explicit pages; the maximum visible item count is a design token.
- Long exact rules use two named pages at most: `RULE` and `DETAILS`. The commit action does not
  move between pages.
- Inscription collections use pages and family filters; 24 owned laws must remain navigable
  without reducing badge size.
- Body text is at least 16 logical px; touch targets are at least 56 logical px.
- Every page exposes its count and current location: `WEAPONS · 8 OWNED · PAGE 1 / 2`.
- Hidden pages are removed from focus order. Closing a dossier or gate restores focus to its
  invoker.

## State grammar

- **Focus:** steel/Tower blue bracket and surface weight.
- **Selected:** stable blue socket/check; visually distinct from focus.
- **Recommended/new:** Sand notch plus text label.
- **Commit:** Sand button with a specific verb.
- **Projected change:** blue field marker plus a compact signed delta; never a second hero column.
- **Disabled:** ash plus exact reason; no hover lift.
- **Equipped/location:** socket icon and owner label, not a selection color.

## Architectural consequence

Implement the four primitives as screen/view contracts around immutable view models:

- `ResultView`
- `WorkbenchView`
- `ChoiceGateView`
- `DeploymentView`

Workbench sources, the hero dossier, and the Armory drawer are composable modules, not navigable
screens.
The navigation stack therefore follows the run state rather than every data category. New item
families extend a source and a detail presenter; they do not add another station or full-screen
destination.

## Questions this concept intentionally exposes

1. Is loadout editing permitted after Wager when the exact encounter is revealed, or is
   Deployment strictly formation-only? The sources currently point both ways; the visual system
   can support either, but the gameplay law must be explicit.
2. Does an equipped weapon reforge from the selected item canvas, selected hero canvas, or both?
   The action is the same; playtesting should choose the faster discovery path.
3. At what owned-item count does paging need family filters or a compact search affordance?
4. Should the Workbench default to the Market after every ordinary victory, or to a neutral
   engine summary with `Market refreshed` attention?
