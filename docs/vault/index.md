# warband vault — manifest

Read this first. One line per page; open only what's relevant. Update on every add/move.

## Design
- [Design/pitch.md](Design/pitch.md) — the current game in one page (v0.4, 2026-07-23):
  asymmetric PvE, broken-build fantasy, authored victory → endless. Start here.
- [Design/heroes.md](Design/heroes.md) — current universal hero anatomy: chassis, Mana,
  duplicate ranks/specs, Weapon+Trinket loadouts, party layer, and PvE commitment flow.
- [Design/roster.md](Design/roster.md) — current 8-hero first-playable PvE contracts:
  identity, forks, encounter contribution, dependencies, weaknesses, and playtest watches.
- [Design/weapons.md](Design/weapons.md) — current 11-weapon system: attack physics,
  universal equip, mastery riders, Worn→Honed→Relic temper, forge law, and fidelity seams.
- [Design/combat-grammar.md](Design/combat-grammar.md) — THE SOUL: clock + field, full effect vocabulary.
- [Design/pve-encounters.md](Design/pve-encounters.md) — partial PvE authoring law: the
  encounter is the boss, it rules and teaches its act, mechanics are disclosed, shared
  combat verbs work unless an authored passive explicitly says otherwise.
- [Design/sim-framework.md](Design/sim-framework.md) — sim architecture: content atom, cascade semantics, fields, determinism, metrics.
- [Design/render-contract.md](Design/render-contract.md) — how the client is guaranteed accurate: tick=100ms, bars set from absolutes, fold-as-view-model.

- [Design/sauce.md](Design/sauce.md) — PARKED: class-identity sauce noodling (two rounds cold);
  superseded by the "wears different hats" quality bar.
- [Design/theme.md](Design/theme.md) — THE LAST HOUR: dying multiverse + Tower frame, binding
  laws, mechanics↔fiction map, class/champion split, era-spread champion riffs.
- [Design/dives/cleric.md](Design/dives/cleric.md) — dive #1 SETTLED: bruiser-healer, censer +
  Sanctified Pyre, front-vs-back fork, 8 named builds; Sister Maren of the Waning Bell.
- [Design/dives/bulwark.md](Design/dives/bulwark.md) — dive #2 SETTLED: tank, Tower Shield +
  Shield Slam, Juggernaut (AoE stun) vs Warden (r3-4 Taunt+Silence, thorns); Brakka.
- [Design/dives/shade.md](Design/dives/shade.md) — dive #3 SETTLED: late-bloomer (fork at A),
  Reaper (crit-fish + Execute) vs Phantom (Phase); Null, the Redacted.
- [Design/dives/sharpshot.md](Design/dives/sharpshot.md) — dive #4 PROPOSED: ranged dps,
  Sniper artillery vs reworked Volleyer (3-line Rooting fan); muskets debut; awaiting Jake.

## Decisions
- [Decisions/0001-identity-and-anti-washout-contract.md](Decisions/0001-identity-and-anti-washout-contract.md) —
  historical original identity + the still-active anti-washout process guardrails.
- [Decisions/0002-run-structure-best-of-5.md](Decisions/0002-run-structure-best-of-5.md) —
  superseded historical best-of-5 spine; recoverable-loss and PvE risk ideas remain inputs.
- [Decisions/0003-combat-soul.md](Decisions/0003-combat-soul.md) — THE SOUL SENTENCE, Clock+Field
  pillars, flat maps/glyphs, displacement demoted, AI legibility.
- [Decisions/0004-sim-framework.md](Decisions/0004-sim-framework.md) — trigger atom, fields=auras,
  cascade semantics, determinism law, tag-change replay, metrics-as-folds.
- [Decisions/0005-loadout-composition.md](Decisions/0005-loadout-composition.md) — items/spec-trees as
  composed loadouts (sim never sees them), remaining sim gaps, PLACEHOLDER-content doctrine.
- [Decisions/0006-shop-and-economy.md](Decisions/0006-shop-and-economy.md) — shop after every node,
  roster 3→6 structure, flat rerolls, bench of 2, gold; slot-offer timing reopened by ADR 0016.
- [Decisions/0007-wager-mechanics.md](Decisions/0007-wager-mechanics.md) — 3 risk tiers per fight,
  per-kill payout + tier-scaled success bonus, no staked gold.
- [Decisions/0008-run-layer-architecture.md](Decisions/0008-run-layer-architecture.md) — run layer =
  pure host-agnostic lib; serializable state, ids-only content, stateless rng; hosting deferred.
- [Decisions/0009-shop-stock.md](Decisions/0009-shop-stock.md) — 3 hero + 2 item offers, per-offer
  freeze, dupe→rank-up forks, banners in rotation (team triggers), 50% sell-back.
- [Decisions/0010-theme-last-hour.md](Decisions/0010-theme-last-hour.md) — theme: The Last Hour
  frame, three binding laws, class/champion naming split; flavor-name candidates pending.
- [Decisions/0011-spec-tree-impact-model.md](Decisions/0011-spec-tree-impact-model.md) — dive
  template: 7 archetypes, ADD/SWAP/DEEPEN fork law, C/B/A/S ladder, variable fork timing.
- [Decisions/0012-weapon-access-model.md](Decisions/0012-weapon-access-model.md) — universal
  weapon equip, class specializations + weapon mastery riders, heal-weapons legal.
- [Decisions/0013-targeting-law.md](Decisions/0013-targeting-law.md) — sticky target; re-acquire
  on death / range-exit / untargetable / Taunt; melee subtlety flagged.
- [Decisions/0014-aura-and-adjacency-law.md](Decisions/0014-aura-and-adjacency-law.md) —
  permanent ally states use muster placement; casts and enemy spatial effects use live geometry.
- [Decisions/0015-weapon-system.md](Decisions/0015-weapon-system.md) — 11-category catalog,
  engine riders, temper tiers, mastery/Relic law, Tower forge.
- [Decisions/0016-pve-first-asymmetric-endless.md](Decisions/0016-pve-first-asymmetric-endless.md) —
  **current identity:** PvE is the product; asymmetric trials, system-breaking builds,
  authored victory into optional endless; PvP deferred.

## Projects
- [Projects/roadmap.md](Projects/roadmap.md) — **THE live board**: staged priorities, deferred list,
  open questions, done log. Sessions plan from here (CLAUDE.md Planning SOP).

*(Daily/ and Bugs/ get created when first needed.)*
