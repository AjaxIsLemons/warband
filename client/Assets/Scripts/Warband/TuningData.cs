using System;
using System.Collections.Generic;
using UnityEngine;
using Warband.Sim;

public enum FeedbackSide { Source, Target }

/// <summary>How a tell travels source→target: None (in-place flash — today's behavior), Lunge (the
/// source steps into its hit), Tracer (a pooled emissive streak flies the gap), Burst (a pooled pop
/// at the endpoint — death poof / arrival spark). Orthogonal to <see cref="FeedbackSide"/>: motion
/// plays source→target while the flash/number payload still lands on the tell's side. See directed-tells.md.</summary>
// Arc = the SOURCE unit hops from where it left to where it landed, along a parabola. It exists
// because a leap is a genuine sim teleport (ADR 0018 keeps it instant on purpose), so the body has
// to cover that distance as pure decoration inside the tick — see the Arc notes in ReplayPlayer.
public enum MotionKind { None, Lunge, Tracer, Burst, Arc }

/// <summary>
/// The full presentation tuning surface. Plain [Serializable] data so it BOTH draws in the
/// Inspector (the human's surface) AND round-trips through Newtonsoft JSON (the source of
/// truth in StreamingAssets/tuning.json, editable by hand or by an agent). See render-polish.md.
/// </summary>
[Serializable]
public class TuningData
{
    public CameraTune camera = new CameraTune();
    public PostTune post = new PostTune();
    public NumberTune numbers = new NumberTune();
    public ImpactTune impact = new ImpactTune();
    public FieldTune fields = new FieldTune();
    public StoryTune story = new StoryTune();
    public WaningTune waning = new WaningTune();
    public MotionTune motion = new MotionTune();
    public BeatTune beats = new BeatTune();
    public BarsTune bars = new BarsTune();
    public ModelsTune models = new ModelsTune();
    public BoardTune board = new BoardTune();
    public EnvironmentTune environment = new EnvironmentTune();
    public PlaybackTune playback = new PlaybackTune();
    public AudioTune audio = new AudioTune();
    public RevisionPresentationTune revision = new RevisionPresentationTune();
    public FxTune fx = new FxTune();
    // Replace (not populate) on reload, or PopulateObject appends the file's tells to the existing
    // list every time. Only the list needs this; the groups above populate in place so live
    // references to them survive a reload — see TuningIO.Settings().
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<TellDef> tells = new List<TellDef>();
}

/// <summary>
/// Zone colors by what the glyph DOES (Warband.Sim FieldFlavor), not by who cast it — the
/// render-polish color language: red=damage · green=heal · cyan=buff · purple=debuff. Neutral
/// keeps the old catch-all yellow, so a glyph whose meaning the sim won't guess stays legible
/// without being colored wrong.
/// </summary>
[Serializable]
public class FieldTune
{
    public Color hazard  = new Color(0.95f, 0.35f, 0.20f);
    public Color boon    = new Color(0.35f, 0.90f, 0.45f);
    public Color buff    = new Color(0.35f, 0.85f, 0.95f);
    public Color debuff  = new Color(0.65f, 0.40f, 0.85f);
    public Color neutral = new Color(0.95f, 0.80f, 0.35f);
    public Color wall    = new Color(0.55f, 0.55f, 0.60f);

    // ---- deployment muster rings (ADR 0014) ----
    // Its own color rather than reusing `buff`: a muster ring is read in a quiet planning screen
    // against a still board, not glanced at mid-fight, and the two want different weights.
    public Color muster       = new Color(0.98f, 0.84f, 0.42f);
    /// <summary>Rim alpha for a ring whose hero is NOT selected. Every placed muster stays up —
    /// the ring's whole job is guiding where the NEXT hero goes — so an unselected muster keeps a
    /// readable OUTLINE and gives up its floor entirely (`musterQuietFill` 0). Overlapping musters
    /// can cover most of a deploy half, and dim floors merely wash that whole area out.</summary>
    public float musterQuiet       = 0.85f;
    public float musterQuietFill   = 0f;
    public float musterBright      = 1.30f;
    public float musterBrightFill  = 1f;
}


/// <summary>
/// Fight-story overlay: the kill feed's lifetime/size, the win-banner size, and how long the
/// playhead holds on the end tick (banner + readout) before a loop wraps. Read live every frame
/// like the field colors, so a tuning.json hot-reload resizes/hides them with no rebuild.
/// </summary>
[Serializable]
public class StoryTune
{
    public bool feedShow = true;
    [Range(1f, 10f)] public float feedLifeSeconds = 5f;
    [Range(0.005f, 0.2f)] public float feedSize = 0.02f;
    /// <summary>Baseline-to-baseline distance as a multiple of characterSize. The current bold
    /// font's 0.02-size bounds are 0.287 world-units tall; 17x gives 0.34 units and 18% leading,
    /// keeping simultaneous death lines distinct instead of overlapping by nearly half.</summary>
    [Range(12f, 24f)] public float feedLineSpacing = 17f;
    [Range(0.01f, 0.4f)] public float bannerSize = 0.056f;
    [Range(0f, 15f)] public float endHoldSeconds = 4f;
    /// <summary>Where the kill/announce feed sits, in hexes out from the board's +X edge and in
    /// world units above the board plane. Hard-coded at 1.6 / 3.0 until 2026-07-27, and that made
    /// it a silent veto on every camera experiment: the feed's anchor alone sits 1.17x the board's
    /// own width out, and its billboarded lines run further still, so the frame has to be wide
    /// enough for the board PLUS a text column beside it. Narrow the FOV to make units bigger and
    /// the feed is the first thing off the screen. Negative gap pulls it back over the board's
    /// top corner, which is where most games in the genre put it. Since ADR 0027 widened the board,
    /// the feed is right-anchored and defaults just inside that edge; its lines grow left into the
    /// stable board frame instead of widening the camera budget. See Design/sim-render-audit.md §3.</summary>
    [Range(-6f, 6f)] public float feedGapHexes = -0.25f;
    [Range(0f, 8f)] public float feedHeight = 3f;
}

