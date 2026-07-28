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
so the first 12 s of overtime drew *nothing*. **Capture verified 2026-07-28** (see item 11); the feel is Jake's.
**2b. ~~Sim/render audit — the three cheap wins.~~ BUILT 2026-07-27** (`Design/sim-render-audit.md`,
items A/C/D of its ranked arc, Jake's selection). `camera.fov` is a tuning field at last · the cast
sigil now outlives its own payoff instead of closing at it · status flashes fire on ONSET, not on
every re-application. All three are F1-revertible. **The measurement is the deliverable**: the audit
also priced the framing question and found the *board shape* caps it, which is item 22.
**2b. THE MUSTER RING — BUILT 2026-07-27 (late). VERIFY.** Deployment now paints the hexes each
placed muster will catch: quiet outlines for every placed muster, filled for the selected hero.
General over the law, not the Banneret — Cleric's Mercy Aura (r2) and Phalanx's Unbroken Line (r1)
were equally invisible before this. One definition (`MechanicalRulePresenter.MusterSeats` +
`RunController.IsDeployable`, 13 tests) so the board and the lock-in validator cannot disagree.
**Board API capture-verified** (`Warband/MCP/Capture Muster Rings`); the `ShowDeploymentOnBoard`
call into it has never run — no live run reached Deploy. **That is what VERIFY means here.**
Open: overlapping musters share one gold, so whose ring is whose is not readable.
**3. JAKE'S VERIFY PASS (item 1). ← NEXT, AND IT IS YOURS.** Both cheap feel wins have landed, so
the pass now judges a build with the balloon halved, the storm visible, the sigils held, and the
status strobe down ~26% in a cast-heavy fight.
**4. ~~Item 1c — THE COMBAT RECAP.~~ BUILT 2026-07-27** — contribution bars, damage composition
and death timeline all ship, folded in `Warband.Sim/CombatRecap.cs` (8 headless tests) and drawn
by a client that computes nothing. The client's **first graph of any kind**. Composition reads
every `Cause`, not the harness's five, so Counter and Trigger get slices — that is the
*"why did my build work"* chart ADR 0016 wants. **Pixels unseen** (Play-Mode-only surface); the
capture path is one menu command and its fixture no longer passes vacuously. See item 1c.
**5. Item 5a — the Inscription engine layer. ← SET BY THE 2026-07-27 ROADMAP REVIEW (Jake).**
The review measured the build against its own budget and found one large gap: **Inscriptions are at
5 of 24**, and that is the layer ADR 0016's north star — *compounding builds that feel like they
break the game* — actually lives in. Everything above it in this list is render and shell. It also
absorbs **item 17** (Silence), and unlike items 4 and 18 it is **not** blocked behind the balance
question the content doctrine parks until playtest #1. Target the twelve-family vocabulary proof.
**5b. THE PERSISTENT INSCRIPTION RAIL — SPEC'D BY JAKE 2026-07-27 after playing the Stilled Bell.**
His words: *"we should take this from guildrun's book … a persistent icon rail somewhere (maybe top
of the screen?) EVERYWHERE … during shop/UI as well. Then the icon should flash and draw a quick
indicator to the affected units."* Supersedes the world-space TextMesh rail (hours old — delete it,
keep its laws: coalescing, fold-driven pips, team-0 only, acquisition order).
**Spec:** ① screen-space UI Toolkit strip, top of screen, in the SHELL's persistent layer so it
survives Management/Wager/Deploy/Fight alike · ② one icon per owned Inscription (programmer-art
glyphs in `PresentationCatalog` — 12 needed, no art dependency) · ③ hover/press tooltip = full rule
via `MechanicalRulePresenter` (same copy as the Hourstone tool) · ④ counter pips under the icon
(fold's `RuleCounters` in fight; static elsewhere) · ⑤ on `TriggerFired`: icon flash + a brief
indicator line from icon to the affected unit's screen-projected position, coalescing under the
passive-onset ration so cast-storms don't strobe · ⑥ fight bridge: `SkirmishController` owns both
the shell surface and `ReplayPlayer`, so the event hook and world→screen projection live there.
**Player laws only (settled with the v8 wire); enemy laws stay on the encounter reveal.**
**REVISED BY JAKE 2026-07-27 before the build went deep:** the rail is a **limited-size icon TRAY**
— compact fixed footprint collapsed, hover "opens" it like a **drawer on top**, and only inside the
open drawer do individual icons give their tooltips. Use the existing `DrawerExpand`/`DrawerCollapse`
cue pattern (sound + motion conventions come free). Collapsed tray: capped width, overflow as
"+N". **IN COMBAT the tray is ALWAYS EXPANDED to the full rail — every icon visible, no hover
needed (Jake 2026-07-27): you must be able to read activations at a glance while fighting.**
Collapsed/hover-drawer behavior applies to the non-combat surfaces (Hall/Wager/Deploy).
**⚠ VERIFICATION IS PART OF THE SPEC (Jake):** follow existing UI conventions (`UiEnvironment`
sheets, accent classes, layout tokens), and prove with SCREENSHOTS that no existing page breaks —
before/after captures of Management/Wager/Deploy/fight at the size matrix (1024x768@130%,
1280x720, 1600x900, 2556x1317, phone), READ BY EYE, per the flex-shrink lesson. Expect rework.
**Build note:** follow `WarbandBarView`'s exact pattern — constructed once in `RunShell.BuildUI`
into `_safeAreaFrame` beside the Warband Shelf, shares `RuntimeTooltipService`; views never touch
it (data via `RunShellModel`, ids→words in RunShell only). Icons: per-inscription glyphs are in
`PresentationCatalog` (all 12, done). `InscriptionRailView.cs` exists and compiles — rework it to
the tray/drawer shape rather than starting over. Fight flash/indicator: `SkirmishController` hears
`TriggerFired` via the player's dispatch and projects unit→screen for the line; respect the
passive-onset ration. The v1 world-space TextMesh rail in ReplayPlayer still awaits deletion.
**5b PROGRESS 2026-07-27 late:** tray BUILT + wired (RunShell persistent layer, QA fixtures push
seven laws, combat pins expanded). Smoke matrix before/after: baseline 14/15, after 13/15 — BOTH
failures are the pre-existing `rail-full` header/Market overlap, now at 2 viewports because the
market row gained a 5th card type (INSCRIPTION offers — the PARALLEL session's content, landed
mid-run; their surface, flagged not fixed). Tray verified by eye on the contact sheet at every
surface/viewport; two seed glyphs were font-tofu, swapped. FIGHT BRIDGE BUILT 2026-07-28 00:xx (`ReplayPlayer.LawDispatched`
→ RunShell → Flash/pips/`InscriptionIndicatorLayer` lines; team-filtered by `RuleTeamOf`), v1
world-text rail DELETED, `make check-client` green. POST-BRIDGE VERIFY DONE 2026-07-28 AM:
smoke matrix **17/17 PASS** (`ui-qa/20260728-093053/`, six captures read by eye at both
viewports) — the two pre-existing `rail-full` header/Market overlaps now PASS, fixed en route by
item 24's header/market reshape; the tray reads correctly on Workbench/Wager/Deploy/Result.
World-rail absence capture-proven (`McpCaptures/verify5b/hourstone_t24/t200.png` + a live
world-TextMesh inventory — only StatusIconRow/feed/clock texts remain). LEFT: VERIFY-in-motion
with Jake (drawer hover, flash/indicator feel).
**6. ~~Item 9 — the options screen.~~ BUILT 2026-07-28 (see item 9). VERIFY in motion is Jake's** —
the last P1 blocker on friends playtest #1 (item 6) is now a modal + three entry points, all seams
pre-existing.
**7. Then re-decide.** Standing candidates: item 1d (camera) · item 19 (measure a human) ·
items 12, 13, 15's unspent event. Items 4 and 18 are one balance question wearing two hats, and the
doctrine holds them until playtest #1.

**24. WORKBENCH DOSSIER & ARMORY-DRAWER REDESIGN — BUILT + CAPTURE-VERIFIED 2026-07-28
(overnight, Jake's directive). VERIFY: the in-motion feel pass is Jake's.** *"The dossier is
quite crowded … per-type formats … remove the armory tab, keep it like a drawer on the
footer."* Research-first (three UX reports: autobattler shops, card anatomy, progressive
disclosure), decision in **`Design/workbench-dossier.md`**; full build/loop record in
`Daily/2026-07-28.md`. Core moves: section ROLE (Primary/Deferred) replaces width/index
demotion — deferred = compact row + hover, never hidden · signature-first hero dossiers ·
rank-up gains as before→after rows (delta chips deleted) · spec options show the AUTHORED
lexicon one-liner, full generated rule on hover (2026-07-28 AM — machine prose has no sentence
break; Pikewall was the clipped repro; `market-rankup-long` is now the real Phalanx fork) ·
stat rail leads the detail column · armory side column deleted → footer drawer band above the
unit rail, Market always live, drawer-open dossier always compact · peek strip deleted
(2026-07-28 AM, Jake) — the footer ARMORY chip is the one drawer handle, hint state-driven
(`DROP TO UNEQUIP` / `OPEN DRAWER ▴` / `CLOSE DRAWER ▾`) · synthetic passive filler
gone · fixes: comparison-cell overlap, `accent--choice`/`stilledbell` accents, RESERVE wrap,
roster contract vs at-capacity warbands, 1024 cost-digit clip. **Workbench Full Matrix 68/70**
(overnight round 11 `ui-qa/20260728-025334/`; morning round 3 `ui-qa/20260728-091422/`), the 2
residual rows are a 2556×1317 subtitle measurement artifact — capture shows the text intact.
⚠ Process laws from the loop are in the daily note: the matrix leaves play mode ON
(stale-assembly stalls) · Hall base styles leak into `--workbench` scopes (`justify-content:
center` cost five rounds) · the unfocused-Editor player-loop freeze is SOLVED —
`Application.runInBackground` pinned in `WarbandUiQa` (2026-07-28 AM), full matrix ≈ 70 s. Deferred polish recorded in the note:
weapon-tier augmented marking · WHEN/THEN trigger anatomy · text-budget CI · rank pips ·
paradox badge · rule-delta rows clipped at drawer-open (info still on tile hover).

**SESSION HYGIENE — RESOLVED 2026-07-28.** The whole 07-27/28 build wave is committed as
`cc058c2` (104 files, 5020 insertions) after Jake confirmed no Codex session was live; `make test`
green at **514 (275 sim + 239 run)** at commit time. `client/TempCaptures/` and `.playwright-mcp/`
are now git-ignored (capture artifacts — synced for review, never history). Keep it this way:
commit at stream boundaries, don't let verified work pool uncommitted.

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
deadtime 1.81%) · **Inscriptions 12 of 24 — ADR 0026 wave 2 landed 2026-07-27** (was 5 at review
time, and closing that gap was this review's whole point). The three-act run, the shell, save/resume, the build and
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
1e. **Responsive Workbench correction pass** — **BUILT + VERIFIED.**
    Jake's 2556×1317 capture found visible command-text escapes, an art-starved always-compact
    Market, and Rank Up split across three redundant internal pages despite a 57/57 structural
    report. Root causes and two one-screen corrections are in
    `docs/ui-reviews/outbox/responsive-ui-v1/`; `01-one-page-choices-r1.png` approved 2026-07-27.
    Focused build complete: typed one-page Rank Up, B/A/S tier ladder + tooltips, contextual
    inline trait labels, visible Market art, independent responsive axes, TextElement/button
    layout checks, semantic diagnostics, and B/A/S fixtures. The reported pending-fork crash
    (`sharpshot|A|-`) is guarded in both the run projection and Workbench action state, with an
    exact live-controller regression. Headless smoke **15/15 PASS** at 1280×720 + 1600×900.
    Full matrix **82/82 PASS** across 1024/1280/1600/2556/3440, expanded-copy, phone, Armory,
    tooltip, route, and rotation states; final captures reviewed under
    `client/TempCaptures/ui-qa/20260727-191233/`. Semantic follow-up complete: authored glossary
    concepts now become themed hover targets inside their rule sentence (`Gain 1 Riposte`) instead
    of consuming a detached keyword row. A dedicated Workbench-only full runner keeps this surface
    independently verifiable; its post-migration matrix is **65/65 PASS** with no scrolling or
    content/action overlap under `client/TempCaptures/ui-qa/20260727-202843/`.
1f. **Persistent Warband footer roster manipulation** — **BUILT, VERIFY.**
    Stable-ID drag/drop now moves into open field/reserve slots and atomically swaps occupied
    slots; Space/Escape provides keyboard placement, and the retained footer owns its drag ghost,
    target semantics, and cancellation. `Warband.Run.Tests` 239/239 and the 59-script headless
    client compile are green. Final gate: Unity console + by-eye `rail-open` fixture capture
    (first attempt found the shared editor correctly leased by another session).
1c. **THE COMBAT RECAP — a comprehensive, polished post-fight report.** — **BUILT + PIXEL-VERIFIED
    2026-07-27, after shipping broken once.**
    **⚠ READ THIS BEFORE ADDING ANYTHING TO THE RESULT GATE.** The first build was unreadable in a
    real fight (Jake, `inbox/post-match-recaps/`). Root cause: **every element in UI Toolkit
    defaults to `flex-shrink: 1`**, and the gate is capped at `max-height: 94%` of a 900px
    reference viewport. The recap pushed content past that budget, so Yoga did not clip or
    scroll — it **silently squashed every child**. 22px rows resolved to ~11px, their text spilled
    out of the box, and even the *pre-existing* stat cards dropped their values outside their own
    background. Nothing errored. **Everything with a fixed height in this panel is now
    `flex-shrink: 0`**, so an overrun presents as visible overflow a contract can fail on.
    **THE PROCESS LESSON, which cost more than the bug: a green layout contract is NOT evidence.**
    It said PASS over a broken screen **twice**. ① The first contracts asserted min-font and
    single-line width — blind to a vertical collapse, because the font never changed and nothing
    clipped horizontally. ② After fixing that, the phone layout drew composition, timeline and the
    recommendation *on top of each other* and still passed, because overlapping siblings are each
    the right height and each inside the panel. Both were caught by **looking at the capture**.
    `UiLayoutContract` is a regression net, not a substitute for eyes.
    **Four more defects the captures caught, none of which any assertion would have:** the board's
    own world-space end-of-fight readout drew *through* the gate (two post-fight surfaces at once —
    now `ReplayPlayer.EndReadoutSuppressed`) · the QA fixture still printed the three death labels
    the shipping path had dropped, so the capture rendered a screen the game no longer produces ·
    the name label's own `overflow: hidden` clipped its descenders until given an explicit height ·
    the exit buttons hung through the panel border while `RequireInside` on their *row* passed,
    because the buttons overflow the row, not the row the panel.
    **Verified 2026-07-27:** `Warband/UI QA/Run Result Gate Matrix` (new 5-shot mode — the 82-shot
    full matrix is too slow to iterate one surface against, which is why nobody ran it, which is
    why this shipped) **PASS at 1024x768/130%, 1280x720, 1600x900, 2556x1317 and phone**, with the
    fixture at a **six-hero worst case**; four of the five captures inspected by eye.
    **Still unseen: the double-readout fix**, which needs a real fight — the fixture runs no battle.
    All three approved charts ship.
    **Built:** `Warband.Sim/CombatRecap.cs` — the fold from `FightSummary` to the exact rows,
    segments and marker positions the panel draws (contribution · composition · timeline), with
    **8 headless tests** (`CombatRecapTests`). `CombatRecapPanel.cs` + `CombatRecapStyles.uss`
    draw it and compute nothing; `RunShell` builds it at the existing `FightSummary.Build` call
    site; TOP DAMAGE is gone, replaced by a bar for every hero.
    **Why the fold lives in the sim:** a chart fails in arithmetic (shares that don't sum, a bar
    normalised to the wrong denominator, a zero-tick fight dividing by zero), and arithmetic is
    testable headlessly while a Unity panel is not.
    **Two decisions worth keeping:** ① the bar is normalised to the LEADER while the number is
    the share of the TEAM — six even contributors would otherwise each draw a 17% stub;
    ② composition reads `UnitSummary.ByCause`, not the harness's five-way split, so **Counter and
    Trigger get their own slices** — measured on the act-3 boss, the CONTROL axis reads
    Attack 65 / Ability 19 / **Counter 9 / Trigger 8** where DAMAGE reads Attack 92. That
    difference IS the "why did my build work" chart.
    **The cleric case is handled:** a support hero shows `0 · 0%` damage, so the row carries one
    secondary fact and healing leads it — measured 2093 healed on a real fight, which is the
    difference between "did nothing" and "kept everyone alive".
    **Verified:** 485 tests green (268 sim + 217 run) · `make check-client` 0 errors · a second
    compile with `DEVELOPMENT_BUILD` defined to cover the editor-only fixture · `make baseline`
    **byte-identical**, fingerprint `3dba11673c26e858` unchanged — the recap changed no fight.
    Numbers eyeballed end-to-end on real act-3 boss fights across all four probe axes (ASCII
    render of the same fold).
    **NOT verified: a single pixel.** The gate only exists in Play Mode. **The path is already
    built and is one menu command:** `Warband/UI QA/Run Responsive Full Matrix` covers surface
    `result` at `result-nominal` + `result-phone`. Its fixture carried **no recap**, so it would
    have passed vacuously — that is now `CombatRecapPanel.EditorFixture()`, deliberately the worst
    plausible case (a four-digit heal on a zero-damage hero, a name long enough to need its
    ellipsis, five composition slices, clustered deaths, the Waning on the track) so
    `UiLayoutContract` gates something real. New contracts added to
    `ResultGateView.EditorResolvedLayoutReport`; **height on phone is the live risk** — the panel
    is contract-bound not to scroll.
    **Deliberately still text:** the three death lines stay under the timeline. The track shows
    *when* the fight turned, the lines show *what* happened. If that reads as redundant in the
    play pass, deleting them is one line.
    **Original spec below.** — **RANKED ABOVE 5a BY JAKE, 2026-07-27**
    (*"a comprehensive and polished combat recap, with graphs and such"*).
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
9. **No player-facing options at all** — **BUILT 2026-07-28, machine-gated green. VERIFY: the
   in-motion click-through (menu button, fight button, Esc, audible sliders, live speed change) is
   Jake-only — Play Mode.** The screen is a MODAL over the shell's persistent layer, so one
   implementation serves Menu, Hall and fight: `OptionsPanel.cs` (scrim + `.modal`, applies
   instantly, no OK/Cancel) over `PlayerOptions.cs` (PlayerPrefs store; the only new state).
   Entries: OPTIONS on the menu · OPTIONS beside SKIP on the fight overlay · Esc everywhere
   (accepted collision: Esc during an armed keyboard drag also opens it — Esc again closes).
   **Seams, none invented:** sound on/off + Master/Interface/Battle sliders drive the mixer's
   exposed params via `SfxPlayer.SetBusVolume` (**param names verified against the real
   `GameMixer` asset with a negative control** — a wrong name is a SILENT no-op, the one failure
   mode this build could not afford) · mute = MasterVol −80 dB, so the per-surface enables in
   tuning.json/HubPresentation.json stay shipped defaults · reduced motion reuses the
   `ui.reducedMotion` key + the Flow Lab toggle's exact Rebuild seam · battle speed is a
   0.5–2× multiplier over tuning's `playback.ticksPerSecond` at ReplayPlayer's two read sites;
   a live fight re-reads it through `ReapplyTuning()` (the F1 cockpit's proven path).
   **Verified:** `make check-client` 61 scripts 0 errors · smoke matrix now 18 items
   (`options-nominal` surface + `EditorOptionsLayoutReport` contracts; loaders close the modal so
   it cannot haunt later captures) — **18/18 PASS**, capture read by eye
   (`ui-qa/20260728-095256/`) · scrim measured correct (0.78 alpha composited in LINEAR space —
   it reads lighter than gamma intuition; same class as every existing modal). **Full matrix run
   same day: 90/92** (`ui-qa/20260728-095949/` — the 2 fails are the documented pre-existing
   2556×1317 subtitle measurement artifact, byte-for-byte the same rows as item 24's baseline);
   options PASS at all 5 viewports, phone + 1024-expanded captures read by eye.

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
    **⚠ THE OWED CAPTURE WAS TAKEN 2026-07-27, AND IT FOUND A BUG — in the capture path itself.**
    The instruction here used to read *"`BuildPreview(tick)` routes through `LayoutStory(true)` →
    `LayoutWaning`, so a capture at tick ~950 verifies the clock in edit mode"*. **It does not.**
    `LayoutWaning` reads `Mathf.FloorToInt(_clock)` — the PLAYHEAD, in ticks — and
    `BuildLoadedPreview` set the fold to the requested tick but **never moved the playhead**. So
    every frozen capture computed `tick = 0` and drew a flat **`0:00`** no matter what was
    previewed: at tick 950, fifty ticks into the storm, the clock read `0:00`.
    Play Mode was always correct (`Update` advances `_clock`); it is the *verification* path that
    could not tell the truth — which is the path every check in this project runs through, so the
    blast radius is wider than this one clock.
    **Fixed:** `_clock = tick` in `BuildLoadedPreview`. Hand-checked against the formula
    (t=700 → `1:10` · t=800 → `THE WANING IN 0:10` · t=950 → `THE WANING — 2/TICK`).
    **SEEN 2026-07-28 AM.** Edit-mode captures at t=800 (`THE WANING IN 0:10`, warning gold) and
    t=950 (`THE WANING — 2/TICK`, storm red) verified in pixels AND in the live world-text
    inventory (`client/McpCaptures/verify5b/`) — the readout matches the hand-checked formula at
    both states, so the `_clock = tick` fix is confirmed in the real capture path. The blocking
    `WarbandMixerTools.cs` CS0122 is gone (step 4 landed). The feel is still Jake's.

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
17. **Silence is disclosed but unplayable — a shipped honesty defect.** — **DONE 2026-07-27:
    shipped as ADR 0026 catalog #10, "The Stilled Bell"** (reaction shape: "when an enemy casts,
    Silence the caster 30 ticks" — zero new selector machinery; the Mana-selector build note below
    only applies if it ever becomes a preemptive opener). `roster.md`'s false claim fixed same day.
    Tested (content tests + presenter grammar) and on the badge rail in the `hourstone` fixture.
    **PLAYED by Jake same evening — "worked great." First Inscription verified in a real run.
    His verdict on the same run: the PRESENTATION lags the mechanics → feeds item 1's fix list.** `grep StatusKind.Silence` across `Warband.Content/` returns
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
19. **Every instrument measures a BOT. Nothing measures a human.** —
    **BUILT 2026-07-28, same day Jake spec'd it (mobile chat: log every fight, every purchase,
    every tier selection — the full decision trail). VERIFY: one real Play-Mode run writing +
    uploading is Jake's; everything below it is machine-gated green.**
    **Built:** `Warband.Run/RunTelemetry.cs` — pure line formatter, RunSave's law (no IO, no
    packages, hand-rolled JSON), one JSONL line per event: `start` · `fight` (kind, tier,
    encounter, outcome, ticks, per-hero dmg/pct/healed/died, party+paths — the tier is chosen at
    the wager that resolves the fight, so this line IS the tier-selection record) · `buy` ·
    `reroll` · `slot` · `reforge` · `sell` · `interlude` · `bossReward` · `victory`/`defeat`.
    Run id = seed + content prefix, **stable across save/resume (tested)**; every line
    re-simulable by construction. **5 headless tests** verify the writer against System.Text.Json
    as the independent parser, hostile ids included (519 total green).
    **Client:** `RunTelemetryWriter.cs` appends to `persistentDataPath/runlog.jsonl` beside
    `run.save`; RunShell hooks at BeginRun/resume, BuyOffer, Reroll, BuySlot, Reforge, sells,
    Interlude, boss reward, and fight resolution (brief captured BEFORE resolving — the node
    advances). Fights are the only run-enders, so victory/defeat logs exactly once, then uploads
    fire-and-forget. **Every hook is fail-silent by design** — telemetry can never break a
    purchase. `make check-client` 62 scripts 0 errors.
    **Site:** `POST /api/runlog` (`site/runlog.go`) — static-key spam gate (404 either way),
    1 MiB cap, one file per UTC day under `WARBAND_RUNLOG_DIR` (default `~/warband-runlogs`),
    single-write append so concurrent uploads can't interleave. **Smoke-tested end to end
    locally** (404/404/204/204/413 + file contents). **DEPLOYED 2026-07-28 (Jake's tap) and
    verified against the LIVE site**: healthz ok · unkeyed POST 404 · keyed POST 204 · the test
    line landed in `~/warband-runlogs/2026-07-28.jsonl` (removed after). The sink is listening;
    the next finished run anywhere is the first human data point.
    **Original finding, kept as the argument:** `run.*` is a default-policy bot
    (no placement, no purchase decisions) over 120 runs/tier; the `--enc` "naive line" is a
    fixed-comp bot at 2/12. **Both are floors, not forecasts** — the whole point of the game is the
    two levers the bots do not pull. So the honest state is: *we do not know the human win rate, and
    we have no way to find out.* ADR 0001 says playtests decide and the content doctrine parks
    balance until playtest #1 — but **nothing on this board captures what playtest #1 yields**, so
    the decision it is supposed to settle would arrive as anecdote. Cheapest honest version: the run
    already serialises (item 7) and every fight is re-simulable from (seed, snapshots,
    contentVersion), so a per-run outcome line appended locally is most of it. Settle **what to
    record and how it comes back** before friends play, not after.

### New 2026-07-27 (from the sim/render audit — `Design/sim-render-audit.md`; Jake picked B, E, G, H)
20. **The passive layer has no renderer** (audit headline **B**) — **BUILT 2026-07-27. VERIFY:
    machine-gated green, never watched** (Unity lock held by Codex all session).
    `Design/passive-legibility.md` has the research, the laws and the measured cost.
    **What it was:** `StatRule` — the read-time conditional stats that ARE the passives (Full Draw,
    Burning Hours, Grudgekeeper) — emitted **no event, ever**, and `Trigger` emitted anonymous
    echoes. ADR 0016's north star was the one layer with zero visual representation.
    **What shipped:** rule identity stamped automatically at composition from the contributing
    content (`Loadout.AddRules`), so **new content is identified the day it is authored** — plus
    `D.Named()` for authored enemies/bosses and `Catalog.Identify` for banners. Two appended
    EventKinds (`TriggerFired`, `RuleChanged`), a per-tick StatRule transition sweep, replay **v6**
    carrying the rule table, `ActiveRules` on the fold, and a `byRule` tell filter at +2 specificity
    with two fallback rows. **Zero unnamed rules across every fixture.**
    **⚠ THE INVARIANT WORTH KEEPING:** presentation events are dropped in the drain loop *before*
    they spend cascade budget or scan a trigger — so they are **structurally incapable** of changing
    a fight, not merely tested not to. Proof: `make baseline` byte-identical over 129 metrics and
    the content fingerprint still `3dba11673c26e858` (no save invalidated — `RuleId` is deliberately
    NOT hashed, because the fingerprint exists to catch a retune, not a rename).
    **Cost, measured:** `TriggerFired` runs 1.4–7.1/s raw against a ~21/s budget, so
    `fx.passiveOnsetSeconds` (2.5 s) rations repeats — a passive firing every swing is the engine
    running, not news. Net across 11 fixtures **+5.2%**, and it landed where there was room:
    castfest **20.8 → 18.1/s (−13%)**, wallfort 5.7 → 7.1/s.
    **Open:** the `RuleChanged` badge is a transition pulse, not yet a persistent rim while live —
    the fold has the state, so that is a `StatusIconRow`-shaped follow-on best paired with item 21.
    **Still lands with item 5a** — Inscriptions compile to the same `Trigger` atom, so they are
    already covered by this and arrive nameable.
21. **The in-fight hover card is three bars** (audit headline **E**) — **BUILT 2026-07-27. VERIFY:
    the card exists ONLY in Play Mode, so no session can ever see it — Jake-only, full stop.**
    The card now carries: the identity line (chassis · signature · weapon + temper) · HP/Shield/Mana
    · the placement facts (reach, cadence and step in SECONDS, crit, "swings heal") · **the targeting
    rule** ("Acquires the FARTHEST enemy, holds 5 hexes") · **the passive roster, with conditional
    ones marked LIVE or idle** off `ActiveRules` · statuses by Lexicon name rather than enum name.
    **This is where item 20's persistent-state half landed** — a passive coming online is now
    readable, not just a flash you had to be watching for.
    **Wire cost:** the item claimed `PlaybackUnit` already carried everything; it did not. Targeting
    (`TargetPref`/`Standoff`) and each unit's span in the rule table are new → **replay v7**, all 11
    fixtures regenerated. Both are hashed into `HashView`, which is what makes the round-trip check
    prove the wire carries them — and that immediately caught a real ordering bug: `BuildRuleTable`
    ran AFTER the tick-0 snapshot, so every fight's first tick disagreed with the fold.
    **Copy:** `ContentLexicon.Rule(id)` is the single resolver for every id shape the composer emits
    (spec node · chassis · weapon · `weapon/mastery` · `banner.*` · authored enemy/boss · `#2`), and
    `RuleCopyTests` is the CI contract that **no raw id can reach a player-facing card** — 321 rules
    across all 8 chassis × their nodes at Relic+mastered, positive-controlled.
    **Closes item 12's "deeper inspector on an enemy"** — enemies use the same card, and their
    authored rules (Ward, the Bell, the Bond, Death-fed…) are all named.
    **Open:** an ON-BODY mark for a live passive, so it reads without hovering (the fold has it).
22. **The board is square, and that is what caps the camera** (audit headline **G**) — **DESIGN.**
    6 cols × 8 rows is **0.91 : 1**, and a frontally-framed board fills 16:9 only when
    `width/depth = 1.78 × sin(pitch)` — so 0.91 is optimal at **pitch 31°**, which still stacks
    ~2.3 rows of unit UI on itself. **The shape mathematically forbids a pitch high enough to be
    readable**; a pitch/distance sweep found no setting that unstacks the rows without shrinking the
    units. **8 × 8 at ~45° measures 98% frame fill AND 1.37 rows of overlap** — the only point that
    does both. Costs +16 hexes: `Battle.InBounds`, `Pathing.Cells`, every authored formation, every
    deployment fixture. **Justify on framing, measure for balance (`make baseline` before/after) —
    not the reverse.** The rhombus is NOT the answer: yaw costs frame area (55% at today's 13°
    vs 83% at yaw 0), it does not buy any.
    (Audit headline **H** = the combat recap, already ranked as **item 1c** — no new item.)

23. **A whole sensory channel ships at zero — and the tooling that would fix it does not exist**
    (audit headline **F**) — **BUILT 2026-07-27, steps 0–6. VERIFY: machine-gated green, NEVER
    HEARD.** Plan + full record: `Design/audio.md`. Step 7 (volume sliders) is a screen and belongs
    to **item 9**, not here.
    **Gate state: `make sfx-lint` PASSES against shipped `Resources/`** — 0 violations, 20/20 board
    ids resolve, all 6 UI families present, no silent weapons.
    **What VERIFY means here:** nobody has heard the mix in motion. Open questions only ears can
    answer — does the −6 dB duck read on a death · do crits cut through at ~9.6 onsets/s · is the
    UI tick right at 41–51 ms · is `State` (the tightest bus at 1.6/3) audibly crowded in overtime.
    Folds into item 1's pass.
    **Original diagnosis, kept because it is the argument for the gate:** A measurement pass over all 35 clips replaces "the stings were bad" with
    three separable, measurable defects. **UI:** 14 of 18 clips carry **0.5–1.0 s** of continuous
    audible content (a click is 40–120 ms; `route_1` is a full second for *moving a resource*), the
    level spread across the set is **20 dB** (`error_1` at −20.7 dBFS is the quietest thing in it,
    seating a unit at −0.0 the loudest), and crest runs 13.7→32.9 dB so nothing binds the set into
    one instrument. `commit_1` starts **157 ms** late — audible input lag. **Board:** 27 sound ids
    referenced, **17 clips exist** (the whole per-weapon `hit_*` layer is authored and mute),
    `riser_cleric` starts 156 ms late so the windup cue lands *after* the windup, and
    `ReplayPlayer.PlaySfx` is **one `AudioSource` + `PlayOneShot`** — unbounded overlap at one
    priority, so at the measured **~9.6 sound onsets/s** Unity culls a `death` sting *by audibility*
    in favour of Burn ticks. **No `AudioMixer` asset exists in the project**, so item 9's sliders
    have nothing to drive. **Root cause is process, not taste:** the gate that "passed structural
    validation" (`hall-polish`) checked that files *imported* — never onset/length/level/crest — so
    a regenerated batch has no reason to beat this one. **Headline finding: length beats voice
    management.** Cutting impacts 0.8 s → 0.2 s takes sustained concurrency ~8 voices → ~2.
    Plan: two policies over one substrate (ported subset of Shoota's `SfxPlayer`; **no FMOD/Wwise**,
    **no `AudioRandomContainer`** — it is an editor asset per family and fights JSON hot-reload)
    plus the missing `sfxlint` / `sfxbake` / audition-sheet tooling.
    **Jake decided 2026-07-27:** build steps 0–2 now (D5) · **cut** the Hall ambience bed (D1) ·
    **collapse** the 11 per-weapon impacts to ~5 material families (D3). D2 (re-bake vs regenerate)
    and D4 (combat bed) answer themselves off the audition sheet.
    **STEPS 0–2 BUILT 2026-07-27** — `tools/sfx/sfx.py` (measure/lint/bake/sheet/density) +
    `families.json` + five `make sfx-*` targets. **28/28 clips baked and passing**; UI ticks land at
    **41–51 ms** with ~1 ms onset, and the set now sits in a **±2 dB** window where it spanned 20 dB
    (`error` alone went −20.7 → −4.0 dBFS). Only **3** clips are genuinely missing after the D3
    collapse, down from 10. Working files are under `docs/audio/`, **deliberately outside
    `client/Assets/`** so Unity never imports them; **`Resources/` was not touched, so the game is
    bit-identical** — promotion is step 5, with the code change that renames the families.
    **ENDINGS PASS 2026-07-27** — Jake reviewed: *"much better than before … overall massive
    improvement"*, one defect: *"some def end really abruptly."* Measured it: **12 of 28 clips were
    cut while still near full amplitude** (`riser_phalanx` at **−3.0 dB**) with the same 12 ms fade.
    Two causes — ① one fade served two different endings (natural decay vs cap truncation), now split
    into `fadeOutMs` 12 ms linear and `releaseMs` 60–160 ms **exponential**, inside the length budget
    so density is untouched; ② **the caps were a board law applied to surfaces with no density
    problem** — `bind`/`major`/`error` are once-per-interaction and `riser_*` is a one-per-cast
    windup that §5.2.3 says *should* be long. Raised those, held every family that repeats. Cost:
    concurrency 4.9 → 5.5, well inside the per-bus caps. **28/28 pass; every clip now ends ≤ −34 dB.**
    Two tool bugs found by verifying rather than assuming: truncation detection was a **one-sample
    coin flip** on zero crossings (`cast_generic` missed by one and shipped gated), and the dead-tail
    threshold must stay **peak-relative** — the shipped padding is low-level noise, not silence
    (`select_1`: 820 ms of tail at −34 dB rel., but only 20 ms at −60 dBFS), so an absolute floor
    would score the worst clip in the set as clean.
    **STEP 3 BUILT 2026-07-27, compile-verified headless.** `Scripts/Warband/SfxPlayer.cs` — 24-voice
    pool, five buses (`Ui`/`Decisive`/`Cast`/`Impact`/`State`), priority ladder, **per-bus caps so a
    dense class steals from itself rather than crowding another**, same-id coalescing ("bigger, not
    more"), and the duck envelope. Plus `Editor/WarbandMixerTools.cs` →
    `Warband/Audio/Create Game Mixer`, reflection over the internal `AudioMixerController` because a
    `.mixer` **cannot be authored through any public API** (approach proven in Shoota). Bus tree puts
    `Decisive` as a SIBLING of the ducked group, so death/crit ride over the duck — and `Ducked` has
    to exist as an intermediate bus at all because **a mixer param can only be exposed once**, so
    `BoardVol` and `BoardDuck` cannot share a group. Both files are NEW (no collision).
    **Not wired yet — dead code until step 4/5 call it.** Verified by compiling against real Unity
    reference assemblies on homeserv (0 errors); the editor script is syntax-clean but type-checks
    only inside Unity.
    **STEP 4 BUILT 2026-07-27** (once Codex released the lock). `UiAudioDirector` is now a ~90-line
    cue→family adapter over `SfxPlayer`. Hover/tooltip/projection silent · 10 families → 6 · ambience
    bed, its duck, both synthesizers and the hover cooldown deleted, with the dead
    `hoverCooldownMs`/`ambienceVolume`/`commitDuck` config removed from C# *and* `HubPresentation.json`.
    **New law:** an unmapped *cue* is silent (the old `Family()` fell through to `commit`, so under
    clicks-only any future ambient signal would have started clicking on its own); unmapped
    *transactions* still commit. Baked UI clips promoted so the six families resolve.
    **Caught a silent-wrong-answer trap:** `SfxPlayer` tries `{id}_1..n` before bare `{id}`, so
    promoting `error.wav`/`major.wav` left the **stale 1.04 s `error_1`/`major_1` shadowing them** —
    both files exist, both import, both play, no warning. Contract now says `variants: 1` for those
    two so the promotion overwrites rather than hides. *Verify resolution, not copying.*
    **VERIFIED:** all 57 client scripts compile headless, 0 errors — new `make check-client`
    (`tools/check-client-compile.py`) against real Unity reference assemblies, so client changes no
    longer need a Syncthing round-trip + the Unity lock to find an API error.
    **MIXER ASSET: self-creating, waiting on ONE Unity domain reload.** `WarbandMixerTools` now
    carries `[InitializeOnLoadMethod] EnsureMixerOnLoad` (deferred via `delayCall`, guarded by the
    same existence check as the menu item), so the asset builds itself the next time Unity reloads —
    **no menu item, no MCP call, no lock needed.** Anything that reloads the domain does it:
    focusing the Editor, a script edit, a restart.
    **Why not just call the menu item:** `Unity_RunCommand` is **currently unusable for this**. It
    compiles into a library, so top-level statements fail `CS8805`, and a class-shaped payload
    compiles but the harness finds no entry point ("No logs available"). Five shapes tried
    2026-07-27; `Unity_GetConsoleLogs` also returns `totalCount: 0` for everything (a known trap —
    see the `unity-mcp-runcommand-quirks` memory, note 6b). Unity's asset watcher DID import the new
    scripts and clips unattended (their `.meta` synced back), so a reload is the only gap.
    **Deliberately NOT hand-authoring the `.mixer` YAML** even though Shoota's could be adapted: an
    untestable hand-built asset that resolves no groups fails *identically* to having no asset, but
    leaves something in the repo that looks correct. Letting Unity's own API build it keeps the
    self-check (`FindMatchingGroups` on all five buses, logged) meaningful.
    Until it lands, `SfxPlayer` plays unrouted with one warning (no buses, no duck, no volume
    params) — degraded by design, not broken. `audio.enabled` is still `false` regardless.
    **MIXER LANDED 2026-07-27** — the self-healing loader fired on Unity's next reload and built
    `Resources/Audio/GameMixer.mixer`, which synced back. Verified structurally, not just by
    presence: all 5 buses resolve, all 4 params exposed, and **`Decisive` serialises as a SIBLING of
    `Ducked`, not a child** — the one thing that had to be right, or death and crit would duck
    themselves.
    **STEP 5 BUILT 2026-07-27.** 17 board clips promoted · **16 tell rows repointed** onto the 5 D3
    families (dangling ids 12 → 3) · `ReplayPlayer` routes through `SfxPlayer` with a bus per event
    class and ducks the board −6 dB on a Decisive onset · chip-damage silence law added (guarded on
    `Amount != 0`, or a Cast reporting 0 would be silenced by a threshold of 1 — the status-refresh
    half was already free from item 2b's onset filter).
    **AUDIO IS ON.** `audio.enabled: true` in both `tuning.json` (board, live under F1) and
    `HubPresentation.json` (Hall UI, hot-reloadable). Those two values are the mute until item 9.
    **Design bug caught before it shipped:** a global `SfxPlayer.Muted` written by `UiAudioDirector`
    made the board depend on the Hall initialising — in a fight scene with no Hall it would have been
    silent forever with no clue why. Each surface owns its own switch now.
    **STEP 6 DONE 2026-07-27** — `hit_blunt`/`hit_pierce`/`hit_powder` generated
    (`elevenlabs-sound-effects-v2`, Jake consented) and baked through the same contract. **The gate
    proved itself on first contact:** the raw batch returned the *identical* pathology as the
    original one — all padded to 1.045 s with a **23 dB level spread** — so generating without it
    would have reproduced the exact defect this pass exists to fix. Baked: ±2 dB, 98–232 ms.
    **`make sfx-lint` PASSES against shipped `Resources/`: 0 violations, 20/20 board ids resolve, no
    silent weapons, all 6 UI families present.** First clean end-to-end run.
    **Swept 3.49 MB of dead weight** — 15 superseded clips deleted from `Resources/`, which ships
    everything it contains regardless of references (`hall_ambience` alone was 1.4 MB of a bed D1
    cut). UI 11 clips/360 KB · board 20 clips/1.1 MB.
    **STEP 7 LANDED 2026-07-28 with item 9:** Master/Interface/Battle sliders + the sound switch
    drive the mixer through `SfxPlayer.SetBusVolume`; param names verified against the asset.
    **CAPS PRICED + A ROUTING BUG FOUND 2026-07-27.** `make sfx-density` now also reports per-bus
    pressure against every fixture. Building it exposed that **the per-weapon hit sounds sit on
    `EventKind.Attack` (the swing), not `DamageDealt`** — `Damage/Attack` has no sound row at all —
    so `BusFor` was filing **every weapon hit in the game** under `State`: lowest priority, smallest
    cap, first stolen. `Cast` bodies and `CheatDeath` were mis-filed the same way. Fixed and
    re-priced: peak pressure Cast 1.7/4 · State 1.6/3 · Impact 1.0/6 · Decisive 0.2/4 — **no bus
    steals from itself on any committed fixture**, so nothing silently vanishes. Found by measuring,
    not by reading the code.
    **⇒ NOW IT WANTS EARS, NOT ARCHITECTURE.** Re-audition clips at
    `https://warband.inhouseboyz.com/sfx/`; judge the MIX in motion during the item 1 verify pass. (or `make sfx-serve` locally) — before/after players, absolute-scale
    waveforms, pass/fail. Answer **D2** per family. Steps 3–7 are client work and wait on that.
    The route is `site/sfx.go`: **admin-gated and fail-closed** (`WARBAND_ADMIN_IDS`; unset = 404
    for everyone), which is deliberately NOT the launcher's gate — that one is open to any signed-in
    Discord account, so "signed in" would show every friend the WIP audio.
    **Two findings worth keeping:** ① the density pass says the worst case is **`overtime`, not
    `castfest`** — 9.6 onsets/s sustained for **3.6 minutes**, so THE WANING is the fixture board
    audio must be judged against; ② `lint` caught a real defect in `bake` on its first run (tail
    trimmed before filtering left up to 105 ms of dead tail, which holds a pooled voice open), which
    is the whole argument for the gate.

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
5a. **Hourstone / Inscription engine layer** — **WAVE 2 BUILT 2026-07-27 (ADR 0026). VERIFY in
    motion folds into item 1's pass; wave 3 (12→24) gated on the twelve staying legible in play.**
    Shipped this pass, all machine-gated green (`make test` 492, `make baseline` explained,
    `make check-client`, capture-reviewed):
    ① sim machinery — once-per-root guard (Inscriptions only), `EveryN` counters with
    `RuleProgress` pips on the wire, `AdjacentToAlly` selector, `HealToShield` status,
    `TriggerFired` hook; baseline was **byte-identical** for the guard alone. ② the twelve —
    five seeds renamed (First Bell/Closed Gate/Cinder Law/Bronze Testament/Chorus of Hours) +
    tithe/woundclock/thirdchime/ashbequest/stilledbell/shoulder/bloodless; Paradoxes reachable
    via boss rewards only. ③ Living Inscription replaced Bearer of the Mark (`DoublesBanners`
    deleted everywhere). ④ full `Banner*`→`Inscription*` rename; RuleIds are `inscription.*`;
    replay v8 carries per-rule owning team. ⑤ **the combat badge rail is BUILT** — left-edge
    world-text badges, team-0 laws only, counter pips fold-driven (capture: `hourstone` fixture,
    "The Third Chime 2/3"), pulse+coalesce on TriggerFired. **Unverifiable from a session: the
    pulse/coalesce glow in motion (Play Mode is Jake-only).** In-fight full-rule inspection
    deferred — the Hourstone Table remains the disclosure surface. Fixed en route: `PlayBattle`
    never carried the result's rule table, so LIVE fights resolved passive names against a stale
    file table (item 20 latent). New render fixture: `hourstone` (5 player + 1 enemy law).
    Numbers all placeholder by doctrine — shapes tune in review once heard/seen in play.
6. **Friends playtest #1** — the milestone that ends arguments (ADR 0001), after the PvE vertical
   slice. Distribution/launcher work is allowed only as needed to put that slice in friends' hands.
   **Mechanically nothing blocks it as of 2026-07-28** — items 7, 8 and 9 are built and the site
   is live; item 9's in-motion verify folds into item 1's pass. No date until Jake calls it.

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

**Measured against the cap, 2026-07-27 evening** (`make baseline`, fingerprint `b8640a3ea7cd360b` —
moved by ADR 0026: hash schema + twelve Inscriptions + Living Inscription, saves invalidated once):
8 chassis ✓ · 78 spec nodes · 11 weapons ✓ · **5 trinkets** (this line said "1 trinket" until today —
ADR 0022 added four and the budget was never updated) · **12 Inscriptions of 24** ← *was 5 — the one place the
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
- **2026-07-27** — Muster spacing/responsive pass: duplicate role/context copy removed, facts and
  authored rule metadata reflowed, card-width-owned compact composition added, and a dedicated
  six-capture 1024–3440/copy-stress matrix added; **6/6 structural PASS and pixels reviewed** at
  `client/TempCaptures/ui-qa/20260727-233852/`.
- **2026-07-27** — Responsive UI foundation: shared 1600×900 height-locked panel profile, root-owned safe area + portrait guard, independent geometry/form-factor/input/motion classes, semantic type/spacing/hit tokens, route-scoped notices, responsive dossier pages, and a deterministic 57-case Play Mode matrix across Workbench/Wager/Deployment/Result; 57/57 structural contracts PASS. **Uncommitted.**
- **2026-07-27** — Item 21, the in-fight inspector (476 tests, replay v7): identity line, placement
  facts, targeting rule, and the passive roster with live conditionals — plus `ContentLexicon.Rule`
  and a CI contract that no raw content id can reach a player-facing card. Closes item 12's enemy
  inspector. **Play-Mode-only surface — Jake-only verification.**
- **2026-07-27** — Item 20, the passive layer's renderer (`Design/passive-legibility.md`, 471 tests):
  auto-stamped rule identity, `TriggerFired`/`RuleChanged`, replay v6, `byRule` tells + fallbacks.
  Baseline byte-identical and fingerprint unchanged — presentation cannot move the sim. **Unwatched.**
- **2026-07-27** — Sim/render audit (`Design/sim-render-audit.md`) + its three cheap wins (A/C/D):
  `camera.fov` + tunable feed anchor, `castSigilHoldSeconds` (sigils outlive their payoff, full-alpha
  0.03–0.33s → 0.38–0.68s), `statusRefreshQuiet` (onset-not-refresh, −25.8% of castfest's tells).
  460 tests, client compile negative-controlled. **Not watched — Unity lock held by Codex.**
- **2026-07-27** — Workbench overhaul (Market Recruit R5, Armory Mode R4, keyword + equipment
  tooltips R6): object-centric Workbench, live dossiers, selected-trait ribbon, permanent equipment
  rail, paged Armory, runtime tooltip layer, and rail-safe Wager command band; no scrolling.
  Initial 50-case matrix plus correction-pass 1280×720/2558×1313 contracts PASS. **Uncommitted.**
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
