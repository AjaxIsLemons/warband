# PvE encounter authoring — v0.1

**Status:** partial source of truth; foundational laws settled with Jake on 2026-07-23–24.
ADR 0016 owns the PvE-first identity and balance law. This page turns that identity into
encounter-authoring guidance as the vertical slice is designed.

## The encounter is the boss

A boss is an **encounter-level mechanic**, not a privileged unit chassis. A boss encounter
may be:

- a swarm or multiplying ecology;
- a formation whose members create the threat together;
- a ritual, survival clock, or painted-board crisis;
- a spawning source and what it produces;
- a global rule package;
- a singular creature; or
- a combination of these.

No anchor unit is required. If one exists, it serves the encounter's core mechanic rather
than defining what makes the fight a boss.

## A boss is a strength exam

Every boss encounter has one defining pressure that an ordinary warband cannot comfortably
absorb. Victory requires the player to demonstrate **exceptional strength in at least one
relevant axis**, but never one mandatory class, item, or keyword.

The defining question may be:

- Can the warband destroy enemies faster than the swarm multiplies?
- Can it survive or outrun an accelerating Clock?
- Can it reach several protected threats before their casts resolve?
- Can it preserve enough usable ground as the Field turns hostile?
- Can it withstand repeated synchronized attacks?
- Can it dismantle a linked formation without the survivors becoming overwhelming?

Each question must admit multiple qualitatively different strong answers. A swarm might be
beaten by overwhelming area damage, kill-chain tempo, field control, Clock denial, extreme
sustain, destruction of its source, or a sufficiently powerful general engine. The encounter
demands power and adaptation; it does not prescribe the player's build.

When a spectacular engine trivializes a boss through a legitimate answer, that is a payoff,
not automatically a balance failure. ADR 0016's balance law still applies: intervene only
when one engine erases discovery or every encounter problem, or breaks determinism,
resolution, or readability.

## The boss rules the act

The act boss is revealed at the beginning of its act. The player sees its identity, its core
pressure, and a plain-language description of the rule that makes it dangerous. The boss is
an act-long build target, not a surprise knowledge check.

Normal encounters are warm-ups for that exam. They introduce, isolate, combine, and escalate
pieces of the boss's pressure:

- early encounters teach one piece cleanly;
- middle encounters combine that piece with another familiar problem;
- late encounters stress the player's emerging answer;
- the boss recombines the act's lessons into the defining strength exam.

These encounters need not be miniature copies of the boss. They remain distinct placement
and build problems, but the act should develop a mechanical through-line. Reaching the boss
should feel like facing the culmination of what the player has been learning and preparing
for.

## Formation is public information

Before every PvE fight, the enemy formation is visible. The player builds and places their
warband against the actual enemy deployment; formation is not hidden difficulty.

For bosses, information is layered:

1. **Act start:** identity and core pressure.
2. **Act encounters:** playable demonstrations of the pressure's component parts.
3. **Boss preview:** the actual boss form and formation.
4. **Deployment:** the player arranges their warband against what they saw.

The law below defines what must be available before Play. How densely the default board
presents that information remains a playtest question.

## Know the rules, not the result

There are **no surprise mechanics after the player presses Play**. Before locking deployment,
the player can inspect every rule that may materially affect the fight:

- enemy roles, formation, and relevant combat stats;
- attacks, signatures, passives, triggers, and targeting rules;
- encounter-wide rules and phase changes;
- immunities, resistances, and special status interactions;
- reinforcements, transformations, or other scheduled pressures; and
- the mechanical changes made by the selected risk tier.

A phase transition may still be a visual spectacle, but its rule is not a gotcha. Likewise,
an enemy may be unfamiliar, but its relevant behavior is inspectable on first contact rather
than learned only by losing to it.

Full information does not mean a solved fight. The preview does not provide win odds, a
future event timeline, or a simulated outcome. Once deployment is locked, the player watches
their deterministic systems collide; targeting changes, movement, fields, triggers, deaths,
and compounding engines make the result uncertain to a human even though the simulation is
reproducible.

The UI should use progressive disclosure:

