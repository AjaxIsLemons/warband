# Sim ↔ render audit — what we have, what it costs, where to go (2026-07-27)

Jake's brief: *"a large research project and comparison: (1) our sim and its outputs, (2) the
render, everything about it, (3) a sim/render roadmap, (4) telegraphing abilities/passives —
the cast circle is short-lived, how do others do it better, (5) the grid shape (square) vs
rhombus + camera angle. Headline suggestions to delve into."*

This page is the **measurement**, not a decision. It extends [[fight-legibility]] (the 07-25 plan),
[[combat-spectacle]] (the visual language) and [[render-contract]] (the accuracy law). Where those
pages say "designed" or "open", this one says **what the numbers actually are today**, because
several of them turned out to be materially different from what the board records.

**Method.** Line-level read of `Warband.Sim` (Battle/Events/Playback/TellMatch/FightSummary),
`Warband.Content` (Trigger/StatRule grammar), `ReplayPlayer.cs` (2703 lines) + the `Vfx/` set, the
73-row tell registry in `tuning.json`, `Game.unity`'s camera, and `make coverage` over five
committed replay fixtures. Geometry figures are computed from the shipped constants; the two
modelled inputs are noted where they appear.

---

## 1. The sim's output — what is actually on the wire

**Shape.** 19 `EventKind` · 9 `Cause` · 27 `StatusKind`. Every mutating event carries the delta
*and* the absolute post-state; `Root`/`Depth` carry causality. The fold (`PlaybackState`) is the
view-model and is guardrail-tested against live sim state every tick. **This layer is in good
shape and is not the problem.** The problem is what never becomes an event at all.

### 1.1 Measured census (`make coverage`, committed fixtures)

| fixture | units | events | ticks | sim sec | @5 tps | **visual events/s** | **numbers/s** |
|---|---|---|---|---|---|---|---|
| stomp | 6 | 232 | 41 | 4.1 | 8.2 s | ~14 | ~4 |
| skirmish | 6 | 528 | 77 | 7.7 | 15.4 s | ~17 | ~5 |
| **castfest** | 6 | 612 | 80 | 8.0 | **16.0 s** | **20.8** | **6.3** |
| glyphwar | 6 | 679 | 87 | 8.7 | 17.4 s | ~21 | ~6 |
| boss-waning-crown | 12 | 763 | 200 | 20.0 | 40.0 s | 11.5 | 4.6 |

