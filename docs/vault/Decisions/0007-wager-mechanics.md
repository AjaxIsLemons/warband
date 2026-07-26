# ADR 0007 — Wager mechanics: risk tiers, per-kill payout, tier-scaled success bonus

**Date:** 2026-07-22 · **Status:** superseded by ADR 0019 · **Participants:** Jake + Claude

> **2026-07-24:** terminal losses, zero loss payout, one fixed visible reward, and
> Stable/Fraying/Collapsing replace the recoverable-loss/kill-share design below.

## Context
ADR 0002 made PvE the wager layer but left the shape open. ADR 0006 settled the sink side
(shops); this settles the income side. Numbers are placeholder per content doctrine.

## Decisions
1. **Every fight node offers 3 escalating tiers** (working names Safe / Even / Greedy —
   rename with theme). Same node, harder enemy composition and bigger reward pot per tier.
   Tier difficulty modulates *around the act baseline* — difficulty still anchors to act
   number, never W/L (ADR 0002 law).
2. **Payout = per-kill share + success bonus (Jake's shape).** Each node has a reward pot
   R(act, tier). Every enemy unit killed pays a proportional slice of the pot's kill-share;
   winning the fight pays the tier's success bonus on top. A loss still pays your kills'
   slices — you're never zeroed, greed reads as variance, and scrappy losses stay rewarding.
3. **No staked gold.** The "wager" of ADR 0002 is reinterpreted as opportunity cost: a
   Greedy loss forfeits the bonus and costs tempo, never your bank. Keeps "a PvE loss is
   never run-ending" true by construction.
4. **Item drops upgrade with tier** — greed buys tinker-fuel, not just gold.
5. *Placeholder proposal (Claude, veto-able):* greedier tiers shift pot weight from
   per-kill toward the on-win bonus — Safe ≈ mostly guaranteed drip, Greedy ≈ mostly
   on-win. Makes tier choice a variance decision, not just a size decision.

## Consequences
- Run-layer node generation needs per-tier enemy composition scaling + pot parameters
  (placeholder values at build time).
- Payout computes from sim output — FightStats already tracks kills (conservation), so the
  run layer folds gold from the fight result, no new sim work.
- Archetype sweep harness should report expected value per tier — first sanity check that
  Greedy isn't strictly dominant/dominated.
- "Reading your power spike and cashing in" (pitch) now concretely = picking the tier whose
  variance your board can afford.
