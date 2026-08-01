# UI review: interlude-cleanup

Status: AWAITING_REVIEW
Created: 2026-07-28

## Brief

- Screen or flow: the Interlude blocking choice — `WorkbenchView.BindBlockingChoice`
  rendering `PlanningBeat.Interlude` (`RunShell.BuildInterludeBeat`,
  `RunController.PreviewInterlude`).
- Primary player decision: take exactly one of up to seven fully-visible rewards,
  structured as a **three-way path choice** — Treasury (fixed Sand, certainty), Armory
  (one Worn weapon or trinket), Hourstone (one run-wide Inscription law). The roadmap
  calls this "a real three-way choice"; the current UI renders it as seven
  undifferentiated strips.
- Required information and actions:
  - All offers fully visible before choosing (design law: "Every offered reward is shown
    before you choose") · exact-rules language with keyword inlays.
  - Per offer: path, name, classification (WORN · SABRE / TRINKET / Run-wide rule), exact
    rule text; weapons also stats (POWER/CADENCE/REACH) + mastery; trinkets stat deltas;
    treasury amount.
  - The beat rule: "The choice also unlocks the next field capacity."
  - Action: choose one → beat resolves, routes to the matching Hall station.
  - Sibling beats share this scrim: SPEC AWAKENING, REVISION EVOLUTION, BOSS REWARD. A
    redesign must keep them working (restyle together or split Interlude out explicitly).
- Required states: 1 Treasury + up to 3 Armory + up to 3 Hourstone (Hourstone pool can
  shrink late-run) · 130% expanded text · forced-phone · reduced motion.
- Target viewport/aspect ratio: 2556×1317 primary · 1024×768 min desktop · 1920×1080
  forced-phone.
- Must preserve: full pre-choice visibility of every offer; exact-rules language; one
  choice resolves the beat; hall-polish semantic palette and laws (blue = choosing,
  Sand = advances/rewards the run, no neon station colors — path identity from symbol,
  geometry, grouping; no ambient motion behind rules text).
- May change: layout, grouping, card anatomy, modal-vs-screen, interaction shape
  (single-click vs select→commit), how sibling beats are handled.

## What is broken today (from the inbox screenshot)

1. **Title overflow collision** — card titles neither wrap nor shrink; "Censer",
   "Deepwell Reliquary", "Bronze Testament", "The Wound Clock", "The Third Chime"
   overlap into an unreadable smear across card boundaries.
2. **Seven equal strips hide the three-way structure** — ARMORY/HOURSTONE repeat as card
   eyebrows instead of grouping; Treasury reads as a seventh sibling, not a path.
3. **One-word-per-line copy** — ~130px columns force the rules text vertical.
4. **The established card grammar is thrown away** — the models carry full card anatomy
   (glyph, accent, subtitle, trigger/rule blocks, stat chips, mastery, tier tags via
   `WeaponCard`/`TrinketCard`/`InscriptionCard`) but `Choice()` renders only
   eyebrow + title + summary as a flat text button.
5. **No hierarchy between "certainty" and rich cards** — +5 SAND gets the same shape and
   weight as a run-law.
6. Fixed 1180px panel, oversized header, dead space below the cards.

## Inputs

| Source | Role |
|---|---|
| `inbox/interlude-cleanup/Screenshot 2026-07-28 180634.png` | Current state, content seed (Act 1 interlude: +5 Sand · Officer's Sabre · Censer · Deepwell Reliquary · Bronze Testament · The Wound Clock · The Third Chime) |
| `client/Assets/Resources/UI/WorkbenchStyles.uss` (1339–1462) | Current scrim/card styling |
| `docs/vault/Design/hall-polish.md` | Semantic palette + visual laws |
| `sim/Warband.Content/{Catalog,Weapons}.cs`, `SkirmishProof.MasteryCopy` | Real names, numbers, rules, mastery copy |

## Assumptions

- No Codex material arrived in the inbox; samples are Claude coded mockups (structural
  prototypes rendered headless), not raster art. Codex `$imagegen` can follow later if a
  mood pass is wanted.
- Path identity is carried by glyph + geometry (per hall-polish anti-goals), not color.
- A select→commit step is a legal hypothesis (sample 02/03); current one-click choose is
  preserved in sample 01.
- Sample 03's compact row chips (MEND, OPENER, ON ALLY HIT, EVERY 3RD CAST) are invented
  compressions for the mockup — the real vocabulary would come from the presentation facts.

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `samples/01-three-paths.png` — Evolution, keep the modal | Grouping the seven offers into three labeled path frames + restoring the real card anatomy fixes the screen | Smallest change that fixes everything broken; sibling beats inherit the restyle for free | Still a modal over the market — the quiet Hour keeps shop noise behind it | Backdrop dimmed workbench is schematic; fonts/icons stand-in; content real |
| `samples/02-quiet-hour.png` — Structural, own full-screen beat | An Interlude is a run beat like Wager/Deploy and deserves a dedicated screen; select (Tower blue) → one Sand commit ("SEAL THE HOUR") | Ceremony matches fiction; select-then-commit kills misclicks on a 7-way irreversible choice; stacks cleanly on phone | New screen chrome + a second click; sibling beats need their own answer or stay on the old scrim | Fonts/icons stand-in; content real |
| `samples/03-hour-ledger.png` — Wildcard, no modal at all | Reuse the Workbench's proven select→inspect→commit loop: grouped offer rows in the rail, full dossier in the inspector, commit where TO THE BREACH lives | Maximum reuse (rows + inspector + commit exist and are QA-covered); inspection depth for free | Least ceremonial — the Interlude reads like more shopping; market/roster chrome stays visible | Fonts/icons/portraits stand-in; content real |
| `samples/04-free-market.png` — **Jake's direction** (r2): the Interlude IS the shop | The Live Market deals the interlude rewards as FREE offers in the normal tile row + dossier; taking one seals the Hour and the normal paid stock is dealt | Zero new UI grammar — tile row, dossier, action strip all exist; the "after" state is just the current market screen | FREE badging must not read as a discount gimmick; reroll must hide during the free pick; sibling beats still need the old scrim | Mirrors `ui-qa …-2556x1317-workbench-market-recruit.png`; glyph art, fonts, tier-bar copy illustrative; names/rules/stats real |

Samples 01–03: rejected 2026-07-28 (kept for history).

## Jake review

1. Preferred sample, combination, or reject all:
2. Must keep:
3. Most important next change:

## Approval

- Approved sample:
- Conditions:
- Date:

## Review log

- 2026-07-28 — Job created.
- 2026-07-28 — Inbox screenshot received; brief written; three coded-mockup samples
  generated and rendered headless at 2554×1303. AWAITING_REVIEW.
- 2026-07-28 — Jake: **rejected all three.** New direction: the Interlude takes the form
  of the shop — the Live Market tile row deals the interlude rewards as **free** offers;
  once one is taken, the normal (paid) market stock is dealt. Sample
  `04-free-market.png` built to test that form, mirroring the real market composition
  from `client/McpCaptures/ui-qa-20260728-095949-2556x1317-workbench-market-recruit.png`.
  Implementation implications (for the eventual spec, recorded here so they aren't lost):
  - The blocking scrim disappears for Interlude beats; `PlanningBeat.Interlude` instead
    projects the preview offers into the market rail as zero-cost offers.
  - The "after you pick" state needs no new UI — it is the existing market screen; the
    market deal is simply gated behind the free pick at Interlude beats.
  - Reroll is hidden during the free pick (interlude offers are deterministic and fully
    visible); it returns with the normal stock.
  - Sibling beats (Spec Awakening, Revision Evolution, Boss Reward) keep the existing
    scrim for now — splitting Interlude out is explicit in this direction.
- 2026-07-28 — Jake steer on 04: "not quite." The point is **literal reuse of the
  MarketOfferCard component** (UI/MarketOfferCard template + select→dossier→action-strip
  loop), not lookalike tiles — and the pattern generalizes: pretty much every post-round
  blocking choice (Interlude rewards, Revision tier-up, Boss Reward, …) should present its
  options as free market offer cards dealt into the same rail, replacing the bespoke
  choice scrim. Awaiting confirmation of restated model + Spec Awakening boundary.
