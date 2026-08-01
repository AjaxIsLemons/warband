# UI review: revision-time-toy

Status: IMPLEMENTED_UNVERIFIED (R1 + R3 built; motion unseen)
Created: 2026-07-29

## Brief

- **Screen or flow:** the Revision draft — the held Hour, mid-fight, after REVISE.
- **Primary player decision:** *how far back do I reach, and who do I reach for?*
- **Jake's note that opened this job:** the shipped rail "looks like a tool UI and not a game… make
  it feel like a toy and not a slide deck." Rule of thumb: **SHOW, don't TELL** — use the units,
  draw numbers above them, stop menuing.
- **Target viewport:** 2560×1440 (16:9), must survive down to the phone layout.

**What is wrong with the current build** (`work/current-panel.png`): a 760px docked panel carrying
two numbered steps, a prompt paragraph, a landmark rail with an abstract legend, a per-anchor price
row, a wrapping readout sentence, a target chip and two buttons. Every fact about the battlefield is
rendered as *text about the battlefield*, six inches below the battlefield. The board — already
frozen, already lit, already showing the units — is doing none of the work.

### Required information

| Fact | Today | Where it should live |
|---|---|---|
| How far back am I | rail + `−2s` labels | one big diegetic number |
| What was different then | prose: "returns before Bram Oathkeeper died at 1.3s" | **the board — Bram is standing** |
| Who can I target | chip list + board glow | board only |
| What do I get | readout sentence | **above the target's head** |
| Confirm / cancel | two buttons | one held key + a hint |

### Must preserve

- ADR 0028 law 6 — **readable, not omniscient.** Landmarks are past facts from two witnessed
  moments; no forecast of the branch outcome. Copy says "returns before", never "undoes".
- ADR 0028 law 5 — local time only; the playhead moves, Unity's clock never does.
- One split, one authored fact. No ability buttons, no command queue.
- Whole battle-seconds only, 1..4 (6 with Long Memory).
- Reduced Motion needs a non-spatial path to the same information.
- UI Toolkit only.

### May change

Everything about the panel: its existence, the rail metaphor, where numbers live, how confirm reads.

## Inputs

| Source | Role |
|---|---|
| `work/current-panel.png` | The shipped rail — the thing being replaced |
| `work/board.png` | Real Unity board render (`hourstone.bytes` @ tick 46, edit-mode preview) — backdrop and scale reference |
| `docs/vault/Decisions/0028-revisions.md` | Laws |
| `docs/vault/Design/revisions.md` | Lineages, carry maths, upgrade names |

## Assumptions

- Illustrative values (`+33`, `25`, `−1.3s`, unit names) stand in for live data.
- The backdrop is a real render, so overlay scale against real units is honest.
- Direction art is placeholder: flat CSS shapes, not the intended Hourstone/sand material.
- Borrowed Future is the lineage shown; Recall would swap the carry orb for a formation ghost.

## Samples

All three are **structural prototypes** — coded mockups composited over a real Unity board render.
They test hierarchy, placement and density. They are not art, not typography, and not proof that
UI Toolkit will lay them out.

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `samples/a-quiet-strip.png` — **Quiet Strip** (evolution) | The panel was never the problem; the *prose* was. Keep one thin control, move every fact onto the units. | Smallest delta from what ships; keeps the rail's spatial correctness. | Still a docked strip — may read as "less slide deck" rather than "toy". | Medallion glyphs and values illustrative; track geometry literal |
| `samples/b-hourstone-dial.png` — **Hourstone Dial** (structural) | The *timeline* is the wrong metaphor. A rail with tick labels is an editing tool; a stone dial you turn is a toy — and it is already the world's fiction. | Biggest tonal shift for the least information loss. Turning it is a verb. | Drops the landmark rail entirely, so "when did Bram die" must be legible purely from watching the board scrub. Eats prime bottom-centre space. | Dial material illustrative; notch count and selection literal |
| `samples/c-past-on-the-board.png` — **The Past Is On The Board** (wildcard) | The most valuable fact is inherently *spatial* — "Bram died **here**, 1.3s ago". A rail forces a translation from rail-position to board-meaning. Delete the translation. | Timeline and battlefield become one object. Choosing a second becomes "put me back before *that*". | Highest cost and clutter risk: world-space anchoring, occlusion, landmarks stacking on one hex, and fractional landmark times must resolve to whole-second anchors. | Trail and marker placement illustrative; the interaction claim is the point |

