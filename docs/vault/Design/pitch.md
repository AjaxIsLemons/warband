# warband — pitch (v0.4, 2026-07-23)

**One line:** A PvE roguelike autobattler where you deep-spec an era-spanning warband on a
hex battlefield, assemble interactions that feel illegal, conquer authored asymmetrical
encounters — then take the same broken build into endless until time finally kills it.

**The fantasy:** You are not assembling a fair team. You are finding a loophole in the Tower:
a healer turned immortal frontline, a field engine that consumes the board, a firing line
that accelerates until the enemy never acts. Every shop, rank-up fork, weapon, Inscription,
and placement is another part of the machine. The payoff is watching the machine come online.

**The identity:** Guildrun-depth hero transformation + TFT-style hex positioning, built for
authored PvE rather than competitive parity. Enemies are free to be monsters: uneven unit
counts, bespoke roles, timing windows, phases, and rule packages the player cannot equip.
The player answers by bending the shared Clock + Field systems harder than the encounter does.

## The run — authored victory, endless horizon (ADR 0016)
- A run crosses authored **PvE acts** made of fights, events, rewards, and a shop after every
  node. Each act ends with a real PvE boss built to test a different property of the warband.
- Fight tiers remain the confidence wager: take a more dangerous visible version of an
  encounter for better build fuel. Exact tier and defeat rules belong to the vertical-slice
  design, not this pitch.
- The standard run has a clear final boss and a real victory. After winning, the player may
  leave with the win or continue with the same warband into escalating **endless PvE** until
  defeated.
- Endless exists to answer, *how far can this ridiculous engine go?* It must preserve
  meaningful build/placement decisions after ordinary rank and roster progression cap out.
- PvP is deferred. Deterministic snapshots may later support optional, no-stakes Echo
  exhibitions, but ghosts, matchmaking, rating, and servers do not shape the core game.

## PvE encounter law
- Encounters are **asymmetrical and authored**, not mirrored player warbands and not random
  hero kits with larger numbers.
- Every enemy family presents a legible problem: hold a swarm, reach artillery, interrupt a
  ritual, survive a clock spike, protect against a dive, or reclaim a painted battlefield.
- Difficulty adds new pressure before it adds raw stats: composition, formation, timing,
  affixes, phases, and interactions.
- The shared grammar stays small. Enemies combine it differently and may package it into
  bespoke units and bosses; bespoke one-off simulation rules remain a last resort.

## Combat
**The soul (ADR 0003): "A war for time and ground: your build bends the clocks and paints
the battlefield — placement is the only order you give."**
- Auto-resolves on hexes: **4 rows × 6 columns per side**. Warband scales **3 units → cap 6**
  across the run (availability timing is reopened for the PvE structure). Flat board, zero
  predetermined terrain — all ground effects are unit-cast **glyphs** (fire fields, healing
  ground, summoned walls). Full grammar: combat-grammar.md.
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
- **Weapons/trinkets** tie builds together across heroes and are the churny re-tool axis; heroes
  are sticky and never bricked (circuit's lesson).
- Persistent Hourstone Inscriptions and cross-system riders turn several individually
  understandable pieces into a team engine. The balance target is many discoverable ways
  to become outrageously strong, not a narrow band of equal outcomes.

## Tech
- Unity 6.3 LTS, Guildrun/TFT isometric camera, 2.5D programmer art until the loop is proven.
- Combat sim = **pure C# assembly**, headless-tested on homeserv, zero Unity references;
  Unity is a renderer on top. Sim comes first.
- Run rules are a second pure C# assembly. Core PvE is local and needs no server.
- Deterministic snapshots preserve optional future Echo/leaderboard possibilities without
  making network work part of the first playable.
- Shoota's launcher copied for one-click friend installs.

## How this one ships (ADR 0016 + ADR 0001's anti-washout contract)
1. First playable = **one complete authored PvE vertical slice**: the current eight-hero /
   twelve-item build kit, a tiny enemy grammar, several encounters, one boss, shops and
   placement, plus the cheapest possible continue-until-defeat seam. Programmer art.
   Content budget is a hard cap.
2. Nothing is "LOCKED" until it's been **played**. Reviews propose; playtests decide.
3. Do not build multiple acts, a large monster roster, a difficulty ladder, a full endless
   metagame, or PvP before the vertical slice proves the loop.
4. **Friends playtest #1 outranks every new system.** All three
   previous autobattlers died within sight of this exact step.
