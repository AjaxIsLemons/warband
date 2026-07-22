# ADR 0002 — Run structure: best-of-5 acts, PvE wagering, anti-snowball laws

**Date:** 2026-07-22 · **Status:** accepted (round-3 pitch Q&A) · **Participants:** Jake + Claude

## Context
Round 3 had to answer "what does losing cost?" Jake's explicit worry: PvP wins snowballing —
"in a good system, you are W/L 50% of the time; losing the first [boss] feels like too big of a
punishment otherwise."

## Decisions
1. **Best-of-5 run:** 5 acts, each closed by a ghost-boss PvP fight. Runs always complete all
   5 acts; the boss record is the outcome (3+ = victory, 5-0 = flawless, record feeds
   rating/leaderboard). No lives system — a boss loss is recoverable by construction.
2. **PvE = the wager layer:** PvE fights are chosen at a risk tier; higher wager, better reward.
   Reading your own power spike and cashing in is a named core skill. A PvE loss costs the wager
   and tempo — never run-ending.
3. **Anti-snowball laws (design invariants):**
   - Difficulty and rewards anchor to **act number, never W/L** (autobattle's match-number rule).
   - **PvP results touch the scoreboard, never your power.** Consequence: spoils-of-war
     (taking an item from a beaten ghost) is deferred past v1 — it couples PvP wins to power.
   - Ghost pools keyed to **act + current record** (synthetic-fill when thin), so ~50% boss
     win-rate is structural rather than tuned.

## Consequences
- Run-layer sim needs wager-tier node generation; exact tiers/costs are a first-playable
  design question (play it, don't paper it).
- Snapshot format must carry act + record for pool keying.
- Supersedes pitch v0.2's "3 acts, stakes OPEN" strawman.
