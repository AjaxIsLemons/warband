# ADR 0006 — Shop & economy: every-node shops, act-close slot offers, small bench

**Date:** 2026-07-22 · **Status:** accepted (design session w/ Jake) · **Participants:** Jake + Claude

## Context
The run layer (roadmap 1a) needs the economy's *structure* before it can be built. Round 4
(heroes.md) already decided: duplicates→ranks C/B/A/S, one currency for everything, and the
central widen-vs-deepen tension. This ADR settles the remaining structural forks. All numbers
below are **placeholder** per the content doctrine — structure is the decision, values are not.

## Decisions
1. **Shop after every node** (TFT/Guildrun cadence). A small shop (hero cards + items, banners
   occasionally) rolls after every node, including act-close bosses. **"Shop" is removed as a
   map node type** — map nodes are wager-tiered fights and events. ~20 offer screens per run
   gives dupe-chasing real pacing and maximizes the tinker loop.
2. **Roster 3 → 6 via act-close slot offers** (Guildrun's buy-your-width model + our act
   anchor). Start with 3 slots. At each act close *while under the cap*, one slot is offered at
   an escalating placeholder price — buy it or bank the gold for dupes. Cap 6. Four offers
   for three needed slots = one offer can be declined without locking out max width.
   Availability is act-anchored (anti-snowball law — no act-1 wide-rush polluting the act-1
   ghost pool); *whether* you widen is a real economic fork against going deep.
3. **Rerolls: flat gold cost** per reroll. Simplest, TFT-proven; odds become pure tuning later.
4. **Small bench (2 slots, placeholder size)** — *amended same-session; first cut was
   no-bench.* Jake's catch: with hero cards in every shop, a full field would dead-end the
   widen axis between slot offers — a card with nowhere to go is a dead offer. Bench is pure
   storage for P0: benched heroes don't fight, carry no effects, and **never enter the ghost
   snapshot** — the ghost is the fielded board only. Duplicates auto-merge across
   field + bench.
5. **Placeholder economic frame** (explicitly not tuned): rank-up costs **1 dupe per step**
   (4 total copies for S) · income = **act-anchored base per node + wager outcomes** · **no
   interest mechanic** (hoarding fights the tinker-every-fight pillar).

## Open / flagged
- **Currency named "gold"** — deliberately generic placeholder until theme/lore lands
  (Jake: nothing decided there yet). First working name was "shards", dropped same-session —
  it's literally Guildrun's currency name.
- All prices/income values are placeholder; the archetype sweep harness + playtest tune them.
- Wager shape/reward curve settled same-day in ADR 0007 — plugs into the income side here.

## Consequences
- Run-layer skeleton models the node loop as: pick node → fight/event → rewards → shop tick;
  act close appends the slot offer to that shop.
- Pitch/heroes/roadmap references to "2→6" become "3→6"; pitch's node-type list drops "shop".
- Guildrun research (party 3→5 field/6 roster, purchasable team-size expanders at act-end
  auctions) is the precedent for decision 2.
