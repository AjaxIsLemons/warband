# warband vault — manifest

Read this first. One line per page; open only what's relevant. Update on every add/move.

## Design
- [Design/pitch.md](Design/pitch.md) — the current game in one page (v0.4, 2026-07-23):
  asymmetric PvE, broken-build fantasy, authored victory → endless. Start here.
- [Design/heroes.md](Design/heroes.md) — current universal hero anatomy: chassis, Mana,
  duplicate ranks/specs, Weapon+Trinket loadouts, party layer, and PvE commitment flow.
- [Design/roster.md](Design/roster.md) — current 8-hero first-playable PvE contracts:
  identity, forks, encounter contribution, dependencies, weaknesses, and playtest watches.
- [Design/weapons.md](Design/weapons.md) — current 11-weapon system: attack physics,
  universal equip, mastery riders, Worn→Honed→Relic temper, forge law, and fidelity seams.
- [Design/inscriptions.md](Design/inscriptions.md) — Hourstone fiction and persistent
  Inscriptions: ownership, authoring families, cascade law, 24-effect rollout, and badge UI.
- [Design/events-and-inscriptions.md](Design/events-and-inscriptions.md) — plan of record for item 15:
  the Interlude becomes a data-driven event pool, Inscription `Duration`, why hero-scoped
  inscriptions are rejected on evidence, and the 12→24 catalog target.
- [Design/combat-grammar.md](Design/combat-grammar.md) — THE SOUL: clock + field, full effect vocabulary.
- [Design/pve-encounters.md](Design/pve-encounters.md) — partial PvE authoring law: the
  encounter is the boss, it rules and teaches its act, mechanics are disclosed, shared
  combat verbs work unless an authored passive explicitly says otherwise.
- [Design/preparation-and-deployment.md](Design/preparation-and-deployment.md) — accepted
  board-first Planning workspace: persistent formations, capacity-driven Muster Drawer,
  combined roster/loadout/position editing, direct-manipulation rules, one Fight commitment,
  undo/validation, architecture, and polish ladder.
- [Design/sim-framework.md](Design/sim-framework.md) — sim architecture: content atom, cascade semantics, fields, determinism, metrics.
- [Design/render-contract.md](Design/render-contract.md) — how the client is guaranteed accurate: tick=100ms, bars set from absolutes, fold-as-view-model.
- [Design/render-polish.md](Design/render-polish.md) — juice/readability systems design: decoupled
  playback clock, Feedback Director + beat sequencer, tell vocabulary, diorama post stack.
- [Design/directed-tells.md](Design/directed-tells.md) — motion tells (lunge/tracer/burst) + the
  Root-keyed impact latch spec on the JSON tell system.
- [Design/fight-legibility.md](Design/fight-legibility.md) — **2026-07-25 researched plan for
  roadmap item 1**: three failure modes, genre laws, five phases (repair → grammar → KayKit
  models → per-ability VFX → comprehension), costs, and Jake's six pending decisions.
- [Design/combat-spectacle.md](Design/combat-spectacle.md) — **2026-07-25 spectacle direction**
  (fight-legibility Phase 3 expanded): palette law + intensity tiers, cast grammar + era
  sigils, per-signature specs, field/status/attack language, ranked go-big proposals,
  asset manifest, Jake's decision points.
- [Design/fx-runtime.md](Design/fx-runtime.md) — engine spec for combat-spectacle: VfxLibrary
  recipes, Director-stepped particles, hand-written URP shader set, ground substrate,
  status icon row, death linger, sim/wire changes (incl. the Burn fold bug), build phases.
- [Design/sim-render-audit.md](Design/sim-render-audit.md) — **2026-07-27 measurement pass over the
  sim's outputs and the whole render**: event census (~21 tells/s worst case), the two mechanic
  classes that emit nothing (`StatRule`/`Trigger` identity), the cast-sigil lifetime bug, exact
  board/camera geometry (FOV 60, 63 px back rank, 2.95 rows of UI overlap), the rhombus question
  answered with frame-fill numbers, and ten ranked headlines.
- [Design/audio.md](Design/audio.md) — **2026-07-27 audio research + plan (roadmap item 23)**: why the muted UI stings
  are bad (measured onset/length/level over all 35 clips), the ~9.6 sound onsets/s combat density,
  the two-system/one-substrate architecture (mixer buses, priority, same-clip coalescing, the duck),
  the missing `sfxlint`/`sfxbake`/audition-sheet tooling, build order, and Jake's five decisions.
- [Design/passive-legibility.md](Design/passive-legibility.md) — **2026-07-27 BUILT (roadmap item
  20)**: making the engine visible. Genre grounding (Balatro's sequential per-source callouts,
  Underlords' threshold-as-event, HSBG persistent state, SAP's ordering failure), the three laws
  (named source · coming-online is an event · thin executor over the existing tell registry),
  the `TriggerFired`/`RuleChanged` wire, the onset ration, and the extension story for new content.