/// <summary>
/// THE WANING — the overtime clock, made visible (roadmap item 11). The sim has dealt Cause.Storm
/// damage to every living unit every tick past Battle.OvertimeStartTick since the first build, and
/// the client drew NOTHING for it: no clock, no warning, no tell. The pitch calls this a pillar
/// ("an escalating overtime clock guarantees resolution") and theme.md names it the Waning, the Hour
/// running out — but on screen a long fight simply became "units started dying for no reason".
///
/// It renders GLOBALLY, as one clock, and that is a design choice rather than an economy: the storm
/// strikes every living unit every tick, so per-body damage numbers would be ~40 floating numbers a
/// second across the board — a blizzard that buries the fight instead of explaining it. The clock
/// carries the state (how long, how bad, getting worse); the feed carries the two moments that
/// matter (it is coming, it is here); the Storm tell row carries the per-unit flash, deliberately
/// with numbers OFF.
/// </summary>
[Serializable]
public class WaningTune
{
    public bool show = true;
    [Range(0.01f, 0.2f)] public float size = 0.034f;
    [Range(0f, 10f)] public float height = 4.6f;      // world Y of the clock, above the end banner
    // Sim ticks of warning before the storm opens (10 ticks = 1 s, ADR render contract). 150 = 15 s,
    // long enough to change a plan, short enough that it is not background noise.
    [Range(0, 900)] public int warnLeadTicks = 150;
    public Color normalColor = new Color(0.72f, 0.76f, 0.84f);
    public Color warnColor = new Color(1f, 0.78f, 0.30f);
    public Color stormColor = new Color(1f, 0.42f, 0.36f);
}

/// <summary>
/// How a walk READS. The sim owns the walk's timing completely — departure hex, destination and
/// arrival tick all arrive on the event log — so nothing here can change where a unit is, only how
/// convincing the trip looks. Position is a straight line across the sim's window (constant speed
/// is what makes a chase legible); the bob and the turn are the only liberties taken.
/// </summary>
[Serializable]
public class MotionTune
{
    [Range(0f, 0.3f)] public float bobHeight = 0.07f;   // world units of vertical footfall
    [Range(0f, 4f)] public float bobSteps = 2f;         // footfalls per hex — 2 = left, right
    [Range(0f, 0.3f)] public float leanAmount = 0.05f;  // forward pitch (radians-ish) while walking
    [Range(1f, 30f)] public float turnSpeed = 14f;      // how fast the body swings to face its path
    // Non-walk corrections only (teleports, leaps, a rebuilt view catching up). A walk is driven
    // exactly, never chased, or the body would trail the hex the sim says it occupies.
    [Range(1f, 40f)] public float snapSpeed = 12f;
}

[Serializable]
public class CameraTune
{
    [Range(-180f, 180f)] public float yaw = 0f;       // orbit around the board
    [Range(5f, 89f)] public float pitch = 52f;        // elevation angle
    [Range(0.4f, 3f)] public float distance = 1.25f;  // multiple of board span
    /// <summary>Vertical field of view. Lived ONLY in `Game.unity` until 2026-07-27, which made it
    /// the one camera number unreachable from F1 — and the one that was furthest wrong: at 60° the
    /// board covered ~57% of frame width and the enemy back rank rendered ~63 px tall at 1080p,
    /// three pixels over fight-legibility's own 60 px silhouette gate. Narrowing it is also the
    /// only lever on the near/far taper (2.29× today: a back-row unit gets 44% of a front-row
    /// unit's pixels), because taper is set by the camera's DISTANCE and a wide FOV forces it close.
    /// Shipped at 60 = the scene value, so this is a slider, not a silent re-frame; the value that
    /// caps how far it can be narrowed is the world-space kill feed's horizontal budget
    /// (see Design/sim-render-audit.md §3).</summary>
    [Range(15f, 75f)] public float fov = 60f;
    /// <summary>Pulls the camera's look-at from board center toward the near (player) edge, as a
    /// fraction of the near half-span. At high pitch the near half of the board projects much
    /// taller than the far half, so aiming at true center leaves the top of the frame empty while
    /// the front rank clips the bottom — distance can trade the two but never center them. 0 = aim
    /// at center (pre-8×8 behavior).</summary>
    [Range(-1f, 1f)] public float aimBias = 0f;
    public Color background = new Color(0.055f, 0.06f, 0.08f);
}

/// <summary>Playback pacing. ticksPerSecond IS the battle speed (1 tick = 100ms at the contract
/// default of 10) and lives in tuning so the F1 save persists it — it used to be only a live
/// field on ReplayPlayer, which is how a 10000 t/s fast-forward experiment got baked into the
/// scene as the reboot default (Jake 2026-07-25).</summary>
[Serializable]
public class PlaybackTune
{
    [Range(1f, 40f)] public float ticksPerSecond = 10f;
    /// <summary>The opening beat: seconds the deployed board is held STILL before the playhead
    /// starts. Tick 0 is the busiest tick of a fight — both lines step off and every AtStart trigger
    /// fires at once — so without a moment of stillness first, the player has nothing to read the
    /// opening against. 0 = start instantly (the old behavior).</summary>
    [Range(0f, 3f)] public float openingHoldSeconds = 0.7f;
}

/// <summary>Combat audio master. Disabled after the first standalone-build play pass: the generated
/// stings were too long and noisy to support legibility. Keep the seam so the future options screen
/// can expose it and a replacement sound pass can be auditioned without rewiring every tell.</summary>
[Serializable]
public class AudioTune
{
    public bool enabled = false;
}

