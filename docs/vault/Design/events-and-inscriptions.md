# Events + the Inscription layer — plan of record

Owns roadmap **item 15 (THE EVENT)** and the `InscriptionDef` extension. Written 2026-07-30 from a
research pass (three axes, sources at the end) plus a measurement of the current build.

**Jake's decision already taken (2026-07-30):** *the Interlude becomes a data-driven event pool.*
No new node types, no added run length.

> ⚠ **This supersedes one line on the board.** Item 15 currently reads *"Spend the budgeted ONE
> event… **Do not create an event catalog**."* A pool is a catalog. That line is retired by the
> decision above; §5 states what replaces it. Flagged rather than silently overridden.

---

## 1. Measured status — what exists today (before any proposal)

| Thing | Measured | Source |
|---|---|---|
| Run shape | `Acts=3`, `NodesPerAct=4` (Fight, Fight, Interlude, Fight) + boss | `RunConfig.cs` |
| **Combats per run** | **9 normal fights + 3 bosses = 12** | `RunSkeletonTests.cs:72` |
| **Event beats per run** | **3** (`EventsPerAct=1` × 3 acts) | `RunConfig.cs:13` |
| Inscriptions authored | **12** | `Catalog.cs:93` |
| Of those, `Paradox` (drawback) | **1** — `bloodless` only | `Catalog.cs` |
| Trinkets authored | **5** — hourglass, quickstone, deepwell, gravemark, martyrsknot | `Catalog.cs` |
| Interlude content | Hardcoded `InterludePath { Treasury, Armory, Hourstone }` | `RunState.cs:58` |
| Run Sand budget | **63** (all Stable, no Treasury) – **105** (all Collapsing + Treasury ×3) | computed |
| Inscription price | 7 Sand (vs weapon 4, trinket 3) — the premium currency | `RunConfig.cs:38` |
| Persistent HP between fights | **None.** Units start every battle full. | `RunState.cs` — no HP field |

**The two measurements that drive everything below.** First: the drawback family is a family of
**one**, so the cost of this work is authoring Paradoxes, not adding a `Duration` field. Second:
a run sees **3** event beats against 12 combats — a 1:4 ratio, essentially identical to Slay the
Spire's 1:4.4. **Event frequency is already correct. Do not add nodes.**

---

## 2. Research findings that change the design

Full findings and URLs in the sources section; mechanisms only here.

**2.1 A hero-scoped modifier layer is the most-cut feature in this genre.** Hearthstone
Battlegrounds' Buddies were added and cut **five times in four years**, then removed permanently.
The devs state the trap as a dilemma with no middle: *"if the buddies were designed too much for a
hero, they could be useless for other players. If the buddies were too generic, they may as well
just be regular minions."* Dota Underlords deleted a 22-node per-hero talent tree **eight weeks**
after shipping it, collapsing it to a binary pick. Monster Train and Underlords independently
arrived at the same rule: **the entity with a tree does not take items.**

**2.2 Drawback-by-RNG is the #1 killer; drawback-by-choice is fine.** Balatro's Perishable/Rental
stickers attach randomly to a reward you wanted, and reception is poor. The disclosed-trade shape —
*"take X, accept Y"* — is the correct side of that line.

**2.3 Balatro's Boss Blinds are the reference implementation for a one-fight rule rewrite.** ~28 of
them, pure data, each announced **by name before the player commits** (`The Flint` halves base Chips
and Mult for the round; `The Manacle` is −1 hand size). Structurally identical to a warband Paradox
scoped to one fight.

**2.4 "N combats" only works if non-combat beats don't tick it.** Slay the Spire 2's Wax Relics melt
every 3 *combats* and explicitly do not melt at shops, chests, events or rest sites — players route
around combat to stretch them. A duration the player can manipulate is a decision; one they can't is
a tax.

**2.5 On-pickup payoffs must survive expiry.** Wax Relics' `On Pickup` effects persist after the
relic melts. This is the rule that stops a timed reward from feeling retroactively stolen.

**2.6 Scope/visibility inversion is the worst documented failure.** Battlegrounds' Anomalies rewrote
the whole lobby's rules but only flashed briefly on screen; a player reported losing ~25% of games
on turn one to not knowing which was active. **The higher a modifier's scope, the more permanent its
on-screen home must be.**

