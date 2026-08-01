# UI review: unit-dossier-density

Status: IMPLEMENTED_UNITY_VERIFIED  
Created: 2026-07-29

## Brief

- Screen or flow: Workbench, shared right-hand unit dossier for selected Recruit offers and
  owned Champions.
- Primary player decision: **Is this recruit worth its Hourstone cost and one scarce roster
  slot for the warband I am building?**
- Required information:
  - identity: offer kind, chassis, role/mechanic family, rank, portrait;
  - economy: cost and affordability;
  - baseline body: Health;
  - weapon-owned attack profile: damage, cadence, reach;
  - Signature loop: Mana gained per attack and capacity;
  - Signature effect;
  - Passive identity and trigger;
  - future or selected B/A/S Specs.
- Required actions: Recruit context uses Buy and Hold Stock; owned-Champion context uses its
  existing move/equip/sell actions.
- Required states: selected Recruit; selected owned Champion; unaffordable Recruit; held stock;
  C/B/A/S; locked or chosen Specs; composed/multiple Passives; owned Weapon temper and Trinket;
  long Signature/Passive copy; pointer hover, keyboard focus, and tap disclosure.
- Target viewport/aspect ratio: source is 2536×1310. The stable hierarchy must also survive the
  existing desktop matrix; geometry may switch at a dossier-width breakpoint.
- Must preserve:
  - exact composed game facts and semantic colour families;
  - portrait identity and rank escalation;
  - Signature → Weapon → Passives → Specs grammar;
  - pinned action dock and no ScrollView;
  - the existing shared `InspectorPanel` data source and per-kind formats.
- May change:
  - portrait geometry and size;
  - grouping/order of stat facts;
  - redundant copy and repeated values;
  - one-column rule flow;
  - use of icon, focus, hover, and tap detail.

## What the current dossier is trying to communicate

The information itself is reasonable. It is one identity, one price, six raw combat facts, two
active rules, one deferred Passive, a Spec promise, and two actions.

| Player question | Current facts |
|---|---|
| Who is this? | Recruit · Reaction · Phalanx · Rank C · portrait |
| Can it survive? | 150 Health |
| What does it do between casts? | Pike · 9 damage · 1.1s cadence · reach 2 |
| How does its cast loop work? | +11 Mana per hit · 35 Mana capacity |
| What happens when it casts? | Skewer · 3-hex line · 12 damage |
| What makes it distinct? | Riposte Passive |
| How does it grow? | B/A/S Spec ladder |
| What is the commitment? | Buy for 5 or Hold Stock |

The dossier does not clearly communicate the direct kit structure: **Phalanx has a durable body,
a Pike attack profile, a line Signature, Riposte, and a future Spec path.** It displays the
ingredients without grouping them by owner.

## Diagnosis

1. **Landscape space, document layout.** At 710px wide the dossier remains a single vertical
   article. The width becomes long dividers and gutters instead of parallel comparisons.
2. **Six unrelated stats are really three systems.** Health is the body; damage/cadence/reach
   are the weapon; Mana-per-hit/capacity are the Signature clock. Equal columns erase those
   relationships.
3. **One fact occupies several channels.**
   - damage/cadence/reach appear in the strip and again in the Pike sentence;
   - 35 Mana appears in the strip and Signature trigger;
   - cost appears on the offer, identity block, and Buy action;
   - Recruit and rank repeat across the offer and dossier.
4. **Hierarchy is inverted.** Large portrait/padding and the generic subtitle are guaranteed;
   Passive and Specs — the build-defining information — slide beneath the action dock.
5. **The subtitle carries no decision value.** “Basic attacks damage enemies” describes the
   default rule rather than Phalanx's role, target behaviour, or build identity.
6. **Icon implementation is costing legibility.** The current stat icons collide with labels.
   A proper icon-only strip would need stable glyph bounds and label tooltips; otherwise use
   grouped text.

## Disclosure contract

Always visible:

- chassis, rank, role/mechanic family;
- Health;
- weapon name + damage/cadence/reach as one attack profile;
- Signature name, cost, and outcome;
- Passive name and trigger;
- Spec slots or chosen Spec names;
- Buy/Hold and cost/affordability.

Safe on hover/focus/tap:

