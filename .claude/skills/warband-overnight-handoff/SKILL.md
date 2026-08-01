---
name: warband-overnight-handoff
description: Run an unattended multi-hour warband block — "I'm going to bed", "take the wheel", "I'm not at home / can't play", "what can you work on while I'm away", "let's rip out some work overnight".
---

Jake has handed you hours with nobody watching. He wants to wake up to **built work plus a
trail he can check without asking you anything** — not a plan. Plan from
`docs/vault/Projects/roadmap.md` per CLAUDE.md's Planning SOP; never from memory, never invent
priorities.

## The shape he asks for
Four steps, stated once in full: **research the game** (read the vault) → **gap analysis**
(missing features / content / design / combat rendering) → **priority list of things not yet on
the board, then get them onto the board** → **implement one thing end to end**. Questions go
into a morning pile, not a blocking prompt.

## Step 0 — declare scope, then build
"Tell me what you are going to take" is literal. Your first response is a short commitment
list: what you're taking, what gate proves each item, what you are deliberately not taking. No
code in that message. Then go — asking "shall I?" burns the whole block.

## Pick work whose value AND risk live in headless C#
- **Default lane, uncontended and self-verifiable:** `sim/`, `make test`, `make check-client`,
  authored content, probes (`make enc`, `make boss`, `make oath`, `make baseline`), vault work.
- The `unity-warband` lease may be held by Codex all night. Denied → sim lane, no spin-retry
  (CLAUDE.md). A foreign session's broken `Assets/Editor/*.cs` also stops assembly reload
  entirely, so captures may be impossible through no fault of yours — say so, don't fake it.
- **Jake is the only Play Mode verifier and his passes are scarce.** Label visual work
  best-effort *when you claim it*, and never stack several unverified visual surfaces behind
  one pass — four board items once queued behind a single play pass.
- Never put "you should playtest" on the board. He always plays and always reports; in-motion
  feel lives in `docs/vault/Projects/play-pass.md`.

## Report gates, not vibes
Split the wrap in two, explicitly:
- **Verified:** N tests green · console clean · headless compile passed · captures you actually
  opened. Name the mechanism.
- **Not verified:** beat stagger, hit-stop, audio mix, palette strength — "needs your eyes."

Mark board items with the gate they actually passed. The language that satisfied him:
`BUILT 2026-07-27, steps 0–6. VERIFY: machine-gated green, NEVER HEARD.` Same construction for
`never seen`, `never watched`, `pixels unseen`.

⚠ **Writing "unverified" does not license shipping.** The post-mortem that matters: *"I wrote
'unverified' and let that stand in for 'this might be unusable.' That was the mistake."* If a
surface might be unusable, say **unusable** — or don't ship it.

## The morning trail ("I'll check your work tomorrow")
All of this is the job, not cleanup:
- Board stages flip as state changes; finished work → one dated line under roadmap `## Done`,
  detail into `Projects/roadmap-done-archive.md`. A stale board is a failed session.
- Item numbers are immutable. New discoveries become new items, or Deferred/Open-question rows.
- A `docs/vault/Daily/<date>.md` note with the blow-by-blow.
- Loose ends stated plainly: half-done work, red tests, and anything you owe him — including
  sudo commands only he can run.
- Close with **one cheapest-first next step**. A good one opened: "the one thing I'd do first:
  play it — not build anything."

## Morning briefing inbox
Route genuinely morning-relevant loose ends through the `mcp__portal__submit_briefing_item` MCP
tool. ⚠ `$PORTAL_AGENT_TOKEN` is **not set in warband sessions** despite `~/CLAUDE.md` claiming
it is, so a raw curl 401s. If the write fails, carry the handoff in the daily note + roadmap
and **say the briefing item didn't land**. Never promise one you didn't confirm.

## Status cadence
He checks in when you're *silent*, not when you're slow — five unprompted "are we stalled?" /
"done?" pings in two days, and twice it genuinely was a stall. Post a one-line status on
anything running longer than a few minutes.
