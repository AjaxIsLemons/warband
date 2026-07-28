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
The eight hero kits, weapons, and five legacy Banner triggers are mechanically authored
**first-playable candidates**, but their numbers and interactions remain unplayed. The
Banner triggers are now only the seed set for the Hourstone/Inscription layer (ADR 0017).
Do not conduct a detailed balance pass before the interactive playtest; fix only broken
machinery, unreadable behavior, or crazy outliers that invalidate the test.

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

## Sharing Unity with other sessions (Jake runs Claude + Codex in parallel)
The Unity Editor is a **singleton**: one scene, one play-mode state, one loaded replay, one console
buffer. Two agents driving it corrupt each other's state and misread each other's errors. Protocol
and rationale: `~/brain/meta/agent-locks.md`.
- **Claude sessions are gated automatically** — a `PreToolUse` hook takes a lease on `unity-warband`
  before any `mcp__unity-mcp__*` call and denies it when another live session holds it. Nothing to
  remember. **Codex must call `agent-lock` itself** (same CLI, see the brain page).
- The lock is **per project**, so warband and Shoota never block each other.
- If you're denied: do NOT spin-retry. Sim/tests/sweep/content/vault work is completely
  uncontended — go do that, `agent-lock status` before returning, tell Jake if truly blocked.
- **Clear the Unity console before acting inside a hold.** `GetConsoleLogs` has no session
  attribution, so otherwise you will read someone else's errors (or your own stale mid-edit
  compile) and chase a ghost.
- **Never steal Windows desktop focus.** MCP work must not foreground, raise, maximize, or focus
  the Unity Editor, Game View, or a Player window. Keep the Game View on **Play Unfocused** and use
  semantic inspection, deterministic stepping, camera/RenderTexture captures, or other
  background-safe checks. If verification truly requires a focused Game View, stop and ask Jake
  immediately before doing it.
- The lock does **not** protect files. If another session is live, agree on file/subsystem ownership
  first — `ReplayPlayer.cs` has already been edited by two sessions at once.

## Vault upkeep
Light-touch: ADRs for MAJOR decisions only; a daily note when a session meaningfully moves the
project. Don't over-document.
