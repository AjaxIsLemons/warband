# ADR 0020 — Distinct run states and mechanical UI language

**Date:** 2026-07-25 · **Status:** accepted and implemented · **Participants:** Jake + Codex

## Context

Direct play invalidated ADR 0019's board-first workspace. Keeping the encounter, placement,
Market, Armory, Hourstone, and inspector visible around one board made every job smaller and made
combat feel like another management tab. The same play pass found that names such as “heavy
strike” and “violent rush” were flavor, not rules: they did not say who is affected, by how
much, when, or for how long.

The game lives in menus and replay viewing. Those two surfaces therefore need distinct jobs,
large type, exact rules, and deliberate transitions rather than one maximally persistent layout.

## Decisions

1. **The run loop is Management Hall → Wager → Deployment → Combat → Management Hall.**
   Interludes and boss rewards are blocking choices inside the Hall. Bosses skip Wager and enter
   Deployment directly.
2. **The board exists only for Deployment and Combat.** Management and Wager park the renderer
   empty. Market, Armory, Hourstone, and future management systems are full-screen Hall tabs.
3. **Wager precedes encounter disclosure.** Stable / Fraying / Collapsing show qualitative
   pressure and exact victory Sand. Enemy identities and placement remain hidden until the wager
   is locked. Deployment then reveals the exact formation before placement is committed.
4. **Cards use one mechanical grammar:**
   - `BASIC ATTACK` states target behavior, value, cadence, reach, and crit when non-zero.
   - `SIGNATURE · AT N MANA` states explicit targets, shape/range, values, state changes, and
     duration.
   - `PASSIVE · TRIGGER` states the exact trigger before the effect.
   - Keyword notes define closed vocabulary such as Burn, Stun, Regeneration, Haste, Riposte,
     and Leap. Flavor never substitutes for a rule.
5. **Summary and disclosure have different sizes, not different facts.** Draft and Hall cards
   compare the exact rules. Hover/focus opens a runtime rules popover. Clicking a Hall card opens
   a large modal dossier with the same composed stats and authored rules.
6. **Combat is a clean playback surface.** A small hint and Skip affordance are the only fixed
   shell chrome. Hover gives an enlarged live-state readout; clicking a battlefield unit opens a
   large live combat card with current HP, Shield, Mana, statuses, basic attack, Signature,
   Passive, and keyword definitions. Covering combat with this requested modal is intentional.
7. **Typography is tested at the capture resolution.** Fixed-height cards must reserve enough
   space for the longest authored rule and optional fifth stat. A clean compiler is not layout
   verification; Game View captures are the acceptance evidence.

## Consequences

- Supersedes ADR 0019 decision 3's fully disclosed pressure choice and decision 5's persistent
  board-first workspace. ADR 0019's run shape, Sand economy, stock, capacity, and content budget
  remain accepted.
- The Management Hall is the expansion seam for future meta systems; combat never needs to make
  room for them.
- Encounter authors can rely on staged disclosure: stakes first, exact formation before
  deployment, outcome only after simulation.
- Presentation metadata owns names, triggers, exact display rules, keyword notes, icons, and
  portraits. Composed `UnitDef` / `PlaybackUnit` still owns all displayed combat numbers.
