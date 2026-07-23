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
2. **Real content pass** — **DONE 2026-07-23 (161 tests incl. fidelity pass).**
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
   bonus. Leftover judgment calls → Open questions below.
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
Content-fidelity leftovers (2026-07-23, from the de-SIMPLIFY pass): **Wide Banner**
reads as "inner circle gets innate+crown" instead of "reach replaces" — proposed as
the actual design, needs Jake's nod · **sig-override composition wart**: an S
signature override drops an A override's texture (Sarissa+DeepThrust keeps length,
loses escalation) — last-wins is ADR 0005 discipline; fix would need additive sig
mods · **Twist's crit-memory** is a 30-tick Mark, not "since last cast" (cast-event
ordering) ·
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