/// <summary>
/// The flagship Revision ceremony. Every duration is REAL time: battle speed changes how quickly
/// the ordinary replay advances, never how long it takes the player to seize, unwrite, and land an
/// Hour. Keeping the complete sentence here prevents the same beat from being retuned independently
/// in RunShell, ReplayPlayer, UI Toolkit, and audio.
/// </summary>
[Serializable]
public class RevisionPresentationTune
{
    [Range(0.2f, 2f)] public float firstOpenSeconds = 0.90f;
    [Range(0.05f, 0.5f)] public float reopenSeconds = 0.18f;
    [Range(0.05f, 0.6f)] public float tearSeconds = 0.25f;
    [Range(0.02f, 0.5f)] public float scrubPerSecond = 0.30f;
    [Range(0.02f, 0.4f)] public float scrubMinSeconds = 0.12f;
    [Range(0.1f, 1f)] public float scrubMaxSeconds = 0.85f;
    [Range(0.5f, 3f)] public float rewindBaseSeconds = 1.35f;
    [Range(0f, 0.4f)] public float rewindPerSecond = 0.10f;
    [Range(0.5f, 3f)] public float rewindMaxSeconds = 1.85f;
    /// <summary>Battle-seconds of already-witnessed run-up replayed before the split (Jake
    /// 2026-07-29). 0 disables the run-up and lands on the anchor exactly as before.</summary>
    [Range(0f, 10f)] public float runUpSeconds = 5f;
    /// <summary>How held the board stays while the run-up re-treads known ground, 0..1. Colour
    /// floods back on the landing punch, so this is what makes the split read as the change.</summary>
    [Range(0f, 1f)] public float runUpDress = 0.45f;
    [Range(0.02f, 0.4f)] public float vacuumSeconds = 0.10f;
    /// <summary>Hold ON the fork frame before the landing punch, so the split registers as a beat
    /// instead of a flicker (Jake 2026-07-29: "pause briefly on the fork moment").</summary>
    [Range(0f, 1.5f)] public float forkHoldSeconds = 0.55f;
    [Range(0.05f, 0.8f)] public float landingSeconds = 0.30f;
    [Range(0.2f, 2f)] public float receiptSeconds = 0.55f;
    [Range(0.1f, 1f)] public float receiptTailSeconds = 0.35f;

    [Range(0f, 1f)] public float heldLight = 0.58f;
    [Range(-100f, 20f)] public float heldSaturation = -62f;
    [Range(0f, 1f)] public float heldVignette = 0.46f;
    [Range(0f, 1f)] public float fractureStrength = 0.85f;
    [Range(0.5f, 8f)] public float fractureEdgeWidthPx = 2.2f;
    [Range(0f, 4f)] public float fractureEdgeGlow = 1.45f;
    [Range(0f, 16f)] public float fractureRefractionPx = 5.5f;
    [Range(0f, 16f)] public float fracturePlateSlipPx = 8.5f;
    [Range(0f, 6f)] public float fractureChromaticPx = 1.75f;
    [Range(0f, 1f)] public float fractureFutureOpacity = 0.92f;
    [Range(0f, 0.5f)] public float fractureHeldSeamStrength = 0.16f;
    [Range(0f, 2f)] public float fractureSandFlow = 1f;
    [Range(0f, 1f)] public float futureEchoAlpha = 0.34f;
    [Range(0, 6)] public int rewindEchoCount = 3;
    [Range(0f, 1.5f)] public float landingPunch = 0.52f;
    [Range(0f, 0.6f)] public float landingShake = 0.18f;

    [Range(0.05f, 1f)] public float reducedOpenSeconds = 0.25f;
    [Range(0.1f, 2f)] public float reducedRewindSeconds = 0.55f;
    [Range(0.2f, 2f)] public float reducedReceiptSeconds = 0.70f;
}

/// <summary>Board geometry (Jake 2026-07-25: "space the hexes out — things feel cluttered").
/// hexSize = world spacing between hex centers (units reposition live; tiles rebuild on
/// reload); tileScale = tile footprint within its cell (smaller = wider gap lines).</summary>
[Serializable]
public class BoardTune
{
    [Range(0.6f, 2f)] public float hexSize = 1.15f;
    [Range(0.5f, 1f)] public float tileScale = 0.9f;
}

/// <summary>The shard frame (item 35 Stage 1, `Design/theme.md` "salvage spine"): the battle is
/// fought on the last coherent shard of a dying era, floating in the void outside time. Everything
/// here is render-only dressing built under ~generated — the sim never knows it exists, and every
/// vertex is a deterministic function of (tuning, seed) so contact sheets stay byte-identical.
/// enabled=false restores the bare pre-item-35 board exactly.</summary>
[Serializable]
public class EnvironmentTune
{
    public bool enabled = true;

    // The shard: a fractured cliff skirt falling away from the board's rim into the void.
    [Range(0.2f, 3f)] public float shardMargin = 0.9f;    // rim overhang beyond the tiles, in hexSize units
    [Range(0.5f, 6f)] public float shardDepth = 2.6f;     // how far the cliff falls
    [Range(0f, 1f)] public float shardJagged = 0.55f;     // fracture displacement amount
    [Range(1, 9999)] public int shardSeed = 7;            // pick a different tear
    public Color shardRim = new Color(0.086f, 0.129f, 0.192f);   // Slate — the Hall's palette
    public Color shardCliff = new Color(0.051f, 0.078f, 0.118f); // Obsidian
    public Color shardKeel = new Color(0.027f, 0.043f, 0.067f);  // Ink

    // Board surface: quantized per-tile value jitter + a short skirt on each tile so the grout
    // channels read as recessed cuts (SSAO does the rest).
    [Range(0f, 0.2f)] public float tileVariation = 0.05f;
    [Range(0f, 0.15f)] public float tileBevelDepth = 0.06f;

    // The void: an inverted gradient dome. At the play camera (pitch 42) the visible band beyond
    // the far rim is BELOW the horizon, so the glow band sits below the equator by default.
    public Color voidTop = new Color(0.030f, 0.038f, 0.058f);
    public Color voidGlow = new Color(0.095f, 0.115f, 0.165f);
    [Range(-1f, 1f)] public float voidGlowHeight = -0.35f;  // -1 = bottom pole, 0 = equator, 1 = top
    [Range(0.02f, 0.8f)] public float voidGlowWidth = 0.25f;
    [Range(0, 12)] public int debrisCount = 6;              // torn shard-lets adrift below the board

    // Authored void backdrop (item 35 Stage 2). A feathered billboard hung in the deep rather than
    // a dome texture: the shard's void art is a VERTICAL depth composition, and squashing it into
    // an equirect latitude band destroys the very depth it exists to show (measured 2026-07-30).
    // Empty path = gradient dome only, exactly as Stage 1 shipped.
    public string voidArt = "";                             // Resources path under Board/
    [Range(-180f, 180f)] public float voidArtYawDeg = -12f;  // bearing from board centre (0 = +Z, far side)
    // Sits BEYOND the Tower (towerDistance 150) so the Tower still silhouettes against it — ADR 0010
    // keeps the Tower the constant, so the backdrop must never occlude it.
    [Range(20f, 215f)] public float voidArtDistance = 180f;  // inside the dome's 220 radius
    [Range(-320f, 40f)] public float voidArtCenterY = -140f;
    [Range(5f, 400f)] public float voidArtWidth = 240f;      // height follows the texture's aspect
    [Range(0f, 1f)] public float voidArtOpacity = 1f;