- [Design/authoring-combat-fx.md](Design/authoring-combat-fx.md) — **the how-to** for adding
  spell/weapon/field/status/death visuals: three change tiers, 5-step new-spell workflow,
  verification gates, and the consolidated next-steps ledger. Skill: `.claude/skills/spell-fx`.
- [Design/hall-polish.md](Design/hall-polish.md) — **approved Hall polish direction; foundation
  built**: obsidian Tower instrument + living Sand language, preview/selection/commit grammar,
  reusable motion/feedback/Painter2D FX/audio-haptic seams, live UI FX tuning, mobile/accessibility
  contracts, and remaining P3–P5 slices.

- [Design/workbench-dossier.md](Design/workbench-dossier.md) — the 2026-07-28 dossier/armory
  redesign decision: section ROLE over width-demotion, per-kind dossiers, footer drawer (research:
  autobattler shops, card anatomy, progressive disclosure).
- [Design/client-architecture.md](Design/client-architecture.md) — client architecture + the
  session gotchas that have each cost real time (Play Mode unreachable, Syncthing, capture paths).
  Moved off the roadmap 2026-07-28.
- [Design/content-budget.md](Design/content-budget.md) — the first-playable content hard cap
  (ADR 0001 + 0016) + measured-against-cap block. Moved off the roadmap 2026-07-28.
- [Design/ui-responsive-contract.md](Design/ui-responsive-contract.md) — **the UI foundation SOP**:
  how UI Toolkit's three responsive layers (panel scale / flex / breakpoint classes) actually work,
  an audit of warband's 14.4k lines of USS against them, and the proposed contract — one classifier,
  one safe-area frame, one token sheet, a 14px body floor, and the QA viewport matrix that gates it.

- [Design/sauce.md](Design/sauce.md) — PARKED: class-identity sauce noodling (two rounds cold);
  superseded by the "wears different hats" quality bar.
- [Design/theme.md](Design/theme.md) — THE LAST HOUR: dying multiverse + Tower frame, binding
  laws, mechanics↔fiction map, class/champion split, era-spread champion riffs.
- [Design/dives/cleric.md](Design/dives/cleric.md) — dive #1 SETTLED: bruiser-healer, censer +
  Sanctified Pyre, front-vs-back fork, 8 named builds; Sister Maren of the Waning Bell.
- [Design/dives/bulwark.md](Design/dives/bulwark.md) — dive #2 SETTLED: tank, Tower Shield +
  Shield Slam, Juggernaut (AoE stun) vs Warden (r3-4 Taunt+Silence, thorns); Brakka.
- [Design/dives/shade.md](Design/dives/shade.md) — dive #3 SETTLED: late-bloomer (fork at A),
  Reaper (crit-fish + Execute) vs Phantom (Phase); Null, the Redacted.
- [Design/dives/sharpshot.md](Design/dives/sharpshot.md) — dive #4 PROPOSED: ranged dps,
  Sniper artillery vs reworked Volleyer (3-line Rooting fan); muskets debut; awaiting Jake.

## Decisions
- [Decisions/0001-identity-and-anti-washout-contract.md](Decisions/0001-identity-and-anti-washout-contract.md) —
  historical original identity + the still-active anti-washout process guardrails.
- [Decisions/0002-run-structure-best-of-5.md](Decisions/0002-run-structure-best-of-5.md) —
  superseded historical best-of-5 spine; recoverable-loss and PvE risk ideas remain inputs.
- [Decisions/0003-combat-soul.md](Decisions/0003-combat-soul.md) — THE SOUL SENTENCE, Clock+Field
  pillars, flat maps/glyphs, displacement demoted, AI legibility.
- [Decisions/0004-sim-framework.md](Decisions/0004-sim-framework.md) — trigger atom, fields=auras,
  cascade semantics, determinism law, tag-change replay, metrics-as-folds.
- [Decisions/0005-loadout-composition.md](Decisions/0005-loadout-composition.md) — items/spec-trees as
  composed loadouts (sim never sees them), remaining sim gaps, PLACEHOLDER-content doctrine.
- [Decisions/0006-shop-and-economy.md](Decisions/0006-shop-and-economy.md) — shop after every node,
  roster 3→6 structure, flat rerolls, bench of 2, gold; slot-offer timing reopened by ADR 0016.
- [Decisions/0007-wager-mechanics.md](Decisions/0007-wager-mechanics.md) — 3 risk tiers per fight,
  per-kill payout + tier-scaled success bonus, no staked gold.
- [Decisions/0008-run-layer-architecture.md](Decisions/0008-run-layer-architecture.md) — run layer =
  pure host-agnostic lib; serializable state, ids-only content, stateless rng; hosting deferred.
