# Deep dive #2 — Bulwark (v0.1 PROPOSAL, 2026-07-22)

Status: **PROPOSED** — full web drafted under ADR 0011 (+ amendments) and ADR 0012 for
Jake's reaction. Explicit-language law applied throughout. Numbers placeholder.

## The kit (proposed)

**Identity: the wall the warband is built behind.** Tank, melee r1, slow. The mana engine
loves him: he is hit constantly, so his Slam clock runs hot — his cost is that he deals
little on his own. Fork = *what kind of wall*: one that breaks what touches it (Juggernaut)
or one that draws the storm onto itself (Warden — taunt).

- **C — recruit** *(tank)*. Starter weapon: **Tower Shield** (r1, low damage, slow; shield
  category). Signature: **Shield Slam** — deals damage to the nearest enemy and briefly
  Stuns it. Innate: **Bastion** — starts combat with a Shield equal to X% of max HP.
  Weapon specializations: shields + maces. Complementary verbs ✓: auto = damage, sig =
  control + the fork's defensive halves.
- **B — Juggernaut** *(tank ADDs disruptor)*: Slam now hits **every adjacent enemy** and
  the Stun lasts longer.
- **B — Warden** *(DEEPEN tank — protection by redirection; Jake's taunt redraw)*: the Slam
  stays but becomes a challenge — it **deals no damage and does not Stun**; adjacent
  enemies are **Taunted** onto him for T seconds and he gains a **self-Shield**.
  (**Taunt**, defined once: the enemy's target is forced to the taunter while it lasts;
  normal targeting resumes on expiry. NEW sim status — second vote after Phalanx's
  Pikewall; promoted to v1 vocabulary.)

## A/S web (verb-riders, explicit)

- **Juggernaut A** (choose one): **Concussive Swings** [AUTO] — *every 3rd swing briefly
  Slows the target (applies Slow; no damage change)* | **Shockwave** [SIG] — *Slam also
  grants HIM a flat Shield per enemy hit (stacks add; replaces Momentum — Jake: mana-per-
  hit compounded into a snowball; Shield-per-hit converts crowds to survivability, no loop)*.
- **Juggernaut S**: **Faultline** [SIG] — *Slam's radius grows to 2 hexes; every enemy hit
  is Stunned (full duration, no falloff)* | **Grudgekeeper** [AUTO] — *his swings deal
  bonus damage equal to W% of the Shield he currently holds (damage only; Shield is not
  consumed)*.
- **Warden A** (redrawn around Taunt): **Cowing Bellow** [SIG] — *enemies Taunted by his
  Slam also get AttackDown while the Taunt lasts (they swing weaker at him)* | **Iron
  Rebuke** [AUTO] — *every swing against an enemy Taunted by him also grants him a small
  Shield (sustain while holding the line)*.
- **Warden S**: **Unbreakable Challenge** [SIG] — *the Slam's Taunt reach grows to 2 hexes
  and the self-Shield scales up — the whole field answers him* | **Retribution** [AUTO] —
  *each swing also deals P% of its damage to every other enemy currently Taunted by him
  (the gathered crowd shares the beating)*.

## The build web (A × S braid)
Juggernaut: Concussive+Faultline = **The Landslide** ✦ (everything slowed, then everything
stunned) · Concussive+Grudgekeeper = **Stonegrinder** · Shockwave+Faultline = **Bedrock** ·
Shockwave+Grudgekeeper = **Iron Tide** ✦ (crowds become Shield, Shield becomes damage).
Warden: Bellow+Challenge = **The Great Gather** ✦ (everything gathered, everything cowed) ·
Bellow+Retribution = **Punishing Court** · Rebuke+Challenge = **The Immovable** ·
Rebuke+Retribution = **The Reckoning** ✦ (self-sheltered storm).

## Weapon wardrobe (ADR 0012)
Juggernaut × tower shield: the classic anvil (shield mastery: swings grant a small Shield —
feeds Grudgekeeper) · Juggernaut × mace: damage-tank bruiser (mace mastery: double mana per
swing — feeds Momentum) · Warden × tower shield: pure wall · Warden × censer: **medic-tank**
(heal-autos from the frontline; unmastered until a spec grants it) · unmastered spice:
Juggernaut × bow — a ranged stun-battery who wants to be hit less (fights his own mana
engine; legal, weird, someone will make it work).
**Wardrobe test: 5 distinct loadouts ✓.**

## Banner hooks
- **On-Shield banner family** ("whenever an ally gains Shield: …") — Warden fires it
  constantly; like On-Heal, magnitude must assume per-swing frequency (balance note).
- *"Enemies recovering from Stun stay Slowed"* — Juggernaut amplifier.
- Cross-class: Warden clump + Cleric's Fortress Garden = the ultimate turtle — placement
  web at team level, exactly the emergent-synergy doctrine (no traits needed).

## Sim gaps this dive adds
**Taunt status** (forced targeting; 2 votes — Warden + Phalanx Pikewall — promoted to v1
vocabulary) · shield-scaled attack bonus (Grudgekeeper — StatRule needs a shield-reading
input) · "every Nth swing" rider counter (Concussive Swings; Cleric's Chorus also wants
it — 2 votes, promoted) · splash-to-taunted (Retribution — swing damage echoed to units
carrying my Taunt).

## Open (for Jake)
Bless/adjust the redrawn A/S pairs · champion name (theme.md riff floating: **Brakka,
Shieldmaid of the Bronze Hour**, Bronze Age).
