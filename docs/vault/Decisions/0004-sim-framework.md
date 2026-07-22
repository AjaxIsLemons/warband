# ADR 0004 — Sim framework architecture

**Date:** 2026-07-22 · **Status:** accepted · **Basis:** circuit engine teardown +
SabberStone/StS/SAP/Riot research. Full spec: [[../Design/sim-framework.md]].

## Decisions
1. **One content atom:** `Trigger{On, When, Do}` over five orthogonal axes (event ×
   condition × selector × shape × effect), pure data, one named escape hatch. Passives,
   forks, banners, items, glyph rules, reactions — all the same shape.
2. **Circuit's four walls fixed up front:** condition negation/OR + ExcludeSelf;
   read-time conditional stat rules; enemy-relative spatial selectors; **fields as
   first-class entities**.
3. **Fields unify glyphs AND auras** (aura = field attached to a unit). Per-tick
   deterministic sweep — polling is correct at our scale; no aura-invalidation machinery.
4. **Resolution:** frozen-tick clock layer (mirror fairness structural) + FIFO cascade
   queue with immediate mutation, id-asc trigger order, depth ≤ 8 / drain ≤ 50k, and a
   Hearthstone-style batched death phase. Neutral id ordering (SAP's stat-order
   considered, rejected).
5. **Determinism law:** integer-only math (FP=1000), one save/restorable PCG32, banned
   APIs list, explicit tie-breaks, per-tick hash + cross-machine goldens.
6. **Replay = tag-change log** (delta + absolute post-state; client sets, never
   accumulates) with a fold-and-compare-every-tick guardrail test.
7. **Metrics first-class:** all stats are folds of the log; events carry
   Source/Cause/RootSource; conservation test (credit-by-source == credit-by-target);
   archetype sweep harness now, economy-honest run harness later (metasim lesson).
8. **Not building:** replacement effects/layers, scripting DSL, interrupts,
   enchantment entities.
