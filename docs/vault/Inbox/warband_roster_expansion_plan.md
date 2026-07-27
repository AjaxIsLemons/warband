# Warband: Roster and Evolution Expansion Plan

**Status:** design proposal for staged implementation  
**Repository basis:** `AjaxIsLemons/warband`, inspected 2026-07-26  
**Primary goal:** make rank-up decisions broad enough to create surprising runs while preserving readable automatic combat, explicit tuning controls, and implementation discipline.

---

# 1. System decision: expand the existing roster before depending on new heroes

## Executive recommendation

Warband should move from **two visible choices per rank** to **three visible choices per rank**.

For a standard hero:

- **C — Recruit:** fixed chassis, innate, signature, and starter weapon.
- **B — Discipline:** choose one of **three paths** that changes the hero's job or operating model.
- **A — Technique:** choose one of **three path-specific amplifiers**.
- **S — Crown:** choose one of **three path-specific capstones**.

This changes the authored outcome count from:

- current: `2 B paths × 2 A choices × 2 S choices = 8` final builds per standard hero;
- proposed: `3 B paths × 3 A choices × 3 S choices = 27` final builds per standard hero;
- eight current heroes: **216 authored hero builds before weapons, trinkets, Inscriptions, formation, or party composition.**

Shade may keep its late-bloom structure, but should still expose three choices at each actual choice point.

This is intentionally more content than the first-playable needs. Implementation should be staged, but the data model and design documents should target the full structure now.

## Why three is the correct choice width