    // The Tower — the constant, visible from every shard (ADR 0010 law 3). It rises from DEEPER
    // in the void: the play camera looks down, so a horizon tower would never be in frame.
    public bool towerEnabled = true;
    [Range(-180f, 180f)] public float towerYawDeg = -16f;   // direction from board center (0 = +Z, the far side)
    [Range(30f, 200f)] public float towerDistance = 110f;
    [Range(-80f, 60f)] public float towerTopY = -14f;       // where the spire tip reaches
    [Range(20f, 160f)] public float towerHeight = 85f;      // rise from base to tip
    public Color towerColor = new Color(0.035f, 0.045f, 0.070f);

    // Light rig on top of the scene's authored key: cool fill + a warm Sand rim from behind —
    // the one warmth in the frame (Sand is time; everything else in the void stays cold).
    [Range(0f, 1.5f)] public float fillIntensity = 0.35f;
    public Color fillColor = new Color(0.45f, 0.62f, 0.85f);   // Tower blue, desaturated
    [Range(0f, 2f)] public float rimIntensity = 0.6f;
    public Color rimColor = new Color(0.851f, 0.643f, 0.227f); // Sand

    // Stage 2: the era's dressing kit planted on the fracture shelf.
    public RimDressTune rim = new RimDressTune();
}

/// <summary>One entry in an era's dressing kit. `model` is a Resources path under Board/ — that
/// indirection is the whole point: the next era (KayKit Medieval Hexagon, CC0) is a data edit
/// here, not a code change.</summary>
[Serializable]
public class RimPropTune
{
    public string model = "";                            // e.g. "KayKit/Props/spear_A"
    [Range(0f, 8f)] public float weight = 1f;            // relative share of the prop budget
    // Size in WORLD UNITS along the prop's longest axis, not a raw multiplier. KayKit authors its
    // packs at wildly different scales — a Weapons Bits spear mesh is 0.031u long (it only works in
    // a hand slot because the rig's bone chain scales it), while a dungeon banner is 3.7u — so a raw
    // multiplier would need a magic constant per kit and break "the next kit is a data edit".
    [Range(0.05f, 6f)] public float targetSize = 1.2f;
    [Range(0f, 1f)] public float sizeJitter = 0.25f;
    [Range(-180f, 180f)] public float pitchDeg = 0f;     // stand up a prop authored lying down
    [Range(-1f, 1f)] public float sink = 0.03f;          // bed the measured BASE into the shelf
}

/// <summary>Rim dressing (item 35 Stage 2): the kit planted along the shard's fracture shelf so the
/// board edge reads as a place someone fought over. Placement is a deterministic function of
/// (tuning, seed); enabled=false leaves the Stage 1 frame exactly as it shipped.</summary>
[Serializable]
public class RimDressTune
{
    public bool enabled = true;
    [Range(0, 80)] public int count = 16;
    [Range(1, 9999)] public int seed = 21;

    // Where to stand, as a fraction of shardMargin measured OUT from the board's envelope. The
    // BoardBase plane ends at exactly the envelope, and the cliff's ring 0 is +0.2 / ring 1 is +1.0
    // — so positive values put props on the narrow falling ledge and NEGATIVE values plant them on
    // the visible flat apron between the outermost tiles and the lip. The apron is what reads.
    [Range(-1.4f, 1.4f)] public float bandInner = -0.42f;
    [Range(-1.4f, 1.4f)] public float bandOuter = -0.06f;

    // The camera lives on -Z. Props inside this arc stand between lens and front rank, so the
    // near side stays bare (Stage 1 learned the same lesson with debris).
    [Range(0f, 200f)] public float nearGapDeg = 96f;

    [Range(0f, 30f)] public float leanDeg = 7f;          // seeded lean off vertical
    [Range(0f, 180f)] public float yawJitterDeg = 25f;   // off the outward-facing normal
    public Color tint = new Color(0.44f, 0.49f, 0.58f);  // multiplies the kit texture: desaturate + darken

    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<RimPropTune> props = new List<RimPropTune>();
}

/// <summary>KayKit board models (fight-legibility Phase 2). enabled=false collapses every unit
/// back to the primitive silhouettes; a chassis with no model entry falls back automatically.</summary>
[Serializable]
public class ModelsTune
{
    public bool enabled = true;
    [Range(0.3f, 1.5f)] public float scale = 0.75f;   // KayKit minis are ~1.9u tall at 1.0
    [Range(0f, 1.2f)] public float barLift = 0.35f;   // extra bar/pip height over the model
}

/// <summary>Unit bar conventions (fight-legibility Phase 1). Ally-green/enemy-red HP (the TFT
/// convention), segment ticks every hpPerSegment so absolute HP reads without text, and the
/// Underlords mana law: the bar COLOR-FLIPS + pulses the instant a cast is ready — a discrete
/// event the eye can't miss, instead of an analog fill it has to measure.</summary>
[Serializable]
public class BarsTune
{
    [Min(0)] public int hpPerSegment = 25;
    public Color allyHp = new Color(0.35f, 0.85f, 0.35f);
    public Color enemyHp = new Color(0.90f, 0.36f, 0.30f);
    // Shield rides at the HP tip (TFT convention, ui-review unit-hud-readability P3). Pale
    // grey-white, not blue: saturated blue is mana's hue and must stay unambiguous.
    public Color shield = new Color(0.88f, 0.93f, 0.97f);
    // The delayed damage trail (ui-review P5): the real fill snaps with the fold; this pale
    // segment drains after it on a t² ease-in (the ease manufactures the hold). Its length is the
    // delta rendered at full bar scale — the readback numbers alone carried until now.
    public Color trail = new Color(0.96f, 0.87f, 0.72f);
    [Range(0.2f, 2.5f)] public float trailSeconds = 0.8f;
    public Color mana = new Color(0.35f, 0.55f, 0.95f);
    public Color manaReady = new Color(0.91f, 0.96f, 1.00f);
    [Range(0f, 2f)] public float manaReadyPulse = 0.9f;
}

