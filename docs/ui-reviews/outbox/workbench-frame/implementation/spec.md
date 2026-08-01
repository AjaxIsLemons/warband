# Implementation spec — workbench-frame (item 34)

Approved 2026-07-28: `samples/01-muster-state.png` + `samples/02-rankup-modal.png`.
Condition: the rank-up modal entrance is a BIG animated moment ("a dopamine shot"),
with a reduced-motion fallback.

Base geometry is the built workbench (see `../workbench-refactor/implementation/spec.md`);
this spec describes only the deltas. The workbench becomes THE out-of-combat frame:
`Menu → [workbench: muster] → [choice scrim: first revision] → [workbench: act play]`,
with Wager/Deploy/Fight/RunOver unchanged and explicitly out of scope.

## A. Muster as a workbench state

Routing: `RunScreen.Recruit` and `RunScreen.RevisionDraft` are deleted. `NewRun` goes to
`Management`; `_run == null` IS the muster state. `Rebuild` branches: pre-run populates the
same `PlanningModel` in muster mode (`PlanningModel.MusterMode = true`); post-run population
is untouched. `RecruitView`, `MusterCard`, `RevisionDraftView` retire.

Header:
- Title label reads `MUSTER YOUR WARBAND` (bind from model — title becomes data, not UXML
  text). Act label: `BEFORE ACT 1`.
- Track: the real act-1 track with node 1 in the `--current` state, nothing `--past`.
- Hourstone host: hidden (nothing to spend). Armory rack chip + rack: hidden (nothing stored).
- Continue button = BEGIN RUN, gated: disabled with `BEGIN RUN · n / 3 MUSTERED` until
  3 picks, then enabled `BEGIN RUN ›` in the primary gold state (existing btn--primary).

Market (selection mode):
- Offers = the real 5-candidate `RunSetup.RecruitOffer` as MarketOfferCards:
  eyebrow `<musterRole> · <role>` (e.g. `TANK · WARD`), name, portrait art, tier strip
  `RANK C ◇◇◇`. No price anywhere.
- Picked state: `MarketOfferCardModel.MusterSlot` (int, -1 = unpicked). Picked cards show a
  gold slot badge (1-based), `✓ MUSTERED` in the commerce row, and the picked border
  treatment. Click toggles via existing `ToggleRecruit` (keeps MusterSelect/Deselect cues).
- Inspected state unchanged (drives the dossier).
- 6th cell: instruction ghost — dashed hairline cell carrying the muster instruction copy
  (`CHOOSE THREE CHAMPIONS…`). Not a button.
- Reroll rail: visible, `FREE` instead of a cost, wired to `RerollSeed` (new seed + offer,
  clears picks — existing behavior).

Dossier:
- The full standard candidate sheet (kind row `CANDIDATE · <role words>` + `RANK C`, banner,
  chips, signature, basic attack, passive strip, PATH rows all-dormant with the fork named).
  Reuses the existing inspector; only the kind eyebrow and actions differ.
- Actions row: ONE primary button — `MUSTER · SLOT n` (next free slot) or
  `REMOVE FROM MUSTER` when the inspected candidate is picked. Invokes `ToggleRecruit`.
  No forge/reserve/dismiss actions pre-run.
- Empty state (nothing inspected): existing EmptyHint mechanism with the muster instruction.

Rail (shell-owned Warband Bar in muster mode):
- Slots 1–3: picked candidates as full progression cards (rank C badge, signature slot
  filled, starter-weapon slot filled, trinket empty, B/A/S path slots empty).
- Next free slot renders the AWAITING tile: gold-dashed `n` + `CHOOSE YOUR THIRD/SECOND/…`
  (new tile state, distinct from LOCKED). Remaining slots: LOCKED as today. Reserve: OPEN
  as today. Identity block: `WARBAND / MUSTER n / 3 / PICK YOUR THREE`.

## B. Starting revision = choice-scrim beat #0

- `BeginRun` (3 picked) no longer routes to a screen: it sets a pending-first-revision flag
  and rebuilds; the existing choice scrim presents `PlanningBeat.StartingRevision` (new enum
  value): eyebrow `FIRST REVISION`, title `Bind one way to alter a battle`, copy = the old
  RevisionDraft instruction, one choice card per `RevisionCatalog.Starting` entry (name,
  rule, evolution preview lines as the card copy). Choosing invokes
  `ChooseStartingRevision` → `BeginSelectedRun` (unchanged) → the scrim drops into the
  act-1 workbench.
- The muster market/rail stay visible (dimmed) beneath the scrim — same as every other beat.

## C. Rank-up modal (replaces the generic scrim rendering of SpecChoice)

