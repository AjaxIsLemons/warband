# Authoring combat FX — the how-to (2026-07-25)

Operational guide for adding/changing combat visuals after the combat-spectacle arc.
Direction lives in [[combat-spectacle]] (palette lanes, tiers, per-signature specs);
engine detail in [[fx-runtime]]. This doc is the *workflow*. A project skill
(`.claude/skills/spell-fx`) wraps the checklist for agent sessions.

## The three change tiers (know which you're in before starting)
1. **Data only** (minutes, hot-reload, no recompile): everything on a tell row in
   `client/Assets/StreamingAssets/tuning.json` — matcher, colors, glow, scale, windup,
   sounds, which vfx ids fire, announce/hitAnim/pulseGround/bigImpact — plus every FxTune/
   BeatTune/ImpactTune knob (F1 cockpit auto-generates UI for all of it; Save persists).
2. **Recipe** (≈ an hour incl. verification): new/changed look in
   `client/Assets/Scripts/Warband/Vfx/VfxLibrary.cs` — compose ParticleElement /
   QuadElement / LightElement. C# syncs over; **compile-check headless first**
   (memory: headless-client-compile-check). Recipes are SHAPES; tells are PAINT
   (motionColor/motionGlow/motionScale tint any recipe — author recipes color-neutral).
3. **Primitive** (≈ a day, rare): a new element type or shader
   (`client/Assets/Shaders/Warband/*.shader`). Only for genuinely new visual physics
   (beams tethering moving units, chained hops). Follow the P1 conventions: URP tag,
   UnityPerMaterial CBUFFER, **no `_Time` ever** (MPB floats from Step), queue 2900 +
   Offset -1,-1 for ground overlays.

## Adding a new spell's visuals (the 5 steps)
1. **Identity is automatic.** A new spec node with a `SignatureOverride` resolves via
   `AbilityIdentity.Resolve` (last override in trait order wins; chassis id for stock
   kits). No sim change, no event change. Id = the node id (e.g. `pyro.starfall`).
2. **Author one tell row** (append to tuning.json, never reorder): `eventKind: Cast`,
   `byAbility` + the id, `windupSeconds` (B-rank 0.4 / S-crown 0.5-0.55 / reload-type
   casts small), `defer: true`, lane `motionColor` + tier `motionGlow` from the
   [[combat-spectacle]] §1 table (T2 ≈ 2.5-3.5, T3 4-6, **defensives ≤1.0 — below bloom**),
   `vfx: "cast-aura-<chassis>"` (or a bespoke recipe), `impactVfx` for the release,
   `castSound: "riser_<chassis>"`, `announce: true` ONLY for S-crown/T3 damage-forward
   casts (the ration law; a per-caster 6s cooldown also applies).
3. **Payload events** (the damage/heal/status children, Cause.Ability, Root=caster) get
   their own rows if the spell needs a distinct projectile/impact — `byAbility` works
   there too. Multi-victim fan-out and release timing come free from the latch.
4. **Fixture + probe:** add a build to `sim/Warband.Viewer/scenarios.json` that takes the
   node, `make scenarios`, find the Cast tick (fold the fixture headlessly), write
   `fixture tick advance` lines to `%USERPROFILE%\warband-shots\probes.txt`, run menu
   `Warband/Render Probe Shots`, scp the PNGs. **Same fixture+tick twice = filename
   collision — run multi-advance pairs as separate passes.**
5. **Gate:** `Warband/Render Contact Sheet` twice → binary-diff 28 PNGs. Byte-identical
   or you broke the determinism law (usually: unseeded particles, `_Time`, TrailRenderer,
   wall-clock anything).

New sigil/texture wanted? GenerateAsset (mono mask, white on black, 512²) →
`Resources/Board/FX/Sigils/<chassis>.png`; recipes no-op cleanly while it's missing.
Watch for retry-stub files (memory: unity-mcp-runcommand-quirks §6c).

## Weapon attacks — current truth and the gap
Autos key on `byRanged` + chassis today (melee lunge / ranged tracer / per-chassis
swing clips via the weapon-class animator states). The [[combat-spectacle]] §6 per-weapon
table (dagger crossing nicks, greataxe 1-frame hang, musket instant smoke line, censer
mote to lowest ally, Relic edge-glow) is **authored direction, not yet reachable from
data**: tells have no weapon filter. To build it:
1. Sim: add `byWeapon`/`weapon` to `TellMatch` (mirror the chassis filter exactly —
   null context never matches; +1 specificity; headless tests). `WeaponName`/`WeaponTier`
   are already on the wire (fold), so the client just passes source context. ~30 min.
2. Then it's pure authoring: one row per weapon class with a `projectileVfx`/arc recipe
   + impact sound. Recipes needed: slash-arc variants (exists: slash-arc), smoke-line,
   lobbed-arrow (exists: arrow-streak). Relic prop edge-glow is a TryBuildModel touch,
   not a tell.

## Statuses, fields, deaths — where their looks live
- **Status icons:** glyph shape/priority in `StatusIconRow.cs`; color stays in the
  StatusColor map (single authority). Icon ART upgrade = generate the 16-icon atlas
  (§5 prompt skeleton) + fill the `StatusAtlas` seam marked TODO in StatusIconRow.
- **Fields:** per-flavor look in `FieldView.cs` + FieldTune colors (live). Pulses ride
  `pulseGround` tells. Boon pulses are DORMANT until Heal carries a Cause (one-line sim
  change, flagged).
- **Deaths:** DeathSequence constants + FxTune (linger/dissolve/ashMarkAlpha/graveTilt).
  Ash/crack decal TEXTURES from the manifest are still ungenerated (procedural fallbacks
  render today).

## Next-steps ledger (consolidated from the arc — the honest list)
**Jake's live pass (VERIFY gate):** fight-ender slow-mo + camera punch/shake feel ·
riser mix + announce density in motion · F1: fieldIdleAlpha (greens hot), statusIconSize
(small), wall tint, cleric sigil star (regen?) · HP-bar snap vs 0.5s T3 windups → add a
short bar tween if it reads wrong (render-contract-legal).
**Small code items:** byWeapon tell filter (unlocks §6 weapon language) · Heal Cause
one-liner (Boon pulses) · hex-edged field floors (GroundFill fades radially → coins;
a hex-distance fade in the shader restores telegraph=hitbox crispness) ·
**Shader.Find → Always Included Shaders before the FIRST standalone build** (editor-only
risk flagged in P1; silent URP/Unlit fallback otherwise) · per-ability cast clip variants
(deferred — VFX carries identity).
**Asset batch remainder (~18 gens, approved):** 10 per-weapon impact stings · nova whump,
fissure crack, star whistle+boom, taunt horn, rally drum, mana-full tick, death knell,
execute shk-thud, Waning drone · 16 status icons · ash/crack decals · skybox.
**Shelved proposals (next wave):** 6 Waning ambient board · 7 hourglass mana rings ·
10 prop idle life · 8 Overtime (its own slice).
**Adjacent:** fight-legibility Phase 4 client UI (damage chart + forecast — sim side
DONE, needs a shell home) · camera-pitch/framing pass with Jake.
