# Combat spectacle — the visual language (2026-07-25)

Jake's brief: *"improve combat rendering significantly — spell/signature casting system,
detailed aura/hex effects, status icons with time+stacks, better attack VFX, and propose
more. Go big — the reward for playing IS the combat."* This doc owns the creative
direction and authoring spec; [[fx-runtime]] owns the engine. Both extend
[[fight-legibility]] (its Phase 3, expanded) under its genre laws — Riot proportionality
(ult biggest, defensives QUIET, autos small), telegraph = hitbox, status-on-material,
audio as the free channel.

## 1. Palette law — "the light is the Hour"
Three axes, never mixed: **hue = WHAT · undertone = WHOSE · saturation+bloom = HOW MUCH.**

| Lane | Meaning | Ash/idle | Payoff |
|---|---|---|---|
| Bone | physical, autos | #E8E2D4 | hot-white #FFF6E3 |
| Blood | damage numbers, death | #8A2F24 | #D6452F |
| Gilt | CRIT only (locked) | — | #F2C14E, 1 frame |
| Ember | fire / Burn | #7E3A1D | #E2581F / #FF9E45 |
| Verdant | healing only | #7FA85C | #A3D37E |
| Ward | shield/defense | #A9D6D8 ≤35% op | none — **defensives have no payoff state** |
| Ash-violet | control + debuff (the Waning's color) | #7B6A96 | #A78FD3 |
| Sand | mana / time / tempo | #D8C69A | lit #FFE7A8 |

**Intensity tiers** (bloom threshold 1.1 ⇒ bloom eligibility is *authored by tier*):
**T0** ambient + ALL defensives (HDR ≤0.8 — never blooms; a defensive S-rank gets its rank
via AREA and audio, never brightness) · **T1** autos/riders (≤1.0, one bright frame) ·
**T2** signature casts (1.3–1.8, ≤0.3 s) · **T3** S-crowns/Starfall/deaths/CheatDeath
(2.5–4.0 + ONE board-level accent). Anti-rainbow: one lane per effect; no cross-lane
gradients; **effects never encode team** — rims/bars own it (proposal: rim fresnel
candle-warm #FFD9A0 ally / ash-cold #9FB2C4 enemy).

## 2. Cast grammar — one 4-beat sentence, eight sigils
1. **Windup** (B 0.4 s / S 0.5–0.6 s): era-sigil unfolds under the caster (1 hex,
   lane-tinted, T0→T1) + 2–3 rising wisps + cast pose + windup riser SFX.
2. **Release**: sigil snap-flash 1 frame at tier peak; mana drains with a sand-spill.
3. **Impacts**: standard hit language per victim; multi-victim resolves 1 tick apart via
   the beat sequencer (the causality read).
4. **Recovery**: sigil burns out to ash over 0.3 s.

Era sigils (512² mono masks, lane-tinted): bulwark bronze knotwork · phalanx meander ·
berserker rune wheel · cleric bell-and-rosary · banneret compass heraldry · sharpshot
revolver cylinder · shade redaction-glitch · pyromancer collapsing stellar orbit.

## 3. Signature specs (tier · beats · "what the player says out loud")
- **Cleric / Sanctified Pyre** T2 — bell sigil + candle smoke → nova ring expands to
  exactly r1 (**the ring IS the hitbox**), bone flame w/ verdant sparkle → enemies flash,
  allies sparkle green. Bell toll + whump. *"She burned the ground around herself."*
  **War-Priest**: r2 + ember tongues, Burn ignites as the ring passes. **Lifebinder**: one
  candle-bead arcs to the LOWEST ally (travel = attribution) → verdant bloom + haste
  chevrons. **Great Chorus** (S): two beads, 2 ticks apart, visibly re-seeking. *"The bell
  rang twice."*
- **Bulwark / Shield Slam** T2, short 0.3 s windup — shield-face slam, stone-crack on the
  target hex, cracked-hourglass Stun icon + desat-freeze. **Juggernaut**: stomp — cracks on
  all 6 adjacent hexes. **Faultline** (S, T3): 0.6 s overhead, dust → radial ground
  fissures to exactly r2 + ONE rationed shake; ember-glow cracks persist (aftermath).
  *"The whole yard cracked open."* **Warden / Open Challenge**: DEFENSIVE = T0/T1 — thin
  ash-violet tethers snap to every enemy in r3/r4 then collapse into leash icons; zero
  bloom; the S-ness is that the archers' tethers exist.
- **Shade / Backstab** T2 fast — 60% alpha + scanlines, glitch sigil stutters → one hard
  slash, thin blood core; on kill the Leap plays as ghost-trail blink. Static tick, knife.
  *"The ghost gutted him and was gone."* Phase status = NO color: alpha + desat +
  scanlines; re-entry is a 1-frame white glitch.
- **Sharpshot / Piercing Bolt** T2 — kneel-aim, dim rust aim-line traces the ACTUAL line
  hexes (telegraph=hitbox, 30% op) → heavy tracer + powder smoke; victims flash in order
  down the line. **Sniper** (T3): board-length aim-line dwells 0.3 s + eye glint → one huge
  number. **Overpenetration**: tracer brightens per body passed. **Volleyer**: cast
  deliberately SMALL (a reload — ghost-arrow fan + countable chevron ramp); the spectacle
  moves to her windowed autos: primary + N thin split tracers per swing. *"Count her
  chevrons."*
- **Pyromancer / Fire Glyph** T2 — orbit sigil spins, ember motes gather INWARD (a star
  collapsing) → fire slug lobs, ground ignites r1. *"She lit that ground — get out or
  cook."* **Inferno**: 3 falling cinders, r2; mini-glyph blooms under Burning corpses.
  **Everburn** (S): T3 cast then T0 forever — SOLID field edge + black smoke column; **the
  expiry blink never plays — the missing countdown IS the read.** **World Alight** (S):
  every Burn pip board-wide brightens 2 ticks → r0 ignitions under every Burning enemy at
  once (the one sanctioned mass-simultaneous event). **Starfall** T3 — she points UP; a
  light streaks down from top-of-frame → column of white-orange flame, 8 Burn pips slam on
  with an audible stack-tick, 2-frame camera punch. *"She dropped a star on his head."*
- **Berserker / Frenzy** T2 self — rune wheel, rears, breath fog → blood steam off the
  shoulders; for the WHOLE window his weapon trails ember-red and his rim pulses 1 Hz
  (the tell IS the state). Window end: steam snuffs, 2-frame slump; Aftershock rides it.
- **Phalanx / Skewer** T2 — meander sigil, pike level, dim 2-hex line → thrust: thin white
  line-flash, two thuds 1 tick apart. *"One thrust, two bodies."* Lancer/Deep Thrust/
  Sarissa: same sentence, line grows to board-length; Deep Thrust reuses Overpen's
  escalation; Sarissa adds a full-line pre-flash.
- **Banneret / Rally** T1/T2 support-quiet — banner-plant, cloth ripples once → sand-mote
  wave to exactly r2 at 30% op; **each ally's mana bar visibly surges** (mana cast — bars
  are the payoff). Drum + cloth snap. **Herald**: + ward film per recipient. **War-Caller**:
  a second, darker ash-violet wavefront chases it into enemies — rims dim, moves drag
  violet. *"One shout — ours surged, theirs slogged."*

## 4. Field / ground language
Hex-snapped decals on the EXACT member hexes. Three layers: **edge ring** (authoritative
extent) · **scrolling floor fill** · ≤3 vertical particles per hex (silhouette first at
60 px). **Spawn**: edge traces the perimeter 0.2 s, then floor 0.3 s — boundary before
body. **Idle**: T0, below bloom. **Pulse** (event-driven): floor T1 flash; burst/pip only
on OCCUPIED hexes. **Expiry**: edge double-blinks the final 1.0 s, floor drains
edge-inward + rising motes.
- **Burning ground**: #7E3A1D + crawling ember veins; flame licks on occupied hexes;
  leaves charred tint after expiry (aftermath).
- **Grace**: candle-wax pool, ivory 25% + thin warm ring — QUIET until it pulses.
- **Dread** (attached): ash-violet smoke swirl + chain-dash ring that WALKS with the
  banneret; occupants carry the read (drag trails + Slow icon).
- **Wall**: basalt/bronze prop slab; AttackBlocked = dust puff + NO number (the missing
  number is the whiff read).

## 5. Status icons — the Hour owns time
Billboarded row above the HP bar, 24 px, max 4 + "+N" chip. **Priority**: control (Stun/
Taunt/Phase — leftmost, 1.2×, never hidden) → transformative windows (Frenzied,
MultiShotWindow, NextSwingCrit, armed CheatDeath) → counted engines (Burn, MultiShotRamp,
AttackUp, CritUp — stack count bottom-right) → rest. **Countdown = a radial clock-sweep**
darkening across the icon (remaining ticks are on the wire after [[fx-runtime]] S2).
Icons are the roster; the headline stays on the unit's material (Burn emissive, Stun
desat-freeze, Phase alpha; add Frenzied 1 Hz red rim). Style: "flat dark-fantasy engraved
icon, [EMBLEM], [LANE COLOR] on near-black round field, woodcut ink-stamp, readable at
24 px." **Law: every control status is an hourglass variant** (Slow = thick sand, Stun =
SHATTERED hourglass) — the Last Hour owns control.

