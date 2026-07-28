# Genre research — unit HUD readability (hud-research agent, 2026-07-28)

Full report, archived verbatim from the research agent. Raw floating-text sub-report:
`fct-raw.md`; measured Steam screenshots: `shots/`.

**Confidence key:** **[V]** verified from a fetched source (URL given) · **[V-src]** read out of
shipped game source or datamined string tables · **[V-img]** verified by direct inspection of an
official screenshot · **[U]** unverified inference, flagged.

---

## 1. Teamfight Tactics

Closest analog, least-documented of the set — Riot has published almost nothing about the unit
plate. What is solid:

- **Health bar is team-colored, and that is the whole attribution scheme.** Your champions get a
  **green** bar, enemies **red**. No ground ring, no outline tint, no model recolor.
  [V, https://wiki.leagueoflegends.com/en-us/TFT:Champion]
- **Segmented every 300 health.** Fixed bar width; magnitude encoded by tick count, not length.
  [V, same]
- **Mana bar is blue and sits *above* the health bar** — thinner, same width. [V-ish,
  https://blogoflegends.com/2020/03/18/tft-teamfight-tactics-guide-beginners/2/ — ordering is
  moderate confidence, color solid.]
- **Star level is baked into the bar chrome, not a separate widget.** Zach Roberson (Riot UI
  designer, TFT unit frame): *"Unit health bars integrate the star level into their design to be
  informative, quick to recognize and make room for other game info like items equipped on the
  unit."* Also shipped *"a change to improve the readability of the item icons, made a couple sets
  into the release."* [V, https://zacharyrobes.com/teamfight-tactics-ui-design and
  https://www.behance.net/gallery/133371555/Teamfight-Tactics-UI-Design]
- 1-star = bronze tick at the bar's left end; 2-star silver, 3-star gold [V, blogoflegends —
  2020-era, frame redesigned since, **dated**]. A separate source says stars render beneath the
  model. Conflict unresolved — **flagged, not guessed**.
- **Up to 3 item icons** attached to the unit. [V, TFT:Champion]
- **No floating damage numbers.** Substitute is a **post-round combat recap showing damage dealt
  per champion**. [V for existence; **[U]** which set introduced it.]

**Could not verify for TFT:** shield representation on the bar; whether status effects get icons
at all (strong prior: no, VFX only — zero evidence either way); TFT-specific colorblind settings.