"Visual events" = events with a matching tell row. `ManaChanged` (156 of castfest's 612, 25%) is
correctly silent. **castfest is the worst case at ~21 tells and 6.3 floating numbers per second of
real playback time.** That is the measurement behind "too much at once" — nobody parses 21 discrete
visual events a second.

### 1.2 The single largest noise source — and the fix the measurement changed

castfest emits **82 `StatusApplied`/Burn** events, 26% of every tell in the fight. The first guess
was that the decay tick was to blame: `Battle.cs:504` re-broadcasts the Burn pool's shrinking
magnitude every pulse with `Cause = Cause.Burn`, while a real application (`Battle.cs:925`) carries
no cause — so `Cause` already separates them and one tell row would mute the noise for free.

**Measured against the eleven committed fixtures, that was wrong, and by a lot.** castfest's 82
split **70 applications / 12 decays**; across every fixture, decays are 65 events total. Burn is a
*single merged pool per unit*, so a Pyromancer swinging once a second re-stokes the same pool — the
noise is **re-application**, which is exactly the half `Cause` does *not* mark.

The rule the data does support is more general and worth more:

| | onset | **refresh** | **decay** | total tell-bearing events |
|---|---|---|---|---|
| castfest | 28 | **74** | **12** | 333 → **25.8% removed** |
| boss-waning-crown | 48 | 50 | 20 | 460 → 15.2% |
| glyphwar | 38 | 46 | 11 | 331 → 17.2% |
| **all 11 fixtures** | — | **512** | **65** | 6756 → **8.5%** |

**Flash on ONSET, not on refresh.** A re-application says nothing the status icon's own stack count
and countdown ring do not already say; the *transition* is the news. This is the genre's own lesson
(persistent readouts beat transient particles) applied to our own registry.

**BUILT 2026-07-27** — `FxTune.statusRefreshQuiet` (default true). The Director keeps its own
`(unit, status)` set, because the fold has already applied the event by dispatch time and so can
never answer "was this already showing?". Cleared on Reset with the other rations. Note this
subsumes the decay case exactly (a decay is by definition a refresh of a held pool), so the
`byCause: Burn` tell row was dropped rather than shipped alongside it — one mechanism, not two.

### 1.3 THE HEADLINE FINDING — two whole mechanic classes emit nothing, ever

The content atom is `Trigger{On, When, Do}` plus `StatRule{Stat, When, Amount, ScaleBy}`
(`Content.cs`). Those are the passives. Their visibility:

| layer | what reaches the renderer | verdict |
|---|---|---|
| **`StatRule`** — read-time conditional stats: *"while below 50% HP: +6 attack"*, *"+2 per hex to target"* (Full Draw), *"per 10% missing HP"* (Burning Hours), *"per 10 Shield"* (Grudgekeeper) | **nothing. No event exists.** `RuleBonus()` is summed inside `EffAttack` at damage time and folded into one number. | **structurally invisible** |
| **`Trigger`** | its *effects* emit normally with `Cause.Trigger` and `Root = owner`, so you see the result | **the result is visible; the trigger is anonymous** — nothing on the wire says *which* passive fired, or that one fired at all |

So the layer ADR 0016 calls the north star — *compounding builds that feel like they break the
game* — is the **one layer with no representation in the renderer**. A player running a Berserker
whose damage doubles as he drops sees exactly one thing: bigger orange numbers. The engine is
invisible; only its exhaust is visible.

This is also the reason the fix cannot be authored in `tuning.json`. Every other legibility gap on
the board is a data row. **This one needs an event.**

### 1.4 Smaller wire gaps (each cheap, each currently costing a visual)

| gap | evidence | cost to close |
|---|---|---|
| `EventKind.StormTick` declared but **never emitted** | only the enum + `EventText` reference it | n/a — do not build on it |
| `Heal` carries no `Cause` | `Battle.cs:946` | one field; unblocks the dormant Boon pulse (roadmap item 1) and ability-heal tracers |
| `Cast` carries no target and no ability id | `Battle.cs:410` — `{Source, Cause.Ability}` | **already solved view-side**: `TellMatch` resolves `sourceAbility` from `Content.AbilityIdentity(chassis, traits)` off the fold. 27 per-ability cast rows exist. No sim change needed. |
| no "acquired target" / "why this target" event | targeting is recomputed each tick internally | would need a `TargetChanged` event; see §4 |
| `FightSummary` / `FightStats` / `BattleForecast` fully computed, **~5% displayed** | roadmap item 1c owns this | UI job, already spec'd |

---

## 2. The render — what is actually built

**The board's docs understate the build by a lot.** [[fight-legibility]] still describes "14 tells,
five things, no particles". Reality as of today:

| system | state |
|---|---|
| tell registry | **73 rows**, 48 authored fields each |
| tell filters | `Cause` · `StatusKind` · `FieldFlavor` · `ranged` · **`chassis`** · **`ability`** (weighted ×2) · **`weapon`** — most-specific-wins, headless-tested |
| cast tells | **35 rows** — 1 fallback + 8 per-chassis + **27 per-ability**, windups 0.18–0.55 s, `announce` banners on 8 S-tier casts, `bigImpact` camera on 3 |
| attack tells | 15 rows — per-weapon language (nick-cross, slash-wide, muzzle-flash, thrust-line, pole-swipe…) |
| VFX runtime | hand-rolled `VfxLibrary` — **~25 recipes**, Director-stepped (deterministic under capture), 6 custom URP shaders (Ring/Sigil/Glow/Particle/Dissolve/GroundFill) |
| era sigils | **8 PNGs shipped**, `RequireTexture` degrades safely |
| models | **KayKit minis on the board**, 6 chassis bodies + 2 kitbashes, shared 23-joint rig, Idle/Walk/Attack/Cast/Hit states, `Animator.speed` fitted to the sim's gap |
| bars | segmented HP (1 divider per 25 HP), ally/enemy tint, mana threshold flip + pulse |
| statuses | `StatusIconRow` — icons with **duration + stacks**, priority ordering, cap 5 + "+N" |
| pacing | beat sequencer (stagger 45 ms), hit-stop (death 140 ms / crit 60 ms), **playback at 5 ticks/s = half the contract's 10** (spectator retiming already applied), 0.7 s opening hold |
| dress | Deathless freeze + board dim, fight-ender slow-mo 0.2× / 0.6 s, rationed camera punch + shake (1 per 3 s), death dissolve + ash marks |
| story | world-space feed + banner + end readout + the new Waning clock |
| tuning | every number above is in `tuning.json`, hot-reloadable, auto-sliders at F1 |

**Conclusion: the render's vocabulary is not the bottleneck. Its dosage, its camera, and its
coverage of the passive layer are.**

### 2.1 "The cast circle is short-lived" — measured, and the doc was right

`ReplayPlayer.cs:558` — the sustained cast aura is ended **at the end of the windup**, i.e. exactly
when the payoff starts, and then runs a 0.25 s fade. The sigil's own alpha curve ramps in over
**0.22 s**.

Only the **27 per-ability** cast rows carry a sigil (`cast-aura-<chassis>`); of the eight
chassis-level rows only pyromancer's does, so the sub-0.25 s chassis windups draw no sigil at all.
Across the 27 rows that do (`motion: None`, so **contact == the end of the windup, exactly when the
sigil was being closed**):

| per-ability row | windup | sigil at full alpha for |
|---|---|---|
| sharpshot.volleyer | 0.25 s | **0.03 s** |
| shade / pyromancer-chassis / bulwark | 0.28–0.30 s | 0.06–0.08 s |
| the median row (pyromancer, cleric, banneret, …) | 0.40 s | 0.18 s |
| starfall / faultline / everburn | 0.55 s | 0.33 s |

So the most expensive, most identity-bearing art in the game is at full opacity for **0.03 to 0.33
seconds**, and every single row closed its sigil *at the exact moment its payoff landed.*
[[combat-spectacle]] §2 specifies a **four**-beat sentence (windup → release → impacts →
**recovery: "sigil burns out to ash over 0.3 s"**). Beats 3 and 4 had no sigil at all.

**FIXED 2026-07-27** — `FxTune.castSigilHoldSeconds` (default 0.35 s, speed-scaled, F1-tunable).
The sustain now closes 0.35 s *past* the release rather than at it, and the recipe's own 0.25 s fade
plays the recovery beat. Measured over all 27 rows: full-alpha window **0.03–0.33 s → 0.38–0.68 s**,
every sigil outlives its own contact by 0.35 s, **zero rows regress**. Watch item: a Great Chorus
Lifebinder casts every ~1.1 s and the sigil's total life is now ~1.10 s, so a spamming caster's
sigils will just touch — one slider if that reads as a smear.

---

## 3. Geometry and camera — the numbers

### 3.1 What we have (exact, from shipped constants)

- **Sim board:** `BoardRows = 8`, `BoardCols = 6`, odd-r offset, **pointy-top** axial hexes. Team 0
  rows 0–2, team 1 rows 5–7, two neutral rows. 48 hexes, 18 per side, capacity 3→6 units.
- **World layout:** `hexSize 1.15` → lateral centre-to-centre **1.992**, row spacing **1.725**.
  Footprint **10.96 × 12.08** world units — i.e. **the board is very nearly square** (0.91 : 1).
  Jake's read is correct.
- **Camera:** `yaw 13° · pitch 25° · distance 1.13 × span`, **perspective, vertical FOV 60°**
  (`Game.unity:182`). Nothing sets FOV at runtime for the board.
- **Orientation is right and matches the genre:** pointy-top rows running toward the enemy is the
  same layout TFT uses (straight horizontal ranks of hexes, alternate rows offset a half-hex).
  A consequence worth knowing: with pointy-top there is **no straight-ahead neighbour** — advancing
  a row is always a diagonal step, so closing reads as a weave, and no unit is ever *exactly*
  behind another. That last part is a free anti-occlusion property we should not throw away.

### 3.1b CONFIRMED IN THE ENGINE, 2026-07-27 (first capture of the session)

An edit-mode `BuildPreview` capture of `skirmish` at tick 6 put the modelled numbers against the
real renderer, and they hold:

- **Camera position matched the model to the centimetre** — predicted `(2.70, 5.76, −6.01)`,
  actual `(2.70, 5.77, −6.01)`. Every geometry figure in §3 rests on the same arithmetic, so the
  occlusion, taper and frame-fill numbers can be trusted.
- **The nameplate collision is real and is the most glaring thing in the frame.** Six units produce
  six overlapping labels — `Bulwark`'s plate sits across the Berserker's body, `Phalanx` and
  `Banneret` collide, `Cleric` floats over units two rows away. This is §3.2(c) rendered.
- **The board reads as a rhombus already** — the perspective plus the 13° yaw project it as a
  diamond. So Jake's instinct is half-satisfied and half-diagnosed: it is not that the board is not
  a rhombus, it is that it is a **deeply foreshortened** one. The near rank is roughly twice the
  size of the far rank, and the far half is squashed against the top of the play area.
- **The empty margins are enormous.** The board sits in the middle of a large dark frame, and the
  six units occupy something like a sixth of it.

**DONE 2026-07-27: `nameplates.show` now defaults to FALSE**, on capture evidence rather than taste.
The A/B was shot on the same frame (`skirmish` t6, nameplates on vs off) and the difference is not
subtle: with them off, six distinct bodies, their HP bars, their status icon rows, a floating damage
number and an in-flight spark tell are all legible; with them on, the labels lie across the bodies.
Unit identity is carried by silhouette, the ground disc and the hover card (item 21). One F1 toggle
back, and worth revisiting if the pitch ever rises.

**One thing the capture cannot verify:** the passive tells from item 20. The Game scene is currently
parked in its Hall configuration by the parallel UI session — **`Main Camera` is disabled and a
`Hall Camera` is live**, so `Camera.main` is null and `FrameCamera()` has been returning early. The
capture enabled the board camera, hid the Hall world, shot, and restored all three exactly
(scene never saved, `isDirty=False`). Worth knowing before the next session wonders why the board
renders the Hall on top of itself.

### 3.2 The three things that are measurably wrong

Modelled from the constants above. The one estimated input is the unit body height (~1.30 world
units at `models.scale 0.75`); every other figure is exact — bars sit at `1.55 + barLift 0.35`,
icons at 2.07, nameplate at ~2.37.

**(a) FOV 60 is the outlier number in the whole render.** At `distance = 1.13 × span` the board
occupies **57% of frame width and 49% of frame height** — under a fifth of the screen once you
account for the trapezoid. Units render **144 px tall in the front rank and 63 px in the back** at
1080p. [[fight-legibility]]'s own adoption gate is *"every model must pass the 60 px black-shape
test"* — **the enemy back line, where artillery and bosses sit, is at 63 px.** It is passing its
own gate by three pixels.

**(b) Perspective taper 2.29×.** Because FOV is wide, the camera is close: the near edge of the
board is ~8.2 units away and the far edge ~19.1. A back-row unit renders at **44% the size** of a
front-row one. Same unit, same importance, less than half the pixels.

**(c) Pitch 25° stacks ~3 rows of UI on top of each other.** Row spacing projects to
`1.725 × sin 25° = 0.73` world-units of screen-vertical, while a unit's full stack (body + HP/mana
bars + status icons + nameplate) projects to `2.37 × cos 25° = 2.15`. **Ratio 2.95 — every unit's
nameplate and bars sit on top of the units up to three rows behind it.** On an 8-row board that is
the entire enemy half smeared under the friendly half's UI.

