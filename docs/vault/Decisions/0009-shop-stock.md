# ADR 0009 — Shop stock: offers, freeze, rank-up forks, banners in rotation, sell-back

**Date:** 2026-07-22 · **Status:** amended by ADRs 0017 and 0019 · **Participants:** Jake + Claude

> **2026-07-24 amendment:** ADR 0017 replaces the public Banner system with unlimited
> persistent **Inscriptions** held by the expedition's Hourstone. The offer-slot decision
> remains historical input; exact Inscription acquisition cadence is reopened.
>
> **2026-07-24 amendment:** ADR 0019 settles the first-playable implementation: three Recruit
> cards plus two weighted Workshop cards (45% Weapon / 35% Trinket / 20% Inscription), free
> Hold, 1-Sand reroll, and explicit select-then-Buy interaction.

## Context
ADR 0006 settled the shop's cadence; this settles what it sells. Research inputs: Guildrun
(3 hero slots + items + relics per post-fight shop, dupes bought as rank-ups, free freeze,
act-end auction), Super Auto Pets (flat income, free per-item freeze, sell-back — the
closest async-ghost economy), The Bazaar (income-investment economy — the pole we rejected
with interest; 50% sell). Numbers below are placeholder.

## Decisions
1. **Per-node shop = 3 hero cards + 2 item cards** (Guildrun's layout at our scale — both
   economic axes on every tick).
2. **Infinite weighted hero pool.** No shared finite pool — TFT scarcity is a lobby
   mechanic, meaningless solo vs ghosts. Uniform over the 8-chassis P0 roster; chassis
   already at rank S drop out of generation (one instance per chassis in v1).
3. **Flat hero pricing v1** — no TFT-style cost tiers on an 8-hero roster; revisit in hero
   deep dives. A dupe costs the same as the card. **Buying an owned chassis auto-merges:**
   rank-up fires immediately and presents the 1-of-2 spec choice (B = path fork, sets the
   hero's path; A/S = in-path nodes). The choice must be resolved before any other shop
   action or leaving.
4. **Free per-offer freeze** (SAP-style): a frozen offer survives into the next node's shop
   in its slot, until bought or unfrozen. Reroll (flat cost, ADR 0006) regenerates only
   unfrozen slots.
5. **Banners live in the regular rotation** (Jake's call, over the act-close-auction
   proposal): each item slot has a placeholder ~25% chance to roll a banner instead;
   owned banners excluded; act close stays a slot-offer-only moment.
6. **Sell-back at 50%:** heroes refund 50% of total gold sunk (card + dupes, tracked on the
   hero); their equipped gear returns to inventory. Unequipped items sell for 50% of price.
7. **Items = inventory + equip model:** weapon slot + one trinket slot per hero (heroes.md),
   free re-equip during any shop tick. **Banners ride into battle as team triggers — both
   sides:** the ghost snapshot format grows banner ids, so ghost boards keep their team
   rules (blind placement stays honest about real power).

## Consequences
- `IRunContent` grows content pools (hero/weapon/trinket/banner by act), `Banner(id)`, and
  `SpecOptions(chassis, rank, path)` — the spec-tree shape stays content-side.
- Snapshot format: + banner ids (roadmap 1d bot-ghosts must generate them too).
- Per-rank stat scaling stays an open question (Guildrun runs +25%/+50% — data point for
  that conversation; ranks currently gate spec nodes only).
