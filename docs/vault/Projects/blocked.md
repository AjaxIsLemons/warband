# Blocked — what's waiting, on whom, and what unblocks it

**Why this file exists.** The roadmap's actionable-only law (Jake, 2026-07-28) says every numbered
item is DESIGN or BUILD/SPEC'D and nothing parks there. That's right — but work that is genuinely
blocked then has nowhere to live, so it either clutters Now/Next or disappears. This is the register:
real, still-wanted, currently **cannot move**, with the exact thing that unblocks it.

**Rules.** Every entry names its **gate** and its **unblocker**. Nothing lands here to avoid doing it
— "not now" belongs in the roadmap's Deferred list. When a gate clears, the item goes back on the
board or straight to Done. Sessions read this before saying "what's next," because an item here is
not an answer to that question.

Last swept: **2026-07-30**.

---

## 1. Gated on Jake at the machine (he's away until Sat 2026-08-01 night)

**Item 31 — run pacing + decision economy.** DESIGN, evidence-first.
- **Gate:** one fresh baseline run and its telemetry. Only Jake can produce it.
- **Blocked twice over:** the instrumented build has *never reached him*. His last trail
  (`~/warband-runlogs/2026-07-29.jsonl`) contains start / one Fraying loss / defeat and **zero phase
  records**, while `RunTelemetry.cs:123` emits them and `RunTelemetryTests.cs:114` asserts them.
- **Unblocker:** cut a build (`make release`), then one 45–60 min sitting.
- **Also unblocks:** item 34's acceptance and most of the play-pass, in the same run.

**Item 34 — workbench as THE frame.** BUILD, implementation complete.
- **Gate:** Jake's actual-Unity visual acceptance (the UI-review workflow's final gate).
- **Already green:** 95/95 Workbench matrix, 534/534 headless tests, client compile clean, live
  Muster → first Revision → Wager seam, QHD/1080 evidence packaged.
- **Unblocker:** same build + sitting as item 31.

**Play-pass backlog — 31 watch items across 8 surfaces, none seen in motion.**
- Combat inspection's in-fight card (the QA matrix has no live-fight surface), the Revision
  presentation, the Workbench column refactor, item 30's fight feel and ears, the new enemy bodies,
  the unit HUD pass, and 2026-07-30's rim dressing + void backdrop.
- **Gate:** Jake is the only Play Mode verifier and his passes are scarce.
- **Risk being carried:** this violates his own rule about not queueing several unverified visual
  surfaces behind one pass. It got worse on 2026-07-30 — I added two more.
- **Detail:** `Projects/play-pass.md`.

---

## 2. Unowned findings — real, no item, would otherwise rot

**Render nondeterminism in `enc-the-drop` at tick 24.** Two identical renders differ by ~86 pixels
at <1/255 — sub-visual, nothing above a 2% threshold. **Attributed and NOT caused by item 35:** an
off/off vs on/on control showed both pairs differing by exactly 85.67. Found 2026-07-30 while
gating the void backdrop.
- **Why it matters:** "byte-identical contact sheet" is the project's commit gate, and it is not
  strictly true for every fixture/tick. A future real regression could hide under this noise.
- **Unblocker:** someone decides whether to chase the mechanism or formally tolerate a threshold.

**Heal carries no `Cause`, so Boon pulses never fire.** Known-dormant, one-line fix when wanted.
Currently only recorded on `play-pass.md`, which is the wrong home for a code defect.

**`rim.tint` cannot desaturate.** It's a colour multiply, so it darkens and cools but can't reduce
saturation — the KayKit Hexagon atlas stays inherently warm terracotta against a cold void.
- **Unblocker:** Jake's eye in motion. If it reads too warm, the fix is a saturation knob in the
  shader, not more darkening.

**457 uncommitted files; last commit `fcc0731`, 2 days old.** Spans several sessions' work — items
13, 32, the Revision presentation, VfxLab, the revision SFX bake, and 2026-07-30's item 35.
- **Gate:** not mine to commit unilaterally; much of it is other sessions' in-flight work.
- **Risk:** no clean fallback point if a build goes wrong.

---

## 3. Gated on evidence that doesn't exist yet

**Spec offers are static in a randomised game.** All 39 live `Offer()` rows are 2 entries with
`SpecChoices = 2`, so every rank-up shows the whole pool — the pattern that killed Underlords'
talent trees. The draft machinery already exists and is dormant only because pools are size 2;
growing a row to 4 makes it a real seeded draft with **zero code**.
- **Gate:** playtest #1. Warband is PvE and a knowable tree may serve the system-breaking north
  star, so this is a hypothesis, not a defect.
- **Detail:** `Design/events-and-inscriptions.md` §4b, and the roadmap's design backlog.

**Item 35 Stage 3 — per-act era dressing.** Deferred to the content pass by design.
- **Standing finding:** do **not** re-attempt a skybox/cubemap backdrop. Skybox models emit
  ground+horizon+sky and structurally cannot express "nothing beneath you" — proven at the cost of
  one generation on 2026-07-30. And framing, not art, is the blocker on void work: the board fills
  the dialed camera so completely that a backdrop reaches only a 1202×136 top strip at ≤34/255.