| pitch | rows of overlap | note |
|---|---|---|
| **25° (today)** | **2.95** | |
| 35° | 1.96 | |
| 45° | 1.37 | |
| 50° | 1.15 | |
| 55° | 0.96 | a unit's whole stack fits inside one row's spacing |

### 3.3 The rhombus question, answered

**Yaw does not buy screen area — it costs it, and we are already paying.** A rotated rectangle's
bounding box grows in both axes while the shape fills only about half of it. Frame fill for the
current 6×8 board:

| pitch | yaw 0° | **yaw 13° (today)** | yaw 30° | yaw 45° |
|---|---|---|---|---|
| 25° | 82.8% | **55.4%** | 41.2% | 37.5% |
| 35° | 89.0% | 64.1% | 51.1% | 48.9% |
| 45° | 72.2% | 52.0% | 41.4% | 39.7% |

Today's 13° yaw already costs about a third of the board's potential screen area versus a frontal
framing. Going to a full isometric 45° would cost more than half.

**What yaw genuinely buys** is the diorama read (you see the sides of models, the board reads as an
object on a table rather than a diagram) and partial depth separation. But our pointy-top zigzag
already provides the depth separation for free (§3.1), so for us yaw is **pure taste, priced in
screen area** — which is exactly the currency we are shortest of.

