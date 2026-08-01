# Roadmap — THE live board

**This is the ONLY live priority list.** Sessions plan from here; see CLAUDE.md "Planning SOP".
Keep it honest: the board must match reality better than memory.

**ACTIONABLE-ONLY LAW (Jake, 2026-07-28, second re-cut):** every numbered item here is **DESIGN**
(drive the chat with concrete proposals, then build on his nod) or **BUILD/SPEC'D** (start now).
Nothing parks here — no status essays, no VERIFY limbo, no laws. Where the rest went:
- **Blocked work — what's waiting, on whom, and what unblocks it: `Projects/blocked.md`**
  (read it before answering "what's next" — an item there is NOT an answer to that question)
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

> **Board state, 2026-07-30 (late):** Items 13, 32 and 35 are complete. **Item 15 is SPEC'D — Jake
> approved the researched build order, so it is the top item a session can start and it needs no
> Play Mode.** Items 31 and 34 are both gated on Jake at the machine (he is away until Sat
> 2026-08-01 night); they and everything else waiting now live in **`Projects/blocked.md`** rather
> than cluttering this list.

**1. Item 15 — THE EVENT + the Inscription layer** — **SPEC'D (Jake approved the build order
   2026-07-30). Build autonomously; it is sim-side and needs no Play Mode.** Plan of record:
   `Design/events-and-inscriptions.md` (research pass, 5 axes, ~15 games). The Interlude becomes a
   data-driven event pool — no new nodes, no added run length. **Approved order: A + C + D + F,
   then I, then B, then E. G is rejected on evidence.**
   - **A.** `Duration` enum `{ Run, ThisFight }` on `InscriptionDef`, hashed into `ContentVersion`,
     + tray countdown + at-the-unit rendering (Law 6c). Start binary; `ThreeFights` only if play
     demands it. · **C.** Event pool schema + today's Treasury/Armory/Hourstone kept as the
     guaranteed floor, its values reading run state. · **D.** `followupAfterFights`, scheduled by
     fight count. · **F.** Status source-tagging. · **I.** Next-combat debuff as the downside
     currency. · **B.** 4–5 temporary Paradoxes (the family is currently ONE). · **E.** 12
     *branching* events.
   - Laws that must not be skipped: drawback by choice never RNG · duration ticks on combats only ·
     anything granted survives expiry · no information-removing, agency-removing or time-wasting
     Paradoxes · price in Sand, never a new currency · event selection a pure function of seed.
**2. Item 31 — run pacing + decision economy** — **DESIGN, evidence-first. BLOCKED — see
   `Projects/blocked.md` §1.** The first input is
   Jake's fresh baseline run and its telemetry. Measure total run time, fight time, Hall dwell,
   purchase/rank/roster timing, unused Sand, and repeated or no-op decisions. Tune the existing
   12-fight / 3-Interlude / 3-boss structure to 45–60 minutes while preserving visible build
   evolution. Remove/compress decisions that do not change the warband; add no currency, offer
   layer, hero, item, or progression system. **2026-07-29 evidence seam:** Jake's first trail was
   a 90-second Fraying loss in The Drop (1/3 enemies, one Revision, no purchases, Sand 4→4), so it
   exposed continuation pressure but not run pacing. Append-only phase-entry telemetry now
   separates planning, wager, deploy, fight, result, Revision evolution, Interlude, and boss
   reward dwell. **Opening cadence approved 2026-07-29:** Muster → starting Revision → Wager →
   Deployment → Fight; no pre-fight Workbench and starting Sand remains 4. Next input: one fresh
   baseline on the instrumented build.
