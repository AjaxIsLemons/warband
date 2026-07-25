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
    public NameplateTune nameplates = new NameplateTune();
    public StoryTune story = new StoryTune();
    public MotionTune motion = new MotionTune();
    public BeatTune beats = new BeatTune();
    public BarsTune bars = new BarsTune();
    public ModelsTune models = new ModelsTune();
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
}

/// <summary>
/// World-space unit nameplates (chassis name over the HP bar). Read live every frame like the
/// field colors, so a tuning.json hot-reload resizes/recolors/hides them with no rebuild.
/// </summary>
[Serializable]
public class NameplateTune
{
    public bool show = true;
    [Range(0.02f, 0.2f)] public float characterSize = 0.045f;
    public Color color = new Color(0.88f, 0.88f, 0.82f); // soft off-white — reads on the dark board
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
    [Range(0.02f, 0.2f)] public float feedSize = 0.05f;
    [Range(0.04f, 0.4f)] public float bannerSize = 0.14f;
    [Range(0f, 15f)] public float endHoldSeconds = 4f;
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
    public Color background = new Color(0.055f, 0.06f, 0.08f);
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
    [Range(0f, 2f)] public float bloomThreshold = 0.9f;
    [Range(0f, 1f)] public float vignette = 0.30f;
    [Range(-60f, 60f)] public float saturation = 14f;
    public float dofStart = 16f;
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

    // Hot-white, deliberately NOT gold: crit already owns gold as a categorical signal, so a heavy
    // normal hit must not read as a crit. Magnitude = brightness, crit = hue; a big crit is both.
    public Color heavyTint = new Color(1f, 0.95f, 0.86f);
    [Range(0f, 1f)] public float tintAmount = 0.5f;      // how far toward heavyTint at t=1

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
    // Flavor := FieldCreated's derived FieldFlavor (Aux3) — a hazard glyph vs a boon glyph.
    public bool byFlavor = false;
    public FieldFlavor flavor = FieldFlavor.Hazard;

    [Newtonsoft.Json.JsonIgnore] public Cause? CauseFilter => byCause ? cause : (Cause?)null;
    [Newtonsoft.Json.JsonIgnore] public StatusKind? StatusFilter => byStatus ? status : (StatusKind?)null;
    [Newtonsoft.Json.JsonIgnore] public bool? RangedFilter => byRanged ? ranged : (bool?)null;
    [Newtonsoft.Json.JsonIgnore] public string ChassisFilter => byChassis && !string.IsNullOrEmpty(chassis) ? chassis : null;
    [Newtonsoft.Json.JsonIgnore] public FieldFlavor? FlavorFilter => byFlavor ? flavor : (FieldFlavor?)null;
    [Newtonsoft.Json.JsonIgnore] public int Specificity => TellMatch.Specificity(CauseFilter, StatusFilter, FlavorFilter, RangedFilter, ChassisFilter);

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
    public MotionKind motion = MotionKind.None;
    [Range(0.02f, 1f)] public float motionSeconds = 0.15f;   // travel / lunge out-and-back duration
    public Color motionColor = new Color(1f, 0.95f, 0.82f);
    [Range(0f, 8f)] public float motionGlow = 3f;            // HDR multiplier so Bloom bites (threshold 0.9)
    [Range(0.2f, 4f)] public float motionScale = 1f;         // tracer thickness / burst size / ARC HEIGHT
    [Range(0f, 1f)] public float windupSeconds = 0f;         // pre-motion anticipation (casts)
    public bool defer = false;                               // ORIGIN tells only (Attack/Cast): set the impact latch
}
