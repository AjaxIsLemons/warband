# UI review: workbench-frame

Status: IMPLEMENTATION_CANDIDATE
Created: 2026-07-28

Item 34 on the board. Jake's ask (2026-07-28 evening chat): fold the MUSTER screen and the
starting-revision draft into the workbench so the workbench is THE out-of-combat frame; the
unit rank-up 1-of-2 spec pick moves OUT of the inline dossier ladder into its own modal —
the one interruptive chrome. Direction was agreed in chat ("lets do it"); this is a
single-direction confirmation round, not a comparison set — two states of the already-approved
`workbench-refactor/05-shopfront-obsidian` frame.

## Brief

- Screen or flow: (a) run-start muster as a workbench state; (b) rank-up spec choice as a
  modal over the workbench.
- Primary player decision: (a) which 3 of 5 champions start the run; (b) which of 2 spec
  nodes to bind at a rank-up (permanent for the run).
- Required information and actions:
  - Muster: 5 candidate cards (role, portrait, tier strip RANK C ◇◇◇), full dossier for the
    selected candidate (stats, signature, basic attack, passive, PATH), pick/unpick up to 3,
    seed reroll (free), BEGIN RUN gated on 3 picks, rail shows the 3 selection slots as
    progression cards filling in.
  - Rank-up modal: hero identity + C→B context, the flat stat bump (real: Phalanx +30
    HEALTH · +2 POWER), the two options with real rule text, bind action per option; the
    hero's progression card is center-stage so the pick visibly fills the B path slot.
- Required states: muster mid-selection (2/3 picked — selected cards, empty third slot, gated
  button) is the rendered state; ready state = BEGIN RUN lights gold. Modal renders with one
  option hovered.
- Target viewport/aspect ratio: 1600×900 reference (same as the approved frame).
- Must preserve: the approved obsidian frame — 46px header with track pips, market 3×2 +
  vertical reroll rail, 30% dossier column, 186px progression-card rail. Semantic colors,
  diamond tier language, PATH row idiom.
- May change: header title/meta text per state; commerce row content (selection state instead
  of price at muster); rail identity block copy; armory chip visibility (hidden at muster).

## Inputs

| Source | Role |
|---|---|
| `../workbench-refactor/work/05-shopfront-obsidian.html` | The approved frame — structure and style base |
| `../workbench-refactor/implementation/spec.md` | Built geometry the mockups must respect |
| `UnitPresentation.json` + `Warband.Content/Kits.cs`, `Weapons.cs`, `ContentLexicon.cs` | Real content: roles, ability copy, Bulwark stats, Phalanx fork (Pikewall/Lancer) |

## Assumptions

- Muster candidates carry no price: the commerce row shows the tier strip + slot state
  (`✓ SLOT n` when picked). Sand chip hidden at muster (nothing to spend), armory chip hidden
  (nothing stored). Reroll rail = free seed reroll ("NEW FATES").
- Track pips render all-future with node 1 current — the act exists before the warband does.
- The 6th market cell renders as an empty dashed ghost (offer is 5; grid is 6).
- Starting-revision choice needs no mockup: it reuses the existing choice-scrim idiom
  verbatim as beat #0 (already built and approved in workbench-refactor).
- Rank-up modal replaces the inline dossier ladder *decision*; the ladder stays as
  information. Modal copy uses the real lexicon text; "THE FORK" appears only at ForkRank.

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `01-muster-state.png` | Muster works as a workbench state: market = candidates, dossier = inspection, rail = slots filling in | One UI grammar; the "fill out their cards" promise starts at second zero; MusterCard/RecruitView retire | Ceremony loss (mitigated by title/dressing); selection-mode market needs clear picked-state | Portraits/copy real; seed value + exact empty-cell dressing illustrative |
| `02-rankup-modal.png` | The 1-of-2 spec pick earns interruptive chrome: hero card center, options flank, pick fills the slot | The run's build forks get weight; dossier ladder becomes purely informational | Modal fatigue if rank-ups are frequent; must not fire mid-browse without cause | Phalanx fork content real (Kits.cs/lexicon); glow/blur treatments illustrative (engine: flat stand-ins) |

## Jake review

1. Preferred sample, combination, or reject all:
2. Must keep:
3. Most important next change:

## Approval

- Approved sample: `01-muster-state.png` AND `02-rankup-modal.png` ("Both look great! …
  Love all of this, lets build it!")
- Conditions: the rank-up modal must be staged as a BIG moment — leave room for a large
  entrance animation, "a dopamine shot". Build the entrance choreography (with a
  reduced-motion fallback) rather than a plain visibility flip.
- Date: 2026-07-28

## Review log

- 2026-07-28 — Job created.
- 2026-07-28 — Direction pre-agreed in chat (item 34): muster + starting revision fold into
  the workbench frame; rank-up goes modal; Wager fold-in explicitly out of scope. This round
  confirms the two new visuals against the real frame before spec.
- 2026-07-28 — Jake approved both samples in one pass. Condition: rank-up modal entrance is
  a big animated moment (dopamine shot), reduced-motion respected. → Spec + build.
- 2026-07-29 — Unity implementation candidate captured at 1600×900, 1920×1080, and
  2560×1440. The definitive Workbench matrix passed 95/95 with the live pending-rank
  regression green; the live Muster → first Revision → Wager seam passed; `make check-client`
  reported 0 errors; 534/534 headless tests passed; Unity reported 0 warnings/errors.
  Matched overlays/diffs and the engine deviation ledger are in `implementation/`.
  Awaiting Jake's review of the actual Unity captures; this job is not self-marked accepted.
