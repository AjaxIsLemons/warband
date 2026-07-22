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
1. **Run layer (headless)** — the big one; sub-items in build order:
   a. Act/node skeleton — SPEC'D (ADR 0002: 5 acts × ~4 nodes, act-boss ghost fight,
      best-of-5 record scoring). Headless state machine, no UI.
   b. Shop & economy — **DESIGN** (mechanics decided: duplicates→ranks C/B/A/S, one
      currency, roster 2→6; numbers + reroll rules + roster-slot growth undecided).
   c. Wager mechanics — **DESIGN** (ADR 0002 open: what's wagered, reward curve).
   d. Ghost snapshots + bot-ghost generation — SPEC'D (format: act + record + composed
      loadouts + placement; circuit's same-round pool model).
   e. Full-run headless harness — SPEC'D once a–d exist (metasim lesson: model the economy).
2. **Placeholder content pass** — 8 roster heroes as ChassisDef+starter weapons via the
   composer; sample armies swap to real composition path — SPEC'D (placeholder doctrine!).
3. **Archetype sweep harness** — round-robin win matrix over seeds, flag >85%/<30% — SPEC'D.
4. **Unity client bring-up** — SPEC'D pattern-wise (render from PlaybackState fold,
   render-contract.md). 🎯 Jake creates the Unity 6.3 project on Windows (Shoota pipeline)
   when we're ready for it. Board render → placement drag → replay viewer → run screens.
5. **Ghost server + launcher** — SPEC'D (snapshot store + same-act matchmaking, client-sim
   hash-verified; copy Shoota's site/launcher/ship pipeline).
6. **Hero deep dives ×8** — **DESIGN**, one dedicated pass per hero with Jake. Not before
   the loop is playable.
7. **Weapons/items design pass** — **DESIGN** (categories, attack shapes land here,
   itemization economy).
8. **Friends playtest #1** — the milestone that outranks everything (ADR 0001). Date it
   as soon as the run layer stands.

## First-playable content budget (hard cap — ADR 0001)
8 heroes × ~2 forks (placeholder kits OK) · ~12 items · 5 acts × ~4 nodes · small monster
roster (reuse hero kits) · programmer art, no sound · bot-ghosts only.

## Deferred (explicitly NOT now — don't resurrect without Jake)
Displacement (Push/Pull/collisions) · attack shapes (→ item 7) · spoils-of-war (ADR 0002)
· sim-modeled projectile flight ("dodge by movement" lever, render-contract) · aura
ExcludeOwner option · morale/rout concept · ability crits · predetermined terrain (NEVER)
· account-scoped power (NEVER — fairness law).

## Open design questions (ammo for DESIGN sessions)
Wager shape/reward curve · roster-slot growth (bought vs act-granted) · currency name +
economy numbers · respec cost (free-for-now decided, revisit) · act-boss reward beyond
record · symmetric-vs-enemies-only damage fields (feel it in sim) · per-rank stat scaling
· bench/reserve in v1 · run length target validation (~20-25 min).

## Done
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
