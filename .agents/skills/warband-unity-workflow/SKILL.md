---
name: warband-unity-workflow
description: Work safely in Warband's Unity 6.3 client. Use for any Warband Unity task involving client C# code, UI, input, graphics, scenes, prefabs, assets, editor tooling, tests, Unity MCP operations, or play-mode verification, especially when choosing the relevant Unity guide and protecting serialized Unity files.
---

# Warband Unity Workflow — Codex router

1. Read `.claude/skills/warband-unity-workflow/SKILL.md` completely and follow it as the shared
   Claude/Codex source of truth.
2. Read its skill map at `.claude/skills/warband-unity-workflow/references/claude-skill-map.md`,
   then read every selected `.claude/skills/<name>/SKILL.md` completely.
3. Take the `unity-warband` lease with `agent-lock` before any Unity Editor operation — Codex must
   call it itself; only Claude sessions are gated automatically. Never spin-retry a denial.
4. Never hand-edit `.meta`, `.unity`, `.prefab`, or serialized `.asset` files; route through the
   Editor as the source guide describes.

If `.claude/skills` is absent, say the project-specific source guide was unavailable rather than
improvising a Unity workflow.
