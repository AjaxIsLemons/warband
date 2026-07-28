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
push it.** **Milestone: friends playtest #1 is mechanically unblocked (2026-07-28)** — build,
launcher, site, save/resume, options, and the telemetry sink are all live; Jake calls the date.

## Stages
- **DESIGN** — needs Jake's decision; don't build, propose (chat-sized ones resolve in a message).
- **SPEC'D** — designed, ready to implement autonomously. · **BUILD** — in progress; finish first.
- **DONE** — one line in Done with a date, detail to the archive. Machine verification (tests,
  captures, matrices) is part of BUILD, not a parking stage; in-motion feel goes to
  `Projects/play-pass.md`, never back onto the board.

## Now / Next (ordered — top item a session can start is "what's next"; Jake reorders at will)

**1. Item 13 — the endless seam** — **DESIGN (chat-sized), then a small build.** ADR 0016 identity
   + a content-budget line item. `RunPhase.Complete` is terminal today. Proposed shape (theme name
   **Beyond the Hour**): on Complete, offer CONTINUE — re-enter act 3's pool at escalating scale
   until a loss; score = beats survived. One nod decides.
**2. Item 15 — THE EVENT** — **DESIGN (tiny, chat-sized).** The budget funds ONE authored
   risk/reward event — a real gamble, distinct from a reward pick; nothing like it exists.
   (Guard: the Interlude IS a real three-way choice — corrected 2026-07-27, don't re-derive.)
**3. Item 28 — dead-view cleanup** — **DESIGN (one word), then mechanical.**
   ManagementView/ShopView/PlanningView (~2,300 lines) are unregistered since the view-table
   refactor; WarbandCard/CardRulesPopover only reachable from them. Delete (git remembers) or
   keep? The deletion itself is a compile-gated afternoon.
**4. Item 25 — a live passive has no on-body mark** — **SPEC'D (render-only), from items 20/21.**
   The fold carries `ActiveRules` and the hover card marks conditionals LIVE/idle, but nothing
   reads at a glance on the board. A `StatusIconRow`-shaped rim/mark while a conditional rule is
   live (and the `RuleChanged` pulse becoming a persistent rim) closes the passive renderer's last
   gap. Capture-verifiable.
**5. Item 26 — overlapping muster rings share one gold** — **SPEC'D (small render fix).** Whose
   ring is whose is unreadable when two placed musters overlap; per-owner accent
   (portrait-matched tint or pattern). Verify via `Warband/MCP/Capture Muster Rings`.
**6. Item 27 — Workbench polish batch** — **SPEC'D (item 24's deferred list).** Weapon-tier
   "augmented" marking on a non-hue channel · WHEN/THEN trigger anatomy for trinkets/inscriptions ·
   compact-card text-budget CI assertion · hero rank pips · paradox-inscription badge · rule-delta
   rows clipped at drawer-open · `MarketOfferCardModel.Qualifier` dead slot. Individually small;
   Workbench Full Matrix gates, captures read by eye.
**7. Item 1b — Hall polish, two buildable slices** — **SPEC'D.** Final Bind choreography · Rule
   Preview diagrams (`Design/hall-polish.md`). The device and audio/motion slices are
   feedback-gated → Deferred.

## Deferred (explicitly NOT now — don't resurrect without Jake; detail in the archive)
- **Gated/parked:** Inscription wave 3, 12→24 (gated: the twelve must stay legible in play) ·
  camera/framing pass (wants Jake's play notes; item 22 owns the shape half) · the tier value
  question + encounter sharpness (old items 4 + 18 — balance doctrine parks them until playtest
  data; **the telemetry sink now collects exactly that**) · risk-tier mutation of encounters ·
  1b's real-device + audio/motion slices · audio D2 re-bake vs regenerate (ears —
  `Projects/play-pass.md`).
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
- **The act-1 boss admits 2 answer axes on the 8-wide board, was 3** (ADR 0027's measured
  casualty — control fails the Last Oath at every placement tried, including the literal 6-wide
  coordinates; the width itself flipped it). The settled guard says 3–4. Options: re-author the
  Oath for the wide board · amend the guard for the teaching boss · wait for playtest data.
- **Wide Banner** reads as "inner circle gets innate+crown" instead of "reach replaces"; ADR 0022
  makes the real design a one-liner (`SignaturePatch = Patch(radius: 1)`). Needs Jake's nod.
- **Content-fidelity leftovers** (2026-07-23 de-SIMPLIFY pass): Twist's crit-memory is a 30-tick
  Mark, not "since last cast" · War-Priest doesn't acquire mace mastery · Tower Shield has no base
  defensive stat · reforged-item resale forgets forge spend · returning to an implicit starter
  resets its temper · Company Standard expresses potency as an adjacent opening-Haste muster.
- **Inscription wave 3 ammo:** pool assignment across acts/bosses; which twelve shapes earn slots.
- **Balance/economy (post-playtest-data):** endless cycle scaling + score (item 13's sequel) ·
  Sand/economy values · respec cost (free-for-now, revisit) · per-rank stat scaling.
- **Named-not-tuned outliers (guards against re-discovery):** `banneret` CHASSIS-DEAD (13 avg) ·
  four node pairs lopsided ≥25 · `shade:reaper+widowmaker` dead at 8–9% · The Long Range's ward
  never comes off for `control` · `reach` cannot clear the act-1 boss.

## Done — one line each; full detail + all older lines in `roadmap-done-archive.md`
- **2026-07-28** — Item 22, the board is 8×8 (ADR 0027): `BoardCols` 6→8, board dims in the
  content hash, semantic remap of all authored formations + probes + fixtures, camera defaults at
  the audit dial point (F1-dialable). 522 tests, scenarios round-trip, headless client compile
  PASS, baseline re-measured (content `28b51d86`): known outliers persist, shade's best build now
  flags DOMINANT (91%), slagworks a1 reach 100→46, and the a1 boss dropped to 2 answer axes →
  design backlog. Framing pixels unverified — the editor gets the payload via Syncthing.
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