/// <summary>Beat sequencing + hit-stop (render-polish "the spine", fight-legibility Phase 1).
/// A beat = every event of one sim tick. Distinct causal chains inside a beat fire with a small
/// stagger so simultaneous casts don't visually cancel; blocking events (Death, crits) HOLD the
/// playhead for a few real ms while Director-stepped FX keep animating — never Time.timeScale.
/// Stagger/holds are authored at 10 t/s and compress on fast-forward like tell motion.</summary>
[Serializable]
public class BeatTune
{
    public bool enabled = true;
    // Seconds between distinct causal chains (Root groups) inside one tick's beat.
    [Range(0f, 0.1f)] public float stagger = 0.045f;
    // Real-seconds playhead hold on a Death — the loudest beat buys silence around it.
    [Range(0f, 0.4f)] public float deathHold = 0.14f;
    // Smaller hold on a crit landing.
    [Range(0f, 0.3f)] public float critHold = 0.06f;
}

[Serializable]
public class PostTune
{
    [Range(0f, 3f)] public float bloomIntensity = 0.7f;
    [Range(0f, 2f)] public float bloomThreshold = 1.1f;
    [Range(0f, 1f)] public float vignette = 0.30f;
    [Range(-60f, 60f)] public float saturation = 14f;
    // OFF by default (2026-07-25): world-space text is transparent and writes no depth, so DoF
    // blurs text pixels by whatever is BEHIND them — text over the void gets far-plane blur,
    // which split half-on/half-off-board text boxes down the middle. Tilt-shift needs a
    // text-overlay camera stack before it can come back.
    public bool dofEnabled = false;
    public float dofStart = 26f;
    public float dofEnd = 44f;
}

/// <summary>
/// Combat numbers, including the anti-overlap release schedule. EVERY damage instance still gets
/// its own number — nothing is merged or dropped — so the crowding is solved on the TIME and SPACE
/// axes instead: a unit's numbers launch from <see cref="columns"/> fixed lanes, and a lane can't
/// fire again until <see cref="releaseGap"/> has passed. A 6-hit burst therefore reads as a rhythm
/// (which is the "she got hit six times" story) rather than a stack.
///
/// Defaults are sized from a sweep over the seven replay fixtures: 2 columns @ 0.10s holds every
/// number in every fixture under 0.3s of delay (p90 ≤ 0.10s), so nothing detaches from its cause.
/// One column does NOT work — glyphwar backs up 2.1s.
/// </summary>
[Serializable]
public class NumberTune
{
    [Range(0.01f, 0.25f)] public float characterSize = 0.06f; // global readability base
    public int fontSize = 72;
    [Range(0.5f, 4f)] public float riseSpeed = 2.4f;
    [Range(0.2f, 2f)] public float lifeSeconds = 0.8f;

    [Range(1, 4)] public int columns = 2;                 // parallel launch lanes per unit
    [Range(0f, 0.4f)] public float releaseGap = 0.10f;    // min seconds between two numbers in one lane
    [Range(0f, 1.5f)] public float columnSpread = 0.5f;   // world units between adjacent lanes
    [Range(0f, 2f)] public float columnDrift = 0.5f;      // outward splay speed, so lanes keep parting
    // Safety valve for content denser than the lanes can drain: past this, a number fires anyway and
    // accepts an overlap rather than drifting away from the hit that caused it. Never trips today.
    [Range(0.05f, 1f)] public float maxHold = 0.4f;

    // Cross-unit separation. Per-unit lanes only de-clutter ONE unit's numbers; the bulk of a dense
    // fight's collisions are neighbouring units piling into the same airspace (the "adjacent-hex"
    // bucket, the biggest one). Two deterministic screen-x pushes, applied to launch position AND
    // velocity so trajectories DIVERGE over life (parallel columns re-collide; splayed ones don't):
    //  · outwardBias — push a unit's numbers AWAY from board centre in screen-x, so the two flanks
    //    fan apart. World-space board centre is a fixed constant here, unlike an action game.
    //  · unitJitter  — a STABLE per-unit offset (hashed off unit id, not Random) so two neighbours
    //    on the same side of centre still separate. Deterministic → frozen captures stay reproducible.
    [Range(0f, 1.5f)] public float outwardBias = 0.6f;
    [Range(0f, 1f)] public float unitJitter = 0.25f;

    // Attribution (ui-review unit-hud-readability P1, D3's law mapped to PvE where the player is
    // always Team0): damage LANDING keeps the tell's type color, damage TAKEN overrides to one
    // hostile crimson — "my output" vs "incoming" reads before the digits do. Gold stays reserved
    // for the player's own crits; an incoming crit brightens within the crimson family instead.
    public Color allyHit = new Color(1.00f, 0.25f, 0.19f);
    public Color allyHitCrit = new Color(1.00f, 0.42f, 0.28f);

    // FFXIV's free channel (ui-review P7): a crit's number carries a literal "!" — legible at any
    // size, colorblind-safe, survives a screenshot.
    public bool critBang = true;
}

/// <summary>
/// Magnitude → spectacle. One normalized intensity t = (amount / bigHit)^curve drives EVERY
/// channel below, so "bigger hits feel bigger" stays a single tunable idea instead of five
/// unrelated hacks: a chip hit is small/quiet/brief, a haymaker is huge, gold, launches high and
/// hangs. Per-tell numberScale still multiplies on top, so a tell can stay quiet at any magnitude.
/// </summary>
[Serializable]
public class ImpactTune
{
    public bool enabled = true;
    [Min(1f)] public float bigHit = 40f;             // amount that reads as full power (t = 1)
    [Range(0.2f, 3f)] public float curve = 0.85f;    // <1 lets small hits already grow; >1 saves it for whoppers

    [Range(0.1f, 3f)] public float minScale = 0.9f;  // number size at t=0
    [Range(0.5f, 6f)] public float maxScale = 2.2f;  // number size at t=1
    [Range(0f, 3f)] public float riseBoost = 0.6f;   // extra launch speed at t=1 (fraction)
    [Range(0f, 3f)] public float lifeBoost = 0.5f;   // extra hang time at t=1 (fraction)
    [Range(0f, 3f)] public float punchBoost = 0.8f;  // extra target recoil at t=1 (fraction)

