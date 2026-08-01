# Planning system — implementation and extension contract

> ⚠ **This is not the roadmap planning SOP.** Despite the filename and its home in `Projects/`, this
> page is about the in-game `PlanningSession` (draft/validate/commit/undo of unit placement). The
> board process lives in `CLAUDE.md`'s Planning SOP plus `Projects/roadmap.md`'s Stages block.
>
> ⚠ **Partly superseded by roadmap item 34 (in BUILD as of 2026-07-29).** That item folds Muster into
> the workbench frame and explicitly retires `RecruitView` + `MusterCard` + `RevisionDraftView`, so
> the "Opening Muster presentation contract" section below describes a flow being replaced. Its
> *contract requirements* (the five projected objects, the rejection rules, cadence derived from the
> composed weapon rather than copied card data) should carry over to the shared card — the container
> changes, the honesty rules don't. Amend this page when item 34 lands.
>
> Also stale: the header's test count. The suite is 533–534 as of 2026-07-29, not the 76 recorded
> further down. The six "Honest remaining work" items at the end of this page appear on neither the
> board nor the Deferred list, so they are invisible to the SOP that treats the board as the only
> live list — promote the ones that still matter.

**Status:** foundation plus run workspace live and Unity-verified 2026-07-24. The original
bonded-pair proof still owns the transactional `PlanningSession`/undo experiment. ADR 0019's run
shell now supplies the persistent player-facing workspace: encounter intel, formation, roster,
Market, Armory, Hourstone, rewards, and the wider act remain visible around the same board.
The run shell currently commits formation directly through `RunController`; converging its free
edits onto `PlanningSession` is architectural follow-up, not a blocker for the first playable.

The player-facing law lives in `Design/preparation-and-deployment.md`. This page records how to
extend the implementation without moving game rules into Unity.

## Boundary map

| Layer | Owns | Current implementation |
| --- | --- | --- |
| `Warband.Run` | Draft state, structural validation, atomic actions, history, commit snapshot | `sim/Warband.Run/Planning.cs` |
| `Warband.Content` | Which heroes/options/resources are legal for this encounter or run | `SkirmishProof` and `SkirmishPlanningRules` |
| Unity controller | Converts click/drag intentions into Planning actions; composes the committed battle | `SkirmishController.cs` |
| Unity view | Renders a screen model and emits intentions; never mutates the draft | `SkirmishScreen.cs`, UXML, USS |
| Replay renderer | Board geometry, picking, snapshots, selection marker, and combat playback | `ReplayPlayer.cs` |

The live run host parallels those boundaries in `RunShell` → `RunShellModel` /
`RunShellActions` → the registered `RecruitView`, `WorkbenchView`, `WagerView`, and `DeployView`.
Purpose-built `MusterCard`, `MarketOfferCard`, Workbench renderers, and `InspectorPanel` consume
the shared hydrated `CardModel`. `PresentationCatalog` owns presentation-only copy/art/icon
references; composed `UnitDef` remains the only source of mechanical card values.

Dependencies still point one way: Unity → Content → Run → Sim. `Warband.Run` does not know about
Unity controls, authored catalogs, weapon definitions, or consumable mechanics.

## Opening Muster presentation contract

The run-opening choice is a dedicated `MusterCard`, not a `WarbandCard` density variant. Every
offer projects the same five objects from one composed `UnitDef` plus presentation metadata:

- exactly three scan facts, in order: Health · Basic power + cadence · Reach;
- one named Signature row with a single keyword and its base-attacks-to-cast context;
- one named Passive row with a single keyword; and
- a portrait lens containing weapon identity, Mana per swing, crit/cleave qualifiers, and exact
  `MechanicalRulePresenter` language.

`MusterPresentationContract` rejects missing glyphs, reordered/extra facts, empty keywords, or
empty exact rules. `MechanicalRulePresenter.BasicAttacksToSignature` derives cast cadence from
the composed weapon rather than copied card data. Selection order lives in three portrait sockets
and remains the exact order passed to `RunSetup.Begin`.

The card body is not a tooltip target. Pointer disclosure belongs to individual facts/rules;
keyboard focus opens the combined portrait lens while the whole card remains one tab stop.
`IRunScreenLifecycle` makes screen exit a cancellation boundary for lens timers, reveals, and UI
FX. Seed/reroll controls are debug-only. Mobile composition is explicitly deferred; the current
contract is verified at 1920×1080 and 2560×1440.

## Draft and identity

`PlanningDraft` is the complete provisional answer to one encounter:

- `FieldCapacity`, `BenchCapacity`, and `MinimumFieldCount`;
- hero instance state (`Id`, `ContentId`, Field/Bench, bench slot, preferred hex, loadout slots);
- finite owned Planning resources; and
- queued content-owned intents such as a consumable use.

`Id` is the stable owned instance identity. `ContentId` locates authored content. UI order,
GameObject ids, and card indexes are never Planning identity.

Benched heroes retain their preferred `Position`. A direct field/bench swap deliberately gives the
incoming hero the outgoing hero's hex and moves the outgoing hero into the reserve's exact slot.
Bench capacity is data at every layer; empty sockets are generated from it.

`PlanningSession.Current` returns a clone. Callers cannot mutate the authoritative draft around
validation or history.

## Transaction law

Every free edit implements `IPlanningAction`:

1. `PlanningSession` clones the current draft.
2. The action edits only that candidate.
3. The candidate passes structural and content validation in `Edit` mode.
4. Only then does it replace the current draft and enter snapshot history.