- [Decisions/0009-shop-stock.md](Decisions/0009-shop-stock.md) — 3 hero + 2 item offers, per-offer
  freeze, dupe→rank-up forks, legacy team-rule rotation, 50% sell-back; naming/system amended
  by ADR 0017.
- [Decisions/0010-theme-last-hour.md](Decisions/0010-theme-last-hour.md) — theme: The Last Hour
  frame, three binding laws, class/champion naming split; flavor-name candidates pending.
- [Decisions/0011-spec-tree-impact-model.md](Decisions/0011-spec-tree-impact-model.md) — dive
  template: 7 archetypes, ADD/SWAP/DEEPEN fork law, C/B/A/S ladder, variable fork timing.
- [Decisions/0012-weapon-access-model.md](Decisions/0012-weapon-access-model.md) — universal
  weapon equip, class specializations + weapon mastery riders, heal-weapons legal.
- [Decisions/0013-targeting-law.md](Decisions/0013-targeting-law.md) — sticky target; re-acquire
  on death / range-exit / untargetable / Taunt; melee subtlety flagged.
- [Decisions/0014-aura-and-adjacency-law.md](Decisions/0014-aura-and-adjacency-law.md) —
  permanent ally states use muster placement; casts and enemy spatial effects use live geometry.
- [Decisions/0015-weapon-system.md](Decisions/0015-weapon-system.md) — 11-category catalog,
  engine riders, temper tiers, mastery/Relic law, Tower forge.
- [Decisions/0016-pve-first-asymmetric-endless.md](Decisions/0016-pve-first-asymmetric-endless.md) —
  **current identity:** PvE is the product; asymmetric trials, system-breaking builds,
  authored victory into optional endless; PvP deferred.
- [Decisions/0017-hourstone-and-inscriptions.md](Decisions/0017-hourstone-and-inscriptions.md) —
  Hourstone lore, unlimited persistent Inscriptions, chain safety, badge presentation, and
  staged 24-effect target.
- [Decisions/0018-movement-law.md](Decisions/0018-movement-law.md) — movement is a COMMITTED
  STEP: depart now, arrive MoveInterval ticks later, position stays at the origin the whole
  way, destination reserved. A `Move` with no `MoveStart` is a teleport.
- [Decisions/0019-first-playable-run-and-workspace.md](Decisions/0019-first-playable-run-and-workspace.md) —
  three-act/five-beat run cadence, terminal loss, initial Sand economy, Interludes, and the
  persistent data-first Planning workspace.
- [Decisions/0020-run-flow-and-rules-language.md](Decisions/0020-run-flow-and-rules-language.md) —
  distinct Management/Wager/Deployment/Combat states, staged encounter disclosure, exact
  Signature/Passive grammar, and large modal inspection.
- [Decisions/0021-hourstone-table-and-result-gate.md](Decisions/0021-hourstone-table-and-result-gate.md) —
  spatial Hourstone Table, frozen-battlefield result gate, data-driven station routing,
  bespoke workspaces, and landscape phone/tablet interaction law.
- [Decisions/0022-unit-behavior-and-item-axes.md](Decisions/0022-unit-behavior-and-item-axes.md) —
  the per-unit behavior layer (target preference, standoff, per-chassis speed), weapon cast
  cadence, signature patches vs overrides, the trinket layer, and the Frenzy fix.
- [Decisions/0025-routing-and-the-engagement-law.md](Decisions/0025-routing-and-the-engagement-law.md) —
  units follow a flow field to an engage ring instead of hill-climbing on hex distance (bodies are
  detours, not walls), a unit that can neither reach nor strike its target fights what it can, and a
  leap keeps the victim it jumped at. Fixes units idling entire fights.
- [Decisions/0026-inscription-engine-first-twelve.md](Decisions/0026-inscription-engine-first-twelve.md) —
  item 5a's four gates settled: once-per-root guard (Inscriptions only), Living Inscription replaces
  Bearer of the Mark, full Banner→Inscription rename, and the first-twelve catalog (Silence ships as
  The Stilled Bell, healing→Shield is the first boss-gated Paradox).
- [Decisions/0024-act-bosses-and-the-disclosure-contract.md](Decisions/0024-act-bosses-and-the-disclosure-contract.md) —
  three per-act bosses (Last Oath / Ashfall Battery / Waning Crown), bosses authored for their act,
  brief and spawn built by one method, per-body behavior disclosure, and enemy cards that stop
  borrowing hero names. New `--boss` probe.
- [Decisions/0023-authored-enemies-and-encounter-composition.md](Decisions/0023-authored-enemies-and-encounter-composition.md) —
  enemies are authored not composed, composition is the act's difficulty lever, the two disclosed
  rules that bend the shared model, and the encounter-brief contract.
