# UI review: combat-inspection

Status: IMPLEMENTED
Created: 2026-07-29

## Brief

- **Screen or flow:** unit inspection during Fight and Deploy — hover card, click record, and the
  Deploy enemy panel.
- **Primary player decision:** none. The player cannot act mid-fight. Inspection serves exactly
  one thing: **understanding why the fight is going the way it is, so the NEXT build/placement
  decision is informed.**
- **Required information:** identity (name, role, weapon), live vitals (HP/Shield/Mana), the
  targeting rule, live statuses, and — on the pinned surface only — signature, passive, basic
  attack, specs.
- **Required states:** hover (ally / enemy / enemy-without-portrait), click record, Deploy enemy
  list. Fight keeps running throughout — **Jake, 2026-07-29: no pause.**
- **Target viewport:** 1600×900 (16:9); landscape only per the rotation guard.
- **Must preserve:** mechanic-family colour semantics; `RuntimeTooltipService` keyword layer;
  the 14px body floor; no ScrollView inside these surfaces; reduced-motion path.
- **May change:** placement, material, the hover card's information budget, the record's size and
  presentation mode, the Deploy panel's structure.

## Diagnosis (from code, 2026-07-29)

Three surfaces, two design languages:

| Surface | Built on | State |
|---|---|---|
| Hover a body mid-fight — `Tooltip.cs` | Own `UIDocument` (order 900), DebugMenu palette, hand-rolled | **The problem.** Dumps identity + facts + targeting + every passive + every status at flat priority, all `Muted` grey; labels at `font-size: 9px` (`MechanicPresentationStyles.uss:331`), below the 14px floor the contract enforces on `.runtime-tooltip__body`. Escapes the contract by living in a separate document. Un-hoverable by construction (`pickingMode = Ignore` on all children), so keyword drill-down is impossible. |
| Click a body mid-fight — `InspectorPanel` (`DecisionDetailKind.Combatant`) | `RuntimeTooltipService` + mechanic families + `BindSemantic` | Already the right system. Presented as a full-screen scrim modal — wrong now that Jake has ruled no pause (`RunShell.cs:2846` refreshes it every 150ms over a running fight). |
| Deploy enemy panel — `DeployView._enemyList` | Flat `Label`s | `RunShell.cs:6222` renders `"{Name} · {Role} · {MaxHp} HP · reach {Range} · row {Row}"`. Meanwhile `RunShell.EnemyCard()` already builds a full model (authored name, role, row, behavior line, HP/POWER/REACH/CADENCE chips) that the Wager screen uses. Deploy throws it away. |

**The good tooltip layer is already reachable from all of this.** `RuntimeTooltipService` is
instantiated on the RunShell root (`RunShell.cs:2730`) and `WorkbenchStyles.uss` — which owns
`.runtime-tooltip` — is already in the shipping sheet list (`UiEnvironment.cs:40`). Mostly a
deletion job.

## Inputs

| Source | Role |
|---|---|
| `client/Assets/Scripts/Warband/Tooltip.cs` | Current hover surface — the thing being replaced |
| `client/Assets/Scripts/Warband/RuntimeTooltipSystem.cs` | The target system (eyebrow/title/domain/body/facts/context/footer + keyword links) |
| `client/Assets/Scripts/Warband/InspectorPanel.cs`, `RunShell.cs:6294` | The existing Combatant record |
| `client/Assets/Scripts/Warband/DeployView.cs`, `RunShell.cs:6211` | The Deploy enemy panel |
| `client/McpCaptures/board-live-t46.png` | Real fight board — backdrop for every sample |
| `client/McpCaptures/ui-qa-20260728-095949-1600x900-deploy-deploy-nominal.png` | Current enemy blob |
| `client/Assets/Resources/UI/LastHourTokens.uss` | Palette tokens (copied verbatim into samples) |
| `sim/Warband.Content/Enemies.cs` | Real enemy data + behavior copy |
| `docs/vault/Design/workbench-dossier.md` | The dossier laws these samples inherit |

## Established laws these samples obey

From `Design/workbench-dossier.md`:

1. Role, not geometry, decides what shows. Deferred ≠ hidden — one compact line, full rule on hover.
2. Stat block and prose never interleave.
3. One fact, one channel.
4. Trigger → effect grammar, trigger visually distinct.
5. Three disclosure layers, each answering one question. Combat's mapping:
   **body = "who's winning?" · hover = "what is this?" · click = "what exactly does it do?" ·
   keyword = "what does that word mean?"**