An invalid action cannot partially mutate state or create an undo entry. Snapshot history is
intentional: a warband draft is small, and new actions gain safe undo/redo without hand-authored
inverse logic.

Current reusable actions cover:

- move/swap field positions;
- swap field and bench;
- move to field, bench, or another bench slot;
- set a loadout option; and
- use a finite Planning resource by decrementing its provisional quantity and queuing an intent.

`PlanningRules` is the content adapter. It owns legal board positions, legal loadout options,
resource/target legality, and content validation. Structural invariants remain in
`PlanningValidator`.

## Commit law

`PlanningSession.Commit()` is the only Planning commitment:

- it validates in `Commit` mode;
- failure returns typed issues without mutating the session;
- success returns an isolated exact `PlanningDraft` for battle/run composition; and
- success clears local undo/redo history.

The Unity proof builds its `Battle` from that returned snapshot, not from mutable UI state.

When authoritative `RunState` integration lands, the host must reconcile the successful snapshot
exactly once before or atomically with starting combat:

- persist lineup, loadouts, and formation by stable instance id;
- apply queued intents and consume their resource quantities;
- record the committed formation for the next encounter; and
- reject or safely retry the whole commit if authoritative state has changed.

`PlanningSession` deliberately does not reach into `RunState`; transaction ownership belongs to
the run host.

## Adding a new feature

### A free Planning edit

Add one `IPlanningAction`. Keep it content-agnostic where possible, mutate only the supplied draft,
return a specific failure message, and let the session provide atomicity/history. Add tests for
success, invalid atomicity, undo, redo when relevant, and non-default capacities.

### A new loadout slot

Store the selected content id under a stable slot key in `PlanningHeroState.Loadout`. Extend
`PlanningRules.CanSetLoadoutOption` and commit validation; render options from content data.
Do not add a Unity-only weapon/trinket field.

### A consumable or pre-fight resource

Add a `PlanningResourceState` with a stable instance id, content id, quantity, and optional
content-owned state. Issue `UsePlanningResourceAction(resourceId, intentKind, targetId, parameters)`.

The action:

- asks content rules whether this resource, intent, target, and parameter bag are legal;
- decrements the provisional quantity;
- queues a typed `PlanningIntent`; and
- remains fully undoable before Fight.

Targets may represent a hero, hex, encounter, or whole warband through a stable target id and
validated parameters. The run host interprets the intent only at successful commit. Buying,
selling, forging, or another immediately authoritative economic mutation is not a consumable-use
action and must not be hidden in this undo stack.

### An economic service

Confirm and mutate authoritative `RunState` through the service's own transaction. Then construct
or explicitly reconcile a new valid Planning draft and clear stale history. Never let Undo refund
a purchase by restoring a local snapshot.

### A changed field or bench capacity

Change data only. Validators and actions already read capacities from the draft; the Muster Drawer
generates all reserve sockets and scrolls horizontally. Add a test using a capacity other than two
before changing presentation thresholds.

## Unity interaction contract

The board-first shell uses UI Toolkit for the encounter strip, Muster Drawer, inspector, Armory,
history controls, and commitment. `SkirmishScreen` receives a rendered model and emits intentions.
`SkirmishController` is the only adapter to Planning actions.

Both input paths converge:

- roster cards support click selection and drag/drop;
- the 3D board supports click-place and drag-place;
- dropping onto another friendly performs the appropriate swap; and
- invalid targets leave the draft unchanged and show feedback.

`ReplayPlayer` owns projection and unit picking, but not placement legality. Its Planning selection
marker is presentation-only.

Keep UXML and USS basenames distinct in `Resources`. `Resources.Load<StyleSheet>` may otherwise
return the UXML's generated inline stylesheet subasset. The current pair is `Skirmish.uxml` and
`SkirmishStyles.uss`.

Runtime UI documents use separate `PanelSettings` because Planning scales with the screen while
the debug/tooltip tools use constant pixels. Cross-panel render and input order therefore belongs
to `PanelSettings.sortingOrder`, not only `UIDocument.sortingOrder`: Planning 800, tooltip 900,
debug 1000. Full-screen roots on overlay panels must use `PickingMode.Ignore`; only real
interactive surfaces opt back into picking, or an invisible panel will shield the battlefield.

## Verification

The foundation is covered by headless tests for:

- atomic field/bench and field/field swaps;
- invalid actions not mutating or entering history;
- arbitrary bench capacity and bench reorder;
- content-owned loadout legality;
- edit-versus-commit validation;
- parameterized consumable intent queueing and undo;
- isolated read/commit snapshots; and
- commit clearing history.

On 2026-07-24, 76 `Warband.Run.Tests` passed. The Windows Unity 6.3 editor compiled cleanly and a
Play Mode pass verified weapon change → formation move → reserve swap → undo → drawer collapse →
Begin Fight → Result → return to preserved Planning, with zero console warnings/errors.

## Honest remaining work

- Persist the last committed formation across run encounters, not only retries in this live proof.
- Reconcile commit snapshots/intents into authoritative `RunState`.
- Add actual consumable content and its contextual target presentation.
- Add legal-hex, range, target, relationship, and comparison overlays.
- Add restore/reset commands, controller navigation, touch validation, and camera safe-area
  reframing.
- Integrate shop/forge/reward services only after the current manipulation slice is playtested.