**2.7 Never invent a currency for a layer.** Battlegrounds' Buddies launched with a bespoke "Buddy
Meter" and were re-engineered to a gold-priced button; the one BG layer that kept its own currency
lasted a single season. Warband has Sand.

**2.8 Tag status provenance before shipping a second grant-source.** Monster Train 2 shipped a bug
where *"persistent effects granted by equipment/rooms/enchantments would be stripped off a unit upon
removal of those things even if the unit had those effects previously already."* Four layers could
grant `Quick`; removing one stripped it regardless of source. This is the concrete, non-obvious tax
of a second modifier layer.

**2.9 The catalog-to-exposure ratio is 7–8× across the genre** (StS 46/6.5, MT1 21/~2.5, MT2 27/3.5).
At 3 events per run that implies 24. But the near-term audience plays 3–6 runs, not 300, so the
binding constraint is repeats in the *first few* runs: **12 events = zero in-run repeats and four
clean runs.**

**2.9b The unit to count is BRANCHING events, not events.** Guildrun ships **93** events, but across
its 55 reachable ones the branch distribution is 11 with zero branches, 32 with one, 4 with two, 7
with three — and ~23 are cloned grant-templates. So **~12 of 93 actually pose a decision.** A
"catalog of 24" built from grant-templates is a catalog of about four real choices. Count branches.

**2.10 Two tiers, not one flat list.** StS pairs ~6 recurring utility shrines with ~18 authored
one-shots removed once seen. Without the utility tier, one bad draw wastes a third of the run's
non-combat agency. **Warband already owns the lower tier** — today's Treasury/Armory/Hourstone is
exactly that guaranteed floor.

**2.11 Follow-ups are the biggest multiplier available.** 24% of Monster Train's events resolve N
fights later. With only 3 beats, warband can't afford one-and-done screens. Schedule follow-ups **by
fight count from the trigger**, never by "appears in a later act" — StS's Golden Idol chain orphans
its tail when the chain head never rolls.

