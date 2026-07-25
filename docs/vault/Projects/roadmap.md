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

**STATE, 2026-07-25 (honest):** the first-playable run shape and between-fight UX are
walkable end to end: Menu → five-card Draft → full-screen Management Hall → stakes-first Wager
→ formation-reveal Deployment → Fight/replay → Management Hall → Victory/Defeat. Three acts × five
beats, Sand economy, Interludes, boss rewards, and terminal loss are implemented. **278 tests
green.** The workspace has data-first cards/inspector, portraits, explicit economic actions,
responsive touch/compact rules, reduced motion, and timing polish. **Combat viewing still
does not read well enough, and authored encounters still do not make deployment matter.**

1. **FEEL & READABILITY — the fight does not read** — **DESIGN → then BUILD. THE TOP ITEM.**
   **Jake, 2026-07-24, after playing it:** *"playing it now still does not feel great for a
   lot of reasons (UI is not great, sim viewing has some issues and is not quite clear what's
   happening)."* Take this at face value: **item 4b's entire render arc — signature-matched
   tells, directed tells, unit identity, kill feed, fight story — was aimed at exactly this
   target and has not hit it.** Adding more tells is therefore NOT obviously the fix; the next
   move is to find out *why* it does not read before building more of the same.
   **Do not start by building.** Watch a fight with Jake, or capture one and go through it
   beat by beat, and separate the three candidates:
   ① **Presentation** — too much at once, no pacing, no emphasis; the decoupled clock /
   beat-sequencer / hit-stop work (4b, still unbuilt) is the designed answer to precisely this
   and was never finished. ② **Legibility of state** — you cannot tell what a unit IS mid-fight,
   what statuses are on it, or why it did what it did. ③ **UI quality** — the shell screens are
   functional-but-plain; density, hierarchy and typography were never passed over.
   Likely all three, in different measure. **Name which before spending a session on any.**
   **Candidate ③ now has its second real pass:** ADR 0020 replaces the over-dense board-first
   workspace with distinct Management / Wager / Deployment / Combat states, exact card grammar,
   runtime hover/focus disclosure, and large management/combat inspectors. Treat
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
   intact (82b7a6b). **Still open:** per-event animation crossfades from the tell director ·
   Phase 3 VFX packs (Asset Store needs Jake) · Phase 4 client UI · sound stings ·
   camera/framing pass · **live play-mode eyeball of beats/hit-stop + minis in motion
   (static captures can't verify time — first thing next Jake session).**
2. **Authored PvE content** — **DESIGN/BUILD; the biggest gameplay gap.** Deployment works but
   does not yet MATTER, because every normal fight is random kits-as-monsters and every act
   boss is the same act-scaled Last Oath. A small enemy-role grammar + 3–4 encounters that pose
   genuinely different placement problems is what converts the machinery into gameplay. This is
   item 5's remaining scope; see it for the settled laws.
3. **The Last Oath's decision is unreachable** — **SPEC'D, cheap.** `make oath` proved the
   Bulwark dies first in **1000/1000** fights, so the encounter's own pitch ("choose which
   threat you are willing to leave enraged") never occurs. Start with symmetric placement (a
   data change) and re-measure — the probe re-runs in 2.6s. Report:
   `Projects/oath-probe-2026-07-24.md`.
4. **The pressure tier is a fake choice** — **DESIGN.** Stable/Fraying/Collapsing are visible,
   but the sweep found victory saturates ~99% at every tier, so **Collapsing strictly dominates
   at zero risk**. Either
   make risk mean something or delete tiers. ADR 0007 economy is placeholder either way.
Items **5 / 5a / 6** keep their numbers below — their settled laws are referenced from ADRs and
design docs, so renumbering them would break those references.

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
   3→6 capacity unlock/purchases. **Still scaffolding:** normal fights are random
   kits-as-monsters; `Catalog.Boss` returns the
   act-scaled Last Oath for every act. **Remaining scope:** one small enemy-role grammar ·
   several encounters posing different build/placement problems · one boss · encounter/intent
   preview · defeat/retry rule · how risk tiers alter authored encounters · the cheapest
   post-boss continue-until-defeat
   seam. **Balance law:** preserve spectacular system-breaking engines; intervene only
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

## First-playable content budget (hard cap — ADR 0001 + ADR 0016)
Current 8 heroes × 2 paths · 11 weapons + 1 trinket · **24 Inscriptions, delivered through
the ADR 0017 proof waves** · **one complete authored PvE act/vertical slice** (a tiny
reusable enemy-role grammar, several encounters, one boss, one event) · shops + placement ·
crude post-win endless seam that may reuse and scale the slice · programmer art, no sound.
Random hero-kits-as-monsters remain scaffolding, not acceptable final PvE content. Do not
expand to multiple acts, a full endless mode, or a catalog beyond the 24-effect proof before
playtest #1.

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
the actual design, needs Jake's nod · **sig-override composition wart**: an S
signature override drops an A override's texture (Sarissa+DeepThrust keeps length,
loses escalation) — last-wins is ADR 0005 discipline; fix would need additive sig
mods · **Twist's crit-memory** is a 30-tick Mark, not "since last cast" (cast-event
ordering) · **weapon fidelity:** War-Priest does not yet acquire mace mastery; Tower
Shield has no base defensive stat; reforged-item resale does not remember forge spend;
returning to an implicit starter resets its temper; Company Standard currently expresses
"Company potency" as an adjacent opening-Haste muster ·
Inscriptions: pool assignment · first twelve effect contracts ·
per-root activation representation · exact Bearer of the Mark replacement · legacy
Banner-data migration ·
PvE vertical slice: encounter-role budget · enemy intent preview · risk-tier mutation shape ·
endless cycle/post-rank-S decisions/scaling/score ·
Sand/economy values (initial ADR 0019 tuning until sweep/playtest) · respec cost (free-for-now decided,
revisit) · per-rank stat scaling.

## Done
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
