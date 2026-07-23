# ADR 0014 — Aura & ally-adjacency law: muster for states, live range for moments

**Date:** 2026-07-23 · **Status:** accepted (Jake's formulation, Banneret dive) ·
**Participants:** Jake + Claude

## Context
Players control PLACEMENT and nothing else — movement is emergent (ADR 0001). A
persistent "adjacent allies gain X" aura is therefore a bonus the player can aim only
for the opening seconds; once the formation dissolves it turns on and off by noise the
player can't play. Jake's concern, raised at the Banneret dive (the aura class), plus
the twin worry: a radius-1/2 ally-cast on an in-combat melee unit hits whoever happens
to be swirling past. Precedent already existed: Cleric's Mercy Aura was settled in
dive #1 as *placement-based* — this ADR generalizes that call into law.

## Decision — the law
**Both models are supported in the sim; which one an effect uses is decided by its
duration class:**
1. **Permanent ally auras key off MUSTER, not motion.** "Allies *placed*
   adjacent/within R" — membership snapshotted at fight start, locked for the fight,
   delivered wherever members drift. Placement is the only substrate where a
   persistent aura is an actual decision.
2. **Casts read LIVE geometry at the moment of cast.** A cast is a moment, not a
   state — whoever is there when the banner dips, rallies. Ally-facing cast radii
   must be sized honestly for scrum drift (generous radius, a row, or a named target
   like Lifebinder's lowest-ally — not r1 around a melee unit).
3. **Enemy-facing spatial effects and self-measured conditions stay live.** Taunt
   radii, fields, novas, Slam — enemies bring themselves to you. Full Draw's
   distance, Perfect Form's spacing, Lead From the Front's press-of-bodies — one
   unit's continuously readable relationship to the fight.

## Consequences
- **The Company (Banneret pattern):** allies placed adjacent at muster are "under the
  banner" — a named membership set that permanent-aura effects (innate, Steady the
  Line, The Colors Do Not Fall) key off. Cast effects (Rally) stay live-radius per
  clause 2.
- **Grandfather audit (complete):** Phalanx "The Unbroken Line" retexted to *placed*
  adjacent · formation-banner texture retexted to *placed* adjacent · Cleric Mercy
  Aura already compliant (the precedent) · Cleric Pyre nova legal under clause 2
  (cast moment on a unit built to stand in the scrum) · all other kits enemy-facing
  or self-measured — untouched.
- **Sim:** placement passives already built; adds muster-membership set (Company) +
  member conditions. Live attached-aura machinery remains for enemy-facing presence
  effects. Live ally-radius delivery remains for casts.
