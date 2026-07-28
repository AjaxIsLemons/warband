# Passive legibility — making the engine visible (2026-07-27)

Build spec for **roadmap item 20** / headline **B** of [[sim-render-audit]]. Jake: *"lets build
this, and again id ask to root it in research. we want a system that we can extend as we build new
content and start to get art."*

The audit's finding, restated: **`StatRule` emits no event, ever, and `Trigger` emits anonymous
echoes.** The passives are structurally invisible. So the layer ADR 0016 calls the north star —
*compounding builds that feel like they break the game* — is the one layer with no representation
in the renderer. A player running a Berserker whose damage doubles as he drops sees exactly one
thing: bigger orange numbers.

---

## 1. What the genre actually does

**Balatro is the closest thing to our north star that has shipped**, and it is worth being precise
about *why* it works, because the usual reading ("it's juicy") is the wrong lesson.

- **Each contributing element activates sequentially, with its own callout.** Jokers trigger
  left-to-right; each one **physically bounces** as it fires and **the running total updates after
  each trigger.** The engine is not summarised — it is *performed*, one source at a time.
- **The ordering rule is spatial, therefore inspectable.** Left-to-right is the rule, and you can
  see it, and you can reorder it. This is the exact thing Super Auto Pets got wrong: SAP's trigger
  order is secretly sorted by attack stat, and it is the genre's #1 complaint class. Sequencing
  visuals does not make an ordering rule legible — **only exposing the rule does.**
- **The numbers stay king.** "Core data metrics consistently maintain the highest priority for
  readability and are never overshadowed by exaggerated visual effects."
- **The feedback was built alongside the scoring system, not layered on after.** LocalThunk's own
  framing of the pleasure — *you set up your Rube Goldberg machine and watch it go* — is a
  description of a spectator autobattler, which is what we are.

Reinforcing rules already established in [[fight-legibility]] and re-validated for this slice:

- **Underlords — make thresholds discrete events.** Its mana bar *changes colour at full*: "about
  to cast" becomes a binary flip you cannot miss. **Generalised here: a conditional passive coming
  ONLINE is a discrete event, not an analog condition.** This is the single most load-bearing
  borrowing in this document, and it is what forces the sim change in §3.2.
- **HSBG — persistent state beats transient particles.** A buff you can read at any moment beats a
  flash you had to be watching for. So a passive's *active* state is a persistent mark; its
  *transition* is the flash.
- **TFT — visual impact must equal gameplay impact**, and defensives stay quiet (Riot's table,
  already adopted in [[combat-spectacle]] §1). A passive is a **rider**, not a swing:
  [[combat-spectacle]] §6's trigger-rider law already says riders are ECHOES — no windup, no
  hit-stop, numbers at 0.7×, and a spark-link from ROOT to result. That law was written for this
  and has been waiting for the event that makes it addressable.
- **Guildrun** (direct competitor, demo public 16 July 2026, 93% positive) is praised for
  *"ability telegraphs, positioning swaps, and status effects visible without pausing every two
  seconds"* — i.e. this is table stakes in our genre *right now*, not a nicety.

**What nobody ships first-party:** the rule behind the ordering. We will not solve that in this
slice either — but §6 records it honestly instead of pretending the stagger is an explanation.

## 2. The three laws

**L1 — A passive is a NAMED SOURCE, not an anonymous number.**
Every `Trigger` and `StatRule` carries a stable `RuleId`, and **nothing is hand-authored**: the id
is stamped automatically at composition time from the content that contributed the rule. Chassis
passive → `berserker`; spec node → the node's own id (already in `Traits`); weapon → its name;
mastery rider → `<weapon>/mastery`; trinket → its name. New content is identified for free the day
it is written, which is the whole extension story.

**L2 — Coming online is an EVENT.**
`StatRule` is a read-time predicate evaluated fresh at every stat read and never cached (ADR 0004's
missing primitive) — there is no activation moment to hook. So the sim **samples** the predicates
once per tick and emits **transitions only**. This is Underlords' threshold lesson applied to the
one place it has never been applied.
Non-negotiable reason it must be sim-side: [[render-contract]] law #1 — *the client consumes ONLY
(initial snapshot, event log) and runs zero combat logic, ever.* The client may not evaluate a
condition, so an invisible condition can only be made visible by putting it on the wire.

**L3 — The renderer stays a thin executor over the registry we already have.**
No new registry, no new director, no per-rule code. Two appended `EventKind`s and **one new
`byRule` filter** on `TellDef`, mirroring the `byAbility` filter that already gives 27 casts their
own looks. Consequences, which are exactly what Jake asked for:
- a brand-new passive is **never silent** — it matches the filterless fallback row;
- giving it a bespoke look is **one JSON row**, no recompile, hot-reloadable at F1;
- when art arrives, it attaches through the `vfx` / `impactVfx` / `groundVfx` fields that already
  exist on every tell row, through the same `VfxLibrary` recipes;
