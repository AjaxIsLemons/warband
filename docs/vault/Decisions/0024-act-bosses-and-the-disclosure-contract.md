# ADR 0024 — Per-act bosses, and the brief IS the fight

**Date:** 2026-07-26 · **Status:** accepted-pending-Jake (built autonomously overnight on Jake's
instruction: *"do some gap analysis… get things into the roadmap — THEN implement something for me
end to end while I sleep"*) · **Participants:** Claude, for Jake's review

## Context

Two items were left open by ADR 0023, and they turned out to be one item.

**① Every act closed on the same boss.** `Catalog.Boss(act, rng)` returned the Last Oath's bonded
pair with a `100 + 25×(act−1)` stat multiplier, for all three acts. A three-act run ended three
times on the same two bodies with bigger numbers, which is precisely the "difficulty means larger
stats" failure ADR 0016 legislates against. ADR 0023's role grammar had already made the fix cheap.

**② The disclosure law was not being kept, and worse than the board recorded.** The board said "no
shell screen renders `PreviewBrief`". The truth, read out of the code:

- `RunShell.BuildFightBeat` — the **live** planning beat — hardcoded `p.Heading = "THE LAST OATH"`
  for every boss and set `p.Rule` to a generic stakes line. The four authored node encounters
  disclosed **nothing**: no name, no pressure, no rule.
- The legacy `MapView`/`DeployView` path did render an `EncounterRule`, but only on boss nodes and
  hardcoded to `Encounters.BondedPair()` — correct only by accident, because every act's boss *was*
  the bonded pair. Fixing ① without fixing ② would have turned an accident into a lie.
- Enemy cards were built by `UnitCardFromDef`, which titles a card from
  `ContentLexicon.Chassis(def.ChassisId)` and fills its ability/passive copy from that hero's
  presentation entry. But ADR 0023 established `ChassisId` on an enemy as a **render key**. So the
  preview showed an **Hourling as "Shade"** with the Shade's signature text, an **Ashen Colossus as
  "Bulwark"**, an **Hour-Scribe as "Pyromancer"** reading out Inferno. This is worse than no
  disclosure: the player was told the wrong rules, confidently.

## Decisions

### 1. Each act closes on its own strength exam

| act | boss | one-sentence pressure | answers it admits |
|---|---|---|---|
| 1 | **The Last Oath** (`BOND`) | which threat you leave enraged | focus order · burst · control · placement |
| 2 | **The Ashfall Battery** (`BATTERY`) | reach the gun behind the wall | reach · dive · spread · sustain · Silence/Stun |
| 3 | **The Waning Crown** (`WANING`) | your own kills ring the bell | burst · reach · Silence/Stun · sustain |

**Act 1 deliberately keeps the Last Oath.** It is the only boss whose decision has been *measured*
(`oath-probe-2026-07-25`: placement chooses the survivor in 4/4 lineups, Δ84 win%), and
`pve-encounters.md` says to earn further bonded scope by playing the pair first. Re-authoring it
would have thrown that evidence away for no gain.

**Two new authored boss bodies**, both Rooted, both with `Attack = 0`, both `ManaPerHitTaken = 0`:

- **Ashfall Bombard** (300 HP, bell 9 s) — shells the FARTHEST unit for 58 and leaves a radius-1
  burning crater. The reflex answer to artillery, *bunch up behind the tank*, is the losing one.
- **The Waning Crown** (460 HP, bell 22 s) — at full mana it damages and Slows the entire warband,
  and **every death in its court advances the bell by 4**. Clearing the escorts — the habit three
  acts of node fights train — is what rings it.

Both reuse the Hour-Scribe's grammar on purpose: acts teach a verb, bosses examine it. Both keep the
disclosed four answers, and `Silence` genuinely stops the clock because `GainMana` is gated on it —
that is now a test with its own negative control, not a claim.

### 2. Bosses are authored FOR their act and take no act curve

`Encounters.BossScalePct(act)` is 100 for acts 1-3. The multiplier survives only for acts **beyond**
the authored three — the endless horizon (ADR 0016), where the act-3 boss is all there is to
escalate. Consequence worth stating: act 1's boss is numerically identical to what shipped before,
so the oath probe's measurements remain valid.

### 3. The brief and the spawn are built by the same method

`Catalog` now derives both from one private `NodeComp` / `BossComp`. Previously they were two code
paths agreeing by convention, and the boss path had already drifted. This is the structural fix:
divergence is no longer a bug you can write, it is a compile-time impossibility.

### 4. `EncounterBrief` carries every body it will field

New `EncounterUnitBrief`: name, role + role id + accent, chassis id (**render key, explicitly
documented as not an identity claim**), weapon, post-scaling HP / attack / cadence / reach, row, and
a one-line **behavior sentence**.

The behavior sentence is the part `pve-encounters.md` demanded and nothing delivered: "attacks,
signatures, passives, triggers, **and targeting rules**" inspectable before deployment. A Sanddrift
Gunner's entire design is *acquires FARTHEST, holds standoff 5*, and the player was never told.

### 5. Enemy cards are built from the brief, never from a UnitDef

`RunShell.EnemyCard` gives a monster its authored name, its encounter role, its real numbers and its
behavior line. **No portrait** — a chassis portrait is a named champion's face, and a hero's face on
a monster is the same lie in a different channel. Initials until bespoke enemy art exists (roadmap
item 2③). Role → accent reuses the eight authored accents rather than inventing an enemy palette,
so no stylesheet had to learn a new word.

## Measurement — `--boss`, the new authoring instrument

`dotnet run --project sim/Warband.Sweep -- --boss`. A boss is held to a **harder** bar than a node
encounter, because it is a strength exam: on top of `--enc`'s win% and placement spread, it re-runs
each boss against **four deliberately different parties** (balanced / reach / control / damage) and
reports how many can pass it. A boss only one axis clears is prescribing a build, which the
encounter law forbids.

Report: `Projects/boss-probe-2026-07-26.md`. Results at ship:

| act | boss | axes that clear it | placement spread | rule fired |
|---|---|---|---|---|
| 1 | The Last Oath | 3 of 4 (control cannot) | 100 | 67-100% |
| 2 | The Ashfall Battery | 4 of 4 | 100 | 50-100% |
| 3 | The Waning Crown | 4 of 4 | 100 | 100% |

Two findings the probe produced rather than confirmed:

- **The act-2 boss originally posed nothing.** At a 14 s bell the gun fired roughly once before a
  rank-B party finished the wall, and three of four axes cleared it 100% **from every formation**
  (spread 0). The bell went to 9 s and the shell to 58 with a longer crater. That number is
  measured, not designed.
- **Act 2's `control` axis shows 50% rule-fired — and that is the design working.** The control
  party runs Bulwark Warden, whose Taunt Silences. A Silenced Bombard never gains mana and never
  fires. The disclosed answer is observably the answer.

## Consequences

- Three acts now end on three different problems. Act identity remains thin everywhere else
  (roadmap item 14): `Encounters.PoolFor` still gives acts 2 and 3 an identical node pool.
- The boss reward/`BossBrief` path is act-keyed but still takes an `Rng` it does not use. Left as
  is: the interface is shared with node encounters, which do need it.
- **Not eyes-verified.** Nothing was watched in Unity — the editor is a shared singleton and this
  ran overnight. Two render fixtures (`boss-ashfall-battery`, `boss-waning-crown`) were added to
  `scenarios.json` via a new `encounter` seam so the next session can watch either boss immediately;
  both round-trip and both were confirmed to fire their mechanic by event coverage (the shell's
  58-damage ability and 9-damage crater field; the Crown's 44-damage bell). The contact sheet is now
  **10 fixtures**, not 8 — the determinism gate's expected PNG count changed.

## What this does NOT decide

Risk-tier mutation of authored encounters · bespoke enemy art and per-role tells · the endless
seam · act-scoped node pools · whether the Ashfall crater should be dodgeable rather than a tax.