## 6. Attack / auto language — autos feed the mana bar
All T1 bone; identity = arc/tracer shape + sound (Mechabellum's per-weapon-tracer lesson).
Daggers = two crossing nicks/snick · Sabre = thin elegant arc/shing · Mace = blunt
arc/thock · Greataxe = widest arc with a 1-frame hang, cleave continues the SAME arc ·
Tower Shield = flat shove/clang · Pike = 2-hex thrust line/thud · Standard = pole swipe +
cloth ripple · Longbow = lobbed arrow 0.15 s/thunk · **Musket = instant smoke line +
muzzle flash + the loudest auto (crack)** — its slow cadence earns it · Censer = smoking
thurible arc, warm mote lobbed to the lowest ally/chime · Staff = wisp tinted by chassis
lane. **Weapon tiers**: Honed +20% tracer brightness; **Relic = faint permanent lane
edge-glow on the weapon prop** (T0 — visible on the board without a tooltip).
**Crit**: gilt number + 1-frame gold edge flash + gold tracer core + a tick of hit-stop.
**Shield absorb**: bar segment flashes ward-cyan, muted slate number — the hit "never
reached him." **Whiff/blocked**: dust + NO number.

**Trigger-rider law (fills the silent 2nd-most-common damage cause): riders are ECHOES,
not swings.** No windup/lunge/hit-stop/shake, numbers 0.7×, and every rider draws a brief
spark-link from its ROOT to its result inside the beat (the attribution). Families:
Consume (Detonate — the victim's Burn pips visibly fly INTO the blast, number scales with
them) · Thorns (ricochet spark back along the hit) · Splash/split (thin fan sparks) ·
Drain (red motes victim→drinker + small green number) · **Counter** — the one rider
allowed a real swing, reversed grammar: no windup, sharp directional flash, ting-thud ·
**Execute** — resolves as a death + callout, not small.

## 7. Go-big proposals (ranked; 9 is a day-one law)
1. **Ash-death & the board remembers** (M) — deaths dissolve feet-up into ash + rising
   embers, leaving a permanent ash-silhouette decal + the dropped weapon prop as a grave
   marker; Faultline leaves cracks, expired fire leaves char. By fight end the board IS
   the history. THE grim signature move; markers are T0 so zero ongoing legibility cost.
2. **Deathless dress** (S) — 0.4 s playhead hold, board dims 40%, the survivor alone stays
   candle-lit, red rune flare + DEATHLESS callout, Frenzy bursts out of the freeze.
3. **S-crown callouts** (S) — thin banner strip (STARFALL · FAULTLINE) for T3 casts +
   Execute + Deathless ONLY. Hard ration: never on B-sigs or riders.
4. **Sound layer** (M) — 8 era windup risers, ~10 impact families, state cues (mana-full
   tick, death knell, Waning drone). The genre's proven readability fix.
5. **Fight-ender slow-mo → summary card** (S/M) — LAST death only: 0.2× for 0.6 s +
   vignette, then the FightSummary card slides out of the freeze (sim data already built).
6. **Waning ambient board** (S/M) — edge ember drift, ash gusts, candle-flicker key light
   (±5%). All T0.
7. **Hourglass mana rings** (M) — sand ring at each unit's feet filling with mana,
   flipping lit-gold at full (moves the Underlords flip down where the eye lives).
8. **Overtime: the Hour dies onscreen** (M) — when Storm damage begins, board-edge hexes
   candle-snuff inward: char creep, temperature drop, ash rain. Storm damage = the world
   killing. One "storm level" parameter driven by storm events.
9. **Camera punch + shake DISCIPLINE** (S, day one) — 2-frame push-in on T3 impacts;
   trauma shake only for Death/Faultline/Starfall class; ration ≈1 per 3 s. This keeps
   1–8 from stacking into mush.
10. **Prop idle life** (S each) — banner flutter, censer smoke wisp, musket match glow.

**Killed darlings** (rejected on the laws): mid-fight kill-cam cuts (spatial stability) ·
shake-on-crit (saturates) · Haste speed-trails (half the board smears) · team-colored
ability VFX (breaks one-hue-one-meaning) · heat haze (broken under URP) · full-scale
rider numbers (board whites out).

## 8. Asset manifest (one deliberate GenerateAsset batch, ~60 gens, ≪ 1000/mo)
8 era sigils (512² mono masks) · tileable noises: ember-vein, ash-swirl, wax-pool,
dissolve · flipbooks 4×4: flame-lick, ember-burst, smoke-puff · soft mote sprite · ash
body-silhouette decals ×2–3 · ground-crack + radial-spoke decals · skybox ("dying amber
hour, ash clouds, candle horizon") · 16 status icons (§5 prompt skeleton) · ~26 ElevenLabs
stings (8 risers · 10 weapon impacts · nova whump, fissure crack, star whistle+boom, taunt
horn, rally drum, mana-full tick, death knell, DEATHLESS sting, execute shk-thud, Waning
drone). Procedural noise stays in code where possible; textures 256–512 px, committed.

## Build sequencing (maps to [[fx-runtime]] phases)
Palette/tier discipline into tuning.json first (retunes existing tells, zero assets) →
P0 sim (Burn fold bug + durations + ability identity) → P1 FX foundation → P2 fields →
P3 status icons (glyph fallback before icon gen) → P4 cast grammar + sigils → P5 death →
P6 riders/polish → proposals in ranked order, #9 adopted from day one.

## Decisions (Jake, 2026-07-25 evening)
1. **Go-big menu — "honestly i love them all, but we can def shelf lower prio stuff like
   10 until later."** All ten approved in principle. **This arc:** 1 ash-death, 2 Deathless
   dress, 3 S-crown callouts, 4 sound layer, 5 fight-ender slow-mo → summary card, 9
   discipline law (day one). **Shelved for the next wave:** 6 Waning ambient board, 7
   hourglass mana rings, 10 prop idle life. **8 Overtime** stays its own later slice.
2. **Asset batch — APPROVED in full** (~60 generations: sigils, noises, flipbooks, decals,
   skybox, 16 icons, ~26 stings), spent per-phase as each system needs its assets.
3. Replay v5 + fixture regen and the Burn fold bug fix are mechanical/correctness — being
   built without further ask.
4. Asset Store packs (Epic Toon FX ~$40) remain OPTIONAL raw material, not a dependency —
   revisit only if wanted after seeing the first pass.
