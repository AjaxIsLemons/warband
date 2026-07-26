# Encounter planning workspace — v0.2

**Status:** interaction source of truth; direction accepted by Jake on 2026-07-24 after
comparative research and planning, then simplified to one combined Planning phase. The
transactional foundation and bonded-pair Unity proof are live as of 2026-07-24; run persistence,
consumable content, services, overlays, and the wider act remain staged work. Technical extension
contract: `Projects/planning-system.md`.

## Decision in one page

Warband should use a **board-first Planning workspace**:

- An encounter reveal introduces the pressure, then the same battlefield, enemy formation,
  and camera remain on screen through Planning and Play.
- The player's last committed formation auto-populates when the encounter opens. Persistence
  is a convenience, never an automatic strategic recommendation.
- The run keeps its current **two-slot bench for the first playable**, but bench capacity is
  a data-driven run rule that may grow. The bench is game state: owned heroes who are not in
  the active lineup.
- The bench appears through a compact, collapsible **Muster Drawer** at the bottom of the
  battlefield. The drawer is presentation, not a new system or a larger roster.
- Planning allows lineup, equipment, formation, and service decisions in any order. Dragging
  a reserve hero onto a fielded hero swaps them and gives the newcomer the outgoing hero's
  hex; fielded heroes may move freely among legal player hexes at the same time.
- `BEGIN FIGHT` is the one commitment. It validates and locks lineup, loadouts, and formation
  together.
- Both drag/drop and click-select/click-place perform the same commands. Drag is never the
  only way to play.
- All free Planning edits are reversible until Play. Economic actions are not part of that
  undo history.

This deliberately combines three useful patterns instead of copying one game's screen:

1. a persistent battlefield, because formation is the core decision;
2. a small reserve, because PvE encounter adaptation is part of the run; and
3. a contextual drawer, because inventory and roster controls should not permanently obscure
   the encounter.

**Bench, drawer, and formation persistence solve different problems.** Warband wants all
three. The bench creates a roster choice, the drawer exposes that choice without becoming
the game board, and persistence removes repeated busywork.

## Why this fits Warband

Planning is where the player breaks the game on purpose. The surface has to support
questions such as:

- Which heroes answer this encounter's pressure?
- Which owned weapons and trinkets create the strongest interaction?
- Which unit should receive the engine piece?
- Who wants to take the opening contact, and who needs time to pop off?
- Does the enemy formation reward a line, pocket, flank, spread, or sacrifice?

Those questions should be answered against the actual encounter, not in a detached
spreadsheet screen. Lineup, equipment, and formation are different kinds of edits, but they
all answer one question: **what warband am I sending into this fight?** Separating them into
locked phases adds ceremony without adding a strategic commitment.

Planning therefore keeps every free edit available until `BEGIN FIGHT`. Clarity comes from
contextual focus—selecting a hero, reserve, item, or hex changes the relevant targets and
overlays—not from restricting unrelated actions.

## Comparative research

The useful comparison is not “which game has a bench?” but **why its bench or storage
exists**.

