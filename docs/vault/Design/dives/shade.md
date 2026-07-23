# Deep dive #3 — Shade (v0.1 PROPOSAL, 2026-07-22)

Status: **PROPOSED**. Template firsts: **late-bloomer (fork at A)** and the **documented
double-DEEPEN specialist exception** (both paths deepen melee dps; the fork is a RISK
PROFILE, not an archetype change — ADR 0011 exception clause, used as designed).
Jake's axes: ranged-vs-melee → routed to the wardrobe (ADR 0012); crit-fishing and
phase-out survival → the fork. Ability crits stay deferred: the gamble rides AUTOS.

## Ladder shape (late-bloomer per ADR 0011)
C recruit → **B = matured signature + 1-of-2 PATH-AGNOSTIC riders** (choice-per-rank
preserved without forking early) → **A = THE FORK**, bigger operation than a B-fork
(the compensation) → S = 1-of-2 crowns within the path. Braid: B(2) × path(2) × S(2) =
8 builds.

## The kit (proposed)

**Identity: the knife that picks its moment.** Melee r1, fast, fragile. Ambush opens every
fight in the enemy backline. The fork isn't WHAT he is — it's what he's willing to risk.

- **C — recruit** *(melee dps)*. Starter: **Twin Daggers** (r1, fast, low damage, high
  base crit). Innate: **Ambush** — at combat start, Leap to a free hex adjacent to the
  farthest enemy. Signature: **Backstab** — heavy single hit on his current target.
  Specializations: daggers + bows.
- **B — matured signature (baseline, no choice):** *if Backstab kills its target, he
  immediately Leaps to the farthest enemy* — the roaming assassin both paths build on
  (late-bloomer's stronger baseline). **Plus 1-of-2 path-agnostic riders:**
  **Killer's Tempo** [AUTO] — *after any enemy dies within 2 hexes of him, his swings come
  Y% faster for a short time* | **Opportunist** [SIG] — *Backstab deals +Z% damage to
  targets below half HP*.
- **A — THE FORK (risk profile):**
  - **Reaper** — *the gamble.* His autos gain a large flat crit-chance bonus, and **his
    crits against enemies below H% max HP Execute** (kill outright). Explicit: Execute
    triggers on auto-attack crits only; no stacks, no partial — dead or nothing.
  - **Phantom** — *the phase.* **Phase** (NEW status): when damage taken within a short
    window exceeds P% of his max HP, he Phases — untargetable and immune for D seconds
    (attackers retarget), then re-enters with a Leap to the farthest enemy. Each Phase
    grants a permanent AttackUp stack for the fight — survival IS his scaling.
- **S — crowns:**
  - Reaper: **Twist the Knife** [SIG] — *Backstab deals double damage to a target he has
    crit since his last cast* | **Widowmaker** [AUTO] — *his crit multiplier is doubled*.
  - Phantom: **Here and Gone** [SIG] — *after Backstab resolves he always Phases briefly,
    regardless of damage taken (offense-scheduled phasing)* | **Cold Return** [AUTO] —
    *his first swing after each Phase is an automatic crit*.

## The build web (B × S braid, per path)
Reaper: Tempo+Twist = **Flurry of Knives** · Tempo+Widowmaker = **Red Roulette** ✦ ·
Opportunist+Twist = **The Closer** ✦ · Opportunist+Widowmaker = **Headsman**.
Phantom: Tempo+Here-and-Gone = **Ghostdance** ✦ · Tempo+Cold Return = **Blink Blade** ·
Opportunist+Here-and-Gone = **Executioner's Mist** · Opportunist+Cold Return =
**The Cold Cut** ✦ (phase → guaranteed crit → finish the wounded).

## Weapon wardrobe (ADR 0012 — this is where ranged-vs-melee lives)
Reaper × daggers: the classic knife gambler (dagger mastery: placeholder crit rider) ·
**Reaper × bow: the deadeye — ranged crit-fishing assassin** (Jake's ranged-dps axis,
delivered by wardrobe) · Phantom × daggers: the ghost in the scrum · Phantom × bow:
the skirmisher who phases out of dives · unmastered spice: Phantom × tower shield —
the unkillable annoyance.

## Banner hooks
- *"Your Leaps stun"* (heroes.md's original banner example) — with the B baseline
  (Leap-on-kill) Shade turns it into a chain-stun engine; costed per-Leap frequency.
- On-kill banner family — the assassin fires them most.
- Crit banners ("allies' crits also X") — Reaper amplifier; frequency note like On-Heal.

## Sim gaps this dive adds
**Phase status** (untargetable + immune, attacker retargeting, re-entry Leap — the big
one) · crit-stat modifiers as statuses (crit chance/mult — StatKind today is
AttackFlat/AttackSpeed only) · forced-crit rider (Cold Return) · **TargetBelowHpPct
condition** (Execute + Opportunist; roster's original Reaper wanted it too) ·
damage-in-window threshold trigger (Phase entry).

## Open (for Jake)
Bless the ladder shape (B baseline + agnostic riders) · the four B/S pairs + both fork
kits · champion name (theme.md floating: **Null, the Redacted**, near-future — the Phase
kit fits the redaction fantasy; alt: Edo shinobi Kagerō) · Phase magnitudes (P%, window,
D) are placeholder but the SHAPE (threshold-reactive vs cooldown) is a real question.
