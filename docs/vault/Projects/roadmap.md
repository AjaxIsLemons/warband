# Roadmap — THE live board

**This is the ONLY live priority list.** Sessions plan from here; see CLAUDE.md "Planning SOP".
Keep it honest: the board must match reality better than memory.

**ACTIONABLE-ONLY LAW (Jake, 2026-07-28, second re-cut):** every numbered item here is **DESIGN**
(drive the chat with concrete proposals, then build on his nod) or **BUILD/SPEC'D** (start now).
Nothing parks here — no status essays, no VERIFY limbo, no laws. Where the rest went:
- Play-pass feedback list (sessions keep it current): **`Projects/play-pass.md`**
- Client architecture + session gotchas: **`Design/client-architecture.md`**
- Content budget (the hard cap): **`Design/content-budget.md`**
- Run/PvE laws pages (5 / 5a / 6), parked-item detail, full history: **`Projects/roadmap-done-archive.md`**
- Blow-by-blow logs: `Daily/<date>.md` · balance numbers: `Projects/balance-baseline.md`

🎯 **GOAL (Jake, 2026-07-23): a playable PvE PoC.** **North star (ADR 0016): the fun is breaking
the game with a compounding warband, then seeing how far asymmetrical PvE and endless pressure can
push it.** **Three-month slice proof (Jake, 2026-07-28):** a standard run takes 45–60 minutes;
Jake completes one fresh run through the final boss and enters endless; one cold friend attempts
the run without guidance and can draft, navigate the Hall, deploy, identify the major combat
events, and describe an evolving build intention. No unresolved comprehension or continuation
blocker remains. Jake's run must upload a complete log; the friend's decision trail must be
retrieved through their stopping point. The friend need not finish: confusion and drop-off are
evidence, not a failed test.

## Stages
- **DESIGN** — needs Jake's decision; don't build, propose (chat-sized ones resolve in a message).
- **SPEC'D** — designed, ready to implement autonomously. · **BUILD** — in progress; finish first.
- **DONE** — one line in Done with a date, detail to the archive. Machine verification (tests,
  captures, matrices) is part of BUILD, not a parking stage; in-motion feel goes to
  `Projects/play-pass.md`, never back onto the board.

## Now / Next (ordered — top item a session can start is "what's next"; Jake reorders at will)

