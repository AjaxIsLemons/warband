I have everything. Here is the consolidated report.

---

# Floating Combat Text & Health-Bar Readability in Many-Actor Combat

**Confidence key:** **[V]** = I (or a sub-agent) fetched the source and read the claim in it, URL given. **[V-src]** = read directly out of shipped game source code. **[U]** = unverified / inference — explicitly flagged.

Method note: this session exhausted its WebSearch budget (200/200). Later work was done via `curl` against MediaWiki APIs, the GitHub API, Blizzard's Discourse JSON API, and Wayback. **The single highest-value technique was reading shipped source and datamined string tables rather than wikis** — Blizzard's own Lua and Diablo IV's string table gave exact constants that no article contains.

---

## 1. DIABLO 3

### 1.1 The dev blog — full text recovered [V]

`https://web.archive.org/web/2016id_/http://us.battle.net/d3/en/blog/19996041/engineering-diablo-iiis-damage-numbers-1-22-2016` ("Engineering Diablo III's Damage Numbers", 22 Jan 2016). Fetched whole via curl; Wayback is blocked to WebFetch but fine to curl.

**The framing, verbatim:**
> "We heard a lot of player feedback that combat numbers were starting to be overwhelming or difficult to comprehend in moment-to-moment gameplay. In the past, large numbers were exciting to see because they stood out above the rest. **How do you bring back that emotional appeal without making players feel like they have lost power?**"

**The orange "big hit" highlight algorithm, verbatim — the most directly stealable thing in this whole report:**
> - Damage numbers must be **over 10,000** to be considered
> - If the damage number to be displayed is **larger than the last that was displayed in orange**, then display this new number in orange
> - **Decay the value of the largest number by 3% every second** — "This reduces the likelihood that you'll go on too long without seeing any highlighted numbers"
> - **Ignore the first 10 large numbers** — "This allows for the system to calibrate itself"
> - **If no damage has been dealt for 10 seconds, reset the system**

They explicitly rejected the naive version first: *"Initially, we had the idea of highlighting the top 5% of numbers you've generated in the last few seconds"* — it failed because builds differ wildly in hit rate, and because temporary buffs (Power Pylon) permanently poisoned the baseline. **The 3%/sec decay is the fix for the buff problem; the 10-number warmup is the fix for the cold-start problem.**

**Why color and not size/motion/duration, verbatim:**
> "Do we make these numbers bigger? Have them path differently? Make them flash? Do they hang on the screen longer? Or do we just give them a different color? … **We gravitated towards color because we could present this new information to our players in a drastically different way that passed other user accessibility concerns.** Orange numbers stand out; they're not something you've seen before, so subconsciously you pay close attention to them. **Orange also passes the colorblindness-friendly test.** When you're looking at a color wheel, orange is in a different realm than the other colors we currently use."

**Their HSL model for UI color decisions, verbatim:** *"Do we want to change the mood? Then we adjust **hue**. If we're shifting an image from being juicy to more flat, then **saturation** becomes key. How about drawing attention or driving it away? **Lightness** becomes the go-to."* And the application: *"some of the most critical information in the game is how much healing you're receiving… that's why that information is some of the **brightest** in the game."*

**Abbreviation, verbatim:**
> "In English, we opted not to abbreviate in the low millions because seeing **'1,000,000' is much more satisfying than '1M.'** Skipping the billions place also helped with this, as seeing **'1,000M' tells a much more exciting story than '1B.'**"

So D3's English ladder is: full digits with separators up to 999,999 → `K`/`M` thereafter → **no `B` at all**, it keeps counting in `M` past 1,000M. They use ICU for locale-correct separators and keep a per-locale truncation table; Korean groups every 4th place (myriad-squared), Spanish/French have no native "billion".

### 1.2 Damage-number colors [V]

| Color | Meaning |
|---|---|
| **White** | Normal damage you deal |
| **Yellow** (+ larger font) | Critical hit — `https://diablo.fandom.com/wiki/Critical_Hit` |
| **Orange** | Largest recent hit, per the algorithm above (2.4.0+) |
| **Red** | Damage taken by the player |
| **Green** (+ `+` prefix and a source label, e.g. `+2571 / Health Potion`) | Healing |

⚠️ **Correction:** a search summary claimed D3 blue = Overpower. **That is wrong** — Overpower is a Diablo IV mechanic; D3 has no blue damage number. Note also that Blizzard's own blog is slightly loose: the algorithm is magnitude-based, but the blog's screenshot caption says "Look at that beautiful crit!", so orange is best understood as a *magnitude* highlight that mostly lands on crits.

### 1.3 Batching / merging — the actual answer

**D3 does NOT merge discrete hits.** It buckets only tick-rate sources:

- **[V]** DoTs and channelled skills: *"target will suffer a hit **10 to 30 times per second (combined numbers popping up every 0.5 seconds)**"* — `https://diablo.fandom.com/wiki/Damage_Over_Time` (Fandom 402s to WebFetch; use `?action=parse&prop=wikitext&format=json`).
- **[V]** Area Damage splash is **suppressed entirely**, not merged: *"Attacks boosted by Area Damage do not have any special graphics, and **do not even cause numbers to pop up**"* — `https://diablo.fandom.com/wiki/Area_Damage`.
- **[V]** The orange algorithm's own wording ("the last that was displayed", "ignore the first 10 large numbers") only makes sense over a stream of discrete numbers.

**D3's three-tool kit against number spam:** (a) time-bucket the high-frequency sources at 0.5 s, (b) fully suppress a whole damage class, (c) abbreviate + highlight only the outlier. **It never summed normal hits.** [U] Numbers do not accumulate in place — no growing/ticking number.

### 1.4 The actual D3 Options menu — `Esc → Options → Gameplay` [V]