Added this pass:

6. **Passives leave the hover card.** Static, per-build, and the longest content on it. Hover
   carries live state; the record carries the engine. This single cut ends the dump.
7. **Hover never contains a link; anything with a link is a pinned surface.** A cursor-following
   card cannot be moved onto.
8. **Team identity is the plate EDGE; it never touches a number.** Crimson already means "damage
   your unit took" (item 30) — a red enemy chip beside red numbers collides.
9. **Enemies get a ROLE CREST**, not initials and not a hero's face. Five authored roles
   (`Enemies.cs`: Swarm / Anchor / Artillery / Ritualist / Diver), each a hex-cut crest plate.

## Prose rewrites carried in the samples

| Now | Proposed | Why |
|---|---|---|
| `Acquires the FARTHEST enemy, holds 5 hexes` | `TARGETS  Farthest, held at 5 hexes` | Label becomes the subject; the verb disappears. "held at" removes the holds-them / holds-at ambiguity. |
| `reach 5 · cadence 1.5s · crit 15%` | glyph/colour stat tiles | Numbers belong in tiles, never in a prose run. |
| `PROTECTION 20` | `SHIELD 20` | The *family* name leaking into player copy; the combat card already says SHIELD. |
| `Ash Warden · Defender · 188 HP · Reach 1` | structured enemy row + hover | A CSV is not a card. |
| `The Waning begins after 45 seconds.` | `WANING  at 0:45` | `FormatInline` colours "seconds" (the unit) and leaves "45" (the number) grey. |
| `Formation and rules are final. Combat has no hidden phase.` | delete | A designer's note about the disclosure contract. |
| `FULL INFO` pill | delete / show the rule's authored name | Labels the contract, not the content. |
| `CLICK OR TAP A UNIT · OPEN COMBAT CARD` | `Click a unit to inspect` | Two imperatives joined by a dot reads like a debug string. |
| `LIVE COMBAT · 88 HP REMAINING` | delete | Redundant twice over; HP is a chip directly below. |
| `● live / ○ idle / · armed` | family glyph + `LIVE` pill only when live | Three symbols, no legend, when brightness already carries the state. |

Separately: **`MechanicPresentation.FormatInline`'s regex is the real colour bug.** It colours
~40 common English words wherever they appear (`second`, `line`, `area`, `distance`, `duration`,
`attacks`). Fix = match the magnitude with its unit as one run (`45 seconds`, `3 hexes`,
`12 damage`) plus keyword nouns, not every verb.

## Assumptions

- No pause (Jake, 2026-07-29). Every sample keeps the fight running behind the surface.
- Hover survives as a distinct, lighter tier (Jake, 2026-07-29) rather than being cut for click-only.
- Copy shown is the *proposed* rewrite, not shipped strings.
- Data is real: `Enemies.cs` Sanddrift Gunner (85 HP / 28 atk / 1.5s / reach 5 / Farthest /
  standoff 5); Phalanx Skewer at 35 mana; Riposte.

## Samples

Rendered at 1600×900 over `board-live-t46.png` — a real fight capture — so contrast and placement
are judged against the actual board. Tokens verbatim from `LastHourTokens.uss`; shipped Inter +
Barlow Condensed. **Coded structural prototypes, not final art:** they prove hierarchy, density
and mood; they do not prove UI Toolkit layout, wrapping under copy stress, focus order, or
reduced motion.

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `A-dossier-plate.png` — Evolution | The Workbench dossier language alone fixes this; placement is fine | Smallest change; reuses the shipped dossier idiom exactly; lowest risk | Record still covers a third of a *running* board; hover still floats free of the body it describes | Plate material and information budget are literal; portrait crop and exact metrics illustrative |
| `B-anchored.png` — Structural (**recommended**) | Since the fight does not pause, **no inspection surface may cover the board** | Only direction that honours "no pause". Tether = the TFT move (hover highlights the body), killing "which one am I looking at". The dock is the same component Deploy's enemy panel becomes — one build, two screens | Reframing the board mid-fight needs a camera nudge, or the dock eats the right flank of an 8-wide board. Highest cost | Dock width and tether geometry illustrative; the no-cover law is literal |
| `C-field-banner.png` — Wildcard | ONE inspection surface at two sizes, always attached to the body; click expands *that* banner in place | Most game-like, fewest concepts, strongest fiction fit (Last Hour / scried record) | An expanded banner near a board edge has nowhere to grow; long-rule units may not fit without scrolling, which the layout contract forbids | Bronze/rivet intensity illustrative; the one-surface-two-sizes structure is literal |

