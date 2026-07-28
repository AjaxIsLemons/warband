---
name: warband-art-pipeline
description: Generate, curate, process, import, and verify Warband placeholder art through shared Claude/Codex inbox-outbox jobs. Use for portraits, class or talent icons, weapon art and icons, spell/VFX source images, decals, masks, flipbooks, materials, textures, and environment concepts. Prefer native image generation and require explicit approval before importing assets into Unity.
---

# Warband Art Pipeline — Codex router

1. Read `.claude/skills/warband-art-pipeline/SKILL.md` completely and follow it as the shared
   Claude/Codex source of truth.
2. Read and use `$imagegen` before generating or editing any raster asset.
3. Process the named job, or scan for `WAITING_FOR_CODEX` jobs when Jake asks for pending art.
4. Keep candidates and proofs in the matching outbox; do not generate directly into `client/`.
5. If approved work enters Unity, also use `$warband-unity-workflow`.

Do not bypass either approval gate.