- [Decisions/0027-board-8x8.md](Decisions/0027-board-8x8.md) — the board is 8×8, not 8×6. Load-bearing
  for camera framing, placement geometry, and every `scenarios.json` row.
- [Decisions/0028-revisions.md](Decisions/0028-revisions.md) — the Revisions layer (2026-07-28), which
  drives the revision VFX/SFX lane and several open play-pass items.
- [Decisions/0029-one-frame-one-card.md](Decisions/0029-one-frame-one-card.md) — the workbench frame is
  the game's main UI system; every post-round choice reuses the market card + dossier. Rank-up's modal
  is the sole exception. Read before authoring any new choice surface.
- [Decisions/0030-beyond-the-hour.md](Decisions/0030-beyond-the-hour.md) — defeating the Waning Crown
  banks a real victory, then the existing Workbench offers retirement or a three-fight-plus-Crown
  endless cycle using the existing Act 3 pool, economy, scaling, save, and telemetry systems.
- [Decisions/0031-encounter-competence-ladder.md](Decisions/0031-encounter-competence-ladder.md) —
  item 32's three-rung authoring instrument: no-response full-run bot, disclosed-rule responsive
  floor, and deconfounded answer axes before targeted encounter composition changes.

## Projects
- [Projects/balance-baseline.md](Projects/balance-baseline.md) — **committed golden numbers** for every
  authoring instrument (encounters × 4 answer axes, bosses, hero builds, run EV, sim health).
  Regenerate with `make baseline`; the A/B is `git diff`. Not an assertion — it exists so a change's
  effect on the game is visible instead of reconstructed.
- [Projects/roadmap.md](Projects/roadmap.md) — **THE live board, actionable-only since
  2026-07-28**: every item is DESIGN or BUILD; deferred one-liners, design backlog, one-line done
  log. Sessions plan from here (CLAUDE.md Planning SOP).
- [Projects/play-pass.md](Projects/play-pass.md) — what to watch next time Jake plays (he
  playtests continuously — feedback, never a board item). Sessions keep it current.
- [Projects/blocked.md](Projects/blocked.md) — the gate register: work that is real and still wanted
  but cannot move, each entry naming its gate and its unblocker. Read before answering "what's next".
- [Projects/roadmap-done-archive.md](Projects/roadmap-done-archive.md) — full detail of completed
  and parked roadmap items, the laws pages (5/5a/6), and state snapshots — cut out of the board
  2026-07-27/28. Read only when you need the blow-by-blow; the board is the priority list.
- [Projects/boss-probe-2026-07-26.md](Projects/boss-probe-2026-07-26.md) — `--boss` output at ship:
  each act boss vs four answer-axis parties × six formations. The bar is "how many kinds of strength
  can pass this", not win%.
- [Projects/sweep-2026-07-25.md](Projects/sweep-2026-07-25.md) — post-ADR-0022 outlier re-run:
  per-class deltas, the new Shade flag, and a fresh (non-comparable) tier baseline.
- [Projects/unity-mcp-playtests.md](Projects/unity-mcp-playtests.md) — stable editor bridge,
  deterministic remote stepping, UI capture, and domain-reload rules for Unity verification.
- [Projects/planning-system.md](Projects/planning-system.md) — ⚠ **not the roadmap planning SOP** (that
  lives in `CLAUDE.md` + the roadmap's own Stages block). This is the in-game `PlanningSession`
  implementation: boundary map, transactional actions, commit law, consumable/economy extension
  recipes, Unity seams, verification, and remaining integrations.
- [Projects/vfx-lab.md](Projects/vfx-lab.md) — the VFX Lab editor window (2026-07-29): all recipes
  editable live, `Warband/VFX Lab/*` menu items. The iteration surface for combat FX.
- [Projects/sweep-2026-07-23.md](Projects/sweep-2026-07-23.md) — the first balance sweep
  (superseded by `sweep-2026-07-25.md`; kept for the delta).
- [Projects/oath-probe-2026-07-24.md](Projects/oath-probe-2026-07-24.md) and
  [Projects/oath-probe-2026-07-25.md](Projects/oath-probe-2026-07-25.md) — Last Oath `--oath`
  what-if runs; the 07-25 pass is the one that answered "does placement choose the survivor?".

## Daily, Bugs, Inbox
- `Daily/` — session notes, one per day, `2026-07-22` → present.
- `Bugs/` — created when first needed.
- `Inbox/` — ⚠ **undeclared staging area** holding unrouted design content (a UI/UX redesign brief, a
  roster expansion plan, a market advanced-preview spec, plus reference images). Nothing in the
  documented process says who drains it. Read before proposing anything in those areas, and note the
  roster expansion plan sits against the `Design/content-budget.md` hard cap and the roadmap's
  explicit "deliberately NOT proposed: more heroes".
