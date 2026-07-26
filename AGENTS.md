# Warband Codex guidance

Read `CLAUDE.md` before non-trivial work. It defines the project sources of truth, planning rules,
simulation invariants, current content doctrine, and remote Unity workflow.

For any work under `client/` or any task that changes or verifies the Unity client, use the
`warband-unity-workflow` skill. It routes to the detailed Unity guides already maintained in
`.claude/skills` so Claude and Codex follow the same technical guidance.

Edit source-controlled text (`.cs`, `.asmdef`, `.uxml`, `.uss`, shaders, tests, docs) locally with
`apply_patch`, then verify it through the Windows Unity Editor over `unity-mcp`. Never manually
edit Unity-managed `.meta`, `.unity`, `.prefab`, or serialized `.asset` files. Use
`Unity_RunCommand` editor C# or an Editor script and let Unity save them. The Codex hook enforces
this for patch edits.

After Unity client changes, use the smallest proportional verification loop: ensure sync if
needed, refresh/import, read console logs, then run the relevant Edit Mode, Play Mode, or capture
check. If MCP is unavailable, report that editor verification was not run.

## Unity is shared — take the lease first (REQUIRED for Codex)

Jake runs Claude and Codex at the same time. The Unity Editor is a singleton (one scene, one
play-mode state, one loaded replay, one console buffer), so two agents driving it corrupt each
other. Claude sessions are gated automatically by a `PreToolUse` hook; **Codex has no such hook and
must call the lease CLI itself**:

```bash
agent-lock acquire unity-warband --owner "codex:$$" --note "what you are doing" || exit 1
#   ... all your unity-mcp work, as ONE coarse hold ...
agent-lock release unity-warband --owner "codex:$$"
```

- Acquire is re-entrant — re-run it to refresh the 5-minute lease during long work.
- `agent-lock status` shows the current holder and why. Never spin-retry a busy resource: sim,
  tests, sweep, content, and vault work are completely uncontended, so go do those instead.
- `unity-warband` and `unity-shoota` are separate leases; the two games never block each other.
- **Clear the Unity console before acting inside a hold** — `GetConsoleLogs` has no session
  attribution, so you will otherwise read the other agent's errors and chase a ghost.
- The lease does **not** protect files. When another session is live, agree on file/subsystem
  ownership first; `ReplayPlayer.cs` has already been edited by two sessions at once.

Full rationale and the failure modes it does/doesn't fix: `~/brain/meta/agent-locks.md`.
