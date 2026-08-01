---
name: warband-rules-copy
description: Write, review, or trace exact player-facing Warband rules text from authoritative game data. Use for rank-up/spec descriptions, signatures, passives, weapons, trinkets, Inscriptions, statuses, encounters, cards, tooltips, dossiers, fixtures, or any report that copy is vague, stale, duplicated, misleading, or unlike Warband's established mechanical language.
---

# Warband Rules Copy — Codex router

1. Read `.claude/skills/warband-rules-copy/SKILL.md` and its required references completely, then
   follow them as the shared Claude/Codex source of truth.
2. Trace rendered text through both the live surface and any review fixture to the authoritative
   simulation data before editing prose.
3. Author sentence shapes, but derive every rule fact and tuning value through the production
   presenter. Fixtures consume the same projection.
4. Use concise Warband vocabulary and preserve every decision-relevant trigger, target, amount,
   range, duration, cadence, condition, and consumption rule.
5. Prove the seam with exact grammar coverage plus a mutation test showing that changing data
   changes the rendered text.
6. If the work changes or verifies the Unity client, also use `$warband-unity-workflow`.
