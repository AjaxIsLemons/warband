# FX runtime — architecture for the combat-spectacle pass (2026-07-25)

Engineering spec for [[combat-spectacle]] (Jake's brief: casting system, aura/hex effects,
status icons with time+stacks, better attack VFX — "go big, this is the selling point").
Extends [[directed-tells]] §6.2 (its `prefabId` seam becomes the vfx-id binding here) under
the [[render-contract]] laws. All seams verified against source 2026-07-25 by two inventory
agents (event schema empirically checked against real fights) + the architecture agent.

## Core decisions
1. **VFX recipes are C# code, not JSON.** Tells (tuning.json) stay the *binding + tuning*
   layer (which vfx id, when, color/glow/scale); a static `VfxLibrary` recipe table is the
   *look* layer (particle configs, mesh+shader elements, curves). Gradients/AnimationCurves
   don't survive JSON; C# syncs over Syncthing; per-tell `motionColor/motionGlow/motionScale`
   still tints any recipe so the F1 loop needs no recompiles.
2. **One stepping law.** Everything implements `bool Step(float dt)`, driven from a single
   `ReplayPlayer.StepFx(dt)` wrapping `_director.Tick` + field views + death sequences.
   Both call sites (Update; BuildLoadedPreview's fixed 0.01 s loop) go through it, so frozen
   contact sheets reproduce by construction.
3. **No `_Time` in any shader, ever.** All animated shader inputs (scroll phase, ring
   radius, dissolve cutoff) are MPB floats written from Step. **TrailRenderer is banned**
   (wall-clock); the ParticleSystem *Trails module* is fine (simulated inside `Simulate()`).
4. **Tracer/Burst stay as the zero-asset fallback.** Empty vfx id = today's rendering;
   migration is per-tell; `models.enabled=false` and old fixtures keep working.

## Sim/wire changes (headless first — no Unity contention)
- **S1 Burn fold bug (correctness, standalone commit):** the fold appends on StatusApplied
  and removes on kind+Mag match, but Burn merges into one pool (`Battle.cs:860`), decays
  `--s.Mag` with no event (`:459`), and drain-expires with Amount=0 (`:462`) — fold Burn
  magnitude freezes and the icon never clears. **Guardrail-measured: FOLD DIVERGED @ tick
  10 in any Burn fight; ships in castfest/statusstorm/glyphwar/skirmish.** Fix: emit
  post-decay pool Mag each Burn pulse (Cause.Burn), real Mag on drain expiry; fold Burn law
  = replace-on-apply, kind-only-remove; add a Burn fixture to the fold guardrail tests.
- **S2 Status durations:** stamp `TicksLeft` into `StatusApplied.Aux2` (+`SwingsLeft` Aux3;
  both already serialize — zero event-format change). Fold tuple gains `ExpiryTick`
  (**excluded from ViewHash** — countdown is decoration; expiry truth stays events, per the
  documented hash contract). Initial-snapshot statuses ⇒ replay **v4→v5** + regenerate all
  fixtures (`make replay && make scenarios`) + DLLs in ONE commit.
- **S3 Ability identity — no event change:** new `Warband.Content/AbilityIdentity.cs`
  `Resolve(chassisId, traits)` = last trait in `Kits.Nodes` with a `SignatureOverride`,
  else chassis (the Loadout.cs:161 law, kept in the shared DLL so it lives once) +
  `DisplayName` via ContentLexicon. `TellMatch` gains `byAbility` filter — mirrors the
  chassis param (null context never matches), **+2 specificity** (strictly narrower than
  chassis; document + test, it changes the sum-of-filters contract). Optional later: int id
  in `Cast.Aux` (free — Aux serializes everywhere, log is write-only).
- **S4 authoring note:** `Cause.Trigger` is the 2nd-most-common damage cause in real fights
  (measured 218 Attack / 110 Trigger / 20 Ability) and has **no tell today** — the rider
  echo language in [[combat-spectacle]] fills it (built in P6). No field TTL added (no free
  slot on FieldCreated); expiry animates on the FieldExpired event instead.
- **S5 byWeapon filter (SPEC'D, NOT BUILT — the unlock for §6 per-weapon attack language).**
  Tells can't filter on weapon, so the [[combat-spectacle]] §6 table (dagger crossing
  nicks, greataxe 1-frame hang, musket instant smoke line, censer mote, staff wisp) is
  direction without a data path. The fix, mirroring the chassis filter EXACTLY:
  1. `sim/Warband.Sim/TellMatch.cs`: `Matches(...)` gains trailing optional
     `string? weapon = null, string? sourceWeapon = null` — OrdinalIgnoreCase compare;
     **a byWeapon rule never matches when sourceWeapon context is null** (view-context
     law, same as ranged/chassis/ability). `Specificity` counts weapon at **+1** (a peer
     of chassis, unlike ability's +2) — a byWeapon row TIES a byChassis row and falls to
     registry order; if authoring ever needs weapon>chassis precedence, bump consciously
     and document, don't discover it. Headless tests: match/mismatch, null context never
     matches, tie-with-chassis behavior, combined weapon+cause.
  2. `TuningData.cs` TellDef: `byWeapon` / `weapon` fields (F1 auto-UI free).
  3. Client `FeedbackDirector.Handle`: pass `sourceWeapon = su?.WeaponName` alongside the
     chassis/ability context — **WeaponName is already on the wire** (fold, replay v3+);
     direct field, no memoization needed. Zero sim-event/format change, no replay bump,
     no fixture regen (DLL copy only via `make unity-sim`).
  4. Then pure authoring: one Attack row per weapon class (match on the catalog's
     WeaponName strings — verify exact casing from Kits/weapons content first) with
     arc/tracer recipe + impact sting per §6. Recipes: `arrow-streak`/`slash-arc` exist;
     `smoke-line` (musket) and arc VARIANTS (width/hang timing) are new VfxLibrary
     entries. Fixtures: scenarios.json units accept a `weapon` field — author one
     per-weapon-class duel fixture and probe its attack ticks.
  5. **Adjacent, separate items:** Honed +20% tracer brightness = scale the tell glow by
     `WeaponTier` in the Director when firing weapon-matched rows (small code touch, not
     a filter); Relic edge-glow lives on the PROP in `TryBuildModel`, not in tells.
  Estimate: filter+tests ~30 min · recipes + 11 authored rows + fixtures ~1-2 h ·
  standard gates (headless compile, probes, sheet ×2 diff).

## TellDef growth (TuningData.cs; DebugMenu auto-UI needs zero edits)
- Filter: `byAbility` / `ability` (resolved id, e.g. `"pyromancer"`, `"pyro.starfall"`).
- VFX slots (empty = primitive fallback): `vfx` (at SOURCE at StartAt; sustained recipes
  run through windup), `projectileVfx` (replaces the cube Tracer visual), `impactVfx` (at
  contact), `groundVfx` (hex-anchored — field hexes for FieldCreated, else the side unit's
  hex).
- Riders: `hitAnim` + `hitAnimMinT` (gate flinch on ImpactTune intensity — no DoT-tick
  spasms), `announce` (story-feed "«X» casts Y"), `pulseGround` (flare fields covering the
  impact hex).
- New `FxTune` group: deathLingerSeconds 1.6 · dissolveSeconds 0.8 · fieldSpawnSeconds 0.35
  · fieldExpireSeconds 0.45 · fieldPulseBoost · statusIconSize 0.22 · statusIconCap 5.

## Vfx runtime (NEW client/Assets/Scripts/Warband/Vfx/)
- **VfxDef** `{Id, Duration, Sustained, Elements}`; elements: **ParticleElement** (full
  module config, flipbook, Trails module), **QuadElement** (procedural quad/hex + shader +
  per-param curve tracks via MPB; Ground/Billboard/UpFacing orientation), **LightElement**
  (pooled point light, hard cap ~4 live). Anchor per element: World | FollowUnit.
- **VfxInstance** — pooled MB, FloatingNumber idiom (`Create/Play/Step/recycle`).
  Particles: `playOnAwake=false`, `useAutoRandomSeed=false`, on Play `Clear + randomSeed +
  Simulate(0,true,true,false)`, then per Step **root only**
  `Simulate(dt, withChildren:true, restart:false, fixedTimeStep:false)`.
  `PlayProjectile(start,end,seconds)` translates in Step — ContactOffset law untouched.
- **Seeds:** `(tick*397) ^ (sideUid*31) ^ slot{src0,proj1,impact2,ground3}` — same events,
  same pixels; contact sheets diff-clean run-to-run.
- **VfxLibrary** — static recipe dict + shared material cache (`Shader.Find`). Starter ids:
  slash-arc, arrow-streak, fire-bolt, fire-release, holy-release, cast-aura (sustained),
  impact-spark, impact-heavy, heal-motes, status-pop, death-dissolve-burst, ground-ignite,
  ground-bless, leap-dust. Unknown id = log once + primitive fallback (authoring leads
  assets, same as SFX).
- Pools mirror `_tracerPool`, live under `_generated`, recycled in `FeedbackDirector.Reset`.

## Shader set (NEW client/Assets/Shaders/Warband/ — hand-written URP HLSL)
All: URP RenderPipeline tag, UnityPerMaterial CBUFFER, ZWrite Off (except dissolve),
Cull Off, HDR `_Color` (glow ≥ ~2 crosses bloom threshold 1.1). Compile errors surface only
in the remote editor — GetConsoleLogs after every shader sync.
- **WarbandRing** (additive): _Radius/_Thickness/_Softness/_ArcFill/_Rotation — telegraphs,
  shockwaves, status countdown rings, field rims.
- **WarbandGroundFill** (alpha): _NoiseTex + MPB `_Phase` scroll — burning/blessed floors.
- **WarbandSigil** (additive): _MainTex + _Rotation — cast circles, emblems.
- **WarbandGlow** (additive): procedural radial billboard — release flashes, soft glows.
- **WarbandParticle** (additive): vertex color × flipbook — all particle materials.
- **WarbandDissolve** (alpha, ZWrite On): _NoiseTex + MPB `_Cutoff` + HDR edge — death.
Ground overlays: render queue 2900 + `Offset -1,-1` (never z-fight Lit tiles).

## Ground substrate
- `Dictionary<Hex, Renderer> _tiles` built in BuildBoard (no lookup exists today).
- Second procedural hex mesh **with UVs** (center 0.5,0.5 + unit-circle ring) for polar
  shaders; board tiles keep the UV-less Lit mesh.
- **FieldView** (NEW) replaces the repaint-flat-hex path: per-hex overlay quads
  (GroundFill) + a footprint rim (Ring). States driven by Step: spawn-in (edge traces
  perimeter 0.2 s, then floor fades 0.3 s — boundary before body) · sustain (MPB phase
  scroll; attached auras re-anchor per SyncFields as today) · pulse (Director delegate on
  `pulseGround` impacts → brightness envelope) · expiry (SyncFields flags BeginExpire, fade,
  destroy when Step returns false; frozen scrubs past expiry just build without it —
  decoration law). Walls: grey slab + ring rim base. Telegraph = hitbox by construction
  (overlays come only from fold hexes).

## Status icon row (replaces pip cubes)
- **StatusIconRow** (NEW) at the pips anchor; billboarded quads via the `_numberFace`
  quaternion. **Ship-first fallback needs zero assets:** 6 procedural family glyphs
  (diamond control / flame DoT / chevrons / cross / ghost) tinted by the existing
  StatusColor map; sprite atlas (GenerateAsset) upgrades it later.
- Stacks: entry count per kind (Burn = pool Mag). 2-3 = count pips; ≥4 or Burn = "×N"
  TextMesh (fontSize-180 crisp-text trick). Countdown = Ring `_ArcFill` vs fractional
  playhead clock (needs S2). Priority control > phase > offense > defense > DoT > misc;
  cap + "+N" chip. Fold-driven truth; Director only pops scale on apply.

## Cast choreography (no new sequencing machinery)
Pattern per ability: byAbility Cast tell (`vfx: cast-aura` sustained + windup + `defer` +
`announce`) → latch stamps release on Root → release burst = the tell's `impactVfx` at the
caster → payload events (Cause.Ability, Root=caster) carry `projectileVfx`/`impactVfx`,
their StartAt reads the latch → simultaneous fan-out (consumers never write the latch).
Cast + children share the beat chain key, so stagger moves the sequence as one. Announce
uses the existing kill-feed slots. Scope: 8 chassis first, then the most build-defining
overrides (~25-30 ids eventually, all tuning.json rows).

## Death presentation (corpse linger — client-side only)
DeathSequence: hit-freeze 0.1 s → Death crossfade (ActionSpeed fitted to linger) → dissolve
`_Cutoff` 0→1 (per-texture cached dissolve material, MPB per renderer) → hide + restore.
ApplyFold law becomes `SetActive(!Dead || Lingering)`; bars/nameplate/icons hidden while
lingering. **Reset() must restore materials + clear flags** or loop-wrap shows ghost
corpses. Primitive fallback: scale-down-and-sink 0.4 s + Burst. Animator gains Hit + Death
states via a one-shot idempotent editor script (`Warband/Build BoardUnit Controller`,
run under the unity lease, .controller committed) — clips already imported. Per-ability
cast clip variants deferred (VFX carries identity first).

## Build phases (each lands alone, contact-sheet-verifiable)
- **P0 sim+content** (headless): S1 + S2 + S3 + tests; fixtures + DLLs regenerated in one
  atomic commit.
- **P1 FX foundation:** shaders + Vfx/* + TellDef fields + Director firing; 3 probe tells;
  **run the contact sheet twice and binary-diff the PNGs** (determinism proof).
- **P2 ground substrate:** _tiles, HexMeshUV, FieldView + pulse delegate; field-heavy
  sheets; wall restyle; live recolor check.
- **P3 status icons** (needs P0): row + UpdatePips swap; statusstorm sheets (stacks,
  draining rings at two scrub ticks, overflow); glyph fallback ships before any icon gen.
- **P4 cast choreography:** per-ability tuning rows + announce; castfest sheets; 3-victim
  fan-out check.
- **P5 death + animator:** controller build, DeathSequence, linger law; two full loops in
  play (no ghost corpses); mid-dissolve sheet; models-off sink fallback.
- **P6 polish:** default recipes for remaining primitive tells, hitAnim authoring, F1
  session with Jake.

## Risks that will actually bite
Particle Simulate misuse (root-only / withChildren / fixedTimeStep:false — the PNG diff is
the guard) · play-vs-preview particle divergence is FINE, preview-run-to-run must match
(state in file headers) · URP traps (missing RP tag = invisible in builds; CBUFFER; queue
2900) · loop-wrap material restore · +2 specificity contract change · generated textures
small (256 px) and committed — noise generated procedurally in code instead (skip the
dependency) · v5 bump = one atomic commit or the client desyncs from StreamingAssets.
