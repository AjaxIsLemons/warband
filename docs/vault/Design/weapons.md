# Weapons — attack physics, mastery, temper, and the forge

**Updated:** 2026-07-24

**Status:** current first-playable design reference. The structure is accepted in
[ADR 0015](../Decisions/0015-weapon-system.md); names and numbers remain placeholder-grade
until playtest.

Weapons are the player's cleanest way to change what a hero does without changing who that
hero is. A weapon supplies the hero's basic attack physics. Its mastery rider rewards a
natural pairing without preventing unnatural ones. Temper lets a good find remain relevant,
and Relic temper can turn an off-label experiment into a real build.

## Source boundaries

- This page owns the player-facing weapon catalog, mastery law, temper model, and forge
  contract.
- [ADR 0005](../Decisions/0005-loadout-composition.md) owns loadout composition order:
  chassis → weapon → trinket → spec nodes.
- [ADR 0012](../Decisions/0012-weapon-access-model.md) owns universal equip and
  specialization rather than weapon locks.
- [ADR 0015](../Decisions/0015-weapon-system.md) records the accepted catalog, temper, and
  forge decisions.
- `sim/Warband.Content/Weapons.cs` owns the currently runnable profiles and placeholder
  magnitudes.
- Hero specializations and intended wardrobes live in [roster.md](roster.md) and the
  individual hero dives.
- Trinkets are a separate item family. They do not need to obey the mastery-rider law.

## What a weapon owns

The weapon is the hero's basic attack profile:

- damage or healing;
- attack interval;
- **mana per swing — the cast cadence** (ADR 0022);
- range;
- crit profile;
- attack shape; and
- one latent mastery rider.

### Cadence is the second axis (ADR 0022, 2026-07-25)

Mana per swing used to be a global flat rate, which made cast cadence exactly `1/Interval` — so a
fast weapon won swing events, casts, and (since damage-per-tick sits in a 0.80–1.00 band for nine of
eleven weapons) damage as well. There was only one axis, and it pointed one way.

Authored per weapon, mana-per-tick now runs **0.83 (daggers) → 1.40 (mace; 2.80 mastered)**. Read a
weapon as a pair: how often it swings, and how much of a cast each swing buys.

- **Twin Daggers** — swing-spam: many on-hit events, and a signature that rarely fires.
- **Matchlock Musket** — artillery: rare, enormous shots, each most of a cast.
- **Temple Mace** — the cast engine; its mastery doubles the same number it earns.
- **Longbow** — steady fire, cast-light.

This is what makes an off-label weapon a real decision rather than a range decision: putting a
musket on a cast-driven hero is now a coherent build, not a downgrade.

The chassis still owns HP, movement, Mana capacity, innate passives, and the signature
ability. Spec nodes still own class verbs. A weapon can make the same hero faster, slower,
safer, longer-ranged, or more cast-hungry, but it should not contain a replacement class
kit.

The censer is the deliberate exception to damage autos: it targets the lowest-HP ally and
heals. Heal swings still build Mana and may crit. This makes support a wardrobe choice
rather than a permanently locked role.

## Universal equip and mastery

Any hero may equip any weapon. The shop therefore never offers an item that is unusable
because the player drafted the wrong classes.

Each chassis specializes in one or two categories. A specialist activates that weapon's
mastery rider at every temper. A non-specialist receives the base attack profile without
the rider, unless the weapon reaches Relic.

Mastery riders amplify an engine—crit, Mana, tempo, defense, reach, or formation payoff.
They do not add a second signature ability or import another hero's defining verb. Reach
weapons may modify attack physics because distance is their engine.

The specialization is an incentive, not the answer. Natural pairings should be dependable;
off-label weapons should create understandable tradeoffs and occasional broken-build
discoveries.

## First-playable catalog

The categories are the item list: one weapon in each of eleven categories.

| Weapon | Base attack identity | Designed specialists | Mastery rider |
|---|---|---|---|
| **Twin Daggers** | range 1; fastest, lightest swings | Shade, Berserker | bonus crit chance |
| **Officer's Sabre** | range 1; fast, light swings | Banneret | first swing after each cast is a guaranteed crit |
| **Temple Mace** | range 1; medium cadence | Bulwark; War-Priest is intended to add it | attack-fed Mana is doubled |
| **Greataxe** | range 1; slow, heavy swings with cleave | Berserker | overkill carries to the enemy nearest the corpse |
| **Tower Shield** | range 1; slow, low-damage swings | Bulwark, Phalanx | each auto hit grants the wielder Shield |
| **Pike** | range 2; second-rank reach | Phalanx | bonus damage against an enemy engaged with an ally |
| **Censer** | range 3; heals the lowest-HP ally | Cleric, Pyromancer | overheal becomes Shield |
| **Ashwood Staff** | range 3; steady caster cadence | Cleric, Pyromancer | casting grants brief Haste |
| **Longbow** | range 4; steady long-range attacks | Sharpshot, Shade | additional attack range |
| **Matchlock Musket** | range 4; slowest, heaviest shots | Sharpshot | the first shot of the fight deals double damage |
| **Company Standard** | range 1; light polearm swings | Banneret | an additional opening Haste muster reaches adjacent allies |

