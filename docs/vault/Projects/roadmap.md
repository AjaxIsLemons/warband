# Roadmap — THE live board

**This is the ONLY live priority list.** (Pattern from battle/Shoota execution boards —
multiple competing lists is how projects rot.) Sessions plan from here; see CLAUDE.md
"Planning SOP". Keep it honest: the board must match reality better than memory.

## Stages
- **DESIGN** — needs a design conversation with Jake before building. Don't build; propose.
- **SPEC'D** — designed (ADR/doc exists), ready to implement autonomously.
- **BUILD** — implementation in progress; note what's left. Finish before starting new work.
- **VERIFY** — built, needs verification/tests/polish before calling done.
- **DONE** — move to the Done section with a date.

## Now / Next (ordered — top unblocked item is "what's next")
🎯 **GOAL (Jake, 2026-07-23): a playable PvE PoC.** Path: mechanics build → hero/build
content → outlier sanity sweep → Unity rendering → one authored asymmetric PvE vertical
slice. **North star (ADR 0016): the fun is breaking the game with a compounding warband,
then seeing how far asymmetrical PvE and endless pressure can push it.**
1. **Sim mechanics build queue** — **DONE 2026-07-23 (145 tests, was 109).** The whole
   dive backlog landed as grammar primitives (Jake's law: everything banner/Relic-
   hookable — no unit-hardcoded specials): next-N-swings charges (SwingsLeft) · Burn
   decay pool + merge + BurnAmp · Taunt (forced target + silence) · directional
   Counter (Swing effect + Cause.Counter) · Phase + entry window · Lifesteal/thorns
   (PctOfEventAmount) · overheal→Shield · cheat-death · Death = killer + overkill ·
   gradient StatRules (Full Draw, Burning Hours) · new conds (below-HP, exact-range,
   spacing, engaged-with-ally, taunted-by-owner, has-status, Nth-swing, IsRootEvent) ·
   cleave/pierce-line/MultiShot/double-swing/forced-crit/Execute/Recast/RemoveStatus ·
   HealAutos (censer) · Leap event · corpse field spawns · composer temper tiers +
   Relic rider gate. Muster/Company = BattleStart+AlliesWithin — zero new machinery.
   NOTE: reforge shop action is run-layer → part of item 2.
