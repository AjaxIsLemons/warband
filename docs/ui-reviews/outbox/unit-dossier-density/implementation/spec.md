# unit-dossier-density — implementation spec

**Status:** IMPLEMENTED_UNITY_VERIFIED  
**Approved:** `07-weapon-glyph-row-r5.png` (Jake, 2026-07-29).  
**Composition:** R4's five-region unit sheet plus R5's compact Weapon glyph row.

## Scope

Restyle the shared `InspectorPanel` when it is mounted as the Workbench dossier and bound to a
Recruit or owned Champion. Do not change the combat-card, enemy, Rank Up, item, Inscription,
Capacity, or equipment-preview formats.

The Workbench unit dossier serves both contexts:

- Recruit: purchase/hold actions and affordability.
- Owned Champion: current management/equipment actions.

Only the context eyebrow and action dock change. The body anatomy is identical.

## Information hierarchy

The wide Workbench unit dossier has two columns:

1. **Identity column**
   - full-height vertical portrait when the dossier has enough width;
   - context eyebrow;
   - champion name and rank;
   - no generic baseline subtitle.
2. **Mechanics column**
   - Health;
   - Weapon;
   - Signature;
   - Passives;
   - Specs.

There is no `Unit dossier`, `Combat profile`, Signature clock, derived hits-to-cast baseline,
trigger heading, `future`, `later`, or `hover` instruction.

### Health

- Show current authored/composed Health only.
- Use the typed Health glyph, label, and value.
- Do not repeat Weapon or Signature facts in this region.

### Weapon

Weapon owns the complete basic-attack profile.

- Header: `WEAPON` and the equipped weapon name.
- Universal facts form one persistent glyph/value strip:
  - basic Power or Healing;
  - attack interval;
  - Range;
  - Mana gained per completed hit.
- Crit and Cleave, when present, extend the same typed fact strip rather than becoming prose.
- Each fact is focusable and exposes its full typed definition on pointer hover, keyboard focus,
  and touch focus.
- Variable weapon properties render as compact property rows. The property name and concise
  effect remain visible; the exact mechanical rule and active/inactive mastery state are
  disclosed from the row.
- A Weapon property is never presented as active when the wielder is not a specialist and the
  Weapon is not Relic.

### Signature

- Header `SIGNATURE`.
- Mana cost remains a Mana-coloured context chip.
- Show icon, name, and complete authored rule.
- Weapon Mana generation does not appear here.

### Passives

- One small plural `PASSIVES` header.
- Render each composed Passive as name + complete rule.
- Trigger grammar belongs in the rule prose; no `WHEN HIT`, `COMBAT START`, or similar heading.
- Additional Passives append as rows using the same grammar.

### Specs

- Header `SPECS`.
- Always show three compact B/A/S slots for Recruit/Champion unit sheets.
- Empty slot: rank letter only.
- Selected slot: chosen Spec icon replaces the letter.
- Selected slots expose the Spec name and complete rule on hover, focus, or tap.
- Do not show `AWAKENS`, `future`, or `later` copy.

## Responsive behavior

- Wide dossier: vertical identity column and mechanics column.
- Narrow dossier: identity collapses to a portrait banner above the same five-region mechanic
  order. No information changes at the breakpoint.
- Short viewports may reduce portrait height before reducing type or removing Specs.
- No ScrollView. Text regions use `min-height` and wrapping; authored fixture copy must fit.

## Input, focus, and motion

- Weapon facts, Weapon properties, and selected Specs have visible hover/focus states and
  `tabIndex` order matching reading order.
- Empty Spec slots are non-interactive.
- Runtime tooltip content is supplemental; all purchase-critical numbers and property summaries
  remain visible.
- Touch focus opens the same disclosure as pointer hover/keyboard focus.
- Existing `motion--reduced` behavior remains authoritative. This sheet adds no required motion.

## Must match

- five-region order and information ownership;
- vertical identity treatment at wide width;
- no generic subtitle or mini classification headers;
- compact typed Weapon glyph row;
- direct Passive prose;
- blank/selected B/A/S slot behavior;
- pinned existing action dock;
- body shared across Recruit and owned-Champion contexts.

## Illustrative

- exact prototype glyph artwork;
- exact portrait crop;
- exact pixel radii, border shades, and section heights;
- the prototype's `Brace` wording, provided the live concise copy stays mechanically accurate.

## Acceptance

1. Client source compile passes.
2. Unity refresh/import completes with zero new console errors or warnings.
3. Workbench Recruit fixture shows:
   - vertical portrait at the large target;
   - Health only in the core strip;
   - Weapon Power/interval/Range/Mana glyph facts;
   - Signature, full Passive, blank B/A/S;
   - purchase actions unobscured.
4. Owned-Champion fixture shows selected Spec icons and current management actions.
5. Weapon fact tooltip, Weapon property tooltip, and selected Spec tooltip open by editor fixture.
6. UI QA covers at least 1024×768, 1600×900, 2556×1317, and 3440×1440 without overflow,
   clipping, overlap, or ScrollView.
7. Captures are written under this job's `implementation/` folder and compared with the approved
   sample. Any intentional difference is recorded.

## Verification

Final evidence is under `verification-20260729-173426/`.

- `make check-client`: 61 client scripts, 0 errors.
- Unity refresh/import: 0 errors, 0 warnings.
- Unit dossier matrix: 15/15 PASS.
- Viewports: 1024×768, 1600×900, 2556×1317, 3440×1440.
- States: Recruit, owned Champion, 130% copy stress, Weapon-fact tooltip, Weapon-property
  tooltip, selected-Spec tooltip.
- Live pending-rank regression: PASS.

The implementation uses live typed glyphs and Banneret fixture data rather than the prototype's
illustrative glyphs and Phalanx copy. At wide width, a 28/72 identity/mechanics split preserves the
vertical hero while keeping full Weapon identity visible. Weapon-property scan labels are compact;
the exact mastery name, full generated rule, and active/inactive state remain in disclosure.
