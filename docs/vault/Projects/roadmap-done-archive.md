# Roadmap — Done archive

Full detail of completed roadmap items, moved out of `roadmap.md` during the **2026-07-27 hard-cut grooming**.
The board had re-grown to 1145 lines with 558 of them here, which is how a board stops being usable.

`roadmap.md` keeps a one-line dated entry per item and links here. Nothing was deleted.
Blow-by-blow build logs also live in `Daily/<date>.md`.

- **2026-07-26 — CANDIDATE CONTENT + FIRST THIRD PATH (Sharpshot Spotter), authored but
  UNREACHABLE.** Jake: *"start building that content... doesn't have to be active in the game yet."*
  That threading is what makes it legal — the first-playable cap governs what a run can *offer*, so
  unreachable content spends none of it.

  **The mechanism.** `Kits.CandidateNodes` / `Kits.CandidateOffers` are registries of their own, not
  a flag on the live ones, so leaking a candidate takes a code change rather than a mistake.
  `Catalog.Node` resolves candidates (the sweep must compose and fight them); only `SpecOptions`
  gates them, behind `Catalog.IncludeCandidates`, **default false** — a RunController is always
  handed a live Catalog. The content fingerprint folds `Nodes` and `Offers` only, on the principle
  that *content which cannot be reached cannot influence a fight, so it is not part of a run's
  content identity*. Verified: fingerprint is still `3dba11673c26e858` with Spotter authored, so
  **no save or replay was invalidated.** Promotion is one edit (move the rows into the live tables)
  and moves the fingerprint then, correctly. Pinned by 5 containment tests, incl. a literal
  fingerprint assertion that fails loudly if anyone folds candidates into the hash.

  **Why Sharpshot first.** ADR 0011's own roster audit flags Sniper + Volleyer as **double-DEEPEN**
  (both "ranged dps, more so"), breaking the fork law that one path per class must ADD or SWAP.
  Spotter is the missing SWAP: ranged dps → support. It also needs **zero new sim vocabulary** —
  `StatusKind.DamageTakenUp` already exists (Reckless Swing's dial), so "Marked enemies take +15%
  from all allies" is a plain enemy debuff every ally reads for free.

  **Corrected two feasibility claims** in `Inbox/warband_roster_expansion_plan.md`: it rated Spotter
  "M" (needs a primitive) — it is free today; and it implied Saboteur was cheap — `GainMana` discards
  `amount <= 0` (`Battle.cs:1113`), so Mana burn needs a new effect, and there is no Mana-ordered
  `SelKind`. **Saboteur is the expensive one, Spotter the cheap one.** Glasswright sits between:
  wall machinery is fully built and unused (`Scenarios.cs:31` — no kit has ever made one), but
  `FieldDef` is radius-only, so a line wall needs a new field shape.

  **Deviation worth knowing:** the plan's Spotter A/S nodes are mostly team-facing ("every third
  ALLIED attack", "allies that damaged it"), but a `SpecNode` carries owner-side `Triggers` only —
  team-facing is the Banner/Inscription `TeamTriggers` layer. The authored nodes keep the intent and
  express it through what Calamity does with the Mark; Kill Order pays the warband Mana rather than
  tracking damage contributors, which would need a per-enemy ledger the sim does not keep.

  **First sim read** — `dotnet run --project sim/Warband.Sweep -c Release -- --candidates`.
  73 builds (64 + Spotter's 3×3), **0 safety-cap hits**, all compose and terminate. Node deltas:
  `volleyer vs spotter Δ19`, `sniper vs volleyer Δ-24` → ordering **Volleyer > Spotter > Sniper**,
  Spotter ~5 above Sniper. Mid-pack on placeholder numbers: neither dead nor dominant.
  **Caveat — this fixture understates it.** The round-robin is hero + 2 escorts, so a support path
  gets only two bodies to amplify. `--enc` against authored encounters and formations is the real
  exam. `ProbeParties.Field` now resolves candidates so that probe can run without promoting first.
  Also fixed: the node-delta report read `Kits.Offers` directly and would have silently skipped
  every candidate — exactly the content `--candidates` exists to measure.

  **Encounter + boss probes** (`--enc --candidates`, `--boss --candidates`; `ProbeParties.Active`
  appends a `focus` axis = the `damage` party with pyromancer swapped for Spotter, so the columns
  differ by exactly one hero. Both default reports verified byte-identical without the flag).

  `--enc` **could not grade it, because it cannot currently grade anything**: 4 of 6 node encounters
  are **FREE for every axis** at these party sizes. Spotter cleared everything the four live axes
  cleared and failed nothing they passed (verdicts went ADMITS 4 → ADMITS 5, otherwise identical).
  No signal — and that is a fact about the node pool, not about Spotter.

  `--boss` is the exam that discriminates, and it produced the shape ADR 0016/the plan ask for —
  a favourable exam and an unfavourable one:

  | boss | focus (best/worst/spread) | read |
  |---|---|---|
  | act 2 · Ashfall Battery | **100 / 100 / 0** | **the only axis that clears from all six formations** — every live axis is 100/0/spread 100 |
  | act 3 · Waning Crown | 100 / 38 / 62 | clears, where `reach` (the other Sharpshot party) is **marginal at 33** |
  | act 1 · The Last Oath | 0 / 0 / 0 | **NOT a Spotter measurement — see caveat** |

  Act 2 is the genuinely good result and it is legible rather than lucky: the Battery's rule is
  *"the gun is behind the wall, and the shell falls on whoever stands farthest back"*, and Spotter
  marks the **farthest** enemy — the bolt paints the gun. Act 3 says Spotter is a real upgrade over
  Sniper/Volleyer on that boss. Act 3's spread of 62 (vs `damage`'s 0) plus Δ19 behind Volleyer in
  the round-robin say it is not dominant.

  **⚠ Act-1 caveat — do not read that 0% as Spotter failing.** `ProbeParties.Field` applies **no
  spec node before act 2** ("forks only from act 2"), so at act 1 the `focus` party is a plain
  rank-C sharpshot+berserker+shade. The act-1 row measures pyromancer-vs-sharpshot *chassis*, and
  says nothing about Spotter. Anyone re-reading these tables needs this or they will chase a ghost.

  **NOT promoted. Nothing is live.** Widening a live pool is a content decision gated on playtest #1.

- **2026-07-26 — INBOX MARKET UI REDESIGN + EQUIPMENT PREVIEW.** Implemented the latest
  `warband-market-advanced-preview-spec.md` visual target without taking the roster-expansion
  proposal: one compact Hall command ribbon, 3+2 desktop stock grid, 96px persistent Warband shelf,
  and decision-specific two-column inspector. Weapon/trinket offers now preview every fielded
  recipient without mutating loadout state, retain that preview target in the shelf, show exact
  semantic before/after stats, and disclose lost/gained mechanics. Currency uses the shared
  Hourstone presentation, including `NEED [icon] N MORE`; balance arithmetic is gone from ordinary
  previews. Focus and selection remain distinct, stat direction is explicit beyond colour, and
  semantic stat/rule/recipient surfaces carry tooltips. Unity live gates passed one ribbon, 5-card
  3+2 geometry, recipient/comparison/rule counts, action-dock containment, and a clean console;
  `make test` is green (455 tests). Reference capture:
  `client/McpCaptures/market-advanced-preview-final-v3.png`.
- **2026-07-26 — VARIABLE-ARITY SPEC OFFERS + SEEDED POOL DRAW + FORK-RANK LAW.** Came out of Jake's
  read of `Inbox/warband_roster_expansion_plan.md` (a proposal to go 2→3 choices at every rank). The
  headline was rejected for now; the architecture under it was not. Key finding that reframed it:
  **`SpecOptions` was a pure dictionary lookup, so the spec tree was the ONLY deterministic layer in
  a run** — every Cleric saw War-Priest vs Lifebinder in every run, forever. Pick-3 would have bought
  catalog depth (8→27 builds/hero) and *zero* run-to-run variety.

  Jake's call: author a pool, offer a subset. Shipped:
  - `Kits.Offers` is `Dictionary<string, List<string>>` — an authored POOL, not a pair. `Offer(...)`
    takes `params string[]`, so all 40 call sites were untouched.
  - `IRunContent.SpecOptions` returns `IReadOnlyList<string>`; `PendingSpec.Options` and
    `PurchaseResult.PendingOptions` are lists; `ChooseSpec(i)` indexes with a bounds check.
  - **Fork-rank law** (`RunController.SpecPick`): the rank that decides what a hero IS always offers
    its whole pool — withholding a path hides a leg of the identity triangle, and the player already
    gambled a draft on that hero. Every OTHER rank draws a seeded subset (`SpecChoices = 2`). Uses
    the existing per-chassis `ForkRanks`, so Shade's late-bloom A-fork is handled with no special case.
  - Draw is stateless-by-salt over (Seed, hero instance, rank) — `SaltSpec = 4`, ADR 0008 idiom.
    Stable across save/resume, no rng persisted, no ordering coupling with shop/encounter rolls.
    Authored order is preserved, so a pool never reorders between runs — only its membership changes.
  - `RunSave` writes `pendingSpec.options`; **reads legacy `optionA`/`optionB`** so old saves resume.
    The drawn offer is persisted verbatim — re-drawing on resume would swap the menu under the player.
  - `RunShell.PeekSpecOffer` so the pre-purchase RankUpCard previews the DRAWN offer, never the pool
    (otherwise the card advertises a third option the rank-up then refuses to show). "1 OF 2" strings
    are now computed.
  - Client: `SpecChoiceModel.Options` is a list; ShopView/ManagementView/PlanningView loop instead of
    rendering hardcoded A/B. **No visual change today** — deliberately no 3-card layout work while
    combat legibility (item 0/1) is unresolved.
  - `BuildSweep` and `ContentTests` now branch on real pool width instead of a hardcoded `{0,1}`, so
    widening a pool widens the coverage rather than silently leaving new options untested.

  **Zero behaviour change today**: every authored pool is 2 wide, fork shows all, non-fork draws 2
  of 2 in authored order. Content fingerprint is **identical** (`3dba11673c26e858` before and after,
  checked against a clean HEAD worktree), so saves and replays stay valid. 455 tests green (+9 new in
  `SpecOfferTests`, incl. draw stability, cross-seed variety, resume, and legacy-save migration).
  `RealContentStillOffersEveryAuthoredPoolWhole` is the canary: it fails the day a pool widens, and
  that failure means "the draw went live", not a regression.

  **Not done, deliberately:** no new paths, no content, no balance. Widening any pool is a content
  decision gated on playtest #1.
- **2026-07-26 — ACT-SCOPED ENCOUNTER POOLS (closes item 14).** Acts 2 and 3 drew an identical pool,
  so a three-act run was one act played three times at rising numbers — while ADR 0024 gave each act
  a different boss, and pve-encounters.md's law above it is **"the boss rules the act"**: node fights
  exist to introduce, combine and stress the pieces its boss recombines. A shared pool cannot do
  that for three different exams. Pools are now built backwards from each boss:

  | act | boss | its exam | what the act introduces |
  |---|---|---|---|
  | 1 | The Last Oath | which threat you leave enraged | Gnawing Hour · Ninth Bell · The Drop |
  | 2 | The Ashfall Battery | reach the gun behind the wall | **The Long Range** (kill order past a ward) |
  | 3 | The Waning Crown | your own kills ring the bell | **The Long Procession** · **The Slagworks** |

  **Acts 2 and 3 are now disjoint** — the named defect gone at the root. Act 1 deliberately owns no
  unique encounter: its job is to teach pieces the later acts recombine. **Two new encounters, ZERO
  new roles** (ADR 0023's cap holds): *The Slagworks* — two Colossi hold the lane and the gunners are
  already past your front line, deliberately ruleless, the Battery's geometry without its clock;
  *The Long Procession* — a death-fed Scribe whose ritual advances on every death in its court, the
  Crown's exact trap at survivable scale. **Until now the run's final boss was the FIRST time a
  player ever met the idea that their own kills can be the losing line** — a knowledge check, not an
  exam. `Scribe(deathFed:)` follows the existing `Colossus(warded:)` pattern: same body, one
  encounter-level rule, no new role. **446 tests green**; baseline, replay, scenarios and DLLs
  regenerated; content fingerprint `f1f4a7e9b5cd527b` → `3dba11673c26e858`.
  ⚠ **The build law earned its keep, and the answer was uncomfortable.** It says measure before
  committing — and the first assignment measured FREE/FLAT for both new encounters. Every
  composition that fixed that (a third wall in the Slagworks · a nine-body Procession court · act 2
  drawing only wall fights) drove the naive line from 3/12 completed runs to **0/12** and tripped
  `FullRunsCompleteOnRealContent`. **The gap between the four answer-axis parties and the weakest
  legal comp is currently wider than the band an encounter can sit in** — nothing can be made sharp
  for one without being lethal to the other. That is a BALANCE finding, not a composition one, and
  the doctrine parks it until playtest #1. The shipped compositions are therefore sized to sit beside
  the existing pool rather than to beat a metric, and 4 of 6 encounters still measure FREE/FLAT.
  Cost, straight off the baseline: naive line **3/12 → 2/12** (act 2 node 0 is the pinch, 2 → 4
  deaths), run EV `fraying` victory 8% → 4%. Sim health unchanged-to-better (never-swung 0.27% → 0.00%).
- **2026-07-26 — PERSISTENT WARBAND BAR + ATOMIC LOADOUT TRANSFERS.** One shell-owned retained
  instrument now carries the warband through Hall, Wager, Deployment, and the frozen fight result:
  class portrait, rank, specialization-choice badges, weapon + temper, trinket, field/reserve state,
  capacity, and stored-equipment count. It is editable only in Planning Hall, read-only at Wager and
  result, becomes Deployment's sole friendly roster, and is deliberately hidden during live combat
  (no mid-fight substitution rule was invented). The old Hall shelf and Deployment rail no longer
  build duplicate visible controls. Hero identities are stable run-scoped IDs, saved and
  deterministically migrated for legacy saves. Weapon/trinket drops are atomic: occupied→occupied
  swaps preserve item identity/tier/investment, occupied→empty moves, explicit weapon→starter
  restores the source's own starter, and Armory drops unequip. Pointer-capture drag has legal target
  highlights and a ghost; click/keyboard selection is the equivalent accessible path; runtime
  hover/focus tooltips cover heroes, gear, and specialization badges. The Loadout Table now reserves
  the bar's safe area so its action dock stays reachable. **Gates:** 249 sim + 195 run tests green;
  Unity compiled with zero warnings/errors; one retained bar instance survived Hall→Wager→Deploy,
  hid in active Fight, restored read-only at result, used zero native tooltips, and the Wager Manage
  route returned to the focused Hall loadout. Game View captures checked Hall, Wager, Deployment,
  compatibility phone layout, result, and the expanded Loadout Table.
- **2026-07-26 — UI PROPOSAL SLICE 1: HALL HIERARCHY + COMPACT WARBAND BAR.** The Hall overview
  now follows the approved mockup's information order: one dominant Breach decision with a
  state-aware action, subordinate Market and Armory plaques, and the Hourstone as the lower visual
  anchor. Overview-only lore, duplicate recommendation chrome, the legacy warband shelf, and the
  extra `NEXT` attention pill no longer compete with that decision. Outside Deployment, the
  persistent bar is a 96 px command strip containing only the current field heroes plus a single
  Manage action; each card keeps class, rank, specialization history, weapon, and trinket visible.
  Deployment deliberately expands the same retained bar back to all eight field/reserve addresses.
  The Loadout Table temporarily replaces the compact bar instead of stacking two equipment
  surfaces. **Gates:** 249 sim + 197 run tests green; clean UXML and whitespace checks; Unity
  refresh/compile with zero warnings or errors; Play Mode proved three compact Hall cards, the
  Loadout replacement contract, and eight non-compact Deployment addresses. Visual proof:
  `client/McpCaptures/ui-proposal-slice1-hall-final-live.png`.
- **2026-07-26 — BALANCE INSTRUMENTS: 4-axis `--enc` + a committed baseline.** Jake: *"both enemy
  unit comps and balance tuning for our numbers — do we have a good strategy for that?"* Audit said:
  four good probes, one blind spot and one missing loop. **`--enc` measured every node encounter
  against ONE party** while `--boss` used four, so half the encounter report was conditional on a
  warband the author never chose. Formations, answer axes and the party-size curve now live in
  `ProbeParties` and BOTH probes read them (the `Encounters.Scale` discipline, applied to the player
  side); `--boss` output is byte-identical past its header, so the refactor is provably
  behaviour-preserving. **`make baseline`** writes `Projects/balance-baseline.md` — 104 metrics, one
  per line, dotted keys — so **the A/B is `git diff`** instead of a hand-built worktree. It asserts
  nothing and fails nothing; numbers are meant to move, and it exists so a session can see the
  movement. Byte-stable across regenerations. Also `make enc` / `make boss`.
  ⚠ **Findings the wider net turned up — measurements, NOT a licence to tune** (doctrine holds:
  instruments now, tuning after Jake's playtest — his call, 2026-07-26):
  ① **Party size is the strongest difficulty dial in the game, and it is not a stat.** The Long
  Range at act 2 admits 3 answers with spread 100 against three heroes and is **FREE from every
  formation against four**. One extra body deleted the encounter the vault calls "the sharpest in
  the pool" — that characterisation was an artifact of measuring act 2 with an act-1-sized party.
  Every probe table now prints the hero count.
  ② **The node pool is nearly free for a competent party** — of 48 encounter×act×axis cells, all but
  four sit at win=100. Placement spread, not win%, is the only thing still separating them.
  ③ **`banneret` is CHASSIS-DEAD** (avg 13%, best build 18%) and four node pairs are lopsided by
  ≥25 (shade.reaper vs phantom Δ-52, sniper.onebreath Δ-47, bulwark.juggernaut vs warden Δ-46,
  phalanx.pikewall Δ+30). Pre-existing; now recorded rather than re-discovered.
  ④ **The Long Range's ward never comes off for the `control` axis** (rule fired 0% at acts 2-3):
  control kills the warded Colossus straight through 50% DR, so the encounter's disclosed answer
  never happens even though control wins. An encounter whose rule does not fire is decoration.
  ⑤ **`reach` cannot clear the act-1 boss at all** (0% from every formation; 33% at act 3).
- **2026-07-26 — ROUTING + THE ENGAGEMENT LAW (ADR 0025).** Jake's bug report — "units sit behind
  others in line"; "jump units get stuck between two enemy units doing nothing". Both real, both
  reproduced headlessly, both worse than reported: a flank body logged **0 swings in 1200 ticks**,
  and a diver that leapt into a full backline stood **motionless for ~1000 ticks** while five enemies
  killed it. Cause was never the renderer — movement was a greedy hill-climb on straight-line hex
  distance (local minima everywhere on a 6-wide board), targeting had no notion of reachability and
  no fallback, and `LeapTo` cleared the target it had just chosen (inverting every `Farthest` diver).
  Replaced with a Dijkstra flow field to the **engage ring** (`sim/Warband.Sim/Pathing.cs`): walls
  impassable, **bodies a detour at `BodyCost = 6`** — which doubles as a unit's patience for a queue.
  Plus: a unit that can neither reach nor strike its target fights what it CAN reach (Taunt exempt),
  and a leap keeps its victim. **440 tests green**, new `PathingTests.cs`. Watch `BodyCost` at
  playtest — it is the one tuning constant in the system.
  ⚠ **The Drop went FREE → POSES A PROBLEM (100-point placement swing)** and the naive-line bot now
  dies in **act 1**, not act 2 (still 3/12 runs). That is the enemy AI working, not a regression —
  **do not rebalance against it before the interactive playtest** (content doctrine).
- **2026-07-26 — THE SITE IS LIVE AND THE LAUNCHER PULLS FROM IT (closes item 8).** Two failures, both
  mine, both now guarded in scripts rather than in notes.
  **(1) SSL protocol error.** Jake ran `setup-warband-site.sh` before the DNS record existed publicly.
  Caddy loaded the vhost, asked Let's Encrypt immediately, and got `DNS problem: NXDOMAIN looking up A
  for warband.inhouseboyz.com` on both http-01 and tls-alpn-01, then the same from ZeroSSL. That is a
  HARD failure, so Caddy backed off and never retried — the site answered `tlsv1 alert internal error`
  for hours. **The trap: local resolution is not evidence.** dnsmasq answers `*.inhouseboyz.com` from
  split-DNS, so the name resolved on this box while being NXDOMAIN to the world; I also mis-diagnosed
  it once as "Caddy never loaded the config," which the journal disproved. The vhost also pointed at
  **8090 (arena's port)** because it was generated before warband moved to 8092.
  → `setup-warband-site.sh` now **refuses to add a vhost without a public A record**, checked against
  1.1.1.1 over DoH and deliberately not the local resolver, and after reloading it **waits for a real
  TLS handshake**, printing the verbatim ACME error on timeout. "Reloaded" without a cert was the
  silent failure.
  **(2) `manifest returned HTTP 404` from the launcher.** The build had only ever been published into a
  **scratchpad staging dir**, because `/srv/warband-releases` did not exist during verification. Every
  local check passed and nothing was actually downloadable. `make release-status` said so plainly —
  nobody asked it.
  → `ship-release.sh` now **curls the public manifest URL after publishing and fails if the site does
  not serve the version just shipped**, naming `WARBAND_RELEASES_DIR` as the likely cause. A file on
  disk that the site does not serve is not a release. Negative-controlled against a 404 path.
  **Verified live, over the public HTTPS host:** `make ship` published v0.1.260726.1352 (content
  `f1f4a7e9b5cd527b`, 58 MB, sha matches on-disk) · the manifest and zip serve 200 through Caddy with
  the full 61,337,121-byte length · **the real launcher ran against the real site**: downloaded,
  verified the hash, installed 157 MB, and failed only at `exec` (Windows PE on Linux) · second run
  short-circuits to "Up to date" with no download · all six `Warband/*` shaders and StreamingAssets
  present in the installed payload · unauthenticated `/` 200 and `/launcher` 302 → Discord.
  **Method note:** my first shader check on the installed build reported all six MISSING. It was the
  *check* that was wrong — I grepped invented names (`UnitBody`, `HexTile`, …) instead of the real
  `Ring`/`GroundFill`/`Sigil`/`Glow`/`Particle`/`Dissolve`. A control grep for a known Unity built-in
  string is what exposed it. Same class of error as the `dig`-not-installed miss earlier: **a check
  that can silently return "nothing found" needs a positive control.**
- **2026-07-26 — FIRST STANDALONE BUILD + LAUNCHER/DELIVERY (item 8).** Jake: *"look at what we did
  for shoota too… we can piggy back off that infra."* Done exactly that — the pipeline is Shoota's
  shape (`deploy/ship-release.sh`, `launcher/main.go`) cut down, because warband has no server.
  **THE LANDMINE WAS REAL, and the preflight caught it before a build was spent on it.** New
  `Warband/Build Preflight` reported all six hand-HLSL shaders as *"would be STRIPPED from a player
  build"* — `GraphicsSettings.asset` held seven always-included shaders and every one was a Unity
  built-in. Since all six are reached only by `Shader.Find` and referenced by no material asset, the
  first friend build would have silently lost the ENTIRE combat-spectacle arc and looked like broken
  FX code. `WarbandBuild` now registers them itself every build and logs what it added; all six are
  in `GraphicsSettings.asset` by guid (verified by mapping guid → `.meta`).
  **Built:** v0.1.260726.1352, 162 MB, **0 errors**, content `f1f4a7e9b5cd527b`. Artifacts land in
  `~/warband-builds/` on Windows — OUTSIDE the Syncthing tree, or a 162 MB build syncs back into
  `git status`. `release.json` is written LAST and only on success, so a failed build cannot be
  shipped: there is no manifest to ship.
  **Delivery:** `make ship-preflight` / `ship` / `release-status` / `launcher-release` /
  `content-version`. Publishing is same-filesystem renames throughout, so a launcher polling
  mid-publish never sees a half-copied zip. **`ship` refuses a content-fingerprint mismatch** against
  homeserv's own — that almost always means stale Windows DLLs, and shipping it would make saves
  refuse to load with corruption-looking symptoms.
  **Launcher:** Shoota's, retargeted, with two real changes — the token is **optional** (Shoota's Go
  site checks it; warband serves a static manifest, and the gate can be added later WITHOUT
  reissuing launchers) and the zip URL is **resolved relative to the manifest URL**, so a plain
  static Caddy directory is enough and no application server is needed at all.
  **Verified:** publish pipeline end to end (58 MB zip + manifest, status readback) · launcher end to
  end against a local HTTP server — cold install, SHA-256 verify, atomic install with rollback,
  version marker, and the stand-in exe genuinely ran; second run short-circuits to "Up to date" with
  no download · cross-compiles to a 6.1 MB Windows PE32+ binary · **content fingerprint identical on
  homeserv and Windows** (closes the question left open by the version-stamp work) · a cold player
  correctly writes NO save.
  **First real player pass, 2026-07-26:** the exe boots through the menu and reaches a fight. That
  pass exposed a second build-only shader hole: runtime-created primitives use URP Lit and legacy
  tracers/bursts use URP Unlit; both resolved in Editor but were stripped from the player. The board,
  HP bars, and Mana bars rendered pink, while `Player.log` filled with
  `ArgumentNullException: shader` from `Burst.Create` / `Tracer.Create`. The build guard now includes
  URP Lit/Unlit alongside all six Warband shaders; post-build preflight passes and corrected build
  `0.1.260726.1648` succeeded with 0 errors. Combat and Hall/UI audio are disabled by default after
  the same pass found the generated clips bad and much too long. **Still needs one visual rerun of
  the corrected build before publishing.**
  **Gotcha found, worth knowing:** the **Editor and the built player share
  `Application.persistentDataPath`** (`AppData/LocalLow/InhouseBoyz/Warband` holds both `run.save`
  and the Editor's own `Unity/…/Editor/Analytics`). So a dev Play Mode session and a friend build
  read and write the SAME save file. Benign today, but it is exactly why a fresh build can appear to
  "already have a run" — that is what happened here, and it cost a detour to rule out as a bug.
  **ACCESS DECIDED (Jake): Discord sign-in gates the LAUNCHER, nothing else.** *"no gate needed since
  this doesn't really need a server. I think if someone signs in with discord, they can dl the
  launcher."* Built as `site/` — one Go file, Shoota's session/OAuth scheme (HMAC cookie, state-cookie
  CSRF, `identify` scope only) with the database, accounts, telemetry and admin all cut. `/launcher`
  needs a session; **`/releases/*` is open on purpose**, because the launcher is not a browser and
  carries no session — gating it would mean embedding a secret in every exe that anyone can read back
  out. Shoota reached the same conclusion the hard way. No allowlist: any signed-in Discord account.
  **Verified:** signed out, `/launcher` 302s to Discord while `/releases/*` serves 200; signed in, the
  exe comes back byte-identical to the published one; tampered and expired cookies both rejected. Then
  end-to-end — **the launcher pulled the real 58 MB build through the real site code**, verified its
  hash, installed 157 MB, and failed only at `exec` (a Windows PE on Linux: the correct failure, and it
  proves every step before it). And the shaders are not merely registered — **all six are physically
  present in the shipped `Warband_Data`** (`Ring`, `GroundFill`, `Sigil`, `Glow`, `Particle`,
  `Dissolve`), with StreamingAssets (tuning.json, tuning.ranges.json, 10 fixtures) alongside them.
  **That verification ran against a STAGING dir, not the deployed one** — `/srv/warband-releases` did
  not exist yet — so it proved the pipeline but published nothing a friend could reach. Closed below.
  **Still Jake's:** creating the Discord OAuth app, whose **redirect URI must be exactly**
  `https://warband.inhouseboyz.com/auth/discord/callback`. The Discord round-trip is the one path that
  cannot be tested without those credentials.
- **2026-07-26 — CONTENT VERSION STAMP (433 tests). The prerequisite for a server, not the server.**
  Jake asked whether we have a server and whether we'd want cloud storage for PvP; the answer is no
  server exists (**zero networking anywhere in the codebase** — the one grep hit was a cylinder named
  "Hourstone Socket"), homeserv already runs Caddy/Docker/Tailscale and a native Palworld server so
  hosting is a solved problem, and Shoota has a Unity **LinuxServer** build as precedent. Decision
  stands: **not today.** One clarification worth keeping — every PvP idea in the vault is
  *asynchronous* (Echo exhibitions against a stored snapshot, leaderboards, ghost boards), so it is
  "upload a blob, download a blob", a key-value store behind Caddy, **not netcode**. Real-time PvP
  would be a different game.
  **What was actually missing:** ADR 0008 has specified `contentVersion` since 2026-07-22 and it did
  not exist. `Replay.cs` had a *format* version only. Now `IRunContent.ContentVersion` — a
  **computed** FNV-1a-64 fingerprint of the whole content graph (`Warband.Sim.ContentHash`), not a
  hand-bumped constant, because the failure it guards is a retune and that is exactly what a human
  forgets to bump. Stamped into `RunState` at creation, into `GhostSnapshot` at capture, persisted by
  `RunSave`, checked by `Resume`. **Why the eager id check was not enough:** ids resolving proves a
  rename didn't happen, but a run's encounters derive from its seed at FIGHT time, so identical ids
  with different numbers resume happily and fight a different army than the save was made against.
  **Replays deliberately NOT stamped** — they store the full event log, so playback is
  content-independent; bumping the format to v6 would have forced regenerating 10 fixtures for a
  reason that does not hold. Re-simulation is what content changes break, and that only happens for
  saves and snapshots.
  **Tests (26 new):** the algorithm is pinned to an **independently computed** digest, so swapping in
  `string.GetHashCode()` — which .NET randomizes per process, and would make saves fail after every
  restart while looking like corruption — fails the suite · a retune moves the fingerprint at **11
  different depths** including a magnitude buried in a field's pulse inside a trigger · null ≠ empty ·
  order is significant · a retuned save is refused *while every id still resolves* · an unversioned
  save says "unversioned" · a matching stamp still resumes normally.
  Also: `--version` on the sweep tool prints the fingerprint, and dev builds show it on the menu, so
  "my save refused to load" is diagnosable instead of unfalsifiable. **Cross-machine check:** the
  `Warband.Content.dll` on homeserv and Windows is byte-identical (MD5 4DBDDE32…), and the hash uses
  no platform-dependent primitives. **Still unverified:** the on-Windows harness run — the extended
  `RunSaveCheck` was written and synced but Codex took the Unity lock mid-verification, so the
  fingerprint has not yet been printed from inside Unity. Run `Warband/Verify Run Save` when the lock
  frees.
- **2026-07-26 — RUN SAVE/RESUME (item 7, 412 tests).** Quitting the app no longer destroys the run.
  `Warband.Run.RunSave` converts `RunState` ⇄ text and **does no file IO** (the run layer is pure by
  law, ADR 0008) — the host owns the bytes, which is also what keeps the format headless-testable.
  Hand-rolled rather than JSON: Warband.Run has zero package references so the DLL drops into Unity
  unchanged, and reflection-based serialization gets stripped by IL2CPP. Format is
  `dotted.key=value` lines behind a version header — order-independent, unknown keys ignored,
  explicit `.count` on every list, and **content ids that could collide with a delimiter throw at
  WRITE time** rather than silently corrupting a save.
  `RunController.Resume(state, content, cfg)` rebuilds the machine without regenerating anything
  (regenerating maps or shop stock would replace what the player was looking at) and **resolves every
  content id eagerly**, so a save from an older build fails with the offending id named instead of
  mid-fight. Client half: `RunSaveFile` writes temp-then-move so a crash mid-write leaves the
  previous good save intact, never throws at the caller, and **deletes any save it cannot read** so
  CONTINUE can't fail forever. Autosave hangs off `Rebuild()` — the shell's single choke point, so no
  future action can change the run without the save following — plus `OnApplicationPause/Quit` for
  alt-tab. CONTINUE now means "a run exists, in memory or on disk"; a discarded save says so on the
  menu instead of failing silently.
  **The test that matters:** a run saved, serialized, and rebuilt from text plays out **identical**
  to one that was never saved — same encounters, same battles event-for-event (order-sensitive log
  hash), same Sand. Plus: earned growth and frozen offers survive · sold-out offer slots stay empty ·
  an implicit starter weapon stays implicit (null ≠ "") · a hero with no trinkets resumes with none ·
  truncated/garbage/future-format saves are refused · Reward-phase and PendingSpec saves resume still
  owing the choice.
  **Verified ON WINDOWS, not just headless.** New committed harness
  `client/Assets/Editor/RunSaveCheck.cs` → menu `Warband/Verify Run Save`, MCP-drivable, edit-mode
  only. Run this session against the real DLLs: save lands at
  `C:/Users/jwjwi/AppData/LocalLow/InhouseBoyz/Warband` (2066 bytes) · temp file consumed by the
  move · **bytes survive Windows text IO unchanged and no CR is injected into the record
  separator** · resumed act/beat/phase, Sand, warband and shop stock all match · a future format is
  refused · cleanup works. 12/12 PASS, console 0 errors.
  **STILL UNVERIFIED — needs Jake at the keyboard:** the shell wiring (does the CONTINUE button
  appear on a cold start, does clicking it resume, does the autosave hook fire on every action).
  **`EditorApplication.EnterPlaymode` is refused over MCP** — *"User interactions are not supported
  for MCP tool calls"* — so Play Mode is not reachable from a session at all. **That is a new,
  permanent constraint worth knowing: no agent can ever click-through this client.** Add it to the
  client gotchas.
  **Known behavior, not a bug:** quitting mid-fight-playback resumes at the *next* beat — the fight
  had already resolved and paid, so nothing is lost, but the result report is skipped.
- **2026-07-26 — ACT BOSSES + THE DISCLOSURE CONTRACT (item 2 ①②, ADR 0024, 392 tests).** Built
  overnight, unattended. Each act now closes on a different strength exam instead of the same bonded
  pair three times: act 1 **The Last Oath** (`BOND`, unchanged and deliberately so — it is the only
  boss whose decision has been measured), act 2 **The Ashfall Battery** (`BATTERY` — a Rooted gun
  behind two Colossi that shells your FARTHEST unit and leaves a burning crater, so bunching behind
  the tank is the losing answer), act 3 **The Waning Crown** (`WANING` — a bell fed by time AND by
  **every death in its court**, so clearing the escorts is what rings it). Bosses are authored FOR
  their act and take no act curve; the multiplier survives only past act 3 for the endless horizon.
  **The disclosure half was the bigger find.** The live planning beat hardcoded "THE LAST OATH" and
  disclosed *nothing* for the four node encounters, and enemy cards were built by `UnitCardFromDef`,
  which titles from `ContentLexicon.Chassis(ChassisId)` — so an **Hourling previewed as "Shade" with
  the Shade's ability text**, a Colossus as "Bulwark", an Hour-Scribe as "Pyromancer" reading out
  Inferno. That is worse than no disclosure. Now: `EncounterBrief` carries every body (role, accent,
  post-scaling HP/power/cadence/reach, row, and a **behavior sentence** covering the targeting rule
  `pve-encounters.md` always demanded); brief and spawn are built by ONE method so divergence is
  structurally impossible; enemy cards use the authored name and no portrait.
  **New instrument `--boss`** (`Projects/boss-probe-2026-07-26.md`) holds a boss to a harder bar than
  `--enc`: how many *kinds* of strength can pass it. It immediately caught the act-2 boss posing
  nothing (three of four axes at 100% from every formation, spread 0) — the bell went 14s → 9s
  against the measurement, not against taste. All three now show spread 100 and 3-4 passing axes.
  Two render fixtures added through a new `encounter` seam in `scenarios.json`. **Gates:** 392 tests ·
  scenarios round-trip + byte-stable across two runs · DLLs rebuilt · whole client compiled headless
  against Unity 6000.3.19 reference assemblies (gate itself negative-controlled).
  **NOT eyes-verified — nothing was watched in Unity.**
  **Session-hygiene finding, flagged loudly:** the tree held **178 uncommitted files** including the
  ENTIRE ADR 0022 + ADR 0023 implementation (`Enemies.cs`, `EncounterProbe.cs`, the unit-behavior and
  signature-patch tests, `MechanicalRulePresenter`) and most of the client shell — all listed as
  Done on this board while absent from git. That is why the `--enc` drift above could not be
  bisected. Committed as part of this session; **future sessions must commit their own work.**
- **2026-07-25 — AUTHORED PVE ENCOUNTERS (item 2, ADR 0023, 368 tests).** Enemies now have their own
  designs (Jake's call): authored `UnitDef`s with no chassis/rank/weapon/tree, not composed hero
  kits. Five roles — Hourling (swarm), Ashen Colossus (anchor), Sanddrift Gunner (artillery,
  acquires FARTHEST + standoff), Hour-Scribe (rooted ritual clock), Gloamstalker (opening Leap) —
  compose four node encounters, replacing random kits-as-monsters in `Catalog.Encounter`.
  **Composition is the act lever** (ADR 0016): factories size themselves by act, and an act's pool
  is its identity — The Long Range is act 2+ because a rank-C opening warband cannot clear it from
  ANY formation. Two authored rules bend the shared model on purpose and are both disclosed: WARD
  (50% DR while escorts live, stripped on the first escort death) and RITUAL (mana fed by trickle
  ALONE — needed per-unit `ManaPerHitTaken`, mirroring ADR 0022's `ManaPerSwing`, because on the
  global hit-fed rate a channeller fires the instant it is focused, inverting the problem).
  `IRunContent.EncounterBrief`/`BossBrief` + `RunController.PreviewBrief` carry the disclosure off
  the same private salt as `PreviewEnemies`.
  **The `--enc` probe is the real deliverable here** — it reports per-act win%, the SPREAD between
  best and worst formation, whether each rule fired, and how the naive bot line does. Its first run
  caught three of four encounters posing nothing and the Ninth Bell's ritual never firing (the
  countdown was longer than a fight). All four now pose a placement problem at their debut act.
  **Difficulty moved hard:** bot tier EV 88/92/79 → **35/48/39**, Fraying beating Stable.
  `FullRunsCompleteOnRealContent` stopped asserting the bot always wins (against authored content +
  terminal loss that would mean the PvE poses nothing) and now asserts the machine completes, the
  arc is reachable, and it is not free. `StarterWarband` drafts a plausible comp instead of
  `pool[0..2]` — the arbitrary one had a heal-auto Cleric and a Tower Shield Bulwark, i.e. one real
  damage source, and lost the first fight of every run. **Not eyes-verified: nothing was watched in
  Unity.** Scenarios regenerated, DLLs rebuilt.
- **2026-07-25 — UNIT BEHAVIOR LAYER + WEAPON CADENCE + SIGNATURE PATCHES (ADR 0022, 346 tests).**
  A systems review of the class/weapon/tree layer read the vault against the runnable content and
  found four levers the design already assumed but the sim had never grown. All four built, plus
  one plain bug. **① Every unit shared one brain** — `AcquireTargets` was nearest-only with no
  per-unit hook (combat-grammar.md promised "kits override" in round 6), movement was
  close-then-stop, and **no chassis ever set `MoveInterval`**, so all eight moved at the default.
  Now `TargetPref` (Nearest/Farthest/LowestHp/HighestHp, acquisition only — stickiness/Phase/Taunt
  untouched), `Standoff` (give ground to hold firing distance, never out of range, keeps attacking
  while withdrawing), and per-chassis speeds 3–7. Nodes may set all three, so **a fork can change
  the hat at the behavior layer** — Lifebinder's backline SWAP finally moves her.
  **② `WeaponDef.ManaPerSwing`** replaces the flat rate: mana/tick now spans 0.83 (daggers) → 1.40
  (mace, 2.80 mastered) instead of being purely 1/Interval. **③ Signature patches** — degree, not
  verb; 12 copy-pasted overrides converted; `AbilityIdentity` counts patches so cast tells survive.
  **④ Four trinkets** on the wired-but-unused `ManaMaxDelta` seam; the three item layers now own
  disjoint jobs (weapon = attack profile · trinket = chassis stat-shape · Inscription = team rules).
  **⑤ Frenzy** was bypassing `AttackInterval` outright — a window was worth 4 × weapon Damage at no
  tick cost, making the musket the correct Berserker weapon and his own daggers a trap; it is now
  +300% attack speed. **Sweep re-run** (`Projects/sweep-2026-07-25.md`): Sharpshot 46→62 and
  Pyromancer 32→46 (the two classes named as fighting their own pathfinder), Shade 60→45, Bulwark
  65→53, **Banneret unchanged at 12 — structural, as predicted**. No chassis-DOMINANT build remains
  (top was 94%, now 86%). New flag NAMED not tuned: `shade:reaper+widowmaker` DEAD at 8–9%, most
  likely the daggers cadence cut. Last Oath still poses its decision (Enrage 97%, placement chooses
  in 4/4 lineups, Δ96). Scenarios regenerated + Unity DLLs rebuilt. **Not eyes-verified:** nothing
  in the client was re-watched — Standoff and per-chassis speeds change how fights LOOK, and that
  wants Jake's play pass.
- **2026-07-25 — THE LAST OATH'S DECISION IS REACHABLE (item 3, 313 tests).** The 07-24 probe's
  "**THE CHOICE DOES NOT EXIST**" was geometry, not numbers: the pair stood asymmetrically
  (Bulwark (5,2), Sharpshot tucked behind at (6,4)), so the Sharpshot was structurally
  unreachable first and the Bulwark died in 1000/1000 fights. Both Oathbound now stand on the
  same rank at opposite board edges — **(5,0) / (5,5)**, a two-line data change. Result: both
  survive in real fights, **placement chooses the survivor in 4/4 lineups**, and the two
  branches cost Δ84 win%. Four placements were measured before shipping; the two inner-symmetric
  ones also pose the decision but make act 1 hard enough that the bot loses 4/6 seeded runs.
  The probe gained a "does placement choose the survivor?" section — the pitch is now something
  the report can actually answer. Report: `Projects/oath-probe-2026-07-25.md` (supersedes
  `oath-probe-2026-07-24.md`). **Named not tuned:** the decision has a strongly correct answer
  (kill the archer — a lesson, not yet a dilemma) · four arrangements kill both together so
  Enrage never fires and nothing in the UI names that · two arrangements run ~385 ticks.
- **2026-07-24 — FIRST-PLAYABLE RUN + PERSISTENT PLANNING UX (ADR 0019, 278 tests).**
  Three-act/five-beat state machine · terminal loss · initial Sand economy · deterministic
  Interludes and boss rewards · choose 3 of 5 draft · persistent board-first Planning
  replacing Map/Shop/Deploy · data-first shared cards + inspector · select-then-Buy/Hold ·
  portrait/icon presentation catalog · responsive landscape-touch and keyboard input · reduced
  motion + timing tokens + semantic audio hooks. Direct play feedback then rebuilt the opening
  draft as a full-screen portrait-led comparison: readable signature/passive blocks, semantic
  stat colours/icons, large values, and a strong 0/3 → 3/3 selection/action state. Unity Play
  Mode verified with a clean console; captures are in `client/McpCaptures/`. Detail:
  `Daily/2026-07-24`.
- **2026-07-24 — PLAYABLE POC SHELL + DEPLOYMENT + SCENES (263 tests).** Run layer retargeted to
  ADR 0016 (authored boss, `RunPhase.Defeated`, best-of-5 removed, ghost-capture dropped) ·
  `RunSetup` recruit draft · the whole client shell (Menu/Recruit/Map/Deploy/Shop/RunOver) on a
  view-model + router pattern · deployment with swap/pick-up and previewed enemies ·
  shop depth (equip/unequip/reforge/sell/bench) · `PreviewEnemies` · `GameBoot` startup order ·
  Boot→Game scenes · board no longer self-starts. Detail: `Daily/2026-07-24`.
- **2026-07-24 — RENDER + DATA SYSTEMS (item 4b).** Data-driven replay pipeline (`scenarios.json`)
  · `ReplayInspector` · signature-matched tells (`TellMatch`) · field flavor · directed tells ·
  unit identity + fight story · event viewer · walls block-then-adapt + firing-angle seek ·
  replay v3 snapshot identity · the Lexicon (id → words, one source). Detail: `Daily/2026-07-24`.
- **2026-07-23 — UNITY CLIENT BRING-UP (item 4).** Unity 6.3/URP project, Syncthing + MCP pipeline,
  sim→Unity DLL bridge, first replay render, diorama look, JSON tuning loop + F1 cockpit.
  Detail: `Daily/2026-07-23`.
- **2026-07-23 — OUTLIER SANITY SWEEP (item 3).** `Warband.Sweep`, 2,080 fights + 360 bot runs;
  zero caps/crashes, determinism intact; outliers NAMED not tuned (Phase uptime, Warden Taunt,
  Banneret floor) and victory saturating ~99% at every tier. Report:
  `Projects/sweep-2026-07-23.md`.
- **2026-07-23 — HERO/BUILD CONTENT PASS (item 2).** 8 kits as data (80 nodes traced to their dive
  docs), 11-weapon catalog with mastery riders, stat law, reforge, ForkRank, Bearer; then the
  fidelity pass rebuilding 12/13 SIMPLIFIED nodes to dive truth.
- **2026-07-23 — SIM MECHANICS BUILD QUEUE (item 1).** The whole dive backlog as reusable grammar
  primitives — everything Inscription/Relic-hookable, no unit-hardcoded specials.
- **2026-07-23 — PVE-FIRST IDENTITY AMENDMENT (ADR 0016).** PvE is the product; authored
  asymmetrical encounters and bosses replace mandatory ghost bosses; the player fantasy is
  assembling compounding interactions that feel like they break the game; the authored run
  has a real victory and may continue into endless until defeated. PvP moved wholly to
  Deferred. Pitch, theme, top-level guidance, affected historical ADR statuses, and this
  board realigned. Exact vertical-slice run/loss/endless rules intentionally remain DESIGN.
- **2026-07-23 — DESIGN CAMPAIGN COMPLETE (1a–1d).** Theme (ADR 0010) · impact model
  (ADR 0011) · 8/8 hero dives settled (Cleric, Bulwark, Shade, Sharpshot, Pyromancer,
  Berserker, Phalanx, Banneret — all champions named; laws locked along the way: ADR
  0013 targeting, Burn decay, ADR 0014 aura/muster, cheat-death + cross-layer
  precedents) · weapons pass (ADR 0015: 11-category catalog, engine riders, temper
  tiers + Relic rule, Tower forge). Sauce hunt stays PARKED (Design/sauce.md).
  Full session log: Daily/2026-07-22 + Daily/2026-07-23.
- **2026-07-22 — RUN LAYER COMPLETE (109 tests).** Bot-ghost generation (BotGhosts: boards
  sized to slot growth, deepened by act+record, geared, range-aware placement) + full-run
  harness (RunHarness/RunPolicy/AggregateReport: policy hooks, fight+economy metrics,
  deterministic). Smoke: 600 bot runs — Greedy tier strictly dominant under placeholder
  monsters (harness working as intended; tune at sweep/playtest, not now).
- **2026-07-22 — Run-layer design settled + skeleton & shop built (97 tests).** ADR 0006
  (shop & economy: every-node shops, 3→6 act-close slot offers, bench 2, gold), ADR 0007
  (wager tiers, per-kill payout + success bonus), ADR 0008 (run layer = pure host-agnostic
  lib), ADR 0009 (shop stock: offers/freeze/forks/banners/sell). `Warband.Run`:
  RunController machine — maps, wager fights, events, ghost bosses (draws = wins), record,
  slot offers, bench, shop stock, ProgressionFold, snapshot capture (incl. banners); 32 tests.
- **2026-07-22 — Design foundation.** Pitch v0.3; ADR 0001 (identity + anti-washout
  contract); ADR 0002 (best-of-5, wagering, anti-snowball); ADR 0003 (combat soul: clock +
  field, glyphs on flat maps); ADR 0004 (sim framework); ADR 0005 (loadout composition,
  crit-only RNG, weapon-required/range-on-weapon); combat-grammar, heroes anatomy,
  render-contract, placeholder roster docs.
- **2026-07-22 — Sim framework complete (65 tests).** Deterministic tick loop; hex math +
  lines + PCG32; trigger atom w/ negation; statuses incl. Silence⇄Disarm mirror; cascade
  bounds + death phase; ramp/zone/placement passives; run-scoped bonuses (ProgressionFold);
  PlaybackState fold + per-tick reconstruction guardrail; terminal viewer; fields (pulse/
  wall/projectile-path interaction, attached auras, presence statuses); conditional stat
  rules; FightStats + conservation; crit (seeded, attacks-only, IsCrit); 6×8 bounds; Leap;
  loadout composer (chassis/weapon/trinket/node merge).

---



## Pre-grooming STATE + item 1 full text (2026-07-27)



Item 1 had grown to 175 lines and the STATE block to 50. Both are preserved verbatim below;

the board now carries the compressed versions. Read this for how the feel/readability arc was built.


**STATE, 2026-07-26 (honest):** the first-playable run shape and between-fight UX are
walkable end to end: Menu → five-card Draft → full-screen Management Hall → stakes-first Wager
→ formation-reveal Deployment → Fight/replay → blocking result report → spatial Hourstone Table
→ Victory/Defeat. Three acts × five
beats, Sand economy, Interludes, boss rewards, and terminal loss are implemented. **460 tests
green** (2026-07-27). The workspace has data-first cards/inspector, portraits, explicit economic actions,
responsive landscape phone/tablet compositions, safe-area rules, reduced motion, and timing
polish. The old Management drawer has been replaced by stable Market/Warband/Armory/Hourstone
geography and bespoke workspaces; the Armory previews exact equipment deltas. **Combat viewing still does not read well enough.** Authored
encounters landed 2026-07-25 (ADR 0023) and per-act bosses + full encounter disclosure landed
2026-07-26 (ADR 0024) — deployment has real problems to answer and every fight now states its rule.
**The first standalone combat pass started 2026-07-26, but a build-only shader failure made it an
invalid read of the combat work.** The corrected build is ready for the real pass; that remains the
single biggest blocker on the board (see item 1).
**Opening Muster readability pass built 2026-07-26:** its universal cards were replaced by a
dedicated three-fact / two-rule scan grammar with code-native semantic glyphs, in-portrait exact
mechanics, ordered party sockets, semantic select/deselect feedback, cancellable reveal/lens
timers, F1 tuning, and F2 previews. Desktop resolved-layout verification now passes; mobile is
deliberately deferred.
**Unified decision-card pass built + Unity-verified 2026-07-26:** Feature / Stock / Detail /
Target profiles now share one typed fact registry, code-native glyph and semantic-colour language,
plain-language tooltips, and tunable selection/detail-swap motion. Muster shows flat Signature Mana
cost while attacks-to-ready lives in advanced help. The Hall is a 38/62 browse-and-decide stage:
compact stock/targets show identity and price only, while one persistent selected-card dossier owns
exact stats, rules, comparisons, and actions. Market, Warband, Armory, and Hourstone selection is
station-scoped; empty stations expand instead of inheriting a stale hero dossier. Live gates passed
Muster containment, Market footer/title/split bounds, repeated station rebinds, compact target
containment, empty Hourstone, and return-to-Market flow. Mobile-specific composition remains
deferred.
**Persistent Warband Shelf + Loadout Table built and Unity-verified 2026-07-26:** every Hall
station now keeps the six-address field cap, two reserves, ranks, portraits, and equipment sockets
visible in one bottom rail. Sand appears once in the run ribbon; duplicate FIELD/ledger readouts are
gone. Expanding the Shelf replaces the station body with one 30/36/34 formation/champion/Armory
instrument while preserving Market selection and scroll state. Selected detail is typed by decision
kind: champions get Basic/Signature/Passive, weapons get Weapon Profile/Mastery, trinkets get their
equipped rule, Inscriptions get a Run-Wide Law, and capacity gets six explicit sockets. Recruit/rank,
gear, and capacity receipts now land on the affected Shelf target. Expand/collapse/focus recipes are
live-tunable in F1. Live checks passed 40/60 Market containment, all 6+2 Shelf addresses, exact
30/36/34 Loadout bounds, Market selection restoration, weapon/Inscription no-Basic-Attack grammar,
and the zero-inventory Armory empty state at 2542×1304. Dedicated mobile composition remains
deferred by Jake.
**Shared mechanic presentation system built and Unity-verified 2026-07-26:** durability, offense,
restoration, space, time, mana, and protection now have one stable code-native glyph, colour,
tinted surface, and inline-text treatment. Shared stat tiles and rule formatting are used by
Market stock/detail, rank choices, cards, inspectors, tooltips, deployment/results, and live
combat. Prices now use the Hourstone emblem plus a number; `SHORT N` / `NEED N` commerce labels
are gone. A selected offer owns affordability: the action disables and the dossier shows
balance − cost = after. Live checks passed five typed Market prices, selected-offer affordability,
next-frame Market layout, combat inspector semantics, and a clean Unity console; all 446
sim/run tests pass. Captures: `client/McpCaptures/ui-semantic-market-selected.png` and
`client/McpCaptures/ui-semantic-combat-inspector.png`.

1. **FEEL & READABILITY — the fight does not read** — **VERIFY (was DESIGN → BUILD). THE TOP ITEM,
   AND IT IS BLOCKED ON JAKE, NOT ON BUILDING.**
   **The four live threads, so nobody has to read 90 lines to find them:**
   **1a — combat spectacle P0–P6** (casts, fields, status icons, deaths, dress): BUILT, machine-gated
   green. The first standalone pass was obscured by stripped URP shaders; **the corrected build has
   not been seen in motion.** Needs one play pass; the specific knobs to judge are listed at
   the end of the arc paragraph below. **1b — Hall polish:** BUILT + Unity-verified; four named
   polish slices open (Bind choreography · Rule Preview diagrams · real-device safe-area/haptics ·
   audio/motion feel). **1c — fight-legibility Phase 4 client UI:** HALF built — the damage-share +
   died-to readout shipped (`40eb076`), but `BattleForecast` exists in the sim and is referenced by
   **zero** client code, so the win-probability half has no home. **1d — camera/framing pass:**
   unbuilt, and taste-gated on Jake.
   **Nothing here needs a design conversation any more.** It needs Jake to watch a corrected fight.
   **Jake, 2026-07-24, after playing it:** *"playing it now still does not feel great for a
   lot of reasons (UI is not great, sim viewing has some issues and is not quite clear what's
   happening)."* Take this at face value: **item 4b's entire render arc — signature-matched
   tells, directed tells, unit identity, kill feed, fight story — was aimed at exactly this
   target and has not hit it.** Adding more tells is therefore NOT obviously the fix; the next
   move is to find out *why* it does not read before building more of the same.
   **Do not start by building.** Watch a fight with Jake, or capture one and go through it
   beat by beat, and separate the three candidates:
   ① **Presentation** — too much at once, no pacing, no emphasis. ② **Legibility of state** — you
   cannot tell what a unit IS mid-fight, what statuses are on it, or why it did what it did.
   ③ **UI quality** — the shell screens are functional-but-plain; density, hierarchy and typography
   were never passed over. Likely all three, in different measure.
   **STATUS 2026-07-26: all three have now been built against. The first player pass found build
   integrity failures before it could judge them; the corrected player has not been watched.** The
   "name which before building" instruction above was overtaken by events — three sessions built
   answers to all three candidates. So this item is no longer DESIGN or BUILD, it is **VERIFY, and
   the only person who can advance it is Jake** (see the four threads below). (Superseded detail:
   the beat sequencer and hit-stop, described below as "still unbuilt" in the 07-24 wording, landed
   in `a1fcf8b` the next day. They have never been seen in motion.)
   **Candidate ③ now has its third real pass:** ADRs 0020–0021 replace the over-dense board-first
   workspace with distinct Management / Wager / Deployment / Combat states, exact card grammar,
   a result gate that preserves the fight receipt, a spatial Hourstone Table, bespoke station
   workspaces, landscape mobile compositions, runtime hover/focus/tap disclosure, and large
   management/combat inspectors. Treat
   between-fight UX as VERIFY/polish from play, not as the same untouched problem.
   **First cause named and fixed, 2026-07-24 — movement (ADR 0018).** Jake: *"everyone
   teleports instantly."* It was structural: a move was decided and applied in the same tick,
   so `MoveInterval` was a cooldown between teleports and no client easing could honestly
   smooth it. Movement is now a **committed step** — depart, travel, arrive — and the renderer
   interpolates across the sim's own window. **That is one item off candidate ①; the rest of
   ① (pacing, emphasis, hit-stop, the decoupled clock) is still unbuilt, and ② remains
   untouched.**
   **Second cause of the SAME complaint, named and fixed 2026-07-26 — the opening (render-only).**
   Jake, on the corrected build: *"the start of combat is really jarring — once you press start it
   seems like every unit teleports somewhere; we need normal traversal."* ADR 0018 fixed *walking*;
   this was **leaping**. Tick 0 is the busiest tick of a fight — both lines step off and every
   AtStart trigger resolves — and among those triggers is Ambush (Shade passive) / the Diver role
   (Gloamstalker), an authored cross-board Leap. Headless probe: **24% of run fights open with 1–2
   instant leaps averaging 5.1 hexes** on a board whose longest traversal is 9. Three render defects
   stacked, no sim change: (a) the Arc tell's air-time was a flat `motionSeconds`, so a 5-hex dive
   and a 1-hex hop both took 0.34 s — the arc's HEIGHT already scaled with the jump but its DURATION
   did not; (b) it fired on the first frame of playback, with nothing before it to read it against;
   (c) between dispatch and the tell's beat-stagger/windup the body rendered on its LANDING hex,
   then snapped back to take off — a second teleport inside the first. Fixed with
   `TellDef.motionPerHexSeconds`/`motionMaxSeconds` (air-time scales with span, 0.34 s → 0.74 s for
   a 5-hex dive), a tuning-owned `playback.openingHoldSeconds` (0.7 s of stillness on the deployed
   formation, folded to tick −1 so the hold shows what the player deployed), and seating the arc's
   offset at the take-off on dispatch. **Verified in the editor**: A/B at 0.45 s shows the Shade
   grounded on the landing hex before vs airborne (y=2.39) mid-board after; play-mode frame
   stepping shows the hold gate, its release, and no landing-hex pop. The Ambush/Diver MECHANIC is
   untouched — Jake's call, 2026-07-26, over deleting the tick-0 leap.
   **Researched plan ready, 2026-07-25 (overnight session) → `Design/fight-legibility.md`.**
   Render-layer inventory + genre research (TFT/Underlords/HSBG/SAP/Mechabellum/BB/LTD2) +
   asset-pipeline survey, synthesized into five phases: 0 repair (post stack regressed —
   DoF/saturation knobs dead, MSAA off, scenes untracked; silhouettes key on Name not
   ChassisId) · 1 legibility grammar, no art (cast sentence, beats/clock/hit-stop, 23/27
   silent statuses + 12/20 silent event kinds filled, byChassis cast tells) · 2 real units
   (KayKit shared-rig route, $0 validate/$150 commit; AI-gen rejected for roster) · 3
   per-ability VFX (packs + Shader Graph telegraphs, vfxId on TellDef) · 4 comprehension
   (damage chart, first-party win-prob re-sim).
   **Jake approved 2026-07-25 ("sold on everything but kaykit — find free/cheaper") → BUILD.**
   **Built same session:** Phase 0 repair (acddbf0) · Phase 1 core — byChassis casts,
   ChassisId silhouettes, beat sequencer + hit-stop, mana-ready flip, segmented ally/enemy
   bars, status tints, registry fills (f788491, a1fcf8b) · Phase 4 sim — FightSummary +
   BattleForecast, 299 tests (113a2de) · end-fight readout with damage shares + died-to
   story (40eb076) · **Phase 2 slice: KayKit FREE-tier minis render on the board** — model
   route settled at $0 (same shared rig + 173 CC0 clips as the declined $150 bundle),
   chassis-mapped bodies + handslot kitbash props + Idle↔Walk controller, primitive fallback
   intact (82b7a6b). **Rounds 2-3 same day:** Attack/Cast crossfades + 9 SFX stings + grim
   atlas recolor + bridge portraits (b3898e8) · Jake's three play-note rounds fixed — text
   sharpness, board-spacing tuning, battle-speed persistence, DoF off (transparent-text
   depth), T-pose/lock-in teleport (through 9b4f861).
   **NEXT ARC — combat spectacle (Jake, 2026-07-25 evening: "go big — the reward for
   playing IS the combat"): BUILD, P0-P4 LANDED same evening.** Scope: core systems +
   proposals 1/2/3/4/5/9; 6/7/10 shelved next wave; 8 (Overtime) its own later slice; full
   asset batch approved. Commits: P0 sim f2ea2f4 (Burn fold bug fixed w/ guardrail, 313
   tests, durations on wire, AbilityIdentity, replay v5) · P1 FX foundation 3c7ab1a
   (VfxLibrary + 6 hand-HLSL shaders + TellDef vfx bindings + ProbeShots harness) · P2
   fields 4841f59 (FieldView: edge rings, scrolling floors, pulses, expiry) · P3 icons
   6590382 (icon rows: glyphs, stacks, countdown rings) · P4 casts 14d3d4b+d205c6a (26
   byAbility rows, era sigils, rationed announce) · P5 death pipeline 6fd1a06 (slump /
   ember dissolve / ash-death graves) · P6 dress f074a1c + assets d18399c (rider echoes,
   Deathless dress, fight-ender slow-mo, camera law, 8 era risers). **ARC BUILT END TO END
   — nine commits, every phase gated: headless compile + event-derived probes + contact
   sheet ×2 → 28/28 byte-identical. Stage → VERIFY: needs Jake's live play pass**
   (fight-ender/camera feel, riser mix + announce density in motion, F1 knobs: field
   brightness / icon size / wall tint / cleric sigil, HP-bar snap vs T3 windups → bar
   tween if wrong; Heal carries no Cause so Boon pulses stay dormant — one-line sim change
   when wanted). Detail: Daily/2026-07-25.
   **S5 byWeapon + per-weapon attack language landed 2026-07-25 (317 tests).** Autos could only
   key on chassis, so combat-spectacle §6's per-weapon table was direction with no data path.
   `TellMatch` now filters on the fold's `WeaponName` (+1, a PEER of chassis — a byWeapon row
   TIES a byChassis one); 11 weapon classes authored with 11 new recipes, plus 2 chassis-lane
   staff overrides proving the compose path. **Gotcha worth keeping:** a weapon row needs
   `byCause: Attack` too, or it ties the `byRanged` fallback at 1 and silently loses on registry
   order — and the gate is honest anyway, since Counter/rider swings are also `EventKind.Attack`.
   One new fixture (`weaponry`) covers the three shop-only classes; contact sheet 32/32
   byte-identical ×2. **Found while probing, NOT fixed:** the target-side impact `punch` balloons
   struck units from scale 0.750 → ~1.03 (+37%), hiding neighbours, HP bars and any arc near them
   — reproduces with all VFX hidden, predates this work, and is a live candidate for Jake's
   "not quite clear what's happening". Detail: `Design/authoring-combat-fx.md`.
   `Design/combat-spectacle.md` (direction: palette law + intensity tiers, cast grammar +
   era sigils, per-signature specs, field/status/attack language, ranked go-big proposals,
   asset manifest) + `Design/fx-runtime.md` (engine: VfxLibrary recipes, Director-stepped
   particles, hand-HLSL shaders, ground substrate, status icon row, death linger, phases
   P0-P6). **Inventory found a real shipped bug: the playback fold diverges from sim truth
   at the first Burn tick** (fold Burn magnitude frozen, icon never clears; affects
   castfest/statusstorm/glyphwar/skirmish fixtures) — fix is P0 regardless of the rest.
   Also: `Cause.Trigger` (2nd-most-common damage cause) has no tell · status durations need
   StatusApplied.Aux2 + replay v5 · ability identity derivable with ZERO sim change (last
   SignatureOverride trait wins; resolver belongs in Warband.Content).
   **Still open from earlier rounds:** Phase 4 client UI (damage chart/forecast) ·
   camera/framing pass · live play-mode eyeball of beats/hit-stop + minis in motion.
   **Management Hall polish, 2026-07-25 → `Design/hall-polish.md` (BUILD/VERIFY).**
   Jake approved the obsidian Tower instrument / living Sand direction and asked for the deep
   reusable system. Foundation now built and Unity-verified: hybrid 2.5D Table/Hall environment ·
   accepted authored iron + living-Sand materials with procedural rejection fallbacks ·
   pooled authored UI sound families + Hall ambience and Android/iOS haptic sink ·
   shared theme tokens + dark scrollers ·
   five code-native vector station sigils · payload-bearing semantic feedback · interruption-safe
   reveal/preview/press/select/attention/route/commit/error recipes · identity-aware staggered
   card/choice reveals · one bounded Painter2D pulse/arc/Sand plane · reduced-motion substitutes ·
   purchase/reroll receipts · result count-ups/death-cause reveals · pinned inspector command dock ·
   F1 UI FX/environment/audio/haptic live tuning + F2 Flow Lab previews. A 38-deliverable
   concept/material/FX/mesh/audio batch was generated and curated; rejected tile-heavy surfaces
   and 1.5M-triangle mesh candidates are quarantined, not shipped. Clean compile/console;
   contracts, route spam, forced phone, and reduced motion passed.
   Second-pass station UX is now built and in VERIFY: compact 60–64 px run ribbon · physical
   overview nameplates · data-first station presentation catalog · short pre-handoff route lock ·
   centered five-offer Market rail · pinned exact action tray · optional blocking dossier · typed
   actions with disabled reasons · Armory item→champion pinning and comparison · distinct
   Warband/Hourstone geometry · one-scroll ownership · landscape-phone composition and portrait
   rotate interstitial. Full-size overview/Market/Armory/Warband/phone captures are clean after
   removing inline card-detail overflow.
   Market offer-card redesign is built and Unity-verified: a dedicated typed scan model/component
   replaces the universal Hall card · recruit/weapon/trinket/Inscription/capacity/sold states share
   one exact-rule grammar · four-metric comparison budget + protected commerce dock · inspectable
   unaffordable stock · held/reroll persistence · responsive selection-follow rail · 16 px rule
   copy and 56 px phone actions. Desktop/forced-phone capture contracts now measure actual rule
   containment as well as footer overlap; the longest authored Fire Glyph rule fits in both.
   Rank/item/forge follow-through is built: typed four-fact profiles · dedicated Rank Up cards with
   guaranteed gains + exact 1-of-2 ADD/SWAP/DEEPEN previews · weapon Mana-per-hit, temper, audience,
   and mastery facts · exact trinket/Inscription rules · stable item identity and invested-Sand
   accounting through equip/resale · explicit act-capped Worn→Honed→Relic forge actions · semantic
   Recruit/Rank/Gear/Bind/Capacity/Equip/Forge feedback recipes exposed in F1 and F2. Mechanical
   copy now comes from one headless grammar over the actual content primitives and fails closed on
   unsupported rules.
   **Approved Workbench overhaul BUILT + UNITY-VERIFIED 2026-07-27.** Market Recruit R5, Armory
   Mode R4, Keyword Tooltip R6, and Equipment Tooltip R6 are the named implementation authority
   (`docs/ui-reviews/outbox/out-of-combat-zero-base-v1/`). Scope: one object-centric Workbench,
   full live-offer/hero/item dossiers, permanent equipment-target rail, mutually exclusive paged
   Armory, custom reusable runtime tooltip layer, structured semantic facts/keywords, and
   centrally tunable motion/FX/audio hooks. No Workbench scrolling. Visible currency uses the Sand
   icon instead of the word, and the redundant `cost and remains` sentence is removed. Live
   checks passed Market/Armory containment, explicit no-`ScrollView`, six field + two reserve
   addresses, keyword/equipment tooltip disclosure and safe-edge placement, semantic contracts,
   and a clean Unity console. The four approved 1280×720 states were captured offscreen and
   inspected.
   **Open polish slices:** final Bind choreography · Rule Preview diagrams · real-device
   safe-area/finger/haptic pass · live audio/motion feel tune.

---



## Recovering the pre-grooming board



This archive holds the Done section and item 1 verbatim. The **complete** pre-grooming file

(1145 lines, including superseded prose the 2026-07-27 rewrite compressed) is recoverable from git:



```

git show HEAD:docs/vault/Projects/roadmap.md          # committed version, 979 lines

git log -p --follow -- docs/vault/Projects/roadmap.md  # every prior state

```



Everything dropped in the rewrite was superseded status prose (stale AGREED ORDER lines, stale test

counts, resolved 'what remains' bullets). Three operational details were caught and restored to the

board: `PlanningWorkspaceStyles.uss` ownership, the hex-centre yardstick for `BuildPreview`, and the

parked Dying Procession extrapolation.


---

## Moved 2026-07-28 — the playtest-gate cleanup (Jake: playtesting is continuous feedback, not a board item; the board now carries only session-workable work). Every block below is verbatim as it last stood on the board.

### agreed order 07-27 (final state) — moved 2026-07-28

### ⇒ AGREED ORDER (Jake, 2026-07-27). Item numbers never change; this line does.
**Reordered from the 07-26 line, for one reason: Jake's play passes are the scarcest resource on
this project, and the previous order spent one on a build with a known, one-number defect in it.**

**1. ~~Item 10 — the impact balloon.~~ BUILT 2026-07-27** — new `impact.punchScale` global dial,
default 0.5, halving every recoil (the heaviest tell was at +90%, not the recorded 37%). Feel is
Jake's to judge; the value is one F1 slider away from any other answer.
**2. ~~Item 11 — Overtime is invisible.~~ BUILT 2026-07-27 (THE WANING)** — clock + warning + storm
tell, render-only, plus an `overtime` fixture so the thing can be seen at all. Root cause was worse
than "no clock": storm damage inherited a tell row with `minAmount: 5` while the ramp starts at 1,
so the first 12 s of overtime drew *nothing*. **Capture verified 2026-07-28** (see item 11); the feel is Jake's.
**2b. ~~Sim/render audit — the three cheap wins.~~ BUILT 2026-07-27** (`Design/sim-render-audit.md`,
items A/C/D of its ranked arc, Jake's selection). `camera.fov` is a tuning field at last · the cast
sigil now outlives its own payoff instead of closing at it · status flashes fire on ONSET, not on
every re-application. All three are F1-revertible. **The measurement is the deliverable**: the audit
also priced the framing question and found the *board shape* caps it, which is item 22.
**2b. THE MUSTER RING — BUILT 2026-07-27 (late). VERIFY.** Deployment now paints the hexes each
placed muster will catch: quiet outlines for every placed muster, filled for the selected hero.
General over the law, not the Banneret — Cleric's Mercy Aura (r2) and Phalanx's Unbroken Line (r1)
were equally invisible before this. One definition (`MechanicalRulePresenter.MusterSeats` +
`RunController.IsDeployable`, 13 tests) so the board and the lock-in validator cannot disagree.
**Board API capture-verified** (`Warband/MCP/Capture Muster Rings`); the `ShowDeploymentOnBoard`
call into it has never run — no live run reached Deploy. **That is what VERIFY means here.**
Open: overlapping musters share one gold, so whose ring is whose is not readable.
**3. JAKE'S VERIFY PASS (item 1). ← NEXT, AND IT IS YOURS.** Both cheap feel wins have landed, so
the pass now judges a build with the balloon halved, the storm visible, the sigils held, and the
status strobe down ~26% in a cast-heavy fight.
**4. ~~Item 1c — THE COMBAT RECAP.~~ BUILT 2026-07-27** — contribution bars, damage composition
and death timeline all ship, folded in `Warband.Sim/CombatRecap.cs` (8 headless tests) and drawn
by a client that computes nothing. The client's **first graph of any kind**. Composition reads
every `Cause`, not the harness's five, so Counter and Trigger get slices — that is the
*"why did my build work"* chart ADR 0016 wants. **Pixels unseen** (Play-Mode-only surface); the
capture path is one menu command and its fixture no longer passes vacuously. See item 1c.
**5. Item 5a — the Inscription engine layer. ← SET BY THE 2026-07-27 ROADMAP REVIEW (Jake).**
The review measured the build against its own budget and found one large gap: **Inscriptions are at
5 of 24**, and that is the layer ADR 0016's north star — *compounding builds that feel like they
break the game* — actually lives in. Everything above it in this list is render and shell. It also
absorbs **item 17** (Silence), and unlike items 4 and 18 it is **not** blocked behind the balance
question the content doctrine parks until playtest #1. Target the twelve-family vocabulary proof.
**5b. THE PERSISTENT INSCRIPTION RAIL — SPEC'D BY JAKE 2026-07-27 after playing the Stilled Bell.**
His words: *"we should take this from guildrun's book … a persistent icon rail somewhere (maybe top
of the screen?) EVERYWHERE … during shop/UI as well. Then the icon should flash and draw a quick
indicator to the affected units."* Supersedes the world-space TextMesh rail (hours old — delete it,
keep its laws: coalescing, fold-driven pips, team-0 only, acquisition order).
**Spec:** ① screen-space UI Toolkit strip, top of screen, in the SHELL's persistent layer so it
survives Management/Wager/Deploy/Fight alike · ② one icon per owned Inscription (programmer-art
glyphs in `PresentationCatalog` — 12 needed, no art dependency) · ③ hover/press tooltip = full rule
via `MechanicalRulePresenter` (same copy as the Hourstone tool) · ④ counter pips under the icon
(fold's `RuleCounters` in fight; static elsewhere) · ⑤ on `TriggerFired`: icon flash + a brief
indicator line from icon to the affected unit's screen-projected position, coalescing under the
passive-onset ration so cast-storms don't strobe · ⑥ fight bridge: `SkirmishController` owns both
the shell surface and `ReplayPlayer`, so the event hook and world→screen projection live there.
**Player laws only (settled with the v8 wire); enemy laws stay on the encounter reveal.**
**REVISED BY JAKE 2026-07-27 before the build went deep:** the rail is a **limited-size icon TRAY**
— compact fixed footprint collapsed, hover "opens" it like a **drawer on top**, and only inside the
open drawer do individual icons give their tooltips. Use the existing `DrawerExpand`/`DrawerCollapse`
cue pattern (sound + motion conventions come free). Collapsed tray: capped width, overflow as
"+N". **IN COMBAT the tray is ALWAYS EXPANDED to the full rail — every icon visible, no hover
needed (Jake 2026-07-27): you must be able to read activations at a glance while fighting.**
Collapsed/hover-drawer behavior applies to the non-combat surfaces (Hall/Wager/Deploy).
**⚠ VERIFICATION IS PART OF THE SPEC (Jake):** follow existing UI conventions (`UiEnvironment`
sheets, accent classes, layout tokens), and prove with SCREENSHOTS that no existing page breaks —
before/after captures of Management/Wager/Deploy/fight at the size matrix (1024x768@130%,
1280x720, 1600x900, 2556x1317, phone), READ BY EYE, per the flex-shrink lesson. Expect rework.
**Build note:** follow `WarbandBarView`'s exact pattern — constructed once in `RunShell.BuildUI`
into `_safeAreaFrame` beside the Warband Shelf, shares `RuntimeTooltipService`; views never touch
it (data via `RunShellModel`, ids→words in RunShell only). Icons: per-inscription glyphs are in
`PresentationCatalog` (all 12, done). `InscriptionRailView.cs` exists and compiles — rework it to
the tray/drawer shape rather than starting over. Fight flash/indicator: `SkirmishController` hears
`TriggerFired` via the player's dispatch and projects unit→screen for the line; respect the
passive-onset ration. The v1 world-space TextMesh rail in ReplayPlayer still awaits deletion.
**5b PROGRESS 2026-07-27 late:** tray BUILT + wired (RunShell persistent layer, QA fixtures push
seven laws, combat pins expanded). Smoke matrix before/after: baseline 14/15, after 13/15 — BOTH
failures are the pre-existing `rail-full` header/Market overlap, now at 2 viewports because the
market row gained a 5th card type (INSCRIPTION offers — the PARALLEL session's content, landed
mid-run; their surface, flagged not fixed). Tray verified by eye on the contact sheet at every
surface/viewport; two seed glyphs were font-tofu, swapped. FIGHT BRIDGE BUILT 2026-07-28 00:xx (`ReplayPlayer.LawDispatched`
→ RunShell → Flash/pips/`InscriptionIndicatorLayer` lines; team-filtered by `RuleTeamOf`), v1
world-text rail DELETED, `make check-client` green. POST-BRIDGE VERIFY DONE 2026-07-28 AM:
smoke matrix **17/17 PASS** (`ui-qa/20260728-093053/`, six captures read by eye at both
viewports) — the two pre-existing `rail-full` header/Market overlaps now PASS, fixed en route by
item 24's header/market reshape; the tray reads correctly on Workbench/Wager/Deploy/Result.
World-rail absence capture-proven (`McpCaptures/verify5b/hourstone_t24/t200.png` + a live
world-TextMesh inventory — only StatusIconRow/feed/clock texts remain). LEFT: VERIFY-in-motion
with Jake (drawer hover, flash/indicator feel).
**6. ~~Item 9 — the options screen.~~ BUILT 2026-07-28 (see item 9). VERIFY in motion is Jake's** —
the last P1 blocker on friends playtest #1 (item 6) is now a modal + three entry points, all seams
pre-existing.
**7. Then re-decide.** Standing candidates: item 1d (camera) · item 19 (measure a human) ·
items 12, 13, 15's unspent event. Items 4 and 18 are one balance question wearing two hats, and the
doctrine holds them until playtest #1.


### item 24 — moved 2026-07-28

**24. WORKBENCH DOSSIER & ARMORY-DRAWER REDESIGN — BUILT + CAPTURE-VERIFIED 2026-07-28
(overnight, Jake's directive). VERIFY: the in-motion feel pass is Jake's.** *"The dossier is
quite crowded … per-type formats … remove the armory tab, keep it like a drawer on the
footer."* Research-first (three UX reports: autobattler shops, card anatomy, progressive
disclosure), decision in **`Design/workbench-dossier.md`**; full build/loop record in
`Daily/2026-07-28.md`. Core moves: section ROLE (Primary/Deferred) replaces width/index
demotion — deferred = compact row + hover, never hidden · signature-first hero dossiers ·
rank-up gains as before→after rows (delta chips deleted) · spec options show the AUTHORED
lexicon one-liner, full generated rule on hover (2026-07-28 AM — machine prose has no sentence
break; Pikewall was the clipped repro; `market-rankup-long` is now the real Phalanx fork) ·
stat rail leads the detail column · armory side column deleted → footer drawer band above the
unit rail, Market always live, drawer-open dossier always compact · peek strip deleted
(2026-07-28 AM, Jake) — the footer ARMORY chip is the one drawer handle, hint state-driven
(`DROP TO UNEQUIP` / `OPEN DRAWER ▴` / `CLOSE DRAWER ▾`) · synthetic passive filler
gone · fixes: comparison-cell overlap, `accent--choice`/`stilledbell` accents, RESERVE wrap,
roster contract vs at-capacity warbands, 1024 cost-digit clip. **Workbench Full Matrix 68/70**
(overnight round 11 `ui-qa/20260728-025334/`; morning round 3 `ui-qa/20260728-091422/`), the 2
residual rows are a 2556×1317 subtitle measurement artifact — capture shows the text intact.
⚠ Process laws from the loop are in the daily note: the matrix leaves play mode ON
(stale-assembly stalls) · Hall base styles leak into `--workbench` scopes (`justify-content:
center` cost five rounds) · the unfocused-Editor player-loop freeze is SOLVED —
`Application.runInBackground` pinned in `WarbandUiQa` (2026-07-28 AM), full matrix ≈ 70 s. Deferred polish recorded in the note:
weapon-tier augmented marking · WHEN/THEN trigger anatomy · text-budget CI · rank pips ·
paradox badge · rule-delta rows clipped at drawer-open (info still on tile hover).


### item 1 (the play-pass gate) — moved 2026-07-28

1. **FEEL & READABILITY — Jake's play pass.** — **VERIFY. BLOCKED ON JAKE, NOT ON BUILDING.**
   This item is now **only the verify gate**; the build work that used to live inside it is split
   out as 1b/1c/1d below, because a gate bundled with unbuilt work can never close.
   **Jake, 2026-07-24, after playing:** *"playing it now still does not feel great for a lot of
   reasons (UI is not great, sim viewing has some issues and is not quite clear what's happening)."*
   Three candidate causes were named — ① presentation (too much at once, no pacing/emphasis)
   ② legibility of state (what a unit IS, what's on it, why it did that) ③ UI quality. **All three
   have since been built against, across five sessions.** Two structural causes were found and fixed
   along the way: **movement** (ADR 0018 — decide and apply in the same tick meant everyone
   teleported) and **the opening leap** (2026-07-26, render-only — 24% of fights open with a 1–2
   instant cross-board dive; air-time now scales with span, plus a 0.7 s opening hold).
   **What the pass must judge, specifically:** fight-ender slow-mo + camera law · riser mix and
   announce density in motion · beat sequencer + hit-stop (landed `a1fcf8b`, never seen) · KayKit
   minis in motion · F1 knobs (field brightness / icon size / wall tint / cleric sigil) · HP-bar snap
   vs T3 windups (→ bar tween if wrong).
   **Known-dormant, one-line fix when wanted:** Heal carries no `Cause`, so Boon pulses never fire.
   **The first standalone pass (2026-07-26) was an INVALID read** — URP Lit/Unlit were stripped from
   the player build, so the board and HP/Mana bars were pink. Corrected build `0.1.260726.1706`
   registers them and mutes the (bad, over-long) generated audio by default. **That build has never
   been watched.** Per the 07-27 order, items 10 and 11 land first.

### items 1e + 1f — moved 2026-07-28

1e. **Responsive Workbench correction pass** — **BUILT + VERIFIED.**
    Jake's 2556×1317 capture found visible command-text escapes, an art-starved always-compact
    Market, and Rank Up split across three redundant internal pages despite a 57/57 structural
    report. Root causes and two one-screen corrections are in
    `docs/ui-reviews/outbox/responsive-ui-v1/`; `01-one-page-choices-r1.png` approved 2026-07-27.
    Focused build complete: typed one-page Rank Up, B/A/S tier ladder + tooltips, contextual
    inline trait labels, visible Market art, independent responsive axes, TextElement/button
    layout checks, semantic diagnostics, and B/A/S fixtures. The reported pending-fork crash
    (`sharpshot|A|-`) is guarded in both the run projection and Workbench action state, with an
    exact live-controller regression. Headless smoke **15/15 PASS** at 1280×720 + 1600×900.
    Full matrix **82/82 PASS** across 1024/1280/1600/2556/3440, expanded-copy, phone, Armory,
    tooltip, route, and rotation states; final captures reviewed under
    `client/TempCaptures/ui-qa/20260727-191233/`. Semantic follow-up complete: authored glossary
    concepts now become themed hover targets inside their rule sentence (`Gain 1 Riposte`) instead
    of consuming a detached keyword row. A dedicated Workbench-only full runner keeps this surface
    independently verifiable; its post-migration matrix is **65/65 PASS** with no scrolling or
    content/action overlap under `client/TempCaptures/ui-qa/20260727-202843/`.
1f. **Persistent Warband footer roster manipulation** — **BUILT, VERIFY.**
    Stable-ID drag/drop now moves into open field/reserve slots and atomically swaps occupied
    slots; Space/Escape provides keyboard placement, and the retained footer owns its drag ghost,
    target semantics, and cancellation. `Warband.Run.Tests` 239/239 and the 59-script headless
    client compile are green. Final gate: Unity console + by-eye `rail-open` fixture capture
    (first attempt found the shared editor correctly leased by another session).

### item 1c — moved 2026-07-28

1c. **THE COMBAT RECAP — a comprehensive, polished post-fight report.** — **BUILT + PIXEL-VERIFIED
    2026-07-27, after shipping broken once.**
    **⚠ READ THIS BEFORE ADDING ANYTHING TO THE RESULT GATE.** The first build was unreadable in a
    real fight (Jake, `inbox/post-match-recaps/`). Root cause: **every element in UI Toolkit
    defaults to `flex-shrink: 1`**, and the gate is capped at `max-height: 94%` of a 900px
    reference viewport. The recap pushed content past that budget, so Yoga did not clip or
    scroll — it **silently squashed every child**. 22px rows resolved to ~11px, their text spilled
    out of the box, and even the *pre-existing* stat cards dropped their values outside their own
    background. Nothing errored. **Everything with a fixed height in this panel is now
    `flex-shrink: 0`**, so an overrun presents as visible overflow a contract can fail on.
    **THE PROCESS LESSON, which cost more than the bug: a green layout contract is NOT evidence.**
    It said PASS over a broken screen **twice**. ① The first contracts asserted min-font and
    single-line width — blind to a vertical collapse, because the font never changed and nothing
    clipped horizontally. ② After fixing that, the phone layout drew composition, timeline and the
    recommendation *on top of each other* and still passed, because overlapping siblings are each
    the right height and each inside the panel. Both were caught by **looking at the capture**.
    `UiLayoutContract` is a regression net, not a substitute for eyes.
    **Four more defects the captures caught, none of which any assertion would have:** the board's
    own world-space end-of-fight readout drew *through* the gate (two post-fight surfaces at once —
    now `ReplayPlayer.EndReadoutSuppressed`) · the QA fixture still printed the three death labels
    the shipping path had dropped, so the capture rendered a screen the game no longer produces ·
    the name label's own `overflow: hidden` clipped its descenders until given an explicit height ·
    the exit buttons hung through the panel border while `RequireInside` on their *row* passed,
    because the buttons overflow the row, not the row the panel.
    **Verified 2026-07-27:** `Warband/UI QA/Run Result Gate Matrix` (new 5-shot mode — the 82-shot
    full matrix is too slow to iterate one surface against, which is why nobody ran it, which is
    why this shipped) **PASS at 1024x768/130%, 1280x720, 1600x900, 2556x1317 and phone**, with the
    fixture at a **six-hero worst case**; four of the five captures inspected by eye.
    **Still unseen: the double-readout fix**, which needs a real fight — the fixture runs no battle.
    All three approved charts ship.
    **Built:** `Warband.Sim/CombatRecap.cs` — the fold from `FightSummary` to the exact rows,
    segments and marker positions the panel draws (contribution · composition · timeline), with
    **8 headless tests** (`CombatRecapTests`). `CombatRecapPanel.cs` + `CombatRecapStyles.uss`
    draw it and compute nothing; `RunShell` builds it at the existing `FightSummary.Build` call
    site; TOP DAMAGE is gone, replaced by a bar for every hero.
    **Why the fold lives in the sim:** a chart fails in arithmetic (shares that don't sum, a bar
    normalised to the wrong denominator, a zero-tick fight dividing by zero), and arithmetic is
    testable headlessly while a Unity panel is not.
    **Two decisions worth keeping:** ① the bar is normalised to the LEADER while the number is
    the share of the TEAM — six even contributors would otherwise each draw a 17% stub;
    ② composition reads `UnitSummary.ByCause`, not the harness's five-way split, so **Counter and
    Trigger get their own slices** — measured on the act-3 boss, the CONTROL axis reads
    Attack 65 / Ability 19 / **Counter 9 / Trigger 8** where DAMAGE reads Attack 92. That
    difference IS the "why did my build work" chart.
    **The cleric case is handled:** a support hero shows `0 · 0%` damage, so the row carries one
    secondary fact and healing leads it — measured 2093 healed on a real fight, which is the
    difference between "did nothing" and "kept everyone alive".
    **Verified:** 485 tests green (268 sim + 217 run) · `make check-client` 0 errors · a second
    compile with `DEVELOPMENT_BUILD` defined to cover the editor-only fixture · `make baseline`
    **byte-identical**, fingerprint `3dba11673c26e858` unchanged — the recap changed no fight.
    Numbers eyeballed end-to-end on real act-3 boss fights across all four probe axes (ASCII
    render of the same fold).
    **NOT verified: a single pixel.** The gate only exists in Play Mode. **The path is already
    built and is one menu command:** `Warband/UI QA/Run Responsive Full Matrix` covers surface
    `result` at `result-nominal` + `result-phone`. Its fixture carried **no recap**, so it would
    have passed vacuously — that is now `CombatRecapPanel.EditorFixture()`, deliberately the worst
    plausible case (a four-digit heal on a zero-damage hero, a name long enough to need its
    ellipsis, five composition slices, clustered deaths, the Waning on the track) so
    `UiLayoutContract` gates something real. New contracts added to
    `ResultGateView.EditorResolvedLayoutReport`; **height on phone is the live risk** — the panel
    is contract-bound not to scroll.
    **Deliberately still text:** the three death lines stay under the timeline. The track shows
    *when* the fight turned, the lines show *what* happened. If that reads as redundant in the
    play pass, deleting them is one line.
    **Original spec below.** — **RANKED ABOVE 5a BY JAKE, 2026-07-27**
    (*"a comprehensive and polished combat recap, with graphs and such"*).
    **Rescoped from "fight comprehension UI"**, which read as a Phase 4 leftover — two words,
    "damage chart" — and would never have produced what was actually asked for.
    **There are TWO post-fight surfaces and the board used to treat them as one:**
    ① the **in-board readout** (world-space text during the end hold) — top-3 damage dealers with
    team share + died-to attribution. This is what `40eb076` shipped. ② the **result gate**
    (`ResultGateView`, the blocking screen) — which is **three stat rows and three death lines**:
    `SAND EARNED`, `ENEMIES FELLED`, `TOP DAMAGE` (one name, one number), then up to three
    `X fell to Y · Cause · 12.4s` lines. **No graph, chart, bar or timeline exists anywhere in the
    client.** Both surfaces are text labels. The recap belongs on ②.
    **⚠ THE POINT: this is a UI job, not a sim job. The data already exists and is already tested —
    the client computes none of it and displays ~5% of it.**
    | already computed & tested in the sim | reaches the UI today |
    |---|---|
    | `FightSummary.Units[]` — per-unit damage dealt/taken, healing done/received, shields absorbed, kills, death tick, killed-by + cause, **`DamagePctOfTeam`** | one unit's damage number |
    | `FightStats` — damage split **five ways** (`AttackDamage`/`AbilityDamage`/`DotDamage`/`FieldDamage`/`TriggerDamage`), plus `Casts`, `FirstCastTick`, `CcTicksSuffered`, `Steps`, `ShotsBlocked` | **nothing** |
    | `FightSummary.Beats[]` — every death: tick, victim, killer, cause, overkill, `KillerInferred` | first 3 lines |
    | `FightSummary.Teams[]` team totals · `UnattributedDamage` | no |
    | `BattleForecast.Run(...)` — re-sim win probability | **zero client references** |
    **Approved scope (Jake, 2026-07-27): contribution + composition + timeline.**
    ① **per-unit contribution** — a row per hero with a damage-share bar off `DamagePctOfTeam`,
    replacing the single TOP DAMAGE row · ② **damage composition** — the five-way Attack/Ability/
    DoT/Field/Trigger split, which is the *"why did my build work"* chart and the closest thing on
    the board to rendering ADR 0016's north star · ③ **death timeline** — `Beats[]` laid out on the
    fight's clock, which also gives the Waning (item 11) somewhere to show as a phase.
    **Deliberately NOT in this slice:** `BattleForecast`. It stays orphaned for now — the per-fight
    re-sim cost is unmeasured, and it is the one part needing more than layout. Measure before
    committing to it.
    **Build notes:** `RunShell:2050` builds the result model and already calls `FightSummary.Build`,
    so the data is in hand at the call site — this is a model + view change, not plumbing.
    `FightStats` is currently referenced only by `ReplayPlayer`, so the result gate needs its own
    fold. Charts must be **code-native** (Painter2D / USS), consistent with the Hall's existing
    bounded-Painter2D pulse and the shared `MechanicPresentation` glyph and colour language — do not
    introduce a charting dependency, and do not invent a second colour vocabulary for damage kinds.
    **Related:** this is also the surface that makes **item 19** (nobody measures a human) tractable —
    a recap that shows a player their own contribution is one step from recording it.

### items 7 + 8 — moved 2026-07-28

7. **A run cannot be saved** — **DONE 2026-07-26** (412 tests, verified on Windows). Shell wiring
   (does CONTINUE appear cold, does clicking it resume, does autosave fire) is **Jake-only** — see
   the Play Mode gotcha. Settled item 16 on its back.
8. **Standalone build + launcher/delivery** — **DONE 2026-07-26.** Build, launcher, publish pipeline,
   and the public site are live; the launcher pulls the real build through the real site. Two shader
   landmines were real and are now guarded in the build itself. Remaining: one visual recheck of the
   corrected build before publishing it (folds into item 1's pass).

### item 9 — moved 2026-07-28

9. **No player-facing options at all** — **BUILT 2026-07-28, machine-gated green. VERIFY: the
   in-motion click-through (menu button, fight button, Esc, audible sliders, live speed change) is
   Jake-only — Play Mode.** The screen is a MODAL over the shell's persistent layer, so one
   implementation serves Menu, Hall and fight: `OptionsPanel.cs` (scrim + `.modal`, applies
   instantly, no OK/Cancel) over `PlayerOptions.cs` (PlayerPrefs store; the only new state).
   Entries: OPTIONS on the menu · OPTIONS beside SKIP on the fight overlay · Esc everywhere
   (accepted collision: Esc during an armed keyboard drag also opens it — Esc again closes).
   **Seams, none invented:** sound on/off + Master/Interface/Battle sliders drive the mixer's
   exposed params via `SfxPlayer.SetBusVolume` (**param names verified against the real
   `GameMixer` asset with a negative control** — a wrong name is a SILENT no-op, the one failure
   mode this build could not afford) · mute = MasterVol −80 dB, so the per-surface enables in
   tuning.json/HubPresentation.json stay shipped defaults · reduced motion reuses the
   `ui.reducedMotion` key + the Flow Lab toggle's exact Rebuild seam · battle speed is a
   0.5–2× multiplier over tuning's `playback.ticksPerSecond` at ReplayPlayer's two read sites;
   a live fight re-reads it through `ReapplyTuning()` (the F1 cockpit's proven path).
   **Verified:** `make check-client` 61 scripts 0 errors · smoke matrix now 18 items
   (`options-nominal` surface + `EditorOptionsLayoutReport` contracts; loaders close the modal so
   it cannot haunt later captures) — **18/18 PASS**, capture read by eye
   (`ui-qa/20260728-095256/`) · scrim measured correct (0.78 alpha composited in LINEAR space —
   it reads lighter than gamma intuition; same class as every existing modal). **Full matrix run
   same day: 90/92** (`ui-qa/20260728-095949/` — the 2 fails are the documented pre-existing
   2556×1317 subtitle measurement artifact, byte-for-byte the same rows as item 24's baseline);
   options PASS at all 5 viewports, phone + 1024-expanded captures read by eye.


### items 10 + 11 — moved 2026-07-28

10. **The impact `punch` balloon** — **BUILT 2026-07-27. VERIFY: the number is measured, the FEEL is
    not — it is part of Jake's pass (item 1).** Every unit idles at world scale **0.750**; 0.10 s
    after being struck victims sat at **1.026–1.035**, covering neighbouring units, their HP bars,
    and any arc near them. It **reproduces with every VFX instance hidden**, so it predates the whole
    spectacle arc: a swing's own tell was competing with the victim ballooning over it.
    **Confirmed structurally 2026-07-27:** bars, nameplate and status icons parent to `Root` while
    the punch scales `Body`, so a struck unit never inflates its OWN bars — it grows outward over its
    NEIGHBOURS'. Adjacent hex centres are 1.992 world units apart.
    **⚠ The recorded "~37%" understated it.** 29 of 72 tell rows punch, `punchAmount` spans
    0.18–0.50, and the heaviest row at t=1 reached **+90% — world scale 0.750 → 1.425, near double.**
    **⚠ `impact.punchBoost` alone could NOT fix this** (the 07-26 note assumed it could). It scales
    only the magnitude TERM: driving it to 0 leaves each row's flat `punchAmount` (+25% median)
    untouched *and* destroys the small-vs-big-hit difference `ImpactTune` exists to express.
    **Shipped instead: `impact.punchScale`** — one global dial over every recoil, base included,
    default **0.5**, F1-tunable, hot-reload. Four lines (`TuningData`, `ReplayPlayer:765`,
    `tuning.json`, `tuning.ranges.json`). `PopulateObject` binds by name and the C# default matches
    the shipped value, so a stale `tuning.json` degrades to the intended punch rather than zeroing it.
    | | before | after |
    |---|---|---|
    | median tell, chip hit | +25.0% | +12.5% |
    | median tell, big hit | +45.0% | +22.5% |
    | heaviest tell, big hit | +90.0% | +45.0% |
    Gate: headless client compile 0 errors, **negative-controlled** (injected error caught in the
    changed file, clean after revert). **Not watched in motion — nobody can (Play Mode is unreachable
    from a session).** If 0.5 is wrong, it is one F1 slider, no rebuild.
11. **Overtime is completely invisible — a pillar renders as nothing.** — **BUILT 2026-07-27 (THE
    WANING). VERIFY: machine-gated green, never seen — the Unity lock was held by Codex all session.**
    `Battle.OvertimeStartTick = 900`, after which `Cause.Storm` deals ramping damage to every unit
    every tick. The pitch calls this a pillar (*"escalating overtime clock guarantees resolution"*)
    and theme.md names it **the Waning**.
    **⚠ The root cause was worse than "no clock", and it is worth keeping.** Storm damage had no tell
    of its own, so it fell through to the **generic `DamageDealt` row, whose `minAmount` is 5** —
    and the ramp *starts at 1*. So the first **12 seconds** of overtime drew literally nothing, and
    from damage 5 on it drew **ordinary orange damage numbers with no attacker.** "Units started
    dying for no reason" was not an exaggeration; it was a precise description.
    **NO SIM CHANGE — this was render-only all along.** `Cause.Storm` damage events were always on
    the wire. (`EventKind.StormTick` is declared but **never emitted**; only the enum and `EventText`
    reference it. Do not build on it without emitting it first.)
    **Built:** a world-space **Waning clock** over the board with three states — elapsed `M:SS` ·
    `THE WANING IN M:SS` once inside `warnLeadTicks` (default 150 = 15 s) · `THE WANING — N/TICK`
    showing the storm's CURRENT per-tick damage, the only thing on screen that says *getting worse*.
    Two latched feed beats ("The Hour is running out", "THE WANING — the storm takes everyone") that
    re-arm on a loop wrap. A `byCause: Storm` tell row so storm damage stops borrowing ordinary
    combat's number. All of it lives in a new `waning` tuning block (show/size/height/warnLead + 3
    colours), F1-tunable and hot-reloadable.
    **Design call worth knowing:** the storm renders **globally, as one clock — numbers and punch are
    deliberately OFF on the storm tell.** It strikes every living unit every tick, so per-body
    numbers would be ~40 floating numbers a second and every unit ballooning at once would be item
    10's defect with the volume up. The clock carries the state, the feed carries the two moments.
    **New render fixture `overtime`** (`scenarios.json`, data-only — `Scenarios.cs` was untouched
    because Codex owned it): a warden/lifebinder mirror stalemate that runs **1083 ticks** with
    **931 storm damage events over ticks 900–1082 ramping 1→7**, and all 3 deaths after overtime
    opens. Nothing could see this feature before; now anything can.
    **Gates:** 460 tests green · client compiles (negative-controlled harness) · the clock's readout
    formula reproduces the fixture's real storm output **exactly at both ends of the ramp** (1 at
    tick 900, 7 at tick 1082) and its `M:SS` agrees with the toolchain's own 108.3 s · **all 10
    pre-existing replays regenerated byte-identical**, which also independently proves Codex's
    uncommitted `Scenarios.cs` change is behaviour-preserving.
    **⚠ THE OWED CAPTURE WAS TAKEN 2026-07-27, AND IT FOUND A BUG — in the capture path itself.**
    The instruction here used to read *"`BuildPreview(tick)` routes through `LayoutStory(true)` →
    `LayoutWaning`, so a capture at tick ~950 verifies the clock in edit mode"*. **It does not.**
    `LayoutWaning` reads `Mathf.FloorToInt(_clock)` — the PLAYHEAD, in ticks — and
    `BuildLoadedPreview` set the fold to the requested tick but **never moved the playhead**. So
    every frozen capture computed `tick = 0` and drew a flat **`0:00`** no matter what was
    previewed: at tick 950, fifty ticks into the storm, the clock read `0:00`.
    Play Mode was always correct (`Update` advances `_clock`); it is the *verification* path that
    could not tell the truth — which is the path every check in this project runs through, so the
    blast radius is wider than this one clock.
    **Fixed:** `_clock = tick` in `BuildLoadedPreview`. Hand-checked against the formula
    (t=700 → `1:10` · t=800 → `THE WANING IN 0:10` · t=950 → `THE WANING — 2/TICK`).
    **SEEN 2026-07-28 AM.** Edit-mode captures at t=800 (`THE WANING IN 0:10`, warning gold) and
    t=950 (`THE WANING — 2/TICK`, storm red) verified in pixels AND in the live world-text
    inventory (`client/McpCaptures/verify5b/`) — the readout matches the hand-checked formula at
    both states, so the `_clock = tick` fix is confirmed in the real capture path. The blocking
    `WarbandMixerTools.cs` CS0122 is gone (step 4 landed). The feel is still Jake's.


### item 12 — moved 2026-07-28

12. **Enemy disclosure stops short of the deep inspector.** — **partly addressed.**
    `pve-encounters.md` requires attacks, signatures, passives, triggers, **and targeting rules**
    inspectable before deployment. ADR 0024 added per-unit role + behavior notes to `EncounterBrief`
    (a Sanddrift Gunner's "acquires FARTHEST, holds standoff 5" is now disclosed). Still open: the
    deeper inspector — full signature/passive text on an enemy, as Muster cards already do for heroes.

### item 14 — moved 2026-07-28

14. **Act identity** — **DONE 2026-07-26 (mechanically), BUT REQUALIFIED 2026-07-27.** Acts draw
    genuinely different pools and acts 2 and 3 are *disjoint*. Two new encounters, zero new roles.
    **What it bought is thinner than "done" suggests:** the two encounters authored specifically to
    give act 3 an identity — **The Slagworks and The Long Procession — both measure FREE + FLAT at
    act 3**, the act they exist for. So the pools differ by name and composition while posing the
    same nothing. Do not re-open this as a composition item; it is the same balance wall as **item
    18**, and the honest status is "disjoint pools, no differentiated difficulty".

### item 15 (original) — moved 2026-07-28

15. **~~The Interlude is a non-choice.~~ STALE — the claim was wrong. Corrected + FIXED 2026-07-27.**
    The Interlude **is** a real three-way decision and has been since ADR 0019: `BuildInterludeBeat`
    offers Treasury (certainty) / Armory (equipment) / Hourstone (a run-wide rule), each drawing up
    to `RewardChoices` distinct offers, and the choice **also unlocks the next field capacity**.
    Anyone taking the old item at face value would have built a system that already existed.
    **The real defect was a copy contradiction, and it is now fixed:** the map node still announced
    *"A QUIET STRETCH — No one contests the road. Take the coin and move on"* with a `TRAVEL ON`
    button — telling the player to skip the decision the game was about to hand them, one screen
    later. Now reads AN INTERLUDE / "Take certainty, equipment, or a run-wide rule — and the field
    slot that comes with it" / `TAKE THE INTERLUDE`.
    **Still genuinely unspent:** the content budget funds **one EVENT** — a risk/reward beat with a
    real gamble, distinct from a reward pick. Nothing like that exists. That is the live remnant of
    this item, and it is DESIGN (tiny).

### item 17 — moved 2026-07-28

17. **Silence is disclosed but unplayable — a shipped honesty defect.** — **DONE 2026-07-27:
    shipped as ADR 0026 catalog #10, "The Stilled Bell"** (reaction shape: "when an enemy casts,
    Silence the caster 30 ticks" — zero new selector machinery; the Mana-selector build note below
    only applies if it ever becomes a preemptive opener). `roster.md`'s false claim fixed same day.
    Tested (content tests + presenter grammar) and on the badge rail in the `hourstone` fixture.
    **PLAYED by Jake same evening — "worked great." First Inscription verified in a real run.
    His verdict on the same run: the PRESENTATION lags the mechanics → feeds item 1's fix list.** `grep StatusKind.Silence` across `Warband.Content/` returns
    **zero** hits in Kits, Weapons, or Catalog (re-verified 2026-07-27). Players have Stun only
    (Shield Slam, Banner of the Held Line). Meanwhile authored encounters name Silence as an intended
    answer, **in player-facing disclosure text on two of three act bosses**:
    - `Encounters.cs:249` — "Silence and Stun both stop the clock." (Ninth Bell)
    - `Encounters.cs:502` — "Silence stops the clock; Stun holds it." (**Ashfall Battery, act 2 boss**)
    - `Encounters.cs:538` — "Silence stops the bell completely" (**The Waning Crown, act 3 boss**)
    - `Enemies.cs:218` — the Crown's mana gain is *gated on Silence*, so the bell is designed around it
    - `roster.md:210` claims the roster covers "Stun, Taunt/**Silence**, Slow, Haste, Mana" — **wrong,
      also fix this**
    So ADR 0024's disclosure contract advertises a lever the game does not offer. That is a
    content-honesty defect, not a content-expansion request. **Why an Inscription:** it stays inside
    the 24-effect ADR 0017 proof and spends **none** of the hero-kit content budget.
    **Build note:** needs a target selector. `SelKind` has no Mana ordering or Mana threshold today
    (only `BelowHpPct` / `MustHave`), so **"nearest enemy with Mana" is the cheap shape** and a
    highest-Mana selector is the general one. Depends on / lands with item 5a.

### item 19 — moved 2026-07-28

19. **Every instrument measures a BOT. Nothing measures a human.** —
    **BUILT 2026-07-28, same day Jake spec'd it (mobile chat: log every fight, every purchase,
    every tier selection — the full decision trail). VERIFY: one real Play-Mode run writing +
    uploading is Jake's; everything below it is machine-gated green.**
    **Built:** `Warband.Run/RunTelemetry.cs` — pure line formatter, RunSave's law (no IO, no
    packages, hand-rolled JSON), one JSONL line per event: `start` · `fight` (kind, tier,
    encounter, outcome, ticks, per-hero dmg/pct/healed/died, party+paths — the tier is chosen at
    the wager that resolves the fight, so this line IS the tier-selection record) · `buy` ·
    `reroll` · `slot` · `reforge` · `sell` · `interlude` · `bossReward` · `victory`/`defeat`.
    Run id = seed + content prefix, **stable across save/resume (tested)**; every line
    re-simulable by construction. **5 headless tests** verify the writer against System.Text.Json
    as the independent parser, hostile ids included (519 total green).
    **Client:** `RunTelemetryWriter.cs` appends to `persistentDataPath/runlog.jsonl` beside
    `run.save`; RunShell hooks at BeginRun/resume, BuyOffer, Reroll, BuySlot, Reforge, sells,
    Interlude, boss reward, and fight resolution (brief captured BEFORE resolving — the node
    advances). Fights are the only run-enders, so victory/defeat logs exactly once, then uploads
    fire-and-forget. **Every hook is fail-silent by design** — telemetry can never break a
    purchase. `make check-client` 62 scripts 0 errors.
    **Site:** `POST /api/runlog` (`site/runlog.go`) — static-key spam gate (404 either way),
    1 MiB cap, one file per UTC day under `WARBAND_RUNLOG_DIR` (default `~/warband-runlogs`),
    single-write append so concurrent uploads can't interleave. **Smoke-tested end to end
    locally** (404/404/204/204/413 + file contents). **DEPLOYED 2026-07-28 (Jake's tap) and
    verified against the LIVE site**: healthz ok · unkeyed POST 404 · keyed POST 204 · the test
    line landed in `~/warband-runlogs/2026-07-28.jsonl` (removed after). The sink is listening;
    the next finished run anywhere is the first human data point.
    **Original finding, kept as the argument:** `run.*` is a default-policy bot
    (no placement, no purchase decisions) over 120 runs/tier; the `--enc` "naive line" is a
    fixed-comp bot at 2/12. **Both are floors, not forecasts** — the whole point of the game is the
    two levers the bots do not pull. So the honest state is: *we do not know the human win rate, and
    we have no way to find out.* ADR 0001 says playtests decide and the content doctrine parks
    balance until playtest #1 — but **nothing on this board captures what playtest #1 yields**, so
    the decision it is supposed to settle would arrive as anecdote. Cheapest honest version: the run
    already serialises (item 7) and every fight is re-simulable from (seed, snapshots,
    contentVersion), so a per-run outcome line appended locally is most of it. Settle **what to
    record and how it comes back** before friends play, not after.


### items 20 + 21 — moved 2026-07-28

20. **The passive layer has no renderer** (audit headline **B**) — **BUILT 2026-07-27. VERIFY:
    machine-gated green, never watched** (Unity lock held by Codex all session).
    `Design/passive-legibility.md` has the research, the laws and the measured cost.
    **What it was:** `StatRule` — the read-time conditional stats that ARE the passives (Full Draw,
    Burning Hours, Grudgekeeper) — emitted **no event, ever**, and `Trigger` emitted anonymous
    echoes. ADR 0016's north star was the one layer with zero visual representation.
    **What shipped:** rule identity stamped automatically at composition from the contributing
    content (`Loadout.AddRules`), so **new content is identified the day it is authored** — plus
    `D.Named()` for authored enemies/bosses and `Catalog.Identify` for banners. Two appended
    EventKinds (`TriggerFired`, `RuleChanged`), a per-tick StatRule transition sweep, replay **v6**
    carrying the rule table, `ActiveRules` on the fold, and a `byRule` tell filter at +2 specificity
    with two fallback rows. **Zero unnamed rules across every fixture.**
    **⚠ THE INVARIANT WORTH KEEPING:** presentation events are dropped in the drain loop *before*
    they spend cascade budget or scan a trigger — so they are **structurally incapable** of changing
    a fight, not merely tested not to. Proof: `make baseline` byte-identical over 129 metrics and
    the content fingerprint still `3dba11673c26e858` (no save invalidated — `RuleId` is deliberately
    NOT hashed, because the fingerprint exists to catch a retune, not a rename).
    **Cost, measured:** `TriggerFired` runs 1.4–7.1/s raw against a ~21/s budget, so
    `fx.passiveOnsetSeconds` (2.5 s) rations repeats — a passive firing every swing is the engine
    running, not news. Net across 11 fixtures **+5.2%**, and it landed where there was room:
    castfest **20.8 → 18.1/s (−13%)**, wallfort 5.7 → 7.1/s.
    **Open:** the `RuleChanged` badge is a transition pulse, not yet a persistent rim while live —
    the fold has the state, so that is a `StatusIconRow`-shaped follow-on best paired with item 21.
    **Still lands with item 5a** — Inscriptions compile to the same `Trigger` atom, so they are
    already covered by this and arrive nameable.
21. **The in-fight hover card is three bars** (audit headline **E**) — **BUILT 2026-07-27. VERIFY:
    the card exists ONLY in Play Mode, so no session can ever see it — Jake-only, full stop.**
    The card now carries: the identity line (chassis · signature · weapon + temper) · HP/Shield/Mana
    · the placement facts (reach, cadence and step in SECONDS, crit, "swings heal") · **the targeting
    rule** ("Acquires the FARTHEST enemy, holds 5 hexes") · **the passive roster, with conditional
    ones marked LIVE or idle** off `ActiveRules` · statuses by Lexicon name rather than enum name.
    **This is where item 20's persistent-state half landed** — a passive coming online is now
    readable, not just a flash you had to be watching for.
    **Wire cost:** the item claimed `PlaybackUnit` already carried everything; it did not. Targeting
    (`TargetPref`/`Standoff`) and each unit's span in the rule table are new → **replay v7**, all 11
    fixtures regenerated. Both are hashed into `HashView`, which is what makes the round-trip check
    prove the wire carries them — and that immediately caught a real ordering bug: `BuildRuleTable`
    ran AFTER the tick-0 snapshot, so every fight's first tick disagreed with the fold.
    **Copy:** `ContentLexicon.Rule(id)` is the single resolver for every id shape the composer emits
    (spec node · chassis · weapon · `weapon/mastery` · `banner.*` · authored enemy/boss · `#2`), and
    `RuleCopyTests` is the CI contract that **no raw id can reach a player-facing card** — 321 rules
    across all 8 chassis × their nodes at Relic+mastered, positive-controlled.
    **Closes item 12's "deeper inspector on an enemy"** — enemies use the same card, and their
    authored rules (Ward, the Bell, the Bond, Death-fed…) are all named.
    **Open:** an ON-BODY mark for a live passive, so it reads without hovering (the fold has it).

### item 23 — moved 2026-07-28

23. **A whole sensory channel ships at zero — and the tooling that would fix it does not exist**
    (audit headline **F**) — **BUILT 2026-07-27, steps 0–6. VERIFY: machine-gated green, NEVER
    HEARD.** Plan + full record: `Design/audio.md`. Step 7 (volume sliders) is a screen and belongs
    to **item 9**, not here.
    **Gate state: `make sfx-lint` PASSES against shipped `Resources/`** — 0 violations, 20/20 board
    ids resolve, all 6 UI families present, no silent weapons.
    **What VERIFY means here:** nobody has heard the mix in motion. Open questions only ears can
    answer — does the −6 dB duck read on a death · do crits cut through at ~9.6 onsets/s · is the
    UI tick right at 41–51 ms · is `State` (the tightest bus at 1.6/3) audibly crowded in overtime.
    Folds into item 1's pass.
    **Original diagnosis, kept because it is the argument for the gate:** A measurement pass over all 35 clips replaces "the stings were bad" with
    three separable, measurable defects. **UI:** 14 of 18 clips carry **0.5–1.0 s** of continuous
    audible content (a click is 40–120 ms; `route_1` is a full second for *moving a resource*), the
    level spread across the set is **20 dB** (`error_1` at −20.7 dBFS is the quietest thing in it,
    seating a unit at −0.0 the loudest), and crest runs 13.7→32.9 dB so nothing binds the set into
    one instrument. `commit_1` starts **157 ms** late — audible input lag. **Board:** 27 sound ids
    referenced, **17 clips exist** (the whole per-weapon `hit_*` layer is authored and mute),
    `riser_cleric` starts 156 ms late so the windup cue lands *after* the windup, and
    `ReplayPlayer.PlaySfx` is **one `AudioSource` + `PlayOneShot`** — unbounded overlap at one
    priority, so at the measured **~9.6 sound onsets/s** Unity culls a `death` sting *by audibility*
    in favour of Burn ticks. **No `AudioMixer` asset exists in the project**, so item 9's sliders
    have nothing to drive. **Root cause is process, not taste:** the gate that "passed structural
    validation" (`hall-polish`) checked that files *imported* — never onset/length/level/crest — so
    a regenerated batch has no reason to beat this one. **Headline finding: length beats voice
    management.** Cutting impacts 0.8 s → 0.2 s takes sustained concurrency ~8 voices → ~2.
    Plan: two policies over one substrate (ported subset of Shoota's `SfxPlayer`; **no FMOD/Wwise**,
    **no `AudioRandomContainer`** — it is an editor asset per family and fights JSON hot-reload)
    plus the missing `sfxlint` / `sfxbake` / audition-sheet tooling.
    **Jake decided 2026-07-27:** build steps 0–2 now (D5) · **cut** the Hall ambience bed (D1) ·
    **collapse** the 11 per-weapon impacts to ~5 material families (D3). D2 (re-bake vs regenerate)
    and D4 (combat bed) answer themselves off the audition sheet.
    **STEPS 0–2 BUILT 2026-07-27** — `tools/sfx/sfx.py` (measure/lint/bake/sheet/density) +
    `families.json` + five `make sfx-*` targets. **28/28 clips baked and passing**; UI ticks land at
    **41–51 ms** with ~1 ms onset, and the set now sits in a **±2 dB** window where it spanned 20 dB
    (`error` alone went −20.7 → −4.0 dBFS). Only **3** clips are genuinely missing after the D3
    collapse, down from 10. Working files are under `docs/audio/`, **deliberately outside
    `client/Assets/`** so Unity never imports them; **`Resources/` was not touched, so the game is
    bit-identical** — promotion is step 5, with the code change that renames the families.
    **ENDINGS PASS 2026-07-27** — Jake reviewed: *"much better than before … overall massive
    improvement"*, one defect: *"some def end really abruptly."* Measured it: **12 of 28 clips were
    cut while still near full amplitude** (`riser_phalanx` at **−3.0 dB**) with the same 12 ms fade.
    Two causes — ① one fade served two different endings (natural decay vs cap truncation), now split
    into `fadeOutMs` 12 ms linear and `releaseMs` 60–160 ms **exponential**, inside the length budget
    so density is untouched; ② **the caps were a board law applied to surfaces with no density
    problem** — `bind`/`major`/`error` are once-per-interaction and `riser_*` is a one-per-cast
    windup that §5.2.3 says *should* be long. Raised those, held every family that repeats. Cost:
    concurrency 4.9 → 5.5, well inside the per-bus caps. **28/28 pass; every clip now ends ≤ −34 dB.**
    Two tool bugs found by verifying rather than assuming: truncation detection was a **one-sample
    coin flip** on zero crossings (`cast_generic` missed by one and shipped gated), and the dead-tail
    threshold must stay **peak-relative** — the shipped padding is low-level noise, not silence
    (`select_1`: 820 ms of tail at −34 dB rel., but only 20 ms at −60 dBFS), so an absolute floor
    would score the worst clip in the set as clean.
    **STEP 3 BUILT 2026-07-27, compile-verified headless.** `Scripts/Warband/SfxPlayer.cs` — 24-voice
    pool, five buses (`Ui`/`Decisive`/`Cast`/`Impact`/`State`), priority ladder, **per-bus caps so a
    dense class steals from itself rather than crowding another**, same-id coalescing ("bigger, not
    more"), and the duck envelope. Plus `Editor/WarbandMixerTools.cs` →
    `Warband/Audio/Create Game Mixer`, reflection over the internal `AudioMixerController` because a
    `.mixer` **cannot be authored through any public API** (approach proven in Shoota). Bus tree puts
    `Decisive` as a SIBLING of the ducked group, so death/crit ride over the duck — and `Ducked` has
    to exist as an intermediate bus at all because **a mixer param can only be exposed once**, so
    `BoardVol` and `BoardDuck` cannot share a group. Both files are NEW (no collision).
    **Not wired yet — dead code until step 4/5 call it.** Verified by compiling against real Unity
    reference assemblies on homeserv (0 errors); the editor script is syntax-clean but type-checks
    only inside Unity.
    **STEP 4 BUILT 2026-07-27** (once Codex released the lock). `UiAudioDirector` is now a ~90-line
    cue→family adapter over `SfxPlayer`. Hover/tooltip/projection silent · 10 families → 6 · ambience
    bed, its duck, both synthesizers and the hover cooldown deleted, with the dead
    `hoverCooldownMs`/`ambienceVolume`/`commitDuck` config removed from C# *and* `HubPresentation.json`.
    **New law:** an unmapped *cue* is silent (the old `Family()` fell through to `commit`, so under
    clicks-only any future ambient signal would have started clicking on its own); unmapped
    *transactions* still commit. Baked UI clips promoted so the six families resolve.
    **Caught a silent-wrong-answer trap:** `SfxPlayer` tries `{id}_1..n` before bare `{id}`, so
    promoting `error.wav`/`major.wav` left the **stale 1.04 s `error_1`/`major_1` shadowing them** —
    both files exist, both import, both play, no warning. Contract now says `variants: 1` for those
    two so the promotion overwrites rather than hides. *Verify resolution, not copying.*
    **VERIFIED:** all 57 client scripts compile headless, 0 errors — new `make check-client`
    (`tools/check-client-compile.py`) against real Unity reference assemblies, so client changes no
    longer need a Syncthing round-trip + the Unity lock to find an API error.
    **MIXER ASSET: self-creating, waiting on ONE Unity domain reload.** `WarbandMixerTools` now
    carries `[InitializeOnLoadMethod] EnsureMixerOnLoad` (deferred via `delayCall`, guarded by the
    same existence check as the menu item), so the asset builds itself the next time Unity reloads —
    **no menu item, no MCP call, no lock needed.** Anything that reloads the domain does it:
    focusing the Editor, a script edit, a restart.
    **Why not just call the menu item:** `Unity_RunCommand` is **currently unusable for this**. It
    compiles into a library, so top-level statements fail `CS8805`, and a class-shaped payload
    compiles but the harness finds no entry point ("No logs available"). Five shapes tried
    2026-07-27; `Unity_GetConsoleLogs` also returns `totalCount: 0` for everything (a known trap —
    see the `unity-mcp-runcommand-quirks` memory, note 6b). Unity's asset watcher DID import the new
    scripts and clips unattended (their `.meta` synced back), so a reload is the only gap.
    **Deliberately NOT hand-authoring the `.mixer` YAML** even though Shoota's could be adapted: an
    untestable hand-built asset that resolves no groups fails *identically* to having no asset, but
    leaves something in the repo that looks correct. Letting Unity's own API build it keeps the
    self-check (`FindMatchingGroups` on all five buses, logged) meaningful.
    Until it lands, `SfxPlayer` plays unrouted with one warning (no buses, no duck, no volume
    params) — degraded by design, not broken. `audio.enabled` is still `false` regardless.
    **MIXER LANDED 2026-07-27** — the self-healing loader fired on Unity's next reload and built
    `Resources/Audio/GameMixer.mixer`, which synced back. Verified structurally, not just by
    presence: all 5 buses resolve, all 4 params exposed, and **`Decisive` serialises as a SIBLING of
    `Ducked`, not a child** — the one thing that had to be right, or death and crit would duck
    themselves.
    **STEP 5 BUILT 2026-07-27.** 17 board clips promoted · **16 tell rows repointed** onto the 5 D3
    families (dangling ids 12 → 3) · `ReplayPlayer` routes through `SfxPlayer` with a bus per event
    class and ducks the board −6 dB on a Decisive onset · chip-damage silence law added (guarded on
    `Amount != 0`, or a Cast reporting 0 would be silenced by a threshold of 1 — the status-refresh
    half was already free from item 2b's onset filter).
    **AUDIO IS ON.** `audio.enabled: true` in both `tuning.json` (board, live under F1) and
    `HubPresentation.json` (Hall UI, hot-reloadable). Those two values are the mute until item 9.
    **Design bug caught before it shipped:** a global `SfxPlayer.Muted` written by `UiAudioDirector`
    made the board depend on the Hall initialising — in a fight scene with no Hall it would have been
    silent forever with no clue why. Each surface owns its own switch now.
    **STEP 6 DONE 2026-07-27** — `hit_blunt`/`hit_pierce`/`hit_powder` generated
    (`elevenlabs-sound-effects-v2`, Jake consented) and baked through the same contract. **The gate
    proved itself on first contact:** the raw batch returned the *identical* pathology as the
    original one — all padded to 1.045 s with a **23 dB level spread** — so generating without it
    would have reproduced the exact defect this pass exists to fix. Baked: ±2 dB, 98–232 ms.
    **`make sfx-lint` PASSES against shipped `Resources/`: 0 violations, 20/20 board ids resolve, no
    silent weapons, all 6 UI families present.** First clean end-to-end run.
    **Swept 3.49 MB of dead weight** — 15 superseded clips deleted from `Resources/`, which ships
    everything it contains regardless of references (`hall_ambience` alone was 1.4 MB of a bed D1
    cut). UI 11 clips/360 KB · board 20 clips/1.1 MB.
    **STEP 7 LANDED 2026-07-28 with item 9:** Master/Interface/Battle sliders + the sound switch
    drive the mixer through `SfxPlayer.SetBusVolume`; param names verified against the asset.
    **CAPS PRICED + A ROUTING BUG FOUND 2026-07-27.** `make sfx-density` now also reports per-bus
    pressure against every fixture. Building it exposed that **the per-weapon hit sounds sit on
    `EventKind.Attack` (the swing), not `DamageDealt`** — `Damage/Attack` has no sound row at all —
    so `BusFor` was filing **every weapon hit in the game** under `State`: lowest priority, smallest
    cap, first stolen. `Cast` bodies and `CheatDeath` were mis-filed the same way. Fixed and
    re-priced: peak pressure Cast 1.7/4 · State 1.6/3 · Impact 1.0/6 · Decisive 0.2/4 — **no bus
    steals from itself on any committed fixture**, so nothing silently vanishes. Found by measuring,
    not by reading the code.
    **⇒ NOW IT WANTS EARS, NOT ARCHITECTURE.** Re-audition clips at
    `https://warband.inhouseboyz.com/sfx/`; judge the MIX in motion during the item 1 verify pass. (or `make sfx-serve` locally) — before/after players, absolute-scale
    waveforms, pass/fail. Answer **D2** per family. Steps 3–7 are client work and wait on that.
    The route is `site/sfx.go`: **admin-gated and fail-closed** (`WARBAND_ADMIN_IDS`; unset = 404
    for everyone), which is deliberately NOT the launcher's gate — that one is open to any signed-in
    Discord account, so "signed in" would show every friend the WIP audio.
    **Two findings worth keeping:** ① the density pass says the worst case is **`overtime`, not
    `castfest`** — 9.6 onsets/s sustained for **3.6 minutes**, so THE WANING is the fixture board
    audio must be judged against; ② `lint` caught a real defect in `bake` on its first run (tail
    trimmed before filtering left up to 105 ms of dead tail, which holds a pooled voice open), which
    is the whole argument for the gate.



---

## Moved 2026-07-28 (second re-cut — actionable-only board; Jake). Verbatim.

### session hygiene + STATE + MEASURED snapshots — moved 2026-07-28 (actionable-only re-cut)

**SESSION HYGIENE — RESOLVED 2026-07-28.** The whole 07-27/28 build wave is committed as
`cc058c2` (104 files, 5020 insertions) after Jake confirmed no Codex session was live; `make test`
green at **514 (275 sim + 239 run)** at commit time. `client/TempCaptures/` and `.playwright-mcp/`
are now git-ignored (capture artifacts — synced for review, never history). Keep it this way:
commit at stream boundaries, don't let verified work pool uncommitted.

**STATE, 2026-07-27 (honest):** the first-playable run shape and between-fight UX are walkable end
to end: Menu → five-card Draft → Management Hall → stakes-first Wager → formation-reveal Deployment
→ Fight/replay → blocking result report → spatial Hourstone Table → Victory/Defeat. Three acts ×
five beats, Sand economy, Interludes, boss rewards, terminal loss, save/resume, and a shipped
standalone build + launcher + public site are all implemented. Authored encounters (ADR 0023),
per-act bosses and full disclosure (ADR 0024), and act-scoped disjoint pools are in.
**Combat viewing still does not read well enough, and that judgement is over a year-stale build —
nobody has watched the corrected player.** UI has had five passes (Muster readability, unified
decision cards, persistent Warband Shelf + Loadout Table, shared mechanic presentation, and the
2026-07-27 Workbench overhaul), all Unity-verified by capture, **none watched in motion**.
Detail for every one: `roadmap-done-archive.md` + `Daily/2026-07-26`.

**MEASURED, 2026-07-27 review** (`make baseline`, byte-identical to committed — these are current):
bot run victory **4 / 4 / 7** (stable/fraying/collapsing) · fight win 76% · naive line **2/12 runs
completed** · **4 of 6 node encounters FREE + FLAT at their own debut act** · all 3 bosses admit 3–4
answer axes at spread 100 (**the healthiest content in the game — protect it in any balance pass**) ·
`banneret` still chassis-dead at 13 avg vs berserker 75 · sim health clean (never-swung 0.00%,
deadtime 1.81%) · **Inscriptions 12 of 24 — ADR 0026 wave 2 landed 2026-07-27** (was 5 at review
time, and closing that gap was this review's whole point). The three-act run, the shell, save/resume, the build and
the launcher are all real; **what is thin is the reason to replay it.**


### item 2 — authored PvE content (BUILT header; remnant became item 29) — moved 2026-07-28 (actionable-only re-cut)

2. **Authored PvE content** — **BUILT (ADR 0023 + ADR 0024). Header corrected 2026-07-27: bosses and
   disclosure are DONE, not remaining.** Five authored enemy roles (Swarm/Anchor/Artillery/Ritualist/
   Diver) compose six node encounters across act-scoped, **disjoint** act-2/act-3 pools, each posing a
   placement problem and disclosing its rule; three per-act bosses close the acts.
   **Composition is the act lever, stats are secondary**; an act's pool is its identity.
   Laws: `Design/pve-encounters.md`; decisions: ADR 0023, ADR 0024.
   **What remains:** ③ bespoke enemy art + per-role tells (enemy CARDS no longer borrow hero
   names/portraits, but board silhouettes still do) · ④ risk-tier mutation of authored encounters
   (tiers only scale stats today) · ⑤ **still not watched in Unity** — two boss render fixtures exist
   (`boss-ashfall-battery`, `boss-waning-crown`), so this is one session away.
   → **The "encounters are FREE" finding is now item 18.**

### item 4 — the pressure tier (parked: balance doctrine) — moved 2026-07-28 (actionable-only re-cut)

4. **The pressure tier is a fake choice — AND THE PREMISE INVERTED. Re-measured 2026-07-27.** —
   **DESIGN.** Stable/Fraying/Collapsing are visible. This item has now been true for two OPPOSITE
   reasons, which is exactly why it must be re-measured before it is designed:
   | measured | stable | fraying | collapsing | the reading |
   |---|---|---|---|---|
   | 2026-07-23 | ~99 | ~99 | ~99 | risk is free — everything wins |
   | after ADR 0022 | 88 | 92 | 79 | |
   | after ADR 0023 | 35 | 48 | 39 | Fraying beats Stable |
   | **2026-07-27 (current)** | **4** | **4** | **7** | **the run is near-unwinnable — and Collapsing STILL wins most** |
   So the old sentence "victory saturates ~99%, Collapsing strictly dominates at zero risk" is
   **8× stale and points at the wrong bug.** What survives is the *shape* of the defect: the highest
   risk tier still posts the best victory rate, so the tier costs nothing. What changed is the floor.
   **Caveat that must travel with these numbers:** `run.*` is a **default-policy BOT** over 120 runs
   per tier — it does not choose placement or purchases. It is a floor, not a forecast. See item 19.
   ADR 0007's economy is placeholder either way. **Always start from a fresh `make baseline`.**

### item 16 — defeat/retry rule (settled guard) — moved 2026-07-28 (actionable-only re-cut)

16. **Defeat/retry rule — SETTLED 2026-07-26, no work item.** **Jake's call: terminal loss STAYS —
    the mitigation is save/resume (item 7), not a retry currency and not softening the encounters.**
    Recorded rather than deleted so the next session does not re-open it. Do **not** tune act 2's node
    pool down to address run length; if the cliff hurts, it hurts real playtesters first (ADR 0001).

### item 18 — encounters pose no problems (parked: balance doctrine) — moved 2026-07-28 (actionable-only re-cut)

18. **The authored encounters do not actually pose problems — PARKED BY DOCTRINE, not solved.** —
    **measurement, not a work item until playtest #1.** Promoted out of a Done-entry footnote because
    it directly contradicts item 2's premise. **Re-measured 2026-07-27 — and the shape is worse than
    "4 of 6": the failures are at the acts each encounter was AUTHORED FOR.**
    | encounter | debut | at its debut act |
    |---|---|---|
    | The Gnawing Hour | 1 | **FREE + FLAT** (and at acts 2 and 3) |
    | The Long Range | 2 | **FREE + FLAT** (rule fires only 75%) |
    | The Slagworks | 3 | **FREE + FLAT** |
    | The Long Procession | 3 | **FREE + FLAT** |
    | The Ninth Bell | 1 | poses a problem — spread 100, but FLAT at acts 2–3 |
    | The Drop | 1 | poses a problem — spread 100 at every act |
    **The bosses are the counter-example and the thing to protect:** all three admit 3–4 answer axes
    at spread 100. Whatever eventually fixes the node pool must not be allowed to flatten them. **Root cause, found 2026-07-26 and uncomfortable:** *the gap between the four
    answer-axis parties and the weakest legal comp is wider than the band an encounter can sit in* —
    nothing can be made sharp for one without being lethal to the other. Every composition that
    fixed the flatness drove the naive bot line from 3/12 completed runs to **0/12**.
    **That is a BALANCE finding, and the content doctrine parks balance until the interactive
    playtest.** Two further cautions for whoever picks it up:
    ① **Party size is the strongest difficulty dial in the game, and it is not a stat** — The Long
    Range admits 3 answers with spread 100 against three heroes and is FREE from every formation
    against four. Every probe table now prints hero count; always check it.
    ② the earlier ADR 0023 numbers **could not be bisected** (the whole implementation was
    uncommitted), so **re-measure with `make baseline` before trusting any number here.**

### laws pages 5 / 5a / 6 — moved 2026-07-28 (actionable-only re-cut)

### Laws pages (keep their numbers — ADRs and design docs reference them by name)
5. **PvE-first playable loop** — **LAWS PAGE, not a work item** (dissolved 2026-07-26). ADR 0016
   supersedes mandatory ghost bosses: PvE is the product, encounters are authored and asymmetrical, a
   completed run has a final PvE victory, and the winning warband may continue into endless until
   defeated. `IRunContent.Boss(act, rng)` returns an AUTHORED comp · `RunPhase.Defeated` is terminal
   — **lose any fight and the run ends** (Jake's PoC rule) · `Victory` = reached the end of the last
   act, NOT the old best-of-5 `BossWins >= 3` · ghost-capture removed.
   **`RunController.PreviewEnemies(tier)` exists because the encounter rng derives from private salts
   — never reconstruct a preview client-side**, it will show an army that does not spawn.
   ADR 0019 + 0020: three acts of Fight/Fight/Interlude/Fight/Boss · terminal losses ·
   Stable/Fraying/Collapsing fixed rewards · choose 3 of 5 opening draft · Hall → Wager → Deployment
   → Combat · Sand Market/Armory/Hourstone · 3→6 capacity unlock.
   **Balance law:** preserve spectacular system-breaking engines; intervene only when one line erases
   discovery, all encounter problems, determinism, resolution, or readability.
   **Settled design law** (`Design/pve-encounters.md`): the encounter itself is the boss · every boss
   is a multi-answer strength exam · the boss mechanically rules and teaches its act · enemy
   formations are always previewed before deployment · all mechanics are inspectable before Play, the
   rules known but the outcome not forecast · boss units have **no blanket control immunity**, only
   explicit previewed passives may negate a specific verb · Execute is a true kill preserving normal
   death/transform consequences · Phase grants complete personal absence while encounter clocks
   continue · fields are factional by default, environmental/volatile ones may affect everyone ·
   fight flow is Encounter Reveal → combined Planning → Play → Result, with lineup, equipment and
   positions freely editable together until `BEGIN FIGHT`.
   Remaining scope is tracked elsewhere and must not be re-derived here: risk-tier mutation → item 2④
   · endless seam → item 13 · defeat/retry → item 16 · encounter sharpness → item 18.
   **Parked extrapolation (2026-07-24, never taken up):** the **Dying Procession** — an escalation of
   the Last Oath's bonded pair — remains a possible extrapolation, not current scope.
5a. **Hourstone / Inscription engine layer** — **WAVE 2 BUILT 2026-07-27 (ADR 0026). VERIFY in
    motion sits on the play-pass checklist; wave 3 (12→24) gated on the twelve staying legible in play.**
    Shipped this pass, all machine-gated green (`make test` 492, `make baseline` explained,
    `make check-client`, capture-reviewed):
    ① sim machinery — once-per-root guard (Inscriptions only), `EveryN` counters with
    `RuleProgress` pips on the wire, `AdjacentToAlly` selector, `HealToShield` status,
    `TriggerFired` hook; baseline was **byte-identical** for the guard alone. ② the twelve —
    five seeds renamed (First Bell/Closed Gate/Cinder Law/Bronze Testament/Chorus of Hours) +
    tithe/woundclock/thirdchime/ashbequest/stilledbell/shoulder/bloodless; Paradoxes reachable
    via boss rewards only. ③ Living Inscription replaced Bearer of the Mark (`DoublesBanners`
    deleted everywhere). ④ full `Banner*`→`Inscription*` rename; RuleIds are `inscription.*`;
    replay v8 carries per-rule owning team. ⑤ **the combat badge rail is BUILT** — left-edge
    world-text badges, team-0 laws only, counter pips fold-driven (capture: `hourstone` fixture,
    "The Third Chime 2/3"), pulse+coalesce on TriggerFired. **Unverifiable from a session: the
    pulse/coalesce glow in motion (Play Mode is Jake-only).** In-fight full-rule inspection
    deferred — the Hourstone Table remains the disclosure surface. Fixed en route: `PlayBattle`
    never carried the result's rule table, so LIVE fights resolved passive names against a stale
    file table (item 20 latent). New render fixture: `hourstone` (5 player + 1 enemy law).
    Numbers all placeholder by doctrine — shapes tune in review once heard/seen in play.
6. **Friends playtest #1** — the milestone that ends arguments (ADR 0001), after the PvE vertical
   slice. Distribution/launcher work is allowed only as needed to put that slice in friends' hands.
   **Mechanically nothing blocks it as of 2026-07-28** — items 7, 8 and 9 are built and the site
   is live; item 9's in-motion verify sits on the play-pass checklist. No date until Jake calls it.

### Done lines 2026-07-22 → 2026-07-26 — moved 2026-07-28 (actionable-only re-cut)

- **2026-07-26** — Items 7 + 8: save/resume (verified on Windows) + standalone build, launcher,
  publish pipeline, live site. Cold-start CONTINUE wiring is on the play-pass checklist.
- **2026-07-26** — Item 14, act identity: genuinely disjoint act pools; the "differentiated
  difficulty" half is item 18's balance wall, parked with it.

### Done lines, older — moved 2026-07-28 (actionable-only re-cut)

- **2026-07-26** — Candidate content + first third path (Sharpshot Spotter), authored but unreachable;
  `Kits.Candidate*` registries, `IncludeCandidates` default false, fingerprint provably unchanged.
- **2026-07-26** — Inbox Market UI redesign + equipment preview (455 tests).
- **2026-07-26** — Variable-arity spec offers + seeded pool draw + fork-rank law (455 tests); the spec
  tree was the only deterministic layer in a run. Zero behaviour change, fingerprint identical.
- **2026-07-26** — Act-scoped encounter pools (closed item 14); acts 2 and 3 now disjoint, two new
  encounters, zero new roles (446 tests). Surfaced the balance finding now tracked as item 18.
- **2026-07-26** — Persistent Warband bar + atomic loadout transfers (249 sim + 195 run tests).
- **2026-07-26** — UI proposal slice 1: Hall hierarchy + compact warband bar.
- **2026-07-26** — Balance instruments: 4-axis `--enc`, `make baseline` (104 metrics, A/B by
  `git diff`), `make enc` / `make boss`.
- **2026-07-26** — Routing + the engagement law (ADR 0025): Dijkstra flow field to the engage ring,
  bodies a detour at `BodyCost = 6`. **Watch `BodyCost` at playtest — the one tuning constant.**
- **2026-07-26** — The site is live and the launcher pulls from it (closed item 8).
- **2026-07-26** — First standalone build + launcher/delivery (item 8); the shader landmine was real
  and the preflight caught it.
- **2026-07-26** — Content version stamp (433 tests): computed FNV-1a-64 fingerprint of the content
  graph, not a hand-bumped constant. Replays deliberately unstamped.
- **2026-07-26** — Run save/resume (item 7, 412 tests), verified on Windows.
- **2026-07-26** — Act bosses + the disclosure contract (item 2 ①②, ADR 0024, 392 tests).
- **2026-07-25** — Authored PvE encounters (item 2, ADR 0023, 368 tests); the `--enc` probe.
- **2026-07-25** — Unit behavior layer + weapon cadence + signature patches (ADR 0022, 346 tests).
- **2026-07-25** — The Last Oath's decision is reachable (item 3, 313 tests) — geometry, not numbers.
- **2026-07-25** — Fight-legibility phases 0/1/4-sim + combat-spectacle arc P0–P6, nine commits.
- **2026-07-24** — First-playable run + persistent Planning UX (ADR 0019, 278 tests).
- **2026-07-24** — Playable PoC shell + deployment + scenes (263 tests).
- **2026-07-24** — Render + data systems (item 4b): `scenarios.json`, `TellMatch`, the Lexicon.
- **2026-07-23** — Unity client bring-up (item 4); outlier sanity sweep; hero/build content pass;
  sim mechanics build queue; PvE-first identity amendment (ADR 0016); design campaign complete.
- **2026-07-22** — Design foundation (ADR 0001–0009); sim framework (65 tests); run layer (109 tests).
