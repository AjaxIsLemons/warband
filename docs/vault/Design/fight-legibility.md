# Fight legibility — research + phased plan (2026-07-25, overnight session)

Jake's brief: *"make the fights/sim render more legible — casts, autos, unit identity should
all read; real models with basic life; unique/customizable cast VFX; we have Unity AI tokens."*
This is the researched proposal for **roadmap item 1** (FEEL & READABILITY, DESIGN stage).
Three research tracks fed it: a line-level render-layer inventory, genre legibility research
(TFT, Underlords, Auto Chess, HS Battlegrounds, Super Auto Pets, Mechabellum, Backpack
Battles, Legion TD 2), and an asset/animation/VFX pipeline survey. Extends
[[render-contract]] and [[render-polish]]; **nothing here is built** — phases become SPEC'D
only after Jake's nod on the decision points at the bottom.

## The reframe: "not clear what's happening" is three different problems

Genre history says the sentence decomposes, and teams reliably fix the wrong one:

1. **Can't SEE the event** — a cast fired, a unit died, and no pixel said so. VFX/HUD problem.
2. **Can't ATTRIBUTE the event** — who hit whom, for how much, why that target. Telemetry/UI problem.
3. **Can't INFER THE RULE** — why did it cast *now*, why act first, was I even favored?
   Systems-legibility problem — the one **nobody in the genre has solved first-party**
   (SAP's trigger order is secretly attack-stat-sorted; HSBG needed the community to build
   Bob's Buddy; Mechabellum players call the fight readable and the causality opaque).

Roadmap item 1 already split ① presentation / ② state legibility / ③ UI quality. The
research refines it: our ① and ② both live in problem 1-2, and **problem 3 is a gap the
whole genre left open that our deterministic sim can close almost for free** (Phase 4).

## Diagnosis — what the board actually is today (inventory 2026-07-25)

- Everything is code-built primitives from one 1,575-line file (`ReplayPlayer.cs`). **Zero
  prefabs, materials, meshes, animation clips, or Animators exist in the project.** The 8
  chassis do get distinct primitive silhouettes + accessories (shield slab, spear, staff…).
- The tell system (14 tells in `tuning.json`) can do exactly five things: flash, punch-scale,
  floating number, one motion (lunge/tracer/burst/arc), and the impact latch. No particles,
  decals, sound, shake, or hit-stop.
- **Every cast in the game looks identical**: cyan flash + swell. The `Cast` event carries no
  ability identity (`Battle.cs:374` — `{Source, Cause.Ability}` and nothing else), so a
  fireball and a heal-cast are byte-identical at the event level. BUT the fold carries
  `ChassisId`/`WeaponName`/`Traits` per unit and the dispatcher already holds the caster —
  **a `byChassis` tell filter is a one-field change giving 8 distinct cast looks with zero
  sim change** (`ReplayPlayer.cs:243-247`, growth path already named in [[directed-tells]]).
- **The silent vocabulary is huge:** 23 of 27 StatusKinds produce no visual beyond a grey pip
  (StatusColor has 5 branches for 27 kinds); 12 of 20 EventKinds have no tell at all —
  including **CheatDeath, a hero moment that renders as nothing**, and ShieldChanged; the
  FieldFlavor tell filter is tested in `TellMatch` but unreachable (dispatch hardcodes null).
- **Regressions found:** the active volume profile has no DepthOfField/ColorAdjustments, so
  the tilt-shift diorama look is NOT running and tuning.json's `post.saturation/dofStart/
  dofEnd` knobs are dead; MSAA/SMAA off (spec'd 4×+SMAA); `Game.unity`/`Boot.unity` are
  untracked in git (scene-split collateral — verify in Editor). Silhouettes key on unit
  **Name substring**, not the `ChassisId` that replay v3 added — flavor-named authored
  enemies will render as default capsules.
- **Designed-but-unbuilt** (render-polish steps): decoupled playback clock, beat sequencer,
  hit-stop, lighting rig, screen shake, PrimeTween, decal board. The fold consumption, tell
  registry, anti-overlap numbers, ImpactTune magnitude ramp, and ADR 0018 movement are solid.

## What the genre teaches (proposed as authoring laws)

The full per-game evidence lives in the session research; the load-bearing rules:

1. **The player is a spectator — retime everything.** TFT authored a *second, slower*
   animation set because League timings couldn't be parsed in a 9v9 ("we had to slow
   everything way down"); their stated target was "watching a Bronze teamfight."
2. **Visual impact must equal gameplay impact — as a hard table** (Riot VFX Style Guide):
   ult = biggest/most saturated; damage spell = high-saturation, clear silhouette;
   **defensive spell = LOW saturation, LOW opacity** (quiet — the rule indies get backwards);
   **basic attack = small**. Adopt verbatim as the tell-authoring spec.
3. **Make thresholds discrete events, not analog fills.** Underlords' mana bar *changes
   color at full* — "about to cast" becomes a binary flip you can't miss. Highest
   value-per-line feature found anywhere.
4. **Subordinate autos structurally:** the auto's visible output IS the mana meter filling
   toward the cast. Autos become informative instead of competing.
5. **Persistent numeric state beats transient particles** (HSBG's whole advantage).
   Segmented HP bars (TFT: 1 divider/300hp) = absolute magnitude with no text.
6. **Status on the unit's material, not icons** (Underlords: stone-frozen units, white
   silence mask). 16px pips lose at autobattler zoom — ours already have.
7. **Silhouette + scale tiers + per-weapon tracer signatures** carry unit identity
   (Mechabellum reads 100+ units with almost no combat UI). Riot picked hexes partly FOR
   silhouette recognition. Test: render the board as black shapes at 60px.
8. **Audio is the only free channel** in a crowded frame — it was Underlords' actual
   post-launch readability fix, and Riot requires wind-up sounds ahead of big casts.
9. **Camera distance is a documented legibility killer** (the one substantiated Underlords
   complaint: units too small for deaths to register).
10. **Serialization is the strongest lever and it costs turn time** (HSBG's most-praised and
    most-complained property are the same property). Take it in moderation: beats +
    micro-stagger, not full one-at-a-time.
11. **Sequencing visuals does NOT make the ordering rule legible** (SAP's invisible
    attack-stat trigger order is the genre's #1 complaint class). If we stagger casts, the
    stagger rule must itself be visible/inspectable.
12. **Never let a shop timer tax fight-watching** (Backpack Battles economically punishes
    players for learning). We're safe today — keep it that way.

**Competitive datapoint:** *Guildrun* is real and is essentially our thesis shipping first —
Leyline (ex-Hearthstone/Bazaar devs), PvE roguelike autobattler, hex grid, isometric, 25
heroes/180 specs, tiers + endless leaderboards, announced July 2026, demo hit #1 on Steam
(app 3669200). Turn-based, no time pressure — they took the SAP legibility bet, not TFT's.
Worth a demo-play session for presentation reference.

## The plan — five phases, each independently shippable

### Phase 0 — Repair & baseline (half a day; broken machinery, allowed by content doctrine)
- Verify in Editor then fix the post stack: DoF (tilt-shift) + ColorAdjustments into the
  active profile so the dead tuning knobs work again; MSAA 4× + SMAA; commit the untracked
  scenes so this can't silently regress again.
- Key silhouettes on `ChassisId` (already on the wire) instead of Name substring.
- Capture a baseline contact sheet (`Warband/Render Contact Sheet`) for before/after.

### Phase 1 — The legibility grammar (no new art; the biggest expected win)
The unbuilt half of [[render-polish]], now genre-validated, plus the registry gaps:
- **Spectator retiming:** slow default playback; global FX-duration discipline (~0.1s hit
  frame, short tails — "if your FX feel long, they're too long").
- **The cast sentence:** mana fills (autos feed it) → **bar color-flips + pulses at full** →
  readable wind-up (0.3-0.6s, scaled by ability weight) → payoff → brief recovery dim. Wire
  `ManaChanged` (currently tell-less) + the threshold flip + per-chassis wind-up.
- **Beat sequencer + decoupled playback clock + hit-stop** as designed in render-polish —
  tick-grouped beats, blocking events (Death, marquee cast), micro-stagger inside beats.
- **Fill the silent registry:** filterless StatusApplied/StatusExpired fallbacks, per-status
  material overrides for the big ones (Burn emissive, Stun/Root desaturate-freeze, Phase
  transparency), ShieldChanged cyan bar flash, **CheatDeath as a hero moment**, field tells
  via the FieldFlavor filter fix (one-field change), StatusColor's 22-kinds-are-grey fixed.
- **`byChassis` cast tells** — 8 distinct cast looks (color/motion/windup per chassis), zero
  sim change. Per-ability identity (Aux stamp on Cast) only when a specific spell needs a
  bespoke tell the chassis can't carry — flagged, cheap, later.
- **Unit-state pass:** segmented HP bars, ally-green/enemy-red bar tint, team rim-light
  fresnel shader (VALORANT convention — works identically on capsules and future models).
- **Adopt law #2's table** into tuning.json authoring discipline (autos small, defenses quiet).
- Camera experiment: pull in / scale units up; verify at the real play camera via contact sheet.

### Phase 2 — Real units (KayKit route; $0 to validate, ~$150 to commit)
Pipeline research verdict: **buy KayKit, don't AI-generate the roster.** One shared Mecanim
Humanoid rig (`Rig_Medium`) across every character, **161 CC0 combat animations free** (melee
1H/2H, ranged, spellcasting, hit/death/spawn), chunky silhouettes built for small-on-screen,
single-artist consistency, CC0. Coverage: knight→Bulwark, barbarian→Berserker, rogue→Shade,
mage→Pyromancer, ranger→Sharpshot, cleric pack→Cleric; **Phalanx and Banneret are prop-level
kitbashes** (spear/banner onto a body — and exactly where Unity AI text-to-motion earns its
keep for the two missing clips: spear thrust, standard plant).
- **Integration under the existing tell director, which stays the timing authority:** one
  shared AnimatorController (Idle/Run/Attack/Cast/Hit/Death) + one AnimatorOverrideController
  per chassis swapping only Attack/Cast clips; `CrossFadeInFixedTime` called at the phase
  boundaries `PendingTell` already computes; `Animator.speed = clipLen/(windup+motion) ×
  _speedScale` so swings fit the sim's gaps (render-contract §4) and stay synced under
  fast-forward; root motion OFF (sim owns position, ADR 0018).
- Swap point is exactly `BuildSilhouette()` (`ReplayPlayer.cs:1271`) → prefab lookup via a
  new **BoardCatalog** keyed on ChassisId (mirror of PresentationCatalog). Four contracts to
  preserve: Body container scale/yaw, Root owns position, flash renderer (widen to a list),
  no colliders.
- **Keep the tween layer** — punch/flash/squash read better at 60px than skeletal animation;
  animation adds *identity* (spear thrust vs axe chop), tweens carry *impact*. That division
  is what TFT-likes actually ship.
- **Silhouette gate:** every model must pass the 60px black-shape test before adoption.
- Enemy families later: KayKit skeletons (free) now; Mystery packs / AI 3D for bespoke
  families post-playtest.

### Phase 3 — Per-ability VFX language (~$40-70 in packs + build)
- **Buy:** Epic Toon FX ($40, ~$20 on FX Friday; 390 effects, URP upgrade supplied) +
  Cartoon FX Remaster Free + Unity Particle Pack + Kenney particles (free) + optionally Hovl
  shields/auras ($30). Skip VFX Graph (Shuriken has `Simulate()` for replay scrub; we have a
  replay player), skip realistic packs, skip distortion under URP.
- **Hand-build ground effects as meshes + Shader Graph** (polar-coordinate node → magic
  circles, telegraphs, shockwaves, cones): deterministic silhouette at tiny sizes where
  particles mush, and **AoE telegraphs must match the hex hitbox exactly** — Riot's #1
  VFX-update priority.
- **Binding = extend the tell registry we already have:** add `vfxId` (+ later `animTrigger`)
  to `TellDef`; ScriptableObject prefab registry with direct refs (not Addressables/
  Resources); EditMode test asserting every catalog ability has exactly one binding and no
  binding dangles. VFX authoring inherits the hot-reload loop.
- Determinism: seed `ParticleSystem.randomSeed` from (tick, unitId) — `useAutoRandomSeed`
  defaults true and would break screenshot-diff regression.
- First-hour URP config: Transparency Sort Mode = Custom Axis (0,1,0) on the active quality
  asset; soft particles off pack-wide (broken under our camera); Depth/Opaque texture set
  deliberately. Legacy `Particles/Additive` shaders go pink — use publisher URP upgrades.

### Phase 4 — Comprehension layer (problem 3; determinism makes this nearly free for us)
- Post-fight **damage dealt/taken chart with % of total** + "died to X at t=Ns" (FightStats
  + Root attribution already exist in the sim).
- **Win-probability re-sim** — first-party Bob's Buddy: run the fight across N seeds, show
  "you were 78% favored." **No genre leader has ever shipped this first-party**; our seeded
  sim + Sweep harness make it a weekend, and it answers the "was I scammed?" frustration
  documented in every game researched.
- Replay scrub with state restoration (the fold IS the state — Backpack Battles' best-in-genre
  log, cheap for us), in-fight speed toggle + pause as real UI (not a buried slider).
- **Sound stings** (decision needed — budget says "no sound"): cast wind-up, death, ult
  callout. Audio is the only free channel; ElevenLabs SFX generation is available through our
  MCP for near-zero-cost placeholder stings.

## Money, tokens, and where AI actually helps

- **Cash:** $0 validates Phase 2 (free KayKit packs + free animation library, one afternoon
  on the real camera). Full commit ≈ **$200-220**: KayKit Complete $150 (all packs + every
  future pack, SOURCE .blends for the kitbashes) + Epic Toon FX + Hovl.
- **Unity AI tokens — spend on gaps, never the roster:** (1) text-to-motion clips KayKit
  lacks (spear thrust, banner plant — promote to editable clips, hand-tune); (2) VFX
  masks/sigils/magic-circle textures + ability/status icons; (3) ElevenLabs SFX stings;
  (4) a skybox for the diorama backdrop. Note: credits are ~1,000/mo and one enthusiastic
  day can burn them — batch and be deliberate.
- **AI 3D generation** (Tripo 3.1 / Hunyuan 3D + auto-rig are live in our editor via the MCP
  toolchain, contra Unity's own Generators which don't do meshes): **not for the 8 heroes**
  (style-consistency across independent generations is unsolved below enterprise tiers;
  AI topology fights the low-poly silhouette-first need — Tripo's own blog documents it).
  Reserve for bespoke post-playtest enemy families through a 2D-concept-bottleneck pipeline.
- **PrimeTween — revised to SKIP** (was recommended in render-polish): our juice layer is
  deliberately Director-stepped so frozen captures reproduce; PrimeTween's own update loop
  fights that. Hand-rolled decay is ~10 lines and already correct. UI-only if ever.
- **Mixamo — plan to never need it** (effectively abandonware since mid-2025; KayKit +
  Quaternius UAL2 cover animation). If ever used: harvest once, commit FBXs.

## Decisions (Jake, 2026-07-25 morning): **"sold on everything but KayKit"**
Approved: the plan, phases, sequencing, sound stings, micro-stagger depth — **build it all.**
The one exception: **do not spend $150 on KayKit Complete** — research a free or cheaper
route first (KayKit's own free tier is a candidate; the objection is the bundle price, not
the artist). Model-route decision pending that research.

## Build state (2026-07-25 overnight-into-morning session)
- **Phase 0 — DONE** (commit `acddbf0`): post stack restored (DoF 18-30 measured against real
  camera distances, ColorAdjustments live, knobs work again), MSAA 4× + SMAA, scenes tracked.
  Deeper tilt-shift wants a camera-pitch pass with Jake — the 25° pitch leaves little depth
  span to blur against.
- **Phase 1 core — DONE** (commits `f788491`, `a1fcf8b`): byChassis tell filter (8 distinct
  cast looks, sim-tested) · ChassisId silhouette keying · beat sequencer (causal-chain
  stagger) + hit-stop (playhead hold; Death/crit) · mana threshold flip + pulse · segmented
  ally/enemy HP bars · status-as-material tints · all-27 StatusColor map · registry fills
  (StatusApplied/Expired fallbacks, ShieldChanged, CheatDeath hero moment, 4 byFlavor
  FieldCreated). **Beat stagger + hit-stop need a live play-mode eyeball** (static captures
  can't show time).
- **Phase 4 sim side — DONE** (commit `113a2de`): `FightSummary` (damage chart + killed-by +
  death beats) and `BattleForecast` (N-seed win %) in Warband.Sim, 299 tests green. Client
  UI for both still open.
- **Model route — SETTLED $0, and Phase 2 slice DONE** (commit `82b7a6b`): the alternatives
  research verdict was **KayKit's own free tier** — same artist and the same shared 23-joint
  Rig_Medium skeleton as the $150 bundle, 173 free CC0 combat clips (rig-verified by parsing
  the files: identical joint set across all 6 bodies + 8 animation libraries + 4 skeletons).
  Quaternius lost on zero bow animations + realistic proportions; Kenney's characters have no
  skeleton at all. **The board now renders real KayKit minis**: chassis-mapped bodies, kitbash
  props on the rig's handslot bone (phalanx=Knight+spear, banneret=Knight+banner,
  cleric=Mage+hammer — the one weak seat; the $7.95 Adventurers EXTRA tier adds a Druid if it
  reads poorly), shared Idle↔Walk controller, primitive fallback fully intact
  (`models.enabled:false` collapses the path). Free skeleton enemy pack (same rig) staged in
  research for PvE later.
- **Open:** per-event animation crossfades (Attack/Cast/Hit/Death clips are imported; drive
  from the tell director's phase boundaries with `Animator.speed` fitted to windup+motion ×
  speedScale) · Phase 3 VFX packs (Asset Store purchases need Jake at the editor) · Phase 4
  client UI (damage chart / forecast in the shell) · sound stings (GenerateAsset consent) ·
  camera-pitch/framing pass · **live play-mode eyeball of beats/hit-stop + minis in motion.**

## Sources (load-bearing)
Riot VFX Style Guide (public PDF) + "Clarity in League" + VALORANT shader clarity post ·
"The Story of TFT" parts 1-2 (animation retiming, noise strike team, hex rationale) ·
Underlords patch notes (mana-bar color flip; audio readability fixes) · HSBG combat rules +
Bob's Buddy/HSReplay rationale · SAP dev forum reply on trigger order · Backpack Battles
0.9.12 combat log · LTD2 Leak Ratio/MVP rationale · Guildrun Steam app 3669200 · KayKit
character animations (161, CC0) + Complete bundle · Meshy/Tripo/Rodin docs + Tripo's
mesh-simplification blog · Unity 6.3 AI manual. Full agent reports live in this session's
transcript (2026-07-25); ask if a claim needs its citation.
