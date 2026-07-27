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
