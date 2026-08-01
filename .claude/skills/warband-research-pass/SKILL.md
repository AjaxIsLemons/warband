---
name: warband-research-pass
description: Run a warband research→evaluate→recommend pass — "research X", "how do other games do X", "deep dive on X", "evaluate what we have against Y", "root it in research", "where should we head", or any DESIGN-stage roadmap item.
---

Jake's most-repeated ask: **research X → measure what we have against it → tell me where to head,
as options I can pick.** Verbatim: *"Deep research here, on what we have, and where we should
head. I want headline suggestions to delve into."* Six steps; skipping 2 or 5 fails the pass.

## 1. Name the comparables, cite mechanisms
TFT and Guildrun are our closest. Also Balatro, Slay the Spire, Monster Train, Super Auto Pets,
Hearthstone Battlegrounds, Underlords, Backpack Battles, Hades, CK3, Factorio, Riot's VFX/audio
guidelines, NN/g. Ground each finding in a **mechanism**, never a vibe — "Balatro activates each
contributing element sequentially with its own callout", not "Balatro feels readable". Include
negative cases; they carry the most weight: "Super Auto Pets proves sequencing visuals does NOT
make an ordering rule legible."

## 2. Measure the current state BEFORE proposing
A proposal built on an assumed current state dies to one measurement. Precedents: 35 audio clips
measured for onset/length/peak/crest instead of trusted by ear, which found the worst combat
density was `overtime` at 9.6 onsets/s and **not** the assumed `castfest`; a `make coverage`
census of what the sim actually emits found two whole mechanic classes emitting nothing ever
(`Design/sim-render-audit.md` §1.3); camera position confirmed against the engine to the centimetre
before any framing proposal. Reach for whatever measures the axis — `make test`, `make coverage`,
`make enc`, `make boss`, `make baseline`, `make sfx-density`, headless replay folds, captures.

## 3. Fan out when the axes are independent
Jake: *"Encourage you to use opus agents where you think it will help - but go big here"* ·
*"Defer to opus agents and check their work is the best way."* One agent per research axis, each
writing `<scratchpad>/research/<axis>-key-findings.md`; you review the findings rather than
relaying them. Fan-out mechanics: `.claude/skills/warband-subagent-orchestration/SKILL.md`.

## 4. One deliverable file
`docs/vault/Design/<topic>.md` as a plan-of-record: numbered sections, measured status before
proposal, an explicit "Jake's decisions" section, `## Sources`. Register it in
`docs/vault/index.md`; promote it onto `docs/vault/Projects/roadmap.md` as a numbered item at the
right stage. Models: `Design/audio.md` (§1 status → §4 research → §5 plan → §7 build order → §8
decisions), `Design/sim-render-audit.md`, `Design/passive-legibility.md`. **Amend** the doc that
owns an approach you are replacing — never leave two live specs.

## 5. End in LETTERED, COST-TAGGED options plus your own pick
Load-bearing — he answers by letter: *"Yeah lets knock out those cheap ones. Thne, we should plan
to do B, E, G, H for now. I and J im ok with holdong off on for now."* · *"I think you are right
on R1 + R3. Build it!"* One letter per option, a cost tag (cheap / a day / a week), what it buys,
and say which you would pick and why you reject the others. A prose recommendation is
unanswerable. **Never build in the proposal turn** — approval is by exact letter or name.

## 6. Specs state laws + an extension table
His follow-up is always extensibility: *"we want a system that we can extend as we build new
content and start to get art."* Satisfy it structurally, not with a promise — the passive layer
stamps identity automatically at composition, so a new spec node is identified the day it is
written, zero hand-authoring. Include a table: new content of kind X → what it inherits free /
what must be authored.

## The design-conversation variant (DESIGN items that need Jake's chat)
- Draw a branching **web**, not a linear list: *"can you build me more of a web of proposals?
  ... can you show it to me visually like that?"*
- One archetype lens per kit — ranged dps / melee dps / caster / healer / support / disruptor
  (cc) / tank. A fork **adds or changes exactly one thing**: *"I dont think these ALL need to
  change really, it seems more like 1 does."*
- Flat literal effect language, audited: *"For scorched mercy, we just deal damage right? not
  adding burn stacks. just say deals damage."*
- Present forks as options; never silently commit. His model is presenting the player options.
- Expect blunt kills and redraw-around-a-concept asks, not incremental patches: *"The passive
  sucks, absolutely hate it"* · *"Ill consider A and S for warden dead until you redraw around
  the taunt concept."*
- Pre-empt his standing instincts: anti-snowball (*"feels snowbally"*); a stack must DO
  something, not merely decay; ranges are cheap to tune so bias generous; agency lives in
  PLACEMENT, not movement — cite ADR 0014 instead of re-deriving it.
- Cadence is fast (*"sounds good, next!"*) — batch a full kit per turn.

## Anti-patterns
Prose where lettered options belong · proposing before measuring · a new doc that duplicates
instead of amending the one specifying the approach it replaces · "research" that reports vibes
instead of mechanisms · building inside the proposal turn.
