---
name: warband-unity-workflow
description: Work safely in Warband's Unity 6.3 client. Use for any Warband Unity task involving client C# code, UI, input, graphics, scenes, prefabs, assets, editor tooling, tests, Unity MCP operations, or play-mode verification, especially when choosing the relevant Unity guide and protecting serialized Unity files.
---

# Warband Unity Workflow

Use the existing `.claude/skills` collection as the detailed Unity knowledge base. Keep it as the
single source of truth so Claude-side guide updates are immediately available to Codex.

## Workflow

1. From the Warband repository root, read `CLAUDE.md` before touching Unity code or assets.
2. For non-trivial design or implementation, follow the source-of-truth reading requirements in
   `CLAUDE.md`.
3. Read [the skill map](references/claude-skill-map.md), then read every selected
   `.claude/skills/<name>/SKILL.md` completely.
4. Read only the direct reference files required by the selected source skill.
5. Preserve the pure deterministic simulation boundary: `Warband.Sim` must not reference
   `UnityEngine`; the client renders folded replay state.

## Choose the editing lane

For source-controlled text such as `.cs`, `.asmdef`, `.uxml`, `.uss`, shaders, tests, and docs,
edit the homeserv copy with `apply_patch`. Let Syncthing deliver those changes to Windows.

Never manually edit `.meta`, `.unity`, `.prefab`, or serialized `.asset` files. Use Unity MCP
`Unity_RunCommand` editor C# or add an Editor script, invoke it through MCP, and let Unity save the
result. Treat a serialized-asset hook block as a route correction, not a guard to bypass.

## Verify through Unity

When Unity MCP is available:

1. Confirm source sync when timing is uncertain (`make sync-status`).
2. Refresh/import through `Unity_RunCommand` when needed.
3. Read `Unity_GetConsoleLogs` after compilation.
4. Run the smallest relevant Edit Mode, Play Mode, or visual capture check.

If Unity or MCP is unavailable, perform static checks, report exactly what remains unverified, and
do not claim editor or play-mode validation.

If this skill is invoked outside Warband or `.claude/skills` is absent, use the closest available
Unity guidance and state that the project-specific source guide was unavailable.
