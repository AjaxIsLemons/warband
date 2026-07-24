# warband — hex autobattler (working title)

Fourth autobattler attempt, started 2026-07-22. PvE roguelike autobattler about building an
era-spanning warband into a system-breaking engine, then testing it against authored
asymmetrical encounters and an endless horizon. Deep hero spec trees, hex placement, Unity
client, pure C# deterministic sim. PvP is deferred.

## Source of truth
- `docs/vault/index.md` — vault manifest (Design/, Decisions/, Projects/; Daily/, Bugs/ as needed).
- Read `Design/pitch.md` + `Decisions/0016-pve-first-asymmetric-endless.md` before any
  non-trivial design or build work. ADR 0001 still owns the anti-washout process contract.

## Planning SOP — "ok, what's next?"
**`docs/vault/Projects/roadmap.md` is the ONLY live priority list.** When Jake opens a
session and asks what's next, the answer comes from the board, never from memory:
1. Read the roadmap. "What's next" = the topmost unblocked item, respecting its stage.
2. **Stages:** `DESIGN` = needs a Jake conversation — don't build it, drive the discussion
   with concrete proposals and record outcomes as ADRs, then flip it to SPEC'D.
   `SPEC'D` = build autonomously. `BUILD` = in progress — finish before starting anything
   new. `VERIFY` = needs tests/verification/polish. `DONE` = move to Done with a date.
3. **Update the board as you work** — stage flips when state changes, finished work moves
   to Done, new discoveries become items (Deferred/Open-questions if not now). Session end
   with a stale board = failed session hygiene.
4. Never invent roadmap priorities without Jake; the Deferred list is "explicitly not now."

## ⚠ Content doctrine (amended 2026-07-23 — read before touching content)
The eight hero kits, weapons, and banners are mechanically authored **first-playable
candidates**, but their numbers and interactions remain unplayed. Do not conduct a detailed
balance pass before the interactive playtest; fix only broken machinery, unreadable behavior,
or crazy outliers that invalidate the test.

Current PvE enemies and events are **scaffolding**: random player kits with scaled stats are
not representative content. Real PvE uses small, authored, asymmetrical encounter families
that pose legible build/placement problems. The fun target is the player discovering
compounding interactions that feel like they break the game. Do not flatten a spectacular
engine merely because it is powerful; see ADR 0016's balance law.

## Invariants (ADR 0016 + ADR 0001 process contract)
- Identity: authored asymmetrical PvE acts and bosses → clear run victory → optional endless
  continuation · TFT-style emergent movement on hexes · spec-tree heroes · system-breaking
  player builds · Unity + pure C# sim.
- PvP, ghost matchmaking, ratings, and PvP rewards are deferred. No core design or first
  playable depends on a server.
- The sim is a pure C# assembly: deterministic, seeded, order-independent, headless-testable,
  zero `UnityEngine` references. Replay = re-simulation from (seed, snapshots, contentVersion).
- First-playable content budget is a hard cap (roadmap), not a floor.
- Nothing is "LOCKED" until it has been **played**. Friends playtest #1 outranks any new system.

## Stack
- **Sim + tests:** .NET class library, built and tested headless on homeserv — this comes first,
  before any Unity work.
- **Client:** Unity 6.3 LTS (URP), Guildrun/TFT isometric camera, 2.5D programmer art until the
  loop is proven. Project and remote pipeline are live; detailed state is on the roadmap.
- **Server (deferred):** not required for core PvE or friends playtest. Deterministic snapshots
  leave the door open to later Echo exhibitions and leaderboards without shaping today's game.
- **Distribution:** copy Shoota's launcher for one-click friend installs.

## Vault upkeep
Light-touch: ADRs for MAJOR decisions only; a daily note when a session meaningfully moves the
project. Don't over-document.