- the board presents a concise role and threat summary by default;
- inspecting a unit or encounter rule exposes the complete decision-relevant details;
- visual overlays may explain range, shapes, and opening targeting without forecasting the
  fight.

Exactly how many numbers appear on the default surface, and whether opening-target overlays
are useful, are playtest questions. **Mechanical disclosure is not.**

## Reveal, plan, play

Every PvE fight follows one planning commitment:

1. **Encounter Reveal:** introduce the encounter rule, enemy units, and exact enemy formation.
   This is information, not a locked screen; dismissing the brief enters the same battlefield
   workspace.
2. **Planning:** freely change the active lineup, bench, owned weapons and trinkets, and
   formation in any order while the enemy remains visible and inspectable. Any available
   shop, forge, or paid respec service may open contextually without hiding why the player is
   making the change.
3. **Play:** `BEGIN FIGHT` validates and locks the complete lineup, loadouts, and positions,
   then deterministic combat begins.
4. **Result:** present the outcome and return any permitted retry or continuation to Planning
   with the last committed draft restored.

Lineup, loadout, and formation are not separate commitments. They are interdependent answers
to the same encounter problem: moving a hero may reveal that their weapon is wrong, and a new
weapon may demand another formation change. Contextual selection and valid-target highlights
keep those edits readable without a Prepare/Deploy gate.

Positioning, active/bench selection, and re-equipping owned gear cost no currency. Buying,
selling, forging, and respec use their run-layer services and costs. Rank and specialization
choices are sticky by default; respec, if offered, belongs to Planning through an explicit
service rather than being a free deployment action.

The first playable may expose free respec as clearly labeled testing scaffolding. That does
not change the intended commitment law.

The accepted scalable interaction expression—last-formation persistence, a data-driven
reserve bench initially tuned to two slots and presented through a collapsible Muster Drawer,
simultaneous roster/loadout/formation edits, atomic field/bench swaps, click-and-drag parity,
undo, validation, and implementation seams—lives in `Design/preparation-and-deployment.md`.

## Bosses obey the shared combat rules

Being part of a boss encounter grants no universal immunity or hidden rules. Stun, Silence,
Taunt, Disarm, Root, Burn, and other shared combat verbs affect boss units normally.

Encounter composition is the first defense against one control effect solving an entire
boss: the threat may be distributed across a swarm, formation, global rule, or several
interdependent units. If a player legitimately assembles a control engine that humiliates
the encounter, that may be the intended system-breaking payoff.

An authored enemy may have an inspectable passive that negates or reduces a particular form
of control when that resistance is part of its role or the encounter's defining pressure.
This is a specific content property, not a generic **Boss** tag. The passive must:

- name the affected verb and its exact behavior;
- be disclosed before deployment;
- leave multiple strong answers to the encounter; and
- earn its complexity by making the placement or build problem more interesting.

If one control engine trivializes every encounter, first diversify encounter pressure or
inspect the engine itself. Do not solve the problem by quietly making bosses immune.

## Execute is a real kill

Execute works on boss units under the same rules as any other unit. It is not converted into
bonus damage merely because the target belongs to a boss encounter.

Execute still produces the target's normal death and therefore cannot bypass consequences
attached to that death. It may:

- finish a high-health boss unit at its threshold;
- remove one component of a distributed boss encounter; or
- end the current body of a transforming enemy.

It does not suppress disclosed death triggers, transformations, replacement bodies, or
encounter-state changes. If a creature becomes a second form when it dies, Execute reaches
that second form sooner rather than skipping it.

Specific content may establish an inspectable protection such as **Cannot die while either
Ward remains**. That rule applies to all lethal effects and belongs to the encounter's
authored problem; it is not hidden Execute immunity. Removing a dramatic final portion of a
boss unit's health is otherwise the Reaper engine fulfilling its promise.

## Phase removes the unit, not the encounter

The **Phase** status means a unit is temporarily absent from the battlefield:

- it cannot be targeted or damaged;
- glyphs and other unit-affecting effects do not affect it;
- attackers immediately acquire another valid target; and
- enemy units may use Phase under the same rules.

