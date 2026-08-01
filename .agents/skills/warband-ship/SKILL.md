---
name: warband-ship
description: Build, publish, and verify the warband Windows client, launcher, and download site. Use for release cuts, ship preflight, release status, launcher builds, site deploys, and player-only build failures such as stripped shaders. Publishing requires Jake's explicit go-ahead every time.
---

# Warband Ship — Codex router

1. Read `.claude/skills/warband-ship/SKILL.md` completely and follow it as the shared
   Claude/Codex source of truth.
2. Read `deploy/README.md` before running any target; it owns the pipeline diagram.
3. Default to the read-only path — `make ship-preflight`, `release-status`, `content-version`.
4. `make release`, `ship`, `launcher-release`, `site-deploy` are outward-facing — ask Jake and
   wait. `make release` also needs the `unity-warband` lease; call `agent-lock` yourself first.
5. Touching Unity client code or the Editor: also use `$warband-unity-workflow`.

Never publish, deploy, or restart a service without Jake's explicit approval for that run.
