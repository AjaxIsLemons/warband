# Deep dive #6 — Berserker (v1.0 SETTLED, 2026-07-23)

Status: **SETTLED** (Jake: full web blessed as proposed — **including Deathless:
cheat-death approved as a new mechanic class**, once-per-fight bounded). Champion
working name adopted: **Ulfrik, Who Burns His Hours** (Viking Age). **Resolves the
audit's fork-law "verify" flag:** Bloodreaver is a genuine **ADD** (melee dps + drain
bruiser — sustain becomes a second hat), Rampager DEEPENs into AoE dps. Law satisfied,
no exception needed. Fork timing: B default (no late-bloom rationale). Frenzy specced as
a **next-N-swings window — 3rd vote for the charge-status shape** (Volley window,
Rolling Fire); it's load-bearing across three kits now.

## The kit

**Identity: the engine that runs hotter as it breaks.**
Fork question: **does the frenzy feed HIM (Bloodreaver) or hit THEM (Rampager)?**

- **C — recruit** *(melee dps)*. Starter: **Greataxe** (r1, slow, heavy). Innate:
  **Burning Hours** — *his swings come faster the lower his HP* (gradient, not
  threshold — always live, mirrors Full Draw; the theme name is literal: he trades his
  remaining time for speed). Signature: **Frenzy** — *his next N swings come instantly*
  (the flurry dump). Specializations: greataxes + daggers.
- **B — Bloodreaver** *(ADD drain bruiser — the frenzy feeds him)*: *Frenzy swings
  Lifesteal Z%.* The loop: low HP → Burning Hours accelerates → cast → burst-heal off
  the flurry. He survives BY fighting — the berserker who wants to be hurt.
- **B — Rampager** *(DEEPEN — the whirlwind; the frenzy hits them)*: *Frenzy swings
  Cleave adjacent enemies.* Low HP and surrounded is his ideal state.

## A/S web (verb-riders, explicit)

- **Bloodreaver A:** **Scent of Blood** [AUTO] — *his swings deal +X% against enemies
  below half HP (target-below-HP condition — existing sim vote)* | **Red Harvest**
  [SIG] — *if a Frenzy swing kills, the window extends +2 swings (kill-gated, bounded)*.
- **Bloodreaver S:** **Deathless** [STATUS] — *the first time he would die each fight he
  instead drops to 1 HP and gains a full Frenzy window (cheat-death, once per fight —
  NEW mechanic class, flagged below)* | **Crimson Tide** [AUTO] — *Lifesteal beyond full
  HP becomes Shield (the overheal engine)*.
- **Rampager A:** **Reckless Swing** [AUTO] — *his swings deal +X% but he takes +Y% more
  damage (the risk dial — feeds Burning Hours on purpose)* | **Aftershock** [SIG] —
  *the final swing of each Frenzy Cleaves in a full ring (all 6 adjacent hexes)*.
- **Rampager S:** **Avalanche** [SIG] — *Frenzy gains +N swings (the longer storm)* |
  **No Quarter** [AUTO] — *his Cleave hits deal 100% damage instead of Y% (width becomes
  weight — True Flight's sibling)*.

## The build web (A × S braid)
Bloodreaver: Scent+Deathless = **The Long Death** · Scent+CrimsonTide = **Bleed Them
Dry** ✦ · RedHarvest+Deathless = **Last Man Standing** ✦ · RedHarvest+CrimsonTide =
**The Red Feast**.
Rampager: Reckless+Avalanche = **The Landslide** ✦ · Reckless+NoQuarter = **Death
Spiral** · Aftershock+Avalanche = **Stormcenter** · Aftershock+NoQuarter = **The
Threshing Floor** ✦.

## Weapon wardrobe (ADR 0012 — greataxes debut)
Greataxe mastery (placeholder rider: heavy swings, small self-Shield per swing?).
Bloodreaver × greataxe: the reaping drain — huge lifesteal chunks · Bloodreaver ×
daggers: the mosquito — a hundred tiny sips at Burning-Hours speed · Rampager ×
greataxe: the classic whirlwind · Rampager × daggers: the blender — instant flurries
wall to wall · unmastered spice: Berserker × censer — the blood-monk, healing allies
with every swing while standing at death's door.

## Banner hooks
- Low-HP threshold banners (*"allies below half HP gain X"*) — his lifestyle; the
  placement-legible version of the berserker comp.
- On-kill family — Red Harvest turns kill banners into window-chains.
- Shield/overheal texture with Crimson Tide (pairs with Bulwark/Herald shield comps).

## Sim gaps this dive adds
**Next-N-swings charge status — 3rd vote, promote to build-next** · self-HP-gradient
StatRule (Burning Hours — sibling of Full Draw's target-distance input) · Lifesteal
(v1 vocabulary since day 1; first kit that needs it built) · **cheat-death once-per-fight
(Deathless — NEW mechanic class, needs Jake's explicit yes)** · window-extend-on-kill
(Red Harvest — killer-attribution 3rd vote) · overheal→Shield conversion (Crimson Tide) ·
self-damage-amp rider (Reckless Swing) · full-ring finisher cleave (Aftershock).
Deathless approved by Jake → cheat-death is now sanctioned sim vocabulary.

## Open
None at design level. Magnitudes (N swings, Z% lifesteal, cleave %) placeholder until
sweep/playtest.