Phase is not stopped time. Existing personal durations continue ticking, and overtime,
rituals, spawning, transformations, field changes, and other encounter-state progression
continue normally. A phased unit may avoid the immediate consequence of an event, but it
does not pause, reset, or defer the encounter's governing pressure.

When no valid target exists because every surviving unit is phased, opponents may briefly
have nothing to attack while the encounter continues to advance. Phase durations remain
bounded, so this is an earned reprieve rather than a resolution escape.

Use **stage** or **form** for boss transitions; **Phase** refers only to the combat status.
An exceptional enemy passive may explicitly interact with phased units, but ordinary Phase
promises complete personal safety.

## Fields are factional by default

Field allegiance is authored per glyph:

- harmful hero glyphs affect enemies by default;
- beneficial hero glyphs affect allies by default;
- enemy glyphs follow the same rule from their creator's perspective; and
- environmental hazards and explicitly **volatile** glyphs may affect everyone.

Automatic movement should not make an ordinary Pyromancer burn their own warband or make
enemy field-casters sabotage their allies. Symmetric danger remains available when it creates
a deliberate encounter or build problem rather than being the baseline tax on field use.

Every field's allegiance must be visually unmistakable and inspectable before combat. An
all-unit field is an authored exception with explicit rules, not a surprise friendly-fire
mode.

## First authored proof — a bonded pair

Do not design an act-sized enemy roster before proving that one authored relationship is fun.
The first encounter seed is two enemies with a visible **Bond**:

> When either bonded unit dies, the survivor Enrages.

The preview identifies the pair and explains Enrage. In combat, the death should produce an
unmistakable transfer tell before the survivor changes behavior.

This tiny encounter is enough to test whether enemy relationships create useful preparation
and placement decisions:

- Can the player influence which bonded unit dies first?
- Does spreading or concentrating damage feel meaningfully different?
- Is the survivor's pop-off readable?
- Can different builds answer through burst, control, sustain, or raw power?

Exact Enrage stats are tuning values. Start with the smallest readable expression—likely
Haste, with Attack or Mana added only if Haste alone does not create the promised moment.

### Playable proof status — 2026-07-24

The first test expression is live as **The Last Oath**:

- Oathbound Bulwark in front and Oathbound Sharpshot behind; both positions and roles are
  visible before deployment.
- Bond grants the survivor permanent **+100% Attack Speed**. This is deliberately strong,
  simple, and provisional—not a balance commitment.
- The death and Haste application share a replay tick; a gold/orange burst, body flash, and
  persistent Haste pip make the survivor's change visible.
- The proof party is Bulwark, Pyromancer, and Sharpshot. The player may reposition them only
  within the three blue deployment rows, then locks the lineup for deterministic autobattle.
- The prototype omits the full Planning workspace because there is not yet any run inventory
  to reconfigure. The full-run law is Encounter Reveal → Planning → Play → Result.

The immediate playtest question is not “is +100% the right number?” It is whether choosing which
partner survives creates a legible placement/build problem at all. Tune or deepen Enrage only
after answering that.

An anchorless linked swarm such as the proposed **Dying Procession** is a possible later
extrapolation of this bond. It is not committed first-slice content. Earn that scope by
playing the pair first.

## The shipped role grammar (2026-07-25, ADR 0023)

The "minimum enemy-role grammar" open question below is now answered in code
(`sim/Warband.Content/Enemies.cs`). Enemies are **authored UnitDefs, not composed hero kits**: a
monster has no chassis, rank, weapon or spec tree, just a stat block, a role behavior, and at most
one disclosed rule.

| Role | Unit | Behavior that defines it | Poses |
|---|---|---|---|
| Swarm | Hourling | fastest body on the board, no signature | more bodies than you have answers |
| Anchor | Ashen Colossus | slowest body on the board | a wall you cannot burst |
| Artillery | Sanddrift Gunner | acquires **farthest**, holds standoff | it shoots past your front line |
| Ritualist | Hour-Scribe | rooted, never attacks, time-fed mana | a clock that beats you if ignored |
| Diver | Gloamstalker | opening Leap, acquires **farthest** | it is already in your backline |

