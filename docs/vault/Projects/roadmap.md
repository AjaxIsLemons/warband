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
1. **DESIGN CAMPAIGN** (Jake, 2026-07-22: depth before any playtest talk; reordered
   hero-first same day. **THE BAR: every class can be brought, feels different run to
   run, and can wear different hats.**):
   a. **Theme/lore frame** — **DONE 2026-07-22** (ADR 0010: The Last Hour — dying
      multiverse + Tower, binding laws, class/champion split; Design/theme.md).
   b. **Spec-tree impact model** — **DONE 2026-07-22** (ADR 0011: archetype algebra,
      ADD/SWAP/DEEPEN fork law w/ exceptions, C/B/A/S ladder, variable fork timing,
      wardrobe test; roster audited — Shade/Sharpshot flagged).
   c. **Hero deep dives ×8** — one dedicated pass per class w/ Jake, from roster.md
      drafts + ADR 0011 template — **DESIGN, 2/8 done**: Cleric SETTLED (dive #1) ·
      Bulwark SETTLED (dive #2 — Taunt promoted to sim vocab) · Shade SETTLED (dive #3 —
      A-fork late-bloomer, Phase status, targeting law → ADR 0013) · Sharpshot PROPOSED
      (dive #4 — Volleyer reworked to Rooting fan) · then Pyro/Berserker/Phalanx/Banneret.
   d. **Weapons/itemization pass** — full categories + attack shapes, AFTER a dive or
      two proves what's needed — **DESIGN**.
   ~~Sauce hunt~~ — **PARKED** (Design/sauce.md; two rounds cold. May re-emerge from dives).
2. **Real content pass** — the 8 designed heroes + weapons through the composer + real
   IRunContent (supersedes "placeholder content pass"; waits on campaign) — blocked by 1.
3. **Archetype sweep harness** — round-robin win matrix + per-tier EV over RunHarness —
   SPEC'D (smoke already flags Greedy strictly dominant under placeholder monsters).
4. **Unity client bring-up** — SPEC'D pattern-wise (render from PlaybackState fold,
   render-contract.md). 🎯 Jake creates the Unity 6.3 project on Windows when ready.
5. **Ghost server + launcher** — SPEC'D (snapshot store + same-act matchmaking, client-sim
   hash-verified; copy Shoota's site/launcher/ship pipeline).
6. **Friends playtest #1** — still the milestone that ends arguments (ADR 0001), now
   explicitly AFTER the design campaign yields real content. No date until Jake calls it.

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