2. **Hero/build content pass** — **DONE 2026-07-23 (161 tests incl. fidelity pass).**
   `Warband.Content`: all 8 kits as data (80 nodes, every one traced to its dive doc)
   · 11-weapon catalog w/ mastery riders · 5 starter banners · Catalog : IRunContent
   (kits-as-monsters encounters, act+tier anchored) · **stat law landed** (HP/Attack/
   Speed/Range/Crit/Mana; armor = status pair; rank-up = flat per-chassis HP/Attack
   bump + the 1-of-2 offer — one flat Offers table, Jake's "easily changeable" ask) ·
   weapon TIER state through shop/inventory/equip/ghosts · **Reforge action** (forge
   follows the front) · ForkRank law (Shade forks at A — fixed a real BotGhosts bug)
   · Bearer wired via SpecNode.DoublesBanners, BOTH sides (ghost bearers double too).
   **FIDELITY PASS (Jake's call): 12/13 SIMPLIFIED nodes rebuilt to dive truth** —
   new generic shapes: corpse-pool transfer · escalating lines · line-through-farthest
   · in-field cond · shield-scaled StatRule · any-enemy-has cond · triage filter ·
   behind-only lines · victim-anchored selectors · Mark tag status · node cleave
   bonus. Leftover judgment calls → Open questions below. **ADR 0016 clarification:**
   "real content" here meant player build content; the kits-as-monsters encounters remain
   scaffolding and do not satisfy the authored PvE content requirement.
3. **Outlier sanity sweep** — **DONE 2026-07-23** (`Warband.Sweep` console, 4s run;
   full report + interpretation: **Projects/sweep-2026-07-23.md**). 64-build
   round-robin (2,080 fights) + 360 full bot runs. **Clean bills: zero safety-cap
   hits, zero crashes, no chassis-dominant, determinism intact.** Real outliers
   NAMED, not tuned (the bar): ① Phase uptime near-degenerate (Here-and-Gone ≈
   70-80% immune; phantom builds 86-94%) ② Warden Taunt owns small boards (Δ-49
   over Juggernaut) ③ Banneret floor 5-20% (support/harness confound, still stark).
   Placeholder-difficulty finding: victory saturates ~99% at every tier → Greedy
   strictly dominant on gold at zero risk — fix is the difficulty curve, later.
4. **Unity client bring-up → the playable PoC** — **BUILD (started 2026-07-23).**
   Render from PlaybackState fold (render-contract.md). Decision: **mobile-ready,
   desktop-first** (iterate & playtest on Windows; Android target is a later flip).
   **PIPELINE LIVE end-to-end (2026-07-23):** Unity 6.3 project `client` (Universal 3D /
   URP 17.3, Input System 1.19) created on Windows at `C:\Dev\game\warband\client`,
   synced to homeserv via Syncthing folder `warband`, official Unity MCP relay connected
   + approved (RunCommand round-trips clean). Config mirrors Shoota (`.mcp.json` → relay
   at client path, `.stignore`, Makefile sync-status/mcp-test/test, 31 `unity-*`
   skills + `.meta`/scene-edit guard hooks — skipped Shoota-specific
   fishnet/ship/feedback-triage). **Gotcha logged:**
   Syncthing ignores are PER-DEVICE — the Windows side needed its own ignore patterns or
   it indexed 3GB of `Library/` (that was the "file errors"). **Settings pass DONE +
   MCP-verified:** product=Warband, company=InhouseBoyz, id=com.inhouseboyz.warband
   (Standalone/Android/iOS). Mobile-ready foundation already in the template (Linear
   color, Input System New, .NET Standard = netstandard2.1-compatible). Console clean.
   **SIM→UNITY BRIDGE DONE + verified (2026-07-23):** `make unity-sim` builds
   Warband.Sim (netstandard2.1) → `client/Assets/Plugins/Warband/Warband.Sim.dll`,
   Syncthing carries it, Unity imports as managed plugin (AnyPlatform), assembly loads
   in-domain (40 types incl. PlaybackState/BattleEvent), and REAL project code compiles
   against + runs the fold in-Editor (`Assets/Editor/WarbandSimSmoke.cs` menu item →
   `[WarbandSimSmoke] OK: units=2 tick=0`). MCP gotcha logged: the Unity_RunCommand
   dynamic-compile sandbox can't reference user plugins (CS0246) — test plugin code via
   a real Assets/ script + `ExecuteMenuItem`, not inline RunCommand; and importing a
   DLL/new script triggers a domain reload that briefly returns "Unity not detected",
   recovers on re-probe. **FIRST REPLAY RENDER DONE + eyes-verified (2026-07-23):**
   full chain works — `Replay.Write/Read` (shared binary serializer in Warband.Sim, no
   deps) · `make replay` runs the sample fight → `client/Assets/StreamingAssets/
   replay.bytes` (6 units, 348 events, round-trip view-hashes match) · `ReplayPlayer`
   MonoBehaviour loads it, folds via PlaybackState at 10 ticks/s, builds board + team-
   colored capsules + HP bars, auto-frames the camera · MCP scene wiring (add component
   by reflection, save, EnterPlaymode) · multi-angle scene capture shows units on the
   board, moved from spawn rows into engagement (Update loop advancing the fold). Warband.
   Content stays net10.0 (only needed offline to GENERATE replays; retarget when Unity
   runs live fights). Left in repo: `Assets/Editor/WarbandSimSmoke.cs` (bridge sanity,
   deletable). **NEXT (finish render legibility; item 5 designs the live loop):**
   game-camera capture path (Camera_Capture by-id failed → use scene-view for now) · render the other event
   tells (statuses, fields/walls, casts, deaths) one-canonical-signature each. Then stop
   before wiring run outcomes until item 5 is SPEC'D. Terminal viewer retired for
   fight-watching — the client renders. **Do not wire the obsolete best-of-five/ghost
   outcome into the client.**
4b. **Render polish & juice systems** — **SPEC'D → `Design/render-polish.md` (2026-07-24).**
   Make the client look good + READABLE as reusable SYSTEMS, not per-effect hacks. Spine: a
   **Feedback Director** (EventKind→ScriptableObject tell registry · beat sequencer =
   Hearthstone-BLOCK causality · pooling) running on a **decoupled playback clock** —
   presentation time is FREE (the fight is precomputed, so hit-stop/slow-mo/stagger can never
   desync the sim; never `Time.timeScale`). Spectator lens: readability + pacing > punch
   (telegraphs · causal-linking via visible projectiles · one canonical tell per event kind ·
   fixed color language · rationed trauma-shake · graduated hit-stop for emphasis/de-clutter).
   Aesthetic: **tabletop diorama** (URP post: Neutral tonemap + Bloom via emissive/HDR +
   vignette + tilt-shift DoF · 3-point light + APV + SSAO · compressed palette, bright=important ·
   silhouette-first shapes). Minimal-dep: **+PrimeTween only**, hand-roll the rest (director,
   trauma shake, hit-stop, hex mesh, decals, pooled world-TMP text); skip VFX Graph / Feel /
   DOTween. 10-step build order in the doc; **first visible slice = steps 1-6** (diorama look +
   one event wired end-to-end). Grounded in 2 research passes (juice/reference-games +
   Unity 6.3/URP 17), both folded into the doc. Current `ReplayPlayer` = throwaway v0, refactors
   into clock + Director + proper UnitView. **BUILD PROGRESS (2026-07-23): steps 1-4 done** —
   URP HDR + diorama post-stack (Neutral tonemap · Bloom [armed, idle until emissive VFX] ·
   Color · Vignette · Gaussian DoF) · 3-point light rig (key/fill/rim, soft shadows, tight
   shadow dist, gradient ambient) · dark backdrop → reads as a lit board slab in a void
   (`Assets/Settings/DioramaVolume.asset` + saved SampleScene). **Capture path SOLVED (key
   unblock):** MCP capture tools don't show URP post-processing (scene-view ignores the volume;
   `Camera_Capture` by-id broken) → render `Camera.main` to a PNG in editor C# + `scp` it off
   the Windows box. **STEPS 5-6 DONE (2026-07-23):** real pointy-top **hex grid** (gap-lines
   read as the grid, blue/red **team-zone tints**, aura now renders as hex tiles) · **Feedback
   Director** skeleton (event→tell switch, grows into the SO registry) with **DamageDealt→
   hit-flash (crit=gold) + Cast→caster-glow** wired end-to-end, MPB-based (no material churn),
   verified in a frozen capture (a unit flashed white mid-fight). Edit-mode `BuildPreview(tick)`
   replays the last ~2 ticks' tells so a static shot reveals them. **NEXT:** more tells (death
   poof · cast telegraph/projectile for causal-linking) · floating damage numbers (step 7) ·
   emissive particles so Bloom bites (step 8) · **+PrimeTween** (step 9) · trauma shake + hit-stop
   (step 10) · then the decoupled-clock/beat-sequencer refactor.
   **DATA-DRIVEN REGISTRY DONE (2026-07-23):** Director promoted from switch → **ScriptableObject
   registry** — `FeedbackDefinition` (eventKind · source/target side · flash color/crit/duration ·
   scale-punch · floating-number color/threshold/size) + `FeedbackRegistry` (list, looked up by
   EventKind), auto-loaded from `Resources/FeedbackRegistry`. 3 tells authored as data
   (`Resources/Feedback/DamageDealt|Cast|Heal`): DamageDealt→white/gold flash + punch + red
   number · Cast→cyan glow + punch · Heal→green flash + green number. Jake tunes/adds tells in the
   Inspector, no code. **Pooled floating numbers** (`FloatingNumber`, legacy TextMesh, dependency-
   free) wired + verified (number spawned in a live frame).
   **JSON TUNING LOOP DONE (2026-07-23) — the SO registry was superseded by a JSON pivot** (Jake's
   call: config must be AI-editable text, not Inspector-only `.asset`). Full loop built + PROVEN:
   `StreamingAssets/tuning.json` (camera · post-FX · numbers · `tells[]`) = source of truth, parsed
   with **Newtonsoft** (string enums + hex colors + PopulateObject-over-defaults + Replace lists +
   Error-on-typo). Hybrid surface (Jake's pick): `TuningData` POCO + `TuningConfig` MonoBehaviour +
   custom Inspector (`TuningConfigEditor`) with Reload/Write/Apply buttons → sliders for him, text
   for me. **Hot-reload with NO recompile:** `TuningConfig.ReloadAndApply()` (MCP-callable + menu).
   Proven end-to-end: edited tuning.json (number size, camera) → synced → ReloadAndApply → rendered
   → change visible, instant, no domain reload. Fixed Jake's complaints: numbers now readable
   ("19" legible), camera lower/closer. Old FeedbackDefinition/Registry SO deleted. Research
   (`tuning-loop-research`) validated the setup. Loop details → memory [[render-tuning-loop]].
   **F1 DEBUG MENU DONE (2026-07-23):** in-game tuning overlay (`DebugMenu.cs`, hand-rolled IMGUI
   per `debugmenu-research` — zero-dep, dodges the new-Input-System EventSystem clash). F1 toggles;
   live sliders for battle-speed · camera · post-FX · numbers · per-tell (flash/punch/number + RGB
   color sliders); edits apply instantly (ReapplyTuning); "Save JSON" writes tuning.json, "Reload
   JSON" pulls agent edits. Auto-spawns on play (`RuntimeInitializeOnLoadMethod`), no scene wiring.
   Closes the loop BOTH ways: Jake tunes in-game → saved to JSON → agent sees the diff; agent edits
   JSON → Jake reloads. Verify: press F1 in play (IMGUI can't be captured via the render-to-PNG path).
   **DATA-DRIVEN REPLAY PIPELINE DONE + eyes-verified (2026-07-24):** the client now renders
   **REAL Catalog builds, not toy capsules.** `sim/Warband.Viewer/scenarios.json` authors fights
   as data (each unit = chassis + B/A/S path picks + weapon/tier/mastery/rank + optional banners);
   `make scenarios` composes them via the proven `Loadout.Compose` path → 5 diverse replays into
   `client/Assets/StreamingAssets/replays/` (duel · castfest · stomp · statusstorm · skirmish),
   each round-trip-verified (client view-hash == live fight). Viewer now refs Content+Run.
   **`ReplayInspector`** (Warband.Sim, tested) folds a replay into its distinct PRESENTATION
   signatures (Damage by Cause + crit · Status by kind · Field wall/zone; counts/amounts/tick-spans)
   — the "what tells does this fight need?" tool; `make coverage F=<replay>` prints it. All 5 fixtures
   rendered + eyes-verified via the MCP render-to-PNG/scp loop (real hero kits, fields, bars, numbers,
   diorama post-stack). **SIGNATURE-MATCHED TELLS DONE + eyes-verified (2026-07-24):** tells now key
   on the FULL event signature, not just EventKind. `TellMatch` (Warband.Sim, tested — most-specific
   rule wins, generic = fallback) + `TellDef.byCause/byStatus` filters; Director rewritten to dispatch
   via it. Authored as JSON data (no code per tell): Damage/**Burn**→orange · Status/**Taunt**→purple ·
   Status/**Stun**→violet · Status/**Phase**→cyan, alongside the existing white-hit/gold-crit/cyan-cast/
   green-heal. Verified in captures: a burn-orange flash beside a white attack-flash; twin purple Taunt
   flashes + a cyan Phase shimmer. **165→169 tests** (+4 ReplayInspector, +4 TellMatch, all headless).
   **NEXT:** death poof (needs a new executor — the dying unit hides same-tick, so the tell must be a
   detached pooled effect at the death hex) · field color-per-kind (all fields render one yellow now) ·
   scenario picker in client/F1 (flip fixtures live) · emissive particles (Bloom bites) · committed
   contact-sheet render menu (tonight's render loop, made repeatable) · +PrimeTween · shake/hit-stop ·
   decoupled-clock refactor. Render loop gotcha: write capture PNGs OUTSIDE the Syncthing tree (they
   synced back into `client/shots`; cleaned + now ignored).
5. **PvE-first playable loop** — **DESIGN (identity settled; vertical-slice rules next).**
   ADR 0016 supersedes mandatory ghost bosses: PvE is the product, encounters are authored
   and asymmetrical, a completed run has a final PvE victory, and the winning warband may
   continue into endless until defeated. Before BUILD, settle only what the first slice
   needs: one small enemy-role grammar · several encounters that pose different
   build/placement problems · one boss · encounter/intent preview · defeat/retry rule ·
   how risk tiers alter authored encounters · the cheapest post-boss continue-until-defeat
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
   Preview → Prepare/reconfigure → Deploy positions only → Play. **Scope correction
   2026-07-24:** first authored proof is only a visible bonded pair—when one dies, the other
   Enrages. Play that relationship before committing an act boss, enemy-role roster, or
   encounter ladder; the Dying Procession remains a possible extrapolation, not current scope.
6. **Friends playtest #1** — the milestone that ends arguments (ADR 0001), after the PvE
   vertical slice. Distribution/launcher work is allowed only as needed to put that slice
   in friends' hands. No date until Jake calls it.

## First-playable content budget (hard cap — ADR 0001 + ADR 0016)
Current 8 heroes × 2 paths · 11 weapons + 1 trinket · 5 starter banners · **one complete
authored PvE act/vertical slice** (a tiny reusable enemy-role grammar, several encounters,
one boss, one event) · shops + placement · crude post-win endless seam that may reuse and
scale the slice · programmer art, no sound. Random hero-kits-as-monsters remain scaffolding,
not acceptable final PvE content. Do not expand to multiple acts or a full endless mode
before playtest #1.

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
PvE vertical slice: standard-run act count/length · encounter-role budget · enemy intent
preview · defeat/rewind/fail-forward rule · risk-tier mutation shape · final-boss victory
reward · endless cycle/post-rank-S decisions/scaling/score ·
Currency/tier final names (gold + Safe/Even/Greedy are placeholders until theme/lore) ·
economy numbers (placeholder until sweep/playtest) · respec cost (free-for-now decided,
revisit) · per-rank stat scaling.

## Done
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
