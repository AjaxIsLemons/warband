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
