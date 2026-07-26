# The Hourstone and Inscriptions — persistent warband engine layer

**Date:** 2026-07-24

**Status:** system, presentation, and acquisition doctrine accepted; catalog effects, exact
run cadence, offer layout, prices, and numerical tuning remain design work. Recorded by
[ADR 0017](../Decisions/0017-hourstone-and-inscriptions.md).

Inscriptions are the run's broadest cross-hero engine layer. Heroes, weapons, formation,
the Clock, and the Field create events; Inscriptions connect those events into a machine
that may become spectacularly unfair.

## Source boundaries

- This page owns the Hourstone fiction, persistent ownership law, Inscription authoring
  grammar, content plan, and minimum presentation contract.
- [heroes.md](heroes.md) owns the rest of the party layer and preparation flow.
- [sim-framework.md](sim-framework.md) owns deterministic event cascades, attribution, and
  replay.
- [pve-encounters.md](pve-encounters.md) owns enemy and encounter rules. Those rules are not
  player Inscriptions.
- `sim/Warband.Content/Catalog.cs`, `BannerDef`, `BannerIds`, and related names are the
  current legacy implementation. They remain truthful about what runs today, but are not
  the intended public vocabulary.

## The physical fiction

Every expedition carries an **Hourstone**, a tablet cut from the Tower outside time. It
binds champions from incompatible histories to one shared Hour. Laws recovered from dying
timelines are inscribed into its surface, and every warrior bound to the stone obeys them.
When the expedition ends, the Hourstone is sealed as a dead record; if the warband falls,
it fractures. Either way, that impossible combination of laws cannot enter another run.

This is a load-bearing extension of the Last Hour frame:

- it explains how an era-spanning warband remains coherent;
- it gives teamwide effects one physical source;
- it explains why acquired effects all persist and act together;
- it makes the run reset diegetic without creating account-scoped power; and
- it keeps the mixed-era texture on a Tower artifact rather than blending eras inside a
  champion.

An Inscription need not look like a generic fantasy rune. The Hourstone may accumulate
carved pictograms, illuminated script, military stamps, tally marks, clock notation, and
future circuitry. The Tower is the constant that can hold all of them.

The Hourstone is not a combat unit, objective, target, or source of board occupancy.
Representing it physically during combat is optional. Mechanical clarity outranks showing
the prop.

## Ownership law

- A run may own any number of distinct Inscriptions.
- Every acquired Inscription remains active for the rest of that run.
- There are no equip slots, active limits, or pre-fight Inscription loadouts.
- The same named Inscription is acquired at most once in the first implementation.
  Combinations between different rules are the engine; duplicate stat stacking is not.
- Inscriptions do not survive into the next run. Meta progression may reveal new ones but
  may not preserve their power.
- Inscriptions are player build content. Authored PvE enemies use disclosed encounter rules
  rather than pretending to own the same collection.

Routine sale, erasure, and replacement are not required for the first playable. A later
event may rewrite the Hourstone if removal itself proves to create interesting commitments.

Because every acquisition is permanent and positive unless explicitly labeled otherwise,
the decision lives at acquisition: spend run currency, choose it over another reward, accept
an event consequence, or take on encounter risk. Exact placement and cadence belong to the
run-layer design.

## Acquisition doctrine

Inscriptions use a hybrid acquisition model. **Shops may sell them**, creating an economic
tradeoff against heroes, weapons, forging, and other preparation. **Selected rewards may
offer one Inscription from a visible choice of three**, creating stronger direction without
letting the player order an exact build. Bosses and exceptional events are the natural home
for one-from-three **Paradox** or major rule-rewrite choices rather than routine shop stock.

This source mix is a design staple:

- there is no Hourstone capacity cost, replacement, or equip decision after acquisition;
- shop Inscriptions use the run's shared currency rather than a dedicated resource;
- reward choices grant at most one of the presented Inscriptions;
- normal shop discovery remains imperfect enough that the player adapts to what appears;
- choice rewards provide bounded steering, not a deterministic recipe selector; and
- access to this core engine layer is not reserved exclusively for the hardest risk tier.

