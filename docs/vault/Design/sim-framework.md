# Sim framework — architecture v1.0 (2026-07-22)

Synthesis of: circuit engine teardown (deployed, 58k-fight-proven), SabberStone (HS sim),
Slay the Spire's action queue, Super Auto Pets ordering rules, Riot determinism series,
plus warband's own requirements (glyph fields, reaction triggers, banners). ADR 0004.

## 1. The content atom
Everything — hero passives, fork riders, banners, item effects, glyph rules, reaction
tech — compiles to one shape:

    Trigger { On: EventKind, When: Cond[], Do: Effect[] }
    Effect  { Kind, Select: Selector, Shape, Magnitude, params }

Five orthogonal axes (event × condition × selector × shape × effect), pure data + typed
params, interpreted at runtime. **No code-per-hero.** One named escape hatch
(`CustomHandler("name")`) for the genuinely weird — circuit shipped exactly one in a
full game; treat >3 as a smell.

**Circuit's four expressiveness walls, fixed from day one:**
1. **Condition negation + OR.** `Cond{Kind, Not}` with AND-list semantics plus a
   `CondAnyOf(group)`. Standard `ExcludeSelf` source filter (circuit's Opportunist
   self-chain bug is the cautionary tale).
2. **Conditional stat rules.** Declarative "while <predicate>: ±stat" evaluated at stat
   read time (Berserker's low-HP attack-speed ramp is DATA, not custom code).
3. **Enemy-relative spatial selectors first-class:** NearestEnemy, EnemiesWithin(N, of X),
   EventSource ("the leaper"), HexesAround — on a hex board these are the bread and butter.
4. **Fields are first-class world entities** (circuit's weakest area, our pillar — see §3).

## 2. Time & resolution semantics
Two nested layers, each with an explicit ordering law:

- **Tick layer (the clocks):** frozen-read → decide → apply, as already built. Attacks,
  moves, casts decided from start-of-tick state — mirror fairness is structural
  (mutual-KO Draw test stays a permanent guardrail).
- **Cascade layer (the triggers):** during apply, mutations emit events onto a FIFO
  queue; drain pops front, matches triggers **by unit-id ascending, then declaration
  order** (team-scope triggers after unit triggers), applies effects **immediately**
  (visible to subsequent triggers), children append to tail. Bounds: **cascade depth ≤ 8**
  + **drain budget ≤ 50k events** (circuit's proven numbers), both deterministic.
- **Death phase (Hearthstone lesson):** deaths are detected when the queue settles,
  batched, death events queued, re-drained; repeat to fixpoint; then corpses leave play.
  No mid-cascade removal paradoxes.
- Ordering is neutral id-order, NOT SAP's attack-stat order — considered and skipped
  (hidden strategy lever we don't want; predictability wins).

## 3. Fields & auras — ONE spatial system
A **Field** = { hexes (static area OR attached-to-unit radius), rule, duration, source,
allegiance }. Glyphs are static fields; **auras are fields attached to a moving unit**
(Banneret = radius-1 field). Unified mechanism:
- **Per-tick deterministic sweep** (id-ordered): pulse rules (DoT, heal), stat rules
  ("Haste while standing on") feed the read-time stat system, entry/exit emits
  `FieldEntered`/`FieldLeft` events for triggers.
- Polling is CORRECT at our scale (≤12 units, ≤48 hexes/side, ≤ a dozen fields):
  SabberStone abandoned polling because of thousands of tag-checks; we don't have that
  problem, and the sweep is trivially deterministic. Don't build event-driven aura
  machinery we don't need.
- Field-aware pathing (ADR 0003's step scoring) reads the same field data.
- **Projectile-path interaction (added round 8, Jake):** any attack over ≥2 hexes traces
  the deterministic hex line; fields crossing the interior may **block** (walls — shot
  wasted, no mana), **amplify** (flat bonus), or **attach riders** (arrows ignite over
  fire). Resolution stays instant (render-contract v1) — the PATH is the gameplay.
  Each field acts once per shot regardless of hexes crossed. Built + tested 2026-07-22.

## 4. Determinism law (C# bans, CI-enforced)
- Integer/fixed-point only (FP = 1000); NO float/double in resolution.
- One PCG32 per battle, save/restorable; consumption sites named and few.
- BANNED in sim core: `System.Random`, `DateTime`, `Guid`, float math,
  `Dictionary`/`HashSet` **iteration** (lookup fine), LINQ ordering without explicit keys.
- Every tie-break explicit and documented at the site. All iteration by ascending id.
- Cross-machine golden hashes (the PCG32 reference test pattern) + per-tick BattleHash.

## 5. Replay = tag-change event log (circuit's keystone, ported)
Every mutating event carries **delta AND absolute post-state** (HP/Shield/Mana after).
The client is a pure viewer that SETS bars to carried absolutes — never accumulates —
so a dropped frame is stale for one beat, never permanent drift. Guardrail test ported
verbatim in spirit: **fold the log like a client and compare to sim state every tick**
(`TestLogReconstructsState`). No silent mutations — anything touching a bar emits.

## 6. Metrics & data (first-class, Jake's requirement)
The sim emits ONLY the event log; every stat is a fold over it. Attribution honesty is
a sim responsibility: every damage/heal/status event carries `Source`, `Cause`
(Attack/Ability/DoT/Field/Storm) and `RootSource` (the trigger chain's origin — so a
banner that started a cascade gets the credit).
- **FightStats fold (shared library** — used by tests, harness, later the AAR screen):
  per-unit damage by cause, tanked (hp vs shield), healing, disruption seconds
  (stun/silence/disarm/root uptime), casts + first-cast tick, kills/deaths,
  **field metrics** (hexes painted, field uptime, damage/heal from fields, time-on-field),
  movement (steps, leaps), storm damage (excluded from contribution — src-less).
- **Conservation test:** credit-by-source == credit-by-target, every fight (circuit's
  attribution test — keeps the stats honest forever).
- **Harness 1 (with content):** archetype round-robin matrix over N seeds → win% matrix,
  flag dominant (>85%) / dead (<30%) — counter-web health.
- **Harness 2 (with run layer):** scripted spend-policy full-run sim — MUST model the
  real economy (autobattle's metasim blind spot: it validated combat while ignoring
  affordability). Deterministic per-run seeds from one root.
- Output = JSON/CSV; dashboards render later.

## 6b. Bonus scopes (added 2026-07-22, Jake round 7)
Three scopes, three mechanisms — never mixed:
- **Combat-scoped** = statuses. Born and die with the battle (ramps, buffs, debuffs).
- **Run-scoped** = `RunBonus` + `ProgressionFold`: after each fight the run layer folds
  the battle log (kill participation via damage attribution, damage thresholds, more
  metrics as needed) and bakes earned **permanent statuses** into how the hero spawns in
  later fights. The sim never mutates run state — growth is *derived from the log*, so
  replays and ghost verification stay honest, and the metrics fold and the progression
  system are the same machinery.
- **Account-scoped**: none, ever (circuit's fairness law — no power from meta-progression).

## 7. Deliberately not building
MtG-style replacement effects & layer system; SabberStone's enchantment entities;
interrupt/priority windows; generic scripting/DSL; event-driven aura invalidation.
Bounded vocabulary + escape hatch beats a rules engine (MtG is the cautionary maximum).

## 8. Build order (refactor of current Battle.cs)
1. Event queue + drain + depth/breadth bounds; move attack/cast emission onto it.
2. Trigger tables + condition/selector/shape interpreters; statuses as data.
3. Stat-rule read-time system (replaces raw Def reads).
4. Fields (static + attached) + sweep + entry/exit events + field-aware pathing.
5. Tag-change log + log-reconstruction guardrail + FightStats fold + conservation test.
6. Content tables for the 8-hero roster; archetype sweep harness.
