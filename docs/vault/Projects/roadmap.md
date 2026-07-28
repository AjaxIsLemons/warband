# Roadmap — THE live board

**This is the ONLY live priority list.** (Pattern from battle/Shoota execution boards —
multiple competing lists is how projects rot.) Sessions plan from here; see CLAUDE.md
"Planning SOP". Keep it honest: the board must match reality better than memory.

**HARD-CUT GROOMED 2026-07-27, RE-CUT 2026-07-28** (997 → ~450 lines): Jake's call — playtesting
is continuous feedback, never a board item, so the play-pass gate (old item 1) dissolved into the
"Next play pass" checklist and every BUILT item moved to Done. Full detail lives in
**`Projects/roadmap-done-archive.md`** (nothing deleted); blow-by-blow logs live in
`Daily/<date>.md`. **This file carries state, priority, laws, and the gotchas that would bite
someone.** If a Done entry grows past two lines, it belongs in the archive.

## Stages
- **DESIGN** — needs a design conversation with Jake before building. Don't build; propose.
- **SPEC'D** — designed (ADR/doc exists), ready to implement autonomously.
- **BUILD** — implementation in progress; note what's left. Finish before starting new work.
- **VERIFY** — built, needs verification/tests/polish before calling done.
- **DONE** — move to the Done section with a date, **one line**, detail to the archive.

## Now / Next (ordered — top unblocked item is "what's next")
🎯 **GOAL (Jake, 2026-07-23): a playable PvE PoC.** **North star (ADR 0016): the fun is
breaking the game with a compounding warband, then seeing how far asymmetrical PvE and
endless pressure can push it.**

**BOARD LAW (Jake, 2026-07-28): playtesting is NOT a roadmap item.** Jake plays continuously and
feeds notes back to sessions; the board carries only work a session can knock out while he is
away. Sessions keep the "Next play pass" list below current so his passes are spent well — but
nothing here waits on a pass unless its gate says so explicitly.