Enemy role crests in all three are placeholder marks drawn for these samples. The real set is
five authored glyphs — or, better and later, flat silhouettes of the item-29 procedural bodies so
the crest matches what is actually standing on the hex.

## Jake review — round 1 (2026-07-29)

1. **Direction B**, but as a small **floating card**, not a full-height edge dock. Vertical, not
   horizontal. Take up as little space as possible.
2. **Delete the hover tier entirely.** "That's what the nameplate is for." Only the click card
   remains — one inspection surface, full stop.
3. Enemy crest: **authored role glyph** is fine for now.

Settled earlier this session: **no pause** — the fight runs while the card is open.

### Consequence found while building r2 — needs Jake's call

Deleting hover makes the **board nameplate** the sole answer to "what is this". Today it is not
that:

- `tuning.json` ships `nameplates.show = **false**` — nameplates are **off right now**.
- When on, `MakeNameplate` (`ReplayPlayer.cs:3982`) renders a bare `TextMesh` of `u.Name` and
  nothing else: no role, no team colour, no crest. Styling is size + colour only
  (`StyleNameplate`, `ReplayPlayer.cs:4004`).

So "hover is redundant with the nameplate" is true only *after* the nameplate is turned on and
given at least name + team. Otherwise the board becomes anonymous bodies that must each be
clicked to identify. **Turning nameplates on and styling them is in scope for this job.**

## Samples — round 2

Same card, same real data (Phalanx / Leonnatos — the densest realistic ally: signature + passive +
basic attack + two specs). Placement identical. The only variable is how much rule text stays
visible by default.

| Sample | Hypothesis | Size | Benefit | Risk |
|---|---|---|---|---|
| `B-r2-column.png` — **Column, full** | Everything visible; nothing needs a second interaction | 302 × 571 px (19% of width, 63% of height) | Zero hidden state. One click answers every question. Works untouched on tablet/controller. | Tallest case. A Berserker with a full ladder will run near the safe-area floor. |
| `B-r2-deferred.png` — **Column, deferred** | Rules collapse to named one-liners with their trigger; full text on hover, using the existing `RuntimeTooltipService` keyword layer (legal — this is a pinned surface) | 302 × 441 px (**23% shorter**) | Smallest footprint; the card becomes a table of contents for the unit's engine; height barely grows with content. | Rule text is one interaction away. Hover-only disclosure is the accessibility failure mode research flagged — needs the keyboard/focus path to work, not just pointer. |

Both keep: tether stem + hex target ring on the body · role crest for enemies · team colour on the
plate edge only · compact single-row fact strip (four tiles do not fit a 302px column) · `TARGETS`
row · `LIVE` statuses pinned at the foot.

Open question still unsettled:
- Deploy's enemy panel: reuse this same card component per enemy, or stay a list and only restyle?

## Jake review — round 2 (2026-07-29)

1. **r2-A** looks right. But it should **merge with the Workbench dossier** — "it's basically the
   unit card". Investigate.
2. **Remove the text nameplates.** What Jake meant by "nameplate" is the HP bar + mana bar +
   status bar. No text above units — just status icons + stacks.
3. Same card everywhere: Workbench dossier + combat inspect. Find a middleground.

### Investigation result: the merge already exists

`InspectorPanel` **is** the shared component today. The Workbench dossier and the in-fight combat
card are the same class, the same `InspectorPanel.uxml`, and the same `BuildInspectorSections`
pipeline (`RunShell.cs:5669`). They differ by exactly two things:

- **`DecisionDetailKind`** (`RunShellModel.cs:345`) — 8 kinds; `Champion`/`Recruit` and `Combatant`
  are two of them.
- **Which sections are Primary vs Deferred.** `Champion`/`Recruit` defers PASSIVE to a one-liner;
  `Combatant` keeps it Primary, with the authored reason in the code: *"mid-combat there is no
  second look"* (`RunShell.cs:5696`).

The only presentation difference is the wrapper: combat wraps it in a scrim modal
(`wb-inspector--modal` / `fight-inspector-modal`, `RunFlowStyles.uss:446`).

**So this is not a merge — it is a restyle of one component plus two optional bands, and the
Workbench dossier inherits the new look for free.** The genuinely unshared things are
`Tooltip.cs` (deleted) and the Deploy enemy list (flat labels, must be routed through the
component).