    // One global dial over EVERY impact recoil, base included. punchBoost only scales the magnitude
    // TERM, so driving it to 0 still leaves each tell's flat punchAmount (0.25 default = +25% body
    // scale) and costs the small-vs-big-hit difference this class exists to express. The recoil grows
    // the body outward on a board whose neighbours are ~2 world units apart, so at 1.0 a struck unit
    // covered its neighbours, their bars, and any arc near them (roadmap item 10). Retune here, live.
    [Range(0f, 2f)] public float punchScale = 0.5f;

    // Hot-white, deliberately NOT gold: crit already owns gold as a categorical signal, so a heavy
    // normal hit must not read as a crit. Magnitude = brightness, crit = hue; a big crit is both.
    public Color heavyTint = new Color(1f, 0.95f, 0.86f);
    [Range(0f, 1f)] public float tintAmount = 0.5f;      // how far toward heavyTint at t=1

    // The magnitude→presentation ramp (ui-review P7, Jake 2026-07-28: LUMINANCE, not alpha).
    // Small hits live shorter and spawn dimmer; every number luminance-decays toward dark over
    // its back half (Hades' white→black law) so spent numbers self-extinguish against bright
    // VFX. Alpha stays reserved for the final fade — translucency reads as "expiring".
    [Range(0.3f, 1f)] public float lifeFloor = 0.75f;   // life multiplier at t=0
    [Range(0.3f, 1f)] public float dimFloor = 0.72f;    // spawn luminance at t=0
    [Range(0f, 1f)] public float endLum = 0.25f;        // luminance at end of life (all numbers)
    [Range(0f, 2f)] public float critPop = 0.5f;        // crit-only spawn overshoot (fraction over 1×)

    /// <summary>0..1 spectacle for a hit of <paramref name="amount"/>. Disabled → always 0, which
    /// makes every boost term vanish and leaves the old flat behaviour (bar minScale).</summary>
    public float Intensity(float amount)
    {
        if (!enabled) return 0f;
        float n = Mathf.Clamp01(Mathf.Abs(amount) / Mathf.Max(1f, bigHit));
        return Mathf.Pow(n, Mathf.Max(0.01f, curve));
    }
}

[Serializable]
public class TellDef
{
    public EventKind eventKind = EventKind.DamageDealt;
    public FeedbackSide side = FeedbackSide.Target;

    // Optional signature filters. A tell fires only on events that match, and the MOST specific
    // matching tell wins — so a filterless "DamageDealt" is the fallback and a "cause: Burn" tell
    // overrides it for burn ticks. Matching/precedence live in Warband.Sim.TellMatch (tested).
    public bool byCause = false;
    public Cause cause = Cause.Attack;
    public bool byStatus = false;         // for StatusApplied/StatusExpired
    public StatusKind status = StatusKind.Burn;
    // Ranged := Hex.Distance(src,tgt) >= 2 — the sim's own projectile law (Battle.cs:254), so a
    // melee lunge and a projectile tracer key off the SAME threshold the sim blocks shots on.
    public bool byRanged = false;
    public bool ranged = true;
    // Chassis := the SOURCE unit's ChassisId from the fold — per-caster cast/attack identity
    // without the event carrying an ability id (directed-tells' flagged growth path).
    public bool byChassis = false;
    public string chassis = "";
    // Ability := the SOURCE unit's resolved ability identity (Warband.Content.AbilityIdentity:
    // its last SignatureOverride trait, else the chassis) — "pyromancer" vs "pyro.starfall". A
    // strictly narrower filter than chassis, so it carries +2 specificity, not +1.
    public bool byAbility = false;
    public string ability = "";
    // Weapon := the SOURCE unit's WeaponName from the fold's identity block ("Greataxe", "Twin
    // Daggers", "Matchlock Musket" — the catalog's exact Name strings). This is what gives autos a
    // per-weapon language (combat-spectacle §6) instead of one swing per chassis. A PEER of chassis
    // at +1 specificity, not a narrower filter: weapons and chassis cross freely, so a byWeapon row
    // TIES a byChassis one and the tie falls to registry order.
    public bool byWeapon = false;
    public string weapon = "";
    // Flavor := FieldCreated's derived FieldFlavor (Aux3) — a hazard glyph vs a boon glyph.
    public bool byFlavor = false;
    public FieldFlavor flavor = FieldFlavor.Hazard;
    // Rule := the id of the PASSIVE this TriggerFired/RuleChanged event names, resolved from the
    // event's Aux index against the battle's rule table ("berserker.bloodreaver.redharvest",
    // "Greataxe/mastery", "banner.chorus", "crown.bell"). This is the seam the whole passive layer
    // extends through: a new spec node is identified the day it is authored and matches the
    // filterless fallback, and giving it a bespoke look is one row here — no code, no recompile,
    // and art attaches through the vfx fields below. +2 specificity like ability: a rule id names
    // exactly one authored passive, so it must outrank a chassis- or weapon-scoped row rather than
    // tie with it. See Design/passive-legibility.md.
    public bool byRule = false;
    public string rule = "";

    [Newtonsoft.Json.JsonIgnore] public Cause? CauseFilter => byCause ? cause : (Cause?)null;
    [Newtonsoft.Json.JsonIgnore] public StatusKind? StatusFilter => byStatus ? status : (StatusKind?)null;
    [Newtonsoft.Json.JsonIgnore] public bool? RangedFilter => byRanged ? ranged : (bool?)null;
    [Newtonsoft.Json.JsonIgnore] public string ChassisFilter => byChassis && !string.IsNullOrEmpty(chassis) ? chassis : null;
    [Newtonsoft.Json.JsonIgnore] public string AbilityFilter => byAbility && !string.IsNullOrEmpty(ability) ? ability : null;
    [Newtonsoft.Json.JsonIgnore] public string WeaponFilter => byWeapon && !string.IsNullOrEmpty(weapon) ? weapon : null;
    [Newtonsoft.Json.JsonIgnore] public FieldFlavor? FlavorFilter => byFlavor ? flavor : (FieldFlavor?)null;
    [Newtonsoft.Json.JsonIgnore] public string RuleFilter => byRule && !string.IsNullOrEmpty(rule) ? rule : null;
    [Newtonsoft.Json.JsonIgnore] public int Specificity => TellMatch.Specificity(CauseFilter, StatusFilter, FlavorFilter, RangedFilter, ChassisFilter, ability: AbilityFilter, weapon: WeaponFilter, rule: RuleFilter);