### ⇒ WORKABLE ORDER (proposed at the 2026-07-28 cleanup — Jake reorders at will)
**1. Item 13 — the endless seam** (DESIGN, chat-sized → small build). ADR 0016 identity + a
content-budget line item; the cheapest honest shape is on the item. One chat nod, then build.
**2. Item 15 — THE EVENT** (DESIGN, tiny, chat-sized). The budget's one unspent content beat.
**3. Item 2③ — enemy board identity** (render-only). Enemy silhouettes/tells stop borrowing hero
minis; capture-verifiable without Jake. Boss fixtures (2⑤) ride along.
**4. Item 25 — on-body mark for live passives** (render-only; from items 20/21's open).
**5. Item 26 — muster-ring ownership readability** (small render fix).
**6. Item 27 — Workbench polish batch** (item 24's deferred list, matrix-gated).
**7. Item 28 — dead-view cleanup** (~2,300 lines) — needs Jake's one-word nod before deleting.
**8. Item 1b's two buildable slices** — final Bind choreography · Rule Preview diagrams
(`Design/hall-polish.md`); its device + audio/motion slices stay in the feedback bucket.

### Waiting on play feedback / doctrine (NOT schedulable — do not start)
5a wave 3 (Inscriptions 12→24 — gated on the twelve staying legible in play) · 1d camera + item 22
board shape (taste + DESIGN) · items 4 + 18 (balance, parked by doctrine until playtest data —
item 19's live pipeline now collects exactly that) · item 23 D2 (ears) · 1b's real-device and
audio/motion slices.

### Next play pass — what to watch (sessions: keep current, prune what Jake confirms)
- **Feel:** `impact.punchScale` at 0.5 · the Waning clock + storm tell · cast-sigil hold ·
  status-strobe reduction · the opening hold/leap fix.
- **Fight:** beat sequencer + hit-stop (landed `a1fcf8b`, never seen) · fight-ender slow-mo +
  camera law · KayKit minis in motion · riser mix + announce density · the SFX mix (judge against
  `overtime`, the measured worst case) · a live battle-speed change mid-fight.
- **UI in motion:** combat recap over a REAL fight (the double-readout suppression is unseen) ·
  muster rings on a live deploy · Inscription tray drawer hover + TriggerFired flash/indicator ·
  dossier + armory drawer feel · options (menu/fight buttons, Esc, audible sliders, reduced
  motion) · CONTINUE from a cold start.
- **Known-dormant:** Heal carries no `Cause`, so Boon pulses never fire — one-line fix when wanted.
- **F1 knobs to tune live:** field brightness · icon size · wall tint · cleric sigil.
- **Finishing any run uploads its telemetry** — the first human data point; sessions read it off
  `~/warband-runlogs/` on homeserv.

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

---

### Live items — detail for the workable order above
1b. **Hall polish slices** — **VERIFY/BUILD.** Foundation built and Unity-verified.
    Four named slices open: final Bind choreography · Rule Preview diagrams · real-device
    safe-area/finger/haptic pass · live audio/motion feel tune. `Design/hall-polish.md`.
1d. **Camera/framing pass** — **UNBUILT, waiting on Jake's play feedback** — framing is exactly
    what he'll have opinions about; item 22 owns the board-shape half of the question.

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

(Item 3 — the Last Oath's unreachable decision — is DONE 2026-07-25. The gap is deliberate: item
numbers are load-bearing references, so finished items leave a hole rather than renumber.)


13. **The endless seam does not exist.** — **DESIGN → then small.** ADR 0016's identity and the
    first-playable content budget both include a *"crude post-win continue-until-defeat seam."*
    `RunPhase.Complete` is terminal; nothing in `RunController` continues past the last act.
    theme.md's candidate name is **Beyond the Hour**. Cheapest honest version: on Complete, offer
    CONTINUE, re-entering act 3's pool at escalating scale until a loss.
15. **THE EVENT — the content budget's one unspent beat.** — **DESIGN (tiny), chat-sized.** The
    budget funds ONE authored risk/reward event — a real gamble, distinct from a reward pick;
    nothing like it exists. (The old "Interlude is a non-choice" claim here was WRONG — corrected
    2026-07-27, copy fixed; detail in the archive. Do not re-derive: the Interlude is a real
    three-way choice and also unlocks field capacity.)
16. **Defeat/retry rule — SETTLED 2026-07-26, no work item.** **Jake's call: terminal loss STAYS —
    the mitigation is save/resume (item 7), not a retry currency and not softening the encounters.**
    Recorded rather than deleted so the next session does not re-open it. Do **not** tune act 2's node
    pool down to address run length; if the cliff hurts, it hurts real playtesters first (ADR 0001).

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
22. **The board is square, and that is what caps the camera** (audit headline **G**) — **DESIGN.**
    6 cols × 8 rows is **0.91 : 1**, and a frontally-framed board fills 16:9 only when
    `width/depth = 1.78 × sin(pitch)` — so 0.91 is optimal at **pitch 31°**, which still stacks
    ~2.3 rows of unit UI on itself. **The shape mathematically forbids a pitch high enough to be
    readable**; a pitch/distance sweep found no setting that unstacks the rows without shrinking the
    units. **8 × 8 at ~45° measures 98% frame fill AND 1.37 rows of overlap** — the only point that
    does both. Costs +16 hexes: `Battle.InBounds`, `Pathing.Cells`, every authored formation, every
    deployment fixture. **Justify on framing, measure for balance (`make baseline` before/after) —
    not the reverse.** The rhombus is NOT the answer: yaw costs frame area (55% at today's 13°
    vs 83% at yaw 0), it does not buy any.
    (Audit headline **H** = the combat recap, already ranked as **item 1c** — no new item.)

25. **A live passive has no on-body mark.** — **SPEC'D (render-only), from items 20/21.** The
    fold carries `ActiveRules` and the hover card already marks conditionals LIVE/idle — but
    nothing reads at a glance on the board. A `StatusIconRow`-shaped rim/mark on the body while a
    conditional rule is live (and the `RuleChanged` pulse becoming a persistent rim) closes the
    last gap in the passive layer's renderer. Capture-verifiable.
26. **Overlapping muster rings share one gold.** — **SPEC'D (small render fix), from 2b.** Whose
    ring is whose is not readable when two placed musters overlap; give each owner an accent
    (portrait-matched tint or per-owner pattern). Capture-verifiable via
    `Warband/MCP/Capture Muster Rings`.
27. **Workbench polish batch** — **SPEC'D, item 24's deferred list.** Weapon-tier "augmented"
    value marking on a non-hue channel · WHEN/THEN trigger anatomy for trinkets/inscriptions ·
    compact-card text-budget CI assertion · hero rank pips · paradox-inscription badge ·
    rule-delta rows clipped at drawer-open · `MarketOfferCardModel.Qualifier` dead slot.
    Individually small; matrix-gated (Workbench Full Matrix), read captures by eye.
28. **Dead-view cleanup — NEEDS JAKE'S NOD, then mechanical.** ManagementView/ShopView/
    PlanningView (~2,300 lines) are unregistered since the view-table refactor;
    WarbandCard/CardRulesPopover are only reachable from them. Delete (git remembers) or keep?
    One word decides; the deletion itself is a compile-gated afternoon.

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

## Client — architecture, and where the bodies are buried
Bring-up (item 4), render polish (4b) and the PoC shell (4c) are built and verified; full history in
`Daily/2026-07-23` and `Daily/2026-07-24`. What a future session actually needs:

- **Architecture:** `GameBoot` owns startup order (add a line there — **never** another
  `RuntimeInitializeOnLoadMethod`; four competing ones is why two UIDocuments once raced for input).
  Scenes are **Boot(0) → Game(1)**; Boot holds one `~BootLoader` and nothing else.
  `ReplayPlayer.autoPlayOnStart` is **OFF** by default — the board is driven, never self-starting,
  and the shell parks it on any transition to a non-board screen.
- **Shell pattern (extend, don't fork):** `RunShellModel` (plain view-models) · `IRunScreenView` +
  `RunShellActions` (render out, intent in) · `RunShell` (router, and the ONLY place content ids
  become words). Flow is `ManagementView` → `WagerView` → `DeployView` → board-only Fight;
  management still shares one plain `PlanningModel`.
  `PresentationCatalog` owns art/copy/icon references; composed `UnitDef` owns every mechanical
  number. `WarbandCard` and `InspectorPanel` are shared UXML-backed renderers;
  `PlanningWorkspaceStyles.uss` owns layout/motion tokens.
  **Views may not reference `Warband.*`, so a raw id physically cannot reach the UI.**
  The opening draft is the deliberate exception to universal-card reuse: `MusterCard` accepts only
  three facts and two rules, and exact mechanics disclose inside its portrait.
- **Hydration:** `Warband.Sim.Lexicon` (27 StatusKind + 9 Cause) and `Warband.Content.ContentLexicon`
  (78 spec nodes + 8 chassis). `LexKind` is a **domain, not a valence and not a colour** —
  battlefield colour has one owner, the signature-matched tells in `tuning.json`.
- **Tuning:** `StreamingAssets/tuning.json` + F1 debug cockpit (auto-generates sliders by
  reflection). Hot-reload, no recompile. F2 is the Flow Lab preview.
- **Reused USS across shell and Planning:** inherited *position* is context, inherited *paint* is
  language — override the first, keep the second.

### Client gotchas — these have each cost real time
- **PLAY MODE IS UNREACHABLE FROM A SESSION (found 2026-07-26).** `EditorApplication.EnterPlaymode()`
  inside `Unity_RunCommand` is refused outright: *"User interactions are not supported for MCP tool
  calls."* So **no agent can ever click through the runtime UI** — anything existing only in Play Mode
  (button wiring, shell state transitions, frame-driven feel) is **Jake-only verification, full
  stop.** The workable substitute is a **committed edit-mode Editor script + `ExecuteMenuItem`**
  exercising the real DLLs (`Assets/Editor/RunSaveCheck.cs`, `RenderShots.cs`).
- **`Unity_RunCommand` rejects `System.Reflection`** and its dynamic assembly cannot reference
  Warband plugin types — which is *why* the harness must be a real Editor script. Editor scripts live
  in Assembly-CSharp-**Editor**, so they cannot see `internal` types in Assembly-CSharp either.
- **Refreshing scripts mid-Play-Mode** leaves GameObjects alive but **wipes the code-built UI tree**
  (root has 0 children, console clean). Exit and re-enter Play after every source change.
- **An unfocused Editor idles the player loop**, so `Start()` may simply never have run — same
  symptom, different cause. Pump `QueuePlayerLoopUpdate` before believing a probe. **Sometimes it
  does not recover at all** (2026-07-24: `frameCount` stuck at 1, 40 queued pumps, `isPaused=false`).
  **Verify anything frame-driven in EDIT mode** via `BuildPreview(tick)`, measuring against the
  generated tile transforms as a hex-centre yardstick. Never `Thread.Sleep` in a
  RunCommand to wait for frames — it holds the main thread, so nothing ticks and every sample reads
  identical, mimicking the very bug you are chasing.
- **Exiting Play returns the Editor to the Boot scene**, which holds only `~BootLoader` — so an
  edit-mode probe finds no `ReplayPlayer`. Open `Assets/Scenes/Game.unity` first.
- **Game View capture stalls unattended** (`WaitForEndOfFrame` never completes). Driving the live UI
  tree over MCP is the reliable verification; screenshots need Jake's focused Editor.
- **The Editor and the built player share `Application.persistentDataPath`** — a dev Play Mode session
  and a friend build read/write the SAME `run.save`. That is why a fresh build can appear to "already
  have a run."
- **A check that can silently return "nothing found" needs a positive control.** Cost real time twice:
  a shader grep for invented names reported all six missing, and a `dig` that wasn't installed.
- **Syncthing ignores are PER-DEVICE**, and `sync-status: 100%` can predate the local scan — confirm
  over SSH. **Never write captures inside the Syncthing tree** (they sync back into the repo).
- **`.unity`/`.prefab`/`.asset` edits are guard-hook blocked** — scene work goes through Unity MCP.

## First-playable content budget (hard cap — ADR 0001 + ADR 0016, scope settled 2026-07-26)
Current 8 heroes × 2 paths · 11 weapons + **5 trinkets** · **24 Inscriptions, delivered through the
ADR 0017 proof waves** · **ONE THREE-ACT RUN, one boss per act** (a tiny reusable enemy-role grammar,
several encounters, three act bosses, one event) · shops + placement · crude post-win endless seam
that may reuse and scale the slice · programmer art, no sound.

**Measured against the cap, 2026-07-27 evening** (`make baseline`, fingerprint `b8640a3ea7cd360b` —
moved by ADR 0026: hash schema + twelve Inscriptions + Living Inscription, saves invalidated once):
8 chassis ✓ · 78 spec nodes · 11 weapons ✓ · **5 trinkets** (this line said "1 trinket" until today —
ADR 0022 added four and the budget was never updated) · **12 Inscriptions of 24** ← *was 5 — the one place the
build is far under its own budget, and it is the layer ADR 0016's identity depends on* ·
7 enemy unit types · 6 node encounters · 3 act bosses · **1 event still unspent** (see item 15).
Random hero-kits-as-monsters remain scaffolding, not acceptable final PvE content. Do not expand
beyond three acts, to a full endless mode, or to a catalog beyond the 24-effect proof before
playtest #1.

**Scope decision, Jake 2026-07-26. Three acts is the cap.** (This previously read "one act / one
boss", contradicting ADR 0019's shipped three-act shape and ADR 0024's three bosses.)

## Deferred (explicitly NOT now — don't resurrect without Jake)
**All PvP:** ghost server · matchmaking · ratings/leaderboards · PvP rewards · no-stakes Echo
exhibitions (the snapshot seam may remain, but no feature work) · Displacement (Push/Pull/collisions)
· spoils-of-war (historical ADR 0002) · sim-modeled projectile flight ("dodge by movement" lever,
render-contract) · aura ExcludeOwner option · morale/rout concept · ability crits · predetermined
terrain (NEVER) · account-scoped power (NEVER — fairness law).

**Deliberately NOT proposed** (so the next session does not re-derive them): more heroes, more
weapons, a second trinket family, multi-act expansion, difficulty ladders, PvP-adjacent anything,
and any balance pass on hero kits — all either capped by the content budget or forbidden by the
content doctrine until playtest #1.

## Open design questions (ammo for DESIGN sessions)
- **Bearer of the Mark replacement** — `Inbox/warband_roster_expansion_plan.md` proposes **Living
  Inscription** ("when an Inscription activates, Vespera gains 5% Mana, at most once per root event").
  It scales with *activation* rather than collection size, so it cannot make Vespera mandatory just
  because a run owns many Inscriptions — the exact failure of today's blanket `DoublesBanners = true`.
  Worth taking as-is when the Inscription event layer (item 5a) lands.
- **Wide Banner** reads as "inner circle gets innate+crown" instead of "reach replaces" — proposed as
  the actual design, needs Jake's nod. **ADR 0022 makes it a one-liner:** `SignaturePatch =
  Patch(radius: 1)` grows the Rally radius.
- **Content-fidelity leftovers** (2026-07-23 de-SIMPLIFY pass): **Twist's crit-memory** is a 30-tick
  Mark, not "since last cast" (cast-event ordering) · War-Priest does not yet acquire mace mastery ·
  Tower Shield has no base defensive stat · reforged-item resale does not remember forge spend ·
  returning to an implicit starter resets its temper · Company Standard expresses "Company potency"
  as an adjacent opening-Haste muster.
- **Inscriptions:** pool assignment · first twelve effect contracts · per-root activation
  representation · legacy Banner-data migration.
- **Balance/economy:** risk-tier mutation shape (item 2④) · endless cycle, post-rank-S decisions,
  scaling, score (item 13) · Sand/economy values (ADR 0019 tuning until sweep/playtest) · respec cost
  (free-for-now decided, revisit) · per-rank stat scaling.
- **Named-not-tuned outliers** (recorded so they are not re-discovered): `banneret` is CHASSIS-DEAD
  (avg 13%, best build 18%) · four node pairs lopsided by ≥25 (shade.reaper vs phantom Δ-52,
  sniper.onebreath Δ-47, bulwark.juggernaut vs warden Δ-46, phalanx.pikewall Δ+30) ·
  `shade:reaper+widowmaker` dead at 8–9% · The Long Range's ward never comes off for the `control`
  axis (its disclosed answer never happens) · `reach` cannot clear the act-1 boss at all.

## Done — one line each; full detail in `roadmap-done-archive.md`
- **2026-07-28** — Item 1 DISSOLVED: the play-pass gate became the standing "Next play pass"
  checklist (board law: playtesting is continuous feedback, not an item). History in the archive.
- **2026-07-28** — Item 19, run telemetry: JSONL decision trail (every fight/purchase/tier), 5
  headless tests, fail-silent client hooks, key-gated site sink DEPLOYED + live-verified.
- **2026-07-28** — Item 9, the options screen: modal over the persistent layer (menu + fight +
  Esc), mixer sliders (params verified against the asset), reduced motion, battle speed 0.5–2×;
  full matrix 90/92 (the 2 = the known 2556×1317 artifact).
- **2026-07-28** — Item 24, Workbench dossier + armory-drawer redesign
  (`Design/workbench-dossier.md`): section roles, per-kind dossiers, footer drawer; 68/70 twice;
  process laws in `Daily/2026-07-28.md`.
- **2026-07-28** — Item 5b, persistent Inscription tray + fight bridge: tray/drawer + combat-pinned
  rail, TriggerFired flash/pips/indicator lines, v1 world rail deleted (capture-proven), smoke
  18/18 with the two former rail-full overlaps fixed en route by item 24.
- **2026-07-28** — Item 11, THE WANING: clock/warn/storm states capture-verified at t=800/950;
  render-only (the `minAmount: 5` root cause is the keeper); `overtime` fixture.
- **2026-07-27** — Item 10, the impact balloon: `impact.punchScale` global dial, default 0.5 (the
  +90% worst tell → +45%). One F1 slider to re-judge.
- **2026-07-27** — 2b, the muster rings: one definition (`MusterSeats` + `IsDeployable`, 13 tests),
  board-API capture-verified. The shared-gold open became item 26.
- **2026-07-27** — Item 1c, the combat recap: `CombatRecap` fold in the sim (8 tests), client
  computes nothing; pixel-verified at the 5-shot matrix. The flex-shrink / green-contract lessons
  live in the archive.
- **2026-07-27** — Item 1e, responsive Workbench correction pass (82/82 + 65/65 matrices).
- **2026-07-27** — Item 1f, footer roster drag/drop + keyboard parity (239/239; `rail-open`
  capture green inside the Workbench matrix).
- **2026-07-27** — Item 23, audio steps 0–6: measured bake contract + `tools/sfx` tooling,
  `SfxPlayer` buses/caps/duck, self-built mixer, tells repointed, AUDIO ON; the volume screen
  shipped with item 9. D2-per-family answers on the play-pass checklist (ears).
- **2026-07-27** — Item 17, The Stilled Bell (ADR 0026 #10): the Silence honesty defect closed;
  PLAYED by Jake — "worked great."
- **2026-07-27** — Item 12, the deep enemy inspector — closed by item 21's combat card.
- **2026-07-26** — Items 7 + 8: save/resume (verified on Windows) + standalone build, launcher,
  publish pipeline, live site. Cold-start CONTINUE wiring is on the play-pass checklist.
- **2026-07-26** — Item 14, act identity: genuinely disjoint act pools; the "differentiated
  difficulty" half is item 18's balance wall, parked with it.
- **2026-07-27** — Muster spacing/responsive pass: duplicate role/context copy removed, facts and
  authored rule metadata reflowed, card-width-owned compact composition added, and a dedicated
  six-capture 1024–3440/copy-stress matrix added; **6/6 structural PASS and pixels reviewed** at
  `client/TempCaptures/ui-qa/20260727-233852/`.
- **2026-07-27** — Responsive UI foundation: shared 1600×900 height-locked panel profile, root-owned safe area + portrait guard, independent geometry/form-factor/input/motion classes, semantic type/spacing/hit tokens, route-scoped notices, responsive dossier pages, and a deterministic 57-case Play Mode matrix across Workbench/Wager/Deployment/Result; 57/57 structural contracts PASS. **Uncommitted.**
- **2026-07-27** — Item 21, the in-fight inspector (476 tests, replay v7): identity line, placement
  facts, targeting rule, and the passive roster with live conditionals — plus `ContentLexicon.Rule`
  and a CI contract that no raw content id can reach a player-facing card. Closes item 12's enemy
  inspector. **Play-Mode-only surface — Jake-only verification.**
- **2026-07-27** — Item 20, the passive layer's renderer (`Design/passive-legibility.md`, 471 tests):
  auto-stamped rule identity, `TriggerFired`/`RuleChanged`, replay v6, `byRule` tells + fallbacks.
  Baseline byte-identical and fingerprint unchanged — presentation cannot move the sim. **Unwatched.**
- **2026-07-27** — Sim/render audit (`Design/sim-render-audit.md`) + its three cheap wins (A/C/D):
  `camera.fov` + tunable feed anchor, `castSigilHoldSeconds` (sigils outlive their payoff, full-alpha
  0.03–0.33s → 0.38–0.68s), `statusRefreshQuiet` (onset-not-refresh, −25.8% of castfest's tells).
  460 tests, client compile negative-controlled. **Not watched — Unity lock held by Codex.**
- **2026-07-27** — Workbench overhaul (Market Recruit R5, Armory Mode R4, keyword + equipment
  tooltips R6): object-centric Workbench, live dossiers, selected-trait ribbon, permanent equipment
  rail, paged Armory, runtime tooltip layer, and rail-safe Wager command band; no scrolling.
  Initial 50-case matrix plus correction-pass 1280×720/2558×1313 contracts PASS. **Uncommitted.**
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
