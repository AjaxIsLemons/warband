# UI review: unit-hud-readability

Status: IMPLEMENTED (P1/P3/P5/P6/P7 built + capture-verified + determinism-gated 2026-07-28;
P2 TMP outlines and P4 bar-contrast tuning remain open — per-piece detail in
`implementation/spec.md`)
Created: 2026-07-28

Serves **roadmap item 30 — combat payoff slice** ("make the answer readable and satisfying at the
real play camera"). Requested by Jake 2026-07-28: readability + style review of the in-combat unit
HUD (health bar, status row) and floating damage numbers; explicitly flagged damage-number
attribution ("probably color code by team a bit?").

## Brief

- Screen or flow: per-unit combat overlay (HP/shield/mana bars, status icon row, nameplate) +
  floating combat numbers, at the real play camera (ADR 0027: fov 34 · pitch 42 · aimBias 0.2).
- Primary player decision: mid-fight comprehension — who is winning, who is about to die, whose
  engine is producing the damage, what is disabling whom — feeding the post-fight build decision.
- Required information: HP absolute + trend, shield, cast readiness, active statuses + stacks +
  time left, damage instances with magnitude/crit/type, team attribution of all of it.
- Required states: 0.5×/1×/2× battle speed (item 30 acceptance), dense scrums (castfest ~6.3
  numbers/s worst case per sim-render-audit), frozen BuildPreview captures must reproduce.
- Target viewport: 1080p landscape primary; captures shot at 1600×900.
- Must preserve: fold owns bar truth (render-contract) · one Director clock, no Update/Random ·
  crit owns gold, magnitude owns brightness (ImpactTune) · every damage instance keeps its own
  number (lane schedule is the anti-overlap tool, never merging) · mana-ready flip (Underlords
  law) · tuning-live over recompile where possible · content doctrine (readability yes, balance no).
- May change: colors, sizes, bar anatomy, text rendering tech (TMP), attribution scheme, status
  row chrome.

## Inputs

| Source | Role |
|---|---|
| `current-castfest_t28.png` | dense scrum: unattributed numbers, cluster interleave |
| `current-statusstorm_t46.png` | enemy-bar contrast failure, status glyphs vs busy board, feed overlap |
| `current-duel_t14.png` | small-unit read at real camera; number/name collision |
| `current-glyphwar_t50.png` | text contrast failure over bright field tiles |
| `ReplayPlayer.cs` :2497–2583 (SpawnView), :972 (EnqueueNumber), :1971 (SpawnNumber), :2988 (fills), :3118 (SetFill) | bar/number mechanics |
| `StatusIconRow.cs`, `FloatingNumber.cs`, `TuningData.cs` (BarsTune/NumberTune/ImpactTune), `tuning.json` | current values |
| `Design/sim-render-audit.md`, `Design/fight-legibility.md`, `Design/render-polish.md` | prior laws + measurements |
| `work/hud-research.md` (+ `work/fct-raw.md`, `work/shots/`) | 2026-07-28 genre research, sourced |

## Current anatomy (code-accurate, 2026-07-28)

World-space primitives + legacy TextMesh, Director-stepped, deterministic; most knobs live in
`tuning.json`.

| Element | Implementation | Key values |
|---|---|---|
| HP bar | left-anchored cube fill over dark back bar | 0.9×0.13 world; ally `#59D959` / enemy `#E65C4D`; segment tick per 25 HP (cap 11) |
| Shield | second left-anchored fill IN FRONT of HP, same row | `(0.55, 0.80, 1.00)` light blue |
| Mana | thin bar below; color-flips `#598CF2`→`#E8F5FF` + pulse at full | keep — working as designed |
| Status row | procedural family glyphs, control leftmost @1.2×, countdown rings, stack dots / ×N, +N chip | `statusIconSize 0.22`, cap 5; single StatusColor authority |
| Nameplate | ships OFF (`nameplates.show=false`, audit A/B 2026-07-27) | white `#E0E0D1` when on |
| Numbers | victim-anchored TextMesh, rise+gravity, deterministic 2-lane schedule + outward/jitter splay, impact scale 0.6–1.6, crit = gold + ×1.4 + hot-white heavy tint | color = per-tell damage TYPE, no team info; no outline |
| Team identity | bar hue (green/red), ground disc (Team0 blue / Team1 red), board-half tile tint | nameplates off ⇒ these carry it all |

## Findings

- **F1 — numbers carry no attribution.** Nearly every damage tell is the same salmon family
  (`#FF8066`/`#FF8866`) for both teams; anchor is the victim. In a scrum (castfest: "17", "9",
  "16" floating between six bodies) you cannot tell whose engine produced what. Heals green
  `#8CFF99`, crit gold — those reads survive.
- **F2 — no outline on any world text.** Legacy TextMesh (TMP deliberately deferred,
  `FloatingNumber.cs:7`). Pale numbers die over bright field tiles (glyphwar: the "4"s over
  green/yellow) and over the red enemy half. fight-legibility's own clause: revisit TMP "if the
  numbers need better glyph quality" — they do now.
- **F3 — shield hides HP.** Both fills left-anchored; shield draws in front from the same origin,
  covering true HP (reads as "HP replaced"). Genre convention appends shield at the bar's tip.
- **F4 — enemy bar red-on-red.** `#E65C4D` fill over the red-tinted enemy half + red discs + dark
  backdrop (statusstorm: back-rank bars nearly vanish). Ally identity also split: green bar vs
  blue disc/tiles.
- **F5 — bars snap.** Fill jumps per fold tick; a big hit reads only through the number. Genre
  standard: a decaying trail segment showing what was just lost.
- **F6 — status glyphs float with no backing.** Strong system (families/rings/stacks) but colored
  glyphs blend into VFX behind them on a busy board (statusstorm).
- **F7 — probe captures overstate nameplate pain** — they forced `nameplates.show` on; shipping
  default is OFF per the 2026-07-27 A/B. Any style pass keeps them off or replaces; no restyling
  of the big white labels.
- **Adjacent (item 30, not this job):** statusstorm shows two story-feed lines superimposed
  top-right, on top of the known right-edge clip already folded into item 30.

## Assumptions

- PvE means the player's side is always Team0 — a player-perspective color law is available
  (D3/WoW style) rather than a symmetric team-paint law.
- Style target remains the current diorama look (dark board, saturated accents); this is a
  readability pass, not an art direction change.

## Research (full sourced report: `work/hud-research.md`)

Headlines that shaped the proposals:

1. **Zero of five autobattlers ship per-hit floating damage numbers** (TFT, Underlords, Auto
   Chess, Mechabellum, Super Auto Pets). All route damage into aggregate surfaces (recap, DPS
   tabs, hover panels). Warband already has that layer (combat recap + story feed) — its numbers
   are a deliberate identity choice (the broken-build fantasy IS watching the numbers), so the
   plan keeps them but adopts the genre's discipline: thresholds and suppression, never shrinking
   (WoW's Legion lesson: shrink non-crits and players report their damage "disappeared").
2. **Bar color IS the attribution scheme at this camera distance** — TFT/Underlords/Auto Chess
   are unanimous: ally green / enemy red on the bar itself, nothing else. Warband already
   complies. XAG 103 adds: never color alone — pair with a second channel (warband has discs +
   tile halves).
3. **D3 published its damage-number craft**: white = your damage, **red = damage you took**
   (attribution by perspective, not team paint) · crit = yellow + bigger · orange
   outlier-highlight with 3%/sec decay + warmup · DoTs time-bucketed at 0.5 s · whole damage
   classes suppressed entirely · **color chosen over size/motion because orange passes the
   colorblind test**.
4. **The double-outline trick** (XAG 102, For Honor): dark outline + light halo survives ANY
   background. In TMP: Outline + zero-offset Underlay with positive Dilate, one shared material,
   tint via vertex color, font asset padding ≥7 or the outline clips. Outline is free (1-pass
   SDF); size floor for transient text ≈ 46 px @ 1080p (IGDA); Bold + tabular figures.
5. **The delayed damage trail** ("damage trail" — no settled industry name): snap the real bar,
   pale trail drains on a `t²` ease-in (~0.8 s, the ease manufactures the hold), pin the trail's
   far edge and restart only the timer on repeat hits. Mechabellum shipped exactly this on 2 px
   hairline bars in 2025. Hazard: a cosmetic trail can read as recoverable HP — watch in play.
6. **Status rows want eligibility rules, not caps**: Underlords icons ONLY hard control (DoTs =
   body glow — warband's status-tint already does this); WoW shows your-debuffs-on-enemies /
   your-buffs-on-allies, whitelisted in game data, countdown hidden past 60 s. Warband's
   tiered row + control-never-cut already matches genre best practice.
7. **Subitizing (~4 items) governs tick density**, not 7±2. Segments exist for threshold
   recognition, not quantity estimation; LoL runs a two-tier ruler (fine/coarse) and strips ticks
   entirely from throwaway units. Warband's cap of 11 ticks is past the countable limit.
8. **Crit emphasis should be redundant across channels** (hue + size + motion): WoW's shipped
   values — 2× overshoot in 50 ms settling by 200 ms, and **crits are sticky** (pop in place
   while normals scroll) — are a proven recipe on top of warband's gold+scale.

## Proposals

Ordered by leverage. Cost classes: **T** = tuning.json-live (no recompile) · **C-** = small code ·
**C** = a session incl. capture regression.

### P1 — Victim-perspective number color (the attribution ask) — C-
**Hypothesis:** a number's hue answers "my damage landing" vs "my damage taken" before its digits
are read (D3's law, mapped to PvE where the player is always Team0).
- Damage **to enemies** (player output): keep the warm per-tell type family; crit stays gold.
- Damage **to allies** (incoming): override to one distinct hostile crimson (new tuning color);
  crit-on-ally = gold face, crimson-shifted (post-P2: crimson outline).
- Heals stay green everywhere; number position over the victim remains the redundant channel.
- Implementation: one branch in `ApplyImpact` (victim team is known there); per-tell
  `numberColor` remains the authoring surface for output damage. Deterministic, no new tech.
- Rejected alternatives: source-team paint (kills the type/crit hue law — crit owns gold);
  outline-only attribution (outline's job is contrast, and it needs P2 first).

### P2 — TMP swap + double outline for combat text — C
Dark outline + light zero-offset-underlay halo (XAG 102) on Bold tabular figures; one shared
material, crit/ally tint via vertex color; font asset baked with padding ≥7; keep world-space,
pooling, and Director-stepped animation exactly as-is (`FloatingNumber` internals swap TextMesh →
TMP; no Canvas, no Animator — both known failure modes). Closes fight-legibility's deferred "TMP
when glyph quality matters" clause. Status-row labels + nameplate inherit the font asset later.

### P3 — Shield renders at the bar tip — C-
Shield fill anchors at the HP fill's right edge and extends toward max (clamped at the bar end),
recolored to a pale grey-white so saturated blue stays mana's hue. Two lines in the ApplyFold fill
layout + a tuning color. Fixes the "shield hides true HP" misread.

### P4 — Bar contrast + tick density pass — T (plus two code constants)
- Enemy fill away from the board-half red: brighter/warmer (candidate direction `#FF7060`+) and/or
  desaturate `TileTeam1` a touch (code constant) so back-rank bars separate from their own turf.
- Check ally green against heal-green sameness (likely fine — both mean "ally-positive").
- `hpPerSegment`: keep 25 for hero-scale pools but respect subitizing — consider coarser ticks
  (or LoL's two-tier ruler) once boss pools push past ~8 ticks; cap 11 is past countable.
- All A/B'd on the four evidence captures at the real camera.

### P5 — Delayed damage trail on HP bars — C
Real fill snaps per fold (law intact); a pale trail quad drains from old tip to new tip on a `t²`
ease-in ≈0.8 s; far edge pinned, timer-only restart on repeat hits; no trail on heals v1. Driven
off fold deltas + the Director clock → frozen captures reproduce (same class as icon pops). This
gives bars the "what was just lost" read the numbers currently carry alone, and it is the correct
fix for small-delta-on-big-pool bosses (pixel quantization). Play-pass watch: does anyone read the
trail as recoverable?

### P6 — Status row backing chip — C-
One dark low-alpha rounded quad behind the row (StatusIconRow.Layout), sized to the shown slots.
Figure-ground for the glyphs over busy VFX; the procedural glyph system itself stays untouched —
research says its structure (control-first, never-cut, rings, stacks) is already genre-best.

### P7 — DECISION: the number diet
Genre evidence says numbers don't scale with unit count; warband keeps them as identity but can
adopt the discipline. Options:
- **(a)** as-is — every instance, current thresholds;
- **(b)** light diet (recommended): align per-tell number `minAmount` with the audio silence law's
  thresholds (same values, same rationale — chip damage that makes no sound prints no number) and
  time-bucket DoT ticks à la D3 0.5 s;
- **(c)** later, an options-screen "combat text: all / big only / off" toggle (item 9 seam
  exists; D3/D4 ship exactly this).
Any merge/bucketing ships with a per-tell opt-out (research: global merges destroy
"many-small-hits" ability identities — warband's no-merge law already protects this).

**Deliberately not proposed:** removing floating numbers (genre-native but against warband's
payoff fantasy; recap + feed already cover the aggregate layer) · WoW-style aura whitelist
rework (current tier system is close enough until play shows overload) · colorblind modes and
per-element toggles (options-screen batch, later) · any nameplate restyle (they stay off).

## Samples

| Sample | Hypothesis | Benefit | Risk | Literal vs illustrative |
|---|---|---|---|---|
| (on direction pick) `01-attribution-r1` — composited edit of castfest capture with P1 colors | perspective hue reads at a glance | attribution without new tech | crimson vs salmon too close at small sizes | colors literal, composited by image edit — not a render |
| (on direction pick) `02-bar-anatomy-r1` — composited edit of statusstorm capture with P3+P4+P6 | tip-shield + contrast pass + chip legible at back rank | fixes 3 findings in one look | enemy hue clash with crit gold | same |
| P2/P5 are motion/tech changes — honest samples are re-rendered probe captures post-approval, not mockups | | | | |

Mechanical fixes P3/P6 are objective enough to build straight from approval without a sample pass
if Jake prefers.

## Jake review

1. Preferred sample, combination, or reject all: **"I want all of these"** — P1–P6 approved as
   proposed, no sample pass required.
2. Must keep: every damage instance keeps its number (P7 options a/b/c all rejected — no
   suppression, no thresholds, no bucketing).
3. Most important next change: P7 becomes a **magnitude → presentation ramp** — small hits small,
   a bit see-through, short-lived; big hits opaque, longer-lived, more movement. Plus one more
   research pass: which games have the most renowned, visually pleasing damage-number systems.

## Approval

- Approved sample: proposals P1–P6 by name (no samples generated — approved from the written
  proposals + capture evidence); P7 in Jake's revised form above.
- Conditions: number styling values (P2 typography, P7 ramp, final palette) finalize after
  research round 2; all colors stay tuning-live for the capture A/B.
- Date: 2026-07-28.

## Review log

- 2026-07-28 — Job created.
- 2026-07-28 — Current-state audit + capture evidence filed; genre research in flight.
- 2026-07-28 — Research landed (archived `work/hud-research.md`); proposals P1–P7 filed;
  → AWAITING_REVIEW.
- 2026-07-28 — Jake approved P1–P6 wholesale; P7 revised to a magnitude→presentation ramp (no
  suppression); research round 2 (renowned damage-number systems) launched;
  → APPROVED_FOR_IMPLEMENTATION.
- 2026-07-28 — P1/P3/P6 code landed (`make check-client` PASS); P4/P5 queued; P2/P7 wait on
  research round 2. Visual verification pending the Unity lease — see
  `implementation/spec.md`.
- 2026-07-28 — Jake ruled LUMINANCE over opacity; P5 + P7 built; full verification run: 11 probe
  captures, attribution green-test + pixel proof, contact sheet ×2 byte-identical. Status →
  IMPLEMENTED for P1/P3/P5/P6/P7; P2 (TMP, needs editor font-asset session) and P4 (bar contrast
  A/B) remain; play-pass watches filed.
- 2026-07-28 — Research round 2 archived (`work/hud-research-round2.md` + `round2-primaries/`).
  P7 ramp spec proposed in `implementation/spec.md`; ONE decision open for Jake: research found
  zero precedent for opacity-by-magnitude in ~18 titles and recommends luminance instead
  (Hades' white→black law). P2 ready to spec against a font pick.
