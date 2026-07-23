# Deep dive #2 — Bulwark (v0.1 PROPOSAL, 2026-07-22)

Status: **PROPOSED** — full web drafted under ADR 0011 (+ amendments) and ADR 0012 for
Jake's reaction. Explicit-language law applied throughout. Numbers placeholder.

## The kit (proposed)

**Identity: the wall the warband is built behind.** Tank, melee r1, slow. The mana engine
loves him: he is hit constantly, so his Slam clock runs hot — his cost is that he deals
little on his own. Fork = *what kind of wall*: one that breaks what touches it (Juggernaut)
or one that carries the team on its back (Warden).

- **C — recruit** *(tank)*. Starter weapon: **Tower Shield** (r1, low damage, slow; shield
  category). Signature: **Shield Slam** — deals damage to the nearest enemy and briefly
  Stuns it. Innate: **Bastion** — starts combat with a Shield equal to X% of max HP.
  Weapon specializations: shields + maces. Complementary verbs ✓: auto = damage, sig =
  control + the fork's defensive halves.
- **B — Juggernaut** *(tank ADDs disruptor)*: Slam now hits **every adjacent enemy** and
  the Stun lasts longer.
- **B — Warden** *(tank ADDs support)*: Slam also grants a Shield to **every adjacent
  ally** (magnitude scales with Slam's damage; placeholder).

## A/S web (verb-riders, explicit)

- **Juggernaut A** (choose one): **Concussive Swings** [AUTO] — *every 3rd swing briefly
  Slows the target (applies Slow; no damage change)* | **Momentum** [SIG] — *each enemy hit
  by Slam grants him mana (bigger crowds = faster next Slam)*.
- **Juggernaut S**: **Faultline** [SIG] — *Slam's radius grows to 2 hexes; every enemy hit
  is Stunned (full duration, no falloff)* | **Grudgekeeper** [AUTO] — *his swings deal
  bonus damage equal to W% of the Shield he currently holds (damage only; Shield is not
  consumed)*.
- **Warden A**: **Rhythm of the Wall** [AUTO] — *every swing also grants the lowest-HP
  adjacent ally a small Shield* | **Wide Aegis** [SIG] — *Slam's ally-Shield reach grows to
  2 hexes (the damage/Stun half stays at 1)*.
- **Warden S**: **Living Rampart** [SIG] — *allies Shielded by Slam also gain brief
  AttackUp — the wall fights back* | **Vigil** [AUTO] — *his swings also heal the lowest-HP
  adjacent ally for V% of the damage dealt*.

## The build web (A × S braid)
Juggernaut: Concussive+Faultline = **The Landslide** · Concussive+Grudgekeeper =
**Stonegrinder** ✦ · Momentum+Faultline = **Perpetual Quake** ✦ (crowds feed slams feed
stuns — THE loop) · Momentum+Grudgekeeper = **Iron Tide**.
Warden: Rhythm+Rampart = **Marching Fortress** · Rhythm+Vigil = **Infirmary Wall** ✦ ·
WideAegis+Rampart = **Rallying Wall** ✦ · WideAegis+Vigil = **Quiet Keep**.

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
Shield-scaled attack bonus (Grudgekeeper — StatRule needs a shield-reading input) ·
"every Nth swing" rider counter (Concussive Swings; Cleric's Chorus also wants it — now
2 votes, promote to vocabulary) · dual-radius effect (Wide Aegis: ally-half radius ≠
enemy-half radius on one sig).

## Open (for Jake)
Bless/adjust the four A/S pairs · champion name (theme.md riff floating: **Brakka,
Shieldmaid of the Bronze Hour**, Bronze Age) · fork feel: is Warden's Slam-Shield the
right support verb, or should his support half live on the aura pattern like Mercy Aura?
