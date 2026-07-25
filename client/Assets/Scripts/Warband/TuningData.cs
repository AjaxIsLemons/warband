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
    public BoardTune board = new BoardTune();
    public PlaybackTune playback = new PlaybackTune();
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
}

/// <summary>
/// World-space unit nameplates (chassis name over the HP bar). Read live every frame like the
/// field colors, so a tuning.json hot-reload resizes/recolors/hides them with no rebuild.
/// </summary>
[Serializable]
public class NameplateTune
{
    public bool show = true;
    [Range(0.005f, 0.2f)] public float characterSize = 0.018f;
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
    [Range(0.005f, 0.2f)] public float feedSize = 0.02f;
    [Range(0.01f, 0.4f)] public float bannerSize = 0.056f;
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

/// <summary>Playback pacing. ticksPerSecond IS the battle speed (1 tick = 100ms at the contract
/// default of 10) and lives in tuning so the F1 save persists it — it used to be only a live
/// field on ReplayPlayer, which is how a 10000 t/s fast-forward experiment got baked into the
/// scene as the reboot default (Jake 2026-07-25).</summary>
[Serializable]
public class PlaybackTune
{
    [Range(1f, 40f)] public float ticksPerSecond = 10f;
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
    // Ability := the SOURCE unit's resolved ability identity (Warband.Content.AbilityIdentity:
    // its last SignatureOverride trait, else the chassis) — "pyromancer" vs "pyro.starfall". A
    // strictly narrower filter than chassis, so it carries +2 specificity, not +1.
    public bool byAbility = false;
    public string ability = "";
    // Flavor := FieldCreated's derived FieldFlavor (Aux3) — a hazard glyph vs a boon glyph.
    public bool byFlavor = false;
    public FieldFlavor flavor = FieldFlavor.Hazard;

    [Newtonsoft.Json.JsonIgnore] public Cause? CauseFilter => byCause ? cause : (Cause?)null;
    [Newtonsoft.Json.JsonIgnore] public StatusKind? StatusFilter => byStatus ? status : (StatusKind?)null;
    [Newtonsoft.Json.JsonIgnore] public bool? RangedFilter => byRanged ? ranged : (bool?)null;
    [Newtonsoft.Json.JsonIgnore] public string ChassisFilter => byChassis && !string.IsNullOrEmpty(chassis) ? chassis : null;
    [Newtonsoft.Json.JsonIgnore] public string AbilityFilter => byAbility && !string.IsNullOrEmpty(ability) ? ability : null;
    [Newtonsoft.Json.JsonIgnore] public FieldFlavor? FlavorFilter => byFlavor ? flavor : (FieldFlavor?)null;
    [Newtonsoft.Json.JsonIgnore] public int Specificity => TellMatch.Specificity(CauseFilter, StatusFilter, FlavorFilter, RangedFilter, ChassisFilter, ability: AbilityFilter);

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