The first run-layer test should make an early Foundation available, use paid shop discovery
for most ongoing access, include at least one one-from-three normal reward, and use a
post-boss Paradox choice as a mutation for the next act or endless. This is a test shape, not
a locked node count.

Exact implementation specifics remain parked until the PvE run is built out:

- dedicated shop lane versus sharing an existing offer rotation;
- offer frequency, freeze/reroll behavior, and price bands;
- exact timing and number of one-from-three rewards;
- whether an unwanted reward may be exchanged for currency;
- whether Stable / Fraying / Collapsing changes choice width or reward access;
- standard-run and endless acquisition density; and
- which authored Inscriptions belong to shop, normal-reward, event, and Paradox pools.

Offer generation may protect an early choice from being useless and the UI may identify
owned heroes or rules that can trigger an offered Inscription. It should not secretly
guarantee the exact partner for the player's current engine. The player should recognize
and assemble a line from bounded opportunities, not execute a shopping list.

## Authoring law

An Inscription should state one readable rule. Depth comes from several rules interacting,
not from one tooltip containing an entire build.

Every authored entry identifies:

1. **Trigger or passive condition:** what observable event or state wakes it?
2. **Scope and target:** which heroes, enemies, hexes, or run resource does it affect?
3. **Effect:** what existing combat or run verb does it produce?
4. **Cascade behavior:** may its output wake other Inscriptions, and may it repeat inside
   one root event?
5. **Presentation state:** icon, counter if any, and what the results screen can attribute.

Warband combat is continuous. Use **start of combat**, a Clock threshold, or event counters
rather than “start of round.”

### Content families

| Family | Job | Example shape |
|---|---|---|
| **Foundations** | Introduce a source that other rules can use | Allied automatic attacks apply Burn. |
| **Bridges** | Convert one engine into another | Whenever an ally is healed, it gains Mana. |
| **Counters** | Reward repeated behavior | Every third allied cast Shields the warband. |
| **Payoffs** | Cash out accumulated state | When a Burning enemy dies, transfer its Burn. |
| **Openers and formations** | Change the initial board problem | Heroes mustered beside an ally begin Shielded. |
| **Paradoxes** | Rewrite a rule with a meaningful drawback | Healing cannot restore HP; it creates additional Shield instead. |

Static passives, start-of-combat effects, reactions, thresholds, every-N counters, economy
effects, and run-scoped growth are all legal. Early catalog waves should emphasize combat
connections; economy and cross-fight growth can expand only after the fight-facing engine
is readable.

Common and early Inscriptions should usually provide a useful floor without one exact
partner. Narrow counters such as anti-Leap rules are legal because they accumulate rather
than occupying a slot, but their acquisition timing must let the player make an informed
choice.

## Cascade law

Inscription-to-Inscription chains are intended. Attack → Burn → Mana → cast → Shield →
Haste is a success case, not an exploit.

The large catalog needs a stronger default guard than the sim's existing global depth and
event-budget limits:

- by default, one named Inscription activates at most once per root event;
- an Inscription cannot wake itself from an event it created;
- explicitly authored repeaters may override the once-per-root default;
- all child events preserve deterministic order and root attribution; and
- accidental cycles, safety-cap hits, unreadable trigger storms, and unresolved fights are
  failures even when the resulting power would otherwise be welcome.

This per-root activation rule is planned machinery, not present in the legacy Banner
implementation. Until it exists, new chain content must use the current root-event guards
conservatively.

## Presentation contract

The first combat presentation is a compact **Inscription badge rail** at the top of the
screen. It is sufficient; the Hourstone does not need to appear on the battlefield.

Each owned Inscription has:

- a stable icon badge, ordered consistently;
- hover or tap inspection with its full rule;
- a brief pulse, outline, or brightness change when it activates;
- pips, a ring, or a small numeral when it has persistent counter progress; and
- an optional activation count or attributed result in the post-fight readout.

