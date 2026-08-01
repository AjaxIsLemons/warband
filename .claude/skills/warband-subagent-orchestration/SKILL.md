---
name: warband-subagent-orchestration
description: Fan out subagents on warband work — parallel research, "use opus agents", multi-agent builds, delegating while context fills, and reviewing agent-written code before it lands.
---

Jake's doctrine, near-verbatim: *"you should probably use opus agents and then review their
work. be explicit with them about what they need to do and how they or you can test so we lock
this done."* Delegation is expected. **Unreviewed delegation is not** — you own the gate, not
the agent.

## Write the shared contract FIRST, then fan out
Author the interface, router, data shape, and stylesheet yourself before any agent starts. The
playbook that worked: the orchestrator wrote the contract, two agents built five screens in
parallel against it, one compile error across six agent-written files. Fan out onto a blank
contract and you get five incompatible dialects to reconcile by hand.

## Every brief names the verification method
Not just the task — **how the work gets proved.** An agent that doesn't know how it will be
checked doesn't check itself. Name the concrete gate: `make test`, `make check-client`, a probe
target (`make enc` / `make boss` / `make oath`), a specific capture. Then give each agent:
- **A file/subsystem fence** — the paths it owns and the paths it must not touch.
- **A reporting requirement** — exactly which paths it changed, plus "no commits." The shape
  that keeps parallel work safe on an uncommitted tree reads like: *"Only
  `sim/Warband.Sim/TellMatch.cs` and `sim/Warband.Sim.Tests/TellMatchTests.cs` changed. No
  commits."*
- **Its lane** — sim/tests/content/vault, or the single Unity slot below.

## ⚠ An idle notification is not a delivered report
Background agents routinely finish and go idle **without sending their findings**. This hit all
five research agents on 2026-07-25 (inventory, pipeline, legibility, alternatives, simwork) and
all four audit agents on 2026-07-29; every one needed a follow-up `SendMessage` asking it to
send the full report. Budget for the collection step. Silence is never evidence of an empty
result.

## One lease, many lanes
The Unity Editor is a singleton — **at most ONE agent in a fan-out may hold `unity-warband`**
(protocol in CLAUDE.md; Claude sessions are gated automatically by a PreToolUse hook). Everyone
else gets sim/tests/content/vault work, which is completely uncontended. Never spin-retry a
denied lease. Before planning any capture-based verification, check for foreign
`Assets/Editor/*.cs` compile errors: an in-flight editor-script error stops Unity reloading
assemblies at all, which blocks your verification entirely.

## The lock does not protect files
- If another session is live, agree file/subsystem ownership before dispatching.
  `ReplayPlayer.cs` has already been edited by two sessions at once.
- **`cp` the file before any scripted whole-file rewrite.** A scripted stylesheet cleanup
  permanently destroyed another session's authored work.
- Parallel sessions collide on roadmap item numbers, and **item numbers never change** (ADRs
  cite them). Re-read `docs/vault/Projects/roadmap.md` on disk immediately before claiming a
  number, and again before writing. Real collision: another session claimed 20/21/22
  mid-session, so the new work was renumbered to 23 rather than shuffling theirs.

## Review the work — a green claim is a claim, not evidence
Read the diff, then **re-run the gate yourself**. Check each agent's reported path list against
`git status` for fence breaches. Spot-check that new tests assert something real rather than
passing trivially. If an agent reports "verified," establish which mechanism verified it before
that word reaches Jake — his board language separates machine-green from seen-in-motion for
exactly this reason.
