# The Sauce — class/unit identity noodling (WORKING DOC, nothing decided)

Status: **PARKED** (2026-07-22, end of session). Round 1 (three one-pagers) and round 2
(card-stack synthesis) both came back cold-to-lukewarm. Jake's call: pause the sauce hunt —
build a solid hero/class game first. **The bar that replaces it: every class can be
brought, feels different run to run, and can wear different hats.** If sauce re-emerges,
it'll be from the hero deep dives, not from a hunt. Ideas below preserved for that day.

---

## ROUND 2 — the synthesis: card-stack warbands

**Jake's round-1 reactions:** doctrine best-of-three but "no commitment" (class = vehicle,
gems slotted in; needs a better name) · elite specs re-aimed: branching FINAL/EVOLVED forms
of a class reached by climbing the tree (Warrior → tank line vs bruiser line) · fusion
should be at the FOREFRONT — fuse any time, "almost more like a deck builder" · keep trying
for cool. **Read: transformation/combination as core verbs, not rare events.**

**The pitch:** every unit is a STACK of cards; every card is a component.
- **Class cards** = vehicle (stats, weapon proficiency, signature verb). Play one → a unit.
- **Fusion = core verb, any time:** drop class card onto class card — stats blend, secondary
  verb becomes an echo-rider on the primary's cast (today's content atom expresses this).
- **Evolution = the tree:** each class has branching **elite lines** (Warrior → Juggernaut
  vs Berserker); dupe-ranks are the leveling that unlocks the branch. Promotion moment, not
  stat drip.
- **The combo engine:** fused stacks unlock evolutions neither component has alone
  (Warrior+Cleric → *Paladin*). Recipe discovery = the collection meta. Genre white space.