- `BindBlockingChoice` loses the `spec` branch; a new `rankup-scrim` region (sibling after
  `choice-scrim`) binds when `SpecChoice.Pending`.
- Structure (per approved 02): eyebrow `RANK UP · THE FORK` (fork rank only; otherwise
  `RANK UP`), title `<HERO> · <from> → <to>`, bump line `+<hp> HEALTH · +<atk> POWER — THEN
  BIND HIS PATH` (real per-chassis RankHp/RankAttack; the bump is applied at purchase — the
  line reports what was just gained), then the option row, then hint
  `THE WORKBENCH WAITS UNTIL HE CHOOSES`.
- Center hero card: portrait, `C → B` rank badges, name, gear row (signature/weapon/trinket
  fill states), path row with the awaiting rank slot in the gold-dashed awaiting state;
  hovering an option previews its icon in the awaiting slot. Foot: `HIS CARD REMEMBERS`.
- N-option safety (model contract): options render as a row with the hero card inserted at
  index `count / 2` — two options flank the card (approved layout); more than two stay
  legible in the same row. Never assume two.
- Option panel: kind eyebrow (`REACTION`/`POWER`/… from the rule Change), icon tile, name,
  full rule text, up-to-3 stat deltas (existing Comparisons), `BIND <NAME>` button →
  `ChooseSpec(i)`.
- Model additions (`SpecChoiceModel`): ChassisId/portrait, from/to rank labels, bump text,
  fork flag, gear fill states, path tiers, per-option icon. Controller composes from the
  same sources as the rail cards.

### Entrance choreography — the dopamine shot
- Sequence (~700ms total, USS transitions + scheduled class flips, cancelled on exit):
  1. scrim opacity 0 → 1 (~220ms);
  2. hero card scale 0.62 → 1.04 → 1.0 with a small rise (~350ms, ~80ms delay);
  3. option panels slide in from ±40px with fade (~250ms, ~180ms delay);
  4. the awaiting path slot pulses (slow repeating glow class) until a choice is made.
- Emit the existing RankUp polish cue on open; bind emits its confirm as today.
- Reduced motion: no scale/slide/pulse — a single ~120ms fade, awaiting slot statically gold.
- Screen exit / bind cancels all scheduled work (existing cancellation-boundary law).

## D. Retirements and rewiring

- Delete: `RecruitView.cs`, `MusterCard.cs`, `RevisionDraftView.cs` (+ .meta), their UXML/USS
  assets, `RunScreen.Recruit`/`RunScreen.RevisionDraft` and all routing, the flow-lab MUSTER
  buttons (replace with one `MUSTER STATE` nav button), muster lens machinery
  (`MusterLensTarget`, reveal previews), `RecruitModel`/`MusterCardModel`/
  `MusterSelectionSlotModel`/`RevisionDraftModel` (+ `MusterPresentationContract`).
- Editor QA hooks that opened Recruit/RevisionDraft re-point at the muster state and the
  starting-revision scrim.
- Layout contracts: muster state — BEGIN RUN present + gated, 5 offer cards + instruction
  cell inside the grid, picked badge inside its card, awaiting tile inside the rail, no
  hourstone chip, no rack chip. Rank-up modal — scrim covers root, card + option panels
  inside the safe area, options.count rendered, bind buttons hit-testable.
- Fixtures: `muster-state` (2/3 picked, Bulwark inspected — mirrors the approved sample) and
  `rankup-modal` (Phalanx fork) join `WorkbenchFixtures.Ids`; the workbench-full matrix grows
  by 2 fixtures × 5 viewports.

## Must-match vs illustrative

- Must match: region roles and states above; picked-card badge + MUSTERED language; the
  awaiting rail tile; gated BEGIN RUN copy; modal composition (bump line, card-between-
  options, awaiting-slot preview, hint); all-dormant PATH at muster; FREE reroll.
- Illustrative: glows/soft shadows (engine: flat borders/tints as established), the radial
  scrim (engine: uniform tint), exact copy of the ghost-cell instruction and card foot,
  portrait crops.

## Acceptance

1. `make check-client` clean.
2. Workbench-full matrix (now 15 fixtures × 5 viewports) all-PASS from a verified-synced
   build, editor idle-launched; capture PNGs eyeballed, not just contract-green.
3. Live flow regression in the editor: menu → muster (toggle picks, reroll, pick 3) →
   BEGIN RUN → first-revision scrim → act-1 workbench → buy a rank-up → modal entrance →
   bind → rail path slot fills; console clean throughout.
4. Implementation captures into `implementation/`, compared against both approved samples
   with engine deviations listed.