**So the honest answer to "should the grid be a rhombus?" is: the rhombus is not the fix.** The two
things a wider-looking board would actually buy are available more cheaply:

1. **Raise the pitch** — kills the occlusion, costs nothing.
2. **Make the board genuinely wider than deep** — this is the real version of Jake's instinct, and
   it is a *sim* change, not a camera one.

The governing relation is clean: a frontally-framed rectangular board fills a 16:9 frame exactly
when `width / depth = 1.78 × sin(pitch)`. Our ratio is **0.91**, which is optimal at **pitch 31°** —
and pitch 31° is precisely where the occlusion is still ~2.3 rows. **The current board shape
mathematically forbids a pitch high enough to be readable.** That is the actual finding behind
Jake's question.

| board | pitch | frame fill | rows of overlap |
|---|---|---|---|
| **6 × 8 (today)** | 25° | 83% (yaw 0) / **55% (yaw 13)** | **2.95** |
| 6 × 8 | 45° | 72% | 1.37 |
| 7 × 8 | 35° | 95% | 1.96 |
| **8 × 8** | **45°** | **98%** | **1.37** |
| 9 × 8 | 50° | 97% | 1.15 |

**8 columns × 8 rows at ~45° pitch, yaw 0–10°, is the geometric sweet spot**: it fills the frame,
it halves the occlusion, and it is the only shape in the table that does both. It costs +16 hexes
(24 per side instead of 18), which touches `Battle.InBounds`, `Pathing.Cells`, every authored
encounter's formation, every deployment fixture, and the balance baseline — a real, bisectable,
one-session change, but a **sim** change, and one the content doctrine says must be measured
(`make baseline`) not eyeballed.

