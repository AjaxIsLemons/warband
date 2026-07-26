# Directed tells — who is attacking whom (implementation spec, 2026-07-24)

The next slice of [[render-polish]]: **causal-linking**. Today every tell is a flash on ONE
unit — a viewer cannot tell who hit whom, who cast what at whom, or why a unit died. This spec
adds **motion** to the existing JSON tell system: melee lunges, ranged/spell tracers, cast
wind-ups, death bursts, blocked-shot fizzles — with impacts timed to *contact*, not dispatch.
Built so per-spell/per-weapon bespoke design slots in later with zero rearchitecting.

**In scope:** motion executors (lunge / tracer / burst) · impact-at-contact latch · pooled FX ·
the real-time FX timeline that seeds the future beat sequencer · new TellMatch `ranged` filter ·
authored tells in `tuning.json`. Subsumes two roadmap-NEXT items: **death poof** (burst executor)
and **emissive particles** (tracers/bursts are HDR-emissive, so Bloom finally bites).
**Out of scope (later, already spec'd in render-polish):** decoupled playback clock · hit-stop /
slow-mo / shake · PrimeTween · beat sequencer blocking · prefab/artist VFX · sound.

## 1. What the event log gives us (verified 2026-07-24)

| Event | Semantics (file refs) |
|---|---|
| `Attack` | **Always** Source=attacker, Target=victim. Fires for normal swings (`Battle.cs:284`), censer heal-swings (`:238`), and effect-swings like Counter (`:716`, with real `Cause` + `Depth`). Emitted even when the hit won't land (Phase-immune → `DealDamage` returns before emitting `DamageDealt`, `Battle.cs:920`) — so whiffs vs a Phased unit render honestly for free. No `Crit` flag (crit lives on `DamageDealt`). |
| `DamageDealt` | Source, Target, Amount, `Cause`, `Crit`, `Depth`, `Root`. Ability damage: Source=caster, Cause=Ability. Cleave/MultiShot rider-hits: depth 1, same Root, contiguous after the main strike. |
| `Cast` | Source=caster only — **Target is -1** (`Battle.cs:328`; the ctx on `:329` is trigger context, never emitted). The cast's truth arrives as its child events (Cause=Ability, Root=caster, same tick). **Do not beam to `u.TargetId` — the ability may hit someone else.** |
| `Heal` | Source, Target, Root, Depth — **no Cause** (`Battle.cs:946`). Binding a heal to its swing/cast must come from the Root latch, not a cause filter. |
| `Death` | Target=deceased, Source=**killer**, Amount=overkill (`Battle.cs:1008`). No Root. |
| `AttackBlocked` | Source, Target, wall hex in Amount=Q / Aux=R (`Battle.cs:275`). No damage follows. |
| `Leap` | Source=leaper, landing hex in Amount/Aux. (Arc motion = cheap follow-on, not v1.) |

**Ranged is the sim's own law:** an attack over `Hex.Distance ≥ 2` traces the hex line and can be
blocked (`Battle.cs:254`). The renderer's melee/ranged split mirrors exactly that rule.

**Causality key:** children of a swing/cast carry `Root` = the originating unit; the roots
themselves (`Attack`/`Cast` emits) leave Root at -1 but set Source. So the latch key is
`e.Root >= 0 ? e.Root : e.Source`. Drain order guarantees the root event is handled before its
children (render-contract §5).

## 2. Architecture (all client-side except §4)

Four small additions to `ReplayPlayer`/`FeedbackDirector`; the fold/state layer is untouched.

1. **FX timeline.** The Director owns `List<ActiveFx>` ticked from `ReplayPlayer.Update` with
   `_director.Tick(dt)`. Each `ActiveFx` is a tiny hand-rolled struct-of-phases (delay → motion →
   impact payload), same style as the existing `FlashT/PunchT` decay — **no tween lib yet**
   (PrimeTween is step 9 of the render-polish build order; executor internals swap later without
   touching data). This list is the seed that grows into the beat sequencer.
2. **Impact latch.** `Dictionary<int, float> _readyAt` keyed on the causality key above.
   - When handling any event, its tell timeline starts at `max(now, _readyAt[key])`.
   - **Only ORIGIN tells set the latch** — `defer=true` is authored on `Attack` and `Cast` tells
     only. The setter **replaces** `_readyAt[key]` with its contact time (lunge contact ≈ 55% of
     motion; tracer contact = arrival; cast = start + windup). Consumer tells (DamageDealt/Heal/
     Status/Death) read the latch but never write it — otherwise a 3-victim cast would
     stagger-cascade (each tracer re-latching the next) instead of fanning out simultaneously.
   - Effect: the victim's flash/number/status-pip-burst lands **when the swing connects / the
     tracer arrives**, and a cast's tracers launch after the wind-up. Chains read causally.
   - Entries are replaced per new swing and ignored once past; cleared on loop reset / rebuild.
3. **MotionOffset.** `UnitView` gains `Vector3 MotionOffset`; `Update` composes
   `Root.position = lerp(foldPos) + MotionOffset`. The fold stays the sole owner of true position
   (render-contract); tells only write offsets/scale/color. Lunge = out-and-back curve on
   MotionOffset toward the target's current view position.
4. **Pooled one-shot FX.** Two new pooled views following the proven `FloatingNumber` idiom
   (`Create(parent)` / `Play(..., recycleCallback)`):
   - `Tracer` — a stretched primitive (thin capsule or quad) oriented `Quaternion.LookRotation`
     along flight, ~0.8 world-Y (chest height), flying start→end over `motionSeconds`. Endpoint is
     the target unit's view position, or a hex for `AttackBlocked` (Amount/Aux → `HexToWorld`).
     **Every tracer arrival pops a small same-color spark burst** (one executor rule): it's the
     "directional impact spark" from render-polish on normal hits, and the fizzle on blocked shots.
   - `Burst` — scale-up-then-collapse at a position, **detached from any unit** (this is the
     death-poof executor: the dying unit hides same-tick, so the burst lives at the corpse hex).
   - Neither self-`Update()`s: both expose `Step(float dt)` and the **Director steps them** — one
     clock drives play mode (from `ReplayPlayer.Update`) and the BuildPreview fast-forward alike,
     so frozen captures show exactly what play mode shows.

**Speed scale:** motion/windup times are authored at 10 ticks/s; scale by
`min(1f, 10f / ticksPerSecond)` so fast-forward compresses tells instead of smearing them.
(Existing flash/punch stay real-seconds, as today.)

**Known accepted limit:** HP bars snap at fold-tick while the impact flash may land ≤ ~0.3 s later
(bar leads flash). Contract §3 says stale-never-drift; full sync arrives with the decoupled-clock
refactor already on the board. Do not try to defer the fold.

## 3. Data schema — `TellDef` grows a motion block

New optional fields (defaults = today's behavior; existing tells in `tuning.json` need no edits):

```json
{
  "eventKind": "Attack",
  "side": "Source",
  "byRanged": true, "ranged": true,        // NEW filter: ranged := Hex.Distance(src,tgt) >= 2
  "motion": "Tracer",                      // None | Lunge | Tracer | Burst   (default None)
  "motionSeconds": 0.15,                   // travel / lunge out-and-back duration
  "motionColor": "#FFF3D0FF",              // tracer/burst color
  "motionGlow": 3.0,                       // HDR multiplier — pushes past bloomThreshold 0.9
  "motionScale": 1.0,                      // tracer thickness / burst size
  "windupSeconds": 0.0,                    // pre-motion anticipation on the source
  "defer": true,                           // ORIGIN tells only (Attack/Cast): set the impact latch
  "flash": false, "punch": false, "number": false
}
```

**Visual tuning is free:** the F1 DebugMenu is a reflection-driven UI Toolkit cockpit — it
auto-generates a control per public `TuningData` field (float→slider, Color→RGBA sliders,
enum→dropdown, list→foldouts). New TellDef fields appear in-game automatically; give them
`[Range]`/`[Min]` attributes so the generated sliders get sensible bounds. **No DebugMenu edits.**

Semantics: `motion` plays source→target (Lunge moves the source; Tracer/Burst are pooled FX);
the tell's flash/punch/number remain the **impact payload** applied to `side`'s unit at contact.
Motion is orthogonal to `side`. `Crit` isn't known at `Attack` time — gold stays an
arrival/impact signal (the deferred `DamageDealt` tell), which is where it reads anyway.

### Authored tells (data only — no code per row)

| Signature | Motion | Reads as |
|---|---|---|
| `Attack` (fallback) | Lunge 0.14s, defer | melee swing; impact lands at contact |
| `Attack` + ranged | Tracer 0.15s white-hot, defer | arrow/shot; flash+number at arrival |
| `Cast` (existing cyan glow) | + windup 0.12s, defer | wind-up before the payoff |
| `DamageDealt` + cause=Ability | Tracer 0.15s cyan-arcane, impact deferred | *that* spell hit *that* unit |
| `AttackBlocked` | Tracer to wall hex + small fizzle Burst | shot wasted on the wall |
| `Death` | Burst (red-white, biggest) at corpse hex | death lands as a beat |

Heals: no new tell needed for binding — the Root latch already times the green flash/number to the
censer swing or cast. An ability-heal tracer would need a cause on `Heal` events (deliberately not
added now). Counter ripostes inherit the Attack lunge automatically (they ARE Attack events);
a `cause: Counter` flavored variant is a pure-data follow-on.

Color language stays law: white=hit · gold=crit · cyan=cast/arcane · green=heal ·
red=damage/death · purple=debuff. Tracers are the bright/emissive things — per render-polish,
saturated+bright is reserved for gameplay-critical VFX.

## 4. Sim-side change (small, headless-tested — the only one)

`TellMatch.Matches` gains the ranged filter: `bool? ranged, int? distance = null` — a
ranged-filtered rule matches only when `distance.HasValue && (distance >= 2) == ranged`.
`Specificity` counts it (+1). The client computes distance from **fold positions at dispatch
time** and passes it; events without two unit endpoints pass `null` (ranged-filtered rules then
don't match). Mirror the param style already used for `flavor`. Tests in `TellMatchTests`:
melee vs ranged precedence over the fallback, null-distance behavior, specificity ties.
**No event or replay format changes** — everything needed is already in the log.

## 5. Unity implementation notes (gotchas — read before coding)

- **Emissive = Unlit + HDR color, not `_EmissionColor` via MPB.** Enabling `_EMISSION` is a
  material-keyword operation; a MaterialPropertyBlock can't turn it on. For tracers/bursts use
  `Universal Render Pipeline/Unlit` with an HDR base color (`motionColor * motionGlow`, glow ≈ 3)
  through the existing `CachedMat` dictionary — over bloomThreshold 0.9 it blooms. This is
  render-polish's own "white radial at HDR intensity 3" trick.
- Spawn all FX under `_generated` (HideFlags.DontSave); pool with the `FloatingNumber`
  Create/Play/recycle-callback idiom (`_numberPool` is the template).
- **Loop reset** (`loop=true` wrap) and `ClearGenerated` must clear the timeline + latches and
  recycle in-flight FX (extend `ResetAnim`).
- **`BuildPreview` fast-forward:** after replaying the last 2 ticks' tells, call
  `_director.Tick` in fixed small steps for ~0.12 s so a frozen capture shows tracers mid-flight
  and arrival flashes partially decayed. Without this, motion tells are invisible to the
  render-to-PNG verification loop.
- **Re-read client files before editing** — the Windows box edits sync back via Syncthing
  ([[../Daily/2026-07-24]] gotcha #1). Write capture PNGs OUTSIDE the Syncthing tree.
- Handle `AttackBlocked`'s endpoint (hex, not unit) and `Death`'s position (the view's last fold
  position — the view object still exists, just inactive).

## 6. Extension seams (why this won't be rewritten)

1. **Finer signatures (data-only growth):** TellMatch context can grow caster-chassis filters —
   the client already has `PlaybackUnit.Name`. Per-*ability*/per-*weapon* identity would need the
   sim to stamp an id on `Cast`/`Attack` (an Aux slot) — flagged, not built; do it when a specific
   spell first needs a bespoke tell that chassis can't key.
2. **`prefabId` per tell:** when real VFX art arrives, a tell referencing a pooled authored prefab
   replaces the primitive tracer/burst *visual* while keeping the same anchors, timing, latch, and
   JSON authoring. This is the "great design per spell/weapon" seam: designers author a prefab +
   one JSON rule; zero system code.
3. **Timeline → beat sequencer → decoupled clock:** the ActiveFx list and latch are the embryo of
   render-polish's sequencer; hit-stop/slow-mo/blocking arrive with the clock refactor and consume
   the same tells.

## 7. Build order (each step compiles + is eyeball-verifiable alone)

1. **Sim:** TellMatch ranged filter + tests (headless, `make test`). 181 → ~185 green.
2. **Client:** FX timeline + MotionOffset + **Lunge** on the Attack fallback tell → capture a
   melee brawl (`stomp`), see attackers step into their hits.
3. **Tracer pool** + Attack/ranged tell + AttackBlocked fizzle → `duel-crit-vs-tank` / `skirmish`.
4. **Impact latch** → verify the victim flash/number now lands at tracer arrival, not launch.
5. **Ability tracers** (`DamageDealt`+Ability) + Cast windup+defer → `castfest`, `glyphwar`
   (status flashes like Taunt/Stun also inherit correct timing via the latch — free).
6. **Death Burst** (detached, pooled) → `stomp` deaths read as beats.
7. Author all tells in `tuning.json` · BuildPreview fast-forward · **`Assets/Editor/RenderShots.cs`**:
   a committed menu item (Warband → Render Contact Sheet) that BuildPreviews every fixture in
   `StreamingAssets/replays/` at a few ticks and renders `Camera.main` to PNGs **outside the
   Syncthing tree** (`%USERPROFILE%/warband-shots/`) — this makes tonight's ad-hoc render loop
   repeatable (closes that NEXT item) and is MCP-drivable via `ExecuteMenuItem`.

**Verification:** all existing tests green + new TellMatch tests · `make coverage F=<replay>`
tells you which fixtures exercise which signatures (ranged attacks, ability damage, deaths,
blocked shots) — pick capture ticks from it · two captures with different preview-advance values
prove motion · 0 CS errors via Editor.log or MCP console · new tell fields appear in F1
automatically (reflection cockpit) for Jake's visual tuning.

## Shipped 2026-07-24 (deltas from spec)

Built same-day by two Opus agents + review; see roadmap 4b + Daily/2026-07-24 session 3 for the
verification record. Spec deltas that matter to future readers:
- `BuildPreview` advance is a public `previewAdvanceSeconds` (default 0.12; ~0.25 shows cast
  children + death bursts — the windup latch sits exactly at 0.12, an off-by-epsilon found in review).
- Distance context requires both endpoints **present in the fold**, not alive — a dead unit keeps a
  valid Pos, and requiring alive would misread a same-tick ranged kill as melee.
- Cast windup v1 is delayed-release (silence → flash+launch at 0.12s), not a sustained glow;
  a during-windup source glow is a one-line follow-on if casts feel flat.
- AttackBlocked tell is authored with flash:false (fizzle spark only) but NO current fixture emits
  AttackBlocked — needs a walls scenario to exercise it.
- Watch-item: the Director snapshots ticksPerSecond for its speed scale — live battle-speed slider
  changes don't recompress motion until a tuning reapply.