- **Essences (the "doctrine" rename + better source):** slot-cards are carved from KILLS —
  monster essences drop from wager fights, tier-gated (Greedy drops the rare ones, per ADR
  0007's tier-scaled drops). Wager choice becomes a build choice; "what you kill becomes
  part of you."

**Law check:** dupes→ranks survive (gate evolutions) · fork-transforms law survives
(evolution IS the transform) · one-active survives (echo = cast rider) · budget: 16
evolutions + ~6 fusion-exclusive recipes + ~10 essences. Main rework: hero/item economy
reframed as class-card/essence economy (weapons/trinkets remain as gear).

**Fallback:** evolution-first alone (branching promotions, no stacking) — solid but drops
the fuse-any-time ask.

---

## Proposal 1 — Doctrine slots (chassis × calling)

**Concept.** Split hero identity: **chassis** = the body (stats, weapon proficiency, innate
passive, and the signature *verb* — "Smite: single-target burst"); **doctrine** = the calling
(the spec tree the hero climbs). Doctrines are **cards bought in shops and socketed** like
weapons. Rank-ups (dupes, unchanged) gate tree depth — B unlocks the socketed doctrine's
tier 1, A tier 2, S tier 3.

**Why it's sauce.** 8 chassis × ~6 doctrines = **48 class identities from 14 designed
pieces**. "I built a Bruiser-Chronomancer" is a sentence no other autobattler produces.
TFT's item-components insight applied to class itself.

**The signature answer** (the tension flagged in chat): doctrine nodes are **support-gem
transformations of the chassis verb** (the PoE model heroes.md already cites). Chassis keeps
the verb, doctrine reshapes delivery/riders — preserves the settled "fork transforms, never
replaces" law, and keeps all 48 pairings coherent by construction.

**Run feel.** Doctrine cards roll in shop item slots (like banners). A hero with no doctrine
climbs a generic "Veteran" line. **Respec = re-socketing a different doctrine** (keep rank,
re-pick the new tree's choices) — answers the open respec question with a physical verb.

**Cost & fit.** Content: 8 chassis kits + 6 doctrines × 3 tiers × 2 choices = 36 nodes —
comparable to the current budget, multiplicative payoff. Systems: SpecOptions becomes
doctrine-keyed instead of chassis-keyed (run-layer parametric change); heroes.md §3 and ADR
0005 shapes need a rework pass.

**Opens.** Doctrine pricing/rarity · chassis restrictions (lean NO — restrictions kill the
combinatorial promise; balance via magnitudes) · does the default Veteran line exist or is
socketing mandatory before B?

---

## Proposal 2 — Elite specializations (the chase crown)

**Concept.** A rare **elite card** (working name; GW2's "elite spec" is the model) appearing
in shops from **act 3+**, socketable on a hero at **rank A+**, one per hero per run. It sits
*above* the path: a second signature transformation **plus the one thing normal nodes never
do — change the hero's physics**: weapon-category unlock, range identity shift, or a
movement keyword (Leap, Charge). Prestige rename included (War-Priest → High Exarch).

**Why it's sauce.** Late-run shops currently flatten once ranks cap; this is the chase card
that keeps act 4–5 shop ticks hot. It also crowns the deep strategy: going tall earns a
transformation wide can't touch.

**Run feel.** Rare roll (banner-like, lower odds; placeholder pity: one guaranteed offer at
act-4 close). Seeing the elite for YOUR build is a heart-rate moment; seeing one for a hero
you're not deep on is a pivot temptation.

**Cost & fit.** Buildable **today**: an elite is a SpecNode with SignatureOverride +
triggers; keyword grants already exist in the anatomy; weapon-category unlock is a run-layer
equip rule. Content: 8–16 designs standalone — or **6 if combined with doctrines** (elite =
each doctrine's tier-4 crown, chassis-agnostic).

**Opens.** Gate at A or S? · stacking readability (path transform + elite transform) ·
standalone vs doctrine-crown form.

---

## Proposal 3 — Hero fusion / grafting (the sacrifice)

**Concept.** A rare **Fusion Rite** card (act 2+): consume hero B to **graft** it onto hero
A. A gains B's signature as an **echo** — implemented as an on-cast rider (when A casts, B's
effect fires at ~50% magnitude), plus B's innate passive at reduced strength. Knight grafted
with Cleric = your Paladin. One graft per hero.

**Why it's sauce.** No autobattler fuses distinct units into hybrid classes. And the
decisions are stories: *"I S-ranked my Cleric just to feed her to the Knight."* Consumed
rank feeds echo strength, so fodder-deepening is a real (wild) line. The bench becomes a
ritual chamber.

**Rule-based, not bespoke.** Echo = one rule (X% magnitude rider on cast), so 28+ pairs cost
~zero bespoke content. Sim needs nothing new: the echo is a `Trigger{On: Cast, When:
SourceIsOwner}` — expressible in today's content atom. Snapshot grows a GraftedChassisId;
grafts travel with ghosts.

**Honest risks.** Readability (what IS that unit — needs a strong graft sigil + B's VFX on
echoes) · balance surface is the full pair matrix × ranks · shop incentives distort (buying
heroes as pure fodder — depth or degenerate? unanswerable without play) · brushes the
"heroes are sticky" law: consuming is opt-in sacrifice, but it's permanent.

---

## The full stack (observation, not a commitment)

The three compose into one class system with a distinct acquisition verb per axis:
**chassis** (drafted) × **doctrine** (socketed) × **graft** (sacrificed) × **elite**
(chased). If all three land, design order matters: doctrines are the foundation, elites the
chase layer, fusion the spice. Each is independently cuttable.

## Parked (other sauce axes, revisit after class identity settles)

Board-memory (run-persistent battlefield, ghost ground travels) · time-made-literal (clock
theft, cast banking, dilation glyphs) · bonds (adjacency camaraderie → paired passives —
composes with everything above) · weapons-with-history (previews the weapons pass) ·
spec-card crafting (rejected for now: bulldozes the settled dupe→rank→fork flow).