### 3.4 The free experiment, today

`camera.yaw / pitch / distance` are already `tuning.json` fields with F1 sliders and hot reload.
**FOV is the one camera number not exposed** — it lives in `Game.unity` and `FrameCamera()` never
touches it. Adding `camera.fov` to `CameraTune` and setting `cam.fieldOfView` in `FrameCamera` is
~3 lines and makes the entire framing question a slider session rather than a build cycle.

A sane starting point to dial from: **fov 34 · pitch 42 · yaw 6 · distance 1.45.** All four are
guesses meant to be moved with the sliders in front of a real fight; the point of the change is to
make them *movable*.

---

## 4. Telegraphy — the fourth problem

[[fight-legibility]] decomposed "not clear what's happening" into three problems (can't SEE it /
can't ATTRIBUTE it / can't INFER THE RULE). That decomposition is good and we have built hard
against 1 and 2. This audit adds a fourth that none of the pages name:

> **4. Can't see what a unit CAN do — only what it just did.**

Everything on screen is *event-triggered and transient*. There is no persistent representation of
capability anywhere on the board. Concretely:

- Passives (`StatRule`) have no representation at all (§1.3).
- Triggers are anonymous — you see an echo, never its name (§1.3).
- The in-fight hover card (`Tooltip.cs` → `ReplayPlayer.PickUnit`) shows **name, team, HP, Shield,
  Mana, statuses**. It does *not* show the unit's ability, its passives, its weapon, its range, its
  cadence, or its targeting rule — **even though `PlaybackUnit` already carries `Traits`,
  `WeaponName`, `WeaponTier`, `Range`, `Attack`, `AttackInterval`, `MoveInterval`, `CritChance`.**
  The data is in hand at the call site and thrown away.
