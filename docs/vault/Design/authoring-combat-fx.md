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

## Weapon attacks — BUILT 2026-07-25 (§6 is now reachable from data)
Autos used to key on `byRanged` + chassis only. `byWeapon` ([[fx-runtime]] §S5) closed that:
a tell row can name the catalog's exact `WeaponName` ("Greataxe", "Twin Daggers",
"Matchlock Musket"), matched off the fold's identity block — no event change, no replay bump.

**Authoring a weapon row — the one rule that is not obvious:** give it
**`byCause: Attack` as well as `byWeapon`**. Weapon counts +1, a PEER of chassis, so a bare
byWeapon row ties the `byRanged` auto fallback at 1 and loses on registry order. The cause
gate makes it 2 and clears both fallbacks — and it is honest, because a Counter/rider swing is
also `EventKind.Attack` (with `Cause.Trigger`) and should keep the rider language, not borrow
the weapon's arc. Add `byChassis` on top (specificity 3) when a weapon should take the
caster's lane — that is how "staff = wisp tinted by chassis lane" is expressed, with one
untinted `staff-wisp` recipe and two override rows.

Shipped: 11 weapon classes (13 rows incl. the pyromancer/cleric staff lanes) and 11 recipes —
`nick-cross` · `slash-thin` · `slash-blunt` · `slash-wide` (the 1-frame hang lives in its
Rotation track, held flat for 0.07 s, which is why the axe row is the one auto with a windup) ·
`shield-shove` · `thrust-line` · `pole-swipe` · `muzzle-flash` + `smoke-line` · `censer-mote` ·
`staff-wisp`, with `arrow-streak` reused for the Longbow. Recipes are untinted where the tell
should paint them. Sounds (`hit_dagger`, `hit_axe`, …) are named but ungenerated — silent no-ops
until the manifest's 10 per-weapon stings land.

**Fixtures:** the 8 chassis starters were already covered by existing fixtures; only the three
shop-only classes had none, so `weaponry` (sabre/mace/musket vs a tanky trio) is the single
addition. Note it hangs the sabre on a *bulwark* — the first cut put it on a shade, which died
at t21 without ever swinging, i.e. a fixture that never fired the row it existed for.

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
**Impact punch may be the real readability culprit — MEASURED, not fixed (2026-07-25).** While
probing weapon frames: at `previewAdvanceSeconds` 0, every unit sits at world scale **0.750**;
0.10 s later the units that were just struck are at **1.026–1.035** — a ~37% balloon that hides
neighbouring units, their HP bars and any arc drawn near them. It is the existing target-side
`punch` rider (weapon rows carry `punch: false`), it long predates this work, and it reproduces
with every VFX instance hidden. It is a strong candidate for Jake's *"not quite clear what's
happening"* — a swing's own tell is competing with the victim inflating over it. Worth a look
before authoring more FX; the fix is a `punchAmount` value, not code.

**Small code items:** ~~byWeapon tell filter~~ (BUILT) · Honed +20% tracer brightness and the
Relic prop edge-glow (fx-runtime §S5.5 — still unbuilt, and now the only §6 lines without a data
path) · Heal Cause one-liner (Boon pulses) · hex-edged field floors (GroundFill fades radially → coins;
a hex-distance fade in the shader restores telegraph=hitbox crispness) ·
**Shader.Find → Always Included Shaders before the FIRST standalone build** (editor-only
risk flagged in P1; silent URP/Unlit fallback otherwise) · per-ability cast clip variants
(deferred — VFX carries identity).
**Asset batch remainder (~18 gens, approved):** 10 per-weapon impact stings **(now named by live
rows — `hit_dagger` / `hit_sabre` / `hit_mace` / `hit_axe` / `hit_shield` / `hit_pike` /
`hit_standard` / `hit_musket` / `hit_censer` / `hit_staff`; each is silent until generated)** · nova whump,
fissure crack, star whistle+boom, taunt horn, rally drum, mana-full tick, death knell,
execute shk-thud, Waning drone · 16 status icons · ash/crack decals · skybox.
**Shelved proposals (next wave):** 6 Waning ambient board · 7 hourglass mana rings ·
10 prop idle life · 8 Overtime (its own slice).
**Adjacent:** fight-legibility Phase 4 client UI (damage chart + forecast — sim side
DONE, needs a shell home) · camera-pitch/framing pass with Jake.
