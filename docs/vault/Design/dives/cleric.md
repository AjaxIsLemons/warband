# Deep dive #1 — Cleric (v1.0 SETTLED, 2026-07-22)

Status: **SETTLED** — full web blessed by Jake (explicit-language pass included; Contagion =
harder-not-longer, Conflagration = consume-and-detonate approved). Champion working name
adopted: **Sister Maren of the Waning Bell** (Plague Medieval). Numbers remain placeholder
per content doctrine. Template: ADR 0011 (+ amendments born here); weapons: ADR 0012.
Visual: claude.ai artifact "Cleric — Choice Web".

## The kit (settled spine)

**Identity: a bruiser-healer who wants to be IN the fight.** The mana engine is her engine —
she casts more the deeper she stands. The fork is a placement decision: commit to the front
or retreat to the back.

- **C — recruit** *(healer + melee dps hybrid)*. Starter weapon: **Censer** (r3 heal-auto —
  swings heal the lowest-HP ally). Signature: **Sanctified Pyre** — AoE holy fire around
  HERSELF: damages enemies, heals allies in the radius. Weapon specializations: censer +
  staff. Innate (settled): **Mercy Aura** — allies within 2 hexes Regen X/s (placement-
  based; attached-aura + Regen, expressible today; numbers placeholder).
- **B — War-Priest** *(DEEPEN the bruiser)*: Pyre radius grows and applies **Burn** stacks.
  Adds mace specialization (fork extends the wardrobe, ADR 0012).
- **B — Lifebinder** *(SWAP to backline: healer + support)*: Pyre becomes a **remote pulse
  centered on the lowest-HP ally** — heal + Haste blooming where the pain is. She stands
  back; the effect lands in the scrum; clumping matters on both ends (global was considered
  and rejected — geometry-blind, ADR 0003).

## A/S riffs (verb-rider grammar; awaiting Jake)

(Language law, Jake 2026-07-22: node text is mechanically explicit — name the target, the
effect, and whether states are applied or consumed.)

- **War-Priest A:** **Scorched Mercy** auto-rider — *her swings also deal X% of the amount
  dealt or healed as bonus damage to the nearest Burning enemy (damage only; applies no
  stacks)* | **Contagion** sig-rider — *an enemy that dies while Burning passes its
  remaining Burn stacks to the nearest enemy; stacks add, so the recipient burns HARDER
  per second, not longer*.
- **War-Priest S:** **Conflagration** sig — *casting the Pyre detonates: every Burning
  enemy in radius takes its remaining Burn damage instantly (stacks consumed), then the
  fresh Pyre re-applies Burn* | **Undying Zeal** auto — *while any enemy Burns, she gains
  attack speed and her swings also heal her*.
- **Lifebinder A:** **Lingering Grace** sig-rider — *the pulse leaves healing ground for Y
  seconds* | **Chorus** auto-rider — *every 3rd swing, ANY weapon: brief Haste to the
  target if ally, brief Slow if enemy (weapon-agnostic per the rider law)*.
- **Lifebinder S:** **Great Chorus** sig — *the pulse fires twice, one instance centered on
  each of the two lowest-HP allies (with Lingering Grace: two healing grounds)* | **Sanctuary**
  sig — *pulsed allies also gain Shield*.

## The build web (A × S braid — visual: claude.ai artifact "Cleric — Choice Web")
War-Priest: Scorched+Conflagration = **Slow Fuse** · Scorched+Zeal = **The Zealot** ✦ ·
Contagion+Conflagration = **The Detonator** ✦ · Contagion+Zeal = **Plague Candle**.
Lifebinder: Grace+GreatChorus = **The Gardener** · Grace+Sanctuary = **Fortress Garden** ✦ ·
Chorus+GreatChorus = **Tempo Weaver** ✦ · Chorus+Sanctuary = **Bulwark Choir**.
(✦ = natural partners; all four combos per path are legal — no A→S gates.)

## Weapon wardrobe (ADR 0012 universal equip)
War-Priest × mace: frontline battle-priest (mace mastery: double mana per swing — the cast
engine roars) · War-Priest × censer: sustain smiter, team that never dies · Lifebinder ×
staff: backline gardener (staff mastery: Haste on cast) · Lifebinder × censer: double-healer
(censer mastery: overheal → Shield) · unmastered spice: War-Priest × bow — reach without a
rider. **Wardrobe test: 5 distinct loadouts ✓.**

## Banner hooks (team triggers, all expressible today)
- Any **On-Heal** banner is a Cleric amplifier — and heal-AUTO units fire it every swing:
  magnitude design must assume censer-class frequency (balance note for the banner pass).
- *"Enemies you Burn take +X% damage from all sources"* — War-Priest team play.
- *"Healing ground also burns enemies standing in it"* — Lifebinder A-node zones go
  dual-purpose.

## Sim gaps this dive adds
Ally-targeting autos (ADR 0012, logged) · self/target-centered instant pulse — likely
expressible via existing field-centered-on-resolved-target pattern; verify at content time.

## Open
None at design level. Numbers = placeholder doctrine (sweep/playtest). Sim gaps logged
above + "every Nth swing" rider counter (Chorus needs it).
