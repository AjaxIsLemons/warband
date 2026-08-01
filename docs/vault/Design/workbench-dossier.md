# Workbench dossier & armory redesign — 2026-07-28 overnight build

Jake's directive (2026-07-27, before bed): the dossier is crowded; decide what information
matters, format it per card type, give owned-unit cards a proper shop view, and convert the
Armory side tab into a footer drawer. Research first, then build. This doc records the decision;
the research reports live in the session transcript, distilled notes under the session scratchpad.

## Why it's crowded — the diagnosis (from research + code)

Genre research (TFT, Hearthstone BG, Super Auto Pets, Backpack Battles, Mechabellum, CK3,
Slay the Spire, LoL) converges on three disclosure layers, each answering exactly one question:
**card = "do I want this?" · dossier = "what exactly does it do?" · keyword hover = "what does
that word mean?"**. Warband already has the card layer right (`OfferFactProfiles` 4-fact budget)
and the keyword layer right (`BindSemantic` + `RuntimeTooltipService`). The broken layer is the
dossier, for one structural reason: **section demotion is decided by pixel width and array
index, not by content role** (`InspectorPanel.cs` — secondary = `Recruit && i >= 2`; pagination
only under 1000px). At shipping widths everything renders at once: a recruit shows three full
rule paragraphs; a rank-up shows the ladder plus both exact spec options in full. That is the
exact failure Super Auto Pets shipped in 0.28 ("info is shown double… too cramped") and
Backpack Battles patched away ("show less redundant information… highlight trigger conditions").

## The laws this build follows

1. **Role, not geometry, decides what shows.** Sections carry `Primary | Deferred`. Width
   decides layout; it never decides what exists.
2. **Deferred ≠ hidden.** A deferred section renders as one compact line — icon, name, trigger
   label — with its full rule on hover (the existing tooltip idiom). The name is always visible.
3. **Stat block and prose never interleave.** Numbers in tiles/rows, rules in sentences.
4. **Delta and absolute share one view**: `HEALTH 164 → 188` rows, changed rows only.
5. **One fact, one channel.** Rank appears once. No synthetic filler (the "CARD TYPE / DETAIL"
   passive back-fill is deleted; empty scaffolding is suppressed per kind).
6. **Trigger → effect grammar** with the trigger visually distinct (section labels are the
   trigger; keep them).
7. **The modal stays for the irreversible choice only** (spec awakening — already built).
8. **Cost/affordability is never hover-only.**

## Per-kind dossier formats

Common chassis: identity column left (portrait, kind-colored eyebrow, name, subtitle, cost,
stat tiles ≤5) · detail column right (kind-specific) · action row pinned bottom (unchanged).

| Kind | Detail column — primary | Deferred (compact line + hover) |
|---|---|---|
| **Recruit** | SIGNATURE (name, mana, full rule) · BASIC ATTACK one-liner | PASSIVE · spec preview teaser |
| **RankUp** | changed-row deltas (`HEALTH 164 → 188`) · B/A/S ladder with "you are here" · both options as **name + change verb + clamped rule**, full text on hover ("Choose after purchase" stays) | — |
| **Weapon** | equip preview is PRIMARY: recipients row, CURRENT → OFFERED changed rows, LOSE/GAIN rules | weapon profile line · mastery |
| **Trinket** | trigger→effect rule · recipients/deltas | — |
| **Inscription** | the law as trigger→effect, SCOPE/DURATION chips, no stat tiles, narrower panel | — |
| **Champion (owned, from footer)** | same as Recruit + equipment rows (weapon + temper, trinket) | PASSIVE · chosen traits |
| **Capacity** | socket diagram + one line | — |

## Armory: side column → footer drawer

