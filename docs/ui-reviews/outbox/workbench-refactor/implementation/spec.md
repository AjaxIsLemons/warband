# Workbench column refactor — implementation spec

Approved: `05-shopfront-obsidian` + rack state, with the header node-map condition —
visual record `samples/05-shopfront-obsidian-r2.png` / `-r2-rack.png` (2026-07-28).
Roadmap item 33. Client-only: no sim/run/content changes.

## Region geometry (1600×900 reference; all dimensions re-derive per breakpoint)

| Region | Spec |
|---|---|
| Header | 46px single row: WORKBENCH title · ACT n/3 · **node-map track** · (flex) · Sand chip · CHOOSE NEXT WAGER. The brief sentence is deleted from the header; its copy moves to the dossier empty state. |
| Node map | `PlanningModel.Track` as pips: number per node, ◆ for Interlude, BOSS chip last; past = filled/dim gold, current = ringed glow, future = hollow. Hover: node kind + label (existing tooltip idiom). |
| Body | Columns, ~624px: market (flex) + dossier (30% ≈ 466px), 10px gap. |
| Market | Panel with 52px vertical REROLL rail (glyph + stacked letters + cost) on the left edge + 3×2 offer grid. No heading strip; no "select for full dossier" copy. Sold offers keep their slot (receipt card). |
| Dossier | Full body height, vertical stack: kind band (kind-accent + RANK) → portrait banner (~132px, scrim + name/subtitle) → stat chip grid (≤7, 4-up rows) → SIGNATURE → BASIC ATTACK → deferred PASSIVE line → **PATH** → (grow) → pinned action row. |
| Rail | 186px. Identity block · field cards · reserve group · ARMORY chip. Hero cards 148×166: portrait (rank badge) → name band → kit row → path row. |
| Rack | Floating panel anchored above the ARMORY chip, right-aligned: 244px wide, up to ~552px tall, vertical item tiles, ✕ + Esc close. Overlays the dossier's right edge; market/dossier/rail never move. Chip glows while open. |

## PATH rules (dossier section + card strips)

- Three rows, always B / A / S (every chassis makes a 1-of-2 pick at each — `IRunContent.SpecOptions`).
- Filled row: rank letter tile + node display name + authored one-liner (`ContentLexicon`);
  full machine rule on hover (existing tooltip system).
- Empty row: ghosted tile + `AWAKENS AT RANK X`; the chassis' `ForkRank` row appends `· THE FORK`.
- Unit offers (Recruit/RankUp) carry a **tier strip** in the commerce row: `RANK X` (or `C → B`
  for rank-ups) + three B/A/S diamonds, filled per already-selected picks. Later-run pre-specced
  recruits need zero new UI: filled diamonds on the card, filled PATH rows in their dossier.
- Rail card path row: same three slots, filled = spec icon (existing `WarbandSpecBadgeModel`
  tooltip), empty = ghosted rank letter.

## Rail card kit row

SIGNATURE · WEAPON · TRINKET. Signature slot is disclosure-only (tooltip → name, mana, exact
rule; reflects fork overrides) — never an equip target. Weapon/trinket sockets keep every
current behavior: drag, transfer, keyboard (Space) parity, drop highlights, armed-item equip.
`WarbandHeroModel` gains signature name/icon/rule (+ mana) from the composed loadout.

## Rack behavior

State = existing `PartyShelf.Expanded`; actions = existing `OpenLoadout`/`CloseLoadout`;
cues = `DrawerExpand`/`DrawerCollapse`. ARMORY chip stays the handle (open/close/unequip-drop
modes with the state-driven third line). Esc closes (existing). Items keep hover tooltip +
select-to-arm + drag-to-socket. Pager kept if >8 tiles fit the rack height at the active
breakpoint.

## Style (obsidian)

Near-black glass: panel `rgba(15,15,24,~.8)` over a subtle radial wash, hairline borders
`rgba(255,255,255,.07–.09)`, radii 10–14px (panels) / 6–8px (chips, slots), pill buttons,
luminous gold `#e6b95c` for currency/selection/glow accents. Semantic accent colors
(kind/stat families) unchanged. Existing `LastHourTokens`/`UiFoundationTokens` gain the
obsidian values rather than per-rule forks where possible.

## Must-match vs illustrative

Must-match: region geometry + proportions, header contents incl. node map, PATH/tier-strip
grammar, rail card anatomy, rack anchor + overlay behavior, obsidian material direction.
Illustrative: mockup fonts (use game font assets), emoji glyphs (use `WarbandGlyph`), exact
glow/shadow values, portrait crops.

## Non-negotiables carried over

Per-kind dossier section-role law (`Design/workbench-dossier.md`) · cost never hover-only ·
market never yields · rail visible + interactive while rack open · no ScrollView · blocking
choice modal unchanged · reduced-motion honored on all new transitions · 1280×720 floor.

## Acceptance

`make check-client` clean → Unity Workbench full matrix (rewritten to this structure) across
the 5 viewports → by-eye captures vs the approved sample for: empty-selection, champion
selected (PATH filled/empty mix), recruit + rank-up + inscription + capacity dossiers,
rack open (equip flow reachable: arm item → socket glow → equip), sold slot, locked slots,
blocking choice over the new layout.
