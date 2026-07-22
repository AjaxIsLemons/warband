# First playable — budget, build order, open questions

**Goal:** a rough-but-complete run (3 acts, act-boss ghost fights vs. bots) a friend can play
via the launcher. Fun-signal before depth. Target date: set when the sim skeleton lands.

## Content budget (hard cap)
- **8 heroes**, ~2 spec forks each (16 endpoint identities), 1 rank-up choice per act.
- **~12 weapons/armor pieces** total.
- **3 acts × ~5 nodes**; small monster roster (reuse hero kits as monsters where possible).
- Programmer art, no sound. One board.

## Build order (sim first, Unity later)
1. **C# sim skeleton** (.NET lib + tests, homeserv, no Unity): hex coords + neighbors,
   deterministic tick (read-frozen / buffer / apply), simple pathing + targeting rules,
   attack/HP, overtime clock. Golden tests: determinism, order-independence, mirror-resolves.
2. **Event→trigger→effect vocabulary** ported from circuit (statuses, AoE shapes, triggers) —
   smallest set that expresses the 8-hero budget.
3. **Run layer headless**: nodes, gold/XP economy, rank-up forks, snapshot format, bot-ghost
   generation. Headless full-run harness (autobattle metasim lesson: model the economy).
4. **Unity client**: 🎯 Jake creates the Unity 6.3 project on Windows (Shoota pipeline);
   board render, placement drag, replay viewer, run screens. Ugly is fine.
5. **Launcher copy + friends playtest #1.**

## Open questions (round 3+)
- **Stakes economy (the innovation space):** what does losing cost — PvE loss vs. act-boss
  loss? Lives ("banners")? Guild-HP? Is the PvE fight optional risk/reward (Bazaar) or
  mandatory node? Decide via play, not paper.
- **Act-boss reward:** what does beating the human ghost grant — rating, plunder from their
  board, run-score? What happens on act-boss loss (run over / lose a banner / refight new ghost)?
- Rank-up pacing: XP-driven or act-driven?
- Bench/reserve: exists in first playable or later?
- Working title → shipping name (later; don't bikeshed).
