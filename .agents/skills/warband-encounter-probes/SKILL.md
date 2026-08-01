---
name: warband-encounter-probes
description: Measure warband's PvE content — balance tuning, enemy composition, act difficulty, boss tuning, run length, "does this encounter pose a decision?" — with the baseline/enc/boss/oath probes.
---

# Warband Encounter Probes — Codex router

1. Read `.claude/skills/warband-encounter-probes/SKILL.md` completely and follow it as the shared
   Claude/Codex source of truth.
2. Read `docs/vault/Design/pve-encounters.md` + ADR 0023/0024 before authoring or retuning.
3. Run `make baseline` before AND after any content change; the A/B is `git diff`.
4. Probes NAME what is there; they do not authorize a balance pass (CLAUDE.md, ADR 0016).
5. Every composition change must still pass `ContentTests.FullRunsCompleteOnRealContent`.
6. A new probe report supersedes the old one and is registered in `docs/vault/index.md`.
