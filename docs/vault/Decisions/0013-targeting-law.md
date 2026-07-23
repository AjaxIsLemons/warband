# ADR 0013 — Targeting law

**Date:** 2026-07-22 · **Status:** accepted (Jake's formulation, Shade dive) · **Participants:** Jake + Claude

## Context
Jake asked whether targeting is locked. Today's sim (ADR 0005 "TFT-simple"): acquire
nearest enemy, **sticky until the target dies**, chase meanwhile. The Bulwark's Taunt and
the Shade's Phase force the complete rule.

## Decision — the law
1. **Acquire:** deterministic nearest enemy (existing tiebreak by id).
2. **Sticky until one of:**
   - the target **dies** (current behavior),
   - the target **leaves your attack range** (NEW),
   - the target becomes **untargetable** (Phase — NEW),
   - you are **Taunted** — forced onto the taunter for the duration (NEW; Taunt overrides
     everything while it lasts).
3. **On any of those: re-acquire by the same rule.** (A Taunt's expiry is a re-acquire.)

## The melee subtlety (flagged, accepted)
For a melee unit mid-walk, the target is *always* out of range — so strictly, walking melee
re-evaluates continuously, which collapses to "melee always attacks the currently nearest
enemy." Accepted: it's maximally predictable (AI legibility, ADR 0003) and usually agrees
with sticky-chase anyway. If the viewer shows ugly target-bouncing in swirling fights, the
fallback is hysteresis ("only retarget if someone else is strictly closer than the current
target") — a playtest decision, not a now decision.

## Consequences
- Sim backlog: range-exit retarget · untargetable flag honored in acquisition and
  validity · Taunt override + expiry re-acquire. (Taunt/Phase statuses already logged.)
- Ranged kiting becomes real: stepping out of an archer's range forces its re-evaluation.
