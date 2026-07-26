# ADR 0016 — PvE-first identity: asymmetric trials, broken builds, endless horizon

**Date:** 2026-07-23 · **Status:** accepted · **Participants:** Jake + Codex

> **2026-07-24 amendment:** ADR 0017 renames and expands the former Banner layer into
> unlimited persistent Hourstone **Inscriptions**, now a primary source of compounding
> player engines.

## Context
The original pitch made PvE the workshop and a same-act player ghost the exam. After
reviewing the current game, Guildrun, and other PvE autobattlers, Jake changed the center:
**PvP can come later. The game is PvE, its encounters are asymmetrical, and the fun is
breaking the game and its systems with the warband you build.**

This is a major identity amendment, but it narrows the critical path rather than opening a
new pillar: authored enemies replace the ghost-server dependency, while the combat, hero,
item, shop, run, replay, and Unity foundations survive.

## Decisions
1. **PvE is the product and the run outcome.** The player advances through authored PvE
   acts and bosses toward a clear victory. PvP is not required for the core loop, first
   playable, progression, balance target, or shipping path.
2. **The power fantasy is finding a build that feels illegal.** Specs, weapons,
   Inscriptions, placement, the Clock, and the Field should compound into engines that
   visibly pop off.
   A successful run may become wildly unfair in the player's favor. Balance protects a
   wide ecology of broken builds; it does not flatten every build toward parity.
3. **PvE encounters are deliberately asymmetrical.** Enemies are not constrained to
   player chassis, roster size, economy, ranks, loadouts, or mirrored formations. Authored
   enemy roles and bosses may use bespoke compositions, timing windows, phases, shapes,
   and rule packages expressed through the shared combat grammar. Every encounter must
   present a legible problem for build and placement; random hero kits with higher stats
   are scaffolding, not the target. New simulation machinery requires evidence from the
   vertical slice rather than asymmetry being used as a blank check for system expansion.
4. **The authored run has a win; endless is the horizon.** Beating the final PvE boss
   completes the run. The player may then continue with the same warband into escalating
   PvE until it finally breaks. Endless is the pressure test and victory lap for an
   outrageous engine, never a requirement for calling the run won.
5. **PvP is deferred and additive.** Deterministic snapshots keep the technical door open
   to optional, no-stakes Echo exhibitions later. No current design may depend on a ghost
   pool, record-based matchmaking, PvP rewards, ratings, or a server.
6. **No account-scoped power.** Meta progression may reveal content, difficulty, history,
   or cosmetics, but it may not make future warbands numerically stronger. The thing that
   improves across runs is the player's ability to recognize and assemble engines.
7. **The existing soul survives:** TFT-style emergent movement on hexes, placement as the
   only order, deep hero spec trees, universal weapons, the Clock + Field grammar, the
   Tower frame, deterministic pure-C# simulation, and replay-as-re-simulation.

## Balance law — let players break it
Intervene when a build:
- crowds out discovery by being the correct answer regardless of offers or encounters;
- bypasses every encounter problem with no positioning or adaptation;
- goes infinite accidentally, breaks determinism, stalls resolution, or destroys
  readability; or
- makes other promised build families nonfunctional.

Do **not** intervene merely because a build has an enormous number, wipes a fight, chains
several systems, or makes the player laugh at how unfair it became. Those are success
signals. PvE difficulty comes from presenting different problems and eventually outrunning
the engine, not from keeping the player near a 50% win rate.

## Consequences
- Supersedes ADR 0001's ghost-boss spine while preserving its anti-washout contract and
  all non-PvP identity decisions.
- Supersedes ADR 0002's best-of-five outcome, boss record, and ghost matchmaking. ADR
  0007's visible risk/reward fight tiers remain available for the PvE redesign.
- The content burden shifts from ghost infrastructure to a small authored enemy grammar,
  encounter families, bosses, events, and clear threat presentation.
- The current kits-as-monsters catalog remains useful scaffolding but is explicitly not
  representative PvE content.
- The next playable should prove **one complete PvE vertical slice** before expanding the
  act count, enemy catalog, difficulty ladder, or endless metagame.
- Ghost server and PvP work leave the live roadmap and move to Deferred.

## Open for the PvE vertical-slice design
- Standard-run act count and target length.
- Defeat/rewind/fail-forward rules before endless.
- How much enemy formation and intent is previewed before placement.
- How Stable / Fraying / Collapsing choices modify authored encounters.
- Endless cycle cadence, post-rank-S decisions, scoring, and scaling law.
- The minimum enemy-role and boss budget that can test the first eight heroes.