- The status icon row is the one place persistent state *is* shown, and it is the best thing on the
  board — which is the evidence for the whole argument.

**What the genre does about this** (the load-bearing references, several already logged in
[[fight-legibility]]):

1. **HSBG makes state permanent, not transient.** A buff is a stat change on a card you can read at
   any moment. Its whole legibility advantage is that *almost nothing is an animation*.
2. **Underlords made a threshold a discrete event** — the mana bar changes colour at full, so
   "about to cast" is a binary flip. We shipped this; it is the correct pattern and the argument for
   generalising it.
3. **TFT deliberately slowed its animations** — Riot authored a *second, slower* animation set
   because League timings were unparseable in a 9v9. Wittrock's stated target was *"we wanted it to
   feel like you're observing a Bronze teamfight."* We already run at half contract speed (5 tps);
   the remaining lever is per-effect duration, and §2.1 shows the sigil is the one running short.
4. **Riot's proportionality table** — ult biggest, **defensives quiet**, autos small. Adopted in
   [[combat-spectacle]] as authoring law; the tell rows broadly honour it.
5. **Audio is the only free channel in a crowded frame** — it was Underlords' actual post-launch
   readability fix, and Riot requires a wind-up sound *ahead of* every big cast. Our tell rows have
   `sound` / `critSound` / `castSound` fields wired and **`audio.enabled` currently ships muted**
   because the generated stings were bad. That is a whole sensory channel sitting at zero.
6. **Nobody solves problem 3 first-party** — HSBG needed the community to build Bob's Buddy, SAP's
   trigger order is secretly attack-stat-sorted and is the genre's #1 complaint class. Our seeded
   re-simulable sim can close it, and `BattleForecast` is already written and has **zero client
   references**.
7. **Guildrun — the direct competitor — is shipping and is being praised for exactly this.** Its
   demo went public 16 July 2026 (11 days ago), 93% positive over 1,078 reviews, and the reviews
   specifically credit *"ability telegraphs, positioning swaps, and status effects visible without
   pausing every two seconds"*, *"readable silhouettes, distinct hero outlines, and VFX that
   communicate impact without cluttering the hex grid"*, and *"audio feedback on crits, heals, and
   relic procs"*. **Playing that demo is the single highest-information research action available
   and it costs an evening.**

**The shape of the fix for us** (three tiers, cheapest first):

- **T1, data only.** Mute the Burn decay re-announce (§1.2). Hold the sigil through the payoff
  (§2.1). Re-check `minAmount` thresholds so chip damage stops printing numbers.
- **T2, client only.** Put the unit's ability name, passives, weapon and targeting rule on the
  in-fight hover card — the data is already in the fold. This is the same `MechanicPresentation`
  vocabulary the Hall already speaks.
