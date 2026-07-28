---
name: warband-ui-review
description: Generate review-first UI concepts for Warband through paired inbox/outbox jobs. Use when Jake asks Codex for UI samples, mockups, alternate visual directions, screenshot-based redesigns, or a UI review before implementation. Prefer native image generation and require explicit approval of a named sample before changing the Unity client.
---

# Warband UI Review — Codex router

1. Read `.claude/skills/warband-ui-review/SKILL.md` completely and follow it as the shared source
   of truth with Claude.
2. Prefer Codex's native `$imagegen` lane for samples. Use the image generation skill before the
   first image call and inspect every local reference image first.
3. Keep the exact inbox/outbox job name and approval status defined by the shared skill.
4. If the approved work proceeds into `client/`, also use `$warband-unity-workflow` and follow its
   Unity lease and verification rules.

Do not bypass the shared review gate.