- and `spark-link` — the rider attribution recipe — **already exists**, authored for precisely this.

## 3. The wire

### 3.1 `TriggerFired`
Emitted in `FireIfMatch` when a trigger's conditions pass, **before** its effects resolve, so the
tell is the cause of the children that follow it in drain order (render-contract §5 causality).

| field | meaning |
|---|---|
| `Source` | the rule's owner — the unit whose engine fired |
| `Aux` | index into the owner's `Def.Triggers` — resolves to a `RuleId` through the fold |
| `Target` | the subject of the event that set it off (`ev.Target`, else `ev.Source`), for the spark-link |
| `Root` / `Depth` | inherited, so a rider chain still reads as one chain |

Ids are **not** on the event: `BattleEvent` is all ints by design. The id table rides the initial
snapshot per unit, and the event carries an index into it — the same interning the replay format
would want anyway.

### 3.2 `RuleChanged`
One per-tick sweep, after effects resolve, in deterministic `(unit id, rule index)` order.
`Amount = 1` on, `0` off. **Transitions only.**

| field | meaning |
|---|---|
| `Source` | the owner |
| `Aux` | index into the owner's `Def.StatRules` |
| `Amount` | 1 = came online, 0 = went offline |
| `Aux2` | the rule's current contribution, so the render can show *how much* without recomputing |

Cost: ~3 rules × 8 units = ~24 predicate evaluations per tick, and the same `CondsOk(u, When,
NullEvent)` call `RuleBonus` already makes — so the sweep cannot disagree with the stat read.

**Known limit, deliberately accepted for v1:** a *scaled* rule (`ScaleBy` — Full Draw's per-hex,
Burning Hours' per-10%-missing, Grudgekeeper's per-10-Shield) changes magnitude continuously
without changing on/off state. We emit the transition and the magnitude at that moment; we do not
re-emit on drift. A passive that is on stays on. Sampling magnitude every tick would put a
per-unit number stream on the wire for a readout nobody asked for yet — revisit only if the badge
feels dead.

### 3.3 What does NOT change
- **No `EventKind` is inserted** — the ordinal is the wire encoding. Both are appended.
- **`ContentHash` does not hash `RuleId`.** The fingerprint exists to catch a *retune*, not a
  rename, and a rule id changes no simulation outcome. Hashing it would invalidate every save for a
  presentation-only change. **Gate: the fingerprint must still be `3dba11673c26e858` afterwards.**
- **`RuleId` is stamped onto CLONES.** `Loadout.Compose` currently does
  `def.Triggers.AddRange(chassis.Passives)` — the *shared static catalog instances*. Stamping in
  place would rewrite the kit for every later composition in the process, which is the exact bug
  the `Signature` clone comment already warns about.

## 4. The render vocabulary (authored, not coded)

Fallback rows ship with the slice so nothing is ever silent; everything below is a `tuning.json`
row and all of it is F1-tunable.

| signature | reads as |
|---|---|
| `TriggerFired` (fallback) | the OWNER pulses briefly — the source acts, Balatro's bounce — plus a `spark-link` to `Target`: *that unit's engine did this to that unit* |
| `TriggerFired` + `byRule` | the bespoke per-passive look, added when a passive earns one |
| `RuleChanged` on (fallback) | a quiet persistent mark while active (HSBG: state is persistent) — T0/T1, no bloom |
| `RuleChanged` off | the mark fades |
| `TriggerFired` + `announce` | the feed names it: `«Brakka» — Grudgekeeper`, rationed by the existing 6 s announce cooldown |

