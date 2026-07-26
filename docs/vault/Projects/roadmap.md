# Roadmap — THE live board

**This is the ONLY live priority list.** (Pattern from battle/Shoota execution boards —
multiple competing lists is how projects rot.) Sessions plan from here; see CLAUDE.md
"Planning SOP". Keep it honest: the board must match reality better than memory.

**Groomed 2026-07-24.** This board had grown into an archive (568 lines; one item was 215 of
them), which is exactly how a board stops being usable. Build logs now live in `Daily/` —
this file carries **state, priority, laws, and the gotchas that would bite someone.** If you
want the blow-by-blow of how something was built, read the daily note for that date.

## Stages
- **DESIGN** — needs a design conversation with Jake before building. Don't build; propose.
- **SPEC'D** — designed (ADR/doc exists), ready to implement autonomously.
- **BUILD** — implementation in progress; note what's left. Finish before starting new work.
- **VERIFY** — built, needs verification/tests/polish before calling done.
- **DONE** — move to the Done section with a date.

## Now / Next (ordered — top unblocked item is "what's next")
🎯 **GOAL (Jake, 2026-07-23): a playable PvE PoC.** **North star (ADR 0016): the fun is
breaking the game with a compounding warband, then seeing how far asymmetrical PvE and
endless pressure can push it.**

### ⇒ AGREED ORDER (Jake, 2026-07-26). Item numbers never change; this line does.
**0. JAKE'S VERIFY PASS on item 1** — watch a fight. Three days of combat work is unwatched and
nothing else in item 1 can be judged without it. This is the top of the board and it is not
something a session can do.
**1. Items ~~7~~ → 8 → 9** (~~save/resume~~ **BUILT 2026-07-26, client half needs a click-through** ·
a standalone build · an options screen) — the invisible blockers on item 6. **Item 8 is next**, and
it is also the natural verifier for item 7: a real build is where `Application.persistentDataPath`
stops being a theory.
**2. Then re-decide** from the verify pass. Standing candidates, in Jake's stated preference order
if nothing changes: cheap feel wins (10, 11) · Inscription engine (5a) · act identity (14).
Items 4, 12, 13, 15 are live but unranked. Item 16 is settled — see it. Item 5 is a laws page.

**STATE, 2026-07-26 (honest):** the first-playable run shape and between-fight UX are
walkable end to end: Menu → five-card Draft → full-screen Management Hall → stakes-first Wager
→ formation-reveal Deployment → Fight/replay → blocking result report → spatial Hourstone Table
→ Victory/Defeat. Three acts × five
beats, Sand economy, Interludes, boss rewards, and terminal loss are implemented. **392 tests
green.** The workspace has data-first cards/inspector, portraits, explicit economic actions,
responsive landscape phone/tablet compositions, safe-area rules, reduced motion, and timing
polish. The old Management drawer has been replaced by stable Market/Warband/Armory/Hourstone
geography and bespoke workspaces; the Armory previews exact equipment deltas. **Combat viewing still does not read well enough.** Authored
encounters landed 2026-07-25 (ADR 0023) and per-act bosses + full encounter disclosure landed
2026-07-26 (ADR 0024) — deployment has real problems to answer and every fight now states its rule.
**None of the last three days' combat work has been watched in Unity**, which is now the single
biggest blocker on the board (see item 1).
**Opening Muster readability pass built 2026-07-26:** its universal cards were replaced by a
dedicated three-fact / two-rule scan grammar with code-native semantic glyphs, in-portrait exact
mechanics, ordered party sockets, semantic select/deselect feedback, cancellable reveal/lens
timers, F1 tuning, and F2 previews. Desktop verification is the remaining gate; mobile is
deliberately deferred.

