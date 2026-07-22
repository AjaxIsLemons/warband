# warband — pitch (v0.2, 2026-07-22)

**One line:** A run-based autobattler where you deep-spec a small warband of heroes on a hex
battlefield through acts of PvE tinkering — and **every act ends with a boss fight against a
real player's warband**, placed blind.

**The fantasy:** You're running a warband. Every shop visit, every rank-up fork, every weapon
you strap on a hero serves one question: *will this team beat the team a real human built?*

**The innovation:** Guildrun proved deep hero-building sells (demo Very Positive, week one) —
pure PvE, no humans to beat. TFT has the board and the stakes but shallow per-unit building.
The Bazaar has "build to beat ghosts" but no board. Nobody has all three:
**hex positioning + Guildrun-depth hero speccing + async ghost PvP.** Our twist on the formula:
keep Guildrun's Slay-the-Spire-style beat (a timeline of fights/shops/events in acts, bosses
close acts) — but **the act boss is another player**. PvE is where you tinker; the human is
the exam.

## The run — best of 5 (decided 2026-07-22, ADR 0002)
- ~20–25 min. **5 acts × ~4 PvE nodes** (monster fight / event / shop), each act closed by a
  **PvP ghost boss** drawn from boards other players snapshotted at the *same act*
  (structural fairness — circuit's same-round pools, reused wholesale).
- **The run is a best-of-5 against humanity.** Runs complete all 5 acts; your boss record is the
  outcome: 3+ wins = victory, 5-0 = flawless. No lives — an early boss loss is a scoreboard
  setback, never a death spiral.
- **PvE is the wager layer:** fights are chosen at a risk tier — wager your strength for more
  reward. Knowing when your build spikes (and cashing in) is a core skill. A PvE loss costs the
  wager and tempo, never the run.
- **Anti-snowball laws:** difficulty and rewards anchor to act number, never W/L (autobattle's
  rule); PvP results touch the scoreboard, never your power (spoils-of-war deferred past v1);
  ghost pools keyed to act + record, so ~50% boss win-rate is structural.
- Blind placement both sides. **No scouting, ever** (circuit's ghost-pool-pollution argument).
- Your act-end board enters the ghost pool regardless of result; synthetic-fill bots seed thin
  pools (cold start solved).

## Combat
- Auto-resolves on hexes: **4 rows × 6 columns per side**. Warband scales **2 units → cap 6**
  across the run.
- **TFT-model movement, deliberately simple:** units path toward targets by fixed rules; speed,
  range, and placement do the rest. Tanks hold fronts, AoE respects board shapes, assassins
  reach backlines via abilities/passives — never via player micro. All skill lives between fights.
- Escalating overtime clock guarantees resolution (beltwars finding: deterministic mirrors
  stalemate forever without a designed decider).
- Deterministic event→trigger→effect sim (circuit's vocabulary, ported to C#), seeded,
  order-independent; replay = re-simulation.

## Hero building (the pillar)
- Each hero has a **spec tree**. Rank-ups offer forks; forks change the hat the hero wears —
  a healer can become a frontline anchor on the right path (Guildrun's multiclass model).
- **Weapons/armor** tie builds together across heroes and are the churny re-tool axis; heroes
  are sticky and never bricked (circuit's lesson).

## Tech
- Unity 6.3 LTS, Guildrun/TFT isometric camera, 2.5D programmer art until the loop is proven.
- Combat sim = **pure C# assembly**, headless-tested on homeserv, zero Unity references;
  Unity is a renderer on top. Sim comes first.
- Server = snapshot store + same-act matchmaking; clients simulate, hash-verified. Same lib can
  move server-side later.
- Shoota's launcher copied for one-click friend installs.

## How this one ships (anti-washout contract — see ADR 0001)
1. First playable = **8 heroes × ~2 spec forks, 5 acts, solo vs. bot-ghosts, programmer art.**
   Content budget is a hard cap.
2. Nothing is "LOCKED" until it's been **played**. Reviews propose; playtests decide.
3. The identity above is **settled** — reopened only by playtest evidence.
4. **Friends playtest #1 is a dated milestone that outranks every new system.** All three
   previous autobattlers died within sight of this exact step.