| Game | Established pattern | What creates the need | Lesson for Warband |
| --- | --- | --- | --- |
| [Teamfight Tactics](https://teamfighttactics.leagueoflegends.com/en-us/index/) | A revolving roster is drafted and deployed; purchases go to a large permanent bench, and a full bench blocks buying ([mechanics reference](https://tft.ninja/guides/game-mechanics/the-shop)). | Rapid shops, duplicate hunting, speculative purchases, selling, and round-by-round roster churn. The bench is an economic buffer as much as a lineup tool. | Do not copy the large always-visible bench. Warband's sticky heroes and two reserves do not create TFT's transactional storage problem. |
| [Guildrun](https://www.playguildrun.com/) | The board begins at three and reaches five while the owned roster reaches six, guaranteeing at least one reserve; reserve heroes can also carry Backup effects ([community data reference](https://guildrun.wiki/systems/glossary/)). | A deliberately small roster still needs an active-versus-reserve choice every fight. | This is the closest structural comparison. A small reserve is enough to make PvE adaptation real without turning roster management into warehouse management. Reserve effects are extra complexity and should not be copied initially. |
| [Mechabellum](https://www.mbxmas.com/) | The army is understood spatially on the battlefield; deployment and targeting knowledge determine how the automatic battle unfolds ([official wiki](https://wiki.mbxmas.com/mechanics/targeting/)). | The player's primary work is reading the opposing field and creating a counter-formation before a hands-off battle. | Keep the battlefield as the main preparation context. Warband should restore prior positions as a convenience, although unlike Mechabellum it keeps repositioning free between encounters. |
| [Backpack Battles](https://playwithfurcifer.github.io/backpack-battles-presskit/) | Direct arrangement is the game. As inventories became denser, the developers added a bottom toolbar, undo/redo, separate edit modes, push-to-storage, and a grid storage view ([official update](https://steamcommunity.com/app/2427700/announcements/?l=english)). | Spatial manipulation becomes error-prone and tiring as content and combinations grow. | Direct manipulation needs recovery, alternate inputs, explicit modes, and scalable storage from the beginning. Undo and click alternatives are not optional polish debt. |
| [Super Auto Pets](https://superautopets.wiki.gg/wiki/The_Basics) | A fixed five-pet team is bought, rearranged, replaced, and committed directly; there is no separate reserve roster in the core team surface. | The owned roster and active team are effectively the same thing. | “No bench” works only if every owned hero is fielded. That would remove Warband's intended encounter-to-encounter lineup adaptation. |
| [Astronarch](https://store.steampowered.com/app/1234940/Astronarx/) | A small party is positioned and rebuilt with item combinations between automatic fights. | The roster is compact enough that the formation can remain primary while character details are contextual. | Warband can keep the hero inspector secondary to the board instead of becoming a full-screen roster menu. |

The resulting direction is intentionally closer to **Guildrun's small reserve plus
Mechabellum's board-first thinking**, with Backpack Battles' manipulation safeguards. It is
not miniature TFT.

## Vocabulary and ownership

Use these terms consistently in design and code:

- **Owned roster:** every hero currently owned by the expedition.
- **Active lineup:** the heroes who will enter the next fight, limited by current Field Slots.
- **Bench:** the inactive owned-hero slots, whose capacity comes from the run rules. Benched
  heroes do not fight and initially provide no passive effects.
- **Formation:** a mapping from active hero instance ids to legal starting hexes.
- **Muster Drawer:** the collapsible UI surface that exposes the bench and lineup tools.
- **Armory:** the contextual owned-equipment surface inside Planning.
- **Planning Draft:** the reversible lineup, equipment, and formation proposal from which
  the next battle will be built.

Do not call the drawer itself “the bench.” That distinction prevents UI layout decisions
from leaking into run rules.

The current `RunConfig` values remain the first-playable tuning:

- three starting Field Slots;
- six maximum Field Slots; and
- two Bench Slots.

The bench size is provisional tuning. Every model, command, validator, view, and layout must
read its capacity from data—never compare against a literal `2`. A future reward, difficulty
rule, run upgrade, or baseline revision may change reserve capacity without requiring a new
screen or command type.

## Encounter reveal, plan, play, result

The world does not cut to a different room after the encounter appears. Camera framing,
enemy positions, the player formation, the Hourstone rail, and encounter inspection remain
stable.

| State | Primary question | Player interaction | Muster/Armory | Enemy | Commitment |
| --- | --- | --- | --- | --- | --- |
| **Encounter Reveal** | What is this encounter asking? | Last formation is already visible; the brief foregrounds the new pressure. | Collapsed, with a visible handle and reserve count. | Exact formation, rules, roles, and mechanics inspectable. | Dismiss or interact to enter Planning; this is not a build lock. |
| **Planning** | What warband am I sending into it? | Lineup, equipment, and formation may all change in any order. | Available and freely collapsible; economic services may open contextually. | Remains visible and inspectable. | `BEGIN FIGHT` validates and commits the complete draft. |
| **Play** | What did my decisions create? | Inspection only; all edits are locked. | Closed. | Rules and unit details remain inspectable without editing. | Combat ends in Result. |
| **Result** | What happened, and what do I change next? | Outcome and relevant review are inspectable. | Closed until returning to Planning. | Fight result remains legible. | Continue, retry, or leave according to the run rule. |

A permitted retry or continuation from Result returns to **Planning** with the last committed
lineup, loadouts, and formation restored.

### Contextual focus inside Planning

Planning has one permission set but several temporary interaction contexts:

- No selection emphasizes the encounter and the current complete formation.
- A fielded hero emphasizes legal movement hexes, other fielded heroes, compatible equipment,
  and available bench destinations.
- A reserve hero emphasizes vacant active slots and fielded heroes it may replace.
- An item emphasizes compatible heroes or equipment slots and previews the resulting deltas.
- An enemy emphasizes its rules, range, targeting, and encounter relationships without
  changing the player's draft.

These contexts guide attention; they do not become modes the player must enter or exit.
Cancel returns to the neutral Planning context. The only commitment button is
`BEGIN FIGHT`, never generic `NEXT` or `READY`.

## Formation persistence

### Default law

Entering an encounter restores the **last committed formation by hero instance id**. It must
never recreate formation from roster list order.

Restoration follows a deterministic sequence:

1. Keep every still-active hero on its previous legal, unoccupied hex.
2. When a deliberate field/bench swap occurred, give the incoming hero the outgoing hero's
   hex.
3. If an encounter changes the legal deployment shape, move invalid heroes to the nearest
   legal unoccupied hex using a stable tie-break.
4. If a newly opened Field Slot or a newly recruited active hero has no inherited hex, place
   them in the first open hex of a neutral authored fallback pattern.
5. Mark every system-adjusted or newly placed hero when Planning begins.

The fallback must not pretend to understand the build. No hidden “tank goes in front” or
“ranged goes in back” heuristic should override the player. The starting party may have one
authored default formation, but subsequent persistence belongs to the player.

### Player controls

Planning should provide:

- `RESTORE LAST` — restore the formation committed in the previous fight;
- `RESET` — restore the current encounter's deterministic entry arrangement; and
- undo/redo for formation moves.

An automatic `BEST FORMATION` button is out of scope. Formation is a core decision, and the
game should not imply it can solve that decision.

### When geometry changes

If an encounter removes, blocks, or reshapes player deployment hexes:

- restoration remains deterministic;
- affected heroes receive a visible “formation adjusted” marker;
- the encounter preview explains the deployment rule;
- no hero is silently benched because its old hex is illegal; and
- `BEGIN FIGHT` remains disabled until every active hero has a legal hex.

## The Muster Drawer

### Collapsed state

The collapsed drawer is a shallow bottom rail rather than a hidden hamburger menu. It shows:

- `MUSTER`;
- `active / field cap`;
- `reserve / bench cap`; and
- small portrait pips for occupied reserve slots.

It may auto-peek when a new hero enters the bench, when a swap is recommended by a tutorial,
or when the draft is invalid. It should not repeatedly pulse merely because a reserve exists.

### Open state

The drawer opens upward only far enough to reveal the roster task. It contains:

- reserve sockets generated from the current bench capacity;
- a compact active-lineup summary;
- the selected hero's identity, rank, path, and current loadout;
- an `ARMORY` tab or contextual equipment strip; and
- clear free-versus-economic action language.

The board remains readable above it. Opening the drawer should reframe or pan the battlefield
into the remaining safe area rather than simply covering the front player rows.

The drawer is not a permanent card wall and should not attempt to show full rules text for
every hero and item simultaneously. Selecting a hero opens one contextual inspector anchored
to the drawer or screen edge. Selecting an enemy continues to use the encounter inspector.

### Empty and full states

- An empty reserve socket is still visible while the drawer is open so capacity is obvious.
- A full bench says `RESERVES FULL`; it does not wait for a failed purchase to reveal the rule.
- If the active lineup is under cap, a visible vacant active socket explains that another
  hero can be fielded.
- If all owned heroes are active, the collapsed handle may omit portrait pips but remains
  accessible for Armory and lineup inspection.

### Future scale

The drawer must render from data and support:

- a changed bench capacity;
- six fielded heroes plus two reserves;
- more equipment slots;
- status markers such as new choice, specialization available, or invalid loadout;
- controller focus and touch targets; and
- service-specific subviews without rebuilding the battlefield.

It should not assume the current proof's three heroes or three authored weapon buttons.

For the first-playable capacity, all reserve sockets fit in one row. At larger capacities the
drawer should preserve socket size and board readability through horizontal scrolling,
paging, or a compact expanded roster view chosen through playtesting. It must not shrink
portraits and hit targets indefinitely or grow upward until it covers the encounter. The
interaction commands remain identical regardless of which layout presents the sockets.

## Direct manipulation rules

Every manipulation is an atomic command. The model changes only after a legal destination is
confirmed; a dragged visual is never the authoritative hero.

### Planning

| From | To | Result |
| --- | --- | --- |
| Bench hero | Fielded hero | Swap them. Incoming hero inherits the outgoing hero's provisional hex; outgoing hero occupies the source bench socket. |
| Fielded hero | Empty bench socket | Bench the hero if the remaining lineup is legal; the vacated formation hex is retained as the preferred next active slot. |
| Bench hero | Vacant active socket | Add the hero to the lineup and give them the socket's provisional formation anchor. |
| Fielded hero | Empty legal player hex | Move the hero. |
| Fielded hero | Another fielded hero | Swap their formation hexes. |
| Fielded hero | Occupied bench socket | Swap active and benched heroes; the incoming hero inherits the outgoing hero's hex. |
| Fielded hero | Illegal, enemy, or out-of-zone hex | No mutation; return to origin with a readable invalid tell. |
| Equipment | Compatible hero or equipment slot | Preview, then equip. The displaced item returns to owned Armory in the same atomic command. |
| Equipment | Incompatible hero or slot | No mutation; explain the exact incompatibility. |

Dropping a reserve hero onto a fielded body is the fastest common swap. The active summary
provides a precise alternative when world-space bodies overlap or are hard to target.

### Click and controller equivalents

Drag is a shortcut over a shared selection model:

1. click/tap a hero or item to select it;
2. show legal destinations and relevant comparisons;
3. click/tap a destination to issue the same command as a drop; and
4. click the source, press Cancel, or select another object to cancel.

Keyboard/controller navigation uses the same destinations. Double-click and an explicit
`TO BENCH` / `TO FIELD` action may move a hero to the first legal socket, but must not invent a
strategic formation.

### Drag feedback

After crossing a small movement threshold:

- the source keeps a ghost silhouette;
- the carried representation rises and follows the pointer;
- legal targets highlight by both shape and color;
- hovering a swap target previews both destinations;
- equipment hover previews the affected stats, weapon range, attack pattern, and relevant
  mastery rider on the board; and
- releasing outside any target returns the object to origin without changing state.

World and UI dragging must share one pointer owner. Crossing between the UI drawer and the
3D battlefield cannot create duplicate clicks or leave the pointer captured.

## Inspection and information hierarchy

The board answers **who and where**. Context panels answer **what and why**.

### Selected friendly hero

The compact inspector should prioritize:

1. name, class, rank, path;
2. current weapon and trinket;
3. attack range/pattern and signature summary;
4. the stats altered by the hovered replacement;
5. current formation relationships; and
6. full rules text on deliberate expansion.

Equipment comparison should use deltas and preview overlays, not two walls of numbers.
Changing a weapon should eventually change its readable world prop, but the interaction
cannot depend on art being finished.

### Selected enemy

Enemy inspection stays available throughout Planning and Play. It shows:

- role and relevant stats;
- attack, signature, passives, targeting, and statuses;
- disclosed encounter relationships such as Bond; and
- range, opening-target, or area overlays when requested.

Player drag targets and enemy inspect targets must have distinct hover treatment.

### Global information

- Encounter rule and risk tier live in a stable upper/side strip.
- Hourstone Inscription badges remain at the top and are not moved into the drawer.
- Currency and economic services remain visible during Planning but are visually subordinate
  to the active task.
- Full roster/loadout details should never cover the encounter rule that motivated the edit.

## Undo, validation, and commitment

### Undo scope

The Planning undo history includes only free, reversible draft commands:

- field/bench changes;
- equip/unequip/swap;
- formation moves and swaps; and
- `RESTORE LAST` / `RESET`.

Purchases, sales, forging, paid respec, reward choices, and other economic mutations are not
smuggled into the same undo stack. Those services require their own confirmation, buyback, or
explicit rule.

Undo history is cleared when the fight is committed, the encounter changes, or an economic
action invalidates the relevant draft history.

### Validation

Before `BEGIN FIGHT`:

- active count is within the current Field Slot limit;
- bench count is within capacity;
- every active hero has a legal complete loadout;
- every owned item exists in exactly one legal location; and
- all required rank/path decisions are resolved;
- every active hero occupies one unique legal hex; and
- the encounter preview has no unresolved mandatory choice.

The primary button explains the first unresolved error and focuses it. An invalid press never
silently fixes the lineup.

## Interaction architecture

The implementation should preserve the current pure-simulation boundary.

### Pure draft model

The host-agnostic foundation now lives in `sim/Warband.Run/Planning.cs`:

```text
PlanningDraft
  Capacities
  Heroes (instance id, content id, zone, bench slot, preferred hex, loadout slots)
  Resources (finite owned Planning resources)
  Intents (content-owned effects queued for commit)

PlanningSession
  Current (isolated read snapshot)
  Execute(IPlanningAction)
  Undo / Redo
  ValidateForCommit / Commit

PlanningRules
  Position legality
  Loadout legality
  Resource-target legality
  Content validation
```

All identity is by stable hero and item instance id. UI element order and GameObject instance
ids are never saved as run state.

Free edits are atomic commands:

```text
MovePlanningHero
SwapFieldBench
MoveHeroToBench / MoveHeroToField / MoveBenchHero
SetLoadoutOption
UsePlanningResource
```

Each action edits a clone; the session validates the candidate before replacing current state.
Snapshot history gives new free actions safe undo/redo without bespoke inverse logic. Invalid
actions cannot partially mutate state. `Current` and successful commit results are clones, so a
host or UI cannot bypass actions by mutating session-owned state.

`BEGIN FIGHT` calls `PlanningSession.Commit()`. A successful commit returns the exact validated
snapshot used to compose the deterministic battle and clears Planning history. In the future run
host, that same boundary persists lineup/loadout/formation and applies queued resource intents
exactly once. The Planning library deliberately does not mutate authoritative `RunState`.

Already-owned consumables belong in the reversible draft: provisional quantity decreases and a
typed, parameterized `PlanningIntent` is queued; Undo restores both. Purchases, sales, forging,
and paid respec mutate authoritative run/economy state through their own transaction, then
explicitly reconcile the draft and invalidate stale history. They are never disguised as a local
undoable action.

### Unity presentation

The live proof uses:

- one explicit Planning/Playing/Result state machine after the encounter is revealed in place;
- a board interaction controller whose available targets come from the current selection
  context and Planning draft;
- UI Toolkit for the Muster Drawer, Armory, inspectors, rule strip, and commitment buttons;
- world-space or camera-space picking for units and hexes through one picking gateway;
- a formation overlay for legal hexes, source ghosts, range, relationships, and drop targets;
  and
- a presentation-only animation layer driven by accepted commands.

The model/view/input split must let polish change drawer motion, selection rings, sounds, and
camera response without rewriting run commands.

### Suggested component seams

Current seams:

- `PlanningSession` — pure draft, validation, history, and commit.
- `SkirmishPlanningRules` — authored legality adapter for the proof.
- `SkirmishController` — flow plus UI/world intention-to-action adapter.
- `SkirmishScreen` — UXML-backed Muster/Armory/inspector view.
- `ReplayPlayer` — board projection, picking, snapshot rendering, selection marker, and replay.

As complexity grows, extract board interaction, formation overlays, and input routing from
`SkirmishController` before adding parallel rule paths. Extraction must retain the same
`IPlanningAction` boundary.

Views should receive immutable view data and emit intentions. They should not own bench rules
or mutate `RunState`.

## Polish ladder

The system should be designed for polish now but earn that polish in layers.

### Pass 1 — interaction proof

- Same battlefield across Reveal, Planning, and Play.
- Previous formation restoration by stable hero id.
- Capacity-driven Muster Drawer, initially tuned to two slots.
- Simultaneous lineup, equipment, and formation editing.
- One validated `BEGIN FIGHT` commitment.
- Click-select/click-place and basic drag/drop.
- Atomic swaps, invalid-return behavior, and complete validation.
- Current three-hero/three-weapon proof driven through scalable data rather than hardcoded
  buttons.

Exit criterion: a new player can swap one reserve, change a weapon, reposition the lineup,
and begin a fight without verbal instruction.

### Pass 2 — clarity and recovery

- Undo/redo and restore/reset.
- Range, target, relationship, and equipment comparison overlays.
- Drawer safe-area camera reframing.
- Clear contextual targets and selection cancellation.
- Controller navigation and non-drag equivalence.
- Empty/full/new/invalid states.
- Inspect enemy while an edit is pending.

Exit criterion: mistakes cost seconds, not confusion, and changing task never requires
finding or unlocking a different edit mode.

### Pass 3 — tactile polish

- Drawer spring/motion language and selected-object lift.
- World source ghosts, swap arcs, landing motion, and formation-adjusted tells.
- Equip audio/visual identity and eventual weapon-prop changes.
- Distinct valid, invalid, swap, lock, and commit sounds.
- Contextual camera focus that never loses the enemy formation.
- Reduced-motion setting and scalable animation timings.

Exit criterion: repeated between-fight manipulation remains satisfying rather than merely
functional.

### Pass 4 — run-scale tools

- Market, Armory, Hourstone rewards, and post-boss continuation integration. **Market,
  Armory, visible Interlude/boss choices, and capacity data are live in ADR 0019; forge and
  paid respec remain later services.**
- Changed Field/Bench capacities from data. **Live for Field; bench remains the first-playable
  two-slot constant.**
- Larger equipment catalogs, filters, and comparison shortcuts.
- Touch refinements, screen-aspect stress, localization, and accessibility pass.
- Lightweight telemetry for phase time, drawer opens, lineup swaps, formation moves, invalid
  drops, undo use, and fights begun without changes.

Exit criterion: adding heroes, items, services, or encounters does not require redesigning
the preparation shell.

## Accessibility and input contract

- No action is drag-only, hover-only, color-only, or dependent on animation.
- Legal and illegal destinations differ by icon/shape as well as color.
- Selection and inspector state remain stable when switching input devices.
- Touch targets remain usable when the drawer is dense.
- Controller focus order follows encounter → board → drawer → primary commitment, with
  explicit shortcuts back to the board and enemy rule.
- Tooltips can be pinned; essential rules do not disappear when the pointer moves.
- Reduced motion shortens or removes drawer bounce, object lift, and camera response.
- There is no preparation timer.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Combined editing makes the board feel busy or ambiguous. | Selection-driven destinations, one active drag owner, clear source ghosts, contextual overlays, and Cancel returning to a neutral Planning state. |
| A bottom drawer hides the player's front rows or grows badly with more reserves. | Reserve a UI safe area, reframe the camera while open, and switch larger data-driven capacities to scrolling/paging rather than unbounded height or shrinking sockets. |
| A collapsed bench is forgotten. | Show occupancy pips, auto-peek on a new reserve or invalid lineup, and teach the first swap through play. |
| Restoring the last formation encourages autopilot. | Preserve convenience but foreground encounter-specific pressure; never claim the persisted layout is recommended. |
| Drag/drop becomes fiddly as the board gets busy. | Click-place parity, active-summary targets, generous hit regions, source ghosts, and atomic return. |
| Equipment and roster detail overwhelms the encounter. | One selected hero at a time, progressive disclosure, comparison deltas, and stable enemy/rule access. |
| Future reserve passives become hidden active mechanics. | Bench heroes are mechanically inactive initially. If Backup-style effects are ever authored, surface them permanently rather than relying on an open drawer. |
| Economic actions and free undo create exploits. | Keep economic mutations outside the preparation command history and reconcile explicitly. |

## Rejected first directions

### Full-screen roster/loadout page

This loses the exact enemy formation while the player makes the decisions intended to answer
it. It also duplicates unit presentation and makes returning to the board feel like context
switching.

### Permanent TFT-style bench row

Warband has only two reserves and does not need a constant transactional buffer for buying
duplicate units. A permanent row spends screen area on an occasional decision and visually
competes with the battlefield.

### No bench

This makes owned roster equal active lineup, removes a major axis of PvE adaptation, and
conflicts with the existing 3→6 field plus two-reserve run structure.

### Separate Prepare and Deploy gates

Locking loadouts before allowing movement creates an extra confirmation without adding a
meaningful risk or irreversible decision. It also prevents natural iteration such as moving
a hero, noticing a range problem, changing their weapon, and adjusting the formation again.
Contextual targets provide clarity while one final Fight commitment preserves safety.

### Smart automatic formation

Role-based auto-placement would be wrong for many deliberately broken builds and would
quietly solve part of the game. Restore the player's choices, not the game's opinion.

## First playtest questions

Test these before adding shop/forge complexity:

1. Do players notice and understand the collapsed Muster Drawer without it nagging them?
2. Does dragging a reserve directly onto a fielded hero read as an atomic swap?
3. Is inheriting the outgoing hero's hex the expected result?
4. Can players freely alternate between formation, roster, and equipment edits without losing
   context?
5. Do they prefer drag/drop, click-place, or use both situationally?
6. Does the last formation save time without causing players to ignore the encounter?
7. Can players inspect the enemy while the drawer is open without losing their pending edit?
8. Does the open drawer leave enough battlefield context at 16:9?
9. Is a two-hero reserve enough to create meaningful lineup adaptation without hoarding?
10. Which actions do players immediately try to undo?

## Current recommendation

The exact foundation slice is now live:

> Open The Last Oath on the battlefield with a three-hero formation and one reserve. During
> Planning, open the capacity-driven Muster Drawer, swap the reserve onto a fielded hero, move
> heroes among legal hexes, change owned weapons, undo a change, and Begin Fight from one exact
> committed snapshot.

Play this at human speed before adding shop/forge/run-map complexity. The next clarity work should
be legal-target overlays, safe-area camera response, restore/reset, and real-player input
validation. Actual consumable content can then prove the generic resource-intent seam without
requiring a parallel Planning system.