Only the responsible badge animates. Chained Inscriptions pulse in resolution order so the
player can read the machine without another layer of world-space particles.

High-frequency rules coalesce repeated activations into a held glow plus a rising count
rather than flashing on every event. The rail may wrap, scroll, or collapse into rows as the
collection grows; it must never cover enemy intent, unit health, or the fight result.

### Replay and client contract

The badge rail is a view of authoritative replay data:

1. The sim emits an `InscriptionTriggered`-equivalent event carrying the Inscription id,
   root event, and any counter context.
2. Replay serialization preserves it.
3. Playback state folds counter progress and recent activations.
4. The client pulses the matching badge; it never reconstructs trigger logic itself.
5. Post-fight attribution folds the same event stream.

Public and serialized renames should happen as a deliberate migration. Legacy `Banner*`
code may remain temporarily, but newly authored interfaces should not spread that name.

## Initial catalog plan

The current five runnable team triggers become the seed set:

| Legacy name | Working Inscription name | Family |
|---|---|---|
| Banner of the First Hour | **The First Bell** | death payoff / tempo bridge |
| Banner of the Held Line | **The Closed Gate** | enemy-action counter |
| Banner of the Brand | **Cinder Law** | Burn foundation |
| Banner of the Bronze Hour | **Bronze Testament** | defensive opener |
| Banner of the Chorus | **Chorus of Hours** | cast-to-Shield bridge |

The first meaningful engine catalog targets **24 Inscriptions**:

- 6 Foundations and openers;
- 8 Bridges;
- 6 Counters and payoffs; and
- 4 Paradoxes or major rule-breakers.

This deliberately replaces the old five-banner content cap. The implementation should still
arrive in proof-sized waves:

1. **Proof rail:** migrate the five seeds, trigger events, and badge presentation.
2. **Vocabulary proof:** grow to twelve, covering every family and every major roster
   engine at least once.
3. **Engine proof:** grow to twenty-four only after the first twelve remain legible,
   deterministic, and combinable.

The architecture should tolerate a much larger eventual catalog, but hundreds of rules are
not a first-playable deliverable. Add later content in tested families rather than as
isolated clever effects.

## Banneret relationship

Banneret keeps literal banner identity: the Company Standard is a weapon, and Vespera's
banner defines muster membership and Rally geometry. That is separate from the Hourstone.

The current **Bearer of the Mark** implementation doubles every legacy Banner. Unlimited
persistent Inscriptions would make blanket doubling scale with the entire collection and
risk making Banneret compulsory. Reopen that node before catalog expansion.

The preferred replacement direction is an **Inscription-fed Banneret engine**, such as
gaining Mana when an Inscription activates, at most once per root event. Exact output and
magnitude remain part of the Banneret/content pass.

## Plan gates

### Settled

- Hourstone as the communal physical fiction.
- Inscriptions as the public system name.
- unlimited distinct ownership, all active for the run.
- unique copies rather than duplicate stacking.
- trigger, passive, opener, counter, payoff, and rule-rewrite content.
- compact top-screen badges as the minimum combat presentation.
- triggered badges pulse; counter badges expose progress.
- no required Hourstone battlefield object.
- hybrid acquisition through paid shop offers and one-from-three rewards.
- Paradoxes and major rule rewrites belong primarily to bosses or exceptional events.

### Must settle before implementation expands beyond the five seeds

- shop layout, offer cadence, reward timing, and price bands;
- public-data migration strategy from legacy Banner names;
- per-root activation guard representation;
- exact Bearer of the Mark replacement; and
- the first twelve effect contracts.

### Playtest questions

- Can players explain which two or three Inscriptions made their engine work?
- Does the badge rail reveal chains without stealing attention from combat?
- Do accumulated narrow counters feel like useful preparation history or shop clutter?
- Does unlimited persistence create discovery, or do runs converge on the same collection?
- Do shop offers and one-from-three rewards provide enough steering without enabling recipes?
- Are Paradoxes exciting commitments rather than disguised automatic upgrades?
- At what collection size does inspection become cumbersome?