- **T3, one small sim change — the important one.** A `TriggerFired` event carrying
  `{owner, ruleId, rootEvent}`, and a `StatRuleActive` state on the fold (or a periodic
  "active rules" projection). That single event makes passives authorable in `tuning.json` like
  everything else: a badge that lights, a rim that pulses while a `StatRule` is live, a spark-link
  from the passive to its result (**`spark-link` already exists as a recipe and is already the
  authored attribution for riders**). Without it, the north-star layer stays permanently unrenderable.

---

## 5. Ranked headlines — the proposed arc

Each is independently shippable. Effort is honest; "free" means data-only, no rebuild.

**Jake's call, 2026-07-27: build A, C, D now; plan B, E, G, H; hold I and J. F (audio) not taken.**

| # | headline | why it is here | effort |
|---|---|---|---|
| **A** | **Expose `camera.fov` and re-frame the fight.** FOV 60 makes the board 57% of frame width, the back rank 63 px tall, and the near/far taper 2.29×. Pitch 25° stacks 3 rows of UI on itself. | The largest single measurable defect in the render, and three of its four numbers are already sliders. | ~3 lines + a slider session with Jake |
| **B** | **The passive layer has no renderer.** `StatRule` emits nothing, ever; `Trigger` emits anonymous echoes. Add `TriggerFired` + an active-rule projection, then author badges/rims/spark-links as data. | ADR 0016's north star is the one layer with zero visual representation. Also the missing half of item 5a. | small sim change + tell rows |
| **C** | **Hold the sigil through the payoff.** Eight authored era sigils are at full alpha for 0–0.33 s and vanish *before* the thing they announce. | Jake's own complaint, root-caused to one line. Highest visual value per line in the document. | ~10 lines + one tuning field |
| **D** | **Mute the Burn decay strobe.** 26% of castfest's tells are a Burn pool re-announcing its own decay; `Cause` already distinguishes it from a real application. | One JSON row removes ~65 flashes a fight. | free |
| **E** | **The in-fight hover card is three bars.** Ability, passives, weapon, range, cadence and targeting rule are all in the fold and discarded. | Closes item 12's "deep inspector" for enemies *and* problem 4, in one view. | client only |
| **F** | **Turn the audio channel on.** Every tell row has `sound`/`castSound` wired and the game ships muted because the generated stings were bad. | The genre's proven readability fix, currently at zero. Regenerating ~26 stings is one deliberate batch. | asset batch + Jake's ear |
| **G** | **Widen the board to 8 columns.** 6×8 is 0.91:1 — mathematically it forbids a pitch high enough to be readable (§3.3). 8×8 at 45° is 98% frame fill *and* halves the occlusion. | The real version of Jake's rhombus instinct. Also gives placement more lateral choice. | **DESIGN** — sim change, `make baseline` before/after |
| **H** | **The recap** (roadmap item 1c) — already ranked and spec'd, listed here only so the arc is complete. | The sim computes a full recap and the UI shows ~5% of it. | already on the board |
| **I** | **Ship `BattleForecast`.** N-seed win probability, written, tested, **zero client references**. No genre leader has shipped this first-party. | Answers "was I scammed?", which every game researched has as a documented frustration. | measure the re-sim cost first |
| **J** | **Play the Guildrun demo.** Direct competitor, public 11 days, 93% positive, praised for exactly the axis we are weakest on. | Cheapest information in this document. | an evening |

## 6. Build state (2026-07-27) — A, C, D

**Shipped, machine-gated, NOT watched** (the Unity lock was held by Codex's UI-foundation session
all evening, so not even an edit-mode capture was possible — same standing as roadmap items 10/11):

- **A1 — `camera.fov` is now a tuning field**, clamped 15–75 in `FrameCamera`, F1-sliderable,
  hot-reloadable. **Shipped at 60 = the scene value, so this is a slider and not a silent re-frame.**
