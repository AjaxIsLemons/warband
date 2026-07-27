# Roadmap — THE live board

**This is the ONLY live priority list.** (Pattern from battle/Shoota execution boards —
multiple competing lists is how projects rot.) Sessions plan from here; see CLAUDE.md
"Planning SOP". Keep it honest: the board must match reality better than memory.

**HARD-CUT GROOMED 2026-07-27.** The board had re-grown to **1145 lines with 558 of them in Done**
and item 1 alone at 175 — the exact archive failure the 07-24 grooming fixed. Full Done detail and
item 1's build history now live in **`Projects/roadmap-done-archive.md`** (nothing deleted);
blow-by-blow logs live in `Daily/<date>.md`. **This file carries state, priority, laws, and the
gotchas that would bite someone.** If a Done entry grows past two lines, it belongs in the archive.

## Stages
- **DESIGN** — needs a design conversation with Jake before building. Don't build; propose.
- **SPEC'D** — designed (ADR/doc exists), ready to implement autonomously.
- **BUILD** — implementation in progress; note what's left. Finish before starting new work.
- **VERIFY** — built, needs verification/tests/polish before calling done.
- **DONE** — move to the Done section with a date, **one line**, detail to the archive.

## Now / Next (ordered — top unblocked item is "what's next")
🎯 **GOAL (Jake, 2026-07-23): a playable PvE PoC.** **North star (ADR 0016): the fun is
breaking the game with a compounding warband, then seeing how far asymmetrical PvE and
endless pressure can push it.**

### ⇒ AGREED ORDER (Jake, 2026-07-27). Item numbers never change; this line does.
**Reordered from the 07-26 line, for one reason: Jake's play passes are the scarcest resource on
this project, and the previous order spent one on a build with a known, one-number defect in it.**

**1. ~~Item 10 — the impact balloon.~~ BUILT 2026-07-27** — new `impact.punchScale` global dial,
default 0.5, halving every recoil (the heaviest tell was at +90%, not the recorded 37%). Feel is
Jake's to judge; the value is one F1 slider away from any other answer.
**2. ~~Item 11 — Overtime is invisible.~~ BUILT 2026-07-27 (THE WANING)** — clock + warning + storm
tell, render-only, plus an `overtime` fixture so the thing can be seen at all. Root cause was worse
than "no clock": storm damage inherited a tell row with `minAmount: 5` while the ramp starts at 1,
so the first 12 s of overtime drew *nothing*. **One edit-mode capture still owed** (see item 11).
**3. JAKE'S VERIFY PASS (item 1). ← NEXT, AND IT IS YOURS.** Both cheap feel wins have landed, so
the pass now judges a build with the balloon halved and the storm visible.
**4. Item 1c — THE COMBAT RECAP. ← Jake, 2026-07-27, ranked ABOVE 5a.** A comprehensive, polished
post-fight report: per-unit contribution bars, the five-way damage composition, and a death
timeline. The result gate is three stat rows and three death lines today, and there is **no graph
anywhere in the client** — while the sim already computes and tests a full recap the UI throws away.
So it is a UI job over existing data. It also precedes 5a for a reason: **a recap that shows why a
build worked is what makes collecting Inscriptions worth doing** — ship the legibility, then ship
the engine it explains.
**5. Item 5a — the Inscription engine layer. ← SET BY THE 2026-07-27 ROADMAP REVIEW (Jake).**
The review measured the build against its own budget and found one large gap: **Inscriptions are at
5 of 24**, and that is the layer ADR 0016's north star — *compounding builds that feel like they
break the game* — actually lives in. Everything above it in this list is render and shell. It also
absorbs **item 17** (Silence), and unlike items 4 and 18 it is **not** blocked behind the balance
question the content doctrine parks until playtest #1. Target the twelve-family vocabulary proof.
**6. Item 9 — the options screen.** The last P1 blocker on friends playtest #1 (item 6).
**7. Then re-decide.** Standing candidates: item 1d (camera) · item 19 (measure a human) ·
items 12, 13, 15's unspent event. Items 4 and 18 are one balance question wearing two hats, and the
doctrine holds them until playtest #1.

**⚠ SESSION HYGIENE — UNRESOLVED AS OF 2026-07-27.** The tree carries **5031 insertions across 52
files plus 29 untracked**, including the Workbench overhaul this board marks BUILT + UNITY-VERIFIED.
This is the **second** occurrence; the 07-26 entry flagged 178 uncommitted files, and that is exactly
why the `--enc` drift could not be bisected. **Do not assume this is yours** — Jake runs Claude and
Codex in parallel (CLAUDE.md), so another session may be live in these files. Agree ownership before
committing. `make test` is green at **460 (249 sim + 211 run), verified 2026-07-27**.

**STATE, 2026-07-27 (honest):** the first-playable run shape and between-fight UX are walkable end
to end: Menu → five-card Draft → Management Hall → stakes-first Wager → formation-reveal Deployment
→ Fight/replay → blocking result report → spatial Hourstone Table → Victory/Defeat. Three acts ×
five beats, Sand economy, Interludes, boss rewards, terminal loss, save/resume, and a shipped
standalone build + launcher + public site are all implemented. Authored encounters (ADR 0023),
per-act bosses and full disclosure (ADR 0024), and act-scoped disjoint pools are in.
**Combat viewing still does not read well enough, and that judgement is over a year-stale build —
nobody has watched the corrected player.** UI has had five passes (Muster readability, unified
decision cards, persistent Warband Shelf + Loadout Table, shared mechanic presentation, and the
2026-07-27 Workbench overhaul), all Unity-verified by capture, **none watched in motion**.
Detail for every one: `roadmap-done-archive.md` + `Daily/2026-07-26`.

