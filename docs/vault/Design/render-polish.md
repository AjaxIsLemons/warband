# Render polish & juice systems — design (2026-07-24)

How the Unity client goes from "capsules on a plane" to a good-looking, **readable** hex
autobattler — built as reusable SYSTEMS, not per-effect hacks, while still in PoC land.
Extends `render-contract.md` (the fold is the view-model; one canonical tell per event *signature*). Synthesized
from two research passes (spectator-juice + reference games; Unity 6.3 / URP 17 implementation).

## North star: this is a SPECTATOR battler
No mid-fight input — the player *watches* 6-8 units resolve. That inverts the action-game juice
playbook. Here juice does two jobs, in this priority:
1. **Comprehension** — narrate causality so the viewer reads *what happened and why* ("she died
   because *that* cast landed").
2. **Pacing** — sculpt a 15-40s fight into beats (rises + payoffs) so it plays like a fight
   scene, not a spreadsheet resolving.

**Readability & pacing beat raw punch.** Screen shake is spectacle, not information — it ranks
*below* telegraphs, causal-linking, and a consistent tell-vocabulary. A missed telegraph means
the viewer literally can't read the outcome; a missed shake just feels slightly flat.

## The load-bearing decision: a decoupled playback clock
The presentation layer runs its **own playback head** that consumes the event log. It is 100%
decoupled from sim determinism (the fight is already computed), so **all presentation timing is
free** — pausing, slowing, staggering, or reordering the *view* can never desync the sim.

- The head advances through ticks at a configurable **battle-speed** rate (a speed slider falls
  out for free), easing each unit's HP/shield/mana/position toward the **absolute post-state**
  every event already carries (client computes nothing — it eases toward truth).
- **Hit-stop** = pause the head for N real ms (`WaitForSecondsRealtime`) while decorative
  particles/shake keep animating on their own unscaled lifetime. **Never `Time.timeScale`** —
  that's a blunt global that couples juice to Unity's clock and fights this architecture.
- **Slow-mo finisher** = advance the head at 0.2× for a few hundred ms.

## The spine: a Feedback Director (event → tell)
Rendering splits into two layers over the same fold:
- **State layer** (exists): positions + bars + pips — "what is true now" from `PlaybackState`.
- **Tell layer** (new): as each event is *applied*, dispatch it to the **Feedback Director**.

Three parts, all hand-rolled (~1-2 files):
1. **Registry** — ~~a ScriptableObject mapping `EventKind`/status → a `FeedbackDefinition`~~
   **SUPERSEDED 2026-07-23/24 — built as JSON, and keyed on the SIGNATURE, not the kind.** Jake's
   call: config must be AI-editable text, not Inspector-only `.asset`, so the SO registry was
   deleted. The registry is now `StreamingAssets/tuning.json` (`tells[]`) → `TuningData`/`TellDef`,
   parsed with Newtonsoft (string enums + hex colors), hot-reloadable with **no recompile**
   (`TuningConfig.ReloadAndApply()`), plus an F1 in-game slider overlay that writes back to the same
   JSON. And a tell no longer keys on `EventKind` alone — it declares a kind plus OPTIONAL `Cause` /
   `StatusKind` / `FieldFlavor` filters, and the **most specific matching rule wins** (a filterless
   `DamageDealt` is the fallback; `cause: Burn` overrides it for burn ticks). That matching lives in
   `Warband.Sim.TellMatch` (headless-tested) so the Unity Director is a thin executor. Net effect is
   the same intent — designer/agent-editable, no code per new tell — at a finer grain, because
   "DamageDealt" alone was too coarse to tell a sword hit from a burn tick. See
   [[Projects/roadmap]] item 4b for the built state.
2. **Beat sequencer** — group events by tick into a **beat** (Hearthstone BLOCK). Play beats in
   order; within a beat, sub-events fire in emit order with tiny inter-event delays
   (Attack → delay → DamageDealt → StatusApplied → Death); the next beat waits on the current
   beat's **blocking** events (Death, big Cast). Chains read causally instead of popping at once.
3. **Pooling** — `UnityEngine.Pool.ObjectPool<T>` per definition. No per-event Instantiate/Destroy.

## The event-tell vocabulary (fixed, closed, one signature each)
The highest-leverage, near-free decision — a shared library so no hero "invents its own VFX."
**Color language** (one meaning per color, used nowhere else): white=hit · red=damage/death ·
gold=crit · green=heal · cyan=shield/buff · purple=debuff.

| Event | Canonical tell |
|---|---|
| Attack | lunge (out-and-back) with wind-up anticipation |
| DamageDealt | victim **white-flash** + directional impact spark + *thresholded* number; crit → gold, bigger, + hit-stop + shake |
| Cast | **LOUD**: caster wind-up pose + growing glow + channel; **visible projectile/beam** to target (travel time = the causal line); AoE ground **decal telegraph** before impact |
| Heal | green sparkle + green number |
| Death | **loudest beat**: hit-stop freeze + dissolve/poof + board desaturate-pulse + sound sting |
| StatusApplied/Expired | pip appear/disappear + brief burst, colored per status |
| ShieldChanged | cyan overlay flash on the bar |
| FieldCreated/Hex/Expired | pooled **decal** zone (pulsing, colored per field); walls = raised barriers |
| Leap | arc trajectory |

**Emphasis hierarchy (proportionality is the whole game):** chip = tiny flash only · solid =
flash + number + micro-shake · crit = big flash + gold number + hit-stop + shake · death =
loudest thing on screen · marquee cast = a *hero moment* (brief slow-mo + camera micro-push +
dim everything else). If deaths/ults don't dominate, the fight reads as uniform noise.

## Readability strategy for a dense fight
- **One tell per signature** (discipline — the registry enforces it). Amended 2026-07-24: the unit
  is the signature (kind + optional Cause/StatusKind/FieldFlavor), most-specific-wins — a burn tick,
  a sword hit and a crit are distinct tells; a healing glyph is not colored like a fire glyph.
- **Causal-linking:** visible projectile travel; victim flash timed to *arrival*, not cast start;
  hit-stop freezes attacker+victim together so they're visually bound.
  *Implementation spec 2026-07-24 → [[directed-tells]]:* motion tells (lunge/tracer/burst) +
  Root-keyed impact latch on the existing JSON tell system.
- **Focus/defocus:** slow-mo + vignette/desaturate on big beats; tilt-shift DoF permanently
  softens edges so the eye stays on the board.
- **Spatial stability:** fixed hexes + a per-unit cast/cooldown **ring** so the viewer can
  *anticipate* the next event (anticipation = readability moved earlier in time).
- **Per-actor stagger:** a few frames of jitter on cast starts so simultaneous casts don't
  cancel visually; hit-stop naturally de-clutters by inserting gaps.
- **Post-fight readout** (damage / MVP / "died to X") — the causality safety net for what the eye
  misses live; also the natural home for the "what wrecked me" story.

## Magnitude → spectacle (BUILT 2026-07-24) — `ImpactTune` in tuning.json
A hit's *size* must be felt, not read. One normalized intensity drives every channel, so
"bigger hits feel bigger" stays a single tunable idea rather than five unrelated hacks:

`t = clamp01(|amount| / bigHit) ^ curve` → then each channel is a lerp off `t`:

| channel | at t=0 | at t=1 | why |
|---|---|---|---|
| number size | `minScale` | `maxScale` | the primary read |
| launch speed | 1× | `1 + riseBoost` | big hits *leap* |
| hang time | 1× | `1 + lifeBoost` | big hits linger to be read |
| target recoil | tell's `punchAmount` | `× (1 + punchBoost)` | the victim sells the hit |
| color | tell's color | `tintAmount` toward `heavyTint` | heat |

**`heavyTint` is hot-white, deliberately not gold.** Crit already owns gold as a *categorical*
signal; magnitude is a *continuous* one. Keeping magnitude on brightness and crit on hue means a
big normal hit never lies as a crit — and a big crit is visibly both. Same reason the per-tell
`numberScale` still multiplies on top: a tell can stay quiet at any magnitude.

`bigHit: 40` is grounded in the real spread (all scenarios run ~1–54, bulk 5–30, ceiling 42–54),
so haymakers saturate rather than the curve wasting its range on values that never occur. Re-check
it whenever the stat law moves. Set `enabled: false` to collapse back to flat behaviour.

## Visual direction: tabletop diorama (TFT-style) — RECOMMENDED, pending Jake's nod
Both passes converge on this as *the* answer for this genre. Cheap, programmer-art-friendly, and
it fits "a warband arrayed on a board."
- **Post stack (URP Volume):** Tonemapping **Neutral** (not ACES — it crushes bright toy art) →
  **Bloom** (emissive-driven, HDR) → Color Adjustments (+sat) → Vignette → **Depth of Field
  tilt-shift** (the "miniatures on a table" read). Prereqs: HDR **on** the URP Asset + HDR color
  grading. Tilt-shift: built-in Gaussian DoF gets 80%; `keijiro/MiniBokeh` (free, MIT) for the
  premium look.
- **Lighting:** 3-point — key (~45-55°, soft shadows, warm) + cool fill (no shadows) + rim/back
  (separates units from board) — Gradient ambient, **APV** over the board for cheap moving-unit
  bounce. Tighten Max Shadow Distance to the board + 1 cascade → *sharp* shadows.
- **Renderer:** Forward+, MSAA 4× + **SMAA** (TAA smears on a slow iso cam), **SSAO** feature
  (contact shadows = grounding).
- **Emissive-for-bloom** is the programmer-art glow trick — a white radial at HDR intensity 3
  reads as a bright magic pop with zero art.
- **Compressed palette** (~8-12 colors), desaturated board/units; **reserve saturated/bright
  exclusively for gameplay-critical VFX** (bright = important — doubles as readability).
- **Silhouette-first** unit shapes (round = tank, spiky = DPS, tall = caster) — adopt as a
  constraint now, cheapest to get right while shapes are primitives.

## Tech stack — minimal dependency
**ADD:** URP 17 + Shader Graph · TextMeshPro (already in `com.unity.ugui`) · **PrimeTween**
(the one 3rd-party dep worth taking — zero-alloc, destroy-safe, deterministic; beats DOTween's
GC + footguns) · *optional* MiniBokeh (tilt-shift) · *optional* Cinemachine 3 (only if also used
for camera framing).
**HAND-ROLL:** Feedback Director + signature→tell registry (**built as JSON, not SO** — see above) +
beat sequencer · `ObjectPool<T>`
(built-in) · trauma screen-shake (~30 lines, magnitude = trauma², + a touch of rotation) ·
hit-stop via playback-clock hold · hex mesh (one combined static mesh) + **URP Decal Projector**
highlight/AoE pool · pooled world-space TMP combat text (**built with legacy `TextMesh` instead —
dependency-free; revisit TMP only if the numbers need better glyph quality**).
**SKIP:** VFX Graph (Shuriken is enough at our scale) · Feel/MMFeedbacks (fights the typed-event /
decoupled-clock architecture) · DOTween/LeanTween.

## Phased build order (each step independently shippable + visibly better)
1. **URP Asset config** — HDR on, HDR grading, Forward+, MSAA 4× + SMAA. Instant baseline.
2. **Volume stack** — Tonemap(Neutral) → Bloom → Color Adjustments → Vignette (~1 hr, highest ROI).
   Prove bloom by making one unit emissive-glow on cast.
3. **Lighting rig** — key/fill/rim, soft shadows, tight shadow distance, Gradient ambient, APV.
4. **SSAO + DoF tilt-shift** — the diorama read lands here.
5. **Hex board** — one combined static mesh + Decal renderer feature + a selection decal.
6. **Feedback Director skeleton** — registry + beat sequencer on the decoupled playback clock +
   pooling; wire ONE event (DamageDealt) end-to-end.
7. **Combat text** — pooled world-space TMP hung off the director.
8. **Particle FX** — additive/HDR emissive impact/cast/death via the registry.
9. **PrimeTween** — bar fills, popup punch, unit lunges, cast wind-up, death squash.
10. **Screen shake (trauma) + hit-stop (clock hold)** — wired into blocking events. Final crunch.

**First visible slice = steps 1-6** (diorama look + one event fully wired through the Director).

## Migration
Current `ReplayPlayer` is throwaway v0. It refactors: the **playback clock + Feedback Director**
replace its inline `Update`; `UnitView` becomes a proper rig (billboarded bars, pip strip,
anim states); the fold consumption stays (that part is correct). `WarbandSimSmoke.cs` can be
deleted once the Director exists. Sim side is untouched — all of this is client render layer
(pure C#, zero `UnityEngine` refs preserved).

## Top-8 highest impact-per-effort (if forced to prioritize)
1. Closed event-tell vocabulary (discipline, near-zero code) · 2. Hit-flash + color language ·
3. Graduated hit-stop (emphasis + de-clutter) · 4. Cast telegraphs/wind-ups · 5. Emphasis
hierarchy + rationed trauma shake · 6. Post-stack + compressed palette · 7. Springy motion
(squash/stretch + back-ease) · 8. Post-fight summary readout.
