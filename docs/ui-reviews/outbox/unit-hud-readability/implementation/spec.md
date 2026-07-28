# Implementation contract — unit-hud-readability

Approved by Jake 2026-07-28: **all of P1–P6**, plus **P7 revised** — no suppression; every damage
instance keeps its number, presentation scales with magnitude (small = smaller, translucent,
short-lived; big = opaque, longer-lived, more movement). Final number styling (P2 typography, P7
ramp values, crimson/palette tuning) waits on research round 2 (renowned damage-number systems).

## Build order and status

| # | Piece | Status | Notes |
|---|---|---|---|
| P1 | Victim-perspective number color | **VERIFIED 2026-07-28** | `ApplyImpact`: `DamageDealt` on a Team0 victim → `numbers.allyHit`/`allyHitCrit` (tuning-live, `#FF4030`/`#FF6B47`). heavyTint output-only. Gold stays player-crit-only. **Proof:** green-test (allyHit=#00FF00 → the team0-victim "17" rendered green, statusstorm t22, victims cross-checked against a headless event dump) + on-screen family separation measured in one frame: output (200,173,158) vs incoming (204,121,104). Note: the post stack compresses saturated reds — authored #FF4030 renders ≈(204,121,104); still categorical on screen; deepen toward #E02818 live if play wants more. |
| P3 | Shield at the HP tip | **VERIFIED 2026-07-28** | `SetShieldFill`: appended at hp tip, truncated at bar end; min-sliver 0.06 keeps full-HP Overheal→Shield units visibly shielded. `bars.shield` pale grey-white. Visible on the statusstorm knight (z3 crop). |
| P6 | Status row backing plate | **VERIFIED 2026-07-28** | GroundFill quad (NOT Sigil/Ring — additive shaders cannot darken; caught pre-capture). Soft dark pill behind every row in all 11 verification captures. a=0.55 hardcoded. |
| P5 | Delayed damage trail | **VERIFIED 2026-07-28** | Pale sand drain (`bars.trail` #F5DEB8, `trailSeconds` 0.8) behind the fill, `t²` ease, far-edge pin + timer-only restart, HpFrac sentinel snaps first-fold/wrap. Visible mid-drain in skirmish t24 (z6). Play-pass watch: recoverable-HP misread. |
| P7 | Magnitude → presentation ramp | **BUILT + VERIFIED 2026-07-28 (Jake: LUMINANCE, not alpha)** | `impact.lifeFloor` 0.75 / `dimFloor` 0.72 / `endLum` 0.25 / `critPop` 0.5; `minScale` 0.6→0.78 (Hades floor law); `numbers.critBang` — crit renders "21!"/"24!" gold with crit-only pop (normals spawn at final size, Hades density law); alpha fade compressed to last quarter. Duel t18 shows the whole ramp in one frame (gold "21!" vs small dim "8"). Luminance-decay motion feel → play pass. |
| P4 | Bar contrast + tick density | **PENDING — capture-driven tuning session** | Enemy fill vs red board half (`#E65C4D` → warmer/brighter candidate); possibly desaturate `TileTeam1` (code constant). Tick cap 11 is past subitizing — revisit when boss pools land. |
| P2 | TMP + double outline | **PENDING — needs a Unity editor session (font asset bake)** | Dark outline + light zero-offset underlay halo; heavy weight; padding ≥7; ONE shared material, vertex-color tint; world-space, pooled, Director-stepped. Typography evidence: Hades small-caps heavy faces + 1px outline + hard shadow (blurred shadow = crit tell); HoYo flat fill + thin keyline. Tabular-vs-jitter: resolve at font pick. |

**Verification run (2026-07-28):** `make check-client` PASS ×2 · 11 probe captures over 7 fixtures
(this folder, `*_after.png` + `z*.png` crops) · attribution green-test + pixel histograms ·
determinism gate: contact sheet rendered twice, **all PNGs byte-identical** (MD5). Discovered in
passing: the sheet harness' NAMEPLATES_ON/OFF A/B pair hashes identical — that toggle is dead
(harmless; the A/B decision shipped 2026-07-27). Mirror fixtures cannot attribute numbers by eye —
pick probe ticks from a headless event dump (see `work/` notes / auto-memory).

## P7 ramp spec (proposed 2026-07-28, from research round 2 — `work/hud-research-round2.md`)

Magnitude `t = ImpactTune.Intensity(amount)` stays the one dial. Channels:

- **Size** — keep the continuous minScale→maxScale curve but **raise the floor 0.6 → ~0.78**:
  Hades' shipped bucket table never drops below 0.80 of base across three orders of magnitude,
  and that floor is the practical answer to the Legion "my damage disappeared" failure.
- **Lifetime** — add a low-end cut: effective life ≈ `lerp(0.75, 1.0, t) × lifeSeconds` before
  the existing lifeBoost, landing chip numbers ≈0.6 s and heavies ≈1.2 s+ (measured genre band:
  ~850 ms normal, crits +25%, Hades 1.05/1.30 s). Alpha fade compresses to the last ~quarter of
  life. Warframe-style priority eviction (small numbers shorten further only under pressure) is
  the v2 option if castfest still reads busy.
- **Brightness, not alpha** *(pending Jake — this replaces the "see-through" half of his ask)*:
  small hits spawn slightly luminance-dimmed at full alpha, and every number luminance-decays
  toward dark over its back half (Hades' white→black law) so expired-adjacent numbers
  self-extinguish against bright VFX. Alpha stays reserved for "expiring."
- **Motion** — **no spawn overshoot at warband's density** (Hades spawns at final size at ~our
  event rate; the 2× pops live in turn-based games). Overshoot budget goes to **crits only**
  (~1.5× pop ≈60 ms settling ≈200 ms — WoW/FFXIV band), plus optional `!` suffix on crits
  (free, colourblind-safe, survives screenshots). riseBoost stays the big-hit motion channel.
- **Determinism** — any per-number variety (Hades randomizes sizes; we can't) hashes off
  unit id + tick like unitJitter. No Random.
- **Recorded alternatives**: RoR2's `color * teamTint` multiply is the one-line attribution
  variant that preserves type identity on incoming numbers (current build uses the D3 override
  to crimson — stronger binary read; multiply noted in case capture A/B finds crimson too
  flattening). Cain's motion-per-damage-type (fire wafts, physical shoots, acid drips) is
  backlog ammo against same-tick concatenation ambiguity; composes with the lane schedule.

## Must-match vs illustrative

- Must match: the attribution LAW (incoming = crimson family, output = type colors, gold =
  player crit only) · shield-at-tip anatomy + min-sliver behavior · plate behind every non-empty
  row · determinism (no Random, Director clock, frozen-capture reproducibility).
- Illustrative until tuned in captures: every hex value (`allyHit`, `allyHitCrit`, `bars.shield`,
  plate alpha 0.55, P4 candidates) · plate pill proportions · P7 curve shapes.

## Verification ladder (per warband law)

1. `make check-client` — **PASS 2026-07-28** (57 scripts, 0 errors) for P1/P3/P6.
2. Unity lease → ReloadAndApply → re-render the four evidence fixtures (castfest t28,
   statusstorm t46, duel t14, glyphwar t50) → A/B against `current-*.png`, filed in this folder.
3. RenderShots contact sheet — only intended deltas (bar anatomy, number colors, plates).
4. Specific capture checks: GroundFill plate actually darkens + pill reads (shader assumption);
   crimson vs salmon separable at back-rank size; shield sliver on a full-HP shielded unit
   (statusstorm has Overheal→Shield); no z-fight plate/ring.
5. Item 30 acceptance: watched in motion at 0.5×/1×/2×.

## Conditions / open edges

- Concurrent session note: item 28's dead-view deletion was in flight in `client/` during this
  work; this slice touched only `ReplayPlayer.cs` / `StatusIconRow.cs` / `TuningData.cs` /
  `tuning.json` (disjoint set), compile checked green mid-surgery.
- The min-sliver overlaps up to 6% of the HP fill on a full bar — accepted lie, reviewed at
  capture time.
- Numbers colors may shift when research round 2 lands; the mechanism is settled, values are
  tuning-live.
