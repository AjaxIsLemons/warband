# Hero anatomy — v1.0

**Date:** 2026-07-24
**Status:** current structural source of truth; content and values remain first-playable
candidates until playtested.

Heroes are the persistent pieces the player learns and deepens during a run. Combat is fully
automatic; the hero system exists to make preview, preparation, loadout, and placement the
gameplay.

## Source-of-truth boundaries

- This page owns the universal anatomy shared by every player hero.
- `Design/roster.md` owns each class's PvE contract.
- `Design/dives/` owns the complete class trees.
- ADR 0011 owns the spec impact model; ADRs 0005, 0012, and 0015 own loadout and weapons.
- `Design/inscriptions.md` and ADR 0017 own the Hourstone and persistent team rules.
- `sim/Warband.Content/` owns the currently runnable content and placeholder values.
- `Design/pve-encounters.md` owns the Encounter Reveal → Planning → Play → Result flow.

## The anatomy

**Hero = Chassis + Rank/Spec Tree + Weapon + Trinket + Run Bonuses.**

Hourstone Inscriptions are team rules rather than hero anatomy. Placement is not stored
power, but it decides which parts of the composed hero can actually express themselves.

### 1. Chassis — the identity

A chassis owns:

- name, silhouette, and base combat role;
- health, movement, mana capacity, and per-rank stat growth;
- one starter weapon;
- one or more innate passives;
- exactly one auto-cast signature ability; and
- one or more base weapon specializations; a path may add another.

The chassis does **not** own a fixed basic attack. Damage, attack interval, range, attack
shape, crit profile, and auto-attack riders live on the equipped weapon. Swapping a bow for
daggers genuinely changes how and where the hero fights.

A hero may have multiple passives after chassis, nodes, items, and run bonuses compose
together. Crit remains the only ordinary in-combat random roll; automatic attacks can crit,
while abilities do not unless a future rule explicitly changes that.

### 2. Casting — the mana engine

Heroes attack according to their weapon. Attacks, damage taken, and a small universal trickle
generate Mana. At full Mana, the hero automatically casts the signature and resets the meter.
There is no player input during combat.

This couples placement to cadence:

- a tank under focus gains Mana from being hit;
- a fast weapon produces more attack-fed Mana;
- an untouched backliner relies more heavily on its natural trickle; and
- Haste, Slow, Silence, Disarm, and Mana effects bend different parts of the Clock.

Mana cost is therefore both cooldown and combat identity. The player manipulates the cast
cycle through preparation and placement rather than pressing an ability button.

### 3. Rank and spec tree — deepen the sticky piece

Heroes advance **C → B → A → S** by purchasing duplicates. Every rank-up grants:

1. a flat chassis-specific health and attack bump; and
2. a mandatory one-of-two spec choice.

The default ladder is:

- **C — Recruit:** chassis, starter weapon, innate, and base signature.
- **B — Fork:** normally selects one of two paths and changes the hero's operation.
- **A — Sharpen:** chooses one of two amplifiers inside the selected path.
- **S — Crown:** chooses one of two capstones that makes the finished engine pop off.

Fork timing may move to A for a documented late-bloomer; Shade is the current example.
Paths use ADR 0011's **ADD / SWAP / DEEPEN** language, and named specialist exceptions may
DEEPEN both ways when the fork changes payload or risk profile rather than role.

Spec nodes are composed from the same small primitives used everywhere else: stats, statuses,
triggers, rules, and optional signature overrides. Node text must explicitly identify its
target, trigger, effect, and whether a state is added, transferred, consumed, or replaced.

Rank and path choices are sticky by default. If respec exists in the shipped run, it is an
explicit preparation service with a cost; it is never a free action inside deployment.
The first playable may expose free respec as clearly labeled testing scaffolding.

### 4. Items — the churn axis

Every hero has exactly two equipment slots:

1. **Weapon** — the complete automatic attack profile.
2. **Trinket** — defense, utility, Mana, statuses, or trigger bundles built from existing
   primitives.

Weapon access is universal: any hero may equip any category. A hero's specializations are
bonuses, not locks. Each category carries one latent mastery rider that is active for its
specialists; Relic-tier weapons unlock the rider for any wielder and double it for a
specialist.

