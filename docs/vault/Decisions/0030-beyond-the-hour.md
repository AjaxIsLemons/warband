# ADR 0030 — Beyond the Hour: bank victory, then pressure the same warband

**Date:** 2026-07-30 · **Status:** accepted · **Participants:** Jake + Codex

## Context

ADR 0016 makes the game's ending two-part: defeating the authored final boss is a real victory,
then the player may carry that exact warband into an optional endless pressure test. The first
playable has all of the content machinery needed for a crude seam but no transition into it:
Act 3's encounter pool already serves later acts, normal encounters already scale from the act,
and the Waning Crown already gains 25% Health and Power per act beyond Act 3.

This slice proves the promise without spending the deferred endless-metagame budget.

## Decisions

1. **The authored victory is banked immediately.** Defeating the Act 3 Waning Crown records a
   standard-run victory before the player makes any continuation decision. Nothing in endless can
   erase that win.
2. **The Workbench presents the fork.** The existing blocking-choice scrim offers
   **RETIRE WITH VICTORY** and **CONTINUE BEYOND THE HOUR**. This is another post-round choice in
   the one-frame/one-card system, not a new terminal screen.
3. **One endless cycle is three fights and the Waning Crown.** It reuses Act 3's three-family node
   pool. The standard Interlude beat is skipped: endless adds no reward choice or progression
   surface.
4. **Existing systems continue; no endless economy is added.** Normal fights pay the existing
   Act 3 Sand rewards and refresh the existing Workbench market. There is no endless currency,
   special reward pool, boss reward, hero rank, item tier, or Inscription layer.
5. **Existing scaling is the only v1 pressure law.** Cycle one behaves as virtual Act 4, cycle two
   as virtual Act 5, and so on. Node encounters use the existing act curve; the Waning Crown uses
   the existing +25% per post-Act-3 step. No adaptive scaling or new tuning surface is introduced.
6. **The initial score is structural, not competitive.** The run records completed endless
   cycles and combat beats won in the current cycle. Presentation reads `CYCLE N · BEAT M`; no
   leaderboard, rating, reward conversion, or account record is implied.
7. **Continuation is first-class run state.** Banked victory, continuation choice, current cycle,
   current beat, and score survive save/resume and appear in the append-only telemetry trail.
   Endless defeat produces a victory-preserved conclusion rather than a standard defeat.

## State and extension contract

| New requirement | Inherits free | Must be authored |
|---|---|---|
| Later endless cycle | Act 3 encounter pool, deterministic salt, node scaling, Crown scaling | Nothing |
| New Act 3 encounter family | Eligible for endless through `PoolFor(act >= 3)` | The encounter itself |
| Save/resume | Explicit ids-only `RunState` and `RunSave` format | New continuation fields |
| Telemetry | Existing run id and append-only JSONL header | Choice/cycle/score fields |
| Player presentation | Workbench blocking-choice cards and terminal receipt | Two choice cards and endless copy |

## Explicitly deferred

Endless-only rewards or economy · post-rank-S decisions · special bosses or encounter pool ·
mutators · bespoke scoring formula · leaderboard · metagame · account progression · detailed
cycle balance. Those require evidence from the slice proof.

