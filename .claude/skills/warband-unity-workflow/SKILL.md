---
name: warband-unity-workflow
description: Work safely in Warband's Unity 6.3 client. Use for any Warband Unity task involving client C# code, UI, input, graphics, scenes, prefabs, assets, editor tooling, tests, Unity MCP operations, or play-mode verification, especially when choosing the relevant Unity guide and protecting serialized Unity files.
---

# Warband Unity Workflow

The `.claude/skills` collection is the detailed Unity knowledge base and the single source of
truth; the `.agents` copy of this skill is a router into it so Codex reads the same guidance.

## Workflow

1. From the Warband repository root, read `CLAUDE.md` before touching Unity code or assets.
2. For non-trivial design or implementation, follow the source-of-truth reading requirements in
   `CLAUDE.md`.
3. Read [the skill map](references/claude-skill-map.md), then read every selected
   `.claude/skills/<name>/SKILL.md` completely.
4. Read only the direct reference files required by the selected source skill.
5. Preserve the pure deterministic simulation boundary: `Warband.Sim` must not reference
   `UnityEngine`; the client renders folded replay state.

For player-facing UI work, always read `docs/vault/Design/ui-responsive-contract.md`. If the work
implements or evaluates an approved UI review, also read
`.claude/skills/warband-ui-review/references/implementation-fidelity.md`.

## Choose the editing lane

For source-controlled text such as `.cs`, `.asmdef`, `.uxml`, `.uss`, shaders, tests, and docs,
edit the homeserv copy with your normal file tools. Let Syncthing deliver those changes to Windows.

Never manually edit `.meta`, `.unity`, `.prefab`, or serialized `.asset` files. Use Unity MCP
`Unity_RunCommand` editor C# or add an Editor script, invoke it through MCP, and let Unity save the
result. Treat a serialized-asset hook block as a route correction, not a guard to bypass.

Before any scripted whole-file rewrite, `cp` the file first. A scripted stylesheet cleanup has
permanently destroyed another session's authored work in this repo, and a careless
`write_text(...)` destroyed a 568-line vault doc. Surgical edits over scripts on tracked files.

## Compile before you sync

Run `make check-client` before syncing conclusions or taking the Unity lock — it compiles the
client's C# headlessly against reference assemblies and catches API errors before the Syncthing
round-trip. Nothing is reported built until it is green **and** the console is clean after the sync.

Blind spot: `check-client` excludes editor scripts by construction, so it cannot catch an Editor
script referencing an `internal` type in `Assembly-CSharp`. That class of error only appears on
assembly reload — and it stops Unity reloading at all, which blocks every capture-based check.
Check for foreign compile errors before planning a capture.

## Verify through Unity

When Unity MCP is available:

1. Check source sync when timing is uncertain (`make sync-status`). A healthy sync process is not
   proof that a particular edit reached Windows; before a critical capture, verify an
   edit-specific marker through the Editor, console, or an Editor-side file read.
2. Refresh/import through `Unity_RunCommand` when needed.
3. Clear the console before acting inside a lock hold — `GetConsoleLogs` has no session
   attribution, so otherwise you read someone else's errors or your own stale compile.
4. Read `Unity_GetConsoleLogs` after compilation.
5. Run the smallest relevant Edit Mode, Play Mode, or visual capture check.

A gate is evidence only if it could have failed. Before trusting a new check, break the thing on
purpose and confirm it goes red. A capture showing a suspiciously *default* value (`0:00`, zero,
empty) is the harness lying before it is the feature broken — confirm every piece of state the view
reads was actually advanced, not just the one you set.

For UI, keep structural and visual claims separate:

- layout-contract tests prove measured geometry invariants;
- matched captures prove visual fidelity only when fixture, state, and physical resolution match;
- ordinary visual acceptance targets QHD `2560×1440` with the `1600×900` logical panel, plus a
  `1920×1080` containment smoke;
- the broad viewport matrix is required only for shared shell, breakpoint, or explicitly
  target-specific responsive changes.

Do not call an approved-concept implementation visually done until Jake accepts the actual Unity
evidence through the UI review workflow.

If Unity or MCP is unavailable, perform static checks, report exactly what remains unverified, and
do not claim editor or play-mode validation. Writing "unverified" does not license shipping — if a
surface might be unusable, say unusable.

## Share the editor

The Editor is a singleton and lease-gated on `unity-warband` (Claude sessions are gated
automatically; see `CLAUDE.md`). Never spin-retry a denied lease — sim, tests, content, and vault
work are completely uncontended. Batch every Unity-needing step into one queued list so Jake
unlocks once instead of babysitting handoffs. Never steal Windows desktop focus.

If this skill is invoked outside Warband or `.claude/skills` is absent, use the closest available
Unity guidance and state that the project-specific source guide was unavailable.
