# UI review: workbench-refactor

Status: IMPLEMENTED
Created: 2026-07-28

## Brief

- Screen or flow: Workbench (out-of-combat management screen — market, dossier, armory
  drawer, persistent warband rail).
- Primary player decision: "what do I buy / change about my warband before the next fight?"
  The market offers and the warband rail are the two decision surfaces; the dossier is the
  disclosure surface that serves both.
- Required information and actions: act/beat position, Sand balance, CHOOSE NEXT WAGER
  commit; 6 live market offers + reroll (with cost); full per-kind dossier for the selected
  card (stats, Signature, basic attack, passive, actions: forge/reserve/dismiss); warband
  rail with field slots (incl. locked), reserve, armory chip; equip/transfer drag flows;
  blocking choice modal (spec awakening / interlude / boss reward) unchanged.
- Required states: no selection (dossier empty state), market card selected, owned unit
  selected, armory drawer open (footer rail + dossier must stay visible — equip flow needs
  item source and socket targets simultaneously), sold-out offer slots, locked field slots.
- Target viewport/aspect ratio: authored at the shell's 1600×900 reference (1280×720 floor,
  no scroll regions). Screenshot source is 2545×1310 (~1.94:1).
- Must preserve: per-kind dossier formats and section-role law (`Design/workbench-dossier.md`);
  Sand cost never hover-only; market never yields to the drawer; footer rail visible while
  the drawer is open; keyboard/drag parity on the rail; semantic accent colors; blocking
  choice modal; no ScrollView.
- May change: overall region layout (rows → columns), header height/content, market card
  size/shape, reroll placement, market heading strip (delete), warband rail card size and
  contents, dossier width/aspect (wide two-column → narrow vertical column).

## Jake's directive (2026-07-28, chat)

1. Market/dossier stop stacking as full-width rows; the dossier becomes a right-side column,
   ~30% of screen width, using the full body height.
2. The header is far too thick — compact it significantly.
3. Spend the reclaimed space on bigger footer unit cards. Cards keep weapon/trinket slots and
   gain: an active-ability (Signature) slot + one slot per tier-up perk the unit will
   eventually pick. Empty slots visible = the progression promise ("fill out their cards").
4. Kill the market heading strip ("REROLL + 5 LIVE OFFERS · SELECT FOR FULL DOSSIER") —
   it spends vertical space the cards need.

## Inputs

| Source | Role |
|---|---|
| `inbox/workbench-refactor/Screenshot 2026-07-28 181102.png` | Current UI at 2545×1310 — content, structure, and style baseline |
| `Design/workbench-dossier.md` | Section-role law, per-kind dossier formats, drawer constraints (still binding) |
| ADR 0011 + `Warband.Run/IRunContent.cs` | Rank ladder C→B→A→S; `SpecOptions(chassis, rank, path)` offers a 1-of-2 pick at B, A, and S → exactly three perk slots per card |
| `WarbandBarView.cs`, `WorkbenchView.cs`, `WorkbenchStyles.uss`, `WarbandBarStyles.uss` | Current implementation and authored dimensions (1600×900 reference) |

## Height budget at 1600×900 (the shared frame both samples use)

| Region | Today | Proposed | Notes |
|---|---|---|---|
| Header | 88px | 46px | One row: title+act/beat left, Sand + wager right; brief sentence deleted (moves to dossier empty state) |
| Market heading strip | 44px | 0 | Deleted; reroll relocates into the market column |
| Body (market + dossier) | 244 + ~384 stacked | ~624 shared columns | Dossier column ~466px (30%) full height; market gets the rest |
| Warband rail | 122px | ~186px | Hero cards 88×100 → ~148×166 with kit + path slot rows |

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `01-shopfront.png` | Market as a 3×2 card grid (art-forward), vertical reroll rail on the market's left edge, dossier right 30% | Offer art nearly doubles (96→~170px); reroll spends horizontal space, not vertical; keeps the established card language | 3×2 grid means two visual rows again (though inside one column); vertical REROLL rail is a new control shape | Slot icons, exact paddings, and font are illustrative; region geometry and card contents are literal |
| `02-ledger.png` | Market as a single-column ledger of 6 horizontal offer rows + slim reroll row; dossier widens to ~34% | Fastest scan of all 6 offers; most width for the dossier; reroll costs only 44px | Loses the art-forward shop feel; rows read closer to a menu than a market | Same |
| `01-shopfront-drawer.png` | State capture: armory drawer open in sample 01 — drawer band rises above the rail spanning the market column; dossier + rail stay fully visible | Proves the equip-flow constraint survives the column layout | — | Same |