### The middleground — `r3-unit-card-sheet.png`

One card, five bands. Three always present, two context-only. **Width is the only thing that
changes between contexts** — same anatomy, same order, portrait scales with the column.

| Band | Present in | Content |
|---|---|---|
| 1 Identity | always | crest/portrait · eyebrow (kind · role) · name · subtitle |
| 2 Facts | always | one compact strip — live `current/max` in combat, static in the Hall |
| 3 Rules | always | trigger → effect; Primary in full, Deferred as one-liners |
| 4 State | combat only | `TARGETS` row + `LIVE` statuses |
| 5 Decision | Hall only | price chip + comparison/deltas + action row |

Widths: **440px** in the dossier column, **302px** floating on the board. Portrait bezel 88px vs
44px — same hex cut, same slot.

Retained on purpose: the Primary/Deferred split stays per-kind. The Hall defers PASSIVE (you get a
second look before buying); combat keeps it Primary (you do not). That is law #1 — role, not
geometry — already working.

### Nameplates

`MakeNameplate` (`ReplayPlayer.cs:3982`) builds a world-space `TextMesh` of the unit's name and is
independent of the HP/mana/status chrome (`UnitView.Icons`, the bars). It already ships disabled
(`tuning.json` → `nameplates.show = false`). **Removing it is pure dead-code deletion**; bars,
status icons and stacks are untouched.

## Jake review — round 3 (2026-07-29) + research

Notes: research the portrait question and **decide**; give rank escalating punch; signature mana
must not be gold; the basic-attack row should be owned by the weapon and hoverable; sections
should be **Signature / Weapon / Passives / Specs** — "Deferred" is not a player word.

### Research → decisions

**Portrait.** Design guidance splits cleanly: large portraits carry identity and emotional
engagement (better light/shadow, facial legibility) and belong where the player is *choosing*;
small icons carry reference and belong in compact spaces where identification is secondary.
Below roughly 32–48px a face stops resolving at all.

**Decision — portrait size follows the question being asked**, which is the same three-layer
disclosure law already in the vault:
- **Hall (dossier + shop card) keeps the full portrait banner.** "Do I want this?" is an
  identity/desire question and the Hall has the room.
- **Combat uses a 56px hex bezel** — raised from r3's 44px on the research's legibility floor.
  "What is it doing right now?" is a reference question.
- Same slot, same hex cut, same position in the anatomy. Only the scale changes.

**Rank.** Rarity/tier systems converge on *multi-channel* escalation — shape + colour + finish
together, readable at a glance — with the top tier getting a channel the lower tiers do not have,
so it reads as an event. Warband's ladder is C → B → A → S:

| Rank | Plate | Gems | Name | Exclusive |
|---|---|---|---|---|
| C | iron, undecorated | – | bone | – |
| B | bronze | ◆ | bone | – |
| A | blue-steel + hairline rule | ◆◆ | bone | – |
| S | **gold, inverted dark-on-light** | ◆◆◆ | **gold** | **card spine turns gold + a sheen sweeps the plate** |

C is deliberately undecorated — if every tier is decorated, none of them reads as an escalation.
Rendered as `r4-rank-ladder.png`.

### Colour law fix (my error in r2/r3)

Signature mana was rendering in sand/gold. Fixed to the **Mana family teal**. Chased it further:
**sand is now Hourstone COST only** — nothing else may use it. That moved the `TARGETS` row off
sand onto **Space blue**, which is correct anyway since targeting is spatial
(nearest / farthest / hexes). Trigger chips now carry the family of what they key off:
`35 MANA` teal, `WHEN HIT` offense-orange.

### Section grammar (replaces the old set)

**SIGNATURE · WEAPON · PASSIVES · SPECS.** Four named sections, no meta-vocabulary.

- **SIGNATURE** — trigger chip carries the mana cost in teal; name; rule.
- **WEAPON** — the weapon **owns** the basic-attack row. Name + temper (`Pike · Honed`) is
  hoverable → full weapon dossier (profile, tier, mastery rider). The damage line is the weapon's
  line, not a separate "basic attack" concept.
- **PASSIVES** — plural. Name + trigger label + rule.
- **SPECS** — the tier-up selections, each showing the rank it was taken at (B / A / S), each
  hoverable for its full rule.

`Deferred` stays what it always was in code — a **role** that decides full-text vs one-liner —
and is never a visible heading. Showing it as one in r3 was a mockup error, not a proposal.

## Open for Jake