Today: closed = 190px dead side column; open = 790px panel that **replaces the market**.
New: the side column and the market-replacement behavior are deleted. The armory becomes a
**drawer band directly above the footer unit rail**, inside the workbench body column
(body becomes dossier-over-drawer; market never moves). Open/close is a class toggle with USS
transitions (the `InscriptionRailView` idiom), `DrawerExpand`/`DrawerCollapse` cues kept.
- The footer rail stays fully visible when the drawer is open — the equip flow (select item →
  click socket / EQUIP action) needs both at once. This is the one constraint research flagged
  as fatal if broken (Hades' boon-list failure).
- Closed: no workbench-side chrome at all; the footer ARMORY chip (already present, already
  wired to `OpenLoadout`) is the handle, and gains the item count it already shows.
- Open: one horizontal tile row (page of 6 + pager), instruction line, COLLAPSE. Dossier
  shrinks to fit; during equip flow it's showing the compact projected-unit format anyway.
- Escape closes (kept). State remembered across selection changes (kept — shell model owns it).

## Also fixed in this pass
- Comparison-row text overlap in the equipment preview (visible in `armory-full` capture).
- `accent--choice` emitted by `BuildRankUpDetail` but styled nowhere.
- Footer "RESERVE 2/2" mid-word wrap.
- `rail-full` header/market overlap (the 2-viewport known FAIL) — market row gets the
  reclaimed width and a flex-start grid.
- Mid-word text clipping under copy stress (wrap/ellipsis at line boundaries instead).

## Morning amendments (2026-07-28, Jake's review)
- **Rank-option previews get a fourth text tier: the authored one-liner.** Generated
  trigger-prose (`MechanicalRulePresenter`) runs 170+ characters without a sentence break
  (repro: Phalanx → Pikewall), so "first sentence" clipped mid-line. The preview now binds
  `ContentLexicon.Node(id).Text` — the authored scent line — and the full machine rule stays
  on hover and in the blocking choice. The `FirstSentence` fallback is budget-clamped at a
  word boundary for models with no authored summary. `market-rankup-long` is now the real
  Phalanx fork built from live catalog content, so the wrapped-text contract guards reality.
- **The peek strip is deleted; the footer ARMORY chip is the only handle** (which is what
  this doc specified — the strip was overbuild). The chip's third line is state-driven:
  `DROP TO UNEQUIP` while gear is armed/dragged, else `OPEN DRAWER ▴` / `CLOSE DRAWER ▾`.

## Unit-sheet amendment (2026-07-29, approved R5)

Recruit and owned-Champion inspection now share a dedicated five-region unit sheet. This
supersedes the Recruit/Champion rows above and laws 2, 3, and 6 only for those two kinds; item,
Rank Up, Inscription, Capacity, equipment-preview, and combat formats are unchanged.

1. **Health** is the current non-Weapon chassis block. There is no generic baseline subtitle or
   derived Signature clock.
2. **Weapon** owns its name, Power/Healing, attack interval, Range, Mana per completed hit,
   Crit/Cleave when present, and variable properties. Universal facts use one typed glyph/value
   row; a property keeps its concise effect visible and discloses the exact active/inactive rule
   on hover, keyboard focus, or tap focus.
3. **Signature** owns its name, Mana cost, icon, and complete rule. Weapon Mana generation never
   appears here.
4. **Passives** use one plural header, then name + self-contained rule per Passive. Trigger
   meaning lives in the sentence; there is no trigger-taxonomy header.
5. **Specs** are three fixed B/A/S addresses. Empty slots show only the letter. A selected icon
   replaces its letter and discloses the chosen Spec rule on hover/focus/tap.

At wide dossier widths, portrait identity and mechanics are side by side. Narrow layouts collapse
the portrait to a banner without changing the five-region order or hiding information. The exact
implementation contract and approved pixels live in
`docs/ui-reviews/outbox/unit-dossier-density/implementation/spec.md` and
`07-weapon-glyph-row-r5.png`.

## Explicitly not tonight
- No sim/content changes; no CardModel restructure (per-kind profile table over the existing
  bag); no MusterCard/draft-screen changes; no deletion of the dead ManagementView/ShopView/
  PlanningView trio (~2,300 lines, flagged for Jake — separate cleanup).

## Verification
`make check-client` → sync → `Warband/UI QA/Run Workbench Full Matrix` (70 items, 5 viewports)
+ by-eye reads of every changed state (the flex-shrink lesson: a green contract is not
evidence). Layout contracts updated to the new structure (pager checks out, drawer checks in).
