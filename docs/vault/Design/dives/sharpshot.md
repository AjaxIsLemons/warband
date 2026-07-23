# Deep dive #4 — Sharpshot (v1.0 SETTLED, 2026-07-23)

Status: **SETTLED** (Jake's v1.0 steer: the scaler must scale **autos, not casts** —
"which resource is your damage in, mana or swings?" is the fork question. Volley reworked
from a ramping multi-cast into a swing-window steroid with ramping width; Ricochet's verb
absorbed by the fork itself → True Flight replaces it). Champion working name adopted:
**Calamity Vance, the Last Deadeye** (Wild West). **Documented specialist exception #2**
(with Shade; both paths DEEPEN ranged dps — the fork is a PAYLOAD split, not an archetype
change).

## The kit

**Identity: the queen of distance — and distance is a stat you can see.**
Fork question: **which resource is your damage in — mana (Sniper) or swings (Volleyer)?**

- **C — recruit** *(ranged dps)*. Starter: **Longbow** (r4, moderate damage, slow-ish).
  Innate: **Full Draw** — *her swings deal +X% damage per hex of distance to her target*
  (gradient, not threshold: always live, felt every swing; diving her visibly weakens
  her — built-in counterplay). Signature: **Piercing Bolt** — damage to her target and
  every enemy on the line behind it. Specializations: bows + muskets.
- **B — Sniper** *(DEEPEN — the nuker; the ability is the payload, autos are filler)*:
  Piercing Bolt targets the **farthest enemy**, deals +big%, line runs board-length.
  Alpha-strike artillery.
- **B — Volleyer** *(DEEPEN — the scaler; the autos are the payload, the cast feeds
  them)*: Piercing Bolt becomes **Volley** — *for her next X swings she fires +N extra
  arrows at the enemies nearest her target (each dealing Y% swing damage; Full Draw
  computed off her primary target), and each cast permanently increases N by 1 for the
  rest of the fight.* Extra arrows with no free target strike her primary again. By late
  fight her swings are a spray — the barrage that grows, drawn on her autos.

## A/S web (verb-riders, explicit)

- **Sniper A:** **Twin Nock** [AUTO] — *every 3rd swing fires twice; the second arrow
  deals Y% damage (4th vote for the Nth-swing counter)* | **Overpenetration** [SIG] —
  *each enemy the Bolt passes through adds +X% damage for enemies farther down the line*.
- **Sniper S:** **One Breath** [AUTO] — *swings come half as often but each deals double
  damage* | **Kill Window** [SIG] — *if the Bolt kills its primary target, she refunds
  half her mana instantly (kill-gated, bounded — passed the Momentum check)*.
- **Volleyer A:** **Rolling Fire** [AUTO] — *after each cast, her next 3 swings come Y%
  faster* | **Splitheads** [SIG] — *arrows fired during a Volley window Pierce 1 hex
  behind their target (the pierce heritage kept in the scaler)*.
- **Volleyer S:** **Arrowstorm** [SIG] — *the fight starts as if she had already cast
  Volley twice (window active, N=2 — front-loads the ramp)* | **True Flight** [SIG] —
  *her extra arrows deal 100% damage instead of Y% (width becomes weight)*.

## The build web (A × S braid)
Sniper: TwinNock+OneBreath = **Doubletap** ✦ · TwinNock+KillWindow = **The Drumbeat** ·
Overpen+OneBreath = **Railshot** · Overpen+KillWindow = **Chainfall** ✦ (line kills chain
casts).
Volleyer: RollingFire+Arrowstorm = **The Barrage** ✦ · RollingFire+TrueFlight =
**Fan the Hammer** · Splitheads+Arrowstorm = **Sky of Splinters** · Splitheads+TrueFlight
= **Lead Storm** ✦ (full-damage piercing spray).

## Weapon wardrobe (ADR 0012)
Sniper × musket: the gunslinger — Full Draw + slow enormous hits from max range · Sniper ×
bow: the classic longshot · Volleyer × bow: the fan weaver — fast ramping spray ·
Volleyer × musket: every swing a shotgun blast — fewer, monster volleys · unmastered
spice: Volleyer × daggers — point-blank barrage, Full Draw at zero (she gave up her whole
innate for speed).

## Banner hooks
- Opening-kill / "first blood" banners — Sniper deletes a backliner before lines lock.
- Cast-count banners (*"every 3rd ally cast: X"*) — Volley cadence stays constant (the
  window is fixed, only width ramps), so the hook survives the rework.
- Positional banner texture: *"allies 4+ hexes from their target gain X"* — her lifestyle,
  and placement-legible team play (also feeds Full Draw comps).

## Sim gaps this dive adds
**Next-N-swings charge status** (2 votes: Volley window, Rolling Fire) · **multi-target
swing rider** (+N extra arrows @Y% with per-fight ramp counter + overflow-to-primary —
absorbs Ricochet's verb) · distance-scaled damage (Full Draw — StatRule needs
target-distance input) · Nth-swing counter (Twin Nock — 4th vote) · killer attribution on
Death events (Kill Window) · double-swing rider (Twin Nock) · pre-stacked cast state at
fight start (Arrowstorm).

## Open
None at design level. X (window length), N growth, Y% magnitudes placeholder until
sweep/playtest.
