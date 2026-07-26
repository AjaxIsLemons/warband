# ADR 0022 — The unit behavior layer, weapon cadence, and signature patching

**Date:** 2026-07-25 · **Status:** accepted (Jake: "lets build it") · **Participants:** Jake + Claude

## Context

A systems review of the class/weapon/tree layer read the vault against the runnable content and
found the grammar much wider than the content authored on top of it. Four findings had a common
shape — a lever the design already assumed, which the sim had never actually grown:

1. **Every unit shared one brain.** `AcquireTargets` was nearest-only with no per-unit hook, even
   though `combat-grammar.md` promised "nearest default; **kits override**" from round 6. Movement
   was "close to range, then stop", and **no chassis set `MoveInterval`** — all eight moved at the
   default 5. Range and heal-autos were the only things that made two units behave differently.
   Concrete casualty: Sharpshot's Full Draw pays per hex of distance, but she advanced to exactly
   max range and never gave ground, so her signature stat could only decay from first contact.
2. **Weapons traded on one axis.** Damage-per-tick sat in a 0.80–1.00 band for nine of eleven
   weapons, and mana was a flat 10 per swing against a 1/second trickle — so cast cadence was
   simply `1/Interval`. Fast weapons won swing events, casts, and damage equally.
3. **`SignatureOverride` was last-wins**, so an S crown silently erased an A amplifier's texture
   (Sarissa kept Deep Thrust's length and dropped its escalation), and "the same thing, bigger"
   could only be authored by copy-pasting an effect list with one number changed. Four nodes were
   literally that.
4. **`ManaMax` — the pulse of the whole combat model — was untouchable.** No spec node and no
   weapon modified it. `TrinketDef.ManaMaxDelta` existed, wired, unused: the only trinket was
   `+20 HP`, so half the documented "Weapon + Trinket" anatomy did nothing.

A fifth finding was a plain bug: **Frenzy bypassed `AttackInterval` outright**, so a window was
worth `4 × weapon Damage` at no cost in ticks. The heaviest weapon was always the correct Frenzy
weapon (musket 64 vs the Berserker's own specialized daggers 24) and his dagger specialization was
a trap.

## Decisions

### 1. Units get a behavior layer (chassis-authored, node-overridable)

Three fields on `ChassisDef`, each also settable by a `SpecNode`:

- **`TargetPref`** — `Nearest` (the ADR 0013 default) · `Farthest` · `LowestHp` · `HighestHp`.
  Preference decides **acquisition only**: stickiness, Phase and Taunt still own re-acquisition, and
  Taunt still overrides everything. All comparisons are strict with ascending-id iteration, so ties
  fall to the lowest id — no rng, no floats, order-independence intact.
- **`Standoff`** — a preferred fighting distance. While the target is closer than `Standoff` **and**
  still inside weapon range, the unit gives ground one hex at a time, and **keeps attacking while it
  does** (ADR 0018 clause 6: nothing is gated on walking). Every retreat hex must leave the target in
  range, so a unit can never kite itself out of its own fight. It terminates at `Standoff`, at the
  board edge, or when bodies leave nowhere farther to stand.
- **`MoveInterval`** — now actually authored per chassis (Shade 3 · Berserker 4 · Cleric/Sharpshot/
  Banneret 5 · Pyromancer/Phalanx 6 · Bulwark 7) instead of every hero sharing the default.

Because nodes may set these, **a fork can finally change the hat at the behavior layer, not just the
payload layer** — Lifebinder's "SWAP to backline" now moves her (`Standoff = 3`) instead of being
advice to the player that the unit itself ignored.

### 2. Weapons own their cast cadence

`WeaponDef.ManaPerSwing` (default `Battle.ManaPerAttack`) replaces the global flat rate. Authored
per weapon, mana-per-tick now spans 0.83 (daggers) → 1.40 (mace, 2.80 mastered), where it used to be
purely `10/Interval`. The wardrobe becomes a two-axis choice: light blades spam swing events and
starve their own signature; heavy weapons swing rarely and bank most of a cast each time.

### 3. Signature **patches** for degree, overrides for verb

`SpecNode.SignaturePatch` modifies the signature a node inherits: `RadiusDelta`, `LineRange`,
`AmountPct`, `Escalate`, `FieldRadius`, `FieldTicks`, `Repeat`, `Add`. Patches apply in node order on
top of whatever override is current, so **B-fork → A-amplifier → S-crown composes instead of
colliding**.

**The law:** a node that changes the VERB keeps `SignatureOverride`; a node that changes the DEGREE
declares a patch. Radius and line length never share a knob — both live on `Selector.Range` but a
board-length line (`Range 0`) bumped by a radius delta would silently collapse to one hex.

Twelve nodes converted. `AbilityIdentity` now treats a patch as ability-defining for the same reason
it treats an override that way — Everburn's permanent fire and Sarissa's board-length lunge are
different casts, and a tell keyed on the chassis would say otherwise.

### 4. Trinkets own chassis stat-shape

The three item layers take disjoint jobs, which is what stops them blurring:

| Layer | Owns |
|---|---|
| **Weapon** | the attack profile — damage, interval, range, shape, cast cadence |
| **Trinket** | the chassis stat-shape — mana capacity, durability, reach |
| **Inscription** | team rules that cross heroes (ADR 0017) |

Four trinkets join the stub: `Quickened Stone` (−12 mana cap, −2 attack), `Deepwell Reliquary`
(+15 cap, +3 attack), `Gravemark Charm` (mana on kill), `Martyr's Knot` (+30 HP, mana on damage
taken). Trinkets are the **repair-my-hero's-weakness** axis — what a game with deliberately sticky
heroes needs: keep the champion, change how it fails.

### 5. Frenzy is a speed multiplier, not an interval bypass

`Battle.FrenzySpeedFp = +300%` (4× swing rate) for the window's swings. The burst still scales with
weapon weight, but light weapons now win the window on damage per tick instead of losing it.

## Consequences

- **The 2026-07-23 sweep baseline is retired for the build matrix.** Re-run recorded in
  `Projects/sweep-2026-07-25.md`. Headlines: Sharpshot 46 → 62 and Pyromancer 32 → 46 (the two
  classes the review named as fighting their own pathfinder), Shade 60 → 45, Bulwark 65 → 53.
  **Banneret is unchanged at 12** — exactly as predicted: its floor is structural, not numeric, and
  no amount of tuning reaches it. That stays open (review §7).
- **New outlier, named not tuned:** `shade.reaper+widowmaker` is now DEAD at 8–9%. Most likely cause
  is the daggers cadence cut (10 → 5 mana/swing halves Backstab's rate on the one chassis whose
  signature carries its damage). Placeholder magnitude, deliberately untouched.
- The Last Oath still poses its decision — Enrage fires in 97% of fights, and placement chooses the
  survivor in 4/4 lineups (spread Δ96).
- Content doctrine holds: this is missing *machinery*, not a balance pass. Magnitudes stay
  placeholder until playtest.
- Open and now cheap: **Wide Banner** can express its documented "reach AND Rally radius grow" as a
  one-line `Patch(radius: 1)` — still waiting on Jake's nod (roadmap open question).
- Deliberately not built: preference-driven *re-acquisition* (ADR 0013 stickiness is unchanged) and
  movement-haste.

## What this unlocks

The behavior layer is the piece the authored enemy grammar needs: a Diver is `TargetPref.Farthest`
with a fast `MoveInterval`, an Artillery piece is `Farthest` + `Standoff`, a Finisher is `LowestHp`.
Those roles were unbuildable before this ADR without bespoke sim code.
