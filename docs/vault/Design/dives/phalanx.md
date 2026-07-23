# Deep dive #7 — Phalanx (v1.0 SETTLED, 2026-07-23)

Status: **SETTLED** with three Jake amendments: **(1) Leap-Taunt radius 1→2** — now
exactly his reach, so the punish Counter always connects · **(2) Counter law is
DIRECTIONAL, not fizzle** — a Counter swings toward the attacker and strikes the first
enemy within reach on that line (ranged poking him can riposte into their own
frontline; the outplay is a clear firing line, not just range) · **(3) Skewer
hex-behind accepted with a flagged worry** — vs spaced backlines the second hex may
often be empty; playtest watch-item, not a redraw. Champion working name adopted:
**Leonnatos of the Unbroken Line** (Hellenic). Fork law: ✓ — Pikewall ADDs anti-dive
disruption (the ADR 0003 anti-assassin answer), Lancer DEEPENs line dps. **Second
consumer of Taunt · first kit that needs Counter built.**

## The kit

**Identity: the second rank that fights over the first.**
Fork question: **does the spear hold YOUR line (Pikewall) or break THEIRS (Lancer)?**

- **C — recruit** *(reach fighter / melee dps)*. Starter: **Pike** (r2, moderate,
  hits over the front row — the defining stat: he stands BEHIND an ally and still
  fights). Innate: **Riposte** — *he Counters the first attack made against him; the
  charge refreshes each time he casts.* **Counter law (settled here): a Counter is an
  instant free swing TOWARD the attacker — it strikes the attacker if within his
  reach, otherwise the first enemy within his reach on the line toward the attacker;
  a clear line means the riposte cuts air.** Signature: **Skewer** — *damage to his
  target and the hex directly behind it* (playtest watch: connect rate of the second
  hex vs spaced backlines). Specializations: pikes + tower shields (the hoplite pair).
- **B — Pikewall** *(ADD disruptor/anti-dive — the wall)*: *he Counters EVERY attack
  against him (not just the first), and any enemy whose Leap ends within 2 hexes of
  him is instantly Countered and **Taunted** by him* (radius = his reach, so the spear
  always answers; Bulwark's status reused as-is — forced targeting + Silence). Bulwark
  taunts an area on cast; Phalanx taunts divers reactively. Jump the backline, land on
  the spear.
- **B — Lancer** *(DEEPEN — line dps)*: Skewer becomes a **full-line lunge** — *damage
  to every enemy on the line through his target* (the Pierce heritage, melee edition).

## A/S web (verb-riders, explicit)

- **Pikewall A:** **Spearpoint** [AUTO] — *his swings deal +X% against enemies at max
  reach (exactly r2 — the spacing reward, placement-legible)* | **Sharp Rebuke**
  [COUNTER] — *his Counters briefly Disarm whoever they strike (existing status — the
  sword knocked aside; under the directional law that can be a frontliner eating an
  archer's riposte)*.
- **Pikewall S:** **The Unbroken Line** [AURA] — *adjacent allies gain his Riposte:
  the first attack against each of them is answered by Phalanx (directional law,
  swung from HIS hex); recharges on his cast* | **Give No Ground** [STATUS] — *while
  at least one enemy is Taunted by him, he takes X% less damage (taunted-by-owner
  condition — 2nd vote w/ Bulwark)*.
- **Lancer A:** **Overreach** [AUTO] — *his swings also hit the hex directly behind
  his target for Y% (every auto a mini-Skewer; inherits the hex-behind watch-item)* |
  **Deep Thrust** [SIG] — *the lunge deals +X% per enemy it passes through
  (Overpenetration's melee sibling)*.
- **Lancer S:** **Sarissa** [SIG] — *the lunge runs board-length* | **Perfect Form**
  [AUTO] — *his swings come Y% faster while no enemy is within 1 hex of him (the
  untouched-spearman tempo — spacing as a stat)*.

## The build web (A × S braid)
Pikewall: Spearpoint+UnbrokenLine = **The Bronze Hedge** ✦ · Spearpoint+GiveNoGround =
**The Standing Stone** · SharpRebuke+UnbrokenLine = **Wall of Spears** ·
SharpRebuke+GiveNoGround = **The Bitter Gate** ✦ (dive in, get Disarmed, held forever).
Lancer: Overreach+Sarissa = **The Longest Reach** ✦ · Overreach+PerfectForm =
**Drillmaster** · DeepThrust+Sarissa = **Breach the Line** ✦ · DeepThrust+PerfectForm =
**The Duelist's Measure**.

## Weapon wardrobe (ADR 0012 — pikes debut as the reach category)
Pike mastery (placeholder rider: keeps r2 + over-the-row targeting). Pikewall × pike:
the classic hoplite wall · Pikewall × tower shield: the immovable gate — gives up reach
for bulk, the pure Counter-tank · Lancer × pike: the sarissa artillery · Lancer × tower
shield: the breaching ram · unmastered spice: Phalanx × musket — **the counter-sniper**:
r4 reach means his directional riposte answers even the archers in kind (all answer,
no wall bulk).

## Banner hooks
- Formation banners (*"allies adjacent to an ally gain X"*) — his lifestyle; the
  placement-legible phalanx comp.
- Anti-dive family (*"when an enemy Leaps: X"* — heroes.md's banner space) — Pikewall
  is the payoff piece; pairs with the Leap-stun banner texture from Shade's dive.
- Taunt banners (with Bulwark — the two Taunt owners make a lockdown comp).

## Sim gaps this dive adds
**Counter effect** (v1 vocabulary since day 1, first kit that needs it: instant free
swing, **directional line fallback** — attacker if in reach, else first enemy in reach
on the line toward the attacker; charge semantics + refresh-on-cast) · Leap-landing
reactive trigger (enemy Leap ends within R — Leap events exist, the listener is new) ·
Taunt (2nd consumer — Bulwark's spec, still in backlog) · range-exact condition rider
(Spearpoint) · Disarm-on-Counter rider (Disarm built ✓) · taunted-by-owner condition
(2nd vote) · no-enemy-within-R condition (Perfect Form) · aura-granted Counter
(Unbroken Line) · melee line-lunge shape (lines exist; lunge variant new). Notably:
NO kill-gated mechanics this kit — deliberate verb diversity.

## Open
None at design level. Playtest watch-items: Skewer/Overreach hex-behind connect rate
vs spaced backlines · Pikewall counters-every-attack power level under focus fire.
Magnitudes placeholder until sweep/playtest.
