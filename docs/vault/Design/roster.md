# First-playable roster — DRAFT v0.1 (2026-07-22, for Jake's reaction)

8 heroes × 2 paths (ADR 0001 budget). Format: **Chassis** — innate / signature → *Path A* | *Path B*.
Numbers come later (content before numbers was circuit's mistake in reverse — here shapes
first, tuning in the harness). Synergies listed are emergent, never rule-based.

1. **Bulwark** (tank, melee r1, slow) — innate: starts combat Shielded (% of HP) /
   sig **Shield Slam**: damage + brief Stun to nearest enemy →
   *Juggernaut*: Slam hits all adjacent + bigger Stun | *Warden*: Slam also Shields adjacent allies.
2. **Cleric** (support, ranged r3) — innate: small heal trickle to lowest ally /
   sig **Mend**: heal lowest-HP ally →
   *War-Priest*: Mend also smites nearest enemy for the amount healed; gains melee-grade stats |
   *Lifebinder*: Mend also lays a small healing-ground glyph under the target.
3. **Shade** (assassin, melee r1, fast) — innate: combat start, **Leap** to farthest enemy hex /
   sig **Backstab**: heavy single hit →
   *Reaper*: Backstab Executes targets below threshold | *Phantom*: after casting, Leap to a new target.
4. **Sharpshot** (ranged r4) — innate: +1 Range while no enemy within 2 hexes /
   sig **Piercing Bolt**: damage in a line (Pierce) →
   *Sniper*: targets farthest enemy, huge damage | *Volleyer*: bolt splits into 3 smaller shots.
5. **Pyromancer** (caster, ranged r3, weak auto / big mana — the glyph anchor) — innate:
   attacks apply 1 DoT stack / sig **Fire Glyph**: ignite a radius-1 field at the target
   (DoT while standing/entering) →
   *Inferno*: bigger fields, DoT spreads on kill | *Starfall*: no field, triple-damage single hit + DoT.
6. **Berserker** (melee r1) — innate: attack speed rises as HP falls /
   sig **Frenzy**: next attacks come instantly →
   *Bloodreaver*: Frenzy attacks Lifesteal | *Rampager*: Frenzy attacks Cleave adjacent.
7. **Phalanx** (reach fighter, melee r2 — hits over the front row) — innate: Counters the
   first attack against it each cast cycle / sig **Skewer**: damage target + hex behind it →
   *Pikewall*: Counter always, and enemies whose Leap lands within 1 hex are Countered +
   forced to target Phalanx (the anti-assassin answer, ADR 0003) | *Lancer*: Skewer becomes a full-line lunge.
8. **Banneret** (support, melee r1 — the warband's soul) — innate: adjacent allies attack
   faster (aura) / sig **Rally**: grant Mana to adjacent allies →
   *Herald*: Rally also Shields | *Warcaller*: Rally hits the whole row, adds attack damage.

## The keyword vocabulary this roster requires (= the sim's v1 vocabulary)
Shield · Stun · Burn (DoT) · Heal · Lifesteal · Execute · Counter ·
Cleave (adjacent) · Pierce (line) · Splash (ring) · Leap · Aura (adjacency) ·
Mana-grant · Range-condition. ~14 effects — circuit's engine expressed more than this;
the event→trigger→effect grammar covers all of it.

## Emergent synergy examples (why no trait system is needed)
- Banneret auras + Warden shields reward clumping — enemy Pyromancer Splash punishes it.
- Phantom re-Leaps feed Reaper-style backline pressure; Bulwark Stuns protect against it.
- Rally (mana-grant) accelerates Stall-type casters; Shade hunts exactly those backliners.
- Sharpshot's range-condition wants an empty frontline; Phalanx reach punishes hers.

❓ Open reactions wanted: which chassis feel wrong/missing? (No wall-summoner, dedicated
displacer, or debuffer in v1 — deliberate scope cuts; the wall-summoner Jake likes is the
strongest 9th-hero / post-v1 candidate.)