Guildrun's useful lesson is not its raw content count. Its rank decisions can change a hero's class, role, and item needs, and shared class effects can redirect an entire composition. Players are repeatedly asked to recognize a new engine or pivot rather than merely take the less-bad of two numerical bonuses. See the [official Guildrun description](https://store.steampowered.com/app/3669200/Guildrun/) and [Mobalytics' mechanical overview](https://mobalytics.gg/news/guides/guildrun-demo-showcase).

Three choices create a useful design triangle:

1. **Deepen:** make the selected engine substantially better.
2. **Bridge:** connect it to another Warband system such as Burn, Shield, Mana, movement, Fields, or Inscriptions.
3. **Answer:** cover a weakness or answer a particular PvE pressure at an opportunity cost.

Two choices frequently collapse into “damage or defense,” or one broadly useful option versus one narrow option. Four or more choices increase comparison load and card density. Three is large enough to produce direction and small enough to scan before every rank-up.

## Content law for every three-choice set

Every set of three must pass all of these tests:

- No two choices are primarily the same effect with different numbers.
- One option is strongest when the existing engine is already working.
- One option creates a connection to a different hero, item, Field, status, or Inscription engine.
- One option solves a recognizable encounter problem or failure mode.
- The best option can change based on the player's weapon, party, owned Inscriptions, and next disclosed encounter.
- Every option has at least two independent balance levers.
- Every option can be explained in one rules sentence plus one short “builds toward” line.

## Rank power budgets

These are starting budgets for design and automated sweeps, not final balance promises.

| Rank | Expected role | Conditional power budget | Required tradeoff |
|---|---|---:|---|
| B | changes operation or job | roughly power-neutral to +15% median output | changing role must lose or weaken something from the old operation |
| A | creates a build engine | +15–30% while its condition is active | condition, cadence, geometry, target, or cap |
| S | creates the payoff moment | +30–60% while fully enabled | bounded chain, setup requirement, once-per-fight rule, positional exposure, or meaningful drawback |

The budget is measured across complete fights, not tooltip arithmetic. A spectacular five-second S-rank window may be much stronger than +60% during that window while remaining inside the fight-level budget.

## Universal balance levers

Every node should expose its relevant levers in data rather than embedding unexplained constants in trigger graphs.

| Lever family | Typical fields | Primary failure it controls |
|---|---|---|
| Magnitude | damage %, heal, Shield, Burn stacks, Mana | raw output |
| Cadence | every Nth swing, internal cooldown, once per cast | fast-weapon and Haste multiplication |
| Duration | status seconds, Field lifetime, charge window | uptime and control locks |
| Geometry | radius, line length, max range, target count | AoE multiplication |
| Threshold | target HP %, self HP %, Mana threshold | reliability |
| Capacity | stack cap, activation cap, stored charges | runaway scaling |
| Conversion | source-to-output ratio | cross-engine loops |
| Cost | self-damage, lost Shield, reduced base damage, longer Mana clock | role-changing power |
| Target filter | nearest, farthest, lowest HP, highest Mana, Burning, Taunted | encounter dependence |
| Root guard | once per root event, cannot trigger itself | cascade safety |

### Initial safety bands

- Ordinary single-target control: target **15–25% maximum practical uptime** before Haste or external support.
- Ordinary multi-target control: target **8–15% practical uptime**.
- Free recasts: default maximum **one child recast per original cast**.
- Mana refunds: default maximum **50% of ManaMax per triggering cast**.
- Every-N attack riders: test against the fastest legal weapon and maximum reachable Haste.
- Kill chains: include a per-root or per-cast cap, even when the board has only a few enemies today.
- Permanent in-fight scaling: cap the number of stacks or use diminishing additions.
- Teamwide output should normally be worth **40–60% of an equivalent personal effect per recipient**, because it multiplies equipment and hero engines.

## Player communication contract

Every rank card contains:

1. **Name**
2. **Verb tag:** `SIGNATURE`, `AUTO`, `PASSIVE`, `FIELD`, `COUNTER`, `MUSTER`, or `MOVEMENT`
3. **One rules sentence:** “When X, target Y gains/loses Z for N.”
4. **Builds toward:** one short engine phrase.
5. **Tradeoff:** shown only when the choice removes or weakens something.
6. **Before/after panel:** for signature swaps, behavior changes, target changes, and range changes.

Example:

> **BREACHER — SIGNATURE**  
> Shield Slam Leaps to the highest-Mana enemy before striking, but Brakka begins with 50% less Bastion Shield.  
> **Builds toward:** backline disruption and anti-ritual.  
> **Changes:** Targeting · Movement · Signature.  
> **Trades away:** opening durability.

Rules text should use player verbs, not implementation terms. Do not expose “proc,” “root event,” “selector,” or “effect graph.” Advanced inspection may show exact internal cooldowns, caps, and calculation order.

## Implementation impact

The repository currently assumes pairs in several places:

- `Kits.Offers` stores `(string A, string B)`.
- `Catalog.SpecOptions` returns `(string A, string B)`.
- rank-up UI and tests are likely pair-shaped.

Replace the pair with an ordered offer object:

```csharp
public sealed class SpecOffer
{
    public string ChassisId = "";
    public Rank Rank;
    public string? PathId;
    public List<string> NodeIds = new();
}
```

Required laws:

- offer order is deterministic and authored;
- every playable offer contains exactly three distinct, resolvable node ids;
- save data continues to store chosen node ids, not option indices;
- old saves remain valid because existing node ids are unchanged;
- the composer never depends on presentation order;
- signature overrides remain mutually exclusive within one path;
- A and S nodes prefer patches, riders, statuses, and rules over additional signature overrides because composition currently uses last-applicable override semantics.

## No ninth hero as a near-term dependency

The current roster document correctly says that the eight heroes already cover the first-playable PvE answer space. The near-term content plan therefore deepens them first. New heroes are included later in this document as scoped future packages, not as blockers for the 3-choice conversion.

---

# 2. Target content catalog

## How to read the hero plans

For every existing hero, this document proposes:

- a **third B-rank path**;
- a **third A choice and third S choice** for each existing path;
- three A choices and three S choices for the new path;
- initial balance levers;
- implementation notes where a rule needs new simulation vocabulary.

Existing nodes remain unless explicitly called out. Proposed values are starting points for sweeps.

Legend:

- **E:** expressible with current or already-established combat vocabulary.
- **M:** needs a small reusable primitive or selector.
- **N:** needs a meaningful new simulation feature; implement only after an ADR and focused test.

## Cleric — Sister Maren of the Waning Bell

### Stable contract

Maren converts danger into healing and damage. War-Priest owns frontline Burn; Lifebinder owns remote sustain. The missing third operation is **enemy Clock control through judgment**, creating a controller without replacing Bulwark's Taunt or Banneret's broad tempo role.

### New B path: Exorcist

**Player copy**

> **EXORCIST — SIGNATURE**  
> Sanctified Pyre no longer heals. It damages enemies around Maren, removes 25% of their Mana, and briefly Silences the enemy with the most Mana.  
> **Builds toward:** anti-ritual control and cast denial.  
> **Trades away:** signature healing.

**Starting mechanics**

- Keep self-centered radius 1.
- Damage: 80% of base Pyre damage.
- Mana burn: 25% of each affected enemy's ManaMax or a flat authored amount.
- Silence: highest-Mana affected enemy for 1.5 seconds.

**Levers:** radius, damage scalar, Mana burn, Silence duration, single-target Silence filter.  
**Feasibility:** E/M; highest-current-Mana selector may be M.

### War-Priest: add one A and one S

| Rank | Node | Player rule | Builds toward | Main levers |
|---|---|---|---|---|
| A | **Ashen Sacrament** | When a Burning enemy dies within 2 hexes, Maren gains 20% Mana. Once per death. | Burn deaths → more Pyres | radius, Mana %, root guard |
| S | **Martyr's Pyre** | Below 40% HP, Sanctified Pyre is 35% larger in magnitude and its healing affects Maren twice. | risk-reward frontline carry | HP threshold, magnitude, self-heal scalar |

Ashen Sacrament is the bridge option beside Scorched Mercy and Contagion. Martyr's Pyre is the exposure payoff beside Conflagration and Undying Zeal.

### Lifebinder: add one A and one S

| Rank | Node | Player rule | Builds toward | Main levers |
|---|---|---|---|---|
| A | **Triage Bell** | The remote pulse prefers an ally below 50% HP; if none exists, it targets the lowest-HP ally normally. Its heal is 25% stronger below the threshold. | emergency stabilization | threshold, heal %, targeting rule |
| S | **Procession of Grace** | After the pulse, affected allies carry a healing aura for 4 seconds that heals adjacent allies for a small amount each second. Auras do not heal their bearer. | mobile distributed sustain | duration, radius, pulse cadence, no-self rule |

Triage Bell improves reliability without creating a new effect. Procession turns one remote point into a moving formation engine but prevents self-looping.

### Exorcist A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Penance** | Exorcist Pyre deals +50% damage to Silenced enemies. | deepen anti-caster burst | damage %, condition uptime | E |
| **Absolution** | When Exorcist Pyre removes Mana, the lowest-HP ally gains Shield equal to 40% of Mana removed, up to a per-cast cap. | bridge enemy Clock → defense | conversion %, Shield cap | M |
| **Last Rites** | When a Silenced enemy dies, heal the lowest-HP ally and end Silence without any additional trigger. | kill payoff without chain ambiguity | heal, attribution, root guard | E/M |

### Exorcist S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Great Exorcism** | Pyre radius becomes 2; only the highest-Mana enemy is Silenced, but every enemy hit loses Mana. | broad Clock answer | radius, Mana burn, one Silence | E |
| **The Empty Bell** | The first enemy cast each fight is canceled; Maren immediately gains 50% Mana. | encounter insurance | once/fight, Mana refund | M |
| **Witchfinder** | Maren's attacks against Silenced enemies deal +75% damage and extend their Silence by 0.25 seconds, up to 1 second per application. | weapon-facing controller carry | damage %, extension cap, attack cadence | M |

### Cleric communication notes

- Pyre card must visibly change from `DAMAGE + HEAL` to `DAMAGE + MANA BURN + SILENCE`.
- Enemy Mana bars should flash or drain toward Maren's bell icon.
- Exorcist should use a cold/empty bell color, not Lifebinder green or War-Priest orange.

## Bulwark — Brakka, Shieldmaid of the Bronze Hour

### Stable contract

Juggernaut punishes crowds; Warden redirects attention. The missing transformation is **a wall that refuses to be bypassed**: Breacher sacrifices opening durability to reach protected threats.

### New B path: Breacher

**Player copy**

> **BREACHER — SIGNATURE**  
> Shield Slam Leaps to the highest-Mana enemy before striking and Stuns it. Brakka begins with 50% less Bastion Shield.  
> **Builds toward:** backline disruption and anti-ritual.  
> **Trades away:** opening durability.

**Starting mechanics**

- Target highest current Mana; fall back to nearest.
- Leap to a legal adjacent hex.
- Single-target base Slam damage and Stun.
- Bastion Shield scalar: 50%.
- Optional: movement speed becomes one tier faster so the post-Leap chase does not feel broken.

**Levers:** Shield penalty, target selector, Leap frequency, Stun duration, movement speed.  
**Feasibility:** E/M.

### Juggernaut: add one A and one S

| Rank | Node | Player rule | Builds toward | Main levers |
|---|---|---|---|---|
| A | **Crowdbreaker** | Slam deals +20% damage for each enemy after the first, capped at +80%. | enemy width → damage | per-target %, cap |
| S | **Bronze Avalanche** | After Slam, Brakka advances one hex toward the largest enemy cluster and gains Haste for 4 seconds. She does not Leap. | control → continued pressure | cluster selector, Haste, duration |

Crowdbreaker competes with control and Shield generation. Bronze Avalanche supplies movement without erasing the identity of the Breacher path.

### Warden: add one A and one S

| Rank | Node | Player rule | Builds toward | Main levers |
|---|---|---|---|---|
| A | **Shelter Behind Me** | When Warden Taunts an enemy, the lowest-HP ally within 2 hexes gains a small Shield. At most three allies per cast. | Taunt → party protection | Shield, ally cap, radius |
| S | **The Closed Gate** | While at least three enemies are Taunted by Brakka, allies behind her take 20% less damage. | formation-dependent fortress | required enemy count, rear geometry, DR |

The Closed Gate must use visible deployment geometry: “behind” means farther from the enemy side along the board's forward axis, disclosed in advanced help.

### Breacher A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Overrun** | Breacher's Leap damages enemies on the traveled line for 40% Slam damage. | line-clearing entry | damage %, line rules | E/M |
| **Siege Rhythm** | After Leaping, Brakka's next three attacks grant double Mana. | bridge movement → repeat casts | charge count, Mana multiplier | E |
| **Impact Plating** | Brakka gains Shield for each hex traveled, capped at four hexes. | recover sacrificed durability | Shield/hex, travel cap | M |

### Breacher S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Meteoric Entry** | The first Breacher Leap each fight Stuns every enemy adjacent to the landing hex. | opening disruption | once/fight, radius, Stun |
| **No Sanctuary** | After Slam, the target cannot gain Mana for 4 seconds. | hard anti-ritual answer | duration, boss resistance policy |
| **Battering Ram** | If the Leap travels 3+ hexes, Slam deals double damage and Brakka loses all remaining Bastion Shield. | spectacular risk payoff | distance threshold, damage, Shield cost |

### Bulwark communication notes

- Deployment preview should draw a faint line to the currently predicted highest-Mana enemy for Breacher.
- Battering Ram must preview the Shield loss before confirmation.
- Warden protection effects must use a shield/formation cue rather than more Taunt particles.

## Shade — Null, the Redacted

### Stable contract

Shade currently matures at B and chooses Reaper or Phantom at A. Keep the late-bloomer fantasy but expand each decision:

- B: three path-agnostic assassin techniques.
- A: Reaper, Phantom, or new **Saboteur** discipline.
- S: three crowns inside the chosen discipline.

This still yields `3 B techniques × 3 A disciplines × 3 S crowns = 27`.

### B choices: add Dead Drop

| Node | Player rule | Builds toward | Main levers |
|---|---|---|---|
| Existing **Killer's Tempo** | nearby deaths grant temporary Haste | kill chaining | duration, Haste |
| Existing **Opportunist** | Backstab is stronger against wounded targets | execution | threshold, damage |
| New **Dead Drop** | After the opening Ambush Leap, Null gains 25% Mana and a small Shield. | reliable first cast and survival | Mana, Shield, opening-only |

### New A discipline: Saboteur

**Player copy**

> **SABOTEUR — SIGNATURE**  
> Backstab deals 30% less damage, removes 35% of the target's Mana, and Silences it for 2 seconds. Null prefers the enemy with the most Mana.  
> **Builds toward:** backline shutdown.  
> **Trades away:** assassination burst.

**Levers:** damage penalty, Mana burn, Silence, target preference.  
**Feasibility:** E/M.

### Reaper S choices: add Blood Census

| Node | Player rule | Purpose | Levers |
|---|---|---|---|
| Existing **Twist the Knife** | Backstab amplifies a target Null previously crit | crit/signature bridge | multiplier |
| Existing **Widowmaker** | higher crit multiplier | pure deepen | multiplier |
| New **Blood Census** | Each distinct enemy Null crits takes +8% damage from Null, stacking up to three times per enemy. | reliable scaling without Execute dependence | per-stack %, cap, reset |

### Phantom S choices: add Vanishing Point

| Node | Player rule | Purpose | Levers |
|---|---|---|---|
| Existing **Here and Gone** | Backstab causes Phase | scheduled safety | Phase duration |
| Existing **Cold Return** | first post-Phase attack always crits | Phase/crit bridge | forced crit |
| New **Vanishing Point** | When Phase begins, Null leaves an Afterimage for 3 seconds that can be targeted but cannot attack. | defensive misdirection | duration, once-per-root, HP/target rules |

Afterimage is a new temporary entity and is therefore N. If temporary entities are deferred, use the fallback:

> When Phase begins, enemies that were targeting Null are Slowed for 2 seconds before retargeting.

### Saboteur S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Blackout** | Saboteur Backstab also removes 20% Mana from enemies adjacent to the target. Only the target is Silenced. | anti-caster width | radius, Mana % | E |
| **Cut the Fuse** | Against a target above 80% Mana, Backstab's damage penalty is removed and Silence lasts 1 second longer. | timing payoff | threshold, duration | E |
| **Hostage** | While Null remains adjacent to a Silenced enemy, that enemy loses 5% Mana per second and Null gains the same flat amount, capped per second. | positional Clock theft | rate, cap, adjacency | M |

### Shade communication notes

- Saboteur uses Mana-drain and crossed-bell iconography; Reaper uses execution red; Phantom uses absence/Phase white.
- Target preference changes must appear beside range and movement in the hero inspector.
- Dead Drop should say “Opening Leap” so players do not expect it on kill-reset Leaps.

## Sharpshot — Calamity Vance, the Last Deadeye

### Stable contract

Sniper puts damage in the cast; Volleyer puts damage in swings. The third path should not be a third damage payload. **Spotter** turns extreme range into a team focus-fire engine.

### New B path: Spotter

**Player copy**

> **SPOTTER — SIGNATURE**  
> Piercing Bolt deals 50% less damage and Marks the farthest enemy for 6 seconds. Marked enemies take 15% more attack damage from all allies.  
> **Builds toward:** focus fire and teamwide auto-attack engines.  
> **Trades away:** signature burst.

Use the existing Mark vocabulary if its semantics can be generalized. Otherwise author a distinct `Exposed` status to avoid collision with Shade's personal mark.

**Levers:** damage penalty, duration, team amplification, target selector.  
**Feasibility:** M.

### Sniper: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Measured Breath** | If Calamity has not moved for 4 seconds, her next Bolt deals +40% damage. Moving resets the charge. | stationary artillery | charge time, damage |
| S | **Through the Hour** | Bolt ignores the first enemy it would hit and begins dealing damage from the second occupied hex onward. Its primary target still takes damage. | punish protected backlines | skip count, target exception |

Through the Hour is an answer choice; it is worse when the frontliner itself is the priority.

### Volleyer: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Powder Rhythm** | Every fifth Volley arrow that hits grants 10% Mana. Excess hits during the same attack count once. | wide volleys → cast cadence | hit threshold, Mana, per-attack guard |
| S | **Endless Magazine** | Each cast permanently adds two arrows instead of one, but extra arrows deal 20% less damage. | faster ramp with lower per-arrow weight | growth, damage penalty |

### Spotter A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Rangefinder** | The first attack against an unmarked enemy Marks it for 3 seconds. One target at a time. | opener/reliability | duration, one-target rule | M |
| **Suppressing Fire** | Every third allied attack against the Marked enemy briefly Slows it. | team attacks → control | Nth hit, Slow, duration | M |
| **Crossfire** | Allies at least 3 hexes from the Marked enemy deal +10% attack damage to it. | formation bridge | distance, damage % | M |

### Spotter S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Kill Order** | When a Marked enemy dies, allies that damaged it gain 20% Mana. Once per enemy. | focus-fire payoff | Mana, contributor tracking | M |
| **Open Season** | Mark amplification increases to 25%, but Mark lasts 2 seconds less. | burst window | magnitude, duration | E |
| **Passing the Scope** | When Mark expires or its target dies, it moves to the farthest unmarked enemy at half remaining duration; maximum two transfers. | bounded chain | transfer cap, duration | M |

### Sharpshot communication notes

- Mark must be visually unmistakable and show its teamwide amplification in the enemy inspector.
- Calamity's card should summarize “Damage source: Signature / Attacks / Team” for the three B paths.
- Do not animate every allied Mark hit. Hold a focus-reticle glow and count hits.

## Pyromancer — Ilion-7, Cinder of a Dead Star

### Stable contract

Inferno makes ground dangerous; Starfall makes a target dangerous. The third path should use the Field pillar without creating another Burn damage path. **Glasswright** turns heat into temporary walls and routing pressure.

### New B path: Glasswright

**Player copy**

> **GLASSWRIGHT — FIELD**  
> Fire Glyph deals no direct damage and creates a three-hex Glass Wall across the target's lane for 5 seconds. Enemies adjacent to the wall gain 2 Burn each second.  
> **Builds toward:** routing, separation, and time buying.  
> **Trades away:** immediate spell damage.

Wall orientation should be deterministic and previewable: perpendicular to the line between Ilion and the target, clipped to legal board hexes.

**Levers:** wall length, lifetime, Burn rate, orientation, cast cadence.  
**Feasibility:** M/N depending on authored wall-line support.

### Inferno: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Flashover** | The first time an enemy enters each Fire Field, it immediately takes one Burn tick. | movement punishment | once-per-field-target, tick scalar |
| S | **Cinder Dominion** | Allies standing in Ilion's Fire Fields are immune to their damage and gain 15% Haste. | team Field bridge | Haste, friendly applicability |

### Starfall: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Falling Pressure** | Starfall deals +3% damage for every 10% Mana the target currently has. | anti-caster burst | per-band %, cap |
| S | **Event Horizon** | Starfall pulls no units, but enemies adjacent to the target are Slowed and take 25% of its direct damage. | readable splash without displacement | splash %, Slow, radius |

### Glasswright A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Kiln Wall** | Burn applied beside Glass Walls increases from 2 to 4 per second. | deepen wall threat | Burn rate | E |
| **Annealed Shelter** | Allies adjacent to a Glass Wall gain a small Shield each second, up to a per-wall cap. | bridge wall → defense | Shield rate, cap | M |
| **Shatterglass** | When a Glass Wall expires, adjacent enemies take damage and are briefly Slowed. | expiry payoff | damage, Slow, duration | M |

### Glasswright S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Labyrinth** | Fire Glyph creates a second, shorter parallel Glass Wall one hex behind the first. | routing control | second length, spacing, lifetime | M |
| **Prism Prison** | If the primary target is adjacent to two Glass Wall hexes, it is Silenced while that remains true. | positional anti-caster answer | geometry, boss policy | M |
| **Obsidian Memory** | When a Glass Wall expires, its center hex remains Burning for the rest of the fight. Maximum three permanent hexes. | bounded map transformation | permanent-hex cap, Burn rate | M |

### Pyromancer communication notes

- Deployment and pre-cast preview must show wall orientation.
- The client must distinguish blocking wall hexes from damaging Fire Fields.
- “Permanent” means for the current fight and should always say so in expanded text.

## Berserker — Ulfrik, Who Burns His Hours

### Stable contract

Bloodreaver converts frenzy into survival; Rampager converts it into width. The third path, **Headsman**, converts the multi-swing signature into one deliberate execution blow for boss and durable-target builds.

### New B path: Headsman

**Player copy**

> **HEADSMAN — SIGNATURE**  
> Frenzy becomes Final Stroke: one attack dealing 250% weapon damage, increased by Ulfrik's missing HP. It cannot Cleave or Lifesteal unless another rule adds those effects.  
> **Builds toward:** single-target burst and low-health execution.  
> **Trades away:** the Frenzy swing window.

Suggested missing-HP scaling: +1.0% damage per 1% missing HP, capped at +75%.

**Levers:** base weapon scalar, missing-HP conversion, cap, ManaMax.  
**Feasibility:** M.

### Bloodreaver: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Open Vein** | The first Frenzy hit against each enemy applies a short effect that increases Ulfrik's Lifesteal against it by 50%. | sustained drain target | duration, per-target, Lifesteal |
| S | **Hours Stolen** | Each Frenzy kill extends Deathless's availability or grants a permanent in-fight 5% max-HP heal; if Deathless has not been chosen, use the heal. Cap four. | kill sustain without requiring one crown | heal %, cap |

Do not literally refresh a once-per-fight cheat death unless a future balance pass explicitly approves multiple deaths. The safe implementation is the capped max-HP heal.

### Rampager: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Center of Violence** | Frenzy Cleave deals +15% damage for each adjacent enemy after the first, capped at +60%. | surrounded payoff | per-enemy %, cap |
| S | **Warpath** | During Frenzy, if no enemy is adjacent after a swing, Ulfrik steps one hex toward the nearest enemy before the next charge is spent. | prevent empty Frenzy windows | step count, path legality |

### Headsman A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Scent of the Condemned** | Final Stroke deals +50% damage to targets below 40% HP. | execution deepen | threshold, damage | E |
| **Blood Price** | Before Final Stroke, Ulfrik loses 15% current HP and adds twice that amount as damage. Cannot reduce him below 1 HP. | risk conversion | HP cost, conversion | M |
| **Heads Will Roll** | If Final Stroke kills, gain 50% Mana. | bounded chain setup | refund, kill attribution | E/M |

### Headsman S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **The Last Name** | Against a boss or the final living enemy, Final Stroke deals double damage. | boss/single-target answer | target rule, multiplier | M |
| **Executioner's Mercy** | If Final Stroke fails to kill a target below 25% HP, Ulfrik gains Shield equal to 30% of damage dealt. | miss insurance | threshold, Shield conversion | M |
| **Red Eclipse** | The first Final Stroke each fight hits every enemy adjacent to the target for 50% damage. | opening spectacle/limited width | once/fight, splash % | E |

### Berserker communication notes

- The B-rank comparison should literally read `Frenzy feeds: SELF / CROWD / ONE TARGET`.
- Final Stroke should show predicted current missing-HP bonus in the inspector.
- Blood Price must use a clear cost line and never hide self-damage inside body text.

## Phalanx — Leonnatos of the Unbroken Line

### Stable contract

Pikewall holds the player's line; Lancer breaks the enemy line. The third path, **Tactician**, turns directional geometry into allied formation tempo rather than personal damage.

### New B path: Tactician

**Player copy**

> **TACTICIAN — SIGNATURE**  
> Skewer deals 50% less damage. Allies standing directly behind Leonnatos gain 20% Mana and Haste for 3 seconds when he casts.  
> **Builds toward:** directional formation support.  
> **Trades away:** signature damage.

“Behind” is determined from the enemy-facing board axis and previewed during deployment.

**Levers:** damage penalty, line width, Mana, Haste, duration.  
**Feasibility:** M.

### Pikewall: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Brace Together** | When Leonnatos Counters, the ally directly behind him gains a small Shield. Once per original enemy attack. | Counter → formation defense | Shield, rear selector, root guard |
| S | **Sixth Spear** | After five Counters, the next Counter strikes all enemies on its line and resets the count. | readable counter payoff | N count, line damage |

### Lancer: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Advancing Measure** | Each enemy hit by Skewer grants Leonnatos 5% Haste for 4 seconds, capped at four stacks. | line connection → tempo | Haste, duration, cap |
| S | **King's Road** | After Skewer hits 3+ enemies, its line becomes a friendly Field for 5 seconds that grants movement speed. | damage line → team route | hit threshold, Field duration, speed |

### Tactician A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Dress the Line** | Allies affected by Tactician gain Shield in addition to Mana and Haste. | defensive formation | Shield | E |
| **Forward Signal** | Affected allies also step one legal hex forward if no enemy is currently in their weapon range. | controlled formation movement | step condition, one step | M |
| **Measured Volley** | The next attack by each affected ally deals +30% damage. | party attack bridge | charge count, damage | E |

### Tactician S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **The Long Command** | Tactician affects allies up to three hexes directly behind Leonnatos instead of two. | formation breadth | line length | E |
| **Cadence of Bronze** | Every third allied cast by a unit affected by Tactician refreshes its Haste for 2 seconds. | cast/formation bridge | N casts, duration, per-unit tracking | M |
| **Ordered Retreat** | The first time an affected ally falls below 30% HP, it gains movement speed away from its target and Shield for 3 seconds. Once per ally. | anti-collapse insurance | threshold, Shield, once/ally | M |

### Phalanx communication notes

- Deployment shows the support line as highlighted hexes behind Leonnatos.
- Tactician effects use arrow/formation language; Banneret continues to own circular Company/muster language.
- Forward Signal must never move an ally already able to attack; otherwise the game appears to override a good position.

## Banneret — Capitana Vespera, Banner of the Turning Age

### Stable contract

Herald protects the Company; Warcaller disrupts enemies. The third path, **Standard-Bearer Ascendant**, converts successful leadership into personal combat power without removing Vespera's party identity.

Public path name recommendation: **Vanguard**.

### New B path: Vanguard

**Player copy**

> **VANGUARD — PASSIVE**  
> Rally grants Vespera one Command for each other ally affected, up to 5. Each Command gives +8% Attack until her next Rally. Rally no longer grants Mana to Vespera.  
> **Builds toward:** support-fed personal carry.  
> **Trades away:** self Mana from Rally.

This is a bounded personal scaling loop: a well-maintained formation makes the leader dangerous, but isolation turns it off.

**Levers:** Command cap, Attack per stack, affected-allies requirement, Rally cadence.  
**Feasibility:** M.

### Herald: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Shared Burden** | When a Company ally's Rally Shield breaks, Vespera gains 10% Mana. Maximum once per ally per Rally. | Shield loss → more support | Mana, per-Rally guard |
| S | **Unfading Colors** | Company allies retain 50% of unused Rally Shield when Rally is cast again, up to the normal Shield amount. | Shield continuity | carryover %, cap |

This is not ally cheat-death and preserves the earlier rejection.

### Warcaller: add one A and one S

| Rank | Node | Player rule | Builds toward | Levers |
|---|---|---|---|---|
| A | **Broken Cadence** | Enemies Slowed by Rally gain 20% less Mana from attacks for the Slow's duration. | Slow → cast denial | reduction %, duration |
| S | **The Last March** | The first time the fielded warband is reduced to half its starting members, surviving allies gain Haste and 25% Mana. Once per fight. | comeback tempo | threshold, Haste, Mana |

If `Last March` already exists under another exact implementation, migrate the new effect under a new name rather than silently changing public semantics.

### Vanguard A choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Lead from the Front** | While Vespera is the closest ally to the enemy side, each Command also grants damage reduction. | exposed carry | DR/stack, positional condition | M |
| **Duelist's Standard** | At 5 Command, Vespera's attacks strike twice; the second strike deals 40% damage. | personal payoff | threshold, second-hit % | M |
| **Spoils of Command** | When an ally affected by Rally kills an enemy, Vespera gains one Command. Maximum one per root death. | ally kills → carry | cap, root guard | M |

### Vanguard S choices

| Node | Player rule | Purpose | Levers | Feasibility |
|---|---|---|---|---|
| **Banner of One** | At 5 Command, Rally spends all Command to deal damage around Vespera and Shield affected allies. | cash-out loop | damage/stack, Shield/stack | M |
| **First Through the Gate** | The first Rally each fight Leaps Vespera adjacent to the farthest enemy after buffing allies. She gains Shield per ally affected. | dramatic vanguard conversion | once/fight, Shield, Leap | E/M |
| **Living Inscription** | When an Inscription activates, Vespera gains 5% Mana, at most once per root event. | Hourstone bridge | Mana, root guard | M; depends on Inscription event layer |

Living Inscription is the recommended replacement direction for the current blanket `Bearer of the Mark` multiplier. It scales with activity, not total collection size, and cannot make Vespera mandatory merely because the run owns many Inscriptions.

### Banneret communication notes

- Show Command as five pips attached to Vespera's banner, not a generic status stack.
- The B comparison should read `Rally protects / disrupts / empowers Vespera`.
- Living Inscription should pulse the relevant Hourstone badge first, then the banner pip, preserving causal order.

## Existing-path expansion summary

The proposal adds the following to every standard hero:

- one third B path;
- one A node and one S node to each existing path;
- three A nodes and three S nodes to the new path.

That is **11 new nodes per hero**, or **88 new nodes** across the current roster. Shade uses the same total outcome target through its late-bloomer shape.

Do not implement all 88 at once. The full catalog exists so early architecture does not bake in another two-option ceiling.

## Future hero packages

These are intentionally scoped for future use. Each requires a new combat problem and an explicit simulation feature before implementation.

### Future Hero 1: Chronomancer — clock storage and delayed effects

**Working identity:** a support/caster who stores time rather than directly granting ordinary Haste.

**C kit**

- Innate **Borrowed Second:** every time an ally is Slowed, store one Time charge, cap 5.
- Signature **Release the Hour:** spend Time to advance nearby allies' attack and Mana clocks by a bounded amount.
- Starter weapon: staff.

**B paths**

- **Accelerant:** spend Time on allied tempo.
- **Arrester:** spend Time to delay enemy clocks and Field pulses.
- **Prophet:** schedule an effect now that resolves after a visible countdown at multiplied strength.

**Why it is not Banneret:** Banneret applies immediate formation tempo; Chronomancer banks and schedules clock movement.

**Required feature:** explicit delayed effect/countdown events and clock-advance primitive.  
**Encounter gate:** an authored enemy with a visible timed phase where banking versus immediate tempo creates different answers.  
**Primary balance levers:** charge cap, generation sources, spend rate, countdown, maximum clock displacement.

### Future Hero 2: Geomancer — displacement and authored terrain

**Working identity:** controls engagement geometry through walls, Push, and Pull.

**C kit**

- Innate **Fault Sense:** gains Mana when a summoned wall expires or is destroyed.
- Signature **Raise Fault:** create a short wall line.
- Starter weapon: mace or staff.

**B paths**

- **Architect:** larger, longer walls and allied shelter.
- **Seismist:** walls erupt into damage and Stun.
- **Gravemover:** signatures Push or Pull units relative to wall geometry.

**Required feature:** explicit Push/Pull/collision law, wall ownership/destruction, and displacement preview.  
**Encounter gate:** enemies whose formations make displacement a useful option without making it mandatory.  
**Primary balance levers:** displacement distance, collision behavior, wall count, lifetime, immunity windows.

Glasswright Pyromancer is the low-risk proof for walls. Only add Geomancer if wall play proves fun enough to support a whole chassis.

### Future Hero 3: Alchemist — state conversion

**Working identity:** turns statuses into different resources rather than simply applying more statuses.

**C kit**

- Innate **Residue:** when a status expires nearby, gain one Reagent, cap 6.
- Signature **Transmute:** spend Reagents to convert an ally's harmful status into Shield or convert an enemy's beneficial status into Burn.
- Starter weapon: censer.

**B paths**

- **Physician:** cleansing and regeneration.
- **Toxicologist:** Burn, Slow, and anti-heal pressure.
- **Catalyst:** consumes both allied and enemy statuses for burst Mana and damage.

**Required feature:** status dispel/convert attribution and clear PvE rules for removable versus unremovable effects.  
**Encounter gate:** authored encounters that apply enough debuffs or healing for conversion to be a choice, not a tax.  
**Primary balance levers:** Reagent cap, conversion list, effect value, target count, status immunity.

The current roster deliberately omits cleanse and anti-heal. Do not add Alchemist until enemy content earns those answers.

### Future Hero 4: Machinist — persistent constructs

**Working identity:** spends cast cadence to deploy a small secondary board engine.

**C kit**

- Innate **Calibration:** attacks against the same target improve construct accuracy or damage, with a cap.
- Signature **Deploy Turret:** create one temporary turret in the nearest legal rear hex.
- Starter weapon: musket.

**B paths**

- **Gunner:** one scaling damage turret.
- **Field Engineer:** support beacon that grants Shield/Mana.
- **Demolitionist:** short-lived mines and delayed explosions.

**Required feature:** deterministic temporary entities, ownership, targeting, path blocking policy, replay identity, and summon caps.  
**Encounter gate:** an encounter where setup time and protected backline space matter.  
**Primary balance levers:** entity cap, lifetime, inherited stats, placement, setup time, retargeting.

Vanishing Point's Afterimage is the smallest possible temporary-entity proof. Do not build Machinist until that lifecycle is robust and readable.

## Future catalog policy

New heroes graduate from “future package” to “candidate” only when:

1. an authored encounter exposes a missing satisfying answer;
2. the required simulation primitive has at least two valid consumers;
3. the hero's base contract cannot be delivered as a third path on an existing chassis;
4. the player's one-sentence explanation does not overlap an existing hero;
5. the hero adds at least three new cross-hero build connections;
6. its worst-case combat state remains visually legible.

---

# 3. Execution brief for Claude or Codex

## Recommended delivery sequence

### Phase 0 — architecture only

Goal: remove the two-choice assumption without changing live balance.

- Replace tuple-shaped offers with ordered `SpecOffer.NodeIds`.
- Update `IRunContent`, `Catalog`, rank-up state, UI models, cards, and tests.
- Temporarily make every current offer three choices by adding a clearly labeled development-only duplicate sentinel only in test fixtures, not real content; or keep production validation at `>=2` until the first real three-choice hero lands.
- Preserve all existing node ids and saves.
- Add an offer-arity validation report.
- Add a 3-card responsive layout and keyboard/controller navigation.

**Exit gate:** existing runs, saves, all 8 current trees, and replay fixtures behave identically.

### Phase 1 — vertical slice: Bulwark and Shade

Why these two:

- Bulwark proves a standard B-path expansion and movement/targeting transformation.
- Shade proves the late-bloomer 3×3×3 structure and anti-caster role swap.
- Both interact strongly with current authored encounters: Last Oath, Ninth Bell, The Drop, Ashfall Battery.

Implement:

- Bulwark Breacher plus all proposed third Juggernaut/Warden options.
- Shade Dead Drop, Saboteur, and third Reaper/Phantom crowns.
- Use the fallback Slowed-retarget implementation for Vanishing Point unless temporary entities already have an approved lifecycle.

**Exit gate:** 54 final builds compose (`27 × 2`), resolve fights, serialize, and render their rules.

### Phase 2 — damage-engine quartet

Implement:

- Sharpshot Spotter;
- Pyromancer Glasswright;
- Berserker Headsman;
- their existing-path third choices.

This phase proves Mark/team amplification, wall Fields, and signature-operation replacement.

**Exit gate:** encounter probes show each third path has at least one encounter where it is materially better and one where an existing path is materially better.

### Phase 3 — formation and support

Implement:

- Cleric Exorcist;
- Phalanx Tactician;
- Banneret Vanguard;
- Inscription-trigger event needed for Living Inscription.

This phase should follow the Inscription engine's per-root activation guard so support chains are measured honestly.

### Phase 4 — full tuning and content release

- Tune all 216 final existing-hero builds across weapons and representative parties.
- Cut, merge, or rewrite nodes that remain dominated.
- Add UI art, icons, cast tells, and result attribution only after mechanical survival.
- Promote only stable content into the first-playable pool; keep the remainder authored but feature-flagged for later acts/unlocks if needed.

## Required automated validation

### Structural tests

- Every chassis resolves C, B, A, and S.
- Every real rank offer has exactly three unique nodes.
- Every node id is stable, unique, and serializable.
- Every chosen node is legal for its prerequisite path.
- Every path has exactly three A and three S choices.
- Every final build composes with all 11 weapon categories and all trinkets.
- Signature overrides never conflict silently.
- Player-facing descriptions exist and pass the mechanical-copy grammar.

### Combinatorial compose test

Generate all final trees:

```text
for hero in heroes:
  for b in B(hero):
    for a in A(hero, b):
      for s in S(hero, b):
        compose(hero, b, a, s, everyRepresentativeWeapon)
```

At minimum, test each final tree with:

- starter weapon;
- one specialized alternate weapon;
- one unmastered “weird” weapon;
- no trinket and one cadence-affecting trinket.

### Combat probes

For every path, report:

- win rate and draw rate;
- fight duration;
- damage, healing, Shield, control uptime, Mana generated/burned;
- signature casts and effective casts;
- movement distance and idle time;
- target switches;
- node activation counts;
- unused or capped activations;
- contribution by source;
- result across each authored encounter and pressure tier.

Do not target equal win rates across all encounters. Target **comparative shape**:

- every path has a favorable exam;
- every path has an unfavorable exam;
- no path is best across the entire encounter pool;
- geometry-sensitive paths change outcome across formations;
- high-synergy paths are not also the safest floor.

### Balance alarms

Fail or flag when:

- a node activates zero times in more than 80% of eligible fights;
- one choice is selected by a baseline policy more than 70% of the time across diverse states;
- an S node adds less than 10% to its intended engine when enabled;
- a B path changes text but not placement, targeting, cadence, outputs, or encounter performance;
- control uptime exceeds its safety band;
- a chain hits the global event cap;
- a unit is idle while a reachable target exists;
- a transformation makes the original weapon or signature description false;
- a path has no player-visible event proving its rule occurred.

## Required manual playtest questions

After each hero reaches S:

1. Can the player describe what the hero became in one sentence?
2. Can the player identify which rank choice caused the biggest transformation?
3. Did any offer contain an option that was obviously irrelevant before reading it?
4. Did the chosen path change placement or equipment?
5. Could the player tell when the engine activated during combat?
6. Did the next disclosed encounter create a credible reason to choose a different option?
7. Was the S-rank payoff spectacular enough to feel like a crown?

## Agent implementation prompt

Use the following as the starting prompt for Claude or Codex:

> Implement Warband's three-choice specialization architecture and the next approved hero slice from `warband_roster_expansion_plan.md`.
>
> First inspect the current repository sources of truth: `docs/vault/Design/heroes.md`, `docs/vault/Design/roster.md`, the relevant `docs/vault/Design/dives/*.md`, `sim/Warband.Content/Kits.cs`, `Catalog.cs`, the run-layer rank-up flow, save models, and rank-choice UI. Preserve existing public node ids and save compatibility.
>
> Work in narrow commits. Start with schema/UI/test support for ordered variable-arity offers; do not mix new balance content into that commit. Then implement one hero or one path at a time. Prefer existing Clock, Field, status, trigger, selector, movement, and composition primitives. If a proposal requires a new primitive, stop and write a small ADR covering semantics, determinism, replay representation, cascade behavior, balance levers, presentation, and at least two valid consumers before implementing it.
>
> Every node must have: authored prerequisites, player-facing rules text, a short build direction, explicit tuning fields, activation attribution, structural tests, compose coverage, and encounter-probe output. Do not tune solely against aggregate win rate. Measure encounter-relative strengths, formation sensitivity, activation counts, control uptime, and idle behavior.
>
> Do not silently reinterpret an existing named node. If public semantics change, create a new node id or document a migration. Do not push, open a PR, publish a build, or alter external systems without explicit approval.

## First implementation ticket

**Title:** Three-choice spec offers + Bulwark Breacher vertical slice

**Acceptance criteria**

- `SpecOptions` is no longer tuple-shaped.
- Current saves and existing node ids remain valid.
- Rank UI displays three cards at desktop and landscape-phone widths.
- Keyboard/controller focus reaches every card predictably.
- Bulwark B offer is Juggernaut / Warden / Breacher.
- Every Bulwark path has three A and three S choices.
- All 27 Bulwark final builds compose with starter, specialist, and unmastered test weapons.
- Breacher targeting is deterministic and disclosed.
- Breacher has at least one favorable and one unfavorable authored encounter in probe results.
- Battering Ram's Shield cost is previewed and measured.
- No control lock, pathing stall, replay divergence, or save regression.

## Research references

- [Warband repository](https://github.com/AjaxIsLemons/warband)
- [Guildrun official Steam page](https://store.steampowered.com/app/3669200/Guildrun/)
- [Guildrun systems overview: ranks, multiclassing, Backup, relics, items, and auctions](https://mobalytics.gg/news/guides/guildrun-demo-showcase)
- [Guildrun demo Steam page](https://store.steampowered.com/app/4425970/Guildrun_Demo/)
- [The Last Flame developer description of its hero/item/relic synergy model](https://www.reddit.com/r/pcgaming/comments/1991zj2/i_just_released_my_first_game_the_last_flame_a/)

## Final design call

Build the three-choice architecture now, but release content in proof-sized slices.

The current eight heroes are not too few. Their trees are too narrow relative to the game's stated fantasy. Expanding them to three paths, three techniques, and three crowns creates enough space for tanks to become divers, assassins to become controllers, artillery to become team support, and supports to become carries—while continuing to speak Warband's existing language of Clock, Field, formation, weapons, and authored PvE exams.

New heroes should then arrive as mechanical events: Chronomancer introduces scheduled time, Geomancer introduces displacement, Alchemist introduces status conversion, and Machinist introduces temporary entities. None should be just another damage profile.