- **A2 — the re-frame was deliberately NOT shipped**, for a reason worth keeping: the modelled
  frontier says the current board *cannot* be framed well. Sweeping pitch 28–54° × distance
  1.05–1.70 at yaw 13° finds **no setting** that improves the back rank's pixel size without either
  the board or the kill feed leaving the frame. The board's projected aspect is `0.91 / sin(pitch)`,
  so above ~31° it is taller than 16:9 and must be pushed back — every row you unstack costs unit
  size. Best safe frontal point measured: pitch 42 / distance 1.25 / fov 60 → occlusion **2.95 →
  1.53 rows**, taper 2.29 → 1.86, but units **144 → 113 px** near. That trade is taste, and framing
  is Jake's gate (roadmap 1d), so it stays a slider session. **This is also the quantitative case
  for G**: the board shape, not the camera, is what caps the framing.
- **A3 — the story feed's anchor is now tunable** (`story.feedGapHexes` / `story.feedHeight`,
  defaults reproducing the old anchor exactly), and `RecomputeStoryAnchors` is called on every
  tuning reload rather than only when `hexSize` moves. **Why this was blocking:** the feed anchors
  1.6 hexes *beyond* the board's +X edge — 1.17× the board's own width out — and its billboarded
  lines run further still, so the frame has to hold the board *plus* a text column beside it.
  Narrow the FOV to make units bigger and the feed is the first thing off screen. The model puts a
  ~45-character kill line at 0.94 of the frame's half-width **today**, i.e. already 6% from the
  edge, and a full-length one (`«X» felled «Y» — overkill N`) past it. **Unverified — needs one
  capture**; the char-width figure is modelled from `TextMesh` sizing, not measured.
- **C — the sigil hold** (§2.1). Full-alpha window 0.03–0.33 s → 0.38–0.68 s across all 27 rows,
  zero regressions.
- **D — onset-vs-refresh** (§1.2). Removes 8.5% of the visual load overall, 25.8% of castfest.

**Gates:** 460 tests green (249 sim + 211 run — sim untouched) · headless client compile 0 errors,
**negative-controlled** (injected `camera.fovTYPO`, confirmed caught in `ReplayPlayer.cs`, clean
after revert) · every key in `tuning.json` verified to bind to a real `TuningData` field, which
matters because the loader runs `MissingMemberHandling.Error` and **one unknown key voids the entire
file into silent defaults** (a `"// note"` comment was written and removed for exactly this reason —
do not annotate rows in that file until `TellDef` has a `note` field).

**Deliberately not proposed:** rotating to a true isometric rhombus (costs >50% of the frame for a
taste win we can get more cheaply from pitch), changing hex orientation (pointy-top rows are
correct and match the genre), an orthographic camera (worth trying at the same time as A, but a
narrow FOV gets ~80% of the same read), and any balance-motivated board change — G must be
justified on framing and measured for balance, not the reverse.

## Sources
Riot / TFT animation retiming and the "Bronze teamfight" target —
[The Story of TFT dev blog coverage](https://www.surrenderat20.net/2020/04/red-post-collection-story-of-tft-dev.html),
[dot esports](https://dotesports.com/tft/news/riot-dev-post-explains-how-teamfight-tactics-was-born-in-just-18-weeks) ·
Guildrun demo reception and presentation notes —
[Guildrun review](https://gguildrun.wiki/review/),
[Steam demo](https://store.steampowered.com/app/4425970/Guildrun_Demo/),
[Turn Based Lovers overview](https://turnbasedlovers.com/overview/guildrun-pc-autobattler/) ·
Mechabellum readability/camera criticism —
[MonsterVine review](https://monstervine.com/2025/04/mechabellum-review/),
[Steam camera-angle thread](https://steamcommunity.com/app/669330/discussions/2/6274121610015171064/) ·
hex orientation vs. direction of movement —
[Hex map](https://en.wikipedia.org/wiki/Hex_map) ·
isometric 45°/30° rhombus projection —
[Isometric tiles introduction](https://clintbellanger.net/articles/isometric_intro/).
Riot VFX Style Guide, Underlords patch notes, HSBG/Bob's Buddy and the SAP trigger-order thread are
cited in [[fight-legibility]] and not re-fetched here.
