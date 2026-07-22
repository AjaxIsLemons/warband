# warband — hex autobattler (working title)

Fourth autobattler attempt, started 2026-07-22. Guildrun-inspired PvE-act spine where **the act
boss is another player's ghost warband** (async PvP, blind placement, no scouting). Deep hero
spec trees. Unity client, pure C# deterministic sim.

## Source of truth
- `docs/vault/index.md` — vault manifest (Design/, Decisions/, Projects/; Daily/, Bugs/ as needed).
- Read `Design/pitch.md` + `Decisions/0001-identity-and-anti-washout-contract.md` before any
  non-trivial design or build work.

## ⚠ Content doctrine (Jake, 2026-07-22 — read before touching content)
**ALL current content is PLACEHOLDER.** Roster kits, numbers, status magnitudes, the viewer's
sample armies — they exist to exercise SYSTEMS, not to be tuned. We are in the
build-systems-and-see-them-work phase. Do NOT balance-tune placeholder numbers, do NOT treat
`Design/roster.md` as final, do NOT start per-hero deep dives — each hero gets a dedicated
design pass later, with Jake. Sim-level mechanics and data shapes (ADR 0004/0005) are the
real work product right now.

## Invariants (ADR 0001 — do not relitigate without a playtest)
- Identity is settled: PvE acts + act-closing ghost PvP · TFT-style emergent movement on hexes ·
  spec-tree heroes · Unity + pure C# sim.
- The sim is a pure C# assembly: deterministic, seeded, order-independent, headless-testable,
  zero `UnityEngine` references. Replay = re-simulation from (seed, snapshots, contentVersion).
- First-playable content budget is a hard cap (`Projects/first-playable.md`), not a floor.
- Nothing is "LOCKED" until it has been **played**. Friends playtest #1 outranks any new system.

## Stack
- **Sim + tests:** .NET class library, built and tested headless on homeserv — this comes first,
  before any Unity work.
- **Client:** Unity 6.3 LTS (URP), Guildrun/TFT isometric camera, 2.5D programmer art until the
  loop is proven. Remote dev via the Shoota pipeline (Syncthing + Unity MCP over SSH) —
  runbook: `~/brain/server/unity-remote-dev.md`. Unity project does not exist yet.
- **Server (later):** snapshot store + same-act ghost matchmaking; clients simulate,
  results hash-verified. Door open to server-side sim later (same C# lib).
- **Distribution:** copy Shoota's launcher for one-click friend installs.

## Vault upkeep
Light-touch: ADRs for MAJOR decisions only; a daily note when a session meaningfully moves the
project. Don't over-document.
