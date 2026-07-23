# Deep dive #4 — Sharpshot (v0.2 PROPOSAL, 2026-07-22)

Status: **PROPOSED** (v0.2 after Jake's rework notes: Deadeye innate killed — dead
conditional; Root killed — movement too rare to matter; no disruptor here). **The lens:
the standard backline ranged carry with two DEEPENED routes — one scaler, one nuker.
Documented specialist exception #2** (with Shade; the pure-dps classes are the
specialists — the fork law still bites for hybrid-capable classes).

## The kit (proposed)

**Identity: the queen of distance — and distance is now a stat you can see.**

- **C — recruit** *(ranged dps)*. Starter: **Longbow** (r4, moderate damage, slow-ish).
  Innate: **Full Draw** — *her swings deal +X% damage per hex of distance to her target*
  (gradient, not threshold: always live, felt every swing; diving her visibly weakens
  her — built-in counterplay). Signature: **Piercing Bolt** — damage to her target and
  every enemy on the line behind it. Specializations: bows + muskets.
- **B — Sniper** *(DEEPEN — the nuker)*: Piercing Bolt targets the **farthest enemy**,
  deals +big%, line runs board-length. Alpha-strike artillery.
- **B — Volleyer** *(DEEPEN — the scaler; Jake's multi-target instinct IS the ramp)*:
  Piercing Bolt becomes **Volley** — fires a bolt at each of the **3 nearest enemies**
  (no lines, single hits), and **each cast permanently adds +1 bolt for the rest of the
  fight**. If bolts outnumber enemies, extras strike the nearest enemy again. The barrage
  that grows.

## A/S web (verb-riders, explicit)

- **Sniper A:** **Twin Nock** [AUTO] — *every 3rd swing fires twice; the second arrow
  deals Y% damage (4th vote for the Nth-swing counter)* | **Overpenetration** [SIG] —
  *each enemy the Bolt passes through adds +X% damage for enemies farther down the line*.
- **Sniper S:** **One Breath** [AUTO] — *swings come half as often but each deals double
  damage* | **Kill Window** [SIG] — *if the Bolt kills its primary target, she refunds
  half her mana instantly (kill-gated, bounded — passed the Momentum check)*.
- **Volleyer A:** **Rolling Fire** [AUTO] — *after each cast, her next 3 swings come Y%
  faster* | **Splitheads** [SIG] — *each Volley bolt Pierces 1 hex behind its target
  (mini-lines — the pierce heritage kept in the scaler)*.
- **Volleyer S:** **Arrowstorm** [SIG] — *Volley starts the fight at 5 bolts instead of
  3 (front-loads the ramp)* | **Ricochet** [AUTO] — *her swings bounce to one additional
  enemy within 2 hexes of her target for Y% of the damage*.

## The build web (A × S braid)
Sniper: TwinNock+OneBreath = **Doubletap** ✦ · TwinNock+KillWindow = **The Drumbeat** ·
Overpen+OneBreath = **Railshot** · Overpen+KillWindow = **Chainfall** ✦ (line kills chain
casts).
Volleyer: RollingFire+Arrowstorm = **The Barrage** ✦ · RollingFire+Ricochet =
**Scattergun** · Splitheads+Arrowstorm = **Sky of Splinters** · Splitheads+Ricochet =
**Lead Storm** ✦.

## Weapon wardrobe (ADR 0012)
Sniper × musket: the gunslinger — Full Draw + slow enormous hits from max range · Sniper ×
bow: the classic longshot · Volleyer × bow: the fan weaver — fast ramping barrage ·
Volleyer × musket: heavy volley — fewer casts, monster bolts · unmastered spice: Volleyer ×
daggers — point-blank barrage, Full Draw at zero (she gave up her whole innate for speed).

## Banner hooks
- Opening-kill / "first blood" banners — Sniper deletes a backliner before lines lock.
- Cast-count banners (*"every 3rd ally cast: X"*) — Volleyer casts most as the fight goes.
- Positional banner texture: *"allies 4+ hexes from their target gain X"* — her lifestyle,
  and placement-legible team play (also feeds Full Draw comps).

## Sim gaps this dive adds
**Multi-target volley** (N distinct nearest targets + overflow rule + per-fight ramp
counter) · distance-scaled damage (Full Draw — StatRule needs target-distance input) ·
Nth-swing counter (4th vote) · killer attribution on Death events (Kill Window) ·
double-swing rider (Twin Nock).

## Open (for Jake)
Bless the scaler/nuker rework + Full Draw innate (alt considered: consecutive-swings-same-
target ramp — the focus-fire archer; say the word if that's more the fantasy) · the four
A/S pairs · champion name (floating: **Calamity Vance, the Last Deadeye**, Wild West).