**1. Item 30 — combat payoff slice** — **BUILD** (2026-07-28: unit-HUD readability sub-slice
   SHIPPED — number attribution by perspective (crimson incoming / typed output / gold =
   player crits + "!"), shield at the bar tip, delayed damage trail, status-row plates,
   magnitude→size/lifetime/luminance ramp; capture-verified + contact-sheet byte-identical ×2;
   plan/evidence in `docs/ui-reviews/outbox/unit-hud-readability/`. Remaining from that review:
   P2 TMP double-outline text (needs an editor font-asset session) and P4 bar-contrast tuning;
   play-pass watches filed). The build already asks enough pre-fight
   questions; now make the answer readable and satisfying at the real play camera. Finish
   event-driven Attack/Cast/Hit/Death crossfades on the existing KayKit units; tune camera,
   unit scale, beat sequencing, hit-stop, battle speed, combat mix, and the major cast/death
   effects; keep authored enemy roles distinct in motion. Fold old items 25/26 into this slice:
   conditional passives need a persistent on-body LIVE mark, and overlapping muster rings need
   per-owner identity. Also fold: the world-space kill feed clips at the right frame edge on the
   8-wide board (anchor sits outside fov 34's horizontal budget — ADR 0027, seen in captures). Existing assets/native effects only — no paid pack or new sim verb.
   Acceptance: autos, casts, deaths, enemy threats, and build-rule activations read without the
   inspector at 0.5×/1×/2×; representative normal/boss/swarm/ritual/Inscription fights are
   capture-checked, then watched in motion.
**2. Item 31 — run pacing + decision economy** — **DESIGN, evidence-first.** The first input is
   Jake's fresh baseline run and its telemetry. Measure total run time, fight time, Hall dwell,
   purchase/rank/roster timing, unused Sand, and repeated or no-op decisions. Tune the existing
   12-fight / 3-Interlude / 3-boss structure to 45–60 minutes while preserving visible build
   evolution. Remove/compress decisions that do not change the warband; add no currency, offer
   layer, hero, item, or progression system.
**3. Item 32 — encounter differentiation + difficulty curve** — **DESIGN.** Current probe:
   three of six node families are free/flat for purpose-built parties while the naive line
   completes 1/12 runs. Rework or retune The Gnawing Hour, The Long Range at its intended acts,
   and The Long Procession; close the weak-legal-comp versus competent-party gap; restore a
   credible third answer to the act-1 boss on the 8-wide board. Use the existing five-role
   grammar and protect the strong formation sensitivity/multiple answers already present in
   Ninth Bell, The Drop, Slagworks, Ashfall Battery, and Waning Crown. Human telemetry decides
   direction; probes confirm the result, never set a uniform win-rate target.
**4. Item 15 — THE EVENT** — **DESIGN.** Spend the budgeted ONE event on a genuine run gamble,
   distinct from the deterministic Interlude reward choice. Reuse existing rewards and
   consequences; place it where item 31's telemetry finds the largest pacing valley. Do not
   create an event catalog.
**5. Item 13 — Beyond the Hour, the endless seam** — **DESIGN, then a small build.** After the
   final boss, offer RETIRE WITH VICTORY or CONTINUE with the same warband. Reuse act 3's pool in
   escalating cycles; initial score = cycles + beats survived; endless defeat preserves the
   standard-run victory. Persist continuation/cycle/score in save/resume and telemetry. No endless
   economy, special reward pool, leaderboard, or metagame in this slice.
## Deferred (explicitly NOT now — don't resurrect without Jake; detail in the archive)
- **Gated/parked:** Inscription wave 3, 12→24 (gated: the twelve must stay legible in play) ·
  risk-tier mutation of encounters · item 27's Workbench polish batch · item 1b's remaining Hall
  choreography/Rule Preview/device polish · audio D2 re-bake vs regenerate (ears —
  `Projects/play-pass.md`) · forecast UI · paid VFX packs. The Hall/Workbench already exceed the
  battlefield in finish; they return only when the slice proof identifies a real comprehension
  problem there.
- **Settled guards (do not re-open):** terminal loss STAYS — mitigation is save/resume, no retry
  currency, don't soften act 2's pool (archive: item 16) · the Interlude is a real three-way
  choice · act pools are disjoint; their difficulty half is the encounter-sharpness wall ·
  bosses admit 3–4 answer axes — protect that in any future balance pass.
- **All PvP:** ghost server · matchmaking · ratings/leaderboards · PvP rewards · no-stakes Echo
  exhibitions (the snapshot seam may remain, but no feature work) · Displacement (Push/Pull/
  collisions) · spoils-of-war (historical ADR 0002) · sim-modeled projectile flight · aura
  ExcludeOwner option · morale/rout concept · ability crits · predetermined terrain (NEVER) ·
  account-scoped power (NEVER — fairness law).
- **Deliberately NOT proposed** (so the next session does not re-derive them): more heroes, more
  weapons, a second trinket family, multi-act expansion, difficulty ladders, PvP-adjacent
  anything, and any balance pass on hero kits — all capped by the content budget or forbidden by
  the content doctrine until playtest #1.

## Design backlog (unranked ammo for DESIGN chats — not scheduled)
- **Wide Banner** reads as "inner circle gets innate+crown" instead of "reach replaces"; ADR 0022
  makes the real design a one-liner (`SignaturePatch = Patch(radius: 1)`). Needs Jake's nod.
- **Content-fidelity leftovers** (2026-07-23 de-SIMPLIFY pass): Twist's crit-memory is a 30-tick
  Mark, not "since last cast" · War-Priest doesn't acquire mace mastery · Tower Shield has no base
  defensive stat · reforged-item resale forgets forge spend · returning to an implicit starter
  resets its temper · Company Standard expresses potency as an adjacent opening-Haste muster.
- **Inscription wave 3 ammo:** pool assignment across acts/bosses; which twelve shapes earn slots.
- **Balance/economy after the slice proof:** endless cycle reward/scaling depth (item 13's sequel) ·
  respec cost (free-for-now, revisit) · per-rank stat scaling.
- **Named-not-tuned outliers (guards against re-discovery):** `banneret` CHASSIS-DEAD (13 avg) ·
  four node pairs lopsided ≥25 · `shade:reaper+widowmaker` dead at 8–9%.

## Done — one line each; full detail + all older lines in `roadmap-done-archive.md`
- **2026-07-28** — Item 28, dead-view cleanup: reference-proved and deleted the unregistered
  ManagementView/ShopView/PlanningView + WarbandCard/CardRulesPopover stack and its three dead
  UXML trees (3,105 lines); moved the one live accent helper to DecisionCardPresentation.
  Client compile 0 errors, 522 tests pass, Unity refresh/console/build preflight clean.
- **2026-07-28** — One-command delivery: `make release` tests → rebuilds Unity DLLs → waits for
  sync → leases/drives the open Windows Editor → polls a request-scoped build → atomically ships
  and verifies the public launcher manifest; no competing batch-mode Editor.
- **2026-07-28** — Independent current-game assessment + three-month roadmap recut: combat payoff
  → 45–60m pacing/economy → encounter differentiation → one event → endless seam → cleanup;
  slice proof = Jake full run + one cold friend attempt with retained decision trails. Items
  25/26 merged into combat; Workbench/Hall generic polish feedback-gated.
- **2026-07-28** — Item 22, the board is 8×8 (ADR 0027): `BoardCols` 6→8, board dims in the
  content hash, semantic remap of all authored formations + probes + fixtures. 522 tests,
  scenarios round-trip, headless client compile PASS, baseline re-measured (content `28b51d86`):
  known outliers persist, shade's best build now flags DOMINANT (91%), slagworks a1 reach 100→46,
  and the a1 boss dropped to 2 answer axes → design backlog. Camera dialed against live editor
  renders (probe-shot loop): fov 34 · pitch 42 · yaw 6 · distance 1.6 · **new `aimBias` 0.2**
  (look-at pulled toward the near edge — high pitch makes center-aim waste the top while the
  front rank clips). Full board in frame on Waning Crown + overtime captures. Discovered: the
  world-space kill feed clips at the right edge on the wider board (anchor outside fov 34's
  budget) — folded into item 30.
- **2026-07-28** — Item 29, enemy board identity: `RoleId` on the wire (replay v9), seven authored
  role bodies replacing borrowed hero minis + per-role ground tells (artillery firing line, ritual
  clock); two new role fixtures; 522 tests, contact sheet byte-stable B/C/D.
- **2026-07-28** — Roadmap re-cut twice (playtesting = feedback, then actionable-only board).
- **2026-07-28** — Item 19, run telemetry: JSONL decision trail (every fight/purchase/tier), 5
  headless tests, fail-silent client hooks, key-gated site sink DEPLOYED + live-verified.
- **2026-07-28** — Item 9, the options screen: modal over the persistent layer (menu + fight +
  Esc), mixer sliders (params verified against the asset), reduced motion, battle speed 0.5–2×;
  full matrix 90/92 (the 2 = the known 2556×1317 artifact).
- **2026-07-28** — Item 24, Workbench dossier + armory-drawer redesign
  (`Design/workbench-dossier.md`): section roles, per-kind dossiers, footer drawer; 68/70 twice.
- **2026-07-28** — Item 5b, persistent Inscription tray + fight bridge; v1 world rail deleted
  (capture-proven); smoke 18/18.
- **2026-07-28** — Item 11, THE WANING: clock/warn/storm capture-verified at t=800/950;
  render-only; `overtime` fixture.
- **2026-07-27** — Item 10, the impact balloon: `impact.punchScale` 0.5 (worst tell +90% → +45%).
- **2026-07-27** — 2b, the muster rings (13 tests, capture-verified); shared-gold open → item 26.
- **2026-07-27** — Item 1c, the combat recap: sim-side fold (8 tests), pixel-verified.
- **2026-07-27** — Item 1e, responsive Workbench correction pass (82/82 + 65/65 matrices).
- **2026-07-27** — Item 1f, footer roster drag/drop + keyboard parity (239/239).
- **2026-07-27** — Item 23, audio steps 0–6: bake contract + tooling, SfxPlayer buses/duck,
  self-built mixer, AUDIO ON; volume screen shipped with item 9.
- **2026-07-27** — Item 17, The Stilled Bell: Silence honesty defect closed; PLAYED — "worked great."
- **2026-07-27** — Item 12, the deep enemy inspector — closed by item 21's combat card.
- **2026-07-27** — Item 21, the in-fight inspector (replay v7; no raw id reaches a card — CI).
- **2026-07-27** — Item 20, the passive layer's renderer (replay v6; baseline byte-identical).
- **2026-07-27** — Sim/render audit + cheap wins A/C/D (`camera.fov` · sigil hold · status quiet).
- **2026-07-27** — Muster spacing pass (6/6) · Responsive UI foundation (57/57) · Workbench
  overhaul R4–R6.
- **≤ 2026-07-26** — Everything earlier (bring-up → authored PvE → save/build/site): archive.