### Detail — what each actually changes

**A · Quiet Strip.** Prompt paragraph, step numbers, price row, readout sentence and target chip
deleted. Landmarks become unit medallions on a sand track instead of an abstract colour legend.
Carry appears as a mana orb + `+33` over the chosen unit. The ally who is alive again at this second
wears `◄ ALIVE AGAIN HERE`.

**B · Hourstone Dial.** No panel at all. A sand ring rises into frame from the bottom edge, notched
1s…6s, with a gold grab-knob on the ring and one enormous `2s ROLLED BACK` in its eye. Landmarks are
drawn nowhere — the board shows them, because the board *is* the record.

**C · The Past Is On The Board.** No timeline widget anywhere. Each unit drags a dashed ghost-trail
of its own past hexes, labelled `−1s`, `−2s`. Landmarks become grabbable objects floating over the
hex where they happened (`BRAM FELL −1.3s`, `SEER CAST −2.4s`) — **clicking one sets the anchor.**
The only chrome is a sand counter riding the cursor.

## Illustrative, not literal

- All numbers and unit names are stand-ins.
- Glyphs (`☠`, `✦`, `↻`) are placeholders for authored icons.
- Colours follow current semantics — ally blue, enemy salmon, time amber, mana cyan — but tone and
  material are unresolved.
- The `0:04` battle clock and unit nameplates in the backdrop are the real shipped HUD, unchanged.

## Jake review

1. Preferred sample, combination, or reject all:
2. Must keep:
3. Most important next change:

## Approval

- **Approved samples (round 2, 2026-07-29):** `samples/r1-undertow.png` + `samples/r3-the-fork.png`
  ("I think you are right on R1 + R3. Build it!"). `samples/r2-two-timelines.png` held in reserve if
  R1 tests too subtle.
- **Approved samples (round 1):** `samples/b-r3-borrowed-future.png` and `samples/b-r3-recall.png`
- **Conditions:** none stated. Payoff-weighted notch brightness (proposal §4) is approved as part of
  the direction despite being unvalidated in the stills.
- **Date:** 2026-07-29 — Jake, in chat ("I say lets build it").

## Review log

- 2026-07-29 — Job created.
- 2026-07-29 — Three directions generated as coded mockups over a real board render (Codex lane not
  used; Jake asked to see prototypes in this turn). B re-cut twice: the dial had fallen off the
  bottom edge, then its highlighted notch disagreed with its own readout. Status `AWAITING_REVIEW`.


---

# Final proposal — B, reduced to what the mechanic actually needs

Jake selected **B (Hourstone Dial)** and rejected C's landmark objects with the argument that settles
the whole information question: *the player can already see each moment in time when they scrub.*

## 1 · What the mechanic actually asks

Both lineages collapse to **one axis**: reach further back → stronger effect, but more of what just
happened is thrown away and re-simulated.

- **Borrowed Future:** `carried = max(minimum, manaNow − manaThen)`, filled to ManaMax, overflow →
  Shield. `manaThen` moves with the anchor, so the reach genuinely changes the payoff.
- **Recall:** the effect is fixed (deployment hex, Disarm 15/25 ticks). The reach only decides how
  much of the re-run fight the enemy spends displaced.

Two facts from the sim that shrink the UI further:

1. **Units never resurrect**, so any ally alive at the present was alive at every earlier tick.
   Target legality is effectively anchor-independent (`Omitted` is the only edge case). Target and
   time do not constrain each other — no ordering, no clamping, and the "cannot be reached at −4s"
   message is nearly dead code.