- definitions of Health, reach, cadence, and Mana generation;
- full weapon dossier (temper, mastery, crit/riders);
- exact Passive prose after its name and trigger are visible;
- full chosen Spec rules;
- keyword definitions.

Do not put purchase-critical information in hover. W3C requires hover/focus content to be
dismissible, hoverable, and persistent; Carbon's tooltip guidance likewise reserves tooltips for
additional rather than critical task information.

## Inputs

| Source | Role |
|---|---|
| `docs/ui-reviews/inbox/unit-dossier-density/current-workbench.png` | Current state and target geometry |
| `docs/ui-reviews/inbox/unit-dossier-density/feedback-r1-evolution-grid.png` | Jake's retained Direction 01 reference |
| `docs/ui-reviews/inbox/unit-dossier-density/feedback-r1-icon-ledger.png` | Jake's retained icon-ledger reference |
| `docs/vault/Design/workbench-dossier.md` | Existing per-kind dossier and disclosure laws |
| `docs/vault/Design/heroes.md` | Chassis, Weapon, Mana loop, Passive, rank/Spec anatomy |
| `docs/ui-reviews/outbox/combat-inspection/implementation/spec.md` | Shared InspectorPanel and section grammar |
| W3C SC 1.4.13 + Carbon Tooltip Usage | Hover/focus boundary |

## Assumptions

- This review reopens the current Workbench presentation based on the supplied play capture; it
  does not silently revoke the combat-card behaviour or implementation.
- Samples use only established Phalanx facts. Derived timing summaries are excluded; the dossier
  shows direct composed values only.
- Cost appears only in the action dock in the alternatives. The selected offer already carries
  the same price.
- These are exact-text structural prototypes, not final art or final copy.
- No `client/` files are changed before an exact sample or revision is approved.

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| `01-evolution-grid.png` — **Evolution / recommended base** | Keep the portrait-banner identity, but group facts by system and use the width for Signature vs kit | Lowest-risk change; eliminates repetition; every purchase-critical fact fits above the fold; naturally collapses to one column at narrower widths | The panel intentionally leaves unframed breathing room when a C-rank kit is short | Grouping/order/disclosure are literal; exact dimensions and “At a glance” copy illustrative |
| `02-split-profile.png` — **Structural** | At wide dossier widths, make art a persistent identity column and reserve the other column for the decision | Strongest use of a 710px panel; identity and mechanics never compete vertically; all rules are visible without scrolling | Needs a responsive switch back to the Evolution stack below roughly 600px; portrait crop quality varies by hero | Two-column hypothesis literal; crop, breakpoint, and surface finish illustrative |
| `03-icon-ledger.png` — **Wildcard** | Treat the dossier as an instrument panel: icon facts above a 2×2 capability grid | Fastest expert scan; clearest boundaries between Signature, Weapon, Passive, and Specs; compact header | Most dashboard-like and least characterful; icon comprehension must be taught and accessible | Information budget and capability grid literal; glyphs and final card chrome illustrative |
| `04-hybrid-weapon-owned-r2.png` — **R2 hybrid / current candidate** | Combine Direction 02's vertical identity art with Direction 01's hierarchy, but make the Weapon exclusively own damage, cadence, reach, and the basic-attack rule | No “profile above” cross-reference; facts live with the system that produces them; vertical art uses wide-panel height without stealing rule flow | Requires a responsive portrait-banner fallback; the Signature and Weapon columns need long-copy fixtures before approval | Weapon ownership, region order, and wide-layout split are literal; breakpoint, crop, spacing, and glyphs illustrative |
| `05-hybrid-direct-r3.png` — **R3 direct/readable** | Use the r2 dead space for a larger type scale and full-width focused sections, while removing every derived-baseline explanation | Readable at the supplied viewport; no explanatory filler; Weapon still owns its facts; full-width Passive and Specs can carry owned-unit content and selected names | Longer composed owned-unit states still need fixture verification; vertical portrait needs a banner fallback below the wide breakpoint | Direct information budget, shared section order, type hierarchy, and wide layout are literal; final breakpoint, crop, glyphs, and exact spacing illustrative |
| `06-dense-sheet-r4.png` — **R4 researched dense sheet / current candidate** | Replace nested cards and taxonomy labels with five compact semantic regions: Health, Weapon, Signature, Passives, Specs | Largest useful type so far; dramatically less padding; Weapon owns Damage, attack interval, Range, Mana/hit, and variable property rows; Passive trigger is in its prose; B/A/S are honest empty slots | A weapon with several properties or a unit with several Passives needs a bounded overflow policy; selected-Spec icon treatment still needs an asset-backed state | Five-region hierarchy, direct facts, and disclosure contract are literal; glyphs, exact surface treatment, and portrait crop are illustrative |
| `07-weapon-glyph-row-r5.png` — **R5 compact Weapon / current candidate** | Keep R4 intact but turn the Weapon's four universal stats into one icon-value phrase and shorten each variable property to a keyword row | Removes all repeated stat wording; values become larger and faster to compare; complete definitions remain available on hover/focus/tap | The four glyphs must be standardized and taught consistently across every weapon surface; touch needs a deliberate tap target/tooltip state | Weapon information architecture and compact property grammar are literal; prototype glyph shapes are illustrative |

