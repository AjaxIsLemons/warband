# ADR 0001 — Identity decisions + anti-washout contract

**Date:** 2026-07-22 · **Status:** accepted · **Participants:** Jake + Claude (round-1/2 pitch Q&A)

## Context
This is the fourth autobattler attempt. Deep postmortems of the first three (2026-07-22, saved in
Claude's cross-project memory) found none died for technical reasons — all built excellent
deterministic sims and died the same three ways:
- **beltwars:** design relitigation — core concept redesigned twice in 4 days, specs "LOCKED" by
  multi-agent review without ever being played, quiet sandbox restart 11 days later.
- **battle/circuit:** finished + deployed the entire engine/server, died the week content
  authoring began (last commit 2026-07-13; pivot to arena 07-17).
- **autobattle:** self-diagnosed "design-expansion loop"; froze 2026-06-10 one step short of
  friends playtest #1. External fun-signal never arrived.

## Identity decisions (settled — reopened only by playtest evidence)
1. **Spine:** mostly-PvE acts (Guildrun/Slay-the-Spire beat: fight/event/shop nodes), with
   **async ghost PvP as the act-closing boss**. Bazaar's soul: everything serves beating real
   players' teams. Blind placement, same-act ghost pools, no scouting.
2. **Movement:** TFT-style emergent pathing on hexes — positioning is the input (tanks front,
   AoE shapes, assassin backline access via kit). NOT a tactical-movement game; no player micro.
3. **Hero building is the depth pillar:** spec trees with role-changing forks (multiclass);
   weapons/armor as the cross-hero re-tool axis. Heroes sticky, never bricked.
4. **Board/scale:** 4×6 hexes per side; warband grows 2 → 6 units across a run.
5. **Tech:** Unity 6.3 LTS client (isometric, 2.5D first) over a **pure C# deterministic sim
   assembly** built headless-first on homeserv. Ghost server = snapshot store + matchmaking,
   client-side simulation, hash-verified (door open to server-side sim later — same lib).
   Distribution via a copy of Shoota's launcher.

## The contract (process guardrails, from the postmortems)
- **Hard content budget** for first playable (8 heroes × ~2 forks, 3 acts, bot-ghosts,
  programmer art). Cap, not floor. — *counter: battle's death-by-content-grind.*
- **Nothing "LOCKED" until played.** Design reviews propose; playtests decide. — *counter:
  beltwars' paper-locks.*
- **No reopening settled identity** without a playtest saying so. — *counter: beltwars'
  relitigation loop.*
- **Friends playtest #1 is a dated milestone that outranks any new system.** A session tempted
  to open a new pillar closes toward the playtest instead. — *counter: autobattle's
  design-expansion loop.*

## Consequences
- Sim library starts before any Unity work; Unity project creation on Windows is a later,
  explicit step (Shoota pipeline).
- Balance harness must model the run economy, not just combat (autobattle metasim caveat).
- Known-hard design debts to face early, from prior findings: overtime/tiebreak clock,
  AoE-vs-swarm balance on a board, ranged-behind-melee gridlock, ghost-difficulty vs.
  player-growth tuning.
