# Deep dive #2 — Bulwark (v1.0 SETTLED, 2026-07-22)

Status: **SETTLED** — blessed by Jake after two redraw rounds (Warden rebuilt around
Taunt; Momentum → Shockwave). Champion working name adopted: **Brakka, Shieldmaid of the
Bronze Hour** (Bronze Age). Numbers placeholder. Visual: claude.ai artifact "Bulwark —
Choice Web".

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
  stays but becomes a challenge — it **deals no damage and does not Stun**; enemies
  **within 3 hexes** (Jake: tune in the 3–4 band) are **Taunted** onto him for T seconds
  and he gains a **self-Shield**. Radius reaches ranged attackers on purpose.
  (**Taunt**, defined once: while it lasts the enemy's target is forced to the taunter
  AND the enemy is **Silenced** — no casting, no mana gain (existing status, same
  duration); normal behavior resumes on expiry. NEW sim status — second vote after
  Phalanx's Pikewall; promoted to v1 vocabulary. Identity fallout: the Warden is the
  anti-caster tool — drag the backline into the challenge and mute it.)

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
- **Warden S**: **Unbreakable Challenge** [SIG] — *the Slam's Taunt reach grows to 4 hexes
  and the self-Shield scales up — even the backline answers him* | **Retribution** [SIG —
  rides the Slam's Taunt state] — *thorns: enemies Taunted by him take back P% of the
  damage they deal to him (reflected as damage, active while the Taunt lasts)*.

## The build web (A × S braid)
Juggernaut: Concussive+Faultline = **The Landslide** ✦ (everything slowed, then everything
stunned) · Concussive+Grudgekeeper = **Stonegrinder** · Shockwave+Faultline = **Bedrock** ·
Shockwave+Grudgekeeper = **Iron Tide** ✦ (crowds become Shield, Shield becomes damage).
Warden: Bellow+Challenge = **The Great Gather** ✦ (everything gathered, everything cowed) ·
Bellow+Retribution = **Punishing Court** (swing weak, bleed for trying) · Rebuke+Challenge
= **The Immovable** · Rebuke+Retribution = **The Reckoning** ✦ (the self-sheltered storm —
shields on every swing, thorns on every hit taken).

## Weapon wardrobe (ADR 0012)
Juggernaut × tower shield: the classic anvil (shield mastery: swings grant a small Shield —
feeds Grudgekeeper) · Juggernaut × mace: damage-tank bruiser (mace mastery: double mana per
swing — feeds Momentum) · Warden × tower shield: pure wall · Warden × censer: **medic-tank**
(heal-autos from the frontline; unmastered until a spec grants it) · unmastered spice:
Juggernaut × bow — a ranged stun-battery who wants to be hit less (fights his own mana
engine; legal, weird, someone will make it work).
**Wardrobe test: 5 distinct loadouts ✓.**

## Inscription hooks
- **On-Shield Inscription family** ("whenever an ally gains Shield: …") — Warden fires it
  constantly; like On-Heal, magnitude must assume per-swing frequency (balance note).
- *"Enemies recovering from Stun stay Slowed"* — Juggernaut amplifier.
- Cross-class: Warden clump + Cleric's Fortress Garden = the ultimate turtle — placement
  web at team level, exactly the emergent-synergy doctrine (no traits needed).

## Sim gaps this dive adds
**Taunt status** (forced targeting + Silence for the duration; 2 votes — Warden + Phalanx
Pikewall — promoted to v1 vocabulary) · shield-scaled attack bonus (Grudgekeeper — StatRule
needs a shield-reading input) · "every Nth swing" rider counter (Concussive Swings;
Cleric's Chorus also wants it — 2 votes, promoted) · thorns conditioned on attacker
carrying MY Taunt (Retribution — needs a "source is Taunted by owner" condition).

## Open
None at design level. Numbers placeholder (sweep/playtest); Taunt radius tunable 3–4.
