# ADR 0003 — Combat soul: the clock and the field

**Date:** 2026-07-22 · **Status:** accepted on paper (falsifiable by first playable) ·
**Participants:** Jake + Claude (rounds 5–6)

## The soul sentence (Jake-approved)
> **"A war for time and ground: your build bends the clocks and paints the battlefield —
> placement is the only order you give."**

The second clause is also design law: no micro ever, and unit AI stays predictable enough
that placement is what's being tested.

## Decisions
1. **Two pillars: Clock + Field.** Clock = attack/mana rhythms + tempo tools (Haste/Slow,
   Silence⇄Disarm mirror, Stun premium, Mana-grant/burn). Field = **flat base map, zero
   predetermined terrain, ever** — all ground effects are unit-cast **glyphs** (area+rule+
   duration: fire field, healing ground, consecrated ground, summoned wall = the only
   obstacle in the game).
2. **Displacement demoted** to rare premium spice (Leap/Push/Pull/Root + collision hooks),
   explicitly NOT a pillar (Jake round 6).
3. **Counter layer:** guardian/reaction passives via trigger grammar ("enemy Leap lands
   within 2 → attack it / force it to target me / Root it") — defense is a placement
   statement.
4. **AI legibility principle:** "smart enough not to look dumb, dumb enough to predict."
   Fixed targeting rules + shortest-path + bounded field-aware step scoring, deterministic
   tie-breaks. No lookahead. Extra intelligence is sold as kit features, never engine magic.
5. Open-minor: symmetric damage fields (hurt everyone; lean) vs enemies-only — decide in sim.

Full vocabulary: [[../Design/combat-grammar.md]] v0.2.
