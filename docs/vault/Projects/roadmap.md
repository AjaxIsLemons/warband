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
🎯 **GOAL (Jake, 2026-07-23): a playable PoC.** Path: mechanics build → real content →
outlier sanity sweep → Unity bring-up. Sweep bar (Jake's words): *rule out CRAZY
outliers or broken things — NOT a detailed balance pass.*
1. **Sim mechanics build queue** — **SPEC'D → BUILD next** (specs live in the 8 dive
   docs + ADRs 0013/0014/0015; consolidated backlog in Daily/2026-07-23). Suggested
   order (votes/dependencies): next-N-swings charge status (4 votes) → Burn decay
   engine → Taunt + directional Counter + Phase (the big statuses) → Lifesteal /
   overheal→Shield / cheat-death / killer attribution (3 votes) → StatRule inputs
   (target-distance, self-HP) + condition riders (target-below-HP, taunted-by-owner,
   range-exact, no-enemy-within-R, engaged-with-ally, Nth-swing) → shapes & riders
   (cleave, line lunge, multi-target swing, double-swing, ring finisher, overkill-carry,
   forced-crit) → mana-grant + Company muster set + aura-granted Counter + Leap-landing
   trigger + ally-targeting autos + field-spawn-on-death/permanence/consume-stacks →
   composer tier param + rider gating + reforge shop action (ADR 0015).
2. **Real content pass** — all 8 settled kits (full C/B/A/S webs from the dive docs) +
   11 weapons × 3 tiers + starter banner set through composer + real IRunContent —
   SPEC'D, unblocked once 1 lands.
3. **Outlier sanity sweep** — archetype round-robin win matrix + per-tier EV over
   RunHarness with REAL content (harness exists, 109 tests; extend to matchup matrix).
   Flag only: strict dominance, degenerate/infinite loops, conservation violations,
   never-picked nodes. Explicitly NOT tuning numbers.
4. **Unity client bring-up → the playable PoC** — render from PlaybackState fold
   (render-contract.md). 🎯 Jake creates the Unity 6.3 project on Windows when ready;
   terminal viewer covers fight-watching until then.
5. **Ghost server + launcher** — SPEC'D (snapshot store + same-act matchmaking, client-sim
   hash-verified; copy Shoota's site/launcher/ship pipeline).
6. **Friends playtest #1** — still the milestone that ends arguments (ADR 0001), after
   the PoC. No date until Jake calls it.

## First-playable content budget (hard cap — ADR 0001)
8 heroes × ~2 forks (placeholder kits OK) · ~12 items · 5 acts × ~4 nodes · small monster
roster (reuse hero kits) · programmer art, no sound · bot-ghosts only.

## Deferred (explicitly NOT now — don't resurrect without Jake)
Displacement (Push/Pull/collisions) · spoils-of-war (ADR 0002)
· sim-modeled projectile flight ("dodge by movement" lever, render-contract) · aura
ExcludeOwner option · morale/rout concept · ability crits · predetermined terrain (NEVER)
· account-scoped power (NEVER — fairness law).

## Open design questions (ammo for DESIGN sessions)
Currency/tier final names (gold + Safe/Even/Greedy are placeholders until theme/lore) ·
economy numbers (placeholder until sweep/playtest) · respec cost (free-for-now decided,
revisit) · act-boss reward beyond record · symmetric-vs-enemies-only damage fields (feel it
in sim) · per-rank stat scaling · run length target validation (~20-25 min).

## Done
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
