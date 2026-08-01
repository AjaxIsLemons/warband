---
name: warband-audio
description: Author, bake, promote, and verify warband sound effects. Use for SFX clips, the mixer and bus routing, the audition sheet, the combat voice budget, sfx make targets, and generated audio. Measure every clip against the contract; generation requires Jake's approval.
---

# Warband Audio — Codex router

1. Read `.claude/skills/warband-audio/SKILL.md` completely and follow it as the shared
   Claude/Codex source of truth.
2. Read `docs/vault/Design/audio.md` (§5 laws, §6 tooling, §7 build order) and
   `tools/sfx/families.json` before changing a clip or a contract number.
3. Verify with numbers — `make sfx-lint`, `make sfx-density`, `sfx.py measure`. "It imported"
   is not a gate, and lint passes today, so a red run is a real regression.
4. Bake and audition under `docs/audio/`; promote into `client/Assets/Resources/{UI,Board}/SFX`
   only after checking which file `SfxPlayer` resolves (the `_1` trap).
5. Ask Jake before any generated-audio credit spend. Entering Unity: `$warband-unity-workflow`.

Do not bypass the contract gate or the approval gate.