**Useful inheritance from LoL:** champion bars carry *"a mark for every one hundred health"* and
are *"segmented every thousand health"* — a **two-tier ruler**. Crucially, **minions and lesser
monsters get a small non-descriptive bar with no ticks at all.** [V,
https://wiki.leagueoflegends.com/en-us/Health] Riot spends tick-mark ink only where the player
makes decisions.

## 2. Dota Underlords

Richest case study — Valve visibly over-built the plate then spent four months subtracting.

**Frame** [V-img, official Steam screenshot]: left-anchored stack — **hero portrait icon at left
end**, thick **HP bar**, thinner **mana bar directly below** in blue on a dark track. Width
**fixed**; magnitude from Dota-style vertical ticks. Mana bar renders only for units with a
castable ability.

**Star pips sit centered *below* the bar stack**, and the single best line in the research:
***"Healthbars now only show stars for 2 and 3-star units"*** — 1-star units get no pip at all.
[V, https://store.steampowered.com/news/app/1046930/view/2448198215011453385]

**Attribution: the bar is colored, ally green / enemy red, no ground decal under units in
combat.** Unit outlines exist but are a separate silhouette toggle, not a team tint. **Colorblind
mode specifically for healthbars shipped June 2019** — *"Added colourblind mode for Healthbars"*
[V, https://store.steampowered.com/news/app/1046930/view/2436926440564425309]; exact colors
undocumented.

**Status effects — the transferable rule.** Icons dock to the healthplate (*"the 'broken' status
on the top right of the unit's HP bar"*), but **only hard control gets an icon**: Silence, Stun,
Disarm, Hex, Root, Break. DoTs and immunities get **body VFX only** — poison *"a soft green glow
on their sprite"*, fire *"a fiery red glow"*, magic immunity *"a golden glow"*. [V,
https://dotaunderlordsguides.wordpress.com/2021/03/09/status-effects/] Truncation count unknown
**[U]**.

**No floating damage numbers.** Replaced by a **live DPS table** (hotkeys P/O/I for
Alliance/DPS/Item tabs) where *"the different coloured bars represent the different sources of
damage — green is usually autoattacks, blue by abilities, and purple by items"*, readable
**during or after** battle. [V,
https://dotaunderlordsguides.wordpress.com/2021/02/17/damage-in-underlords/]

**The subtractive patch trail** [all V, Steam announcements]: *"Fixed treant health bar being too
high"* · *"Removed character outlines on PC"* · *"Healthbars are now consistently sized across
all resolutions"* · *"Tweak health bar size, especially on low resolution phones"* · *"Added an
option to disable hero icons on healthbars"* · *"Healthbars now only show stars for 2 and 3-star
units"*. Every fix hides an element, allows hiding it, or normalizes size. **None added
information.**

## 3. Auto Chess (standalone)

Cleanest segmented bar. Bar block inside a dark rounded track, ~one board cell wide: **HP on top
(thicker), mana below (thinner, cyan)**. **Ally green / enemy red on the bar.** [V-img]

**Segment count varies per unit** — high-HP units show many narrow chunks, low-HP few wide ones.
Fixed width, magnitude purely as chunk count. Corroborated by *"Adjusted the gaps in the HP bar"*
[V, https://ac.dragonest.com/en/announcement/detail/e731df9ee61]. **Star pips sit above the bar
block.**

No status icon row. But Auto Chess **does** use floating *event* text — a large white outlined
**"Evasion"** over a unit mid-fight [V-img] — words, not numbers. Damage attribution again
aggregate: *"Optimized the DPS panel presentation, with basic damage and the ability damage
displayed separately."*

## 4. Mechabellum

The far-camera case, most instructive on what breaks at distance.

**HP bars are ~2px hairlines** at 1080p, no chrome/border/portrait/name [V-img]. No mana bar.
**No star/tier pips in combat** — unit level (yellow up-arrow) and Tech glyphs render only in the
**deployment phase** and vanish when the camera pulls in.

**Attribution moved off the UI onto the world.** Primary signal is **team-colored model paint**;
the bar is too thin to carry it. When 4-Player Brawl made ownership ambiguous — *"I particularly
struggle in 4-player Brawl, where identifying unit ownership is difficult"* [V,
https://steamcommunity.com/app/669330/discussions/0/4336483106372029883/] — the dev fix was
***"Player colors will now be clearly marked on the map"*** shipped with *"The color and look of
the HP bars of units and core buildings have been slightly adjusted."* [V, Update 0.8.1.2,
https://store.steampowered.com/news/app/669330/view/5741605105609945145] **Ground decals, not
better bars.**

**They shipped a damage trail on a 2px bar**: *"**Unit HP Bar Visual Change** — Optimized the
visual of Units' HP bar, it now **highlights the most recent damage the unit has taken**."*
[V, Update 1.6.2, 2025-08-01]

**Confirmed: no per-hit damage numbers.** Absent from every combat screenshot and ~200 patch
notes. Two things *do* float, and the distinction is the point: **blue `+24` numerals over the
*player* portrait** — damage to strategic player HP, the number that changes a decision — while
per-unit damage lives in an out-of-combat hover panel. **[U]** A search summary claimed a "show
all health bars" toggle; unsourceable, likely wrong. Colorblind support requested 2024, never
shipped.

## 5. Super Auto Pets, and the card-layout family

**No health bars at all.** Each pet carries two chunky badges below it: gray badge with
**attack**, **red heart badge with health**. Numbers only, no bar, no max, no ratio. Above the
pet, a small "Lvl N" plaque with a thin yellow XP bar. [V-img]

**Attribution is environmental:** the battle screen splits down the middle — your half bright
green forest, enemy half dark red-brown cave. No bar colors, outlines, or rings needed. [V-img]

**Confirmed: no floating damage numbers** — badge numbers change in place, with a visual+sound
treatment that is itself **user-toggleable** (*"Changed visual and sound when a pet increases
another pet's health or attack **with a settings option to use the old one**"* [V, Golden Pack
update]). Battle is **scrubbable** (REWIND/PAUSE/AUTOPLAY/FAST/SKIP) rather than read live.

**Backpack Battles is the most extreme version:** replaces floating combat text with a
**timestamped, hoverable combat log** — *"9.65: Dealt **8** critical damage (Hero Sword)"* — with
**"Hover entries to jump back in time"**, plus per-item attribution
(`Hero Sword 51% — 35 (3.6/s)`). Crits are called out **by word in the log**, not by a floating
crit number. Buffs and debuffs get separate icon rows with **stack-count badges**. [V-img]

## 6. Diablo 3 — the one game that published its algorithm

Blizzard's 2016 dev blog "Engineering Diablo III's Damage Numbers" is the most directly stealable
artifact found. [V, https://web.archive.org/web/2016id_/http://us.battle.net/d3/en/blog/19996041/]

**The orange outlier-highlight algorithm, verbatim:**
- Damage must be **over 10,000** to be considered
- If the number is **larger than the last displayed in orange**, display it in orange
- **Decay the stored maximum by 3% every second** — *"reduces the likelihood that you'll go on
  too long without seeing any highlighted numbers"*
- **Ignore the first 10 large numbers** — *"allows the system to calibrate itself"*
- **If no damage for 10 seconds, reset**

They first tried "highlight the top 5% of recent numbers" and it failed: builds differ wildly in
hit rate, and temporary buffs permanently poisoned the baseline. **The 3%/sec decay fixes the
buff problem; the 10-number warmup fixes cold start.**

**Why color over size/motion/duration, verbatim:** *"We gravitated towards color because we could
present this new information in a drastically different way that passed other user accessibility
concerns… **Orange also passes the colorblindness-friendly test.**"*

**Colors:** white = your normal damage · **yellow + larger font** = crit · **orange** = largest
recent hit · red = damage you took · green with `+` and a source label = healing. (A search
summary claiming "blue = Overpower" in D3 is **wrong** — that's D4.)

**Batching: D3 does *not* merge discrete hits.** Three different tools instead — (a) **time-bucket
high-frequency sources at 0.5 s** (DoTs/channels hit 10–30×/sec, numbers pop every 0.5 s), (b)
**suppress a whole damage class entirely** (Area Damage splash *"do not even cause numbers to pop
up"*), (c) abbreviate + highlight the outlier. [V, https://diablo.fandom.com/wiki/Damage_Over_Time,
/Area_Damage]

**Abbreviation:** full digits with separators to 999,999, then K/M, **no B at all** — *"seeing
'1,000,000' is much more satisfying than '1M'… '1,000M' tells a much more exciting story than
'1B'."* `Display Long Floating Combat Numbers` is unticked by default, i.e. abbreviation is on.

**Options menu worth copying wholesale** — note **normal and critical damage numbers are separate
toggles**: Display Player Health Bars · Display Monster Health Bars · Display Health Bar Numbers ·
Display Healing Numbers · Display Damage Numbers ("floating numbers above monsters, showing
**normal** damage") · Display Critical Damage Numbers · Display Defensive Messages
(blocks/parries/dodges).

**Monster health bars are at top of screen, not over the monster.** Rank is triply encoded: name
color (white/blue/yellow/purple) + frame material (thin/silver/gold/banner) + **notch count**. The
notches are **loot markers, not phases** — *"the triangles represent points where they might drop
a globe."* Tick marks encoding a *reward schedule* is a neat trick.

## 7. Diablo 4

**Nine granular combat-text checkboxes**, from the game's own datamined string table [V-src,
https://raw.githubusercontent.com/tomrus88/d4stl/main/enUS_Text/meta/StringList/GameOptions.txt]:
Normal Damage · Critical Damage · Overpower Damage · Overpower Critical Damage · Vulnerable
Damage · Fortified Buff · Defensive Actions · Crowd Control Effects · Buff Effects. Above them,
`Combat Text Display Mode`: Display All / Hide Damage Numbers / Hide All.

**Colors:** white normal · yellow crit · **blue/cyan Overpower** · **orange = Overpower and crit
simultaneously**. Purple is *not* a number color — Vulnerable is a **purple aura wrapping the
enemy's health bar**.

**`Monster Health Bar Option`: Hover only / Always On / Always Off.** Two independent widgets: the
world-space overhead plate (governed by that setting) and a **top-center screen bar** for
hovered/targeted enemies and bosses (not governed by it).

**The doctrine, Feb 2023 dev stream** [V, https://www.pcgamesn.com/diablo-4/damage-numbers] —
Meng Song: *"the only way we can [increase monster defense] is through increasing HP… it ends up
with a super big number; billions, or even trillions. Because this number is so big, **it covers a
big chunk of the screen**."* Joe Shely: *"**combat in Diablo is really fast and you want to be
able to quickly understand how much damage you're doing. We want to keep the numbers down.**"* It
didn't hold — patch 2.0.3 **force-abbreviated everything with no opt-out**.

**Cautionary tale on overlay stacking:** D4 puts barrier, fortify, DoT, vulnerable and poison all
on the player's health globe. A player's complaint is the design review — *"a red line for your
health, a darker red line for your fortify then a contour when fortified, then purple cracks when
vulnerable, a green tint when poisoned and finally a blueish filter when you have a barrier."*
**Five overlays on one element and players find it illegible.** Only **three** enemy states get a
persistent icon (vulnerable / stunned / unstoppable); everything else is transient floating text.

## 8. Path of Exile — the principled refusal

**No damage numbers, ever, no toggle.** A full-text search of every poewiki page including all
patch notes for `insource:"damage numbers"` returns **zero hits**; requests span 2012→2026,
always unanswered. [V]

**Chris Wilson's rationale, verbatim** [V, https://www.pathofexile.com/forum/view-thread/335514]:
> *"We haven't changed our mind — we still feel it would be very bad for the game… Some
> contemporary Action RPGs did decide to spam the screen with numbers and 'Evaded!', but we feel
> that was massively detrimental to the gameplay. **We want players to look at what's going on in
> the scene — the size of the impact effects and what happens to the monsters, rather than have
> this display cluttered with numbers.**"*

**And the substitute, from Mark_GGG:** *"**Our on-hit damage effects do have different versions
which get bigger with damage**, for the record."* Impact-VFX magnitude replaces the numeral.
Directly relevant to warband's existing tell/recipe workflow.

Two more PoE patterns worth stealing:
- **Resistance icons show direction without magnitude** — *"the monster life bar interface
  indicates if a monster has positive or negative resistances… but the exact resistance value will
  not be visible."*
- **PoE2 replaced a number with geometry**: Heavy Stun buildup is *"a bar under the enemy's life
  bar"* with rarity-scaled thresholds. PoE1 decided stun per-hit and showed nothing.

**Verified negative:** GGG tried splitting energy shield onto its own segment of the player bar in
3.20.0 and **pulled it before ship** — *"This feature did not make its way through our testing
process successfully."*

## 9. World of Warcraft — the nameplate rules

Read out of shipped Blizzard Lua (`Gethe/wow-ui-source`, branch `live`). Most transferable
section for a status row.

**Floating text constants** [V-src, CombatTextConstants.lua]:
```
NumCombatTextLines    = 20     -- pool size
MessageScrollSpeed    = 1.9    -- seconds, full lifetime
MessageFadeOutTime    = 1.3    -- fade STARTS here
CriticalHitMaxHeight  = 60
CriticalHitMinHeight  = 30
CriticalHitScaleTime  = 0.05
CriticalHitShrinkTime = 0.2
StaggerRange          = 20     -- ±10px jitter
```
Lifetime **1.9 s**, alpha holds at 1.0 until **1.3 s** then ramps to 0 over the last **0.6 s**.
**Crit punch is a 2× height overshoot in 50 ms, settled by 200 ms.** **Crits are sticky** —
`endY = startY`, so they pop in place while normals scroll past, and they force vertical mode
even when the global setting is the arc. Three float modes: scroll up (**+225 px over 1.9 s ≈
118 px/s**), scroll down, or a **radius-150 arc** whose `xDir` **flips sign on every message** so
consecutive numbers alternate left/right. On column overflow past 130 px a non-crit restarts in a
second column offset ±80 px; **a crit ignores the overflow rule.**

**Pool exhaustion drops the *newest* message** — `AcquireFontString()` returns nil at 20 and
`AddMessage` silently returns.

**Nameplate buff/debuff filtering — the elegant bit** [V-src, Blizzard_NamePlateAuras.lua]:
```lua
self.DebuffListFrame.requireSourceIsLocalPlayer = isFriend == false;
self.BuffListFrame.requireSourceIsLocalPlayer   = isFriend == true;
```
**On an enemy, show only debuffs *you* applied; on an ally, only buffs *you* applied.** Asymmetric
by design. Three more filters stack: a **spell-data whitelist** (a spell must be flagged
nameplate-displayable centrally, in game data, not UI code), a `nameplateShowPersonal` flag, and
enemy *buffs* pruned to only `isStealable` or `IsSpellImportant` — *"Avoid filling up the list of
enemy unit buffs with information not relevant to the player."* Sorted important-first, capped
per container.

**Truncation is driven by icon scale, not a fixed count:** stride of **12 / 10 / 9 / 8 / 7 / 6**
icons per row as `nameplateAuraScale` goes 0.7 → 1.4 (default 1.0 → **8 icons**), wrapping to a
second line beyond. Stack counts show only when `applications > 1`.
**`hideCountdownNumbers = aura.duration > 60`** — no numbers on auras longer than a minute.

**Health bar color resolves in strict priority order:** threat color → grey 0.5,0.5,0.5 if
dead/disconnected → override → **class color** → light grey if tap-denied → reaction ramp (**red
hostile / orange unfriendly / yellow neutral / green friendly**) → red fallback. Friendly players
get pale blue 0.667,0.667,1.0 with the comment *"we don't want to use the selection color for
friendly player nameplates because it doesn't show player health clearly enough."*

**Two ideas worth singling out.** First, **everything that is neither target nor focus gets a
`deselectedOverlay` darkening pass** — *"Slightly darken the health bar of any unit that's not
the target or focus to make it easier to distinguish those states."* Second,
**`nameplateSimplifiedTypes`**: a per-category downgrade to a name-only plate for Minions / Minus
mobs / Friendly players / Friendly NPCs. **That is WoW's answer to "too many actors" — degrade
whole categories, not a global on/off.**

**Verified negatives:** no execute-range indicator and no quest indicator exist in Blizzard's
default nameplates; both are addon territory. And **I could not verify an engine-side
damage-number merge in WoW** — merging lives in addons (Better Combat Text hard-caps **20
concurrent numbers** and merges **>5 hits within 2 s** into `2.5K (7)`; MikScrollingBattleText
throttles DoTs at **2.5 s** and collapses AoE to one "Multiple" entry). Treat "WoW merges by
default" as **[U]/likely false**. What *is* built in is `floatingCombatTextCombatDamageAllAutos`
— *"Show all auto-attack numbers, **rather than hiding non-event numbers**"* — i.e. **WoW hides
some auto-attack numbers by default.**

**The Legion lesson** [V,
https://eu.forums.blizzard.com/en/wow/t/please-bring-back-old-floating-combat-text/219680]:
Legion made numbers smaller, arc-scattered and short-lived. *"Often I can't even see what damage
I dealt with a spell or attack **just because it wasn't a crit**."* **Making non-crits recede is
what makes players feel their damage disappeared.**

## 10. The delayed damage trail

**It has no settled name** — sourced, not a guess. Five substantial tutorials on the identical
effect each coin a different term and none cite each other: "chip away effect", "damage bar",
"damaged bar", "lazy health bar", "damage trail bar". One author states outright: *"I'm not sure
if this effect has a name."* [V, https://www.youtube.com/watch?v=9b23wgIDX2Y]

**Terminology trap: "chip damage" already means damage through a block** in fighting games.
**"ghost bar" / "ghost health" / "lag bar" are unattested** as terms of art. Recommend **"delayed
damage bar"** or **"damage trail"**.

**Shipped durations and easing:**

| Source | Hold | Drain | Easing | Repeat hits |
|---|---|---|---|---|
| Code Monkey (Unity) | 1.0 s | alpha ramp | — | **reset timer + alpha only**, not the fill |
| Natty GameDev (Unity) | none | ~2 s | `t²` quadratic **ease-in** | restarts drain |
| DashNothing (Godot) | 0.4 s | snaps | — | timer restarts, trail survives the combo |

**Two techniques worth stealing.** The **quadratic ease-in manufactures the hold** — a 2 s `t²`
spends its first ~0.6 s covering ~9% of the distance, which *reads* as a pause; one curve
replaces a timer plus a tween. And **pin the trail's far edge, restart only the timer** —
unanimous across implementations, so consecutive hits accumulate into one growing trail rather
than competing trails. On heal, **invert which bar leads** and recolor it.

**Scale check:** Vlambeer's hitstop is **20 ms**; Celeste's is **3 frames**. The trail runs
10–50× slower. **It is a readback mechanism, not an impact effect.**

**Design hazard:** if the trail is cosmetic, players will try to win it back. Bloodborne's Rally,
SF's provisional damage, P4U2R's Blue Health and Monster Hunter's red health are all
*mechanically* recoverable and players know the visual language. **A cosmetic trail accidentally
promises a mechanic.**

**Correction to a premise in the brief:** Guilty Gear Strive has **no** blue/white recoverable
life — its chip damage is permanent, and R.I.S.C. is a separate defense-reduction meter.

## 11. Chunking, and why the usual argument for it is wrong

**Shipped segment values converge on ~25 HP independently:** Overwatch bars are *"divided into
bars each worth 25 HP"* (health white, armor orange, shields light blue, overhealth green); Apex
*"each bar absorbs 25 points"*. Persona 4 Arena's SP gauge uses 25-SP stocks *"**to help players
recognize when they have enough SP**"* — **the clearest statement of why segments exist:
threshold recognition, not quantity estimation.**

**The real perceptual limit is subitizing, ≈4 items** — 40–100 ms per item inside that range,
**an additional 250–350 ms per item outside it** [V, https://en.wikipedia.org/wiki/Subitizing].
A 4-pip bar reads in ~0.2–0.4 s; an 8-pip bar costs roughly 4× because you've crossed from
parallel perception into serial counting. **This should govern warband's tick density**, not
Miller's 7±2 (wrong domain — working memory, not visual perception).

**The Weber-Fechner justification for segmenting high-HP bars does not hold up, and no source
makes it.** A fixed-width bar maps HP to position linearly. **The actual failure mode at high HP
is pixel quantization**: on a 300 px bar against a 40,000 HP boss, a 100-damage hit moves the
edge **0.75 px**. Segmentation doesn't fix that either. The fixes are stacked bars, floating
numbers (different modality), or **the damage trail — whose length is the delta rendered at full
bar scale rather than as an edge displacement.** Strongest argument for combining §10 and §11.

**Bars that deliberately lie** — worth knowing so you can decline: BBTag's bar is *"not exactly
linear, it's denser near the end"* for drama; GGST's Guts scaling makes *"a Life Gauge that
visually looks like it's 50% full actually [have] much more than 50% life left."* Both make
hits-to-kill unreadable from the bar. **Fine for a fighting game, wrong for a tactics game where
the player is planning lethal.**

## 12. Text legibility, culling, Unity specifics

**The double-outline trick is the only technique found that survives genuinely arbitrary
backgrounds** — Xbox Accessibility Guideline 102's *For Honor* example: symbols use **a black
outline plus a white outline**. *"The white outline ensures the symbols remain visible against
dark backgrounds… the black outline ensures they remain visible against light backgrounds."* In
TMP: Outline = dark, plus **Underlay with zero offset and positive Dilate** = light halo
(underlay as a second concentric ring, **not** a drop shadow). [V,
https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102]

**Measure contrast against the worst pixel, not the average**, per XAG 102: *"the text contrast
ratio should be measured between the text and **the lowest contrasting area of the background**."*

**Size floor:** IGDA GA-SIG specifies **46 px @ 1080p for "text that appears for a limited
time"** — damage numbers are by definition limited-time text, the closest thing to a direct
standard. XAG "large text" threshold is 36 px @ 1080p (PC/VR).

**Stroke weight:** DIN 1450 puts signage at **17–20% of x-height**. Damage numbers over hostile
backgrounds are functionally signage — **rules out Light/Regular, argues for Bold/Black**. Use
**tabular figures** so numbers don't shift horizontally as digits change.

**TMP gotchas** [V, Unity docs + shader source]:
- *"The outline is drawn on the text contour, **with half its thickness inside the contour and
  half outside**"* — a thick outline eats the glyph face; compensate with positive **Face >
  Dilate**.
- **Outline is essentially free** — `TMP_SDF.shader` is **1 pass**, outline computed inline,
  underlay a `shader_feature` in the same shader. No extra geometry or draw call.
- **The real cost is material count.** Tint crit-vs-normal via **vertex color / `TMP_Text.color`**
  on one shared material preset — never a material swap.
- **Outline width is bounded by the font asset, not the material:** Gradient Scale = Padding + 1.
  Community guidance is **padding ≥7 for "titles… which typically have larger outline, bevel and
  glow"**. Damage numbers are titles-class — **regenerate the font asset or the outline clips
  before it's thick enough.**
- `TMP_Text.SetText()` has **numeric overloads with zero GC allocation**, unlike string
  concatenation.

**The canonical Unity failure** [V,
https://discussions.unity.com/t/ui-optimization-hundreds-of-floating-damage-text/250517]: a dev
put a **Canvas inside the damage-number prefab**; a 300-actor AoE spawned 300 Canvases and
dropped to **40 FPS with freezes**. **Each Canvas is an independent rebuild island.** Damage
numbers want **one dedicated dynamic canvas**, separate from static HUD. Also: *"Animators will
dirty their UI Elements on every frame, even if the value does not change"* — **drive the
float/fade in code, not an Animator.** (Warband's world-space TextMesh path sidesteps the canvas
trap entirely; the Animator warning is already law here.)

**Unity's `ObjectPool<T>` will not do what you want:** `Get()` on an empty pool **creates a new
instance** — under load it silently degenerates into instantiate-and-destroy. `Release()` into a
full pool **destroys** the object. **Implement the overflow policy above the pool.**
Recycle-oldest is probably right for damage numbers (the newest hit is what the player is looking
at); Blizzard chose the opposite.

**Shipped culling rules:**

| Rule | Value | Source |
|---|---|---|
| Hard cap on concurrent numbers | **20** | Blizzard `NumCombatTextLines`; Better Combat Text independently |
| Burst aggregation | **>5 hits in 2 s → one entry** as `2.5K (7)` | Better Combat Text |
| DoT/HoT per-ability throttle | **2.5 s** | MikScrollingBattleText |
| Time-bucket high-frequency sources | **0.5 s** | Diablo 3 |
| Suppress a whole damage class | Area Damage spawns no numbers | Diablo 3 |
| Hide non-event auto-attacks | on by default | WoW |
| Bars only when selected or recently damaged, auto-hide | few seconds | Beyond All Reason |
| **Merge-exclusion list** | per-ability opt-out | MikScrollingBattleText |

**That last one matters most:** a global merge destroys abilities whose *identity* is "many small
hits". **Shipping a merge without a per-ability opt-out is a known failure mode.**

**De-overlap:** Blizzard staggers ±10 px and alternates arc direction per message. The
most-shipped approach is **temporal, not spatial** — Dragon Quest XI staggers multi-hit numbers
so players *"don't just see the numbers appear all at once"*; Ragnarok Online shows faint per-hit
numbers then a **yellow total**; Ex Nihilo rate-limits by **delaying, not dropping**. A **0.2 s
pause at the apex** of the rise is recommended [V,
https://howtomakeanrpg.com/a/polish-03-combat-numbers.html]. **[U]** No authoritative
arc-vs-vertical comparison exists; mechanically arc separates in X immediately (better
de-overlap), vertical reads better as a column of ticks over time — which is why WoW ships it as
a player choice with **crits forced to vertical-and-sticky in both modes**.

---

# Distilled principles

1. **No autobattler in this set shows per-hit floating damage numbers.** TFT, Underlords, Auto
   Chess, Mechabellum and Super Auto Pets are 5-for-5. Every one routes damage into an
   **aggregate panel** — TFT's post-round combat recap, Underlords' live DPS tabs, Auto Chess's
   DPS panel, Mechabellum's hover stats, Backpack Battles' scrubbable log. The only floating
   numerals found anywhere in the genre were Mechabellum's damage to **player** HP — the
   strategic number, not the tactical one. A small autobattler that tried it (MetaBattler)
   reported floating text helped *"the legibility of smaller fights"* while feeling **cluttered
   in larger battles**. **Damage numbers do not scale with unit count.**
2. **Fixed bar width + segment ticks is the genre's settled answer** to "does the bar scale with
   max HP". Underlords, Auto Chess, TFT (300 HP) and LoL (100/1000 two-tier) all do it. Keep tick
   counts near the **subitizing limit of ~4** where a fast read matters; Riot's corollary is to
   **strip ticks entirely from small, numerous units**.
3. **Team attribution splits by camera distance.** Close/mid → **color the bar** (TFT, Underlords,
   Auto Chess all green/red). Far → **color the model and add ground decals** (Mechabellum's
   explicit fix). Card layout → **color the background** (SAP). For an 8-wide hex board at
   Guildrun/TFT distance, bar color is the primary channel — but per XAG 103, *"color alone
   should never be used to represent information"*, so pair it with a second channel.
4. **Status icons want a whitelist, not a cap.** Underlords: **hard control gets an icon docked
   to the plate; DoTs and passives get body VFX only.** WoW: **your debuffs on enemies, your
   buffs on allies**, central spell-data whitelist, enemy buffs only if stealable-or-important,
   sorted important-first, stride 6–12 by icon scale, countdown numbers hidden above 60 s.
   **Decide eligibility in content data, not HUD code.**
5. **If you ship damage numbers, ship the outlier-highlight instead of the per-hit readout.**
   D3's algorithm is complete and tested: threshold, beat-the-last-highlighted, 3%/sec decay,
   10-number warmup, 10 s reset. The decay and warmup are the non-obvious parts.
6. **Crit emphasis should be redundant across channels.** WoW: 2× height overshoot in 50 ms,
   settled by 200 ms, plus stickiness. D3 used hue only, chose **orange specifically because it
   passes the colorblind test**. Given XAG 103, size+motion+stickiness is more defensible.
7. **Every readability lever these games shipped is a toggle, not a redesign.** Budget for the
   toggles early rather than retrofitting.
8. **Solve crowding by suppressing information, not by shrinking it.** D3 suppressed Area Damage
   entirely; WoW hides non-event auto-attacks and downgrades whole unit categories; GGG encodes
   magnitude in impact-VFX size; Beyond All Reason auto-hides bars. **Legion tried making text
   smaller and players said their damage had disappeared.**
9. **The delayed damage trail is the right fix for the real high-HP problem** — pixel
   quantization — because the trail's length is the delta rendered at full bar scale. Consensus:
   snap the main bar, hold 0.4–1.0 s (or `t²` ease-in manufactures the hold), pin the trail's far
   edge and restart only the timer on repeat hits, invert and recolor on heal. **If it's
   cosmetic, expect players to try to win it back.**
10. **Don't stack overlays on one element.** D4 put five states on one globe and players can't
    read it. Underlords' split — icon for control, body glow for DoT — is the better instinct.

## Gaps — what the agent could not verify

- TFT shield representation on the unit bar (LoL's grey-white overlay is the likely inheritance,
  unasserted).
- Whether TFT shows status icons on units at all (prior: no, VFX only). Worth 30 seconds of eyes
  on a TFT replay.
- TFT star-level rendering in the current client (sources conflict; Roberson's "integrated into
  the bar" is the claim to trust).
- TFT mana bar ordering (above vs below) traces to a single 2020 source.
- Exact hex colors for any autobattler.
- D4's damage trail (pixel-measurement inference from one screenshot).
- Mechabellum's "show all health bars" toggle (likely doesn't exist).
- Distance/off-screen culling for damage text — no shipped, citable example found.