Both samples share the identical header, dossier column, and footer rail so the review
isolates one hypothesis: what shape the market takes inside its column.

### Footer card anatomy (both samples)

~148×166: portrait (rank badge overlaid) → name band → two slot rows:
- **Kit row** (what it fights with): SIGNATURE · WEAPON (tier pip) · TRINKET
- **Path row** (what it becomes): B · A · S diamonds — filled with the chosen spec icon,
  empty = ghosted letter. A rank-C unit shows three empty diamonds; every rank-up fills one.

## Assumptions

- The header brief sentence ("Read the warband as cards…") is tutorial copy, not state; it
  moves to the dossier's empty state rather than surviving in the header.
- "SELECT FOR FULL DOSSIER" is obsolete in a layout where the dossier is permanently visible
  beside the market; selection affordance is card highlight + dossier response.
- Sold offers keep their slot (SOLD receipt card) so the grid doesn't reflow.
- The Signature slot on footer cards is disclosure (tooltip → exact rule), not an equip
  target; only W/T remain drag targets. B/A/S diamonds reuse the existing spec-badge
  tooltip.
- Reserve tiles and the armory chip scale up with the rail but keep their structure.

## Jake review — round 1 (2026-07-28)

1. Preferred: **01-shopfront** ("for sure"); dossier width right at 30%.
2. Must keep: column structure as sampled.
3. Changes requested → r2:
   - Dossier must show the champion's **selected tier-ups** (the B/A/S picks).
   - Unit offers must show **what tier is being sold**; later in the run recruits arrive
     with previous tier-ups pre-selected — design card + dossier for that now.
   - Armory: not a band in the market column — a **floating rack popup** that grows out of
     the footer ARMORY chip (tall vertical rack, hover/drag from it, ✕ to close).
   - Open to **style stabs** on the same structure; keep noodling.

## Samples — round 2

| Sample | What it tests | Notes |
|---|---|---|
| `01-shopfront-r2.png` | Round-1 changes on the chosen direction | Dossier gains a PATH section (real content: Shade's Killer's Tempo + ghosted "AWAKENS AT RANK A · THE FORK" / "AWAKENS AT RANK S"); Berserker offer carries `RANK C ◇◇◇`, Banneret rank-up carries `C → B ◆◇◇` — the same diamond language as the rail cards, which is also the pre-specced-recruit design (higher-tier offers show filled diamonds; their dossier PATH lists the picks they arrive with) |
| `01-shopfront-r2-rack.png` | The floating armory rack | Grows from the ARMORY chip, 244px wide; market fully untouched. Honest trade shown: while open it covers the dossier's right edge (stat chips / action row). Alternative if that bothers: anchor it one column left, over the market's right edge |
| `03-style-brass.png` | Style stab — Hourstone brass | Same structure; warm umber panels, brass hairlines, serif display names (Georgia as placeholder). Materially warmer, closer to the Tower/Hourstone fiction |
| `04-style-obsidian.png` | Style stab — obsidian glass | Same structure; near-black glass panels, hairline borders, 10–14px radii, pill buttons, luminous gold. The hall-polish obsidian language brought indoors |

## Jake review — round 2 (2026-07-28)

1. Style: **obsidian** ("liking the obsidian styling more I think").
2. Rack: fine as sampled (bottom-right anchor, covers dossier edge while open).
3. PATH rows: great as rows; icons hover to full tooltips.

## Candidate for approval

| Sample | Contents |
|---|---|
| `05-shopfront-obsidian.png` | The consolidated candidate: shopfront structure (46px header · 3×2 market grid + vertical reroll rail · 30% dossier column with PATH section · 186px rail with kit/path slot rows) in the obsidian glass style |
| `05-shopfront-obsidian-rack.png` | Same, armory rack open — rack panel obsidianized (glass, hairline, glow) |

