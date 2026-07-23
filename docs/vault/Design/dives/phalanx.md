# Deep dive #7 — Phalanx (v0.1 PROPOSAL, 2026-07-23)

Status: **PROPOSED.** From roster.md draft (reach fighter r2 — hits over the front row;
Counter innate; Skewer → Pikewall anti-dive | Lancer full-line lunge) + ADR 0011
template. Fork law: ✓ clean per audit — Pikewall **ADD**s the disruptor/anti-dive hat
(the anti-assassin answer promised in ADR 0003), Lancer DEEPENs into line dps. Fork
timing: B default. **Second consumer of Taunt** (Bulwark's status, reused as-is) and
**first kit that needs Counter built.**

## The kit (proposed)

**Identity: the second rank that fights over the first.**
Fork question: **does the spear hold YOUR line (Pikewall) or break THEIRS (Lancer)?**

- **C — recruit** *(reach fighter / melee dps)*. Starter: **Pike** (r2, moderate,
  hits over the front row — the defining stat: he stands BEHIND an ally and still
  fights). Innate: **Riposte** — *he Counters the first attack made against him;
  the charge refreshes each time he casts.* **Counter law (specced here): a Counter
  is an instant free swing at the attacker — and only answers attackers within his
  reach (r2).* Ranged attackers outrange the answer — shooting the pikeman from
  distance is the counterplay, walking into him is the mistake.* Signature:
  **Skewer** — *damage to his target and the hex directly behind it.*
  Specializations: pikes + tower shields (the hoplite pair).
- **B — Pikewall** *(ADD disruptor/anti-dive — the wall)*: *he Counters EVERY attack
  against him (not just the first), and any enemy whose Leap ends within 1 hex of him
  is instantly Countered and **Taunted** by him* (Bulwark's status reused as-is —
  forced targeting + Silence). Bulwark taunts an area on cast; Phalanx taunts divers
  reactively. The assassin who jumps the backline lands on the spear.
- **B — Lancer** *(DEEPEN — line dps)*: Skewer becomes a **full-line lunge** — *damage
  to every enemy on the line through his target* (the Pierce heritage, melee edition).

## A/S web (verb-riders, explicit)

- **Pikewall A:** **Spearpoint** [AUTO] — *his swings deal +X% against enemies at max
  reach (exactly r2 — the spacing reward, placement-legible)* | **Sharp Rebuke**
  [COUNTER] — *his Counters briefly Disarm the attacker (existing status — the sword
  knocked aside)*.
- **Pikewall S:** **The Unbroken Line** [AURA] — *adjacent allies gain his Riposte:
  the first attack against each of them is Countered by Phalanx (if the attacker is in
  his reach); recharges on his cast* | **Give No Ground** [STATUS] — *while at least
  one enemy is Taunted by him, he takes X% less damage (taunted-by-owner condition —
  2nd vote w/ Bulwark)*.
- **Lancer A:** **Overreach** [AUTO] — *his swings also hit the hex directly behind
  his target for Y% (every auto a mini-Skewer)* | **Deep Thrust** [SIG] — *the lunge
  deals +X% per enemy it passes through (Overpenetration's melee sibling)*.
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
shield: the breaching ram · unmastered spice: Phalanx × musket — bayonet drill (r4
poke, but Counters almost never in reach — all reach, no answer).

## Banner hooks
- Formation banners (*"allies adjacent to an ally gain X"*) — his lifestyle; the
  placement-legible phalanx comp.
- Anti-dive family (*"when an enemy Leaps: X"* — heroes.md's banner space) — Pikewall
  is the payoff piece; pairs with the Leap-stun banner texture from Shade's dive.
- Taunt banners (with Bulwark — the two Taunt owners make a lockdown comp).

## Sim gaps this dive adds
**Counter effect** (v1 vocabulary since day 1, first kit that needs it: instant free
swing at attacker, reach-bound fizzle, charge semantics + refresh-on-cast) ·
Leap-landing reactive trigger (enemy Leap ends within R — Leap events exist, the
listener is new) · Taunt (2nd consumer — Bulwark's spec, still in backlog) ·
range-exact condition rider (Spearpoint) · Disarm-on-Counter rider (Disarm built ✓) ·
taunted-by-owner condition (2nd vote) · no-enemy-within-R condition (Perfect Form) ·
aura-granted Counter (Unbroken Line) · melee line-lunge shape (lines exist; lunge
variant new). Notably: NO kill-gated mechanics this kit — good verb diversity.

## Open (for Jake)
Bless the fork frame (hold-yours vs break-theirs) + the **Counter law** (reach-bound —
ranged outplays it; is that the right counterplay?) · Pikewall = counters-every-attack
(strong — the alternative is always-armed-first-attack only) · the four A/S pairs ·
pikes + tower shields as the spec pair · champion name (floating: **Leonnatos of the
Unbroken Line**, Hellenic — the S-aura is deliberately his epithet).