Four node encounters compose them: **The Gnawing Hour** (SWARM), **The Long Range** (WARD, act 2+),
**The Ninth Bell** (RITUAL), **The Drop** (AMBUSH).

**Composition is the act's lever, stats are secondary** (ADR 0016). Factories size themselves by act
— the Gnawing Hour is 5 bodies at act 1 and 10 at act 3, the Ninth Bell teaches its ritual before
putting a wall in front of it — and an act's pool is its identity.

### Measure encounters, do not guess them

`make -C . ` → `dotnet run --project sim/Warband.Sweep -- --enc` reports, per encounter per act:
win% for the best and worst formation, **the spread between them**, whether the authored rule
actually fired, and how the naive bot line fares across whole runs.

**The bar is the spread, not the win rate.** An encounter every formation wins is free; an
encounter where where-you-stood changed the answer is doing its job. The probe's first run caught
three of four encounters posing nothing and the Ninth Bell's ritual never firing at all — its
countdown was longer than a fight lasts. Author against it.

## The shipped act bosses (2026-07-26, ADR 0024)

The "first-slice boss mechanic" open question below is now answered in code. `Catalog.Boss` no
longer returns the act-scaled Last Oath for every act; each act closes on its own exam.

| act | boss | pressure | what it examines |
|---|---|---|---|
| 1 | **The Last Oath** (`BOND`) | which threat you leave enraged | how you distribute damage |
| 2 | **The Ashfall Battery** (`BATTERY`) | reach the gun behind the wall | reach, and whether you can afford to stand together |
| 3 | **The Waning Crown** (`WANING`) | every death in its court rings the bell | whether you can stop clearing and commit |

Act 1 keeps the bonded pair on purpose: it is the only boss whose decision has been measured
(`Projects/oath-probe-2026-07-25.md`), and the law above says to earn further bonded scope by
playing the pair first.

Both new bosses are Rooted, never attack, and run a clock the player reads off a mana bar — the
Hour-Scribe's grammar, examined rather than taught. Both keep the four disclosed answers
(out-damage · reach · Silence · Stun), and Silence genuinely stops the clock because mana gain is
gated on it.

### Measure bosses against a harder bar than node encounters

`dotnet run --project sim/Warband.Sweep -- --boss` adds one question `--enc` does not ask: **how
many kinds of strength can pass this?** It re-runs each boss against four different parties
(balanced / reach / control / damage). A boss only one axis clears is prescribing a build, which
this page forbids; a boss none clear is a stat wall. Report:
`Projects/boss-probe-2026-07-26.md`.

The probe earned its keep immediately: the act-2 boss as first authored posed **nothing** — three of
four axes cleared it 100% from every formation, because at a 14-second bell the gun fired roughly
once before the wall fell. The bell and the shell were re-authored against the measurement.

## Authoring test

Before an encounter earns the label **boss**, its design must answer:

1. What is the one-sentence pressure?
2. Why is an ordinary warband unable to absorb it comfortably?
3. What distinct forms of exceptional strength can overcome it?
4. Which current hero paths get meaningful work to do?
5. Does the encounter pressure weak builds, or merely switch off a legal build?
6. Will a successful engine's answer be visually legible in automatic combat?

## Still open

- How narrow a demanded strength may be.
- Default preview density and whether to show opening-target overlays.
- Special boss-state interactions not covered above.
- Bonded-pair composition and minimum readable Enrage.
- ~~First-slice **boss** mechanic.~~ **ANSWERED 2026-07-26 (ADR 0024)** — three act bosses, see above.
- ~~**Client disclosure UI.**~~ **BUILT 2026-07-26 (ADR 0024).** Every fight now discloses its name,
  pressure and rule from `PreviewBrief`, and each previewed body carries its role, its real
  post-scaling numbers and a behavior sentence covering its targeting rule. Still open: a *deep*
  enemy inspector (full signature/passive text the way Muster cards do for heroes), and bespoke
  enemy art — cards currently show initials, because the chassis portrait is a named champion's
  face and a hero's face on a monster is the same lie the card titles used to tell.
- Muster Drawer discoverability, initial two-reserve capacity, and combined-Planning clarity.
- Which encounter forms justify new simulation machinery such as non-wall spawning.