2. **The cost half needs no UI.** Turning the dial rewinds the board; the player watches their kills
   un-happen and dead units stand back up. That is the landmark rail, already built, in the engine.

### Required information, complete

| # | Fact | Where it lives |
|---|---|---|
| 1 | How far back am I | the dial's numeral |
| 2 | What am I giving up | **the board, rewinding** — no widget |
| 3 | What does my ability do, to whom | one cluster on the chosen unit |
| 4 | How do I commit | hold, and the ring fills |

Everything else in the shipped panel is deleted: step numbers, prompt paragraph, landmark rail,
per-anchor price row, readout sentence, target chips, both buttons.

## 2 · The interface

**The Hourstone.** A sand ring rising into frame from the bottom edge, notched `1s`…`4s` (`6s` with
Long Memory), a gold grab-knob on the ring, and one large `2s ROLLED BACK` in its eye. Turn it by
drag, `A`/`D`, or dpad. The board scrubs continuously to match — the smooth walk already landed today.

**The ability is named once, on the rim** — `BORROWED FUTURE` / `RECALL TO FORMATION` — and nowhere
else. That is the entire "which ability is this" budget.

**Commit is a hold.** Sand fills the ring; when it closes, the Hour splits. One irreversible action
per battle should cost a held beat, not a click, and the filling ring *is* the progress bar. `ESC`
cancels; in the final-chance state it reads `ACCEPT FATE` and the stone turns from amber to bone.

## 3 · Only the required context about the ability

One cluster, on the unit it happens to. Never a panel.

**Borrowed Future** — `samples/b-r3-borrowed-future.png`. Over the champion's head: the carry as the
hero number (`+33`), a mana orb showing the result (`40 · MANA FULL`), and the overflow drawn as a
white shield arc breaking out of the orb (`8 SHIELD`). The cap-and-spill rule is shown by the shape,
not stated in a sentence. Eligible-but-unchosen champions get a quiet ring and not one word.

**Recall to Formation** — `samples/b-r3-recall.png`. A tether hauls from the enemy's current hex to
its deployment hex, where a dashed hex ring and a ghost show exactly where it lands, with
`1.5s CANNOT SWING` under it. "Where does it go" is answered by an arrow pointing at the hex.

**Before a target is chosen** there is no cluster at all — just the dial and the rewinding board.
That is the study phase, and it should be silent.

## 4 · The one thing the dial owes the player

The payoff is **not monotonic in the reach.** `carried` is maximised where `manaThen` is *lowest* —
which is immediately after the champion spent its own Mana. So the best return is often "just after
my caster fired", and it can be a worse deal one second deeper.

The shipped panel solved this with a price row under every anchor. The dial should solve it without
a table: **let each notch's brightness and size carry its payoff.** The sweet spot then reads as a
bright bead on the stone, found by eye and confirmed by turning, with no numbers to parse. This is
the one addition I would make beyond the mockups, and it is the piece I am least able to validate
from a still.

## 5 · States still owed

- **Reduced Motion:** the dial and the cluster stay; the board cuts between anchors instead of
  walking. Same information, no spatial playback.
- **Occlusion:** the dial occupies bottom-centre. The existing `RevisionDockSide` logic should become
  "retract/dim the stone when an eligible target sits under it".
- **Phone layout:** the ring shrinks and the notch labels drop; the numeral does not.

## 6 · Honest risks

- Dropping the landmark rail bets everything on the scrub being legible. It is the right bet given
  the walk now plays smoothly, but it is a bet, and only Play Mode settles it.
- The dial eats prime bottom-centre space that the board's foreground lip currently occupies.
- Notch-brightness-as-payoff is unproven and may read as decoration.
- Mockup nit: the rim label sits behind the grab-knob at some angles; it needs an arc layout or an
  offset in the real build.

## 7 · Ask