Every category has at least one natural user. Shared categories create draft overlap;
single-specialist categories give Relic unlocks more room to produce unusual builds.

### Attack-shape law

Greataxe cleave is the only weapon-level area shape in the first playable. Every other
damage weapon is single-target. Pierce lines, fans, splashes, and detonations remain
ability-side verbs, where their ownership and preview are clearer.

The censer changes target faction and converts the auto into healing, but it remains a
single-target attack cycle.

## Temper: Worn → Honed → Relic

Temper is a tier on the same weapon, not another catalog item.

- **Worn:** base weapon profile. Starter weapons begin here.
- **Honed:** improves the weapon's stats.
- **Relic:** improves the stats again and activates the mastery rider for any wielder.
  A specialist wielding a Relic receives two copies of the rider.

That final rule creates a wardrobe arc. An off-label weapon can be useful early for its
physics, then become build-defining if the player finds or forges the Relic version. A
natural specialist gets the reliable floor and the strongest Relic ceiling.

Temper availability follows authored run progression, never the player's record or chosen
risk tier. The exact PvE gates are not settled while the standard run length remains open.
Wager-linked rarity stays rejected: risk may affect rewards, but it should not compound a
winning board's gear quality into a second snowball axis.

## The Tower forge

During **Prepare**, while economic actions are available, the player may pay gold to raise
a held weapon by one temper tier. The forge is capped by the run's current progression
ceiling: a favorite starter may remain current, but gold cannot skip the intended gear
curve.

The forge creates a third use for gold:

- **widen** the warband with heroes and field slots;
- **deepen** it through duplicates and build pieces; or
- **sharpen** an existing weapon.

Reconfiguration and forging finish before **Deploy**. Once deployment begins, loadouts are
locked and the player only arranges the chosen lineup.

Weapon duplicates are not an upgrade currency, and weapons do not gain kill-fed XP. Those
models would encourage inventory hoarding or reward the board that is already winning.

## Where the build fun comes from

Weapon decisions operate through three different levers:

1. **Physics × hero engine.** Fast daggers produce many on-swing events; a musket creates
   fewer, larger events; a bow changes which formation slots can contribute.
2. **Mastery × compounding engine.** Mace Mana, staff cast-Haste, censer shielding, and
   standard muster all feed loops that another hero, Inscription, or spec node can amplify.
3. **Relic × off-label pivot.** A non-specialist gains the rider late enough to transform an
   experiment without erasing the specialist's advantage.

The target feeling is not “this is the weapon assigned to this class.” It is “I understand
why the natural pairing works, and I can see the machine I might build by breaking it.”

## First-playable scope

The item budget is eleven weapons plus one trinket. Do not add more weapon categories before
the first PvE playtest. Depth should come from hero interactions, temper, formation, and
encounter pressure—not from a larger pile of near-duplicate loot.

## Implementation fidelity follow-ups

The runnable content covers all eleven profiles, riders, temper scaling, Relic access,
double riders for Relic specialists, and the held-weapon forge. The following seams should
stay visible rather than being mistaken for settled design:

- **War-Priest mace specialization:** ADR 0012 and the Cleric dive say the fork may add
  mace mastery. The current composer only reads chassis specializations, so War-Priest does
  not yet gain it.
- **Tower Shield bulk:** ADR 0015 described a defensive base profile. The current weapon
  schema changes only attack stats, so its runnable defense comes entirely from the mastery
  Shield rider.
- **Company Standard potency:** the accepted shorthand was “Company potency.” The current
  concrete expression is an extra opening Haste muster to adjacent allies. Playtest this
  expression before inventing a generic potency multiplier.
- **Forge resale:** ADR 0015 promises a 50% refund of total gold sunk into a reforged
  weapon. Current item state does not track forge investment, so resale only knows the base
  item price.
- **Starter temper persistence:** a held starter can be forged, but switching away and
  later returning to the implicit starter currently restores it as Worn.
- **Progression schedule:** the old five-act scaffold exposes all categories and uses a
  placeholder act-based temper ceiling. The authored PvE run must define its real stock and
  forge gates.

These are implementation or playtest follow-ups, not reasons to reopen universal equip,
the eleven-category cap, the mastery/Relic law, or forge-in-place.

## Intentionally open

- final item names and all numerical tuning;
- the real PvE temper availability curve;
- whether Tower Shield needs base defensive stats once the item schema supports or rejects
  that concept;
- whether the current Standard rider delivers the desired Company fantasy; and
- trinket depth after the first playtest.