Weapon rarity is temper on the same item—**Worn → Honed → Relic**—rather than a larger item
catalog. The Tower forge upgrades an owned weapon in place during preparation, subject to
the current run's pacing ceiling.

Owned weapons and trinkets may be re-equipped freely during Prepare. Entering deployment
locks the loadout; that screen is for positioning, not inventory management.

Heroes are intentionally stickier than their equipment. The player should be able to keep a
beloved hero and re-tool how it delivers its engine as the run's offers and encounters
change.

### 5. Party layer — emergent engines, not trait counting

There are no composition thresholds such as “three Knights grants Armor.” Team identity
emerges from:

- hero triggers and statuses interacting;
- weapon physics and mastery;
- muster placement and live combat geometry;
- Fields and the Clock;
- persistent Hourstone Inscriptions; and
- the encounter problem the player is preparing to solve.

Every acquired Inscription remains active for the run. They are the broadest cross-hero
engine layer; their ownership, authoring, cascade, content, and presentation laws live in
[inscriptions.md](inscriptions.md). The current `BannerDef` catalog is transitional legacy
naming.

The fielded warband grows from a small starting lineup toward a cap, with a small bench
preserving roster decisions between fights. ADR 0006's current structure is three starting
field slots, a six-unit cap, and a two-unit bench. ADR 0016 reopened **when** additional
field slots are offered, so the PvE vertical-slice design owns that schedule.

The same run currency competes across width, duplicate-driven depth, equipment,
Inscriptions, rerolls, forging, and any respec service. These choices should remain
tradeoffs rather than a checklist of automatic upgrades.

### 6. Between-fight commitment

Every PvE fight follows:

1. **Preview:** inspect the encounter, enemies, mechanics, and formation.
2. **Prepare:** choose field/bench lineup, re-equip owned items, and use available economic
   services.
3. **Deploy:** loadout locks; arrange the chosen units.
4. **Play:** positions lock; deterministic combat resolves.

The preview remains accessible during preparation. The player knows the rules but not the
winner.

### 7. Movement and behavior vocabulary

The current player roster uses:

- **Walk:** deterministic pathing toward the current target, at a **per-chassis speed** (ADR 0022;
  Shade 3 ticks/hex → Bulwark 7).
- **Leap:** reposition adjacent to a selected target and reacquire from there.

Push, Pull, collisions, and other displacement remain deferred. Movement depth should come
from readable kit rules and placement, not opaque pathfinding or mid-combat commands.

**The behavior layer (ADR 0022).** A chassis — or a spec node — declares two rules beyond its stats:

- **Target preference:** `Nearest` (default) · `Farthest` · `LowestHp` · `HighestHp`. This decides
  **acquisition only**; ADR 0013's stickiness, Phase and Taunt still own re-acquisition. Shade is
  the roster's one non-default today (`LowestHp` — the knife picks its moment).
- **Standoff:** a preferred fighting distance. The unit gives ground when its target closes inside
  it, never retreats out of its own weapon range, and keeps attacking while it withdraws. Sharpshot
  (4) and Pyromancer (3) hold distance; Lifebinder gains it from her fork.

Because nodes may set both, **a fork can change what a hero DOES, not only what its signature
does** — which is what makes "the fork changes the hat" true in the sim rather than only on the
page. Before this, both Cleric forks walked at the nearest enemy at identical speed, and "Lifebinder
retreats from the scrum" was advice the unit itself ignored.

## Composition law

Before battle, the loadout composer deterministically resolves chassis, rank growth, weapon,
trinket, spec nodes, and run bonuses into one battle-ready unit. The combat simulator does
not know about shops, item inventories, ranks, or trees.

This separation is deliberate: hero content can change without weakening deterministic
combat or replay. When several nodes override the signature, the current composer uses the
last applicable override; combinations that need additive signature modification must earn
new machinery rather than relying on ambiguous merge behavior.

## Intentionally open

- Timing and price of field-slot growth in the PvE run.
- Whether shipped respec exists, and its service cost.
- Post-S hero decisions needed to keep endless preparation meaningful.
- Trinket depth beyond the first-playable placeholder.
- All numerical tuning until interactive playtest evidence exists.
