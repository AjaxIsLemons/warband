# Combat unit sheet R5 — implementation specification

Approved source: `../samples/01-compact-live-sheet.png`  
Enemy omission reference: `../samples/03-enemy-compact-sheet.png`  
Approval date: 2026-07-29

## Product contract

The floating combat inspector is the same unit sheet used by Workbench, fed by a live-combat
adapter. It is not a parallel card. The fight continues while the sheet is open and the existing
pin, tether, retarget, empty-board close, and Escape behavior remain intact.

The ordinary combat sheet is approximately 388 px wide and uses the compact banner portrait.
Its information order is:

1. identity;
2. live core facts — Health and optional Shield/Mana;
3. Weapon — name, compact facts, and zero or more exact properties;
4. optional Signature — cost, name, exact rule;
5. zero or more self-contained Passives;
6. selected Spec glyphs only;
7. compact targeting and visible status footer.

There is no Signature clock, no generic mini-header for self-explanatory facts, and no separate
Attack Profile block. The Weapon owns all attack/heal power, cadence, range, mana-per-hit,
crit/cleave, and future Weapon facts.

## Data and extension contract

The shared inspector receives a structured unit-sheet model:

- ordered `CoreFacts`;
- ordered `WeaponFacts`;
- a list of `WeaponProperties`;
- an optional `Signature`;
- a list of `Passives`;
- a list of selected `Specs`;
- targeting text and a list of live statuses.

The renderer iterates those lists. It must not switch on a fixed Weapon fact order, assume one
property, or assume one Passive. Adding another authored Passive or appending a Weapon
fact/property in an adapter must not require changes to `InspectorPanel`.

Presentation data remains backward-compatible with the legacy single-Passive fields. An optional
`passives` array becomes the extensible source; when absent, the adapter synthesizes the current
single Passive.

`PlaybackUnit` projects immutable Weapon facts needed by the live adapter, including mana per
swing and cleave. Live composed power/cadence/range remain sourced from playback state.

## Context rules

- **Workbench hero:** authored Health, full Weapon profile, Signature, all Passives, B/A/S Spec
  addresses, and existing commerce/manage affordances.
- **Allied combat hero:** live Health/Shield/Mana, live composed Weapon profile, Signature,
  Passives, selected Spec glyphs, targeting, and statuses.
- **Authored enemy:** use the same renderer but omit absent Signature, Specs, player portrait, and
  player mastery. Use enemy role/behavior presentation and never borrow hero identity from a
  render chassis.
- **Defeated unit:** retain identity/build information, show zero Health honestly, and keep
  statuses/targeting limited to facts still present in playback.

Absent optional regions collapse without placeholder headings.

## Interaction and responsive behavior

- The sheet is display-only; clicking it must not leak through to battlefield selection.
- Rules exposed through icons must remain available through hover, keyboard focus, and touch
  focus.
- The sheet stays within the safe-area frame and keeps the current unit tether.
- Ordinary combat uses the compact banner mode. No movement-driven reflow is allowed while the
  sheet is open.
- The sheet does not use an internal `ScrollView`. Short viewport styling may compress spacing,
  but 16 px rule text remains the floor.
- Weapon facts wrap as a group when additional facts exceed one row.

## Acceptance checks

- Allied hero, authored enemy, defeated unit, absent Shield/Mana/Signature, selected Specs,
  multiple Passives, an extra Weapon fact, multiple Weapon properties, and long rule copy.
- Viewports: 1024×768, 1280×720, 1600×900, 2556×1317, and 3440×1440.
- No overlap, clipping, hidden actionable information, or raw content ids.
- Live values refresh without rebuilding combat ownership or pausing playback.
- Static client checks and simulation tests pass.
- Unity imports cleanly with zero new console errors or warnings.
- Final implementation captures are stored beneath this job's `implementation/` directory and
  the review status is closed as `IMPLEMENTED` or `IMPLEMENTED_UNVERIFIED`.
