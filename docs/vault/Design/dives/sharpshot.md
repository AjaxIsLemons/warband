# Deep dive #4 — Sharpshot (v0.1 PROPOSAL, 2026-07-22)

Status: **PROPOSED**. Fork-law resolution: roster's Sniper|Volleyer was double-DEEPEN;
rather than a second specialist exception (Shade holds that card), **Volleyer reworks to
ADD disruptor** — the split-shot fantasy survives as a Rooting fan. Standard B-fork class.
Explicit-language law throughout; numbers placeholder.

## The kit (proposed)

**Identity: the queen of distance.** Ranged r4; the whole kit pivots on one geometric fact —
whether anyone is near her. Placement's job is keeping her math true.

- **C — recruit** *(ranged dps)*. Starter: **Longbow** (r4, moderate damage, slow-ish).
  Innate: **Deadeye** — +1 weapon range while no enemy is within 2 hexes of her.
  Signature: **Piercing Bolt** — deals damage to her target and every enemy on the line
  behind it (Pierce). Specializations: bows + muskets (new category — slow, heavy-hitting
  ranged; the Wild-West wardrobe axis).
- **B — Sniper** *(DEEPEN ranged dps)*: Piercing Bolt now targets the **farthest enemy**,
  deals +big%, and its line runs from her through the far board edge. Backline artillery.
- **B — Volleyer** *(ADD disruptor — the rework)*: Piercing Bolt splits into a **3-line
  fan** (her target's line + the two adjacent lines); every enemy hit is briefly
  **Rooted**. The crowd-pinning fan.

## A/S web (verb-riders, explicit)

- **Sniper A:** **Longer Breath** [AUTO] — *while no enemy is within 3 hexes of her, her
  swings deal +X%* | **Overpenetration** [SIG] — *each enemy the Bolt passes through adds
  +X% damage for enemies farther down the line*.
- **Sniper S:** **One Breath** [AUTO] — *her swings come half as often but each deals
  double damage (deliberate rhythm; same DPS, different burst texture)* | **Kill Window**
  [SIG] — *if the Bolt kills its primary target, she refunds half her mana instantly
  (kill-gated tempo — bounded by enemy count, checked against the Momentum precedent:
  no crowd-compounding)*.
- **Volleyer A:** **Pinning Shots** [AUTO] — *every 3rd swing briefly Roots its target
  (3rd vote for the Nth-swing counter)* | **Spread Volley** [SIG] — *the fan grows to 5
  lines; each bolt's damage is reduced (explicit tradeoff: control up, damage down)*.
- **Volleyer S:** **Lockdown** [SIG] — *enemies hit by 2+ bolts of the same cast are
  Rooted twice as long and briefly Slowed after* | **Ricochet** [AUTO] — *her swings
  bounce to one additional enemy within 2 hexes of her target for Y% of the damage*.

## The build web (A × S braid)
Sniper: LongerBreath+OneBreath = **The Long Silence** ✦ (the untouched metronome) ·
LongerBreath+KillWindow = **Manhunter** · Overpen+OneBreath = **Railshot** ·
Overpen+KillWindow = **Chainfall** ✦ (line kills chain casts).
Volleyer: Pinning+Lockdown = **The Net** ✦ · Pinning+Ricochet = **Scattergun** ·
Spread+Lockdown = **Field of Pins** · Spread+Ricochet = **Lead Storm** ✦.

## Weapon wardrobe (ADR 0012)
Sniper × musket: the gunslinger — slower, enormous single hits (musket mastery:
placeholder) · Sniper × bow: the classic longshot · Volleyer × bow: the fan weaver ·
Volleyer × musket: the suppressor — fewer, heavier pins · unmastered spice: Volleyer ×
daggers — the point-blank pin-fighter who gave up her distance.

## Banner hooks
- Root banners (*"Rooted enemies take +X% from ranged attacks"*) — Volleyer team play.
- Opening-kill banners — Sniper deletes a backliner first; "first blood" rules amplify her.
- Positional banner texture: *"allies 4+ hexes from their target gain X"* — her whole
  backline lifestyle, and a placement-legible team rule.

## Sim gaps this dive adds
**Fan/multi-line attack shape** (Volleyer — first shape beyond single line; feeds the
weapons pass) · "no enemy within R of owner" condition (Deadeye/Longer Breath — owner-
radius-empty check) · killer attribution on Death events (Kill Window needs "I killed it";
Death events don't carry Source today) · Nth-swing counter (3rd vote — promoted).

## Open (for Jake)
Bless/adjust the fork rework (Volleyer as Rooting fan) and the four A/S pairs · champion
name (theme.md floating: **Calamity Vance, the Last Deadeye**, Wild West) · One Breath's
rhythm (half rate / double hit) — feel check in the viewer eventually.