**2.12 Two things not to build.** No random-outcome events (StS's Wheel of Change) — with 3 beats, a
beat that doesn't express a build preference throws away a third of the run's agency. No minigames.
One screen, 2–3 options plus a free Leave, ~15 seconds.

**2.13 ⚠ The guaranteed utility floor (§2.10) has a documented failure mode: it becomes a SOLVED
choice.** Hades' three NPC rooms (Sisyphus / Eurydice / Patroclus) each offer 3 fixed options with
no scaling and no run-state input, and players converge on one pick forever: *"Usually I only took
the same thing from Sisyphus, Eurydice and Patroclus"* · *"I take Ambrosia like 99% of the time"* ·
*"ALWAYS pick the event door in Asphodel. It doesn't matter what the other door is."* The rooms
aren't empty — they're **decisionless**. **Mitigation for warband: the floor's values must read run
state** (Sand held, roster size, act), so the correct pick moves. A fixed 5-Sand Treasury against a
fixed 3-offer Armory is exactly the shape that solves itself.

**2.14 Temporary BOONS read as bad value near a run's climax; temporary DRAWBACKS do not.** Hades'
Well of Charon sells mostly 6-encounter buffs, and the standing complaint is *"I don't tend to buy
any of the limited time ones, as I tend to worry about spending gold on items that won't be there to
help against the final boss."* This confirms the polarity Jake chose: **the permanent half of the
trade should be the reward, and the expiring half should be the cost.** A timed reward invites the
player to compute whether it survives to the boss; a timed cost invites them to gamble that they can
outlast it. Only one of those is a decision.

**2.15 A combat-only duration creates a counting problem the player cannot do in their head.** This
is the cost of Law 4, and Hades pays it: Chaos curses last 3–4 *encounters*, non-combat chambers
don't decrement them, and because chambers 13/23/35 are always non-combat, a "3-encounter" curse
taken at chamber 10 is still live at the boss. The wiki carries a hand-written warning paragraph
teaching players to count manually — *a mechanic that needs a community tutorial has failed*.
**Warband's mitigation is mandatory, not optional: the tray shows the remaining COUNT, never the
rule.** Balatro's string is the model — "Debuffed after 5 rounds (3 remaining)".

**2.17 ★ Guildrun has warband's exact constraint and already solved it.** Guildrun's heroes also
**reset to full every fight** — no persistent HP — and it prices events against a **next-combat
debuff**: a 6-second stun, burn for 3% max HP, −50% defense, or *"wait N combats for the safer
reward."* This is the single most transferable finding in the pass: the nearest comparable, with the
identical no-attrition constraint, converged on exactly the currency §5 proposes. It is also
show-don't-tell — the cost is paid **on the board, in a fight**, not in a menu. Pair with §2.15:
count those debuffs in **fights**, never in beats, or the Interlude silently burns them.

**2.17b Guildrun, resolved from datamined event text.** An earlier axis claimed Guildrun ships no
temporary-curse system; a later axis pulled the actual event strings and that claim was wrong.
Guildrun **does** price events against expiring costs, denominated in combats:

| Event | Text | Scope |
|---|---|---|
| The Void Beast | "Gain {name}. Your Heroes start next combat **Stunned for 6 seconds**." | 1 combat |
| Strange Fruits | "For the next 1 combats your Heroes start combat stunned… **After those 1 combats**, your Heroes permanently gain 500 Max HP." | 1 combat |
| The Ringing Fall | "Rank up now at the price of **−20% stats for 2 combats**" | 2 combats |
| The Nest | "Gain {name}. Your Heroes have **−50% Defense next combat**" | 1 combat |
| The Fairy Ring | "accept **1% Poison per combat** for an epic item" | open-ended |

**The real finding is that Guildrun's scopes are BINARY: 1–2 combats, or permanent.** There is no
mid-length decaying modifier anywhere in its event layer — a deliberate omission. And its run is
**two acts, ~12 fights, ~20 minutes, authored acts → clear victory → optional endless**: warband's
exact shape at warband's exact combat count.

**2.17d ★ One clock, running both directions.** Guildrun's buffs and costs share identical grammar:
Campfire and Guild Banner read *"Your Heroes gain +X% basic stats **next combat**"*, and delayed
rewards mirror delayed costs — *"Get the relic after winning {combatcount} combat."* The player
learns "next combat" **once**, and it covers gifts and penalties alike. Darkest Dungeon 2 does the
same by writing the scope into the authored effect string — *"(until next Inn)"*, *"(3 turn)"*,
*"(1 Region)"* — rather than into a separate widget the player must learn to read. **Vary the
effect, never the duration vocabulary.**

**2.17e ★ Immediate legible costs get REFUSED; deferred expiring costs get TAKEN.** Guildrun patch
0.5.2 stripped the up-front Shard penalties from run-start bonuses, dev reason verbatim: *"Bonuses
which immediately cost the player Shards… tended to be picked less often and perform worse than
bonuses which did not cost Shards."* The deferred, combat-scoped **event** costs in the same patch
were left untouched. This is direct evidence for the shape Jake proposed: the cost should land
*later and expire*, not bite at the moment of choice.

**2.17c An alternative worth knowing before committing: Darkest Dungeon's torchlight.** Instead of
discrete timed curses, DD runs a single continuously visible, always-reversible intensity gauge
(low light → far better loot, but heroes surprised more often). It cannot be forgotten because it is
never hidden, and the player can always buy their way back up. It solves §2.15's counting problem by
having nothing to count. Not proposed here — it is a different game's shape — but it is the strongest
alternative to the whole duration approach and should be rejected deliberately rather than by
omission.

**2.18 Never randomize whether a choice pays out — and disclosure beats power.** Monster Train 1→2
and Balatro (demo→launch) independently hit this wall and both replaced probability with a guarantee;
the Balatro wiki records the probabilistic version as *"very unpopular… players had pretty much no
incentive to take them."* The corollary is permissive: **a weak event is fine, an undisclosed one is
not.** Roughly 8 of Balatro's 24 Tags are near-worthless and the system is healthy, because the tag
is shown before commitment. But a guaranteed reward wrapping a *random payload* is not disclosure —
Charm and Meteor share a template and rank S-tier and 22-of-24 respectively.

**2.19 Difficulty should change the DECISION, not the magnitude.** StS shrinks outcomes at Ascension
15+; Guildrun's Red Rift instead **forces you to decline** Campfire and Challenge rewards. Changing
what you may take is a live decision; scaling a number is invisible. This supersedes the naive
"difficulty-keyed magnitudes" approach in §7.H.

**2.20 Two structural traps to avoid.** A commitment beat that doesn't seed the supply pool is a trap
(Backpack Battles: pick Markswoman, then never be offered a ranged weapon) — if an event commits the
player to a direction, the offer pool must honour it. And **a branch convertible into a competing
branch's currency solves itself within a week** (Guildrun offered 15 Shards against an Epic that sold
for 16; patched to 20).

