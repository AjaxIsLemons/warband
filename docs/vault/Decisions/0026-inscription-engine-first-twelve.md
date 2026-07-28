# ADR 0026 — Inscription engine: root guard, first twelve, Living Inscription

**Date:** 2026-07-27 · **Status:** accepted · **Participants:** Jake + Claude

## Context

ADR 0017 accepted the Hourstone/Inscription system and named three gates that had to be
settled before the catalog could grow past the five migrated seeds: the per-root activation
guard's representation, the Bearer of the Mark replacement, and the first twelve effect
contracts. Item 5a reached the top of the board with those gates open.

A code audit found the layer smaller than the board implied: the trigger grammar
(22 conditions, 11 selectors, 11 effect verbs), rule identity (`RuleId`), the
`TriggerFired`/`RuleChanged` replay events, hybrid acquisition, and the Hourstone Table all
exist. What is genuinely missing: the root guard itself (`Battle.FireIfMatch` fires on every
match, bounded only by cascade depth and drain budget), team-level event counters, a hook
for an Inscription-activated trigger, the combat badge rail, and the catalog past five.

Notably, item 17's Silence Inscription needs **no** new selector machinery in reaction
shape ("when an enemy casts, Silence the caster") — the board's build note assumed a
Mana-aware selector, which is only needed for the preemptive opener shape.

## Decisions (all four Jake, 2026-07-27)

1. **Per-root guard applies to Inscriptions only.** Team rules are compiled with
   once-per-root-event semantics by default; hero kit passives keep today's behavior and
   their existing ad-hoc `IsRootEvent` guards. Authored repeaters remain a legal future
   exception but none exist in the first twelve. Effects are always child events, so
   once-per-root also subsumes the self-wake ban for this wave.
2. **Living Inscription replaces Bearer of the Mark, as proposed.** "When an Inscription
   activates, Vespera gains Mana, at most once per root event." It scales with activation,
   not collection size, so it cannot make Banneret compulsory. `DoublesBanners` and the
   run-layer doubling path (including the ghost-fairness copy) are deleted. Requires the
   drain to let `TriggerFired` match triggers — a deliberate amendment to the
   "presentation-only" law that event was born under; the once-per-root guard is what makes
   it safe.
3. **Full code rename now.** `BannerDef` → `InscriptionDef` and related names migrate in
   this pass, before the catalog triples against the old vocabulary. The `RunState.Banners`
   serialized field keeps its name for save compatibility behind the existing
   `Inscriptions` alias.
4. **The drafted seven are the working set** (numbers placeholder per content doctrine;
   names and shapes tune in review once the badge rail shows them live). With the five
   renamed seeds, the first twelve:

   | # | Working name | Family / engine | Rule |
   |---|---|---|---|
   | 1 | The First Bell *(firstblood)* | death → tempo | Enemy falls → warband Haste |
   | 2 | The Closed Gate *(leapstun)* | reaction | Enemy Leaps → Stun on landing |
   | 3 | Cinder Law *(brand)* | Burn foundation | Allied attacks apply Burn |
   | 4 | Bronze Testament *(bronzehour)* | opener | Warband begins Shielded |
   | 5 | Chorus of Hours *(chorus)* | cast → Shield | Ally casts → gains Shield |
   | 6 | Tithe of Hours | heal → Mana | Ally healed → gains Mana |
   | 7 | The Wound Clock | damage → Mana | Ally damaged → gains Mana |
   | 8 | The Third Chime | team counter | Every 3rd allied cast → brief warband Haste |
   | 9 | The Ash Bequest | Burn payoff | Burning enemy dies → remaining Burn spreads |
   | 10 | The Stilled Bell | Silence (item 17) | Enemy casts → Silenced briefly |
   | 11 | Shoulder to Shoulder | formation opener | Mustered beside an ally → AttackUp |
   | 12 | The Bloodless Hour | **Paradox, boss/event pool only** | Healing grants Shield instead of HP |

## Consequences

- New sim machinery: root-event identity in the drain + once-per-root guard · a team event
  counter condition with counter progress on the wire (badge pips; the client may not count
  itself) · an adjacent-to-ally selector filter (#11) · a heal→Shield team rewrite flag
  (#12) · `TriggerFired` matching for Living Inscription.
- The guard is mechanical: `make baseline` is the gate for whether existing fights move.
- Renames move `ContentVersion` (def names are hashed), invalidating in-flight saves —
  accepted at dev stage.
- The combat badge rail (ADR 0017 §6, presentation contract in `Design/inscriptions.md`)
  is the client half of this item and remains to build.
- Item 17 closes as catalog entry #10; `roster.md`'s false Silence claim gets fixed.
- Pool assignment ships as data with the catalog: Paradoxes to boss/exceptional pools,
  the rest Workshop + one-from-three. Prices and cadence stay run-layer tuning (parked).
- Not in scope: the 24 catalog (wave 3), a battlefield Hourstone, Inscription removal or
  sale, economy Inscriptions.