1. **FEEL & READABILITY — the fight does not read** — **VERIFY (was DESIGN → BUILD). THE TOP ITEM,
   AND IT IS BLOCKED ON JAKE, NOT ON BUILDING.**
   **The four live threads, so nobody has to read 90 lines to find them:**
   **1a — combat spectacle P0–P6** (casts, fields, status icons, deaths, dress): BUILT, machine-gated
   green, **never seen in motion.** Needs one play pass; the specific knobs to judge are listed at
   the end of the arc paragraph below. **1b — Hall polish:** BUILT + Unity-verified; four named
   polish slices open (Bind choreography · Rule Preview diagrams · real-device safe-area/haptics ·
   audio/motion feel). **1c — fight-legibility Phase 4 client UI:** HALF built — the damage-share +
   died-to readout shipped (`40eb076`), but `BattleForecast` exists in the sim and is referenced by
   **zero** client code, so the win-probability half has no home. **1d — camera/framing pass:**
   unbuilt, and taste-gated on Jake.
   **Nothing here needs a design conversation any more.** It needs Jake to watch a fight.
   **Jake, 2026-07-24, after playing it:** *"playing it now still does not feel great for a
   lot of reasons (UI is not great, sim viewing has some issues and is not quite clear what's
   happening)."* Take this at face value: **item 4b's entire render arc — signature-matched
   tells, directed tells, unit identity, kill feed, fight story — was aimed at exactly this
   target and has not hit it.** Adding more tells is therefore NOT obviously the fix; the next
   move is to find out *why* it does not read before building more of the same.
   **Do not start by building.** Watch a fight with Jake, or capture one and go through it
   beat by beat, and separate the three candidates:
   ① **Presentation** — too much at once, no pacing, no emphasis. ② **Legibility of state** — you
   cannot tell what a unit IS mid-fight, what statuses are on it, or why it did what it did.
   ③ **UI quality** — the shell screens are functional-but-plain; density, hierarchy and typography
   were never passed over. Likely all three, in different measure.
   **STATUS 2026-07-26: all three have now been built against, and NONE has been watched.** The
   "name which before building" instruction above was overtaken by events — three sessions built
   answers to all three candidates. So this item is no longer DESIGN or BUILD, it is **VERIFY, and
   the only person who can advance it is Jake** (see the four threads below). (Superseded detail:
   the beat sequencer and hit-stop, described below as "still unbuilt" in the 07-24 wording, landed
   in `a1fcf8b` the next day. They have never been seen in motion.)
   **Candidate ③ now has its third real pass:** ADRs 0020–0021 replace the over-dense board-first
   workspace with distinct Management / Wager / Deployment / Combat states, exact card grammar,
   a result gate that preserves the fight receipt, a spatial Hourstone Table, bespoke station
   workspaces, landscape mobile compositions, runtime hover/focus/tap disclosure, and large
   management/combat inspectors. Treat
   between-fight UX as VERIFY/polish from play, not as the same untouched problem.
   **First cause named and fixed, 2026-07-24 — movement (ADR 0018).** Jake: *"everyone
   teleports instantly."* It was structural: a move was decided and applied in the same tick,
   so `MoveInterval` was a cooldown between teleports and no client easing could honestly
   smooth it. Movement is now a **committed step** — depart, travel, arrive — and the renderer
   interpolates across the sim's own window. **That is one item off candidate ①; the rest of
   ① (pacing, emphasis, hit-stop, the decoupled clock) is still unbuilt, and ② remains
   untouched.**
   **Researched plan ready, 2026-07-25 (overnight session) → `Design/fight-legibility.md`.**
   Render-layer inventory + genre research (TFT/Underlords/HSBG/SAP/Mechabellum/BB/LTD2) +
   asset-pipeline survey, synthesized into five phases: 0 repair (post stack regressed —
   DoF/saturation knobs dead, MSAA off, scenes untracked; silhouettes key on Name not
   ChassisId) · 1 legibility grammar, no art (cast sentence, beats/clock/hit-stop, 23/27
   silent statuses + 12/20 silent event kinds filled, byChassis cast tells) · 2 real units
   (KayKit shared-rig route, $0 validate/$150 commit; AI-gen rejected for roster) · 3
   per-ability VFX (packs + Shader Graph telegraphs, vfxId on TellDef) · 4 comprehension
   (damage chart, first-party win-prob re-sim).
   **Jake approved 2026-07-25 ("sold on everything but kaykit — find free/cheaper") → BUILD.**
   **Built same session:** Phase 0 repair (acddbf0) · Phase 1 core — byChassis casts,
   ChassisId silhouettes, beat sequencer + hit-stop, mana-ready flip, segmented ally/enemy
   bars, status tints, registry fills (f788491, a1fcf8b) · Phase 4 sim — FightSummary +
   BattleForecast, 299 tests (113a2de) · end-fight readout with damage shares + died-to
   story (40eb076) · **Phase 2 slice: KayKit FREE-tier minis render on the board** — model
   route settled at $0 (same shared rig + 173 CC0 clips as the declined $150 bundle),
   chassis-mapped bodies + handslot kitbash props + Idle↔Walk controller, primitive fallback
   intact (82b7a6b). **Rounds 2-3 same day:** Attack/Cast crossfades + 9 SFX stings + grim
   atlas recolor + bridge portraits (b3898e8) · Jake's three play-note rounds fixed — text
   sharpness, board-spacing tuning, battle-speed persistence, DoF off (transparent-text
   depth), T-pose/lock-in teleport (through 9b4f861).
   **NEXT ARC — combat spectacle (Jake, 2026-07-25 evening: "go big — the reward for
   playing IS the combat"): BUILD, P0-P4 LANDED same evening.** Scope: core systems +
   proposals 1/2/3/4/5/9; 6/7/10 shelved next wave; 8 (Overtime) its own later slice; full
   asset batch approved. Commits: P0 sim f2ea2f4 (Burn fold bug fixed w/ guardrail, 313
   tests, durations on wire, AbilityIdentity, replay v5) · P1 FX foundation 3c7ab1a
   (VfxLibrary + 6 hand-HLSL shaders + TellDef vfx bindings + ProbeShots harness) · P2
   fields 4841f59 (FieldView: edge rings, scrolling floors, pulses, expiry) · P3 icons
   6590382 (icon rows: glyphs, stacks, countdown rings) · P4 casts 14d3d4b+d205c6a (26
   byAbility rows, era sigils, rationed announce) · P5 death pipeline 6fd1a06 (slump /
   ember dissolve / ash-death graves) · P6 dress f074a1c + assets d18399c (rider echoes,
   Deathless dress, fight-ender slow-mo, camera law, 8 era risers). **ARC BUILT END TO END
   — nine commits, every phase gated: headless compile + event-derived probes + contact
   sheet ×2 → 28/28 byte-identical. Stage → VERIFY: needs Jake's live play pass**
   (fight-ender/camera feel, riser mix + announce density in motion, F1 knobs: field
   brightness / icon size / wall tint / cleric sigil, HP-bar snap vs T3 windups → bar
   tween if wrong; Heal carries no Cause so Boon pulses stay dormant — one-line sim change
   when wanted). Detail: Daily/2026-07-25.
   **S5 byWeapon + per-weapon attack language landed 2026-07-25 (317 tests).** Autos could only
   key on chassis, so combat-spectacle §6's per-weapon table was direction with no data path.
   `TellMatch` now filters on the fold's `WeaponName` (+1, a PEER of chassis — a byWeapon row
   TIES a byChassis one); 11 weapon classes authored with 11 new recipes, plus 2 chassis-lane
   staff overrides proving the compose path. **Gotcha worth keeping:** a weapon row needs
   `byCause: Attack` too, or it ties the `byRanged` fallback at 1 and silently loses on registry
   order — and the gate is honest anyway, since Counter/rider swings are also `EventKind.Attack`.
   One new fixture (`weaponry`) covers the three shop-only classes; contact sheet 32/32
   byte-identical ×2. **Found while probing, NOT fixed:** the target-side impact `punch` balloons
   struck units from scale 0.750 → ~1.03 (+37%), hiding neighbours, HP bars and any arc near them
   — reproduces with all VFX hidden, predates this work, and is a live candidate for Jake's
   "not quite clear what's happening". Detail: `Design/authoring-combat-fx.md`.
   `Design/combat-spectacle.md` (direction: palette law + intensity tiers, cast grammar +
   era sigils, per-signature specs, field/status/attack language, ranked go-big proposals,
   asset manifest) + `Design/fx-runtime.md` (engine: VfxLibrary recipes, Director-stepped
   particles, hand-HLSL shaders, ground substrate, status icon row, death linger, phases
   P0-P6). **Inventory found a real shipped bug: the playback fold diverges from sim truth
   at the first Burn tick** (fold Burn magnitude frozen, icon never clears; affects
   castfest/statusstorm/glyphwar/skirmish fixtures) — fix is P0 regardless of the rest.
   Also: `Cause.Trigger` (2nd-most-common damage cause) has no tell · status durations need
   StatusApplied.Aux2 + replay v5 · ability identity derivable with ZERO sim change (last
   SignatureOverride trait wins; resolver belongs in Warband.Content).
   **Still open from earlier rounds:** Phase 4 client UI (damage chart/forecast) ·
   camera/framing pass · live play-mode eyeball of beats/hit-stop + minis in motion.
   **Management Hall polish, 2026-07-25 → `Design/hall-polish.md` (BUILD/VERIFY).**
   Jake approved the obsidian Tower instrument / living Sand direction and asked for the deep
   reusable system. Foundation now built and Unity-verified: hybrid 2.5D Table/Hall environment ·
   accepted authored iron + living-Sand materials with procedural rejection fallbacks ·
   pooled authored UI sound families + Hall ambience and Android/iOS haptic sink ·
   shared theme tokens + dark scrollers ·
   five code-native vector station sigils · payload-bearing semantic feedback · interruption-safe
   reveal/preview/press/select/attention/route/commit/error recipes · identity-aware staggered
   card/choice reveals · one bounded Painter2D pulse/arc/Sand plane · reduced-motion substitutes ·
   purchase/reroll receipts · result count-ups/death-cause reveals · pinned inspector command dock ·
   F1 UI FX/environment/audio/haptic live tuning + F2 Flow Lab previews. A 38-deliverable
   concept/material/FX/mesh/audio batch was generated and curated; rejected tile-heavy surfaces
   and 1.5M-triangle mesh candidates are quarantined, not shipped. Clean compile/console;
   contracts, route spam, forced phone, and reduced motion passed.
   Second-pass station UX is now built and in VERIFY: compact 60–64 px run ribbon · physical
   overview nameplates · data-first station presentation catalog · short pre-handoff route lock ·
   centered five-offer Market rail · pinned exact action tray · optional blocking dossier · typed
   actions with disabled reasons · Armory item→champion pinning and comparison · distinct
   Warband/Hourstone geometry · one-scroll ownership · landscape-phone composition and portrait
   rotate interstitial. Full-size overview/Market/Armory/Warband/phone captures are clean after
   removing inline card-detail overflow.
   Market offer-card redesign is built and Unity-verified: a dedicated typed scan model/component
   replaces the universal Hall card · recruit/weapon/trinket/Inscription/capacity/sold states share
   one exact-rule grammar · four-metric comparison budget + protected commerce dock · inspectable
   unaffordable stock · held/reroll persistence · responsive selection-follow rail · 16 px rule
   copy and 56 px phone actions. Desktop/forced-phone capture contracts now measure actual rule
   containment as well as footer overlap; the longest authored Fire Glyph rule fits in both.
   Rank/item/forge follow-through is built: typed four-fact profiles · dedicated Rank Up cards with
   guaranteed gains + exact 1-of-2 ADD/SWAP/DEEPEN previews · weapon Mana-per-hit, temper, audience,
   and mastery facts · exact trinket/Inscription rules · stable item identity and invested-Sand
   accounting through equip/resale · explicit act-capped Worn→Honed→Relic forge actions · semantic
   Recruit/Rank/Gear/Bind/Capacity/Equip/Forge feedback recipes exposed in F1 and F2. Mechanical
   copy now comes from one headless grammar over the actual content primitives and fails closed on
   unsupported rules.
   **Open polish slices:** final Bind choreography · Rule Preview diagrams · real-device
   safe-area/finger/haptic pass · live audio/motion feel tune.