## Jake feedback — round 2

1. Combine elements of Directions 01 and 02.
2. Delete “Attack profile is grouped above.”
3. The Weapon must entirely own its section: weapon name, damage, cadence, reach, and any future
   crit/shape/rider facts all live there rather than in the top fact strip.
4. Keep the vertical hero treatment when space allows.

`04-hybrid-weapon-owned-r2.png` applies those decisions. The top facts are now only chassis Health
and the Signature clock. At wide dossier widths, the vertical portrait is a dedicated identity
column. When two honest columns no longer fit, art collapses to a portrait banner while the
information hierarchy stays unchanged.

## Jake feedback — round 3

1. The r2 structure is right, but its type is too small and its lower-right region wastes space.
2. Remove the derived four-attack baseline entirely. Keep every area simple, focused, and direct.
3. This is the shared owned-hero inspector too, not a Recruit-only purchase card.

`05-hybrid-direct-r3.png` removes the baseline, enlarges the whole reading scale, and converts the
right column to full-width Signature, Weapon, Passive, and Specs regions. The card body is
context-neutral: Recruit versus owned Champion changes the identity tag, optional equipment
rows, and footer actions—not the anatomy or reading order.

## Research check and Jake feedback — round 4

The follow-up comparison covered Dota 2's in-match hero-stat panel, League of Legends champion
ability presentation, Wildermyth's character-sheet disclosure, and combat-summary weapon tables.
The useful common pattern is not their visual skin; it is their information ownership:

1. core durability is separate from attack/equipment values;
2. weapon facts form one dense numeric cluster and variable properties become rows;
3. abilities use icon + name + self-contained rule prose;
4. trigger meaning belongs in the rule sentence rather than a second classification header;
5. deeper explanation is disclosed from the exact stat, ability, or chosen upgrade—not held in
   permanent helper text.

Jake's requested hierarchy is now literal in `06-dense-sheet-r4.png`:

1. **Health** is the only current non-weapon core attribute; there is no Signature clock.
2. **Weapon** owns Pike, Damage, attack interval, Range, Mana/hit, and the real Brace property.
3. **Signature** retains the successful name, cost, icon, and direct outcome pattern.
4. **Passives** is a compact list. Riposte's trigger and refresh are written into its prose.
5. **Specs** shows only empty B/A/S slots. A selected slot replaces its letter with the chosen
   Spec icon; hover, focus, or tap discloses the chosen name and rule.

All generic “Unit dossier,” “Combat profile,” “Signature clock,” “when hit,” “future,”
“focus/hover,” and “later” labels are removed.

## Jake feedback — round 5

R4 is nearly approved, but the Weapon stat table is still too verbal. Revise only that region.

`07-weapon-glyph-row-r5.png` preserves the complete R4 layout and replaces:

- `9 / DAMAGE`
- `1.1s / ATTACKS EVERY`
- `2 / RANGE`
- `+11 / MANA / HIT`

with one persistent glyph-value strip: `✦ 9 · ↻ 1.1s · ⬡ 2 · ◇ +11`. Pointer hover, keyboard
focus, or tap provides each full definition. The real Pike property is shortened from a sentence
to `Brace · +30% damage vs engaged enemies`; disclosure defines **engaged** as engaged with an
ally. No other region changes.