Approve by exact sample name — `b-r3-borrowed-future.png` and `b-r3-recall.png` — and I will write
`implementation/spec.md` and build it. Nothing under `client/` has been touched.
- 2026-07-29 — Jake approved both r3 samples. Status `APPROVED_FOR_IMPLEMENTATION`; spec written to
  `implementation/spec.md`.
- 2026-07-29 — **Implemented.** The docked draft panel, landmark rail, price row, readout, target
  chips and both buttons are gone; the Hourstone and the on-unit ability cluster are in.
  `implementation/spec.md` holds the contract.
  **Verified:** headless client compile · Unity Editor compile clean · 533/533 sim tests ·
  semantic layout probe of the live panel (notches resolve onto the 250px arc at the intended
  angles, payoff drives notch diameter 16.25/26.25/20/25px, knob lands on the selected notch,
  numeral + lineage + contextual hint correct, 40 sand beads, Long Memory yields 6 notches, Recall
  shows tether/home/disarm and hides the Borrowed-Future items).
  **NOT verified: how any of it looks.** The remote Game View stopped repainting in this session —
  `ScreenCapture.CaptureScreenshot` never writes (its `WaitForEndOfFrame` runner stays alive
  forever) and a `PanelSettings.targetTexture` read comes back 100% transparent, while
  `Time.frameCount` climbs normally. Colour, contrast, legibility over the board, and the
  approved-sample comparison are all outstanding.
  **Two bugs the probe caught:** the per-frame cluster refresh cleared the fixture's cluster every
  frame (real code was right; the fixture needed a suppression flag), and the fixture placed the
  cluster in screen pixels where the live path uses panel units.
- 2026-07-29 — **Jake played it.** Four notes: overhead too big · dial control wonky, drag poor,
  wants any second mark clickable · rewind still not legible · wants a pause on the fork.
  Captured the live build (`work/live-r1.png`) and the cause of note 1 is arithmetic: the mockups
  were authored at **2560x1440** and the UI panel reference is **1600x900**, so every pixel value
  lifted from them shipped **1.6x oversized**. Whole dial + cluster block rescaled /1.6, hero
  numbers trimmed further (carry 118→54, dial numeral 146→78, disarm 104→52).
  Note 2: the notch bead was 11–26px and *was* the click target. Split into a 54px invisible hit
  area with the bead drawn inside, plus hover state and tooltip. `HOLD ⏎` was overlapping
  `ROLLED BACK` (hint lifted clear) and the knob was covering the lineage name (moved above the arc).
  Note 4: new `revision.forkHoldSeconds` (0.55) holds ON the branch frame before the landing punch —
  the split was previously struck and resumed inside ~0.3s.
  Note 3 answered with three new prototypes: `samples/r1-undertow.png`, `samples/r2-two-timelines.png`,
  `samples/r3-the-fork.png`. Status back to `AWAITING_REVIEW` for those three.
- 2026-07-29 — **R1 + R3 built.**
  *R1 Undertow:* `SetRevisionRewindEchoes` now trails EVERY living body, not just the Revision's
  targets — one unit moving backwards is ambiguous, the whole board moving backwards is not. Targets
  keep the lane colour and full weight; the crowd is dimmer and smaller. Units alive at the scrub
  position but dead at the witnessed present get their own bright white ring: a body coming back is
  the loudest proof time is reversing. New `SetRevisionSand` rains motes UPWARD across the board as
  a global signal that survives frames where nobody moves (hashed scatter, stable per index).
  Performance note: the old shape re-folded the whole battle once per echo, which was fine for two
  targets and is not fine for a full board — it now folds once per sample depth and reads every unit
  out of that fold.
  *R3 The Fork:* the 0.55s `forkHoldSeconds` beat now draws two rings out of the split — a bright one
  blooming for the Hour that runs, a grey one collapsing for the Hour being discarded.
  **Verified:** headless compile · Unity Editor compile clean, 0 console errors/warnings · 533/533
  sim tests. **Not verified: any of it in motion.** The Game View repaint has been intermittent all
  session, so no capture of the corrected sizing or of either new effect.