2. **Authored PvE content** — **BUILT 2026-07-25 (ADR 0023); BOSS + DISCLOSURE UI REMAIN.**
   Normal fights are no longer random kits-as-monsters. Five authored enemy roles (Swarm/Anchor/
   Artillery/Ritualist/Diver) compose four node encounters — Gnawing Hour (SWARM), The Long Range
   (WARD, act 2+), The Ninth Bell (RITUAL), The Drop (AMBUSH) — each posing a different placement
   problem, each disclosing its rule. **Composition is the act lever, stats are secondary**; an
   act's pool is its identity. Measured with the new `--enc` probe: all four pose a placement
   problem at their debut act (spread 100 for Long Range and The Drop). Laws in
   `Design/pve-encounters.md`; decisions in ADR 0023.
   **What remains:** ~~① act bosses~~ **BUILT 2026-07-26 (ADR 0024) — see Done** · ~~② client
   disclosure~~ **BUILT 2026-07-26 (ADR 0024) — see Done** · ③ bespoke enemy art + per-role tells
   (roles borrow hero silhouettes as render keys; enemy CARDS no longer borrow hero names/portraits,
   but the board silhouettes still do) · ④ risk-tier mutation of authored encounters (tiers only
   scale stats today) · ⑤ **still not watched in Unity** — two boss render fixtures now exist
   (`boss-ashfall-battery`, `boss-waning-crown`) so this is one session away.
   **Open follow-on, NOT chased:** `--enc` now reports Gnawing Hour / Ninth Bell / The Drop as
   **FREE at their debut act** (spread 0, every formation wins), which contradicts the ADR 0023
   line below claiming all four pose a placement problem. It could not be bisected: the entire
   ADR 0022/0023 implementation was uncommitted, so **no git baseline existed to compare against**
   (see the Done entry). Re-measure before trusting either number.

4. **The pressure tier is a fake choice** — **DESIGN.** Stable/Fraying/Collapsing are visible,
   but the sweep found victory saturates ~99% at every tier, so **Collapsing strictly dominates
   at zero risk**. Either
   make risk mean something or delete tiers. ADR 0007 economy is placeholder either way.
   **Re-measured twice on 2026-07-25** (`Projects/sweep-2026-07-25.md`). After ADR 0022: 88/92/79
   victory at 69/77/82 Sand. After ADR 0023's authored encounters: **35/48/39 victory at 40/54/65
   Sand — and Fraying now BEATS Stable**, because Sand buys the survival that authored encounters
   demand. That is a real risk curve arriving as a side effect, not a solved item: nothing in either
   ADR targeted tiers, and the run structure changed (ADR 0019) since the 07-23 baseline. Start the
   DESIGN pass from a fresh measurement, never from the 07-23 claim.
(Item 3, the Last Oath's unreachable decision, is DONE — 2026-07-25, see Done. The gap is
deliberate: item numbers are load-bearing references, so finished items leave a hole rather
than renumber.)
Items **5 / 5a / 6** keep their numbers below — their settled laws are referenced from ADRs and
design docs, so renumbering them would break those references.

