# Deep dive #5 — Pyromancer (v1.0 SETTLED, 2026-07-23)

Status: **SETTLED** (fork frame + pairs blessed; **the Burn law settled the dive** —
decay model chosen, see below; never-decay White Heat rejected by Jake as broken →
retexted to the plain-double crown). B-fork confirmed (roster's late-bloomer flag
declined: late-bloom is for risk-profile forks per the Shade precedent; an archetype
SWAP wants early divergence). Censer confirmed as second specialization. Champion
working name adopted: **Ilion-7, Cinder of a Dead Star** (far-future). Fork law: ✓
clean — Inferno DEEPENs the zoner, Starfall SWAPs zone→burst.

## The kit

**Identity: the caster who makes ground a weapon.**
Fork question: **does the fire live on the GROUND (Inferno) or on the TARGET (Starfall)?**

- **C — recruit** *(caster dps / zoner-lite)*. Starter: **Ashwood Staff** (r3, weak
  quick swings, big mana pool — the cast IS the class). Innate: **Firebrand** — *her
  swings apply 1 stack of Burn*. Signature: **Fire Glyph** — *ignite the target's hex +
  radius 1: enemies standing in or entering the blaze take Burn stacks over time.*
  Specializations: staves + censers.
- **B — Inferno** *(DEEPEN — area denial; the ground is the weapon)*: Fire Glyph ignites
  **radius 2**, and *when a Burning enemy dies, the hexes under it ignite* — the fire
  spreads through their ranks. Owns the map, punishes clumps and chokepoints.
- **B — Starfall** *(SWAP zone→burst; the target is the weapon)*: no field — Fire Glyph
  becomes **Starfall**: *a massive single hit on her current target (~3× glyph damage)
  plus a heavy slug of Burn stacks.* The artillery caster.

## The Burn law (settled here — sim vocabulary, the Taunt promotion path)
**Burn is a single integer pool per unit; all sources merge.** On a fixed global cadence
(0.5s placeholder), each Burning unit takes damage equal to its current stacks, then
loses 1 stack. No durations, no timers — "how long does a stack last" isn't a question
this model asks. Stacks are a currency (Detonate consumes them). Tall piles pay
superlinear total damage (N + N−1 + …) — the deliberate scaler fantasy, and THE lever to
watch at sweep time. Rejected: per-stack duration timers (bookkeeping nobody feels under
Firebrand's constant application) · never-decaying stacks (Jake: broken — unbounded
growth even within fight length).

## A/S web (verb-riders, explicit)

- **Inferno A:** **Choking Smoke** [FIELD] — *enemies inside her fields swing X% slower
  (zone as disruption)* | **Stoke the Coals** [AUTO] — *her swings against enemies inside
  a field apply +2 extra Burn stacks*.
- **Inferno S:** **The World Alight** [SIG] — *casting Fire Glyph also ignites the hex
  under every currently-Burning enemy* | **Everburn** [FIELD] — *her fields never expire
  (the board slowly becomes fire — permanent denial)*.
- **Starfall A:** **Detonate** [SIG] — *Starfall consumes the target's Burn stacks,
  dealing +Z% damage per stack consumed (autos load the shell, the cast fires it)* |
  **Kindling** [AUTO] — *her swings apply +1 additional Burn stack*.
- **Starfall S:** **Dying Star** [SIG] — *if Starfall kills its target, it immediately
  recasts free on the nearest Burning enemy (kill-gated, Burning-bounded chain)* |
  **White Heat** [STATUS] — *enemy Burn ticks deal double damage (2× pool payout —
  plain-double crown, One Breath/Widowmaker precedent; replaced never-decay, rejected
  as broken)*.

## The build web (A × S braid)
Inferno: Choking+WorldAlight = **Firestorm Front** · Choking+Everburn = **Nothing Grows
Here** ✦ (the slow lava lockout) · Stoke+WorldAlight = **The Spreading Blaze** ✦ ·
Stoke+Everburn = **Tend the Pyre**.
Starfall: Detonate+DyingStar = **Chain Reaction** ✦ (stack-nukes that chain on kill) ·
Detonate+WhiteHeat = **Flashpoint** · Kindling+DyingStar = **Meteor Shower** ·
Kindling+WhiteHeat = **The Slow Collapse** ✦ (maximum DoT pressure).

## Weapon wardrobe (ADR 0012 — staves debut as the caster category)
Stave mastery (placeholder rider: casts charge faster / bonus mana on swing). Inferno ×
staff: the classic warlock anchoring the back · Inferno × censer: the fire-priest —
walking the front swinging embers, Burn in melee arcs · Starfall × staff: the long-range
howitzer · Starfall × censer: the zealot who marches at you while the sky falls ·
unmastered spice: Pyro × tower shield — the immovable pyre who stands INSIDE her own
Everburn field.

## Inscription hooks
- Burn Inscription family (*"allies' attacks apply Burn"* / *"Burning enemies take +X%"*) —
  she's the amplifier AND the enabler; frequency-costed like On-Heal. One-pool law means
  Inscription Burn merges into her engine (and White Heat doubles it — deliberately global).
- Opening-cast Inscriptions (*"first ability cast each fight: +X%"*) — big-mana Starfall
  openers.
- Ground texture: *"allies standing in friendly fields gain X"* — pairs with Inferno
  (and Cleric's Pyre) for placement-legible fire-party comps.

## Sim gaps this dive adds
**Burn decay engine** (single int pool + global tick cadence — law settled above,
implementation new) · field-spawn-on-death (Inferno spread) · consume-stacks-for-damage
(Detonate) · free-recast-on-kill w/ target filter (Dying Star — killer attribution 2nd
vote, cascade bounds already built) · enemies-in-field conditional riders (presence
statuses exist — verify shape covers Choking/Stoke) · field permanence flag (Everburn) ·
attack-speed-down status · Burn-tick damage multiplier (White Heat).

## Open
None at design level. Magnitudes (tick cadence, per-stack damage, radii, Z%) placeholder
until sweep/playtest.