Column **HUD** (tooltips are the game's own text):

| Label | Tooltip |
|---|---|
| `Display Player Health Bars` | "Show player health bars above heroes" |
| `Display Monster Health Bars` | "Show monster health bars above monsters" |
| `Display Player Names` | — |
| `Display Health Bar Numbers` | "Display numerical health values on player health bars" |
| `Display Healing Numbers` | "Display floating health numbers above heroes showing healing effects on player" |
| `Display Damage Numbers` | "Display floating numbers **above monsters**, showing **normal** damage dealt by the player" |
| `Display Critical Damage Numbers` | "…showing **critical** damage dealt by the player" |
| `Display Defensive Messages` | "…showing blocks, parries and dodges" |
| `Always Show Item Labels on Drop` / `Item Label Display` | — |

Column **INTERFACE** ends with the 2.4.0 addition: **`Display Long Floating Combat Numbers`** — un-ticked by default, i.e. **abbreviation is ON by default**. `https://gaming.stackexchange.com/questions/252853/` · patch note: `https://news.blizzard.com/en-gb/article/19998542/patch-2-4-0-now-live`

Note the design: **normal and critical damage numbers are separate toggles.** Keybinds `V` (monster health bars) and `D` (player health bars) toggle bars live. Even with bars on, PC monsters **must be hit first** before a bar appears — `https://us.forums.blizzard.com/en/d3/t/24089`.

### 1.5 Elite / boss bars [V, incl. screenshot measurement]

**Monster nameplates live at the TOP OF SCREEN, not over the monster.** Official Game Guide: *"While you're attacking a monster, you'll see **a red bar with its name at the top of your screen**."* Bashiok, 2008, on why they cut floating bars: *"Health bars for uniques and bosses are displayed at the top, and health bars for everything else are displayed at the bottom (both stationary)."* — `https://www.purediablo.com/diablo-iii-monster-health-display-changed-2`

Anatomy: **NAME** (rarity-colored small caps) → framed health bar → **affix names as plain space-separated small-caps text, no icons** (or a title for uniques).

| Tier | Name color | Frame | Notches |
|---|---|---|---|
| Normal | White | thin floating bar | — |
| Champion | **Blue** | silver filigree | 1 (center) |
| Rare | **Yellow** | **gold** filigree | 3 (quarters) |
| Unique / Boss / Rift Guardian | **Purple** | gold unfurled-scroll banner, ~1.4× wider | 3 |

**The notches are NOT phases — they're loot markers.** Official: *"The triangles in monsters' health bars represent **points where they might drop a globe**"* (Champion 60% chance at 50% life + on death; Rare 100%) — Wayback of `us.battle.net/d3/en/game/guide/gameplay/combat-skills`. That's a nice trick: the tick marks encode a *reward schedule*, not a difficulty schedule.

Affix count scales with hero level: 1 / 2 / 3 / 4 at levels 1-29 / 30-49 / 50-59 / 60+ — `https://diablo.fandom.com/wiki/Monster_Traits_(Diablo_III)`.

**Player damage-taken feedback:** red numbers over the hero, plus a **low-health red vignette** over the outer ~25-30% of frame with a pulsing heartbeat sound. Official: *"When your Life gets dangerously low, you'll see a red halo encompass your screen."* [U] threshold disputed (10% vs 25%); [U] no per-hit directional damage flash; [U] no option to disable the vignette. **No delayed/ghost health trail on D3 bars** [V-img].

---

## 2. DIABLO 4

### 2.1 The best D4 source is the datamined string table [V]

`https://raw.githubusercontent.com/tomrus88/d4stl/main/enUS_Text/meta/StringList/GameOptions.txt` — the game's own option labels and tooltips, verbatim. Use this instead of guides.

**Master toggles:**
| Label | Verbatim description |
|---|---|
| `Show Combat Text` | "Enables/Disables the display of all damage numbers and statuses" |
| `Show Damage Numbers` | "**Enabled:** All damage dealt by the player is displayed as floating numbers on screen. **Disabled:** Hides all damage numbers." |
| `Combat Text Display Mode` | `Display All` / `Hide Damage Numbers` / `Hide All` |

**Section `Combat Text Options` — exactly nine checkboxes:**
`Normal Damage` · `Critical Damage` · `Overpower Damage` · `Overpower Critical Damage` · `Vulnerable Damage` ("Enables/Disables **messages** displaying when targets are made vulnerable") · `Fortified Buff` · `Defensive Actions` · `Crowd Control Effects` · `Buff Effects`

⚠️ **There is no checkbox for DoT, healing, resource, or pet/minion damage** — confirmed by absence in the string table *and* by a Nov-2024 feature request asking for exactly those (`https://us.forums.blizzard.com/en/d4/t/204356`).

**Timeline** [V]: 1.2.0/S2 (Oct 2023) added the coarse all-or-nothing mode → **1.5.0/S5 (Aug 2024) added the nine granular checkboxes** ("Settings for Floating Combat Text have been added to allow players to further customize what sort of floating text they want to see") → **2.0.3/S6 (Oct 2024) force-abbreviated all numbers with no opt-out**: *"Combat text for damage numbers are now abbreviated. e.g. 10000 damage now displays as 10k damage."* Blizzard called it *"a 'small potatoes' sort of change… Minimizing screen clutter and dialing back on visual noise in combat should allow players to have a greater sense of exactly what's going on."* Players objected that D3 made it optional.

### 2.2 D4 damage colors [V]

**White** = normal · **Yellow** = crit · **Blue/cyan** = Overpower · **Orange** = Overpower **and** crit simultaneously. `https://us.forums.blizzard.com/en/d4/t/203037`

⚠️ **Purple is not a damage-number color.** Vulnerable is a text callout plus **a purple aura wrapping the enemy's health bar** (≈RGB 68,45,127 → 85,51,172), and a cracked health globe on the player. [U] no verified color for DoT or healing numbers — consistent with them having no options either.

The full callout vocabulary is datamined in `Callouts.txt` and is a useful word list: `Dodge! Block! Parry! Deflected! Absorbed Immune! Immobilized! Stunned! Feared! Frozen! Slowed Taunted Dazed! Weakened Vulnerable Unstoppable! Fortified Crushing Blow! Overkill! Executed! +N Stagger` plus `Broke Vulnerable! / Broke Root! / …`.

### 2.3 D4 monster health bar rules

**Exact setting** [V]: `Monster Health Bar Option` → `Hover only` / `Always On` / `Always Off`, tooltip "Configure the display mode for monster health bar." **[U] default not verified anywhere** — strong inference is `Hover only`, since an entire genre of "how to always show monster health bars" tutorials exists.

**The key structural insight: there are two independent widgets.** (A) the world-space overhead plate, governed by that setting; (B) a **top-center screen HUD bar** for the hovered/targeted enemy and for bosses, which is **not** governed by it — a player on `Always Off` still reports *"I still see enemy health bars at the top of the screen when my mouse is over them"* (`https://us.forums.blizzard.com/en/d4/t/23712`).

**Elite plate anatomy** [V-img]: name (parchment serif) → optional title → **orange level number in a dark diamond badge** left of the bar → health bar with pale steel frame, red fill, **3 tick marks quartering it** → **row of affix ICONS only** (no names). The top-center version shows `Name  (Elite)  ·  59`, is wider, has **no quarter ticks**, and shows **icon + name** pairs. Tags that exist are exactly `Elite` / `Champion` / `Minion`. There is **no star or skull glyph** and **no D3-style tier color coding** — and players complained: *"I wish the name tags and HP bars were gold or even red. **Silver just blends in with everything.**"* (`https://us.forums.blizzard.com/en/d4/t/6222`). Devs reportedly *"wanted to be more subtle in order not to break immersion but they went too far."*

**World boss bar (Ashava, measured from screenshot)** [V-img `https://www.icy-veins.com/d4/guides/ashava-world-boss-guide/`]: top-center, ornate dark frame, red on black. Title `ASHAVA, THE PESTILENT   25`. **Segmented at 20/40/60/80% (5 phases)**; an unreached breakpoint is a **red downward chevron** on the bar which **flips upward and turns grey** once passed. Each breakpoint drops healing vials and escalates the fight. A **gold/pale stagger bar sits directly beneath** the health bar, split by a central skull crest; when full the boss is helpless ~12 s and a **blue rapidly-draining bar replaces it**.

**Barrier/Fortify are on the player's health GLOBE, not as bar overlays** [V `https://web.archive.org/web/20250121092158/https://www.wowhead.com/diablo-4/guide/gameplay/health-globe-ui`]: Barrier = light purple fill + heavy blue-purple ring; Fortify = darker opaque red mask + bright red ring, plus a **spiky iron ring** when Fortify > Health (Wowhead calls the ring "in some ways a more important visual cue than the darker red mask"); DoT = a **yellow band inside the globe** that shortens as it expires. **There is no debuff bar in D4 — the globe is the status display.** A player's warning is worth heeding: *"a red line for your health, a darker red line for your fortify then a contour when fortified, then purple cracks when vulnerable, a green tint when poisoned and finally a blueish filter when you have a barrier"* — five overlays on one element, and players find it illegible.

Only **three** enemy states get a persistent icon under the bar: **vulnerable / stunned / unstoppable**. Everything else is transient floating text.

### 2.4 The "smaller numbers" doctrine [V]

From the 28 Feb 2023 D4 developer livestream, quoted at `https://www.pcgamesn.com/diablo-4/damage-numbers`:

**Meng Song (Principal Game Designer):** *"we have a problem that, whenever we want to increase the monster's defensive power, the only way we can do it is through increasing the monster's HP… If you do this from level one to level 100, it ends up with a super big number; **billions, or even trillions**. So, because this number is so big, **it covers a big chunk of the screen**."*

**Joe Shely (Game Director):** large numbers are *"hard to understand"*; *"**combat in Diablo is really fast and you want to be able to quickly understand how much damage you're doing. We want to keep the numbers down.**"*

Their mechanism: **monster Armor replaces raw HP inflation as the difficulty lever.** It did not hold — a Wowhead commenter in Oct 2024: *"Less than 2 years and D4 already reached D3 level of damage"*, hence the forced abbreviation.

### 2.5 Ghost/trail bar in D4

**[U — high-confidence inference from pixel measurement, no text source anywhere.]** In a Gamepressure HUD screenshot an elite mid-burst shows a **three-zone fill on both its overhead and top-center bars simultaneously**: bright red current → **a dark red band** → black empty, split ~43/31/17% with a **hard edge** (rules out gradient/compression artifact). A non-elite in the same frame, damaged but not currently being hit, shows no band. Two independent widgets, same proportions, transient, tied to just-taken damage — that is the signature of a delayed drain trail. Treat as strong inference, not fact.

---

## 3. PATH OF EXILE 1 & 2

### 3.1 PoE1: no damage numbers, no toggle, ever [V]

Four independent lines: a full-text search of **every** poewiki page including all patch notes for `insource:"damage numbers"` returns **zero hits**; the Options page's UI tab lists nothing about combat text (`https://www.poewiki.net/wiki/Options`); Maxroll's options guide never mentions them; and a **Feb 2026** forum request titled *"QoL Request: Damage Numbers on the Boss Health Bar (Already Exists in PoE2)"* confirms they're still absent (`https://www.pathofexile.com/forum/view-thread/3912031`). Requests span 2012 → 2026, always unanswered.

### 3.2 PoE2: still no toggle, but bosses get a number on the static bar [V]

`insource:"Damage Numbers"` across poe2wiki returns one unrelated hit. **But** two independent forum posts confirm a number renders on the boss's static health bar, and the 0.5.0 patch notes corroborate the mechanism: *"The Debuff is now a Life Loss effect instead of secondary hit damage, and **the Debuff amount is now displayed on static health bars.**"* (`https://www.poe2wiki.net/wiki/Version_0.5.0`)

**The pattern: GGG will print a number, but only on the persistent boss bar — never floating in the world.**

⚠️ The popular story that *"Jonathan Rogers changed his mind after seeing Elden Ring"* is **unverified folklore** — it appears only in SEO/likely-AI-generated articles; it is absent from both major Rogers interviews on Maxroll. [U] Also unresolved: whether the boss-bar number is a DPS rate or cumulative damage.

### 3.3 The rationale — Chris Wilson [V, second-hand transcription with source link]

Quoted on the official forum at `https://www.pathofexile.com/forum/view-thread/335514`:

> **"We haven't changed our mind — we still feel it would be very bad for the game."**
> "I honestly don't feel we've 'disabled' or are 'missing' this feature. Traditionally, Action RPGs did not have it (as a conscious design choice)… Some contemporary Action RPGs did decide to spam the screen with numbers and 'Evaded!', but we feel that was massively detrimental to the gameplay. **We want players to look at what's going on in the scene — the size of the impact effects and what happens to the monsters, rather than have this display cluttered with numbers.** In addition, due to lag, it's not simple to get accurate numbers to even be known to the client at the right point in time."

Three separable arguments: genre convention, **attention direction**, and netcode honesty.

**And the design answer to "then how do I read damage?" — Mark_GGG, verified via the forum's staff-post filter** (`https://www.pathofexile.com/forum/view-thread/16472/filter-account-type/staff`):

> **"Our on-hit damage effects do have different versions which get bigger with damage, for the record."**

**GGG's substitute for the number is impact-VFX magnitude.** GGG staff have never replied in any PoE2 damage-numbers thread (verified by running the staff filter on four of them — all empty).

### 3.4 Bars, rarity colors, and how PoE reads magnitude without text

**Rarity colors are the game's own shipped constants** — poewiki's `Common.css` is headed *"Colors exported from Metadata/UI/UISettings.xml"*, and poe2wiki's values are byte-identical:

| Rarity | Hex |
|---|---|
| Normal | `#C8C8C8` (**not** pure white — 200-grey) |
| Magic | `#8888FF` |
| Rare | `#FFFF77` |
| Unique | `#AF6025` (**burnt orange-brown, not orange**) |

Damage types from the same table: fire `rgb(150,0,0)`, cold `rgb(54,100,146)`, lightning `rgb(255,215,0)`, chaos `rgb(208,32,144)`.

**Static boss bars are recent in PoE1** — 3.25.0 (July 2024): *"Act and Pinnacle Bosses now have static life bars."* Eleven years of over-monster bars only. PoE2 has them from the start.

**Energy shield split — a verified negative result.** GGG tried splitting ES onto its own segment of the player's mini bar in 3.20.0 and **pulled it before ship**: *"This feature did not make its way through our testing process successfully, and will be re-considered at a later date."* [U] The exact ES overlay color and layering on monster bars is **undocumented** in both wikis — no image assets, no stated values.

**Information attached to the bar:**
- **Resistance icons show direction without magnitude**: *"The monster life bar interface indicates if a monster has positive or negative resistances… but **the exact resistance value will not be visible**."* A good pattern.
- Non-damaging ailment icons render **under** the life bar; PoE2 0.2.0 shipped *"Improved the visibility and readability of text and icons under monster life bars."*
- Rare-monster auras render as plain text lines below the name.
- **PoE2's big move: replace the number with a filling bar.** Heavy Stun buildup is *"a bar under the enemy's life bar"*, with rarity-scaled thresholds (40/50/60/70% for normal/magic/rare/unique). PoE1 decided stun per-hit and showed nothing; PoE2 turned the same information into visible geometry.

**Screen shake history — the whole arc is instructive** [V]:
| Patch | Note |
|---|---|
| 0.9.6 | "Added screen-shake on **very large attacks** or dramatic events such as bosses dying. **It can be disabled in the options.**" |
| 0.9.13 | "Modified screen shake so that it'll cause **less headaches**." |
| 3.11.0 | "improved how Screen Shake effects (**such as those played on a critical hit**) are handled, **toning down the wild variance and ensuring you can always still see the action**." |

Gated to large hits and crits, dialled back twice for comfort, **player-disableable since 0.9.6**.

⚠️ **Correction:** "Always Highlight" is **not** a life-bar option — it permanently highlights ground items and world objects, bound to `Z`. Verified by a Support-tagged reply at `https://www.pathofexile.com/forum/view-thread/607828`.

---

## 4. WORLD OF WARCRAFT — the deepest section

I read **shipped Blizzard Lua** from `Gethe/wow-ui-source` branch `live` (currently 12.0 / Midnight). Everything below marked **[V-src]** is a constant read out of that code, not a wiki paraphrase.

### 4.1 Blizzard's own scrolling combat text — every constant [V-src]

`Interface/AddOns/Blizzard_CombatText/Shared/CombatTextConstants.lua`:

```lua
NumCombatTextLines    = 20     -- font-string pool size
MessageScrollSpeed    = 1.9    -- seconds: full lifetime
MessageFadeOutTime    = 1.3    -- seconds: fade STARTS here
MessageHeight         = 25
CriticalHitMaxHeight  = 60
CriticalHitMinHeight  = 30
CriticalHitScaleTime  = 0.05
CriticalHitShrinkTime = 0.2
StaggerRange          = 20
LowHealthThreshold    = 0.2
```

Decoded from `CombatText.lua` / `CombatTextUtil.lua`:

- **Lifetime 1.9 s.** Alpha stays 1.0 until **1.3 s**, then ramps linearly to 0 over the last **0.6 s**.
- **Crit punch: text height ramps 30 → 60 px in 0.05 s, then shrinks 60 → 30 px over the next 0.15 s** (i.e. by t=0.2 s it's back to normal size). **A 2× overshoot in 50 ms, settled in 200 ms.** That is the single most concrete "juice" number in this report.
- **Crits are sticky**: `fontString.endY = startY`, so crits **do not scroll** — they pop in place while normals travel past them. Crits also force `StandardScroll` even when the global mode is the arc.
- **Three float modes** (`floatingCombatTextFloatMode`): `1` = scroll **up** (y 384 → 609 on a 1024×768 reference, i.e. **+225 px over 1.9 s ≈ 118 px/s**); `2` = scroll **down** (384 → 159, same distance); anything else = **FountainScroll**, an arc of **radius 150** driven by `cos/sin` over a quarter period — `xPos = startX - xDir*(150*(1-cos(90*t/1.9)))`, `yPos = startY + 150*sin(90*t/1.9)`. **`xDir` flips sign on every message**, so consecutive numbers alternate left/right.
- **Stagger:** flagged types get `fastrandom(0, 20) - 10`, i.e. **±10 px horizontal jitter**.
- **Column stacking with overflow:** new messages anchor 16 px + `textSpacing` (10 px × scale) below the lowest active message. If the column exceeds `textOffsetMax = 130 px × scale`, a **non-crit** restarts at the top in a **second column offset ±80 px × scale**; a **crit ignores the overflow rule** and takes the anchor position anyway.
- **Pool exhaustion = drop the message.** `AcquireFontString()` returns nil when the 20-slot pool is empty and `AddMessage` silently returns. **Blizzard's culling rule is "drop the newest", not "recycle the oldest".**

**Message colors** (`CombatTextTypeInfo`, r/g/b floats) [V-src] — note this is *self*-centric text (what happens **to you**), which is why damage is red here:

| Type | RGB | Note |
|---|---|---|
| `DAMAGE`, `DAMAGE_CRIT`, `MISS/DODGE/PARRY/EVADE/IMMUNE/RESIST/BLOCK/ABSORB` | `1, 0.1, 0.1` | red |
| `SPELL_DAMAGE`, `SPELL_DAMAGE_CRIT`, `DAMAGE_SHIELD` | `0.79, 0.3, 0.85` | **purple — spell damage is a distinct hue from melee** |
| `HEAL`, `HEAL_CRIT`, `PERIODIC_HEAL`, `ABSORB_ADDED`, `SPELL_CAST` | `0.1, 1, 0.1` | green |
| `ENERGIZE`, `RUNE`, `COMBO_POINTS`, `HONOR_GAINED`, `FACTION` | `0.1, 0.1, 1` | blue |
| `SPELL_ACTIVE` (reactive proc) | `1, 0.82, 0` | gold |
| `INTERRUPT`, `SPELL_DISPELLED`, `EXTRA_ATTACKS`, `SPLIT_DAMAGE`, all `SPELL_MISS/DODGE/…` | `1, 1, 1` | white |

Only a handful default to `show = 1`; the rest are gated behind individual CVars.

### 4.2 The damage numbers over enemies are engine-side, and the merge story

The world-space damage numbers over targets are **not** in Lua — they're C++, driven by CVars. The relevant ones, with Blizzard's own descriptions from `Stanzilla/AdvancedInterfaceOptions/cvars.lua` [V]:

- `floatingCombatTextCombatDamage` — the master for target damage numbers
- **`floatingCombatTextCombatDamageAllAutos`** — *"Show all auto-attack numbers, **rather than hiding non-event numbers**"* ← **this is an explicit built-in culling rule: by default WoW hides some auto-attack numbers.**
- `floatingCombatTextCombatDamageDirectionalScale` / `…DirectionalOffset` — *"Amount to offset directional damage numbers when they start"*. Setting `DirectionalScale 0` is exactly what the **WodCombatText** addon does to convert Legion's scatter-arc back to a clean vertical scroll — *"By default in Legion, damage arcs all over the place… scroll up instead of arc"*, and it makes autoattacks *"the same size as your spell/ability damage"* (`https://www.wowace.com/projects/wodcombattext`).
- `WorldTextScale` — *"The scale of in-world damage numbers, xp gain, artifact gains, etc"*
- `floatingCombatTextCombatLogPeriodicSpells`, `…PetMeleeDamage`, `…PetSpellDamage` — separate toggles for DoT ticks and pet damage.

**The Legion FCT rework and the complaints** [V `https://eu.forums.blizzard.com/en/wow/t/please-bring-back-old-floating-combat-text/219680`]: Legion made numbers smaller, arc-scattered, and short-lived. Player complaints, verbatim: *"Often I can't even see what damage I dealt with a spell or attack **just because it wasn't a crit**"* and *"I love the addicting nature of **stacking numbers** being thrown in your face whenever you go on a rampage."* No Blizzard response in the thread. **The lesson: making non-crits recede is exactly what makes players feel their damage disappeared.**

**⚠️ On "does WoW merge damage numbers":** I could **not** verify an engine-side merge for discrete hits. What is verified is (a) the `AllAutos` culling above, and (b) that *addons* are where merging lives — **MikScrollingBattleText** merges AoE across targets into one event labelled "Multiple" with cumulative damage plus normal/crit counts, throttles DoT/HoT per-ability at **2.5 s default**, merges off-hand into main-hand, and ships a **merge-exclusion list** so a specific multi-hit ability stays unmerged. **Better Combat Text** hard-caps **20 concurrent floating numbers** and merges **>5 hits within 2 seconds** into one entry rendered as `2.5K (7)`. Those are addon behaviours, not Blizzard's. [U] Treat "WoW merges damage numbers by default" as unverified.

**A structural finding [V-src]:** in current retail, the Options UI exposes **only one** FCT control — `enableFloatingCombatText` ("Enable Floating Combat Text") in `Mainline/CombatOverrides.lua`. There is **no FloatingCombatText settings file** anywhere in `Blizzard_SettingsDefinitions_Frame`. The ~25 per-type toggles still exist as CVars but were dropped from the settings panel in the Dragonflight rework.

### 4.3 Nameplates — health bar color [V-src]

`CompactUnitFrame_UpdateHealthColor` (`Blizzard_UnitFrame/Shared/CompactUnitFrame.lua`) resolves color in strict priority order:

1. **Threat color** (tank spec, `ShouldUseThreatHealthBarColor`)
2. **Grey `0.5,0.5,0.5`** if disconnected or dead
3. `healthBarColorOverride`
4. **Class color** if the unit is a player (or `UnitTreatAsPlayerForDisplay`) *and* the class-color option is on
5. **Light grey `0.9,0.9,0.9`** if tap-denied (someone else's kill)
6. **Selection/reaction color** via `UnitSelectionColor` — this is the classic **red = hostile / orange = unfriendly / yellow = neutral / green = friendly** ramp. Two special cases: `considerSelectionInCombatAsHostile` forces **pure red `1,0,0`** for any non-friend on your threat list, and friendly human players get **`0.667, 0.667, 1.0`** (pale blue) with the comment *"We don't want to use the selection color for friendly player nameplates because it doesn't show player health clearly enough."*
7. Fallback **red `1,0,0`**

**Class-color CVars:** `ShowClassColorInNameplate` (the "V key" option) and `ShowClassColorInFriendlyNameplate`.

**Target/focus distinction** [V-src, `Blizzard_NamePlateHealthBar.lua`]: the target gets a `selectedBorder` in `NAMEPLATE_BORDER_TARGET_COLOR`, focus gets `NAMEPLATE_BORDER_FOCUS_TARGET_COLOR`, and — the good bit — **everything that is neither target nor focus gets a `deselectedOverlay` darkening pass**: *"Slightly darken the health bar of any unit that's not the target or focus to make it easier to distinguish those states."* Border colors from `NamePlateEnemyFrameOptions`: selected `(1,1,1,.9)`, soft-target `(1,1,1,.4)`, **tank `(1,1,0,.6)`**, default `(0,0,0,1)`.

**Health text** is off by default and driven by a bitfield `nameplateInfoDisplay` with **`CurrentHealthPercent`**, **`CurrentHealthValue`**, **`RarityIcon`**. Large values use `AbbreviateLargeNumbers` (`capNumericDisplay = true`).

### 4.4 Nameplate buff/debuff icons — the exact filtering rules [V-src]

`Blizzard_NamePlateAuras.lua` (Midnight rewrite). This is the most transferable part.

**Four separate containers** on one plate: `DebuffListFrame`, `BuffListFrame`, `CrowdControlListFrame`, `LossOfControlFrame`.

**The source-ownership rule flips by hostility** — the elegant bit:
```lua
self.DebuffListFrame.requireSourceIsLocalPlayer = isFriend == false;
self.BuffListFrame.requireSourceIsLocalPlayer   = isFriend == true;
```
i.e. **on an enemy, show only debuffs *you* applied; on an ally, show only buffs *you* applied.** That is the "only show my own" rule, and it's asymmetric by design: your debuffs on them, your buffs on them, never anyone else's noise.

**Three further filters stack on top:**
1. **Spell-data whitelist.** Both filter strings include `AuraUtil.AuraFilters.IncludeNameplateOnly` — a spell must be flagged nameplate-displayable **in the game's spell data** to be eligible at all. Blizzard curates eligibility centrally rather than in the UI.
2. **`nameplateShowPersonal`** must be true on the aura, or the debuff is rejected outright.
3. **Enemy *buffs* are aggressively pruned:** *"Avoid filling up the list of enemy unit buffs with information not relevant to the player"* — an enemy buff is shown only if `aura.isStealable` **or** `C_Spell.IsSpellImportant(spellId)`.

**Sorting:** buffs sort **important-first**, then by `auraInstanceID`; debuffs and CC use `AuraUtil.DefaultAuraCompare`. Rendering iterates the sorted priority table and **stops at `listFrame.maxAuraItemsDisplayed`** — a per-container hard cap.

**Truncation is driven by icon size, not a fixed count** [V-src] — `NamePlateAurasMixin:UpdateAuraScale` sets how many icons fit per row:
| `nameplateAuraScale` | Debuff row stride |
|---|---|
| ≤ 0.71 | **12** |
| ≤ 0.81 | 10 |
| ≤ 0.91 | 9 |
| ≤ 1.01 (default 1.0) | **8** |
| ≤ 1.21 | 7 |
| else (≤1.4) | **6** |

with the comment *"As the size increases, any debuffs beyond the stride will wrap onto a second line."* Base template stride is 10; `AURA_ITEM_HEIGHT = 25` px.

**Per-icon rules:** stack count shown only when `applications > 1`; a cooldown swipe with `forceShowDrawEdge`; and **`hideCountdownNumbers = aura.duration > 60`** — *"Don't show numbers for auras longer than a minute."*

**Nameplate geometry constants** [V-src]: `LARGE_HEALTH_BAR_HEIGHT = 20`, `SMALL_HEALTH_BAR_HEIGHT = 10`, `HEALTH_BAR_FONT_HEIGHT = 12`, `LARGE_CAST_BAR_HEIGHT = 16`, `SMALL_CAST_BAR_HEIGHT = 10`, `CAST_BAR_FONT_HEIGHT = 10`, `CAST_BAR_ICON_HEIGHT = 12`. Size presets scale horizontal/vertical/classification/aura together: Small 0.75/0.8, Medium 1.0, Large 1.25, ExtraLarge 1.4, Huge 1.6.

### 4.5 The full Midnight nameplate options panel [V-src]

`Blizzard_SettingsDefinitions_Frame/Nameplates.lua`. Every option below is real, in-code, today:

- **Visibility:** `nameplateShowAll` (Ctrl+V), `nameplateShowEnemies` (+ sub-toggles Minions, Minus mobs), `nameplateShowFriendlyPlayers` (+ Minions), `nameplateShowFriendlyNpcs`, `nameplateShowOffscreen`
- **Stacking** — a *bitfield*, not a boolean: `nameplateStackingTypes` with independent **Enemy** and **Friendly** checkboxes (legacy `nameplateMotion` had 0=Overlapping / 1=Stacking / **2=Spreading, default 2** per `https://warcraft.wiki.gg/wiki/CVar_nameplateMotion`)
- **`nameplateSize`** — slider over `Small / Medium / Large / ExtraLarge / Huge`
- **`nameplateAuraScale`** — slider **0.7 → 1.4, step 0.1**, shown as a percentage
- **`nameplateDebuffPadding`** — slider **0 → 50, step 1**
- **`nameplateStyle`** — six presets: **Modern / Thin / Block / HealthFocus / CastFocus / Legacy**
- **`nameplateInfoDisplay`** — checkboxes: Current Health Percent · Current Health Value · **Rarity Icon**
- **`nameplateCastBarDisplay`** — Spell Name · Spell Icon · Spell Target · **Highlight Important Casts** · Highlight When Cast Target
- **`nameplateThreatDisplay`** — **Progressive** · **Flash** · **Health Bar Color** (three independent threat channels)
- **`nameplateEnemyNpcAuraDisplay`** — Buffs · Debuffs · Crowd Control
- **`nameplateEnemyPlayerAuraDisplay`** — Buffs · Debuffs · **LossOfControl** ("big debuff")
- **`nameplateFriendlyPlayerAuraDisplay`** — same three
- **`nameplateShowDebuffsOnFriendly`** — friendly NPCs only ever show debuffs, never buffs or CC
- **`nameplateSimplifiedTypes`** — reduce to name-only for **Minions / Minus mobs / Friendly players / Friendly NPCs**. ⭐ **This is WoW's answer to "too many actors": a per-category downgrade to a stripped plate, not a global on/off.**

**Rarity/classification icons** [V-src, `Blizzard_NamePlateClassificationFrame.lua`]: `elite` or `worldboss` → **gold dragon**; `rareelite` → **silver dragon**; `rare` → **star**. A raid target marker **overrides** the classification icon. PvP classification (flag carrier, cart runner, orb carrier, assassin) overrides both, and only on PvP maps.

**Personal Resource Display**: `nameplateShowSelf` — in the *Combat* panel as `DISPLAY_PERSONAL_RESOURCE`. Shows health, primary resource, secondary resources (combo points, holy power, soul shards), absorbs and incoming heals as a nameplate-style widget under your character. Related CVars: `nameplatePersonalShowAlways` / `…ShowInCombat` / `…ShowWithTarget` / `…HideDelaySeconds`, and `nameplateResourceOnTarget` (0 = on self, 1 = on target).

### 4.6 Verified negatives — execute range and quest indicators

**Neither exists in Blizzard's default nameplates.** I read the complete component list (`NamePlateAuras`, `Base`, `CastingBar`, `ClassificationFrame`, `Component`, `Constants`, `FrameOptions`, `HealthBar`, `RaidTarget`, `UnitFrame`) plus the entire settings-definition file. There is no execute-range marker or threshold coloring, and no quest icon or kill-counter on the plate. Both are **addon territory** (Plater, Threat Plates, QuestPlates, Kib: Quest Mobs). The closest built-in is **`ShowQuestUnitCircles`** — world-space circles under quest-relevant NPCs, set via the NPC-names dropdown, **not** a nameplate element.

**Midnight's stated direction** [V `https://news.blizzard.com/en-us/article/24223311/midnight-get-up-to-speed-with-user-interface-updates`]: nameplates gain *"a wider range of relevant buffs and debuffs"*, lethal casts get *"a larger highlighted cast bar"* flagged by encounter designers, and CC is surfaced in PvP. Blizzard's principle, verbatim: ***"You shouldn't need an add-on or an external guide to tell you these things when the game should."***

---

## 5. GENERAL CRAFT — THE DELAYED DAMAGE TRAIL

### 5.1 It has no settled name — and that's a sourced fact, not a guess

Wintermute Digital, in his own tutorial description [V `https://www.youtube.com/watch?v=9b23wgIDX2Y`]: ***"I'm not sure if this effect has a name,** and the way I'd describe it is where the bar has two components — one that changes immediately and one that takes some time to catch up."*

Five substantial tutorials on the identical effect each coin a different name and none cite each other:

| Term | Community | Source |
|---|---|---|
| **"chip away effect"** | Unity (119k views) | `https://www.youtube.com/watch?v=CFASjEuhyf4` |
| **"damage bar" / "damage indicator"** | Godot | `https://www.youtube.com/watch?v=f90ieBOoIYQ` |
| **"damaged bar"** | Unity (Code Monkey) | `https://www.youtube.com/watch?v=cR8jP8OGbhM` |
| **"lazy health bar"** | Unity | above |
| **"damage trail bar"** | Roblox (low-authority SEO blog) | `https://kitsblox.com/blog/how-to-make-health-bar-roblox` |

⚠️ **Terminology trap: "chip damage" already means something else** — damage dealt *through a block*, with hard numbers per game: GGST 25% on blocked specials / 12% projectiles (permanent); P4U2R 15% delivered as Blue Health; BBTag 5% as red health, can never kill; SF6 zero chip until Burnout. **The Unity community has independently repurposed "chip away" for the visual trail.** Saying "chip damage bar" to a fighting-game person will be misunderstood.

⚠️ **"ghost bar", "ghost health", and "lag bar" are unattested** — no tutorial, wiki, or dev writeup uses them as terms of art. Recommend **"delayed damage bar"** or **"damage trail"**.

### 5.2 ⭐ Cosmetic trail vs mechanical recoverable health — the distinction that matters most

**Purely cosmetic** (HP is already gone; the trail is a readback): Dark Souls 3 / Elden Ring, Apex Legends (trail **fades out**), Sekiro (trail **shrinks**), and the visual layer in MK/Tekken/Borderlands. [⚠️ these attributions come from tutorial authors, not from the games' own docs.]

**Genuinely mechanical** — each of these answers four questions the cosmetic version never has to (*how long do I have · what restarts the clock · what do I do to claim it · what forfeits it*):

| Game | Name | Color | Recovery | Loss condition |
|---|---|---|---|---|
| **SF4** | Provisional / white damage | grey (wiki self-contradicts: "faded gray" vs "faded yellow") | gradual over match | **Getting hit again permanently voids all unrecovered grey.** Can KO |
| **SF5 / SF6** | Provisional / white | grey | gradual | cannot KO in SFV; SF6 wiki pages **contradict each other** on whether it can |
| **SFxT** | Provisional | yellow → **changed to orange in a 2013 patch "for higher contrast"** | only while tagged out | throws & Cross Arts wipe it instantly |
| **Persona 4 Arena Ultimax** ⭐ | **Blue Health** | blue | **~5 s timer**, then regenerates | **timer resets on more blue damage OR on blocking anything**; all lost on being hit; blue can never kill; **blue is not counted for a time-out decision** |
| **BBTag** | red health | red | **3 HP/frame = 180 HP/s** while tagged out, starting 60 F after Partner Skill is available | being the point character |
| **DBFZ** | recoverable | blue | gradual while tagged out | being tagged in |
| **Tekken 8** | recoverable damage | — | **Heat Engagers heal a portion**; during Heat all attacks chip 30% | — |
| **Bloodborne** ⭐ | **Rally / Regain** | **orange**, adjacent to red | **5 s window**, claimed by *hitting an enemy*; 30–75 HP per R1 | **only the *last* hit is recoverable** — a heavy hit followed quickly by a light one forfeits the heavy |
| **Monster Hunter** | red health | red behind green | passive **1 HP per 1.5 s tick**, stacking to 64 HP/1.5 s | — |

❌ **Correction to a premise in the brief: Guilty Gear Strive has no blue or white recoverable life.** Its chip damage is **permanent**. GGST's **R.I.S.C.** is a separate defense-reduction meter: *"defense is reduced by 1% for each 2% of the R.I.S.C bar filled… 33% damage increase at half filled, and 100% when completely filled."* Blue Health is P4U2R; DBFZ uses blue; BBTag uses red.

**The design hazard:** if you ship the cosmetic version, players will try to win it back. You have accidentally promised a mechanic.

### 5.3 Concrete durations and easing

| Source | Hold | Drain | Easing | Repeated hits |
|---|---|---|---|---|
| **Code Monkey** (Unity) | `damagedHealthFadeTimerMax = **1.0 s**` | alpha ramp | — | ⭐ *"instead of resetting the effect and resetting the damage bar image fill amount, we just want to **reset the timer and the alpha**… so if we get hit twice really quickly then it will show the whole thing as just one bar"* |
| **Natty GameDev** (Unity) | **none explicitly** | `chipSpeed = 2` → **~2 s** | ⭐ `percentComplete = percentComplete²` — a **quadratic ease-IN**; *"starts off slow and speeds up"* | restarts the drain |
| **DashNothing** (Godot) | `Timer.wait_time = **0.4 s**` | **snaps**, no tween | — | `timer.start()` on every hit → **timer restarts**, trail survives the whole combo |
| **kitsblox** ⚠️ low authority | **0.4 s** | **0.5 s** | main bar 0.3 s `Quad.Out` | *"Cancel active tweens before starting new ones"* |

**⭐ Two techniques worth stealing:**
1. **The quadratic ease-in manufactures the hold.** A 2 s `t²` spends its first ~0.6 s covering ~9% of the distance, which *reads* as a pause. One curve replaces a timer plus a tween.
2. **Pin the trail's far edge, restart only the timer.** Unanimous across Code Monkey and DashNothing. Consecutive hits accumulate into one growing trail rather than spawning competing trails.
3. **On heal, invert which bar leads** and recolor it (Natty tints the trailing bar green when healing) — the trail is always "the bar that shows where you were", on whichever side of the change that falls.

**Scale check against real hit feedback:** Vlambeer's *sleep* is **20 ms** [V, Art of Screenshake @18:01]; Celeste's `Celeste.Freeze(0.05f)` is **3 frames** [V, decompiled `Player.cs`]. **The trail bar operates 10–50× slower than hitstop. It is a readback mechanism, not an impact effect.**

⚠️ **No source anywhere theorizes the delayed damage bar.** No GDC talk, no Game Developer article, no paper. The perceptual argument below is reasoning, not citation: a plain bar shows only the *integral* (your state); the trail shows the *derivative* (the size of the last event) as a spatial length comparable against the whole bar. It converts a one-frame transient into a ~1 s persistent artifact — exactly Vlambeer's **permanence** principle [V, @13:07] and the **Persistence** category (Trails, Decals & Debris, Follow-Through) in Pichlmair & Johansen, *Designing Game Feel: A Survey* (`https://arxiv.org/pdf/2011.09201`).

### 5.4 Chunking and tick marks

**⭐ Shipped segment values converge on ~25 HP, independently:**
- **Overwatch** [V `https://overwatch.fandom.com/wiki/Hit_points`]: health bars are *"divided into bars each worth **25 HP**"*, layered and consumed top-down — **health white, armor orange, shields light blue, overhealth green**.
- **Apex Legends** [V]: *"each bar absorbs **25 points** of damage"*; health itself is a flat 100 with **no** segmentation.
- **P4U2R** [V, dustloop]: SP gauge in **25-SP stocks** *"**to help players recognize when they have enough SP**"*. ⭐ **The clearest statement of *why*: threshold recognition, not quantity estimation.**

**⭐ The two-tier ruler — most directly applicable to an autobattler** [V]:
- **LoL**: *"a mark for **every one hundred health**"* and *"**segmented every thousand health**"*. Minions and lesser monsters get *"a small, non-descriptive bar"* — **no ticks at all**.
- **TFT**: *"separated for every **300 health**"*.

Two lessons: minor-tick/major-segment gives one bar both a fine and a coarse read without the pip count becoming uncountable; and **Riot deliberately strips ticks from small, numerous units** — the information budget is spent only where decisions get made.

**Segments as tier, not quantity:** Destiny Elites get *"an **orange health bar**, larger than an equivalent Minor and **segmented in three parts**"*; Majors/Ultras get yellow plus a skull glyph. Color + segment count + glyph is a **triply-redundant rank encoding** that survives colorblindness and small render size at zero extra screen space. (Same idea as D3's champion/rare/unique notch counts and WoW's gold/silver dragon icons.)

**⭐ Bars that deliberately lie** — three verified cases:
1. **BBTag**: *"the health bar is slightly misleading; **it's not exactly linear, it's denser near the end** to give the effect that a player is barely surviving a 'fatal' blow, and thus more dramatic."*
2. **GGST Guts**: damage scales down as health drops, so *"**the Life Gauge is misleading; a Life Gauge that visually looks like it's 50% full actually has much more than 50% life left**."*
3. Wikipedia, *Health (game terminology)*: *"More recent games can use a **nonlinear health bar**, where earlier hits take off more damage than later ones, in order to make the game appear more exciting."*

BBTag warps the *display*, GGST warps the *damage* — same felt result. **Both make hits-to-kill unreadable from the bar**, which is fine for drama and bad for a tactics game where the player is planning lethal.

**Hiding enemy HP as a deliberate choice** [V]: *"In some games such as The Legend of Zelda and Monster Hunter, **only the player's health points are visible. This is done so that the player does not know how many blows still need to be delivered.**"* Cliff Harris (Positech) on *Gratuitous Tank Battles*: *"You get an extra bit of unknown-information tension… It's tense, it's worrying, it's exciting, and it builds suspense."* (`https://www.positech.co.uk/cliffsblog/2012/04/02/help-me-decide-about-health-bars-for-enemies-in-my-game/`)

**⭐ The real perceptual law is subitizing, not Weber–Fechner** [V `https://en.wikipedia.org/wiki/Subitizing`]: the limit is **≈4 items** *"unless the items appear in a pattern with which the person is familiar"*; **40–100 ms per item inside the range, an additional 250–350 ms per item outside it.** A 4-pip bar reads in ~0.2–0.4 s; an 8-pip bar costs roughly 4× because you've crossed from parallel perception into serial counting.

⚠️ **The Weber–Fechner argument for segmenting high-HP bars does not hold up, and no source makes it.** A fixed-width bar maps HP to *position* linearly — a hit worth 3% of max moves the fill edge 3% of the bar width regardless of the HP total. **The real failure mode at high HP is pixel quantization**: on a 300 px bar against a 40,000 HP boss, a 100-damage hit moves the edge **0.75 px** — below the display's ability to represent it. **Segmentation doesn't fix that either.** The actual fixes are stacked/wrapping bars, floating numbers (different modality), or **the damage trail bar** — whose length is the delta rendered at full bar scale rather than as an edge displacement. **That is the strongest argument for combining §5.3 and §5.4.**

⚠️ **Miller's 7±2 is the wrong citation** — Laws of UX's own page warns *"Don't use the 'magical number seven' to justify unnecessary design limitations."* Miller is working memory; bar reading is visual perception. The one empirical study is **Gittens & Gloumeau (2015), IEEE GEM, DOI `10.1109/GEM.2015.7377232`** — ⚠️ paywalled; snippet-level reported result: N=32, **72% preferred segmented vs 28% single**, p=0.01333. Preference, not performance, N=32, one game.

**Many-unit specific** [V]: **Beyond All Reason** (RTS, hundreds of units) — *"**Health-bars, while being very practical gameplay-wise, are also working against the effect of immersion**"*; they encode damage on the unit mesh (bending metal, texture degradation) and ship a setting to **show bars only when a unit is selected or actively taking damage, auto-hiding after a few seconds** — the trail idea applied to *bar visibility itself*. And **MetaBattler**, a small autobattler, replaced health circles with team-colored bars for readability and added optional floating damage text to *"improve the legibility of smaller fights"* while noting **it feels cluttered in larger battles** — ⭐ the most warband-applicable line found: **damage numbers do not scale with unit count.**

### 5.5 The juice canon — with corrections

**⚠️ "Juice It or Lose It" (Jonasson & Purho, GDC Europe 2012) contains no UI, no bars, and no numbers.** Verified against the human-authored caption track and the actual source repo (`https://github.com/grapefrukt/juicy-breakout`) — Juicy Breakout has **no HUD at all**. Anyone attributing delayed-health-bar advice to this talk is extrapolating. Video: `https://www.youtube.com/watch?v=Fy0aCDmgnxg`

**The one transferable primitive, verbatim @03:51:** *"you can't always use a tweening engine… but you can always use this baby right here… Basically we're moving x (**whatever value that is**) **10% of the way we need to go**. So at first, it will go fast and then as it approaches its target it will slow down… **have it run every frame**."* — i.e. `x += (target - x) * 0.1`, and he explicitly generalizes it beyond position.

Other portable bits, verified from `Settings.as`: **random per-object delay** (`Math.random() * EFFECT_TWEENIN_DELAY`, 0–1 s) for staggering many elements; the **jelly pulse** — `scaleX` → 1.2 over **0.05 s** `Quadratic.easeInOut`, back to 1 over **0.6 s** `Elastic.easeOut`, with `scaleY` identical but **offset +0.05 s**; flash-to-white recovery **0.7 s** `Back.easeOut`. Screen shake is **directional and spring-damped, not random**: `shake(-ball.velocityX * POWER, -ball.velocityY * POWER)`.

**⚠️ "The Art of Screenshake" is INDIGO Classes 2013, not GDC.** `https://www.youtube.com/watch?v=AJdEqssNZ-U`. Nijman renames it live @07:01: *"this talk is now officially called **30 tiny tricks that will make your action game better**."* The hitstop quote, verbatim @18:01: *"**sleep** — it just pauses the game for a couple of milliseconds… **when I hit an enemy it will pause for 20 milliseconds or something**, and your brain won't notice that but kind of uses that time to process what's happening… I put some tiny sleeps in there **when you hit an enemy and also when they die**."* ❌ **He never says 0.05 s and gives no frame count** — the ubiquitous "~0.05 s" is Celeste's. Accessibility, from the Q&A @32:21: *"we had to put an option in Nuclear Throne to disable the screen shake because some people were getting really nauseous."*

**Swink, *Game Feel*.** Polish, verbatim: *"any effect that artificially enhances interaction without changing the underlying simulation… **for players — simulation and polish are indistinguishable**."* ⭐ His method: *"When prototyping, I like to **list these cues out and sort them in order of importance** to the physical impression that should be conveyed"* — across four channels: **Motion / Tactile / Visual / Sound**. ❌ No section on health bars, damage numbers, or HUD in either the book chapter or the articles. ⚠️ The famous "under 100 ms" control threshold is **not** in ch.1.

**Best formula found for damage-driven feedback** [V `https://www.ssbwiki.com/Hitlag`]: Smash hitlag is affine in damage — base 3–6 frames + ~0.33–0.65 frames per damage point, floored and clamped (Melee cap 20 F, Brawl-onward 30 F).

**Best academic collation:** Pichlmair & Johansen, *Designing Game Feel: A Survey* (`https://arxiv.org/pdf/2011.09201`) — Table I is a ready-made checklist. Their sharpest claim, corroborating both canonical shake implementations: *"**Instead of randomly moving the camera, a carefully selected easing function in a semantically significant direction communicates more information about what has happened.**"*

---

## 6. TEXT LEGIBILITY, CULLING & POOLING

### 6.1 Outline vs shadow vs plate

**Practitioner consensus is plate first, outline/shadow as fallback, ideally both** [V `https://gameaccessibilityguidelines.com/provide-high-contrast-between-text-ui-and-background/`]: (1) *"Ideally place your text and UI elements on a plain high contrast background"*; (2) where infeasible, *"prominent outlines and shadows to separate them from the background"*. Their subtitles page: *"Text is against a solid or semi-opaque background… **ideally combined with an outline/shadow too**."*

**Xbox Accessibility Guideline 102** [V `https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102`] names the exact problem: *"Often, the gameplay environment is in constant visual flux, and on screen elements like text, symbols, or visual cues **don't meet contrast ratios at all times in all gameplay scenarios**."*

**⭐ The double-outline trick — the only technique found that survives genuinely arbitrary backgrounds**, from XAG 102's *For Honor* example: symbols use **a black outline plus a white outline**. *"The white outline ensures that the symbols remain visible against dark backgrounds… while the black outline ensures that the symbols remain visible against light backgrounds."* In TMP this maps to Outline = dark plus **Underlay with zero offset and positive Dilate** = light halo (i.e. use the underlay as a second concentric ring, not as a drop shadow).

**⭐ The correct measurement rule for floating text**, XAG 102: *"When text is displayed over a non-solid color background, the text contrast ratio should be measured between the text and **the lowest contrasting area of the background**."* Measure against the worst pixel, not the average.

Drop shadow is the weaker choice over complex backgrounds — an outline follows the glyph contour and is direction-independent. ⚠️ Well-supported folklore; I did not find a single authoritative practitioner page stating it outright.

**The one concrete outline number found anywhere** ⚠️ (snippet-only, `gamejuice.co.uk` 403s to fetch): *"a white number with a **1–2px black stroke** reads clearly whether it is floating over a bright sky, a dark dungeon wall, or a complex particle effect"*; *"bold or black-weight fonts read clearly against busy backgrounds while thin fonts disappear."* **No source gives outline thickness as a fraction of font size.**

Counter-warning [V]: *"Bold does not automatically mean legible, as crowded spacing, complicated outlines, deep shadows, and several simultaneous effects can turn a clear font into visual noise."*

### 6.2 The actual numbers

| Standard | Value |
|---|---|
| XAG 102 contrast — standard text | **4.5:1** |
| XAG 102 — large text / inactive | **3:1** |
| XAG 102 — high contrast mode | **7:1** |
| XAG 102 "large text" threshold, PC/VR | **36 px @ 1080p** (72 @ 4K) |
| XAG 102 "large text", console | **52 px @ 1080p** |
| XAG 101 minimum default font (body height), PC/VR | **≥18 px @ 1080p**; console **≥26 px @ 1080p**; must scale to **200%** |
| GAG foreground/background | **≥4.5:1**, explicitly citing WCAG |
| GAG subtitles | **≥46 px @ 1080p**, ≤40 chars/line, ≤2 lines |
| **⭐ IGDA GA-SIG — "text that appears for a limited time"** | **46 px @ 1080p** ← *damage numbers are by definition limited-time text; this is the closest thing to a direct standard for them* |
| WCAG 1.4.3 AA | 4.5:1 normal; 3:1 large (=18pt, or 14pt bold) |

Sources: `https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102`, `/101`, `https://gameaccessibilityguidelines.com/`, `https://igda-gasig.org/how/platform-level-accessibility-recommendations/text-size/`. AbleGamers' *Clear Text* pattern is about **configurability** rather than fixed values — Hitman ships five sizes (22/28/34/40/48 pt) with live preview.

**Underlying model** [V `https://www.sidebearings.com/game-ui-type-system/`]: *"a practical floor for game UI body text is around **0.3 degrees of visual angle**"* — the px minimums are just this instantiated per platform.

### 6.3 Font choice for numerals

**Stroke width** [V, DIN 1450 via `https://legibility.info/characters/stroke-width`], as % of x-height: body text 10–20%, consultation 13–20%, **signage 17–20%** (hairline min 12%). **Damage numbers over hostile backgrounds are functionally signage → 17–20%, which rules out Light/Regular and argues for Bold/Black** — with DIN's caveat that counters and apertures must not close.

**Tabular figures** [V, Google Fonts Knowledge]: identical advance widths so *"numbers do not jump around"*. With proportional figures a `1` is narrower than a `9`, so a centered number **shifts horizontally as digits change**. For a static spawned number the bigger win is a **predictable optical center and width budget** for de-overlap math.

**Numeral disambiguation** [V]: *"HUD text should favor a clean, high-x-height sans with clearly differentiated numerals, as players read a lot of numbers in games and confusion (like a 6 reading as an 8) is a real usability problem."* **Condensed** faces are recommended when horizontal space is constrained — directly relevant, since 5-digit numbers in a wide face collide far more often.

**Damage-number-specific** [V `https://shweep.medium.com/damage-numbers-in-rpgs-1f0e3b1bc23a`]: *"the more compact and monospaced the more readable"* (Xenogears' cursive numbers cited as the negative example).

### 6.4 Unity TextMeshPro — verified specifics

From `https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.2/manual/ShadersDistanceField.html` and the shader source:

- **⚠️ The outline trap, verbatim:** *"The outline is drawn on the text contour, **with half its thickness inside the contour and half of it outside**."* A thick outline **eats the glyph face**; the documented fix is positive **Face > Dilate** to compensate.
- **Face > Softness affects both face and outline.**
- **Underlay** types: None / **Normal** (*"Renders the underlay underneath the original text. This creates a standard drop-shadow style effect"*) / Inner. Its own Color, Offset X/Y, Dilate, Softness. **Zero offset + positive Dilate = halo, not shadow.**
- **⭐ Cost: one pass, one variant.** `TMP_SDF.shader` has exactly **1 Pass**; outline is computed inline in the fragment shader; underlay is a `#pragma shader_feature` in the *same* shader. **No extra geometry, no extra pass, no extra draw call.** Outline is essentially free.
- **⭐ The real cost is material count, not effect count.** Tint per-hit-type via **vertex color / `TMP_Text.color`**, keep **one shared material preset** — crit-vs-normal color should be a vertex-color change, never a material change.
- **⭐ Outline width is bounded by the font asset, not the material:** *"**Gradient Scale** — Represents the spread/range of the font's signed distance field. This determines the effective range of material properties such as **Outline > Width** and Underlay > Offset. This value is equal to **Padding + 1**."* Padding 5 works at 512×512; ⚠️ community guidance is **spread 7 for "titles… which typically have larger outline, bevel and glow options"**. **Damage numbers are a titles-class use case — regenerate the font asset with padding ≥7 or the outline will clip before it's thick enough.**
- **Bitmap is disqualified**: SDF gives *"completely smooth edges regardless of the distance from the camera"* where bitmap edges are *"more or less jagged/blurry"*. Damage numbers scale-punch and move.
- Distance Field shaders are **unlit** — which is what you want; scene lighting would wreck contrast predictability.

### 6.5 Color coding caveats

**XAG 103, verbatim: *"Color alone should never be used to represent information."*** Anything critical expressed through color *"also needs to be expressed using at least one additional signifier such as **shape, pattern, iconography, or text labels**"*, and if color is primary, the player should be able to reconfigure it. **XAG 102 specifically flags red/green** for targeting icons — directly applicable to the near-universal red=damage / green=heal convention. GAG: red/green affects *"around 8-10% of males"*; recommends **orange vs blue** as the default safe pairing. ⚠️ Simulator filters *"shouldn't be used as a replacement for testing with actual players"*.

**Redundant encodings actually shipped:** **Paper Mario** attaches a varying number of decorative stars to convey damage intensity *"if you were unable to read numbers"* — a non-color, non-numeric channel. And ⚠️ crits at *"150 to 200 percent of standard size"* with a pop-scale plus yellow/orange — **size + motion + color, three redundant channels for one bit**. Compare WoW's shipped 2× crit overshoot in 50 ms (§4.1) and D3's explicit rejection of size in favour of hue (§1.1).

### 6.6 Pooling — the counterintuitive part

**Unity's `ObjectPool<T>` will not do what you want** [V `https://docs.unity3d.com/6000.4/Documentation/Manual/performance-reusable-code.html`]:
- `Get()` on an **empty** pool **creates a new instance**. It does not fail, block, or recycle. **Under load it silently degenerates into instantiate-and-destroy** — exactly what you pooled to avoid.
- `Release()` into a **full** pool **destroys** the object.

**You must implement the overflow policy yourself, above the pool:** keep an active list; when `activeCount >= cap`, either refuse the request or force-release the oldest and reuse it. **Recycle-oldest is better for damage numbers** — the newest hit is what the player is looking at; the oldest is already faded. (Note Blizzard chose the opposite: **drop the newest** at a 20-line cap — §4.1.)

Other verified Unity guidance: reset state in `actionOnRelease`, **deactivate the GameObject on release** so `Update()` stops running on idle entries, prewarm during loading. ⚠️ `TMP_Text.SetText()` has **numeric (int/float) overloads that produce zero GC allocations**, unlike string concatenation; ⚠️ reading `.text` after `SetText` allocates.

### 6.7 Culling rules from real games

| Rule | Value | Source |
|---|---|---|
| **Hard cap on concurrent floating numbers** | **20** | [V] Better Combat Text (WoW) |
| | **20** | [V-src] Blizzard's own `NumCombatTextLines` |
| **Burst aggregation** | **>5 hits within 2 s → one entry**, rendered `2.5K (7)` | [V] Better Combat Text |
| **DoT/HoT per-ability throttle** | **2.5 s** default, events within the window merged | [V] MikScrollingBattleText |
| **AoE merging** | collapse to one event, unit shown as "Multiple", cumulative total + normal/crit counts | [V] MSBT |
| **⭐ Merge-exclusion list** | per-ability opt-out so a multi-hit ability stays unmerged | [V] MSBT |
| **Per-actor cap on status ticks** | **10** — *"reducing how many damage events are shown helps with performance"* | ⚠️ Warframe wiki (Slash) |
| **Suppress-below-threshold** | player-set floor | ⚠️ Warframe forums |
| **Hide non-event auto-attacks** | on by default | [V] WoW `floatingCombatTextCombatDamageAllAutos` |
| **Suppress a whole damage class** | Area Damage splash spawns no numbers at all | [V] Diablo 3 |
| **Time-bucket high-frequency sources** | **0.5 s** | [V] Diablo 3 DoTs |
| **Force abbreviation, no opt-out** | 10000 → 10k | [V] Diablo 4 patch 2.0.3 |
| **Bars only while selected or recently damaged, auto-hide** | few seconds | [V] Beyond All Reason |

**⭐ MSBT's merge-exclusion list is the important pattern:** a global merge destroys the readability of abilities whose *identity* is "many small hits". **Shipping a merge without a per-ability opt-out is a known failure mode.**

❌ **Not found:** any shipped, citable example of distance/off-screen culling for damage text, of "prioritize the player's own damage" as an explicit rule, or of a published minimum-spawn-interval-per-actor value.

### 6.8 Canvas / de-overlap

**The canonical Unity failure** [V `https://discussions.unity.com/t/ui-optimization-hundreds-of-floating-damage-text/250517`]: a dev put a **Canvas inside the damage-number prefab**; a 300-actor AoE spawned 300 Canvases and dropped the game to **40 FPS with freezes**. Accepted answer: *"You should use a single canvas you don't need to have 300 different canvas."* **Each Canvas is an independent rebuild island — per-number canvases is the single worst thing you can do.** Splitting canvases isolates rebuilds but adds draw calls, so damage numbers want **one dedicated dynamic canvas**, separate from static HUD, and not merged into it. ⚠️ *"Animators will dirty their UI Elements on every frame, even if the value in the animation does not change"* — **do not drive the float/fade with an Animator on a uGUI canvas; move the transform in code.**

**Unreal** [V `https://dev.epicgames.com/documentation/en-us/unreal-engine/optimization-guidelines-for-umg-in-unreal-engine`]: Rich Text widgets are *"very expensive"*; Canvas Panels/Overlays increment child Layer IDs and **multiply draw calls**; never bind UI attributes directly to data; mark constantly-updating widgets **Volatile**. The scaling alternative [V `https://kolosdev.com/shooter-tutorial-niagara-umg-damage-indicators/`]: one persistent **Niagara** system for all indicators, digits from a **4×4 texture atlas** with HLSL UV math, because with Widget Components *"each indicator is a separate component being rendered"*.

**De-overlap techniques with actual numbers:**
- **Random spawn offset**: `Random.insideUnitSphere * spawnRadius`, ⚠️ `spawnRadius` **0.5 units** as a starting point.
- **Blizzard's staggering** [V-src]: **±10 px** horizontal jitter; **alternating `xDir` sign** on every message in arc mode; second-column offset **±80 px** on overflow.
- **⭐ Pause at apex** [V `https://howtomakeanrpg.com/a/polish-03-combat-numbers.html`]: *"a **0.2 second pause** when the number reaches the peak of its ascent"* to improve readability; numbers should otherwise *"move quickly… it gives a nice fast pace."* Same page: two-layer draw (black offset ±1 px, colored text on top) as a poor-man's outline, and `math.floor` on positions to keep glyphs on pixel boundaries.
- **⭐ Temporal de-overlap has the most shipped precedent** [V]: **Dragon Quest XI** staggers multi-hit numbers so players *"don't just see the numbers appear all at once"*, giving processing time between them. **Ragnarok Online** shows faint per-hit numbers then a **yellow total**. **Ex Nihilo** rate-limits: *"if a message is requested within a short time after another one — **it will be delayed a bit**"* — **delayed, not dropped**.
- **Arc vs vertical for de-overlap** ⚠️ no authoritative comparison exists. Mechanically: vertical scroll from a fixed anchor puts N simultaneous numbers in the same column; an arc separates them in X immediately and the separation grows. Arc is better for de-overlap; vertical is better for reading a column of ticks over time. This is exactly the tradeoff WoW ships as a player choice (`floatingCombatTextFloatMode`), with **crits forced to vertical-and-sticky in both modes**.

---

## 7. The cross-cutting patterns worth carrying into warband

1. **Every studio that solved this suppressed information rather than shrinking it.** D3 suppressed Area Damage entirely; WoW hides non-event auto-attacks by default and downgrades whole unit *categories* to name-only plates (`nameplateSimplifiedTypes`); GGG never shows numbers at all and encodes magnitude in impact-VFX size; MetaBattler found floating text *"feels cluttered in larger battles"*. Nobody solved many-actor combat by making text smaller — **Legion tried exactly that and players said their damage had disappeared.**
2. **The outlier-highlight beats the per-hit readout.** D3's orange algorithm (>10k, beat-the-last, **3%/s decay**, **10-number warmup**, **10 s reset**) is a complete, tested design for "which of these hundreds of numbers deserves attention," and its two non-obvious parts — the decay and the warmup — are precisely the fixes for buff windows and cold starts.
3. **Redundant crit encoding is universal, but the channels differ.** WoW: **2× size overshoot in 50 ms, settled by 200 ms, plus stickiness** (crits don't scroll). D3: **hue only**, size and motion explicitly rejected for accessibility. D4: color-only across four types. XAG 103 says color must never be the sole channel — so **WoW's size+motion+stickiness is the more defensible model**.
4. **Two-tier tick marks are the autobattler answer** (LoL 100/1000, TFT 300), *plus* Riot's corollary: **strip ticks from small numerous units**. And note both D3 and D4 use segments as a **schedule marker** (loot globes, boss phases) rather than a quantity ruler.
5. **The damage trail bar is the right fix for the actual high-HP problem**, which is pixel quantization, not Weber–Fechner. Consensus implementation: **snap the main bar, hold 0.4–1.0 s, drain with ease-in, pin the trail's far edge and restart only the timer on repeat hits, invert and recolor on heal.** But if it's cosmetic, expect players to try to win it back.
6. **Aura rows want a whitelist, not a cap.** WoW's Midnight rules are the state of the art: **your debuffs on enemies, your buffs on allies**, gated by a **spell-data whitelist**, with enemy buffs shown only if stealable or flagged important, sorted important-first, capped per container, and wrapping at a **stride of 6–12 icons driven by icon scale**. Countdown numbers hidden above 60 s.
7. **Pool exhaustion is a design decision, not an implementation detail.** Blizzard drops the newest at 20; recycle-oldest is probably better for damage numbers; Unity's built-in pool does **neither** and will quietly instantiate under load.

No files were written. Working files (Blizzard Lua sources, the archived D3 blog, CVar dumps) are in the session scratchpad if anything needs re-checking.agentId: ac660bdaf9c14809f (use SendMessage with to: 'ac660bdaf9c14809f', summary: '<5-10 word recap>' to continue this agent)
<usage>subagent_tokens: 242320
tool_uses: 74
duration_ms: 3886411</usage>