### Gap analysis, 2026-07-26 (overnight, Jake asleep — items 7-15 are NEW to the board)
Jake: *"research our game, do some gap analysis on what features or content or design
ideas/combat rendering we are missing. Make a priority list (things not in the roadmap yet)."*
Everything below was verified against the code, not remembered. **Priority order is P1 → P4;
none of these outrank items 1 and 2**, which are Jake's own stated pain. The three P1s share one
property that makes them urgent out of proportion to their size: **item 6 (friends playtest #1)
cannot happen without them, and each is the kind of work that is discovered too late.**

**P1 — silently blocks friends playtest #1 (item 6).**
7. **A run cannot be saved.** — **BUILT 2026-07-26 (412 tests). VERIFY: the client half is
   compile-checked but not yet clicked through.** See the Done entry for what landed and what is
   still unverified. Jake also settled item 16 on the back of this: terminal loss stays, and
   save/resume is the whole mitigation.
8. **No standalone build has ever been produced.** — **SPEC'D (small, do it EARLY).** Every
   verification to date is in-Editor. One landmine is already known and written down in the FX
   ledger: the six hand-HLSL shaders are found by `Shader.Find`, which **silently falls back to a
   URP/Unlit stand-in in a player build unless they are in Always Included Shaders** — i.e. the
   first build loses the entire combat-spectacle arc and looks merely broken. Unverified for the
   same reason: `Resources.Load` paths, StreamingAssets on a real build (`tuning.json`,
   `scenarios`, replays), the generated audio, KayKit import settings, and IL2CPP/Mono behavior.
   First builds always find ten of these. Find them on a Tuesday, not the night of the playtest.
9. **No player-facing options at all.** — **SPEC'D (small).** Audio enable/volume live in
   `HubPresentation.json`, reduced motion in a dev-key `PlayerPrefs` toggle, battle speed in
   `tuning.json` behind F1. A friend on their own machine cannot mute the game, slow the fight
   down, or turn motion off. The values are all already plumbed and hot-reloadable — this is a
   screen over existing seams, not a system.

**P2 — combat legibility (item 1's actual target), cheap, high suspicion.**
10. **The impact `punch` balloon — the cheapest possible test of Jake's top complaint.** —
    **SPEC'D (data-only).** Measured 2026-07-25 while probing weapon frames: every unit idles at
    world scale **0.750**, and 0.10 s after being struck the victims sit at **1.026–1.035** — a
    ~37% inflation that covers neighbouring units, their HP bars, and any arc drawn near them. It
    **reproduces with every VFX instance hidden**, so it is not the new FX; it predates the whole
    arc. A swing's own tell is competing with the victim ballooning over it. The fix is a
    `punchAmount` value in `tuning.json`, hot-reload, no recompile. Highest suspicion-to-effort
    ratio on the board. **Exact knobs (checked 2026-07-26):** the balloon is
    `punchAmount` (per tell row, default 0.25) × `(1 + impact.punchBoost × t)` with
    `punchBoost` defaulting to **0.8** — so `impact.punchBoost` is a single global slider in the F1
    cockpit that scales every impact recoil at once. Try that before touching 197 tell rows.
11. **Overtime is completely invisible — a pillar renders as nothing.** — **SPEC'D.**
    `Battle.OvertimeStartTick = 900`, after which `Cause.Storm` deals ramping damage to every unit
    every tick until someone dies. The pitch calls this a pillar (*"escalating overtime clock
    guarantees resolution"*), theme.md names it **the Waning** and makes it the Hour running out —
    and the client draws **no clock, no approach warning, and no storm tell**. A long fight
    currently reads as "units started dying for no reason." This was filed as combat-spectacle
    proposal 8 ("its own later slice"); **it is not spectacle, it is item 1's failure mode ②
    (legibility of state)** and belongs there. A tick clock + a threshold warning + a Storm damage
    tell is most of it.

**P3 — settled laws the build does not yet keep.**
12. **Enemy disclosure stops at name/HP/reach.** — **partly addressed by tonight's build.**
    `pve-encounters.md` requires attacks, signatures, passives, triggers, **and targeting rules**
    inspectable before deployment. A Sanddrift Gunner's entire design is "acquires FARTHEST, holds
    standoff 5" and nothing ever tells the player. Tonight's work adds per-unit role + behavior
    notes to `EncounterBrief`; the deeper inspector (full signature/passive text on an enemy, as
    the Muster cards already do for heroes) is still open.
13. **The endless seam does not exist.** — **DESIGN → then small.** ADR 0016's identity and the
    first-playable content budget both include a *"crude post-win continue-until-defeat seam."*
    `RunPhase.Complete` is terminal; nothing in `RunController` continues past the last act.
    theme.md's candidate name is **Beyond the Hour**. Cheapest honest version: on Complete, offer
    CONTINUE, which re-enters act 3's pool at escalating scale until a loss.

**P4 — content identity.**
14. **Act identity is thin; acts 2 and 3 are the same game at bigger numbers.** — **SPEC'D
    (was DESIGN; unblocked by Jake's 2026-07-26 three-act budget decision).**
    `Encounters.PoolFor` differs between act 1 and act 2+ by exactly one encounter, and acts 2 and
    3 draw an **identical** pool. theme.md says acts are eras and pve-encounters says *"an act's
    pool is its identity"*; neither is true yet. The per-act bosses are the first real
    differentiation. Cheapest next step is act-scoped pools, **not** new roles.
    **Build law for this item:** propose the act→pool assignment and **measure it with `--enc`
    before committing** — the probe already caught three of four encounters posing nothing, so an
    assignment that reads well in prose can still be flat. Do not add a sixth role to solve this.
15. **The Interlude is a non-choice.** — **DESIGN (tiny).** ADR 0019 gives each act one Interlude
    beat; it currently reads *"A QUIET STRETCH — No one contests the road. Take the coin and move
    on"* with one button. The content budget explicitly funds **one event**, and it has not been
    spent. A single two-or-three-way risk/reward choice would make the beat exist.
16. **Defeat/retry rule — SETTLED 2026-07-26, no work item.** Raised during the grooming because
    terminal loss (ADR 0019) compounds badly with authored encounters dropping bot completion to
    **3/12** and there being no save: a friend's first run ends permanently in act 2, ~15 minutes
    in. **Jake's call: terminal loss STAYS — the mitigation is save/resume (item 7), not a retry
    currency and not softening the encounters.** So a loss keeps meaning something and the run
    merely survives across sittings. Recorded here rather than deleted so the next session does not
    re-open it. Do **not** tune act 2's node pool down to address run length; if the cliff turns out
    to hurt, it will hurt real playtesters first (ADR 0001: playtests decide).

**Deliberately NOT proposed** (so the next session does not re-derive them): more heroes, more
weapons, a second trinket family, multi-act expansion, difficulty ladders, PvP-adjacent anything,
and any balance pass on hero kits — all are either capped by the content budget or forbidden by
the content doctrine until playtest #1.

### Client — built, working, and where the bodies are buried
The client bring-up (item 4), render polish (4b) and the PoC shell (4c) are **built and
verified**; full history in `Daily/2026-07-23` and `Daily/2026-07-24`. What a future session
actually needs from them:

- **Architecture:** `GameBoot` owns startup order (add a line there — **never** another
  `RuntimeInitializeOnLoadMethod`; four competing ones is why two UIDocuments once raced for
  input). Scenes are **Boot(0) → Game(1)**; Boot holds one `~BootLoader` and nothing else.
  `ReplayPlayer.autoPlayOnStart` is **OFF** by default — the board is driven, never
  self-starting, and the shell parks it on any transition to a non-board screen.
- **Shell pattern (extend, don't fork):** `RunShellModel` (plain view-models) ·
  `IRunScreenView` + `RunShellActions` (render out, intent in) · `RunShell` (router, and the
  ONLY place content ids become words). The player flow is `ManagementView` → `WagerView` →
  `DeployView` → board-only Fight; management still shares one plain `PlanningModel`.
  `PresentationCatalog` owns art/copy/icon references; composed
  `UnitDef` owns every mechanical number. `WarbandCard` and `InspectorPanel` are shared
  UXML-backed renderers; `PlanningWorkspaceStyles.uss` owns layout/motion tokens. Views may
  not reference `Warband.*`, so a raw id physically cannot reach the UI.
  The opening draft is the deliberate exception to universal-card reuse: `MusterCard` accepts
  only three facts and two rules, and exact mechanics disclose inside its portrait.
- **Hydration:** `Warband.Sim.Lexicon` (27 StatusKind + 9 Cause) and
  `Warband.Content.ContentLexicon` (78 spec nodes + 8 chassis). `LexKind` is a **domain, not a
  valence and not a colour** — battlefield colour has one owner, the signature-matched tells in
  `tuning.json`.
- **Tuning:** `StreamingAssets/tuning.json` + F1 debug cockpit (auto-generates sliders by
  reflection). Hot-reload, no recompile.
- **Reused USS across shell and Planning:** inherited *position* is context, inherited *paint*
  is language — override the first, keep the second.

### Client gotchas — these have each cost real time
- **Refreshing scripts mid-Play-Mode** leaves GameObjects alive but **wipes the code-built UI
  tree** (root has 0 children, console clean). Exit and re-enter Play after every source change.
- **An unfocused Editor idles the player loop**, so `Start()` may simply never have run — same
  symptom as above, different cause. Pump `QueuePlayerLoopUpdate` before believing a probe.
  **Sometimes it does not recover at all** (2026-07-24: `frameCount` stuck at 1 through play mode,
  40 queued pumps, `isPaused=false`). **Verify anything frame-driven in EDIT mode** via
  `BuildPreview(tick)`, measuring against the generated tile transforms as a hex-centre yardstick.
  Never `Thread.Sleep` in a RunCommand to wait for frames — it holds the main thread, so nothing
  ticks and every sample reads identical, which mimics the very bug you are chasing.
- **Exiting Play returns the Editor to the Boot scene**, which holds only `~BootLoader` — so an
  edit-mode probe finds no `ReplayPlayer`. Open `Assets/Scenes/Game.unity` first.
- **Game View capture stalls unattended** (`WaitForEndOfFrame` never completes). Driving the live
  UI tree over MCP is the reliable verification; screenshots need Jake's focused Editor.
- **PLAY MODE IS UNREACHABLE FROM A SESSION (found 2026-07-26).** `EditorApplication.EnterPlaymode()`
  inside `Unity_RunCommand` is refused outright: *"User interactions are not supported for MCP tool
  calls."* So **no agent can ever click through the runtime UI** — anything that only exists in Play
  Mode (button wiring, shell state transitions, frame-driven feel) is Jake-only verification, full
  stop. The workable substitute is a **committed edit-mode Editor script + `ExecuteMenuItem`**
  exercising the real DLLs (see `Assets/Editor/RunSaveCheck.cs` and `RenderShots.cs`). Note
  `Unity_RunCommand`'s dynamic assembly cannot reference Warband plugin types, which is *why* the
  harness must be a real Editor script; and Editor scripts live in Assembly-CSharp-**Editor**, so they
  cannot see `internal` types in Assembly-CSharp either.
- **`Unity_RunCommand` rejects `System.Reflection`** outright, and its dynamic assembly cannot
  reference Warband plugin types. Use `SerializedObject.FindProperty` / `SendMessage`, or a real
  `Assets/Editor` script driven by `ExecuteMenuItem`.
- **Syncthing ignores are PER-DEVICE**, and `sync-status: 100%` can predate the local scan —
  confirm over SSH before concluding a file landed.
- **Never write captures inside the Syncthing tree** (they sync back into the repo).
- **`.unity`/`.prefab`/`.asset` edits are guard-hook blocked** — scene work goes through Unity MCP.

5. **PvE-first playable loop** — **RUN/UX BUILT; encounter content BUILD.** ADR 0016 supersedes mandatory ghost bosses: PvE is
   the product, encounters are authored and asymmetrical, a completed run has a final PvE
   victory, and the winning warband may continue into endless until defeated.
   **Run layer is ADR-0016-shaped as of 2026-07-24:** `IRunContent.Boss(act, rng)` returns an
   AUTHORED comp (act-anchored only) · `RunPhase.Defeated` is terminal — **lose any fight and
   the run ends** (Jake's PoC rule) · `Victory` = reached the end of the last act, NOT the old
   best-of-5 `BossWins >= 3` · ghost-capture removed (the snapshot seam may remain unused).
   `RunController.PreviewEnemies(tier)` exists because the encounter rng derives from private
   salts — **never reconstruct a preview client-side**, it will show an army that does not spawn.
   **ADR 0019 + ADR 0020 implementation:** three acts of Fight/Fight/Interlude/Fight/Boss;
   terminal losses; Stable/Fraying/Collapsing fixed rewards; choose 3 of 5 opening draft;
   full-screen Management Hall → Wager → Deployment → Combat flow; Sand
   Market/Armory/Hourstone; visible Interlude and boss choices; and
   3→6 capacity unlock/purchases.
   **GROOMED 2026-07-26 — this item is now a LAWS page, not a work item.** Its two "still
   scaffolding" claims are both false (authored encounters, ADR 0023; per-act bosses, ADR 0024), and
   four of its seven remaining-scope bullets are done: ~~enemy-role grammar~~ ✓ · ~~several
   encounters~~ ✓ · ~~one boss~~ ✓ (three) · ~~encounter/intent preview~~ ✓. What actually remains
   is tracked elsewhere and should not be re-derived here: **risk-tier mutation of authored
   encounters** → item 2④ · **the endless seam** → item 13 · **a defeat/retry rule** → item 16
   (new, below). Everything below this line is settled law that ADRs and design docs reference by
   name, which is why the item keeps its number and its text.
   **Balance law:** preserve spectacular system-breaking engines; intervene only
   when one line erases discovery, all encounter problems, determinism, resolution, or
   readability. **Design notes:** `Design/pve-encounters.md` now owns the settled laws that
   the encounter itself is the boss, every boss is a multi-answer strength exam, the boss
   mechanically rules and teaches its act, and enemy formations are always previewed before
   deployment. All mechanics are inspectable before Play; the rules are known but the outcome
   is not forecast. Boss units have no blanket control immunity; only explicit, previewed
   content passives may negate or reduce a specific verb. Execute remains a true kill and
   preserves normal death/transform consequences. Phase grants complete personal absence
   while encounter clocks and state continue advancing. Fields are factional by default;
   environmental and explicitly volatile fields may affect everyone. Fight flow is
   Encounter Reveal → combined Planning → Play → Result; lineup, equipment, and positions
   remain freely editable together until `BEGIN FIGHT`. **Scope correction
   2026-07-24:** first authored proof is only a visible bonded pair—when one dies, the other
   Enrages. The proof is now playable; evaluate it before committing an act boss, enemy-role roster, or
   encounter ladder; the Dying Procession remains a possible extrapolation, not current scope.
5a. **Hourstone / Inscription engine layer** — **BUILD (acquisition/UI seed integrated;
    engine catalog next).** The expedition carries one
    Hourstone; every distinct Inscription acquired remains active for the run with no slot
    cap. Player-facing presentation is a compact top-screen badge rail driven by replay
    events: inspectable badges pulse on activation, counters expose progress, and
    high-frequency triggers coalesce rather than flash-spam. Catalog target is 24, staged
    as five migrated seeds → twelve-family vocabulary proof → twenty-four engine proof.
    Hybrid acquisition is live: 20%-weighted 7-Sand Workshop offers plus visible
    one-from-three Hourstone Interlude and boss rewards. The Hourstone tool shows owned rules;
    the combat badge/counter rail remains unbuilt. Before catalog expansion, settle the
    per-root activation guard, Bearer of the Mark replacement, and first twelve contracts.
    Legacy `Banner*` code names are migration debt.
6. **Friends playtest #1** — the milestone that ends arguments (ADR 0001), after the PvE
   vertical slice. Distribution/launcher work is allowed only as needed to put that slice
   in friends' hands. No date until Jake calls it.

## First-playable content budget (hard cap — ADR 0001 + ADR 0016, scope settled 2026-07-26)
Current 8 heroes × 2 paths · 11 weapons + 1 trinket · **24 Inscriptions, delivered through
the ADR 0017 proof waves** · **ONE THREE-ACT RUN, one boss per act** (a tiny reusable enemy-role
grammar, several encounters, three act bosses, one event) · shops + placement ·
crude post-win endless seam that may reuse and scale the slice · programmer art, no sound.
Random hero-kits-as-monsters remain scaffolding, not acceptable final PvE content. Do not
expand beyond three acts, to a full endless mode, or to a catalog beyond the 24-effect proof before
playtest #1.

**Scope decision, Jake 2026-07-26.** This cap previously read "one complete authored PvE act / one
boss / do not expand to multiple acts", which contradicted ADR 0019's shipped three-act run shape
and ADR 0024's three bosses. Jake settled it: **three acts is the cap.** Consequence — item 14
(acts 2 and 3 draw an identical pool) is real work, not something to delete, because a three-act
budget only buys anything if the three acts differ.

## Deferred (explicitly NOT now — don't resurrect without Jake)
**All PvP:** ghost server · matchmaking · ratings/leaderboards · PvP rewards · no-stakes
Echo exhibitions (the snapshot seam may remain, but no feature work) ·
Displacement (Push/Pull/collisions) · spoils-of-war (historical ADR 0002)
· sim-modeled projectile flight ("dodge by movement" lever, render-contract) · aura
ExcludeOwner option · morale/rout concept · ability crits · predetermined terrain (NEVER)
· account-scoped power (NEVER — fairness law).

## Open design questions (ammo for DESIGN sessions)
Content-fidelity leftovers (2026-07-23, from the de-SIMPLIFY pass): **Wide Banner**
reads as "inner circle gets innate+crown" instead of "reach replaces" — proposed as
the actual design, needs Jake's nod (**ADR 0022 makes it a one-liner now**:
`SignaturePatch = Patch(radius: 1)` grows the Rally radius, so this is a nod away) ·
~~sig-override composition wart~~ **FIXED 2026-07-25 (ADR 0022)** — signature patches
compose in node order, so Breach the Line is board-length AND escalating; pinned by
`SignatureCompositionTests` · **Twist's crit-memory** is a 30-tick Mark, not "since last cast"
(cast-event ordering) · **weapon fidelity:** War-Priest does not yet acquire mace mastery; Tower
Shield has no base defensive stat; reforged-item resale does not remember forge spend;
returning to an implicit starter resets its temper; Company Standard currently expresses
"Company potency" as an adjacent opening-Haste muster ·
Inscriptions: pool assignment · first twelve effect contracts ·
per-root activation representation · exact Bearer of the Mark replacement · legacy
Banner-data migration ·
PvE vertical slice: ~~encounter-role budget~~ (answered, ADR 0023: five roles) ·
~~enemy intent preview~~ (answered, ADR 0024: rule + per-body behavior disclosed every fight;
the DEEP enemy inspector is still open — item 12) · risk-tier mutation shape ·
endless cycle/post-rank-S decisions/scaling/score ·
Sand/economy values (initial ADR 0019 tuning until sweep/playtest) · respec cost (free-for-now decided,
revisit) · per-rank stat scaling.

## Done
- **2026-07-26 — RUN SAVE/RESUME (item 7, 412 tests).** Quitting the app no longer destroys the run.
  `Warband.Run.RunSave` converts `RunState` ⇄ text and **does no file IO** (the run layer is pure by
  law, ADR 0008) — the host owns the bytes, which is also what keeps the format headless-testable.
  Hand-rolled rather than JSON: Warband.Run has zero package references so the DLL drops into Unity
  unchanged, and reflection-based serialization gets stripped by IL2CPP. Format is
  `dotted.key=value` lines behind a version header — order-independent, unknown keys ignored,
  explicit `.count` on every list, and **content ids that could collide with a delimiter throw at
  WRITE time** rather than silently corrupting a save.
  `RunController.Resume(state, content, cfg)` rebuilds the machine without regenerating anything
  (regenerating maps or shop stock would replace what the player was looking at) and **resolves every
  content id eagerly**, so a save from an older build fails with the offending id named instead of
  mid-fight. Client half: `RunSaveFile` writes temp-then-move so a crash mid-write leaves the
  previous good save intact, never throws at the caller, and **deletes any save it cannot read** so
  CONTINUE can't fail forever. Autosave hangs off `Rebuild()` — the shell's single choke point, so no
  future action can change the run without the save following — plus `OnApplicationPause/Quit` for
  alt-tab. CONTINUE now means "a run exists, in memory or on disk"; a discarded save says so on the
  menu instead of failing silently.
  **The test that matters:** a run saved, serialized, and rebuilt from text plays out **identical**
  to one that was never saved — same encounters, same battles event-for-event (order-sensitive log
  hash), same Sand. Plus: earned growth and frozen offers survive · sold-out offer slots stay empty ·
  an implicit starter weapon stays implicit (null ≠ "") · a hero with no trinkets resumes with none ·
  truncated/garbage/future-format saves are refused · Reward-phase and PendingSpec saves resume still
  owing the choice.
  **Verified ON WINDOWS, not just headless.** New committed harness
  `client/Assets/Editor/RunSaveCheck.cs` → menu `Warband/Verify Run Save`, MCP-drivable, edit-mode
  only. Run this session against the real DLLs: save lands at
  `C:/Users/jwjwi/AppData/LocalLow/InhouseBoyz/Warband` (2066 bytes) · temp file consumed by the
  move · **bytes survive Windows text IO unchanged and no CR is injected into the record
  separator** · resumed act/beat/phase, Sand, warband and shop stock all match · a future format is
  refused · cleanup works. 12/12 PASS, console 0 errors.
  **STILL UNVERIFIED — needs Jake at the keyboard:** the shell wiring (does the CONTINUE button
  appear on a cold start, does clicking it resume, does the autosave hook fire on every action).
  **`EditorApplication.EnterPlaymode` is refused over MCP** — *"User interactions are not supported
  for MCP tool calls"* — so Play Mode is not reachable from a session at all. **That is a new,
  permanent constraint worth knowing: no agent can ever click-through this client.** Add it to the
  client gotchas.
  **Known behavior, not a bug:** quitting mid-fight-playback resumes at the *next* beat — the fight
  had already resolved and paid, so nothing is lost, but the result report is skipped.
- **2026-07-26 — ACT BOSSES + THE DISCLOSURE CONTRACT (item 2 ①②, ADR 0024, 392 tests).** Built
  overnight, unattended. Each act now closes on a different strength exam instead of the same bonded
  pair three times: act 1 **The Last Oath** (`BOND`, unchanged and deliberately so — it is the only
  boss whose decision has been measured), act 2 **The Ashfall Battery** (`BATTERY` — a Rooted gun
  behind two Colossi that shells your FARTHEST unit and leaves a burning crater, so bunching behind
  the tank is the losing answer), act 3 **The Waning Crown** (`WANING` — a bell fed by time AND by
  **every death in its court**, so clearing the escorts is what rings it). Bosses are authored FOR
  their act and take no act curve; the multiplier survives only past act 3 for the endless horizon.
  **The disclosure half was the bigger find.** The live planning beat hardcoded "THE LAST OATH" and
  disclosed *nothing* for the four node encounters, and enemy cards were built by `UnitCardFromDef`,
  which titles from `ContentLexicon.Chassis(ChassisId)` — so an **Hourling previewed as "Shade" with
  the Shade's ability text**, a Colossus as "Bulwark", an Hour-Scribe as "Pyromancer" reading out
  Inferno. That is worse than no disclosure. Now: `EncounterBrief` carries every body (role, accent,
  post-scaling HP/power/cadence/reach, row, and a **behavior sentence** covering the targeting rule
  `pve-encounters.md` always demanded); brief and spawn are built by ONE method so divergence is
  structurally impossible; enemy cards use the authored name and no portrait.
  **New instrument `--boss`** (`Projects/boss-probe-2026-07-26.md`) holds a boss to a harder bar than
  `--enc`: how many *kinds* of strength can pass it. It immediately caught the act-2 boss posing
  nothing (three of four axes at 100% from every formation, spread 0) — the bell went 14s → 9s
  against the measurement, not against taste. All three now show spread 100 and 3-4 passing axes.
  Two render fixtures added through a new `encounter` seam in `scenarios.json`. **Gates:** 392 tests ·
  scenarios round-trip + byte-stable across two runs · DLLs rebuilt · whole client compiled headless
  against Unity 6000.3.19 reference assemblies (gate itself negative-controlled).
  **NOT eyes-verified — nothing was watched in Unity.**
  **Session-hygiene finding, flagged loudly:** the tree held **178 uncommitted files** including the
  ENTIRE ADR 0022 + ADR 0023 implementation (`Enemies.cs`, `EncounterProbe.cs`, the unit-behavior and
  signature-patch tests, `MechanicalRulePresenter`) and most of the client shell — all listed as
  Done on this board while absent from git. That is why the `--enc` drift above could not be
  bisected. Committed as part of this session; **future sessions must commit their own work.**
- **2026-07-25 — AUTHORED PVE ENCOUNTERS (item 2, ADR 0023, 368 tests).** Enemies now have their own
  designs (Jake's call): authored `UnitDef`s with no chassis/rank/weapon/tree, not composed hero
  kits. Five roles — Hourling (swarm), Ashen Colossus (anchor), Sanddrift Gunner (artillery,
  acquires FARTHEST + standoff), Hour-Scribe (rooted ritual clock), Gloamstalker (opening Leap) —
  compose four node encounters, replacing random kits-as-monsters in `Catalog.Encounter`.
  **Composition is the act lever** (ADR 0016): factories size themselves by act, and an act's pool
  is its identity — The Long Range is act 2+ because a rank-C opening warband cannot clear it from
  ANY formation. Two authored rules bend the shared model on purpose and are both disclosed: WARD
  (50% DR while escorts live, stripped on the first escort death) and RITUAL (mana fed by trickle
  ALONE — needed per-unit `ManaPerHitTaken`, mirroring ADR 0022's `ManaPerSwing`, because on the
  global hit-fed rate a channeller fires the instant it is focused, inverting the problem).
  `IRunContent.EncounterBrief`/`BossBrief` + `RunController.PreviewBrief` carry the disclosure off
  the same private salt as `PreviewEnemies`.
  **The `--enc` probe is the real deliverable here** — it reports per-act win%, the SPREAD between
  best and worst formation, whether each rule fired, and how the naive bot line does. Its first run
  caught three of four encounters posing nothing and the Ninth Bell's ritual never firing (the
  countdown was longer than a fight). All four now pose a placement problem at their debut act.
  **Difficulty moved hard:** bot tier EV 88/92/79 → **35/48/39**, Fraying beating Stable.
  `FullRunsCompleteOnRealContent` stopped asserting the bot always wins (against authored content +
  terminal loss that would mean the PvE poses nothing) and now asserts the machine completes, the
  arc is reachable, and it is not free. `StarterWarband` drafts a plausible comp instead of
  `pool[0..2]` — the arbitrary one had a heal-auto Cleric and a Tower Shield Bulwark, i.e. one real
  damage source, and lost the first fight of every run. **Not eyes-verified: nothing was watched in
  Unity.** Scenarios regenerated, DLLs rebuilt.
- **2026-07-25 — UNIT BEHAVIOR LAYER + WEAPON CADENCE + SIGNATURE PATCHES (ADR 0022, 346 tests).**
  A systems review of the class/weapon/tree layer read the vault against the runnable content and
  found four levers the design already assumed but the sim had never grown. All four built, plus
  one plain bug. **① Every unit shared one brain** — `AcquireTargets` was nearest-only with no
  per-unit hook (combat-grammar.md promised "kits override" in round 6), movement was
  close-then-stop, and **no chassis ever set `MoveInterval`**, so all eight moved at the default.
  Now `TargetPref` (Nearest/Farthest/LowestHp/HighestHp, acquisition only — stickiness/Phase/Taunt
  untouched), `Standoff` (give ground to hold firing distance, never out of range, keeps attacking
  while withdrawing), and per-chassis speeds 3–7. Nodes may set all three, so **a fork can change
  the hat at the behavior layer** — Lifebinder's backline SWAP finally moves her.
  **② `WeaponDef.ManaPerSwing`** replaces the flat rate: mana/tick now spans 0.83 (daggers) → 1.40
  (mace, 2.80 mastered) instead of being purely 1/Interval. **③ Signature patches** — degree, not
  verb; 12 copy-pasted overrides converted; `AbilityIdentity` counts patches so cast tells survive.
  **④ Four trinkets** on the wired-but-unused `ManaMaxDelta` seam; the three item layers now own
  disjoint jobs (weapon = attack profile · trinket = chassis stat-shape · Inscription = team rules).
  **⑤ Frenzy** was bypassing `AttackInterval` outright — a window was worth 4 × weapon Damage at no
  tick cost, making the musket the correct Berserker weapon and his own daggers a trap; it is now
  +300% attack speed. **Sweep re-run** (`Projects/sweep-2026-07-25.md`): Sharpshot 46→62 and
  Pyromancer 32→46 (the two classes named as fighting their own pathfinder), Shade 60→45, Bulwark
  65→53, **Banneret unchanged at 12 — structural, as predicted**. No chassis-DOMINANT build remains
  (top was 94%, now 86%). New flag NAMED not tuned: `shade:reaper+widowmaker` DEAD at 8–9%, most
  likely the daggers cadence cut. Last Oath still poses its decision (Enrage 97%, placement chooses
  in 4/4 lineups, Δ96). Scenarios regenerated + Unity DLLs rebuilt. **Not eyes-verified:** nothing
  in the client was re-watched — Standoff and per-chassis speeds change how fights LOOK, and that
  wants Jake's play pass.
- **2026-07-25 — THE LAST OATH'S DECISION IS REACHABLE (item 3, 313 tests).** The 07-24 probe's
  "**THE CHOICE DOES NOT EXIST**" was geometry, not numbers: the pair stood asymmetrically
  (Bulwark (5,2), Sharpshot tucked behind at (6,4)), so the Sharpshot was structurally
  unreachable first and the Bulwark died in 1000/1000 fights. Both Oathbound now stand on the
  same rank at opposite board edges — **(5,0) / (5,5)**, a two-line data change. Result: both
  survive in real fights, **placement chooses the survivor in 4/4 lineups**, and the two
  branches cost Δ84 win%. Four placements were measured before shipping; the two inner-symmetric
  ones also pose the decision but make act 1 hard enough that the bot loses 4/6 seeded runs.
  The probe gained a "does placement choose the survivor?" section — the pitch is now something
  the report can actually answer. Report: `Projects/oath-probe-2026-07-25.md` (supersedes
  `oath-probe-2026-07-24.md`). **Named not tuned:** the decision has a strongly correct answer
  (kill the archer — a lesson, not yet a dilemma) · four arrangements kill both together so
  Enrage never fires and nothing in the UI names that · two arrangements run ~385 ticks.
- **2026-07-24 — FIRST-PLAYABLE RUN + PERSISTENT PLANNING UX (ADR 0019, 278 tests).**
  Three-act/five-beat state machine · terminal loss · initial Sand economy · deterministic
  Interludes and boss rewards · choose 3 of 5 draft · persistent board-first Planning
  replacing Map/Shop/Deploy · data-first shared cards + inspector · select-then-Buy/Hold ·
  portrait/icon presentation catalog · responsive landscape-touch and keyboard input · reduced
  motion + timing tokens + semantic audio hooks. Direct play feedback then rebuilt the opening
  draft as a full-screen portrait-led comparison: readable signature/passive blocks, semantic
  stat colours/icons, large values, and a strong 0/3 → 3/3 selection/action state. Unity Play
  Mode verified with a clean console; captures are in `client/McpCaptures/`. Detail:
  `Daily/2026-07-24`.
- **2026-07-24 — PLAYABLE POC SHELL + DEPLOYMENT + SCENES (263 tests).** Run layer retargeted to
  ADR 0016 (authored boss, `RunPhase.Defeated`, best-of-5 removed, ghost-capture dropped) ·
  `RunSetup` recruit draft · the whole client shell (Menu/Recruit/Map/Deploy/Shop/RunOver) on a
  view-model + router pattern · deployment with swap/pick-up and previewed enemies ·
  shop depth (equip/unequip/reforge/sell/bench) · `PreviewEnemies` · `GameBoot` startup order ·
  Boot→Game scenes · board no longer self-starts. Detail: `Daily/2026-07-24`.
- **2026-07-24 — RENDER + DATA SYSTEMS (item 4b).** Data-driven replay pipeline (`scenarios.json`)
  · `ReplayInspector` · signature-matched tells (`TellMatch`) · field flavor · directed tells ·
  unit identity + fight story · event viewer · walls block-then-adapt + firing-angle seek ·
  replay v3 snapshot identity · the Lexicon (id → words, one source). Detail: `Daily/2026-07-24`.
- **2026-07-23 — UNITY CLIENT BRING-UP (item 4).** Unity 6.3/URP project, Syncthing + MCP pipeline,
  sim→Unity DLL bridge, first replay render, diorama look, JSON tuning loop + F1 cockpit.
  Detail: `Daily/2026-07-23`.
- **2026-07-23 — OUTLIER SANITY SWEEP (item 3).** `Warband.Sweep`, 2,080 fights + 360 bot runs;
  zero caps/crashes, determinism intact; outliers NAMED not tuned (Phase uptime, Warden Taunt,
  Banneret floor) and victory saturating ~99% at every tier. Report:
  `Projects/sweep-2026-07-23.md`.
- **2026-07-23 — HERO/BUILD CONTENT PASS (item 2).** 8 kits as data (80 nodes traced to their dive
  docs), 11-weapon catalog with mastery riders, stat law, reforge, ForkRank, Bearer; then the
  fidelity pass rebuilding 12/13 SIMPLIFIED nodes to dive truth.
- **2026-07-23 — SIM MECHANICS BUILD QUEUE (item 1).** The whole dive backlog as reusable grammar
  primitives — everything Inscription/Relic-hookable, no unit-hardcoded specials.
- **2026-07-23 — PVE-FIRST IDENTITY AMENDMENT (ADR 0016).** PvE is the product; authored
  asymmetrical encounters and bosses replace mandatory ghost bosses; the player fantasy is
  assembling compounding interactions that feel like they break the game; the authored run
  has a real victory and may continue into endless until defeated. PvP moved wholly to
  Deferred. Pitch, theme, top-level guidance, affected historical ADR statuses, and this
  board realigned. Exact vertical-slice run/loss/endless rules intentionally remain DESIGN.
- **2026-07-23 — DESIGN CAMPAIGN COMPLETE (1a–1d).** Theme (ADR 0010) · impact model
  (ADR 0011) · 8/8 hero dives settled (Cleric, Bulwark, Shade, Sharpshot, Pyromancer,
  Berserker, Phalanx, Banneret — all champions named; laws locked along the way: ADR
  0013 targeting, Burn decay, ADR 0014 aura/muster, cheat-death + cross-layer
  precedents) · weapons pass (ADR 0015: 11-category catalog, engine riders, temper
  tiers + Relic rule, Tower forge). Sauce hunt stays PARKED (Design/sauce.md).
  Full session log: Daily/2026-07-22 + Daily/2026-07-23.
- **2026-07-22 — RUN LAYER COMPLETE (109 tests).** Bot-ghost generation (BotGhosts: boards
  sized to slot growth, deepened by act+record, geared, range-aware placement) + full-run
  harness (RunHarness/RunPolicy/AggregateReport: policy hooks, fight+economy metrics,
  deterministic). Smoke: 600 bot runs — Greedy tier strictly dominant under placeholder
  monsters (harness working as intended; tune at sweep/playtest, not now).
- **2026-07-22 — Run-layer design settled + skeleton & shop built (97 tests).** ADR 0006
  (shop & economy: every-node shops, 3→6 act-close slot offers, bench 2, gold), ADR 0007
  (wager tiers, per-kill payout + success bonus), ADR 0008 (run layer = pure host-agnostic
  lib), ADR 0009 (shop stock: offers/freeze/forks/banners/sell). `Warband.Run`:
  RunController machine — maps, wager fights, events, ghost bosses (draws = wins), record,
  slot offers, bench, shop stock, ProgressionFold, snapshot capture (incl. banners); 32 tests.
- **2026-07-22 — Design foundation.** Pitch v0.3; ADR 0001 (identity + anti-washout
  contract); ADR 0002 (best-of-5, wagering, anti-snowball); ADR 0003 (combat soul: clock +
  field, glyphs on flat maps); ADR 0004 (sim framework); ADR 0005 (loadout composition,
  crit-only RNG, weapon-required/range-on-weapon); combat-grammar, heroes anatomy,
  render-contract, placeholder roster docs.
- **2026-07-22 — Sim framework complete (65 tests).** Deterministic tick loop; hex math +
  lines + PCG32; trigger atom w/ negation; statuses incl. Silence⇄Disarm mirror; cascade
  bounds + death phase; ramp/zone/placement passives; run-scoped bonuses (ProgressionFold);
  PlaybackState fold + per-tick reconstruction guardrail; terminal viewer; fields (pulse/
  wall/projectile-path interaction, attached auras, presence statuses); conditional stat
  rules; FightStats + conservation; crit (seeded, attacks-only, IsCrit); 6×8 bounds; Leap;
  loadout composer (chassis/weapon/trinket/node merge).
