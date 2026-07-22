# Combat grammar — the soul — v0.2 (2026-07-22, round 6)

Soul sentence (**approved, ADR 0003**):
**"A war for time and ground: your build bends the clocks and paints the battlefield —
placement is the only order you give."**

## Pillar 1 — the Clock (unchanged from v0.1)
Two clocks per unit: attack interval + mana (fills from attacks, damage taken, trickle;
full = auto-cast). Tools: **Haste/Slow** (attack speed); **Stun** (both clocks stop,
short/premium); **Silence** (no cast/mana-gain, autos continue — caster denial) mirrored
by **Disarm** (no autos, casting continues — carry denial); **Mana-grant/Mana-burn**.
Because attacks feed mana, Haste accelerates casts and Slow starves them.

## Pillar 2 — the Field (reworked round 6: Jake)
**The base map is FLAT — no predetermined terrain, ever.** All ground effects are
**glyphs: unit-cast area effects that change what hexes DO.**
- A glyph = area (e.g. radius N from cast cell, trail, wall-line) + rule + duration.
  Examples: fire field (DoT while standing/entering) · healing ground · consecrated
  ground (Haste allies on it) · mana font zone · **summoned wall** (the only obstacle
  in the game — always unit-made).
- Glyphs come from signatures, forks, innates ("leaves a scorched trail"), banners.
- By mid-fight the flat board is painted with both builds' zones — territory as a
  fight-time resource, authored by kits, not level design.
- ❓ minor: do damage fields affect all units (symmetric — enables shove-into-fire plays,
  needs AI avoidance) or enemies-only (simpler)? Lean: all units, revisit if it reads badly.

## The vocabulary — movement & answers (demoted round 6: options, NOT a pillar)
Displacement is deliberately lightly represented — spice, not identity:
- **Leap** (assassin mobility) · **Push/Pull** (rare, premium) · **Root**.
- **Collision** (kept): displaced into a unit or wall = damage/Stun hook.
- **Reaction tech — the counter layer (Jake's ask):** the trigger grammar expresses
  guardian passives: *"when an enemy Leap lands within 2 hexes → immediately attack it"*,
  *"enemies that Leap into my aura are forced to target me"*, *"leaper landing adjacent
  is Rooted"*. Anti-assassin defense becomes a placement choice — the guard must stand
  near what it guards.

## Sustain & damage (unchanged)
Shield · Heal · Regen · **one generic DoT** with hero-specific riders (no burn/poison
typing — identity via tree riders). One damage number.

## Shapes
Single · Adjacent-ring · Line · Ring-splash · **Glyph-area** (radius/trail/wall-line).

## Trigger grammar
when <event> [condition] → <effect>. Events: combat-start · attack-fired · hit-taken ·
cast · kill · death · ally-death · **enemy-arrival (leap/displacement lands nearby)** ·
displacement-suffered · **tile/field-entered** · HP-threshold · overtime-start.

## Unit AI — legible, deterministic, cheap (new, round 6: "sim AI must be smart to an extent")
- Baseline: fixed targeting rule per unit (nearest default; kits override) + shortest-path
  toward target with **field-aware hex scoring**: among near-equal next hexes, avoid
  harmful fields; bounded detour (never a long walk around); deterministic tie-breaks in
  fixed hex order (order-independence preserved). No lookahead, no planning.
- **Principle: smart enough not to look dumb, dumb enough to predict.** Placement skill
  requires predictable units; depth belongs in KITS, not the pathfinder (e.g. "ignores
  harmful fields" or "always routes through own glyphs" can be passives).

## v1 scope line
In: full Clock set · glyph system (fire/heal/consecrate/wall to start) · Leap/Push/Pull/
Root + collisions · reaction triggers · shapes above · field-aware pathing.
Out (post-v1): predetermined terrain (never), Charge, cones/rows, cleanse/tenacity,
non-wall summons, morale/rout, high-ground.

