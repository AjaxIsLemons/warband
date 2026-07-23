# The weapons pass — catalog, riders, rarity, the forge (v0.1 PROPOSAL, 2026-07-23)

Status: **PROPOSED** (board 1d — first conversation after the dive campaign). Inputs:
ADR 0005 (weapon = the attack profile: damage/interval/range/shape; weapon-required),
ADR 0012 (universal equip, weapon carries ONE latent mastery rider, classes tag 1–2
specializations, heal-weapons legal), ADR 0006/0009 (per-node shop: 2 item cards,
freeze/reroll/sell 50%, `IRunContent` pools **by act**), all 8 dive wardrobes, the
~12-item first-playable cap. Numbers placeholder per doctrine — SHAPES are the
decisions here. Settles ADR 0005's deferred "attack shapes" question. Becomes ADR 0015
once Jake settles it.

## 1 · The catalog — 11 categories, the dives already built it

The masters matrix (chassis tags per dives; ✚ = fork-added per ADR 0012 clause 3):

| Category | Physics (placeholder) | Masters | Mastery rider |
|---|---|---|---|
| Twin daggers | r1 · fastest · lightest | Shade · Berserker | +crit chance *(settle Shade's placeholder)* |
| Sabre | r1 · fast · light | Banneret | **NEW:** first swing after each cast is an automatic crit (the officer's finisher) |
| Mace | r1 · medium · medium | Bulwark · War-Priest✚ | double mana per swing *(settled ×2)* |
| Greataxe | r1 · slow · heavy · **CLEAVE** | Berserker | **NEW:** overkill damage carries to the nearest enemy |
| Tower shield | r1 · slow · low dmg · bulk statline | Bulwark · Phalanx | swings grant a small self-Shield *(settled)* |
| Pike | r2 · medium (the second-rank reach) | Phalanx | **NEW:** the braced spear — +X% vs enemies adjacent to an ally |
| Censer | r3 · medium · heal-autos (lowest ally) | Cleric · Pyromancer | overheal → Shield *(settled)* |
| Staff | r3 · medium · caster statline | Cleric · Pyromancer | brief Haste on cast *(settled Cleric; Pyro's placeholder reconciled to this)* |
| Bow | r4 · medium | Sharpshot · Shade | **NEW:** +1 range (the only range rider — queen-of-distance physics) |
| Musket | r4 · slowest · hugest hits | Sharpshot | **NEW:** the opening shot — first swing each fight deals double |
| Standard | r1 · light (the pole) | Banneret | muster/Company bonuses +X% potency *(settled Banneret)* |

Coverage: every category has ≥1 master; daggers/bows/censers/staves/shields have 2 —
the single-master five (sabre/greataxe/pike/musket/standard) are where the Relic rule
(§3) earns its keep.

**Attack shapes resolved (ADR 0005 debt):** every weapon is single-target EXCEPT
greataxe (cleave: target + adjacent enemies at Y%). Pierce-line/splash stay
ability-side verbs in v1 (Skewer, Piercing Bolt, Pyre). One new shape to build, total.

## 2 · The rider law
**Mastery riders amplify ENGINES, never add verbs** — mana (mace), tempo (staff,
sabre), defense (shield, censer), crit (daggers), potency (standard). Verbs live in
spec nodes; a weapon swap re-skins your delivery physics, it never steals a class
identity. Sanctioned exception: **reach categories get physics riders** (bow +1 range;
pike's braced-spear condition) — reach IS their engine.

## 3 · Rarity — temper tiers, not more items
**Rarity is a TIER on the same 11 weapons, not new content.** Three tiers:
**Worn → Honed → Relic** (theme: a Relic is a weapon that outlived its era — "forged
in a world the Hour already ate"; time-law compliant).

- **Worn:** base stats. **All starter weapons begin Worn** — your free censer is its
  weakest self; the upgrade economy includes your own gear from node 1.
- **Honed:** stat scale-up (placeholder ~+25%).
- **Relic:** bigger scale-up (~+50%) **+ the mastery rider goes live for EVERY
  wielder — masters instead get the rider doubled.**

The Relic rule is the playstyle engine: the dive wardrobes' "unmastered spice" builds
(Volleyer × daggers, Warden × censer medic-tank, Phalanx × musket counter-sniper,
Pyro × tower shield) are deliberately rider-less early — a Relic drop late-run is the
moment a spice build matures into a real line. Runs get a wardrobe ARC: mastered
basics early, Relic pivots late.

**Stock is act-gated, never record-gated:** `IRunContent` pools by act (already
shaped, ADR 0009) — act 1–2 stocks Worn, mid acts add Honed, late acts add Relic
(placeholder curve). Anti-snowball law holds: everyone's shop deepens with the clock,
not with their winrate. **Wager-linked rarity (Greedy stocks rarer gear) considered
and recommended DEAD** — it stacks gear advantage on top of ADR 0007's gold advantage
for winners; one snowball axis is enough.

## 4 · Upgrades — the Tower's forge
**Yes: reforge in place.** Any shop tick, pay gold to raise one owned weapon a tier
(Worn→Honed→Relic; escalating placeholder prices). **The forge follows the front:**
reforge is capped at the current act's stock ceiling — you can keep a beloved starter
current, never skip the pacing curve.

Rejected alternatives: **dupe-merge** (TFT-style — with an 11-weapon pool dupes are
constant, bench is 2, and hoarding fights the tinker pillar) · **kill-fed growth**
(weapon XP — snowbally, adds tracking, rewards the already-winning board).

Economy fallout: reforged weapons sell at 50% of TOTAL gold sunk (mirrors ADR 0009's
hero rule — the "respec my wardrobe" path). Reforge is the gold sink that competes
with dupes/slots/rerolls — the widen-vs-deepen tension gets a third axis: **sharpen**.

## 5 · How weapons enable playstyles (the three levers)
1. **Physics × innate:** Firebrand loves the fastest weapon (stacks/sec); Full Draw
   loves range; Burning Hours turns the greataxe's slow heavies into a death-spiral
   crescendo. The composer already delivers this free (range/interval on weapon).
2. **Rider × engine:** mace on any caster chassis doubles the cast engine; staff
   Haste-on-cast compounds Rally/Frenzy loops; standard potency scales the Company.
3. **Relic × spice:** unmastered riders unlocking late-run (§3) — every class's
   wardrobe section becomes an act-4 decision, not a day-1 meme.

## 6 · Budget math (the squeeze, resolved)
11 weapons (one per category — **the categories ARE the item list**) + 1 trinket = 12
✓ cap honored. Tiers multiply depth without adding items. Trinkets get a mini-pass
later (ADR 0005: bundles of existing primitives — cheap, post-playtest).

## 7 · Sim/run gaps this pass adds
Loadout composer: **tier param** (stat scalar + rider-gate: mastered / Relic-any /
master-doubled) · cleave attack shape (greataxe — only new shape) · overkill-carry
effect (greataxe rider) · forced-crit-after-cast (sabre — 2nd vote w/ Cold Return) ·
engaged-with-ally condition (pike rider) · first-swing-double flag (musket) · +range
rider (bow — composer-side) · **reforge shop action** (ADR 0009 amendment) ·
act-tiered item pools (`IRunContent` — shape exists, content fills it).

## Open (for Jake)
Rarity frame: temper-tiers-of-the-same-weapon (rec) vs separate rare items · the
**Relic rule** — rider-for-everyone + doubled-for-masters (rec) · reforge-only
upgrades (rec) vs dupe-merge vs both · tier names (Worn/Honed/Relic floating) · the
six NEW riders (bow/musket/sabre/greataxe/pike/dagger) — bless or redraw · confirm
wager-linked rarity stays dead · starters-begin-Worn (rec).