**2.16 Warband lacks the opportunity cost that balances opt-in events elsewhere.** Hades' Erebus
gates are balanced by consuming a chamber — *"if it was an extra room, you would go into it 100% of
the time."* Warband's event beat is mandatory and replaces nothing, so there is no forgone reward to
price against. That is fine (it's a reward beat, not a gamble beat) but it means **the cost side of
every event trade must be internal to the event** — there is no "you gave up a fight reward" to lean on.

---

## 3. Laws

1. **Events live in the existing Interlude beat.** `NodesPerAct` and `InterludeNodeIndex` are
   untouched. The event system is a payload swap, not a structural change.
2. **Two tiers, and the floor must read run state.** The current Treasury/Armory/Hourstone stays as
   the guaranteed utility floor; authored events sit on top of the same beat, so a run always has a
   boring-but-useful option. **But a fixed-value floor solves itself** (§2.13) — its numbers scale
   with act, Sand held and roster size, or it becomes the same pick every run.
3. **Every drawback is a disclosed choice at a decision point.** Never RNG-attached, never a
   surprise. The trade is named before commitment (Boss Blind rule).
4. **Duration ticks on COMBATS only.** Interludes, shops and planning do not tick it. **The unit is
   `{ Run, ThisFight, ThreeFights }` — not "N acts."** ⚠ Consider dropping `ThreeFights` on first
   ship: Guildrun, at warband's exact run length, deliberately runs a **binary** scope — 1–2 combats
   or permanent — with no mid-length band anywhere in its event layer (§2.17b). Start binary; add
   the middle only if play demands it. With 12 combats total, "one act" is ~4 combats
   and degenerates at the edges: a one-act cost taken late in act 3 expires into nothing, while the
   same row taken at act 2 is effectively permanent. Same-named duration, wildly different price.
   Hash the enum into `ContentVersion` exactly as `Paradox` already is.
4b. **No duration slider — offer parallel rows at different durations.** Nobody in the genre ships a
   player-dialled duration, because a free slider collapses to a solved optimum. Games dial
   *intensity* (Heat, Covenant, Ascension) or *scope*. The nearest real thing is Hades' Chaos gate
   pairing curse duration against boon magnitude as a fixed offer.
5. **Anything already granted survives expiry.** A Paradox that ends does not claw back what it paid.
6. **A live drawback has a permanent on-screen home showing the REMAINING COUNT** — the existing
   Inscription tray, not a hover, and the number not the rule (§2.15). Scope and screen-presence must
   not invert.
6c. **A tray badge is necessary but NOT sufficient: the rewrite must render AT THE POINT OF EFFECT.**
   The best-executed timed mechanics in the genre never rely on a separate counter — Balatro's
   Popcorn *is* its own countdown, RoR2 promoted Curse to an engine stat so lost HP draws as a hollow
   shattered segment of the bar in place, Balatro stamps a red X on the debuffed card, and Dead Cells'
   shipped fix for an illegible global modifier was to pop the icon for the specific rule that just
   fired. Concretely for warband: a heal→shield Paradox must render as a **conversion at each unit,
   six times for six heals** — never one rail badge and a silent board. This is Jake's standing law
   ("six damage instances spawn six numbers, not `84 ×6`") applied to rule rewrites.
6d. **Three Paradox classes are banned outright: information-removing, agency-removing, and
   time-wasting.** They are the genre's most-hated drawbacks. Monster Train prices "previews
   disabled" at −8 Boon Value and patched a full-round daze out entirely; Hearthstone's Anomalies
   (§2.6) are the same lesson. Isaac is the proof at scale: **every disliked curse subtracts
   information** — map, health readout, item identity, light — summarised by a player as *"someone's
   putting their hand over part of your screen"*, and a NO CURSES mod has 103k subscribers. Its two
   most-hated (Lost, Maze) are resented specifically for **making runs longer rather than harder** —
   both complaint threads name minutes, not lethality. **Subtracting time is worse than subtracting
   power.** A Paradox may change what the rules DO; never what the player can SEE, CHOOSE, or how
   long the run TAKES.
6e. **The only well-liked curse in the genre is the one that TRADES.** Isaac's Labyrinth gives two
   treasure rooms and takes two bosses, and players actively want it. Pair every drawback with its
   upside **on the same card** — which is already the shape of Jake's "take this weapon, but…".
6f. **Show the BIG price, not just the small one.** Isaac's worst pattern: the pedestal displays
   "2 hearts" and never mentions that taking a Devil Deal locks Angel Rooms out **for the entire
   run**. If a trade has a scoped cost and an unscoped consequence, the unscoped one belongs on the
   card.
6g. **The tray must never occlude the board.** Guildrun shipped its Event Effects panel large enough
   to block the fight and patched it to be *collapsible* rather than smaller — inheriting the
   problem. Warband's tray is already persistent; keep it out of the play area by construction.
6b. **The permanent half of a trade is the reward; the expiring half is the cost** (§2.14). Never
   sell a timed boon — it invites arithmetic, not a gamble.
7. **Everything prices in Sand.** No event currency, no meter, no bespoke resource.
8. **Statuses carry a source tag** before any second grant-source ships.
9. **Event selection is a pure function of (seed, act, beat index, run state).** No mutable "seen"
   list outside the snapshot — the replay invariant depends on it.
10. **The environment of the decision is one screen, 2–3 options, always a free Leave.**
11. **No hero-scoped inscriptions.** See §4.

---

## 4. Hero-scoped inscriptions — recommended NO, on evidence

Jake raised this as an open question ("some kind of hero level inscription maybe as well?"). The
research answer is unusually one-sided, and it is the genre's single most-repeated failure (§2.1).

The specific risk for warband: trinkets are already a per-unit modifier (5 authored). A hero-scoped
inscription would be a **second** per-unit layer, added before the twelve team inscriptions have
been proven legible in play — which is exactly the condition the Deferred list guards. Underlords
shipped precisely this blur (hat items rewrote a unit's Alliance membership, so you could no longer
read your team totals off the board) and deleted the layer.

**If it is ever revisited**, the rule that keeps it distinct is attachment scope, not fiction:
a trinket is an *item* (bought, sold, occupies a slot); an inscription is a *law* (no slot, cannot
be sold). Monster Train 2's Souls are the working model — one per card, reassignable out of combat
only, and **must always be attached to something** so there is no inventory to diff.

---

## 4b. ⚠ Adjacent risk this pass surfaced: the spec tree is static in a randomised game

Not item 15, but found while researching §4 and too specific to lose.

**Measured:** all **39** live `Offer()` rows hold exactly **2** entries, and `SpecChoices = 2`, so
`SpecPick` returns the whole pool every time. Today's spec "tree" is **perfect-information and
identical in every run**, while the market it sits inside is randomised.

**Why that pattern is dangerous.** It is exactly what killed Dota Underlords' talent trees. The
community diagnosis Valve then acted on: *"static talent trees have no place in a drafting game…
with no randomness and perfect information, it feels like there's basically one 'best' build… even
if the talents were somehow perfectly balanced, then it basically wouldn't even matter which one you
picked."* The predicted homogenisation landed within a week — *"every single hobgen take the same
talents."* Valve cut the 22-talent tree **8 weeks** after shipping it, and the over-correction
(2 randomised presets) was reviewed as *"You pick one, then go back to thinking about the same old
stuff"* — **simplifying a layer to make it legible can simplify it into irrelevance.** Warband
currently sits at that over-corrected end already.

**Why it may not bite warband, honestly:** this is PvE, so nobody is facing eight copies of the
solved build; hero *choice* already varies (8 chassis, draft 3); and ADR 0016's north star is
*system-breaking builds*, which arguably wants a knowable tree so combos can be planned.

**The cheap insurance, if it ever bites:** the draft machinery already exists and is dormant only
because pools are size 2. `SpecPick` already draws `SpecChoices` from a larger pool as a seeded pure
function of (seed, hero, rank). **Growing a row from 2 entries to 4 turns that rank-up into a real
draft with zero code.** So this is a content decision, correctly gated behind playtest #1 — not
something to act on now. Logged to the design backlog.

## 5. What replaces "do not create an event catalog"

**12 BRANCHING events at the playtest gate, growing to 24** (§2.9b — count branches, not rows;
grant-templates don't count). Because events are removed from the pool once seen, 12 gives zero
in-run repeats and four clean runs — enough for friends playtest #1. Split ~4/4/4 by act window with
overlap. Events 13–24 are `tuning.json` rows, not code.

**The primary downside currency is a next-combat debuff, per §2.17** — Guildrun's solved answer to
warband's own no-persistent-HP constraint, and the one that pays the cost *on the board* rather than
in a menu.

Event currency mix, ported from Monster Train's *build* economy rather than StS's *HP* economy
(warband has no HP to spend): roughly **55% roster/unit · 40% permanent modifiers · 30% Sand ·
15–25% negative Inscription as the downside currency · 10% touching the next fight directly.**
Percentages overlap; an event usually touches two.

---

## 6. Extension table

| New content of kind… | Inherits free | Must be authored |
|---|---|---|
| **Event** (13th onward) | Beat placement, act windowing, weighting, eligibility, Leave option, determinism, save/replay | One `tuning.json` row: id, actWindow, weight, eligibility, 2–3 options + outcomes |
| **Temporary Paradox** | Duration ticking, expiry, tray countdown, survives-expiry rule, save | The rule rewrite itself + its `ContentLexicon` line |
| **Permanent Inscription** | Everything it has today | Unchanged from today |
| **Follow-up payoff** | Scheduling by fight count, persistence, save | The payoff row + its copy |
| **Difficulty scaling** | Outcome magnitudes keyed by difficulty | Nothing — a tuning row |

---

## 7. Jake's decisions

Lettered, cost-tagged. See the chat message accompanying this doc for the recommendation.

- **A.** `Duration` enum `{ Run, ThisFight, ThreeFights }` on `InscriptionDef`, hashed into
  `ContentVersion`, + tray countdown + **at-the-unit rendering** (Law 6c) — *cheap for the field,
  a day for the rendering; the rendering is the part that must not be skipped*
- **B.** Author 4–5 temporary Paradoxes (the drawback family, currently 1) — *a day*
- **C.** Event pool schema + two-tier floor, Interlude becomes the payload — *a day*
- **D.** `followupAfterFights` field, scheduled by fight count — *cheap, only if done with C*
- **E.** Author 12 events — *a week*
- **F.** Status source-tagging (prerequisite for any second grant-source) — *cheap*
- **G.** `Scope` (hero-scoped inscriptions) — **recommended against**, see §4
- **H.** Difficulty scaling that **removes options rather than shrinking numbers** (§2.19) — *cheap, later*
- **I.** Next-combat debuff as a downside currency (§2.17) — *a day; the highest-value single addition
  after the schema, and the only one that pays the cost on the board instead of in a menu*

---

## Sources

Research findings with full URLs: `tmp/research/{event-structure,temporary-modifiers,modifier-scope}-key-findings.md`
(scratchpad, gitignored). Primary sources cited there include Balatro wiki (Blinds/Stickers/Negative
Effects), Slay the Spire wiki + Mega Crit patch notes (Ascension 11 slot change, relic overflow
paging), Slay the Spire 2 Wax Relics, Monster Train / MT2 wikis + Shiny Shoe patch notes and the
GamingBolt interview, Hearthstone Battlegrounds patch notes + dev Q&A recaps (Buddies, Trinkets,
Anomalies), Dota Underlords Steam patch notes (Jull-tide changelog, Ace removal), Super Auto Pets
wiki (perks/ailments/toys), Backpack Battles wiki (skills).