Awaiting explicit approval of `05-shopfront-obsidian` (+ its rack state) before writing
`implementation/spec.md` and starting the build.

## Approval

- Approved sample: **`05-shopfront-obsidian`** (both states: base + rack), by Jake in chat
  ("LGTM! … otherwise I think we are set!").
- Conditions: **the node map stays in the top header** — the act's beat track
  (`PlanningModel.Track`) rendered as pips beside ACT n/3; captured in
  `05-shopfront-obsidian-r2.png` / `-r2-rack.png` (the approved visual record).
- Date: 2026-07-28.

## Review log

- 2026-07-28 — Job created.
- 2026-07-28 — Brief established from Jake's chat directive + screenshot; two coded
  structural mockups + one drawer-state capture prepared (Claude lane, rendered headless
  at 1600×900).
- 2026-07-28 — Samples rendered and self-reviewed: drawer state confirms market + rail +
  dossier stay simultaneously visible; ledger variant fits 6 offers + reroll in 632px with
  a 520px dossier. Known mockup jank (not design): item-tile glyphs are placeholder emoji;
  fonts approximate the game face. Status → AWAITING_REVIEW.
- 2026-07-28 — Round 1 feedback: 01-shopfront chosen, 30% dossier confirmed. r2 built
  (dossier PATH section, offer tier strips incl. the pre-specced-recruit design, armory
  drawer → floating rack) + two style stabs (brass, obsidian) on the identical structure.
  Fixture consistency fix: armory count 0→3 to match the rack contents. Status stays
  AWAITING_REVIEW (round 2).
- 2026-07-28 — Round 2 feedback: obsidian style, rack as sampled, PATH rows confirmed.
  Consolidated candidate `05-shopfront-obsidian` built (r2 content + obsidian style incl.
  the rack panel) and rendered in both states. Awaiting explicit approval by name.
- 2026-07-28 — APPROVED (Jake: "LGTM", condition: node map stays in the header; captured
  as `-r2`). `implementation/spec.md` written; roadmap item 33 → BUILD.
- 2026-07-28 — Build landed (client-only): Workbench.uxml + WorkbenchStyles.uss rewritten
  to the column/obsidian structure (46px header + beat-track pips from `PlanningModel.Track`;
  vertical reroll rail; % market grid — USS has no grid layout, flex-wrap carries it);
  InspectorPanel permanent narrow form + PATH section (real `RankTierSlotModel` rows,
  authored one-liners, fork rank named); offer tier strips (`TierLabel` + pips incl.
  rank-up pending state); rail 186px progression cards (signature disclosure slot +
  W/T + B/A/S path row replacing the floating spec badges, drag/keyboard parity intact);
  armory drawer → floating rack (same state/actions/cues); layout contracts rewritten
  (columns, rack-never-covers-market, node-map presence, path-slot containment).
  Engine deviations vs the PNG as predicted: no box-shadow/gradients in USS → glow/scrim
  became border/tint treatments. `make check-client` PASS ×4. Unity full Workbench matrix
  launched (run 20260728-191934) — verification in flight.
- 2026-07-28 (late) — VERIFIED. Nine fix batches against the full 70-item / 5-viewport
  matrix: 0 → 12 → 17 → 64 → 69 → 70/70 PASS ×3 (runs 212510/212726/212936) + live
  rank-up regression PASS + Unity console clean. The decisive findings: (1) Unity's default
  theme puts ~4px top+bottom margins on every Label — in the column's tight rows that was
  the majority of all overflow; (2) the base sheet's partial-width `wb-inspector__column`
  survived from the two-column era and silently narrowed all rule text (caught by eye, not
  by contract — wrapping inside a wrongly-narrow label is legal to the checks); (3) the
  matrix leaves play mode on and Syncthing lies about freshness (see memory:
  unity-matrix-drive-gotchas). 4:3 concession: the rail keeps the slim legacy card (no
  kit/path rows — eight addresses don't fit them at 4:3; tooltips carry the facts).
  By-eye pass vs `05-shopfront-obsidian-r2`: structure matches; fonts/glyphs/flat-glow are
  the recorded engine deviations. Evidence: `implementation/impl-*.png` +
  `implementation/matrix-report-20260728-212936.md`. Unseen in motion — play-pass entry
  filed.