**MEASURED, 2026-07-27 review** (`make baseline`, byte-identical to committed — these are current):
bot run victory **4 / 4 / 7** (stable/fraying/collapsing) · fight win 76% · naive line **2/12 runs
completed** · **4 of 6 node encounters FREE + FLAT at their own debut act** · all 3 bosses admit 3–4
answer axes at spread 100 (**the healthiest content in the game — protect it in any balance pass**) ·
`banneret` still chassis-dead at 13 avg vs berserker 75 · sim health clean (never-swung 0.00%,
deadtime 1.81%) · **Inscriptions 5 of 24**. The three-act run, the shell, save/resume, the build and
the launcher are all real; **what is thin is the reason to replay it.**

---

1. **FEEL & READABILITY — Jake's play pass.** — **VERIFY. BLOCKED ON JAKE, NOT ON BUILDING.**
   This item is now **only the verify gate**; the build work that used to live inside it is split
   out as 1b/1c/1d below, because a gate bundled with unbuilt work can never close.
   **Jake, 2026-07-24, after playing:** *"playing it now still does not feel great for a lot of
   reasons (UI is not great, sim viewing has some issues and is not quite clear what's happening)."*
   Three candidate causes were named — ① presentation (too much at once, no pacing/emphasis)
   ② legibility of state (what a unit IS, what's on it, why it did that) ③ UI quality. **All three
   have since been built against, across five sessions.** Two structural causes were found and fixed
   along the way: **movement** (ADR 0018 — decide and apply in the same tick meant everyone
   teleported) and **the opening leap** (2026-07-26, render-only — 24% of fights open with a 1–2
   instant cross-board dive; air-time now scales with span, plus a 0.7 s opening hold).
   **What the pass must judge, specifically:** fight-ender slow-mo + camera law · riser mix and
   announce density in motion · beat sequencer + hit-stop (landed `a1fcf8b`, never seen) · KayKit
   minis in motion · F1 knobs (field brightness / icon size / wall tint / cleric sigil) · HP-bar snap
   vs T3 windups (→ bar tween if wrong).
   **Known-dormant, one-line fix when wanted:** Heal carries no `Cause`, so Boon pulses never fire.
   **The first standalone pass (2026-07-26) was an INVALID read** — URP Lit/Unlit were stripped from
   the player build, so the board and HP/Mana bars were pink. Corrected build `0.1.260726.1706`
   registers them and mutes the (bad, over-long) generated audio by default. **That build has never
   been watched.** Per the 07-27 order, items 10 and 11 land first.
1b. **Hall polish slices** — **VERIFY/BUILD.** Foundation built and Unity-verified.
    Four named slices open: final Bind choreography · Rule Preview diagrams · real-device
    safe-area/finger/haptic pass · live audio/motion feel tune. `Design/hall-polish.md`.
1c. **THE COMBAT RECAP — a comprehensive, polished post-fight report.** — **SPEC'D. RANKED ABOVE 5a
    BY JAKE, 2026-07-27** (*"a comprehensive and polished combat recap, with graphs and such"*).
    **Rescoped from "fight comprehension UI"**, which read as a Phase 4 leftover — two words,
    "damage chart" — and would never have produced what was actually asked for.
    **There are TWO post-fight surfaces and the board used to treat them as one:**
    ① the **in-board readout** (world-space text during the end hold) — top-3 damage dealers with
    team share + died-to attribution. This is what `40eb076` shipped. ② the **result gate**
    (`ResultGateView`, the blocking screen) — which is **three stat rows and three death lines**:
    `SAND EARNED`, `ENEMIES FELLED`, `TOP DAMAGE` (one name, one number), then up to three
    `X fell to Y · Cause · 12.4s` lines. **No graph, chart, bar or timeline exists anywhere in the
    client.** Both surfaces are text labels. The recap belongs on ②.
    **⚠ THE POINT: this is a UI job, not a sim job. The data already exists and is already tested —
    the client computes none of it and displays ~5% of it.**
    | already computed & tested in the sim | reaches the UI today |
    |---|---|
    | `FightSummary.Units[]` — per-unit damage dealt/taken, healing done/received, shields absorbed, kills, death tick, killed-by + cause, **`DamagePctOfTeam`** | one unit's damage number |
    | `FightStats` — damage split **five ways** (`AttackDamage`/`AbilityDamage`/`DotDamage`/`FieldDamage`/`TriggerDamage`), plus `Casts`, `FirstCastTick`, `CcTicksSuffered`, `Steps`, `ShotsBlocked` | **nothing** |
    | `FightSummary.Beats[]` — every death: tick, victim, killer, cause, overkill, `KillerInferred` | first 3 lines |
    | `FightSummary.Teams[]` team totals · `UnattributedDamage` | no |
    | `BattleForecast.Run(...)` — re-sim win probability | **zero client references** |
    **Approved scope (Jake, 2026-07-27): contribution + composition + timeline.**
    ① **per-unit contribution** — a row per hero with a damage-share bar off `DamagePctOfTeam`,
    replacing the single TOP DAMAGE row · ② **damage composition** — the five-way Attack/Ability/
    DoT/Field/Trigger split, which is the *"why did my build work"* chart and the closest thing on
    the board to rendering ADR 0016's north star · ③ **death timeline** — `Beats[]` laid out on the
    fight's clock, which also gives the Waning (item 11) somewhere to show as a phase.
    **Deliberately NOT in this slice:** `BattleForecast`. It stays orphaned for now — the per-fight
    re-sim cost is unmeasured, and it is the one part needing more than layout. Measure before
    committing to it.
    **Build notes:** `RunShell:2050` builds the result model and already calls `FightSummary.Build`,
    so the data is in hand at the call site — this is a model + view change, not plumbing.
    `FightStats` is currently referenced only by `ReplayPlayer`, so the result gate needs its own
    fold. Charts must be **code-native** (Painter2D / USS), consistent with the Hall's existing
    bounded-Painter2D pulse and the shared `MechanicPresentation` glyph and colour language — do not
    introduce a charting dependency, and do not invent a second colour vocabulary for damage kinds.
    **Related:** this is also the surface that makes **item 19** (nobody measures a human) tractable —
    a recap that shows a player their own contribution is one step from recording it.
1d. **Camera/framing pass** — **UNBUILT, taste-gated on Jake.** Deliberately not started before the
    verify pass, since framing is exactly the kind of thing that pass will have opinions about.

2. **Authored PvE content** — **BUILT (ADR 0023 + ADR 0024). Header corrected 2026-07-27: bosses and
   disclosure are DONE, not remaining.** Five authored enemy roles (Swarm/Anchor/Artillery/Ritualist/
   Diver) compose six node encounters across act-scoped, **disjoint** act-2/act-3 pools, each posing a
   placement problem and disclosing its rule; three per-act bosses close the acts.
   **Composition is the act lever, stats are secondary**; an act's pool is its identity.
   Laws: `Design/pve-encounters.md`; decisions: ADR 0023, ADR 0024.
   **What remains:** ③ bespoke enemy art + per-role tells (enemy CARDS no longer borrow hero
   names/portraits, but board silhouettes still do) · ④ risk-tier mutation of authored encounters
   (tiers only scale stats today) · ⑤ **still not watched in Unity** — two boss render fixtures exist
   (`boss-ashfall-battery`, `boss-waning-crown`), so this is one session away.
   → **The "encounters are FREE" finding is now item 18.**

4. **The pressure tier is a fake choice — AND THE PREMISE INVERTED. Re-measured 2026-07-27.** —
   **DESIGN.** Stable/Fraying/Collapsing are visible. This item has now been true for two OPPOSITE
   reasons, which is exactly why it must be re-measured before it is designed:
   | measured | stable | fraying | collapsing | the reading |
   |---|---|---|---|---|
   | 2026-07-23 | ~99 | ~99 | ~99 | risk is free — everything wins |
   | after ADR 0022 | 88 | 92 | 79 | |
   | after ADR 0023 | 35 | 48 | 39 | Fraying beats Stable |
   | **2026-07-27 (current)** | **4** | **4** | **7** | **the run is near-unwinnable — and Collapsing STILL wins most** |
   So the old sentence "victory saturates ~99%, Collapsing strictly dominates at zero risk" is
   **8× stale and points at the wrong bug.** What survives is the *shape* of the defect: the highest
   risk tier still posts the best victory rate, so the tier costs nothing. What changed is the floor.
   **Caveat that must travel with these numbers:** `run.*` is a **default-policy BOT** over 120 runs
   per tier — it does not choose placement or purchases. It is a floor, not a forecast. See item 19.
   ADR 0007's economy is placeholder either way. **Always start from a fresh `make baseline`.**

(Item 3 — the Last Oath's unreachable decision — is DONE 2026-07-25. The gap is deliberate: item
numbers are load-bearing references, so finished items leave a hole rather than renumber.)

### P1 — silently blocks friends playtest #1 (item 6)
7. **A run cannot be saved** — **DONE 2026-07-26** (412 tests, verified on Windows). Shell wiring
   (does CONTINUE appear cold, does clicking it resume, does autosave fire) is **Jake-only** — see
   the Play Mode gotcha. Settled item 16 on its back.
8. **Standalone build + launcher/delivery** — **DONE 2026-07-26.** Build, launcher, publish pipeline,
   and the public site are live; the launcher pulls the real build through the real site. Two shader
   landmines were real and are now guarded in the build itself. Remaining: one visual recheck of the
   corrected build before publishing it (folds into item 1's pass).
9. **No player-facing options at all** — **SPEC'D (small). #4 in the agreed order.** Audio
   enable/volume live in `HubPresentation.json`, reduced motion in a dev-key `PlayerPrefs` toggle,
   battle speed in `tuning.json` behind F1. **Re-verified 2026-07-27: no options/settings view exists
   in the client.** A friend on their own machine cannot mute the game, slow the fight down, or turn
   motion off. Every value is already plumbed and hot-reloadable — this is a screen over existing
   seams, not a system.

### P2 — combat legibility (item 1's actual target), cheap, high suspicion
10. **The impact `punch` balloon** — **BUILT 2026-07-27. VERIFY: the number is measured, the FEEL is
    not — it is part of Jake's pass (item 1).** Every unit idles at world scale **0.750**; 0.10 s
    after being struck victims sat at **1.026–1.035**, covering neighbouring units, their HP bars,
    and any arc near them. It **reproduces with every VFX instance hidden**, so it predates the whole
    spectacle arc: a swing's own tell was competing with the victim ballooning over it.
    **Confirmed structurally 2026-07-27:** bars, nameplate and status icons parent to `Root` while
    the punch scales `Body`, so a struck unit never inflates its OWN bars — it grows outward over its
    NEIGHBOURS'. Adjacent hex centres are 1.992 world units apart.
    **⚠ The recorded "~37%" understated it.** 29 of 72 tell rows punch, `punchAmount` spans
    0.18–0.50, and the heaviest row at t=1 reached **+90% — world scale 0.750 → 1.425, near double.**
    **⚠ `impact.punchBoost` alone could NOT fix this** (the 07-26 note assumed it could). It scales
    only the magnitude TERM: driving it to 0 leaves each row's flat `punchAmount` (+25% median)
    untouched *and* destroys the small-vs-big-hit difference `ImpactTune` exists to express.
    **Shipped instead: `impact.punchScale`** — one global dial over every recoil, base included,
    default **0.5**, F1-tunable, hot-reload. Four lines (`TuningData`, `ReplayPlayer:765`,
    `tuning.json`, `tuning.ranges.json`). `PopulateObject` binds by name and the C# default matches
    the shipped value, so a stale `tuning.json` degrades to the intended punch rather than zeroing it.
    | | before | after |
    |---|---|---|
    | median tell, chip hit | +25.0% | +12.5% |
    | median tell, big hit | +45.0% | +22.5% |
    | heaviest tell, big hit | +90.0% | +45.0% |
    Gate: headless client compile 0 errors, **negative-controlled** (injected error caught in the
    changed file, clean after revert). **Not watched in motion — nobody can (Play Mode is unreachable
    from a session).** If 0.5 is wrong, it is one F1 slider, no rebuild.
11. **Overtime is completely invisible — a pillar renders as nothing.** — **BUILT 2026-07-27 (THE
    WANING). VERIFY: machine-gated green, never seen — the Unity lock was held by Codex all session.**
    `Battle.OvertimeStartTick = 900`, after which `Cause.Storm` deals ramping damage to every unit
    every tick. The pitch calls this a pillar (*"escalating overtime clock guarantees resolution"*)
    and theme.md names it **the Waning**.
    **⚠ The root cause was worse than "no clock", and it is worth keeping.** Storm damage had no tell
    of its own, so it fell through to the **generic `DamageDealt` row, whose `minAmount` is 5** —
    and the ramp *starts at 1*. So the first **12 seconds** of overtime drew literally nothing, and
    from damage 5 on it drew **ordinary orange damage numbers with no attacker.** "Units started
    dying for no reason" was not an exaggeration; it was a precise description.
    **NO SIM CHANGE — this was render-only all along.** `Cause.Storm` damage events were always on
    the wire. (`EventKind.StormTick` is declared but **never emitted**; only the enum and `EventText`
    reference it. Do not build on it without emitting it first.)
    **Built:** a world-space **Waning clock** over the board with three states — elapsed `M:SS` ·
    `THE WANING IN M:SS` once inside `warnLeadTicks` (default 150 = 15 s) · `THE WANING — N/TICK`
    showing the storm's CURRENT per-tick damage, the only thing on screen that says *getting worse*.
    Two latched feed beats ("The Hour is running out", "THE WANING — the storm takes everyone") that
    re-arm on a loop wrap. A `byCause: Storm` tell row so storm damage stops borrowing ordinary
    combat's number. All of it lives in a new `waning` tuning block (show/size/height/warnLead + 3
    colours), F1-tunable and hot-reloadable.
    **Design call worth knowing:** the storm renders **globally, as one clock — numbers and punch are
    deliberately OFF on the storm tell.** It strikes every living unit every tick, so per-body
    numbers would be ~40 floating numbers a second and every unit ballooning at once would be item
    10's defect with the volume up. The clock carries the state, the feed carries the two moments.
    **New render fixture `overtime`** (`scenarios.json`, data-only — `Scenarios.cs` was untouched
    because Codex owned it): a warden/lifebinder mirror stalemate that runs **1083 ticks** with
    **931 storm damage events over ticks 900–1082 ramping 1→7**, and all 3 deaths after overtime
    opens. Nothing could see this feature before; now anything can.
    **Gates:** 460 tests green · client compiles (negative-controlled harness) · the clock's readout
    formula reproduces the fixture's real storm output **exactly at both ends of the ramp** (1 at
    tick 900, 7 at tick 1082) and its `M:SS` agrees with the toolchain's own 108.3 s · **all 10
    pre-existing replays regenerated byte-identical**, which also independently proves Codex's
    uncommitted `Scenarios.cs` change is behaviour-preserving.
    **NOT seen.** `BuildPreview(tick)` routes through `LayoutStory(true)` → `LayoutWaning`, so a
    capture at tick ~950 verifies the clock in edit mode **without Play Mode** — do that first when
    the Unity lock frees. The feel (does it read? is it in the way?) is Jake's.

### P3 — settled laws the build does not yet keep
12. **Enemy disclosure stops short of the deep inspector.** — **partly addressed.**
    `pve-encounters.md` requires attacks, signatures, passives, triggers, **and targeting rules**
    inspectable before deployment. ADR 0024 added per-unit role + behavior notes to `EncounterBrief`
    (a Sanddrift Gunner's "acquires FARTHEST, holds standoff 5" is now disclosed). Still open: the
    deeper inspector — full signature/passive text on an enemy, as Muster cards already do for heroes.
13. **The endless seam does not exist.** — **DESIGN → then small.** ADR 0016's identity and the
    first-playable content budget both include a *"crude post-win continue-until-defeat seam."*
    `RunPhase.Complete` is terminal; nothing in `RunController` continues past the last act.
    theme.md's candidate name is **Beyond the Hour**. Cheapest honest version: on Complete, offer
    CONTINUE, re-entering act 3's pool at escalating scale until a loss.
14. **Act identity** — **DONE 2026-07-26 (mechanically), BUT REQUALIFIED 2026-07-27.** Acts draw
    genuinely different pools and acts 2 and 3 are *disjoint*. Two new encounters, zero new roles.
    **What it bought is thinner than "done" suggests:** the two encounters authored specifically to
    give act 3 an identity — **The Slagworks and The Long Procession — both measure FREE + FLAT at
    act 3**, the act they exist for. So the pools differ by name and composition while posing the
    same nothing. Do not re-open this as a composition item; it is the same balance wall as **item
    18**, and the honest status is "disjoint pools, no differentiated difficulty".
15. **~~The Interlude is a non-choice.~~ STALE — the claim was wrong. Corrected + FIXED 2026-07-27.**
    The Interlude **is** a real three-way decision and has been since ADR 0019: `BuildInterludeBeat`
    offers Treasury (certainty) / Armory (equipment) / Hourstone (a run-wide rule), each drawing up
    to `RewardChoices` distinct offers, and the choice **also unlocks the next field capacity**.
    Anyone taking the old item at face value would have built a system that already existed.
    **The real defect was a copy contradiction, and it is now fixed:** the map node still announced
    *"A QUIET STRETCH — No one contests the road. Take the coin and move on"* with a `TRAVEL ON`
    button — telling the player to skip the decision the game was about to hand them, one screen
    later. Now reads AN INTERLUDE / "Take certainty, equipment, or a run-wide rule — and the field
    slot that comes with it" / `TAKE THE INTERLUDE`.
    **Still genuinely unspent:** the content budget funds **one EVENT** — a risk/reward beat with a
    real gamble, distinct from a reward pick. Nothing like that exists. That is the live remnant of
    this item, and it is DESIGN (tiny).
16. **Defeat/retry rule — SETTLED 2026-07-26, no work item.** **Jake's call: terminal loss STAYS —
    the mitigation is save/resume (item 7), not a retry currency and not softening the encounters.**
    Recorded rather than deleted so the next session does not re-open it. Do **not** tune act 2's node
    pool down to address run length; if the cliff hurts, it hurts real playtesters first (ADR 0001).

### New 2026-07-27 (promoted out of footnotes during the hard cut)
17. **Silence is disclosed but unplayable — a shipped honesty defect.** — **SPEC'D. Jake decided the
    lever 2026-07-27: an INSCRIPTION.** `grep StatusKind.Silence` across `Warband.Content/` returns
    **zero** hits in Kits, Weapons, or Catalog (re-verified 2026-07-27). Players have Stun only
    (Shield Slam, Banner of the Held Line). Meanwhile authored encounters name Silence as an intended
    answer, **in player-facing disclosure text on two of three act bosses**:
    - `Encounters.cs:249` — "Silence and Stun both stop the clock." (Ninth Bell)
    - `Encounters.cs:502` — "Silence stops the clock; Stun holds it." (**Ashfall Battery, act 2 boss**)
    - `Encounters.cs:538` — "Silence stops the bell completely" (**The Waning Crown, act 3 boss**)
    - `Enemies.cs:218` — the Crown's mana gain is *gated on Silence*, so the bell is designed around it
    - `roster.md:210` claims the roster covers "Stun, Taunt/**Silence**, Slow, Haste, Mana" — **wrong,
      also fix this**
    So ADR 0024's disclosure contract advertises a lever the game does not offer. That is a
    content-honesty defect, not a content-expansion request. **Why an Inscription:** it stays inside
    the 24-effect ADR 0017 proof and spends **none** of the hero-kit content budget.
    **Build note:** needs a target selector. `SelKind` has no Mana ordering or Mana threshold today
    (only `BelowHpPct` / `MustHave`), so **"nearest enemy with Mana" is the cheap shape** and a
    highest-Mana selector is the general one. Depends on / lands with item 5a.
18. **The authored encounters do not actually pose problems — PARKED BY DOCTRINE, not solved.** —
    **measurement, not a work item until playtest #1.** Promoted out of a Done-entry footnote because
    it directly contradicts item 2's premise. **Re-measured 2026-07-27 — and the shape is worse than
    "4 of 6": the failures are at the acts each encounter was AUTHORED FOR.**
    | encounter | debut | at its debut act |
    |---|---|---|
    | The Gnawing Hour | 1 | **FREE + FLAT** (and at acts 2 and 3) |
    | The Long Range | 2 | **FREE + FLAT** (rule fires only 75%) |
    | The Slagworks | 3 | **FREE + FLAT** |
    | The Long Procession | 3 | **FREE + FLAT** |
    | The Ninth Bell | 1 | poses a problem — spread 100, but FLAT at acts 2–3 |
    | The Drop | 1 | poses a problem — spread 100 at every act |
    **The bosses are the counter-example and the thing to protect:** all three admit 3–4 answer axes
    at spread 100. Whatever eventually fixes the node pool must not be allowed to flatten them. **Root cause, found 2026-07-26 and uncomfortable:** *the gap between the four
    answer-axis parties and the weakest legal comp is wider than the band an encounter can sit in* —
    nothing can be made sharp for one without being lethal to the other. Every composition that
    fixed the flatness drove the naive bot line from 3/12 completed runs to **0/12**.
    **That is a BALANCE finding, and the content doctrine parks balance until the interactive
    playtest.** Two further cautions for whoever picks it up:
    ① **Party size is the strongest difficulty dial in the game, and it is not a stat** — The Long
    Range admits 3 answers with spread 100 against three heroes and is FREE from every formation
    against four. Every probe table now prints hero count; always check it.
    ② the earlier ADR 0023 numbers **could not be bisected** (the whole implementation was
    uncommitted), so **re-measure with `make baseline` before trusting any number here.**
19. **Every instrument measures a BOT. Nothing measures a human — and nothing plans to.** —
    **DESIGN (small), found 2026-07-27 during the roadmap review.** `run.*` is a default-policy bot
    (no placement, no purchase decisions) over 120 runs/tier; the `--enc` "naive line" is a
    fixed-comp bot at 2/12. **Both are floors, not forecasts** — the whole point of the game is the
    two levers the bots do not pull. So the honest state is: *we do not know the human win rate, and
    we have no way to find out.* ADR 0001 says playtests decide and the content doctrine parks
    balance until playtest #1 — but **nothing on this board captures what playtest #1 yields**, so
    the decision it is supposed to settle would arrive as anecdote. Cheapest honest version: the run
    already serialises (item 7) and every fight is re-simulable from (seed, snapshots,
    contentVersion), so a per-run outcome line appended locally is most of it. Settle **what to
    record and how it comes back** before friends play, not after.

### Laws pages (keep their numbers — ADRs and design docs reference them by name)
5. **PvE-first playable loop** — **LAWS PAGE, not a work item** (dissolved 2026-07-26). ADR 0016
   supersedes mandatory ghost bosses: PvE is the product, encounters are authored and asymmetrical, a
   completed run has a final PvE victory, and the winning warband may continue into endless until
   defeated. `IRunContent.Boss(act, rng)` returns an AUTHORED comp · `RunPhase.Defeated` is terminal
   — **lose any fight and the run ends** (Jake's PoC rule) · `Victory` = reached the end of the last
   act, NOT the old best-of-5 `BossWins >= 3` · ghost-capture removed.
   **`RunController.PreviewEnemies(tier)` exists because the encounter rng derives from private salts
   — never reconstruct a preview client-side**, it will show an army that does not spawn.
   ADR 0019 + 0020: three acts of Fight/Fight/Interlude/Fight/Boss · terminal losses ·
   Stable/Fraying/Collapsing fixed rewards · choose 3 of 5 opening draft · Hall → Wager → Deployment
   → Combat · Sand Market/Armory/Hourstone · 3→6 capacity unlock.
   **Balance law:** preserve spectacular system-breaking engines; intervene only when one line erases
   discovery, all encounter problems, determinism, resolution, or readability.
   **Settled design law** (`Design/pve-encounters.md`): the encounter itself is the boss · every boss
   is a multi-answer strength exam · the boss mechanically rules and teaches its act · enemy
   formations are always previewed before deployment · all mechanics are inspectable before Play, the
   rules known but the outcome not forecast · boss units have **no blanket control immunity**, only
   explicit previewed passives may negate a specific verb · Execute is a true kill preserving normal
   death/transform consequences · Phase grants complete personal absence while encounter clocks
   continue · fields are factional by default, environmental/volatile ones may affect everyone ·
   fight flow is Encounter Reveal → combined Planning → Play → Result, with lineup, equipment and
   positions freely editable together until `BEGIN FIGHT`.
   Remaining scope is tracked elsewhere and must not be re-derived here: risk-tier mutation → item 2④
   · endless seam → item 13 · defeat/retry → item 16 · encounter sharpness → item 18.
   **Parked extrapolation (2026-07-24, never taken up):** the **Dying Procession** — an escalation of
   the Last Oath's bonded pair — remains a possible extrapolation, not current scope.
5a. **Hourstone / Inscription engine layer** — **BUILD (acquisition/UI seed integrated; engine
    catalog next).** The expedition carries one Hourstone; every distinct Inscription acquired
    remains active for the run with no slot cap. Player-facing presentation is a compact top-screen
    badge rail driven by replay events: inspectable badges pulse on activation, counters expose
    progress, and high-frequency triggers coalesce rather than flash-spam. Catalog target is 24,
    staged as five migrated seeds → twelve-family vocabulary proof → twenty-four engine proof.
    Hybrid acquisition is live: 20%-weighted 7-Sand Workshop offers plus visible one-from-three
    Hourstone Interlude and boss rewards. **The Hourstone tool shows owned rules; the combat
    badge/counter rail remains unbuilt.** Before catalog expansion, settle the per-root activation
    guard, Bearer of the Mark replacement, and the first twelve contracts. Legacy `Banner*` code names
    are migration debt. **Item 17 (Silence) should land as part of this catalog work.**
6. **Friends playtest #1** — the milestone that ends arguments (ADR 0001), after the PvE vertical
   slice. Distribution/launcher work is allowed only as needed to put that slice in friends' hands.
   **Mechanically, only item 9 still blocks it** — items 7 and 8 are done and the site is live.
   No date until Jake calls it.

## Client — architecture, and where the bodies are buried
Bring-up (item 4), render polish (4b) and the PoC shell (4c) are built and verified; full history in
`Daily/2026-07-23` and `Daily/2026-07-24`. What a future session actually needs:

- **Architecture:** `GameBoot` owns startup order (add a line there — **never** another
  `RuntimeInitializeOnLoadMethod`; four competing ones is why two UIDocuments once raced for input).
  Scenes are **Boot(0) → Game(1)**; Boot holds one `~BootLoader` and nothing else.
  `ReplayPlayer.autoPlayOnStart` is **OFF** by default — the board is driven, never self-starting,
  and the shell parks it on any transition to a non-board screen.
- **Shell pattern (extend, don't fork):** `RunShellModel` (plain view-models) · `IRunScreenView` +
  `RunShellActions` (render out, intent in) · `RunShell` (router, and the ONLY place content ids
  become words). Flow is `ManagementView` → `WagerView` → `DeployView` → board-only Fight;
  management still shares one plain `PlanningModel`.
  `PresentationCatalog` owns art/copy/icon references; composed `UnitDef` owns every mechanical
  number. `WarbandCard` and `InspectorPanel` are shared UXML-backed renderers;
  `PlanningWorkspaceStyles.uss` owns layout/motion tokens.
  **Views may not reference `Warband.*`, so a raw id physically cannot reach the UI.**
  The opening draft is the deliberate exception to universal-card reuse: `MusterCard` accepts only
  three facts and two rules, and exact mechanics disclose inside its portrait.
- **Hydration:** `Warband.Sim.Lexicon` (27 StatusKind + 9 Cause) and `Warband.Content.ContentLexicon`
  (78 spec nodes + 8 chassis). `LexKind` is a **domain, not a valence and not a colour** —
  battlefield colour has one owner, the signature-matched tells in `tuning.json`.
- **Tuning:** `StreamingAssets/tuning.json` + F1 debug cockpit (auto-generates sliders by
  reflection). Hot-reload, no recompile. F2 is the Flow Lab preview.
- **Reused USS across shell and Planning:** inherited *position* is context, inherited *paint* is
  language — override the first, keep the second.

### Client gotchas — these have each cost real time
- **PLAY MODE IS UNREACHABLE FROM A SESSION (found 2026-07-26).** `EditorApplication.EnterPlaymode()`
  inside `Unity_RunCommand` is refused outright: *"User interactions are not supported for MCP tool
  calls."* So **no agent can ever click through the runtime UI** — anything existing only in Play Mode
  (button wiring, shell state transitions, frame-driven feel) is **Jake-only verification, full
  stop.** The workable substitute is a **committed edit-mode Editor script + `ExecuteMenuItem`**
  exercising the real DLLs (`Assets/Editor/RunSaveCheck.cs`, `RenderShots.cs`).
- **`Unity_RunCommand` rejects `System.Reflection`** and its dynamic assembly cannot reference
  Warband plugin types — which is *why* the harness must be a real Editor script. Editor scripts live
  in Assembly-CSharp-**Editor**, so they cannot see `internal` types in Assembly-CSharp either.
- **Refreshing scripts mid-Play-Mode** leaves GameObjects alive but **wipes the code-built UI tree**
  (root has 0 children, console clean). Exit and re-enter Play after every source change.
- **An unfocused Editor idles the player loop**, so `Start()` may simply never have run — same
  symptom, different cause. Pump `QueuePlayerLoopUpdate` before believing a probe. **Sometimes it
  does not recover at all** (2026-07-24: `frameCount` stuck at 1, 40 queued pumps, `isPaused=false`).
  **Verify anything frame-driven in EDIT mode** via `BuildPreview(tick)`, measuring against the
  generated tile transforms as a hex-centre yardstick. Never `Thread.Sleep` in a
  RunCommand to wait for frames — it holds the main thread, so nothing ticks and every sample reads
  identical, mimicking the very bug you are chasing.
- **Exiting Play returns the Editor to the Boot scene**, which holds only `~BootLoader` — so an
  edit-mode probe finds no `ReplayPlayer`. Open `Assets/Scenes/Game.unity` first.
- **Game View capture stalls unattended** (`WaitForEndOfFrame` never completes). Driving the live UI
  tree over MCP is the reliable verification; screenshots need Jake's focused Editor.
- **The Editor and the built player share `Application.persistentDataPath`** — a dev Play Mode session
  and a friend build read/write the SAME `run.save`. That is why a fresh build can appear to "already
  have a run."
- **A check that can silently return "nothing found" needs a positive control.** Cost real time twice:
  a shader grep for invented names reported all six missing, and a `dig` that wasn't installed.
- **Syncthing ignores are PER-DEVICE**, and `sync-status: 100%` can predate the local scan — confirm
  over SSH. **Never write captures inside the Syncthing tree** (they sync back into the repo).
- **`.unity`/`.prefab`/`.asset` edits are guard-hook blocked** — scene work goes through Unity MCP.

## First-playable content budget (hard cap — ADR 0001 + ADR 0016, scope settled 2026-07-26)
Current 8 heroes × 2 paths · 11 weapons + **5 trinkets** · **24 Inscriptions, delivered through the
ADR 0017 proof waves** · **ONE THREE-ACT RUN, one boss per act** (a tiny reusable enemy-role grammar,
several encounters, three act bosses, one event) · shops + placement · crude post-win endless seam
that may reuse and scale the slice · programmer art, no sound.

**Measured against the cap, 2026-07-27** (`make baseline`, fingerprint `3dba11673c26e858`):
8 chassis ✓ · 78 spec nodes · 11 weapons ✓ · **5 trinkets** (this line said "1 trinket" until today —
ADR 0022 added four and the budget was never updated) · **5 Inscriptions of 24** ← *the one place the
build is far under its own budget, and it is the layer ADR 0016's identity depends on* ·
7 enemy unit types · 6 node encounters · 3 act bosses · **1 event still unspent** (see item 15).
Random hero-kits-as-monsters remain scaffolding, not acceptable final PvE content. Do not expand
beyond three acts, to a full endless mode, or to a catalog beyond the 24-effect proof before
playtest #1.

**Scope decision, Jake 2026-07-26. Three acts is the cap.** (This previously read "one act / one
boss", contradicting ADR 0019's shipped three-act shape and ADR 0024's three bosses.)

## Deferred (explicitly NOT now — don't resurrect without Jake)
**All PvP:** ghost server · matchmaking · ratings/leaderboards · PvP rewards · no-stakes Echo
exhibitions (the snapshot seam may remain, but no feature work) · Displacement (Push/Pull/collisions)
· spoils-of-war (historical ADR 0002) · sim-modeled projectile flight ("dodge by movement" lever,
render-contract) · aura ExcludeOwner option · morale/rout concept · ability crits · predetermined
terrain (NEVER) · account-scoped power (NEVER — fairness law).

**Deliberately NOT proposed** (so the next session does not re-derive them): more heroes, more
weapons, a second trinket family, multi-act expansion, difficulty ladders, PvP-adjacent anything,
and any balance pass on hero kits — all either capped by the content budget or forbidden by the
content doctrine until playtest #1.

## Open design questions (ammo for DESIGN sessions)
- **Bearer of the Mark replacement** — `Inbox/warband_roster_expansion_plan.md` proposes **Living
  Inscription** ("when an Inscription activates, Vespera gains 5% Mana, at most once per root event").
  It scales with *activation* rather than collection size, so it cannot make Vespera mandatory just
  because a run owns many Inscriptions — the exact failure of today's blanket `DoublesBanners = true`.
  Worth taking as-is when the Inscription event layer (item 5a) lands.
- **Wide Banner** reads as "inner circle gets innate+crown" instead of "reach replaces" — proposed as
  the actual design, needs Jake's nod. **ADR 0022 makes it a one-liner:** `SignaturePatch =
  Patch(radius: 1)` grows the Rally radius.
- **Content-fidelity leftovers** (2026-07-23 de-SIMPLIFY pass): **Twist's crit-memory** is a 30-tick
  Mark, not "since last cast" (cast-event ordering) · War-Priest does not yet acquire mace mastery ·
  Tower Shield has no base defensive stat · reforged-item resale does not remember forge spend ·
  returning to an implicit starter resets its temper · Company Standard expresses "Company potency"
  as an adjacent opening-Haste muster.
- **Inscriptions:** pool assignment · first twelve effect contracts · per-root activation
  representation · legacy Banner-data migration.
- **Balance/economy:** risk-tier mutation shape (item 2④) · endless cycle, post-rank-S decisions,
  scaling, score (item 13) · Sand/economy values (ADR 0019 tuning until sweep/playtest) · respec cost
  (free-for-now decided, revisit) · per-rank stat scaling.
- **Named-not-tuned outliers** (recorded so they are not re-discovered): `banneret` is CHASSIS-DEAD
  (avg 13%, best build 18%) · four node pairs lopsided by ≥25 (shade.reaper vs phantom Δ-52,
  sniper.onebreath Δ-47, bulwark.juggernaut vs warden Δ-46, phalanx.pikewall Δ+30) ·
  `shade:reaper+widowmaker` dead at 8–9% · The Long Range's ward never comes off for the `control`
  axis (its disclosed answer never happens) · `reach` cannot clear the act-1 boss at all.

## Done — one line each; full detail in `roadmap-done-archive.md`
- **2026-07-27** — Workbench overhaul (Market Recruit R5, Armory Mode R4, keyword + equipment
  tooltips R6): object-centric Workbench, live dossiers, permanent equipment rail, paged Armory,
  runtime tooltip layer, no scrolling; 50-case viewport/copy matrix PASS. **Uncommitted.**
- **2026-07-26** — Candidate content + first third path (Sharpshot Spotter), authored but unreachable;
  `Kits.Candidate*` registries, `IncludeCandidates` default false, fingerprint provably unchanged.
- **2026-07-26** — Inbox Market UI redesign + equipment preview (455 tests).
- **2026-07-26** — Variable-arity spec offers + seeded pool draw + fork-rank law (455 tests); the spec
  tree was the only deterministic layer in a run. Zero behaviour change, fingerprint identical.
- **2026-07-26** — Act-scoped encounter pools (closed item 14); acts 2 and 3 now disjoint, two new
  encounters, zero new roles (446 tests). Surfaced the balance finding now tracked as item 18.
- **2026-07-26** — Persistent Warband bar + atomic loadout transfers (249 sim + 195 run tests).
- **2026-07-26** — UI proposal slice 1: Hall hierarchy + compact warband bar.
- **2026-07-26** — Balance instruments: 4-axis `--enc`, `make baseline` (104 metrics, A/B by
  `git diff`), `make enc` / `make boss`.
- **2026-07-26** — Routing + the engagement law (ADR 0025): Dijkstra flow field to the engage ring,
  bodies a detour at `BodyCost = 6`. **Watch `BodyCost` at playtest — the one tuning constant.**
- **2026-07-26** — The site is live and the launcher pulls from it (closed item 8).
- **2026-07-26** — First standalone build + launcher/delivery (item 8); the shader landmine was real
  and the preflight caught it.
- **2026-07-26** — Content version stamp (433 tests): computed FNV-1a-64 fingerprint of the content
  graph, not a hand-bumped constant. Replays deliberately unstamped.
- **2026-07-26** — Run save/resume (item 7, 412 tests), verified on Windows.
- **2026-07-26** — Act bosses + the disclosure contract (item 2 ①②, ADR 0024, 392 tests).
- **2026-07-25** — Authored PvE encounters (item 2, ADR 0023, 368 tests); the `--enc` probe.
- **2026-07-25** — Unit behavior layer + weapon cadence + signature patches (ADR 0022, 346 tests).
- **2026-07-25** — The Last Oath's decision is reachable (item 3, 313 tests) — geometry, not numbers.
- **2026-07-25** — Fight-legibility phases 0/1/4-sim + combat-spectacle arc P0–P6, nine commits.
- **2026-07-24** — First-playable run + persistent Planning UX (ADR 0019, 278 tests).
- **2026-07-24** — Playable PoC shell + deployment + scenes (263 tests).
- **2026-07-24** — Render + data systems (item 4b): `scenarios.json`, `TellMatch`, the Lexicon.
- **2026-07-23** — Unity client bring-up (item 4); outlier sanity sweep; hero/build content pass;
  sim mechanics build queue; PvE-first identity amendment (ADR 0016); design campaign complete.
- **2026-07-22** — Design foundation (ADR 0001–0009); sim framework (65 tests); run layer (109 tests).