**Proportionality (Riot's table, already law here):** a rider is an ECHO. No windup, no hit-stop, no
camera. It must never out-shout the swing or cast that caused it — the point is attribution, not
spectacle.

## 5. Why this is the extensible version

| when we… | what it costs |
|---|---|
| author a new spec node with a passive | **nothing** — it gets an id from its node name and the fallback tell |
| want that passive to look distinct | one `tuning.json` row (`byRule`), hot-reloaded |
| get art for it | drop a texture/recipe into `VfxLibrary`, name it in the row's `vfx` field |
| add the Inscription catalog (item 5a, 5 of 24) | Inscriptions compile to the same `Trigger` atom, so they are **already covered** — this is the layer that makes collecting them legible |
| want a per-passive icon row | `TellDef` grows one field; `StatusIconRow` already solves layout, priority and capping |

The last row is the point: this slice is deliberately the **plumbing plus a fallback**, not a
content pass. It is what makes a content pass cheap later.

## 6. BUILT 2026-07-27 — what shipped, and what it cost

**Sim.** `Trigger.RuleId` / `StatRule.RuleId`, stamped on CLONES by `Loadout.AddRules` — the one
door every rule now walks through — plus `D.Named()` for the authored enemies and bosses that build
on a composed kit, and `Catalog.Identify` for banners (so team rules name themselves too).
`EventKind.TriggerFired` + `RuleChanged`, appended. The per-tick `SampleStatRules` sweep, running
after `DeathPhase` so a corpse's rules go offline on the tick it dies. `RuleValue` split out of
`RuleBonus` so the badge and the damage number share one implementation. Replay **v6** carries the
table; the fold carries `RuleIds` + per-unit `ActiveRules` (outside `HashView`, same reasoning as
status `ExpiryTick`). **Every rule in every committed fixture names itself — zero unnamed.**

**Client.** `TellMatch` gains `rule`/`eventRule` at **+2 specificity** (a rule id names exactly one
passive, so it must outrank a chassis or weapon row rather than tie). `TellDef.byRule`, the
dispatcher resolving the id off the fold, and two fallback rows: `TriggerFired` → a quiet gilt pulse
on the OWNER plus a `spark-link` to what it affected; `RuleChanged` → a softer, longer pulse.

**The load, measured, honestly.** `TriggerFired` alone runs 1.4–7.1 events/s raw, against a ~21/s
total budget — enough to cost more legibility than it buys. So the same onset-not-refresh law the
status rows follow applies here: `fx.passiveOnsetSeconds` (2.5 s) rations repeats per (unit, rule),
because **a passive firing every swing is the engine running, not news.** First-fire rate is
0.1–2.9/s, which fits. Net effect across all eleven fixtures:

| | before today | after today |
|---|---|---|
| all fixtures | 15.47 tells/s | **16.27 tells/s (+5.2%)** |
| castfest (the worst case) | 20.8/s | **18.1/s (−13%)** |
| glyphwar | 19.0/s | 18.2/s (−5%) |
| wallfort (the quietest) | 5.7/s | 7.1/s (+26%) |

The passive layer was paid for almost entirely out of the status-strobe savings, and it landed
where there was headroom: **the busiest fights got quieter, the quiet ones got busier.** If it still
reads as noise, `passiveOnsetSeconds` is a slider and the fallback row can drop its spark-link.

**Gates:** 471 tests (260 sim + 211 run; 11 new in `RuleLegibilityTests`) · **`make baseline`
byte-identical across 129 metrics** and content fingerprint still `3dba11673c26e858` — the proof
that the new events changed no fight and invalidated no save · all 11 fixtures regenerated and
round-tripping · headless client compile 0 errors, negative-controlled · every `tuning.json` key
verified to bind. **The negative property is enforced structurally**, not just tested: the drain
loop drops both kinds before they spend cascade budget or scan a single trigger.

## 7. Honest gaps (do not let these be discovered as surprises)

1. **The ordering rule is still not inspectable.** Triggers fire in `_units` order inside the drain
   loop — an arbitrary rule the player cannot see. Balatro's order is legible because it is
   *spatial*; SAP's is the genre's #1 complaint because it is hidden. We are currently SAP. This
   slice makes each firing *visible and attributed*; it does **not** make the order *legible*.
   Fixing it means either exposing the order or making it positional, and that is a design call.
2. **Scaled magnitude drift is invisible** (§3.2).
3. **Nothing is watched.** Play Mode is unreachable from a session and the Unity lock has been held
   all evening; this slice's client half is machine-gated only until Jake looks at it. The two
   fallback rows' colours and the 2.5 s ration are guesses that need eyes.
4. **~~The badge is a pulse, not a persistent mark yet.~~ HALF-CLOSED 2026-07-27 by item 21.** The
   in-fight inspector now shows a unit's whole passive roster with the conditional ones marked
   **live** or idle off `ActiveRules`, so the persistent state exists — **on the hover card**. What
   is still missing is an *on-body* mark that reads without hovering; the fold has the data, so that
   remains a `StatusIconRow`-shaped follow-on whenever the board wants it.

## Sources
[Balatro activation sequence](https://balatrogame.fandom.com/wiki/Guide:_Activation_Sequence) ·
[Balatro: juicy feedback in a poker roguelike](https://blakecrosley.com/guides/design/balatro) ·
[Balatro design analysis — visual packaging and interactive feedback](https://medium.com/@yyh19971004/balatro-design-analysis-visual-packaging-and-interactive-feedback-cc6fa6a65370) ·
[GMTK on Balatro's design](https://gmtk.substack.com/p/balatros-cursed-design-problem) ·
[Guildrun review](https://gguildrun.wiki/review/).
Underlords' mana-bar colour flip, HSBG/Bob's Buddy, the SAP trigger-order thread and the Riot VFX
Style Guide are cited in [[fight-legibility]] and not re-fetched.
