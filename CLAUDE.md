# warband — hex autobattler (working title)

Fourth autobattler attempt, started 2026-07-22. PvE roguelike autobattler about building an
era-spanning warband into a system-breaking engine, then testing it against authored
asymmetrical encounters and an endless horizon. Deep hero spec trees, hex placement, Unity
client, pure C# deterministic sim. PvP is deferred.

## Source of truth
- `docs/vault/index.md` — vault manifest (Design/, Decisions/, Projects/; Daily/, Bugs/ as needed).
- Read `Design/pitch.md` + `Decisions/0016-pve-first-asymmetric-endless.md` before any
  non-trivial design or build work. ADR 0001 still owns the anti-washout process contract.

## Planning SOP — "ok, what's next?"
**`docs/vault/Projects/roadmap.md` is the ONLY live priority list.** When Jake opens a
session and asks what's next, the answer comes from the board, never from memory:
1. Read the roadmap. "What's next" = the topmost unblocked item, respecting its stage.
2. **Stages (actionable-only board, Jake's 2026-07-28 law):** `DESIGN` = needs a Jake
   conversation — don't build it, drive the discussion with concrete proposals and record
   outcomes as ADRs, then flip it to SPEC'D. `SPEC'D` = build autonomously. `BUILD` = in
   progress — finish before starting anything new; machine verification (tests, captures,
   matrices) is part of BUILD, not a parking stage. `DONE` = move to Done with a date.
   In-motion feel goes to `Projects/play-pass.md` (Jake playtests continuously — it is
   feedback, never a board item).
3. **Update the board as you work** — stage flips when state changes, finished work moves
   to Done, new discoveries become items (Deferred/Open-questions if not now). Session end
   with a stale board = failed session hygiene.
4. Never invent roadmap priorities without Jake; the Deferred list is "explicitly not now."

## ⚠ Content doctrine (amended 2026-07-23 — read before touching content)
The eight hero kits, weapons, and five legacy Banner triggers are mechanically authored
**first-playable candidates**, but their numbers and interactions remain unplayed. The
Banner triggers are now only the seed set for the Hourstone/Inscription layer (ADR 0017).
Do not conduct a detailed balance pass before the interactive playtest; fix only broken
machinery, unreadable behavior, or crazy outliers that invalidate the test.

Current PvE enemies and events are **scaffolding**: random player kits with scaled stats are
not representative content. Real PvE uses small, authored, asymmetrical encounter families
that pose legible build/placement problems. The fun target is the player discovering
compounding interactions that feel like they break the game. Do not flatten a spectacular
engine merely because it is powerful; see ADR 0016's balance law.

## Build laws (standing instructions — Jake has repeated each of these unprompted)
- **Build SYSTEMS, not features.** Usually in caps: *"we want to build SYSTEMS we can use data to
  configure to easily add more stuff and alter it!"* The test: adding the next instance of a thing —
  a spell, a tell, an enemy, a screen, an inscription — is a data edit or one enum case, not new
  code. Prefer `tuning.json` rows, `scenarios.json` fixtures, and signature-keyed lookups over
  bespoke branches. Specs state laws plus an extension table.
- **A knob is not real until `tuning.json` owns it.** Every new visual/feel knob ships with its JSON
  entry and F1 menu row in the same change; a slider that only pokes a live field silently drops on
  Save. Sliders never cap a value — free-entry box beside the slider, and the slider's own min/max
  retunable. Never bake an experiment into a scene file (`ticksPerSecond: 10000` overrode his
  setting on every reboot for a day). Never put comments in `tuning.json` — one unknown key voids
  the whole file to silent defaults.
- **One frame, one card.** Every post-round player choice lives in the workbench frame and reuses
  the market card + dossier components: interludes, revision picks, revision tier-ups, muster,
  in-combat unit inspect. Don't author a bespoke screen or a second card layout — extend the shared
  card. The one exception he named: unit rank-up gets its own popped modal, because it should be a
  dopamine shot.
- **Show, don't tell.** *"a good rule of thumb is always to SHOW not tell. We should use the units
  if needed, draw numbers above them ... so we arent doing a bunch of menuing."* His loudest
  recurring complaint is *"it looks like a tool UI and not a game"* — a slide deck of correct labels
  is still a failure. And never summarize away an individual event: six damage instances spawn six
  numbers, not `84 ×6`.
- **Enemies break the player's rules.** Authored PvE uses disclosed rule-bending mechanics (WARD,
  RITUAL), never stat-scaled player kits — ADR 0023.

## "Done" means verified (this is where sessions fail him)
Nothing is reported built until:
1. `make check-client` is green — the headless client compile, run before the Syncthing round-trip
   and before taking the Unity lock.
2. The Unity console is clean after the sync (clear it first — logs carry no session attribution).
3. You have **opened the capture and looked at it.** A green layout contract is not evidence:
   contract-blind bugs have survived a 70/70 passing run, because text wraps legally inside a
   wrongly-narrow label.
4. Any new gate has been **negative-controlled** — break the thing on purpose and watch it go red.
   A gate that cannot fail is not a gate. A vacuous test passed for free because no seed reached the
   state it asserted on; a missing CLI tool read as a clean negative result.

Writing "unverified" does not license shipping. *"I wrote 'unverified' and let that stand in for
'this might be unusable.' That was the mistake."* If a surface might be unusable, say unusable or
don't ship it. Mark board items with the gate they actually passed — `machine-gated green, NEVER
HEARD`, `pixels unseen`, `never watched`.

Jake is the only Play Mode verifier and his passes are scarce: never queue several unverified visual
surfaces behind one pass. If he reports the **same** symptom after a fix, the previous fix was a
guess — stop patching parameters and find the mechanism. (Three rounds of "text is blurry" were DoF
tuning; the cause was that world-space text writes no depth, so DoF blurred each glyph by whatever
was behind it.)

## Invariants (ADR 0016 + ADR 0001 process contract)
- Identity: authored asymmetrical PvE acts and bosses → clear run victory → optional endless
  continuation · TFT-style emergent movement on hexes · spec-tree heroes · system-breaking
  player builds · Unity + pure C# sim.
- PvP, ghost matchmaking, ratings, and PvP rewards are deferred. No core design or first
  playable depends on a server.
- The sim is a pure C# assembly: deterministic, seeded, order-independent, headless-testable,
  zero `UnityEngine` references. Replay = re-simulation from (seed, snapshots, contentVersion).
- First-playable content budget is a hard cap (`Design/content-budget.md`), not a floor.
- Nothing is "LOCKED" until it has been **played**. Friends playtest #1 outranks any new system.

## Stack
- **Sim + tests:** .NET class library, built and tested headless on homeserv — this comes first,
  before any Unity work.
- **Client:** Unity 6.3 LTS (URP), Guildrun/TFT isometric camera, 2.5D programmer art until the
  loop is proven. Project and remote pipeline are live; detailed state is on the roadmap.
- **Server (deferred):** not required for core PvE or friends playtest. Deterministic snapshots
  leave the door open to later Echo exhibitions and leaderboards without shaping today's game.
- **Distribution:** copy Shoota's launcher for one-click friend installs.

## Sharing Unity with other sessions (Jake runs Claude + Codex in parallel)
The Unity Editor is a **singleton**: one scene, one play-mode state, one loaded replay, one console
buffer. Two agents driving it corrupt each other's state and misread each other's errors. Protocol
and rationale: `~/brain/meta/agent-locks.md`.
- **Claude sessions are gated automatically** — a `PreToolUse` hook takes a lease on `unity-warband`
  before any `mcp__unity-mcp__*` call and denies it when another live session holds it. Nothing to
  remember. **Codex must call `agent-lock` itself** (same CLI, see the brain page).
- The lock is **per project**, so warband and Shoota never block each other.
- If you're denied: do NOT spin-retry. Sim/tests/sweep/content/vault work is completely
  uncontended — go do that, `agent-lock status` before returning, tell Jake if truly blocked.
- **Clear the Unity console before acting inside a hold.** `GetConsoleLogs` has no session
  attribution, so otherwise you will read someone else's errors (or your own stale mid-edit
  compile) and chase a ghost.
- **Never steal Windows desktop focus.** MCP work must not foreground, raise, maximize, or focus
  the Unity Editor, Game View, or a Player window. Keep the Game View on **Play Unfocused** and use
  semantic inspection, deterministic stepping, camera/RenderTexture captures, or other
  background-safe checks. If verification truly requires a focused Game View, stop and ask Jake
  immediately before doing it.
- The lock does **not** protect files. If another session is live, agree on file/subsystem ownership
  first — `ReplayPlayer.cs` has already been edited by two sessions at once, and a scripted
  stylesheet cleanup permanently destroyed another session's authored trims. `cp` before any
  scripted whole-file rewrite; prefer surgical edits on tracked files.
- **Batch the Unity work.** Accumulate every step that needs the editor into one queued list and
  tell Jake exactly what will run the moment it frees, so he unlocks once instead of babysitting
  handoffs (he hand-freed the lease four times in one evening).
- A foreign session's broken `Assets/Editor/*.cs` stops Unity reloading assemblies at all, which
  blocks every capture-based check. Look for foreign compile errors before planning a capture.
- Roadmap item numbers are immutable and parallel sessions collide on them: check the board's
  on-disk state immediately before claiming a number, and re-read before writing.

## Working with Jake in-session
- **Give lettered options plus your own recommendation.** He decides by picking letters — *"Yeah I
  think we go with b here"*, *"I think you are right on R1 + R3. Build it!"* A prose recommendation
  is unanswerable. Say which you'd pick and why you'd reject the others; don't build in the proposal
  turn.
- **Silence reads as a stall, slowness doesn't.** Post a one-line status on anything running longer
  than a few minutes — what's running, last result, what wakes you next. He asked "are we stalled?"
  five times in two days and twice it genuinely was a stall.
- **When he repeats an ask, the second version is the real spec — cut or simplify harder than feels
  safe.** The roadmap groom went 997 → 454 lines on ask one, then 131 on ask two.
- **Answer numbered notes with matching numbers**, and give each reported bug its own named root
  cause rather than one summary.
- Honest verification gaps cost nothing; overclaiming costs. He keeps going after "I couldn't verify
  X" and gets sharp only when he has to discover the breakage himself.

## Vault upkeep
Light-touch: ADRs for MAJOR decisions only; a daily note when a session meaningfully moves the
project. Don't over-document.

<!-- portal-suggestion 4d55c88e8ee2676380ce8bdb -->
- **Warband UI drafts default to 'tool/web' look, need explicit gamify pass every time** — First-pass panel/overlay designs (sim presentation, unit dossier, market cards) keep landing looking like a web tool/spreadsheet instead of a game, and Jake has to explicitly request gamification after seeing the bland draft each time. Default new UI work to: portraits/punchy rank styling, status icons instead of text-heavy HP/mana blobs, and 'show don't tell' (numbers drawn over units, not menus).
  <!-- seen 4×, e.g. this look sbetter, but man it looks like a tool UI and not a game right? How can we gamify this a bit? make it feel like… -->
