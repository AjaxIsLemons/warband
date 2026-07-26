# ADR 0019 — First-playable run cadence, Sand economy, and persistent workspace

**Date:** 2026-07-24 · **Status:** partially superseded by ADR 0020 · **Participants:** Jake + Codex

## Context

The first shell proved that Menu → Recruit → Map → Deploy → Fight → Shop could walk the run
machine, but it divided one repeated planning job into three destinations and explained offers
with prose-heavy cards. Jake explicitly prioritized getting a playable, polished loop before
expanding authored content: the game lives in menus and replay viewing, so preparation must
already feel intentional.

This ADR settles the first values needed to playtest the loop. They are a starting tuning
contract, not a balance claim.

## Decisions

1. **The authored shape is three acts of five beats:** Fight → Fight → Interlude → Fight →
   Boss. The first content milestone authors one complete act; later acts may reuse scaffolding
   until their content is earned.
2. **Loss is terminal and pays nothing.** The player can spend no reward after defeat, and the
   PoC tests the clarity of one high-stakes expedition rather than recoverable attrition.
3. **Every normal fight offers three visible pressure choices:** Stable, Fraying, and
   Collapsing. The selected enemy formation and exact reward update together. Difficulty remains
   act-anchored and never reads the player's record.
4. **The shared currency is Sand.** Runs begin with 4. Normal-fight rewards by act are
   `4/5/7`, `5/6/8`, and `6/7/9` for Stable/Fraying/Collapsing. Bosses pay `6/8/0`.
5. **Planning is one persistent board-first workspace.** The act track and Sand stay at top,
   current encounter and pressure at left, the live 3D board in the center, the selected-card
   inspector at right, and Muster / Market / Armory / Hourstone tools in a bottom dock.
   Map, Shop, and Deploy are no longer destinations during a run.
6. **The opening draft is free: choose three of five.**
7. **Market stock is three recruits plus two Workshop offers.** Workshop weights are
   45% Weapon, 35% Trinket, 20% Inscription. Holding an offer is free; rerolling unfrozen
   stock costs 1 Sand. Prices are recruit/dupe 5, Worn weapon 4, trinket 3, Inscription 7.
   Sell-back remains 50%.
8. **Capacity is a visible widen-vs-deepen purchase.** Interludes unlock the next capacity
   up to six; purchase costs are 8/12/16 Sand. The bench remains two.
9. **Interludes present three disclosed paths:** Treasury grants 5 Sand, Armory offers one
   of three equipment choices, and Hourstone offers one of three Inscriptions. A non-final
   boss pays its Sand and blocks on one of three Inscription rewards.
10. **Cards compare; the inspector explains.** A card shows portrait, written role plus icon,
    composed HP / attack-or-heal / reach / cadence, current weapon, signature name, price, and
    state. Selecting never purchases. Buy, Hold, Equip, Move, Sell, and Place are explicit
    actions in the inspector.
11. **Presentation data is separate from mechanics.** Stable ids locate portrait, role icon,
    ability/passive copy, and accent in a presentation catalog. Mechanical values always come
    from the composed `UnitDef`.
12. **Desktop mouse/keyboard and landscape touch share the same actions.** Nothing essential is
    hover-only, drag-only, color-only, or animation-dependent. Cards are focusable, tabs have
    numeric shortcuts, touch targets grow on handhelds, compact/short layouts are explicit, and
    reduced motion is supported.
13. **Motion has named timing tokens:** hover 90 ms, selection 140 ms, panels 180 ms, reroll
    stagger 40 ms per card capped at 300 ms, purchase 260 ms, rank choice 450 ms, and reward
    choice 500 ms. Semantic UI cues exist for later sound/haptic subscribers; this pass adds no
    sound.

## Consequences

- Supersedes ADR 0007's recoverable loss, kill-share payout, and Safe/Even/Greedy vocabulary.
- Amends ADR 0006: shop access is continuous inside Planning, and capacity unlocks at Interludes
  rather than an act-close Shop screen.
- Amends ADR 0009 with the settled first-playable stock weights, prices, free Hold behavior, and
  public Inscription vocabulary.
- The three-act state machine is playable now, but the content gate remains one authored act
  before friends playtest #1. Repeated random hero-kit encounters are scaffolding.
- Economy values must move only from sweep/playtest evidence. The structural UI contract should
  survive those tuning changes without layout rewrites.