## Recommendation

The current candidate is **`07-weapon-glyph-row-r5.png`**:

1. vertical identity art at wide dossier widths, banner at narrower widths;
2. five major information regions and no generic dossier/header chrome;
3. Health isolated as the current core attribute;
4. Weapon exclusively owns a compact damage/interval/range/Mana glyph row and property rows;
5. Signature keeps the established cost/name/rule presentation;
6. Passive triggers live in direct prose; Specs are blank B/A/S until selected;
7. context-specific footer actions without changing the card body;
8. no derived baseline or interpretive helper copy.

Direction 03 remains useful evidence for icon density but should not become the default visual
identity.

## Jake review

1. Preferred sample, combination, or reject all:
2. Must keep:
3. Most important next change:

## Approval

- Approved sample: `07-weapon-glyph-row-r5.png`
- Conditions:
  - Build R4's five-region dossier hierarchy with R5's compact Weapon glyph row.
  - The glyph shapes in the prototype are illustrative; use Warband's typed glyph vocabulary.
  - Recruit and owned-Champion contexts share the body and differ only in context/actions.
- Date: 2026-07-29

## Implementation result

- Implemented in the shared Workbench `InspectorPanel` for Recruit and owned-Champion contexts.
- Final verification: `implementation/verification-20260729-173426/`.
- Targeted Unity matrix: **15/15 PASS** at 1024×768, 1600×900, 2556×1317, and 3440×1440,
  including 130% copy stress plus Weapon-fact, Weapon-property, and selected-Spec tooltips.
- Unity refresh/import completed with zero console errors or warnings.
- Client compile: 61 scripts, zero errors.

Intentional differences from the prototype:

- live Warband typed glyphs replace illustrative glyph shapes;
- the fixture uses Banneret + Company Standard instead of the illustrative Phalanx + Pike;
- wide identity/mechanics geometry resolves to 28/72 so the full Weapon name remains literal;
- variable properties use a compact scan keyword (`MUSTER`, `BRACE`, etc.) while the tooltip
  preserves the exact authored mastery name and complete mechanical rule.

## Review log

- 2026-07-29 — Job created from the supplied Portal capture.
- 2026-07-29 — Current implementation, source model, prior dossier decision, shared combat-card
  spec, and tooltip/accessibility guidance reviewed. Three exact-data structural prototypes
  rendered at 2536×1310. `AWAITING_REVIEW`; no Unity client changes.
- 2026-07-29 — Jake combined Directions 01/02: retain vertical hero art when space permits and
  give Weapon exclusive ownership of the complete attack profile. Rendered
  `04-hybrid-weapon-owned-r2.png`; preserved prior samples. `AWAITING_REVIEW`; no client changes.
- 2026-07-29 — Jake accepted the r2 structure but requested a larger reading scale, less dead
  space, no derived baseline, and an explicit shared Recruit/owned-Champion contract. Rendered
  `05-hybrid-direct-r3.png`; full-width direct sections replace the small two-column body.
  `AWAITING_REVIEW`; no client changes.
- 2026-07-29 — Researched game stat-sheet patterns, then applied Jake's exact five-part hierarchy.
  Rendered `06-dense-sheet-r4.png`: no dossier title, no Signature clock, no trigger/future/hover
  mini-heads, compact padding, and complete Weapon ownership including Mana/hit and Brace.
  `AWAITING_REVIEW`; no client changes.
- 2026-07-29 — Jake retained R4 and requested a less verbal Weapon treatment. Rendered
  `07-weapon-glyph-row-r5.png` with one icon-value stat strip and a shortened Brace property row.
  All other R4 regions are unchanged. `AWAITING_REVIEW`; no client changes.
- 2026-07-29 — Jake explicitly green-lit building the current R5 direction.
  `07-weapon-glyph-row-r5.png` is `APPROVED_FOR_IMPLEMENTATION`; implementation contract written.
- 2026-07-29 — Built the shared Recruit/owned-Champion unit sheet, added typed Weapon facts and
  active/inactive property disclosure, removed redundant trigger/spec taxonomy, and added a
  component-width portrait fallback. Final Unity matrix `20260729-173426` passed 15/15 with clean
  import logs. Status advanced to `IMPLEMENTED_UNITY_VERIFIED`.