    public bool flash = true;
    public Color flashColor = Color.white;
    public Color critFlashColor = new Color(1f, 0.85f, 0.25f);
    [Min(0.01f)] public float flashSeconds = 0.2f;

    public bool punch = false;
    [Range(0f, 1f)] public float punchAmount = 0.25f;
    [Min(0.01f)] public float punchSeconds = 0.18f;

    public bool number = false;
    public Color numberColor = new Color(1f, 0.5f, 0.4f);
    public Color critNumberColor = new Color(1f, 0.85f, 0.25f);
    public int minAmount = 1;
    [Range(0.2f, 3f)] public float numberScale = 1f; // relative to the global character size
    // Per-signature band. Most same-unit collisions are MIXED signature (26 of skirmish's 30
    // same-tick bursts) — a sword hit, a burn tick and a heal landing together. Giving each tell its
    // own launch height + hang time separates them by construction, and the band itself becomes
    // readable: low/small/brief = DoT chip, high = a real hit.
    [Range(-1.5f, 2f)] public float numberBand = 0f;  // world-Y offset from the head anchor
    [Range(0.2f, 2f)] public float numberLife = 1f;   // hang multiplier on numbers.lifeSeconds

    // Motion — how the tell travels source→target. Defaults (None / defer false) preserve today's
    // in-place flash, so existing tells need no edits. Times are authored at 10 ticks/s and
    // compressed on fast-forward. New fields auto-appear in the F1 cockpit, so the bounds matter.
    // Audio sting played at IMPACT (with the flash), by clip name under Resources/Board/SFX.
    // Empty = silent. critSound overrides on a crit. Missing clips no-op, so authoring can lead audio.
    public string sound = "";
    public string critSound = "";
    // The windup channel (combat-spectacle §2 beat 1): played at StartAt, when the cast aura lights
    // up, NOT at contact. The era risers are in the asset manifest but not generated yet — a missing
    // clip is the same silent no-op as `sound`, so the rows can name them now and they just start
    // sounding once the batch lands.
    public string castSound = "";

    public MotionKind motion = MotionKind.None;
    [Range(0.02f, 1f)] public float motionSeconds = 0.15f;   // travel / lunge out-and-back duration
    // Arc AIR-TIME scales with the jump, the way arc HEIGHT already does (see DriveArc):
    // motionSeconds buys the first hex and every hex beyond it adds motionPerHexSeconds, capped by
    // motionMaxSeconds (0 = uncapped). Held flat, a 5-hex Ambush dive crosses the board in a single
    // hop's duration and reads as a teleport instead of a leap (Jake, 2026-07-26: "every unit
    // teleports somewhere"). 0 = the old flat duration; every other MotionKind ignores both.
    [Range(0f, 0.4f)] public float motionPerHexSeconds = 0f;
    [Range(0f, 3f)] public float motionMaxSeconds = 0f;
    public Color motionColor = new Color(1f, 0.95f, 0.82f);
    [Range(0f, 8f)] public float motionGlow = 3f;            // HDR multiplier so Bloom bites (threshold 0.9)
    [Range(0.2f, 4f)] public float motionScale = 1f;         // tracer thickness / burst size / ARC HEIGHT
    [Range(0f, 1f)] public float windupSeconds = 0f;         // pre-motion anticipation (casts)
    public bool defer = false;                               // ORIGIN tells only (Attack/Cast): set the impact latch

    // VFX recipe ids (VfxLibrary). EMPTY = today's Tracer/Burst primitives, so migration is
    // per-tell and every existing row keeps rendering exactly as it does now. motionColor/
    // motionGlow/motionScale tint whichever recipe fires, so the F1 loop still retunes without a
    // recompile. An unknown id logs once and falls back to the primitive.
    public string vfx = "";              // at the SOURCE at StartAt; a Sustained recipe runs through the windup
    public string projectileVfx = "";    // replaces the cube Tracer visual (same start/end/seconds)
    public string impactVfx = "";        // at contact, on the tell's side unit
    public string groundVfx = "";        // hex-anchored under the side unit

    // Riders. hitAnim gates a flinch on ImpactTune intensity so DoT ticks don't spasm; the "Hit"
    // animator state arrives in P5 and this is a silent no-op until then.
    public bool hitAnim = false;
    [Range(0f, 1f)] public float hitAnimMinT = 0.35f;
    // Story-feed line at StartAt: "«X» casts Y", Y being the resolved ability's display name. Rides
    // the kill-feed slots, so it obeys the same cap + fade — ration it (T2/T3 casts, never riders).
    public bool announce = false;
    public bool pulseGround = false;     // flare fields covering the impact hex — wired in P2 (FieldView)
    // The camera's one rationed channel (combat-spectacle §7.9, a day-one LAW). A tell marked
    // bigImpact gets a 2-frame push-in at contact and is the ONLY class allowed to ask for a trauma
    // shake — which is itself rationed to one per FxTune.shakeRationSeconds. Authored explicitly on
    // the T3 moments (Starfall, Faultline, Sarissa, Death) rather than inferred from motionGlow: a
    // silent heuristic rots the first time someone retunes a glow in the F1 cockpit.
    public bool bigImpact = false;
}