**3. Item 34 — BLOCKED (`Projects/blocked.md` §1) — workbench as THE frame: fold muster + starting revision in; rank-up goes modal**
   — **BUILD (2026-07-28: Jake approved `workbench-frame/01-muster-state` +
   `02-rankup-modal`; condition: rank-up modal entrance is a BIG animated moment, "a
   dopamine shot", reduced-motion respected. Spec in
   `docs/ui-reviews/outbox/workbench-frame/implementation/spec.md`. 2026-07-29:
   implementation candidate is ready — 95/95 Workbench matrix, 534/534 headless tests,
   client compile clean, live Muster → first Revision → Wager seam green, and QHD/1080
   evidence packaged. Awaiting Jake's actual-Unity visual acceptance before DONE).**
   Muster becomes a workbench state (5-candidate offer in the market grid, dossier = inspection,
   rail = the 3 selection slots filling in, BEGIN RUN in the continue slot); starting revision
   becomes choice-scrim beat #0 (Interlude/RevisionUpgrade/BossReward already live there);
   RecruitView + MusterCard + RevisionDraftView retire. The unit rank-up 1-of-2 spec pick moves
   from the inline dossier ladder to its own hero-centric modal — the one interruptive chrome.
   Wager screen fold-in is explicitly out of scope (open question for later).
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
- **Spec offers are static in a randomised game** (surfaced by the 2026-07-30 research pass;
  detail in `Design/events-and-inscriptions.md` §4b). Measured: all **39** live `Offer()` rows are
  2 entries and `SpecChoices = 2`, so every rank-up shows the whole pool — perfect information,
  identical every run. This is the pattern that killed Underlords' talent trees ("every single
  hobgen take the same talents"; cut 8 weeks after shipping). Mitigating factors: warband is PvE,
  and a knowable tree may serve the system-breaking north star. **The draft machinery already
  exists and is dormant only because pools are size 2** — growing a row to 4 entries makes that
  rank-up a real seeded draft with zero code. Content decision, gated on playtest #1.
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
- **2026-07-30 — Item 35 Stage 2, rim dressing + the void backdrop:** `RimDressing.cs` plants an
  era's kit on the shard apron, entirely data-driven from `environment.rim.props` and sized by
  **`targetSize` in world units** (KayKit packs are authored at wildly different scales — a spear
  mesh is 0.031u, a banner 3.7u — so raw multipliers would need a magic constant per kit).
  `BuildVoidArt` hangs an authored backdrop billboard beyond the Tower. Three concepts generated on
  Jake's approval; he picked `sunken-strata`. Verified: compile PASS, 7/7 models resolve, console
  0/0, three kill-switches negative-controlled on two fixtures, Tower occlusion confirmed, captures
  reviewed across five probe rounds. **Two findings worth more than the art:** the skybox/cubemap
  route is structurally incapable of "nothing beneath you" (don't re-attempt in Stage 3), and the
  8×8 board fills the dialed frame so completely that a backdrop only reaches a 1202×136 top strip
  at ≤34/255 — **framing, not art, is the blocker on any future void work.** Job:
  `docs/art-reviews/outbox/shard-void-backdrop/`. Not seen in motion → `play-pass.md`.
  **KayKit Medieval Hexagon kit landed the same day and PROVED the systems claim: a pure
  `tuning.json` edit, zero code changed.** The rim now reads as an encampment (tents, barrels,
  crates, weapon racks, rocks). Get KayKit packs from **`github.com/KayKit-Game-Assets/*` branch
  zips**, not itch — itch's file endpoint needs an authenticated browser session, the GitHub mirror
  needs nothing (this is how Shoota's packs were obtained; CC0 confirmed in the pack's own LICENSE).
  Known limit: `rim.tint` is a colour MULTIPLY, so it darkens and cools but cannot DESATURATE — the
  hexagon atlas stays inherently warm terracotta. A real saturation knob needs a shader, deferred.
- **2026-07-30 — Item 32, encounter differentiation:** added the no-response / responsive /
  answer-axis competence ladder, removed the dead-Banneret control confound, and reshaped Gnawing
  Hour, Long Range, and Long Procession with existing roles only. All three now admit 3–4 answers
  with measured placement spread; Last Oath is back to three without a boss change. 559 tests,
  byte-stable baseline, 17 replay round-trips, actual-Unity formation captures, and console passed.
- **2026-07-30 — Item 13, Beyond the Hour:** the final Crown banks victory before an explicit
  Retire/Continue fork; endless repeats three Act 3 fights plus a scaling Crown, with persistent
  cycle/beat score. 556 tests, save/runtime/telemetry seams, QHD/1080 layout, red/green gates, and
  Unity console passed; Jake accepted R2.
- **2026-07-29 — Combat inspection rebuilt (Jake-driven, not a board item).** The in-fight hover
  tooltip and the world-space text nameplates are deleted; unit inspection is ONE pinned card
  (`InspectorPanel`, already shared with the Workbench dossier) floating tethered to its subject,
  never covering a fight that no longer pauses. Sections became SIGNATURE / WEAPON / PASSIVES /
  SPECS with the weapon owning the attack row; rank gained a C/B/A/S escalation badge; sand is now
  Hourstone-cost-only (signature mana went teal, TARGETS went Space blue); `FormatInline` colours
  magnitude+unit runs instead of forty common English words; Deploy's enemy CSV became selectable
  rows opening the same card. Full record, samples and spec:
  `docs/ui-reviews/outbox/combat-inspection/`. Verified: UI QA 19/19, 0 structural failures,
  `make test` 534/534, `make check-client` PASS — **which now also compiles `#if UNITY_EDITOR`
  blocks in runtime scripts**, a gap that let a stale fixture reach Unity green.
  In-fight card is unseen in motion → `Projects/play-pass.md`.

- **2026-07-29** — Item 30, combat payoff slice: event-driven KayKit Attack/Cast/Hit/Death,
  authored enemy motion language, camera/beat/hit-stop/ender choreography, combat SFX buses,
  persistent fold-driven conditional-passive LIVE mark, per-owner overlapping muster identity,
  and inward-growing measured killfeed. Normal/boss/swarm/ritual/Inscription captures reviewed;
  0.5×/1×/2× live contact states reviewed; 533 tests, client compile, SFX contract/density, and
  Unity console all green. Subjective ears/feel remain on `Projects/play-pass.md`.
- **2026-07-29** — PC VFX Lab: dedicated Unity scene + dockable embedded viewport; all 38
  recipes with draft/asset/C# resolution and particle/quad/light/curve editing; contextual tell
  tuning, automatic replay signature bookmarks for special systems, full dual-time Revision
  scrub, production/neutral/isolation environment A/B, mixed/raw audio audition, and optional
  scenario bookmarks. Explicit Apply protects recipe assets and `tuning.json`. Unity contract +
  mode smoke + UI construction PASS, console 0/0. Guide: `Projects/vfx-lab.md`.
- **2026-07-28** — Item 33, Workbench column refactor (Jake-approved `05-shopfront-obsidian` +
  header node map): 46px header w/ beat-track pips, market 3×2 + vertical reroll rail, 30%
  dossier column w/ PATH tier-up rows, offer tier strips (pre-specced recruits ready), 186px
  rail progression cards (Signature + W/T + B/A/S), armory floating rack, obsidian style.
  70/70 matrix ×3 + rank-up regression + by-eye vs approved sample; 4:3 keeps the slim rail
  card. Job: `docs/ui-reviews/outbox/workbench-refactor/`. Unseen in motion → play-pass.
- **2026-07-28** — Revisions (ADR 0028): one provisional watched timeline split per battle,
  proactive or held before terminal defeat; Borrowed Future + Recall to Formation with all six
  evolutions each; First Draft + blocking Interlude growth; whole-second timeline, target rings,
  flagship split/rewind/landing ceremony, authoritative change receipt, dedicated Revision VFX/SFX
  lane, native URP dual-time fracture compositor, Reduced Motion, save + telemetry. 533 tests, headless
  client compile PASS, First Draft + held-Hour captures reviewed, real Play Mode
  open→target→commit branch PASS, 22-frame live-board fracture matrix reviewed, Unity console
  0 warnings/errors, build preflight invoked.
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
  budget) — closed by item 30 on 2026-07-29.
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
- **2026-07-27** — 2b, the muster rings (13 tests, capture-verified); overlapping-owner identity
  completed by item 30 on 2026-07-29.
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