- The Workbench dossier currently leads with a **wide landscape portrait banner**; the unified card
  replaces it with the inline hex bezel. That is a real loss of art presence in the Hall. Keep the
  banner as a Hall-only band 0, or accept the bezel everywhere?
- Deploy enemy panel: one 302px card per enemy is 3 cards side by side — or a compact list that
  opens the card on select?

## Approval

- **Approved samples:** `r4-unit-card.png` + `r4-rank-ladder.png`
- **Conditions:** combat needs a proper interaction contract for click + hover/keyword tooltips
  (Jake, 2026-07-29). Captured in `implementation/spec.md`.
- **Date:** 2026-07-29

## Review log

- 2026-07-29 — Job created.
- 2026-07-29 — Diagnosis written from code; three directions generated and rendered. AWAITING_REVIEW.
- 2026-07-29 — Jake: direction B as a small floating vertical card; hover tier deleted; authored
  role glyph. Round-2 pair rendered (`B-r2-column`, `B-r2-deferred`) testing default rule density.
  Found that nameplates ship OFF, which the "hover is redundant" argument depends on. AWAITING_REVIEW.
- 2026-07-29 — Jake: r2-A approved in principle; merge with the Workbench dossier; delete text
  nameplates. Investigation found `InspectorPanel` is ALREADY the shared component — the work is a
  restyle, not a merge. `r3-unit-card-sheet` renders the unified five-band card in all three
  contexts. AWAITING_REVIEW.
- 2026-07-29 — Jake: research the portrait question and decide; rank needs punch; mana must not be
  gold; weapon owns the attack row; sections = Signature/Weapon/Passives/Specs. Research done and
  decided (portrait size follows the question; Hall keeps the banner, combat gets a 56px bezel).
  Colour law tightened: sand = Hourstone cost only. `r4-rank-ladder` + `r4-unit-card` rendered.
  AWAITING_REVIEW.
- 2026-07-29 — Jake approved r4. `implementation/spec.md` written. Build started.
- 2026-07-29 — Built. `make check-client` PASS, `make test` PASS (534/534). Files confirmed on the
  Windows tree. **Captures NOT taken:** the Unity Editor is unfocused and will not run the import
  (`kAutoRefreshMode=1` but `isApplicationActive=False`; `AssetDatabase.Refresh` +
  `RequestScriptCompilation` both queue without reloading the domain). Per CLAUDE.md the session
  did not steal focus. Status IMPLEMENTED_UNVERIFIED until the QA matrix runs.

### Deviations from the approved samples

1. **No cut-corner plate.** The mockups use `clip-path` for the bevelled slab; UI Toolkit has no
   `clip-path`. The card ships as a square plate with the team spine, top highlight and border
   intact. A bevel needs a 9-slice sprite — deliberately not invented here.
2. **No rank-S sheen.** The diagonal gradient sweep needs a texture; USS gradients are not
   available. S still gets its two exclusive channels — the inverted gold chip and the gold title.
3. **Combat SPECS carry no rank letter.** `PlaybackUnit` is the sim's view and has no rank (it is a
   run-layer fact). The Hall dossier still shows B/A/S. Inventing a letter in combat would be a lie.
4. **Deploy enemy card uses initials, not a role crest.** The five authored crest glyphs are not
   drawn yet; `Initials()` stands in, which is what `EnemyCard` already did.
- 2026-07-29 — **VERIFIED.** UI QA smoke matrix `20260729-144723`: **19/19 captures, 0 structural
  failures** (`UiLayoutContract` green — no ScrollView, 14px floor, wrapped text fits, no overlap).
  `make check-client` PASS · `make test` 534/534 PASS · Unity console clean on a fresh domain.
  Captures in `implementation/`.

  Two real defects were found and fixed by the run, which is the point of running it:
  1. A stale `DeployModel.EnemyPreview` fixture inside `#if UNITY_EDITOR` reached Unity green from
     homeserv. **`tools/check-client-compile.py` now compiles runtime scripts with UNITY_EDITOR +
     DEVELOPMENT_BUILD defined** (three stubbed UnityEditor symbols), so those blocks are checked
     from now on. Proved with a deliberate canary before removing it.
  2. Bolding every magnitude widened rule prose enough to add a wrapped line, overflowing the
     dossier's detail column at 1280×720. Magnitudes are now coloured but not bold; keyword nouns
     keep their weight.

  Fixtures now carry `Rank` (C / B / S) so the badge escalation is guarded by captures rather than
  only existing in a live run.
