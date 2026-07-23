# Deep dive #1 — Cleric (v0.2, 2026-07-22)

Status: **IN PROGRESS** — spine settled with Jake (kit inversion, both forks, verb grammar);
A/S nodes are riffs awaiting reaction. Template: ADR 0011 (+ same-day amendment born here);
weapons: ADR 0012.

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

- **War-Priest A:** auto-rider — *her swings scorch the nearest Burning enemy for X% of the
  amount dealt or healed* | sig-rider — *enemies killed while Burning spread their stacks*.
- **War-Priest S:** sig — **Conflagration**: Pyre detonates all Burn stacks in radius |
  auto — **Undying Zeal**: while any enemy Burns, she gains attack speed and her swings
  also heal her.
- **Lifebinder A:** sig-rider — *pulse leaves healing ground* | auto-rider — *every 3rd
  censer swing grants its target brief Haste*.
- **Lifebinder S:** sig — **Great Chorus**: pulse centers on the TWO lowest allies | sig —
  **Sanctuary**: pulsed allies also gain Shield.

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
A/S picks from the riffs above · champion name (floating: *Sister Maren of the Waning
Bell*, Plague Medieval) · exact numbers = placeholder doctrine, sweep/playtest.
