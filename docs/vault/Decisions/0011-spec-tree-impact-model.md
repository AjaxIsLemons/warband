# ADR 0011 — Spec-tree impact model: archetype algebra, C/B/A/S ladder, variable fork timing

**Date:** 2026-07-22 · **Status:** accepted · **Participants:** Jake + Claude

## Context
The deep-dive template. Claude proposed a 3-axis "hat check" (WHERE/WHAT/WHEN must all
change); Jake corrected to a simpler, generative model — archetype algebra, one real change
is enough — and asked for the rank ladder to be laid out. Fork-timing worry ("a fork at B
may be less interesting than at A") resolved with bounded variance.

## Decisions
1. **Archetype vocabulary (7):** ranged dps · melee dps · caster · healer · support
   (heals/shields/haste) · disruptor (cc) · tank. A class at recruit is **1–2** of these.
2. **A fork path is one operation: ADD / SWAP / DEEPEN.** Every path describable in five
   words ("caster ADDs disruptor: AoE stun on cast").
   **Fork law:** at least one path per class must ADD or SWAP; leaning in (DEEPEN) is
   legal for the other. **Named specialist exceptions allowed** (double-DEEPEN), documented
   per class in its dive — exceptions, never drift.
3. **The C/B/A/S ladder:**
   - **C — recruit:** chassis as-is (1–2 archetypes, signature verb, innate).
   - **B — the fork** (default): the archetype operation; signature transforms to match.
     *Changes what the unit IS.*
   - **A — sharpen:** 1-of-2 in-path amplifiers (keyword add, mana-curve bend, conditional
     rule). *Changes how the build plays.*
   - **S — crown:** 1-of-2 within-hat capstones; never a fork, never a new archetype.
     *Changes what the fight feels like when it goes off.*
   - Flat chassis bump at **every** rank so a dupe always feels good pre-choice
     (magnitude placeholder; Guildrun's +25%/+50% is the reference point).
4. **Fork timing varies per class — B default, A for late-bloomers, never C or S.**
   Late-bloomers compensate: stronger generic baseline early + a bigger operation when the
   fork lands. Free respec (settled) keeps early forks revisable — hypothesis, not prison.
   Meta-texture: early-fork classes are safe drafts; late-bloomers gamble on dupe timing.
5. **Dive exit criterion — the wardrobe test:** 2 paths × 2–3 weapon archetypes must yield
   **4–6 genuinely distinct loadouts** (placement, item priorities, spike timing), or the
   chassis is too narrow and gets reworked.

## Consequences
- Roster audit under the fork law: Bulwark ✓, Cleric ✓, Pyromancer ✓, Phalanx ✓ — but
  **Shade and Sharpshot are double-DEEPEN** (specialist-exception candidates or rework)
  and Banneret leans that way. Resolve in their dives.
- Closes the open per-rank-scaling question (flat bump; value = placeholder doctrine).
- Late-bloomer candidates to test in dives: Pyromancer, Shade.
- No data-shape changes: SpecOptions/SpecNode ids already carry this; content encodes the
  operations.
