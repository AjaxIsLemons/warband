# Genre research round 2 — best-in-class damage numbers + the magnitude ramp (2026-07-28)

Archived verbatim from the research agent. Primary sources it read (shipped Hades Lua, decompiled
RoR2 C#, WoW Lua, D4 string table) are in `round2-primaries/`. Round 1 is `hud-research.md`.

**Confidence key:** **[V]** verified, URL given · **[V-src]** read from shipped game source /
decompile · **[V-m]** measured frame-by-frame from 60fps gameplay footage (source clip + timecode
given) · **[U]** unverified/inference.

**Method caveat:** WebSearch budget exhausted early; Reddit/search captcha-walled. Agents
substituted primary sources — Hades' and Hades II's shipped Lua, decompiled RoR2
Assembly-CSharp, MediaWiki/XIVAPI/Steam News APIs, and yt-dlp+ffmpeg frame analysis. For motion
timings this is stronger than web sourcing, but the Genshin/HSR numbers are **our own
measurements, not published values**.

---

## 0. The reputation question, answered honestly: there is no canon

- GDC Vault has no "damage numbers" talk. [V]
- YouTube on the topic is an implementation-tutorial corpus; the game-feel canon (GMTK juice,
  Sakurai hit-stop) is about screenshake/hitstop/impact frames, not numbers. [V]
- r/gamedev etc. threads are about damage *formulas* and number bloat; presentation threads have
  near-zero engagement. [V]
- **"Juice It or Lose It" contains no UI, bars, or numbers** — verified against the caption track
  and the Juicy Breakout source repo (no HUD at all). [V]

**Two real craft sources exist:**

**Tim Cain** (Fallout/Arcanum/WildStar/Outer Worlds), ~9 min specifically on damage numbers —
https://www.youtube.com/watch?v=GTMdWT8Lb9k [V]:
> *"I will have a character hit another character and it says say **124** — no, I didn't do 124
> hit points of damage, I did **12 plus an additional 4**, but those numbers came up right next
> to each other and it looked just like 124."*
His shipped fix: encode damage type in **colour AND motion together** — fire wafts straight up in
orange, electricity zigzags off to one side in yellow, acid stretches and drips downward in
green, physical shoots off in a direction in white — *"because the colour and motion will be
different for different damage types… those numbers will move away from each other and you will
see that it was a 12 and a 4."* Also names the size-by-magnitude vs size-by-crit-band tradeoff,
against pure crit-band: *"if the person rolls a one for damage and it quadruples to a four, it
seems a little weird to see a big four."*

**Daniel Schuller, How to Make an RPG, "Polish: Combat Numbers"** —
https://howtomakeanrpg.com/a/polish-03-combat-numbers.html [V]: explicit **0.2 s apex pause**,
**full black border** so numbers read on any background, non-monospaced glyphs to avoid gaps.

**Bottom line: design from the measurements below, not from any game's reputation.**

## 1. Hades / Hades II — the deepest verified system (premise correction: Hades DOES show numbers)

All constants from shipped `Content/Scripts/*.lua`. [V-src, mirror:
https://raw.githubusercontent.com/xuqifzz/hades-mod-tutorial/master/Scripts/CombatPresentation.lua]

**⭐ Magnitude → SIZE via a 6-bucket lookup** (`GameData.DamageNumberTextScales`,
`HeroData.lua:8926`): 0–19 → **0.80** · 20–49 → 0.90 · 50–99 → 1.00 · 100–399 → 1.20 ·
400–999 → 1.40 · 1000+ → **1.60**. Effective ≈24 px chip → ≈56 px four-digit on a base of
`RandomInt(30,35)`. **The floor is 0.80, never lower, across three orders of magnitude.**
Per-effect override via `EffectData[x].DamageTextSize`.

**Typography** [V-src]: Hades I `AlegreyaSansSCExtraBold`, Hades II `P22UndergroundSCHeavy` —
small-caps display faces. `OutlineThickness = 1` black, `ShadowOffset = {2,2}`. **Crits get a
*blurred* shadow (`ShadowBlur = 2`) where normals get a hard one.** **No tabular figures — the
opposite:** every number randomizes its point size within its bucket (`RandomInt(30,35)`, crit
`RandomInt(56,62)`). Uniformity deliberately avoided.

**Motion** [V-src]:
1. Spawn at `OffsetY = HealthBarOffsetY or -180`, jitter `OffsetX = RandomInt(-10,10)`. **No
   scale overshoot on a fresh number.**
2. **Attached hold 0.25 s** (`CombatUI.DamageTextHoldTime`) — rides the enemy, stays mergeable.
3. Detach + drift `Shift({ OffsetX = RandomInt(-120,120), OffsetY = -50, Duration = 1.0,
   EaseIn = 0.99, EaseOut = 0.1 })` — **straight eased drift, no arc, no gravity.** Crit:
   `OffsetY = -100, Duration = 2.0`.
4. Shrink + colour tween at +0.1 s: normal `ScaleTarget 0.75 / 0.4 s`, colour → **black** over
   0.4 s. Crit `ScaleTarget 0.5 / 0.15 s`, colour → yellow over 0.5 s. **Crits pop big, collapse
   fast.**
5. Hold (normal 0.2 s, crit 0.45 s) → `FadeDuration 0.25` → destroy.
**Total lifetime: normal ≈1.05 s, crit ≈1.30 s** (Hades II double-damage ≈1.40 s).

**⭐ "Start light, end dark" — the cheapest legibility trick found anywhere.** Every normal
number colour-tweens white `{1,1,1}` → **black** `{0,0,0}` over 0.4 s while drifting; numbers
*self-extinguish* against bright VFX instead of accumulating as white noise. **A luminance ramp
over life, not an alpha ramp.**

**Colour means damage SOURCE, never magnitude** — nine Light→Solid god pairs (Zeus
255,250,165→255,243,45; Poseidon 0,216,255→0,138,255; Ares 180,30,0→0,0,0; …), plus normal
white→black, crit white→yellow.

**Crit = seven simultaneous channels**: ~1.7–2× size · yellow · literal `"!"` suffix · blurred
shadow · own SFX · own anchor (excluded from merging) · 2× rise over 2× duration · +0.25 s
lifetime.

**Merging + deterministic layout**: per-source coalesce inside the 0.25 s hold;
`numValuesPerRow = 3, spacerX = 65, spacerY = 40`,
`sign = (damageIndex % 2 == 1) and -1 or 1` — **alternating left/right of centre, stepping
outward, wrapping to a new row 40 px higher.** (Warband keeps its no-merge law; the layout math
is liftable regardless.)

**⭐ The only scale overshoot in the whole system is the merge tick-up** (`PulseCombatText`):
snap to 1.1×, 50 ms hold, 100 ms ease back — the juice budget spent on the number *changing*,
not appearing.

**Separate system for damage YOU take** (`DisplayPlayerDamageText`): ~5× font
(`RandomInt(170,180)`), red, `OutlineThickness = 3`, screen-space overlay, sized by **fraction of
max HP removed** (`sizeAdjust = max(PercentMaxDealt / HealthUI.MajorHitThreshold * 2, 1)`).

## 2. Risk of Rain 2 — damage numbers as particles

**Every number is one particle**; digits rendered in-shader from the `Custom1` vertex stream
(x=enabled, z=amount, w=crit). Zero GameObjects/Canvas/GC. [V-src, decompiled
`RoR2/DamageNumberManager.cs`] `maxDamageNums = int.MaxValue`.

**⭐ Team attribution is a one-line multiply:** `color * teamTint` — players `Color.white`,
monsters purple `(0.557, 0.294, 0.604)`, unowned grey — separating "damage I dealt" from "damage
dealt to me" across all 13 type-palette entries for free. [V-src, `RoR2/DamageColor.cs`]

Palette = damage-type identity, never magnitude (Default white, Heal green, Bleed red, Item gold,
Poison yellow-green, WeakPoint orange, Void pink, …). Crit is a shader flag. Rounds **up**,
abbreviates 10K/100K/1M, caps "990M". No stagger/merge/layout — deliberate maximalism; PC Gamer:
*"three hundred unreadable numbers layered on top of one another."*

## 3. Destiny 2 — the accumulation hypothesis is FALSE

Two independent frame analyses 8 years apart: sustained fire shows 5–7 simultaneous independent
numbers, each on its own timer; two body shots = two separate `26`s, never `52`. [V-m,
https://www.youtube.com/watch?v=69EQWVsiQQ4 · https://www.youtube.com/watch?v=zLkuebvwKR4] The
merge belief likely comes from the nameplate power-level badge.

**⭐ In Destiny 2, magnitude affects NOTHING** — a `98` and a `12` render identically; the only
channel is precision/crit → yellow-gold vs white. Thin condensed sans, no outline, soft shadow,
~0.5–1 s. No damage-number accessibility option exists (552 help articles enumerated). [V]

## 4. Warframe — the only shipped magnitude/priority ramp on LIFETIME

Update 33.6 "Enhanced Damage Numbers", verbatim [V, https://warframe.fandom.com/wiki/Settings]:
> *"…your **Critical Hits will be prioritized over smaller damage numbers.** Those smaller
> numbers will still appear but now **disappear sooner if higher-priority damage is done.**
> Additionally… Super Critical Hit Damage Numbers will linger longer than regular Critical Hits."*

**Not a fixed ramp — a bounded pool with priority-based eviction.** Small numbers always spawn,
never suppressed; shortened only under pressure. Settings: Show Damage Numbers
Enhanced/Legacy/Off · Damage Number Scale 50–300 · Compact (`100,357`→`100k`) · ally numbers
toggle. Colours: normal `#FFFFFF`, ability `#B195D2`, crit `#E0E93D`, orange crit `#DE6F22`, red
crit `#DE0F0F`, shields `#39B1CB` regardless of crit, invulnerable `#BABABA`; all recolourable
with protanopia/deuteranopia/tritanopia presets. [V] Melee numbers travel with the player/attack
motion rather than pinning at the (often occluded) contact point.

## 5. ⭐ Magnitude → opacity: NOBODY ships it

Across ~18 titles in both rounds, **no game modulates alpha by damage magnitude**. [V, absence]
What ships instead: SIZE (Hades buckets) · LIFETIME (Warframe priority; Hades crit +0.25 s) ·
LUMINANCE-over-life (Hades white→black). Opacity: none.

**Agent's assessment — drop opacity-by-magnitude:**
1. **Alpha already means "expiring"** — the universal learned fade-out signal. A number born
   translucent reads as already-dying, or as a rendering bug.
2. **It fights the outline** that fixes warband's F2: the double-outline works because both rings
   are opaque; alpha-blending glyph+outline drops contrast against light AND dark at once.
3. **Redundant** — small already = smaller + shorter-lived; a third channel on one fact while
   Cain's concatenation ambiguity and F1 attribution remain the real problems (they want colour
   and motion).
4. **Warband already has the correct version**: "magnitude owns brightness" (ImpactTune) is a
   luminance law; Hades corroborates luminance, never alpha.
**Recommendation: magnitude → size + lifetime + brightness; alpha reserved for fade-out.**

## 6. Motion timings worth stealing (measured)

| | Honkai: Star Rail | Genshin |
|---|---|---|
| spawn overshoot | ≥1.67× (occlusion floor, likely ~2×) | ~2.05× |
| overshoot decay | ~117 ms | ~133 ms |
| downward dip | **26 px over ~67 ms** (thrown down, then rises) | not observed |
| rise | 49 px / ~200 ms decel | 84 px / ~383 ms decel |
| fade | ~70–83 ms | ~65 ms |
| total | ~800–870 ms | ~870 ms |

[V-m, HSR https://www.youtube.com/watch?v=sk58nAiwk60 @5:00–5:25; Genshin
https://www.youtube.com/watch?v=gzyz8QCT3xI @5:20–5:50 — world-anchor caveat noted.]

**⭐ Overshoot budget scales inversely with density:** the 1.7–2× spawn punch lives in
turn-based/low-density contexts; **Hades, at action density, spawns at final size.** At
warband's ~6.3 numbers/s worst case, bias to Hades — spend overshoot on crits only.

Typography reality check: both HoYo games measured **flat fill + thin dark keyline** (~1/20 cap
height), no gradient/thick white stroke (HSR crimson `#F04060`, Genshin Pyro `#F09000`; Genshin
face = proprietary variant of HYWenHei-85W [V]). Genshin confirms **size = crit band, not
magnitude** — a grey `2396` rendered LARGER than a simultaneous orange `4182`. [V-m]

## 7. FFXIV — the best-specified escalation ladder

From Dalamud's `FlyTextKind.cs` [V]: `Damage` serif · `DamageDh` same + **"bounce effect on
appearance"** · `DamageCrit` **"larger serif font with exclamation," "bigger bounce"** ·
`DamageCritDh` **"even larger… 2 exclamations," "large bounce… Does not scroll up or down."**
Liftable: **punctuation as a redundant channel** (`1234!` / `1234!!` — free, colourblind-safe,
survives screenshots); bounce magnitude = tier; **the top tier stops scrolling** — longest dwell,
most stable position. Settings: Flying Text Size, Pop-up Text Size, and a 7-subject × 16-event
LogFilter grid whose first axis is WHO did it (chat log; on-screen gating **[U]**).

## 8. Monster Hunter — colour spent on something other than crit

Corrections: no "Add"/cumulative mode, no Simple/Detailed mode in World or Wilds — the only
Wilds axis is decimal precision. [V] **The real design axis: colour = hitzone effectiveness**
(orange effective / grey ineffective); crit is an **orthogonal glyph** — yellow diamond beneath
the number, blue diamond anti-crit, corner arrows for tenderized, blue/orange outlines for
blight/buff. [V, https://www.youtube.com/watch?v=Nzrx__9Ff-k] **One number carries five facts
without a colour collision** — the most relevant precedent for adding attribution on top of
warband's type-coloured numbers.

## 9. The rest

- **Borderlands spam reputation: unsourced [U].** Verified: same-tick simultaneous elemental
  applications merge into one number; staggered ones don't. [V,
  https://borderlands.fandom.com/wiki/Elemental_damage]
- **Dead Cells**: damage-number typeface is a **cosmetic unlock**, normal and crit fonts
  separately skinnable. [V, https://deadcells.wiki.gg/wiki/Outfits]
- **Brotato**: two colours only (white/yellow crit); numbers are particles. [V]
- **Vampire Survivors**: one hide toggle covers damage AND healing text. [V]
- **NieR: Automata**: nothing verified — asserting nothing.

# Distilled principles (round 2)

1. **Colour = source/type. Size = magnitude. Nobody colours by magnitude.**
2. **Hades' 6-bucket size table (0.8→1.6, floor 0.80)** is the shipped magnitude ramp — the
   floor is the practical answer to WoW's Legion lesson. Warband's 0.6 floor is below it.
3. **Lifetime is the underused channel** — Warframe does it as priority eviction (crits linger;
   small numbers shorten only under pressure), which fits "every instance keeps its number."
4. **Drop opacity-by-magnitude** (see §5). Recede via luminance, never alpha.
5. **Escalate crits on 3+ redundant channels**; punctuation is free and colourblind-safe.
6. **Overshoot ∝ 1/density** — at ~6.3 numbers/s, spawn at final size; overshoot crits only.
7. **Cain's concatenation ambiguity is warband's exact scrum failure mode**; his fix
   (motion-per-type) composes with the lane schedule. Hades' grid (3/row, 65×40, alternating
   sign) is liftable math.
8. **RoR2's `color * teamTint`** solves attribution without spending a new colour axis.
9. **~850 ms lifetime / ~70 ms fade** is a two-game measured convergence; Hades ≈1.05/1.30 s;
   crits buy ~+25%.
10. **Keep a non-floating aggregate channel** (warband: recap + story feed — already right).

## Gaps and corrections

- Corrected from round 1 / the brief: Hades DOES show numbers; Destiny 2 does NOT accumulate;
  MH has no cumulative/detail modes.
- Borderlands reputation and element hexes unsourced. Genshin/HSR timings are our measurements;
  HSR overshoot is a floor. HSR colour table unverified. RoR2 motion curves live in the
  ParticleSystem asset (not recoverable). Wuthering Waves/PGR/Diablo Immortal/NieR uncovered.
- Session WebSearch budget exhausted (200/200) — further rounds need the cap raised or must run
  on primary sources.