/// <summary>
/// Timings for the FX systems that outlive a single tell: the death sequence's corpse linger, the
/// field spawn/expire animations, and the status icon row. Grouped here (rather than per-tell)
/// because they are presentation LAWS — every death dissolves for the same length, or the eye stops
/// reading it as "a death". Auto-appears in the F1 cockpit like every other group.
/// </summary>
[Serializable]
public class FxTune
{
    [Range(0f, 4f)] public float deathLingerSeconds = 1.6f;    // corpse stays before it is hidden
    [Range(0.1f, 3f)] public float dissolveSeconds = 0.8f;     // _Cutoff 0→1 inside the linger
    // What the board keeps (combat-spectacle §7.1). The ash silhouette is T0 by law — it is drawn
    // once per death and never fades, so its opacity is the only thing standing between "the board
    // remembers" and "the board is a collage". graveTilt lays the dropped weapon prop down: 90 is
    // flat on its side, less stands it up against the ground.
    [Range(0f, 1f)] public float ashMarkAlpha = 0.5f;
    [Range(0f, 90f)] public float graveTilt = 82f;
    [Range(0.05f, 1.5f)] public float fieldSpawnSeconds = 0.35f;
    [Range(0.05f, 1.5f)] public float fieldExpireSeconds = 0.45f;
    [Range(1f, 4f)] public float fieldPulseBoost = 1.5f;       // brightness multiplier on a pulseGround hit
    // The quiet-idle law (combat-spectacle §4): a field at rest is TERRAIN, ~25-35% opacity and
    // below the bloom threshold. Everything loud about a field is its spawn, its pulse, or its
    // expiry — raise these and the board goes back to reading as solid slabs.
    [Range(0f, 1f)] public float fieldIdleAlpha = 0.30f;       // floor fill opacity at rest
    [Range(0f, 1f)] public float fieldEdgeAlpha = 0.55f;       // footprint rim brightness at rest
    // The status roster (§5): ~24 px at the real camera, and a gap wide enough that adjacent icons
    // read as separate glyphs rather than one strip. Control icons draw 1.2× on top of this.
    [Range(0.05f, 0.6f)] public float statusIconSize = 0.22f;
    [Range(0f, 0.3f)] public float statusIconGap = 0.06f;
    [Min(1)] public int statusIconCap = 5;                     // icons before the "+N" chip
    // The announce ration (combat-spectacle §7.3 rations callouts to S-crown moments; the feed obeys
    // the same law). A caster announcing every time its mana refills turns the four feed slots into
    // wallpaper and pushes the kills — the lines that matter — off the bottom: measured at 1.06
    // lines/s in glyphwar, where one Lifebinder casts Great Chorus six times in 6.6 s. The FIRST
    // cast is the news, so a caster that announced inside this window casts silently.
    // REAL seconds, not ticks: how fast a feed can be read doesn't change with the battle speed, so
    // a fast-forwarded fight simply announces a smaller fraction of its casts. 0 disables the ration.
    [Range(0f, 20f)] public float announceCooldownSeconds = 6f;
    /// <summary>How long the caster's era sigil is HELD past the release before it is allowed to
    /// burn out. combat-spectacle §2 specifies a four-beat cast sentence — windup, release,
    /// impacts, recovery — but the sigil used to be closed the instant the windup ended, so beats
    /// 3 and 4 had no sigil at all and the art vanished BEFORE the thing it was announcing landed.
    /// Worse, the recipe's own alpha ramp is 0.22 s against windups of 0.18-0.55 s: the shortest
    /// chassis sigils (shade 0.18, bulwark/cleric/phalanx 0.20-0.22) never reached full opacity
    /// once. Holding here means the ramp finishes, the sigil is lit under the caster while the
    /// payoff resolves, and the recipe's 0.25 s fade plays the recovery beat.
    /// Scaled by the same speed factor as windup/motion, so fast-forward compresses it too.
    /// 0 restores the old release-on-windup behaviour.</summary>
    [Range(0f, 1.5f)] public float castSigilHoldSeconds = 0.35f;
    /// <summary>Flash on ONSET, not on refresh. StatusApplied fires every time a status is
    /// re-applied and on every Burn-pool decay tick, and each one drew a full body flash saying
    /// exactly what the status icon's own stack count and countdown ring already say. Measured
    /// across the eleven committed replay fixtures: 512 refreshes + 65 decay re-announces out of
    /// 6756 tell-bearing events — 8.5% of the whole visual load, and 25.8% of castfest, the
    /// worst case at ~21 tells a second. The transition is the news; the state belongs on the icon
    /// (the genre's own lesson — persistent readouts beat transient particles).
    /// false restores a flash on every application.</summary>
    public bool statusRefreshQuiet = true;
    /// <summary>Seconds before the SAME (unit, passive) draws a loud tell again. A passive that
    /// fires on every swing is the engine running, not news — the same onset-not-refresh law the
    /// status rows follow. Measured over the eleven committed fixtures, TriggerFired alone runs
    /// 1.4–7.1 events/s against a ~21/s total visual budget, so without a ration the passive layer
    /// would cost more legibility than it buys; first-fire rate is 0.1–2.9/s, which is a channel
    /// that fits. 0 disables the ration and every fire draws. See Design/passive-legibility.md.</summary>
    [Range(0f, 10f)] public float passiveOnsetSeconds = 2.5f;

    // ---- the dress layer (combat-spectacle §7.2 · §7.5 · §7.9) ---------------
    // Deathless (§7.2). The hold is the same channel as the Death hit-stop — REAL seconds of frozen
    // playhead while the Director keeps stepping. The dim drops the board's key light rather than
    // laying a dark sheet over the screen: an overlay would darken the survivor too, and the whole
    // point is that he alone stays candle-lit. Snap dark, ease back over dimSeconds.
    [Range(0f, 1.5f)] public float deathlessHoldSeconds = 0.4f;
    [Range(0f, 1f)] public float deathlessDimAmount = 0.4f;
    [Range(0.1f, 3f)] public float deathlessDimSeconds = 0.9f;

    // The fight-ender (§7.5): the LAST death of a fight — the one that empties a side — drops the
    // PLAYHEAD to enderSlowScale for enderSlowSeconds and swells the vignette, and the existing
    // end-tick hold then slides the FightSummary readout out of that freeze.
    [Range(0.05f, 1f)] public float enderSlowScale = 0.2f;
    [Range(0f, 3f)] public float enderSlowSeconds = 0.6f;
    [Range(0f, 0.8f)] public float enderVignetteBoost = 0.28f;

    // The camera discipline law (§7.9, adopted day one — this is what stops every other channel from
    // stacking into mush). ONE channel: a push-in on `bigImpact` contacts, and a trauma shake that is
    // hard-rationed to one per shakeRationSeconds of REAL time (the announce ration's law, for the
    // same reason: how fast an eye recovers doesn't change with the battle speed). Push amount is in
    // world units along the view axis; a "2-frame" punch at 60 fps is ~0.03 s, but it reads as a jolt
    // rather than a dropped frame at ~0.12.
    [Range(0f, 1.5f)] public float cameraPunchAmount = 0.45f;
    [Range(0.02f, 0.5f)] public float cameraPunchSeconds = 0.12f;
    [Range(0f, 0.6f)] public float cameraShakeAmount = 0.14f;
    [Range(0.05f, 1.5f)] public float cameraShakeSeconds = 0.35f;
    [Range(0f, 10f)] public float shakeRationSeconds = 3f;
}
