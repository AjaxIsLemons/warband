using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Warband.Sim;

/// <summary>
/// Replay renderer. Consumes ONLY (initial snapshot, event log) via the Warband.Sim
/// PlaybackState fold — zero combat logic client-side (render-contract.md). Play to animate
/// at 10 ticks/sec; or call BuildPreview(tick) from the editor to scrub to a frozen tick.
/// All presentation feel comes from a TuningConfig (JSON source of truth) → hot-reloadable.
/// </summary>
[ExecuteAlways]
public class ReplayPlayer : MonoBehaviour
{
    [Header("Playback")]
    public string replayFile = "replay.bytes";
    public float ticksPerSecond = 10f;
    public bool loop = true;

    [Header("Layout")]
    public float hexSize = 1f;

    // How far BuildPreview advances the FX clock so a frozen capture shows motion. 0.12 catches
    // launches and attack tracers mid-flight; bump to ~0.25 to see a cast's children (they start
    // after the 0.12 windup latch) or a death burst at full pop. Captures can set this per shot.
    [Header("Preview")]
    public float previewAdvanceSeconds = 0.12f;

    private List<PlaybackUnit> _initial = new List<PlaybackUnit>();
    private List<BattleEvent> _events = new List<BattleEvent>();
    private PlaybackState _fold;
    private int _endTick, _fxCursor, _lastPreviewTick = 60;
    private float _clock;
    private bool _playing;
    // Fight-story: the playhead holds at _endTick for story.endHoldSeconds (banner + readout) before
    // a loop wraps. _ending latches the hold so FightStats folds once, not per frame; _endHold counts it.
    private bool _ending;
    private float _endHold;

    /// <summary>Raised once when a non-looping live replay reaches its result tick.</summary>
    public event Action PlaybackEnded;
    public bool IsEnding => _ending;

    // Event-viewer ring buffer (debug Events tab). Every dispatched event is appended here (tail =
    // newest, capped to RecentCap); EventSeq ticks up once per append. Poll contract for the menu:
    // track the last EventSeq you saw — when it climbs, the (EventSeq - yourSeq) newest entries of
    // RecentEvents are what's new; when it DROPS below yours the buffer was cleared (fight start /
    // loop-wrap / scenario switch → EventSeq resets to 0), so resync from scratch.
    private const int RecentCap = 256;
    private readonly List<BattleEvent> _recent = new List<BattleEvent>(RecentCap);
    public int EventSeq { get; private set; }
    public IReadOnlyList<BattleEvent> RecentEvents => _recent;

    private readonly Dictionary<int, UnitView> _views = new Dictionary<int, UnitView>();
    // One FieldView per live field id (spawn/sustain/pulse/expiry — see FieldView). A view outlives
    // its fold entry by the length of the expiry fade, which is why removal is driven by Step
    // returning false rather than by the field leaving _fold.Fields.
    private readonly Dictionary<int, FieldView> _fieldViews = new Dictionary<int, FieldView>();
    // FieldCreated tick per field id, folded once per load. A frozen scrub builds every field the
    // fold currently holds in one pass, so without this a capture at tick 60 would show a field
    // created at tick 22 still tracing its spawn-in rim.
    private readonly Dictionary<int, int> _fieldBornAt = new Dictionary<int, int>();
    private readonly Stack<FloatingNumber> _numberPool = new Stack<FloatingNumber>();
    private readonly Stack<Tracer> _tracerPool = new Stack<Tracer>();
    private readonly Stack<Burst> _burstPool = new Stack<Burst>();
    // One pool PER RECIPE: a VfxInstance owns the object graph its recipe needs, so it can only ever
    // replay that recipe. Same lifetime as the other pools — under _generated, dropped by Build.
    private readonly Dictionary<string, Stack<VfxInstance>> _vfxPools = new Dictionary<string, Stack<VfxInstance>>();
    private Transform _generated;
    private FeedbackDirector _director;
    private Mesh _hexMesh;
    private Font _font;
    private Quaternion _numberFace = Quaternion.identity;
    private TuningConfig _tuning;
    private TuningData _data = new TuningData();

    // ---- fight-story overlay (world-space TextMesh, capture-verifiable) -------
    // Kill feed = FeedSlots fixed world-text lines beside the board (newest at top, each fading by
    // age); banner + readout = two centered lines shown during the end-tick hold. All live under
    // _generated, so Build() recreates them and a rebuild/scenario-switch clears the overlay for free.
    private const int FeedSlots = 4;
    private TextMesh[] _feedSlots;
    private TextMesh _bannerText, _readoutText;
    private readonly List<(string Text, float Age)> _feedLines = new List<(string, float)>();
    private Vector3 _feedAnchor, _bannerAnchor, _readoutAnchor;
    private static readonly Color FeedColor = new Color(0.90f, 0.92f, 0.96f);

    private static readonly Color Team0 = new Color(0.30f, 0.55f, 0.95f);
    private static readonly Color Team1 = new Color(0.90f, 0.35f, 0.30f);
    private static readonly Color TileNeutral = new Color(0.22f, 0.23f, 0.27f);
    private static readonly Color TileTeam0 = new Color(0.20f, 0.26f, 0.36f);
    private static readonly Color TileTeam1 = new Color(0.34f, 0.24f, 0.26f);
    private static readonly Color BaseDark = new Color(0.05f, 0.055f, 0.07f);
    // Silhouette accessories stay desaturated neutrals (palette law reserves bright/saturated for VFX);
    // the torso keeps the team color.
    private static readonly Color AccSteel = new Color(0.40f, 0.42f, 0.46f); // gunmetal
    private static readonly Color AccGun = new Color(0.30f, 0.31f, 0.35f);   // darker neutral

    private sealed class UnitView
    {
        public Transform Root, Body;
        public Transform PlanningMarker;
        public Renderer BodyRenderer;
        public Transform HpFill, ShieldFill, ManaFill;
        public TextMesh Nameplate;
        // The status roster over the head — fold-driven, stepped by StepFx, popped by the Director.
        public StatusIconRow Icons;
        public int MaxHp, ManaMax;
        public Color TeamColor;
        public Vector3 BodyBaseScale, Target;
        // Decorative body yaw (degrees about world-up) chased by a slerp in Update — the fold still
        // owns position (render-contract). LeanDeg is a per-chassis forward tilt baked into the
        // facing target (Shade's crouch); it composes with yaw so the lean stays "forward".
        public float TargetYaw, LeanDeg;
        // Smoothed = the lerp state chasing Target (the fold owns Target); MotionOffset = a tell's
        // transient displacement (lunge). Root.position = Smoothed + MotionOffset — the offset must
        // NOT feed back into the lerp, or a lunge would permanently drag the unit off its hex.
        public Vector3 Smoothed, MotionOffset;
        // Walking = the sim says this unit is mid-step, so Target is an exact point along that step
        // and must be TAKEN, not chased (a lerp would leave the body behind the truth). WalkPhase is
        // 0→1 across the step, driving the footfall bob only.
        public bool Walking;
        public float WalkPhase;
        public float FlashT, FlashDur = 0.2f, PunchT, PunchDur = 0.18f, PunchAmt;
        public Color FlashColor = Color.white;
        // Status-as-material (Underlords law: at this zoom the BODY carries the status, not a
        // 16px icon). Set from the fold each apply; a flash still rides on top.
        public Color StatusTint = Color.white;
        public float StatusTintAmt;
        // The cast sentence's threshold flip: ManaReady flips the fill color; the pulse is a
        // brief scale-pop on the mana bar so the flip is an EVENT, not a state you must notice.
        public bool ManaReady;
        public float ManaPulseT, ManaFillBaseH;
        // Non-null only on the KayKit model path — drives Idle/Walk; null for primitives.
        public Animator ModelAnimator;
        private MaterialPropertyBlock _mpb;

        public void ApplyVisual()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            BodyRenderer.GetPropertyBlock(_mpb);
            Color baseCol = Color.Lerp(TeamColor, StatusTint, Mathf.Clamp01(StatusTintAmt));
            _mpb.SetColor("_BaseColor", Color.Lerp(baseCol, FlashColor, Mathf.Clamp01(FlashT)));
            BodyRenderer.SetPropertyBlock(_mpb);
            Body.localScale = BodyBaseScale * (1f + Mathf.Clamp01(PunchT) * PunchAmt);
            if (ManaFill != null && ManaFillBaseH > 0f)
            {
                var s = ManaFill.localScale;
                s.y = ManaFillBaseH * (1f + Mathf.Clamp01(ManaPulseT) * ManaPulse);
                ManaFill.localScale = s;
            }
        }
        public float ManaPulse = 0.9f; // magnitude, set from BarsTune at spawn
    }

    /// <summary>One combat number, already scheduled: the Director has picked its release time and
    /// launch lane, ReplayPlayer only has to fire it. <see cref="Lateral"/> is a signed offset along
    /// CAMERA-right (resolved at spawn, so lanes separate on screen at any orbit yaw).</summary>
    private struct NumberSpawn
    {
        public Vector3 Anchor;   // world point over the unit, band already applied
        public float Lateral;    // lane offset along camera-right — replaces the old Random jitter
        public int Unit;         // owning unit id — seeds a stable per-unit screen-x offset
        public string Text;
        public Color Color;
        public float Scale, T, LifeMul;
    }

    /// <summary>Data-driven event → tell, built from TuningData.tells (JSON). No code per tell.
    /// Each event fires the MOST SPECIFIC matching tell (see Warband.Sim.TellMatch): a filterless
    /// tell is the fallback, a "cause: Burn" / "status: Taunt" tell overrides it for that signature.
    ///
    /// Two layers, per render-contract: the fold owns truth (positions/bars); tells only decorate.
    /// Handle() enqueues a PendingTell (motion + a deferred impact payload); Tick() runs one clock
    /// that plays lunges/tracers/bursts and lands each impact AT CONTACT, not at dispatch. An
    /// impact latch (_readyAt, keyed on the causality root) times a victim's flash to the swing that
    /// connects / the tracer that arrives. Director-stepped FX (no self-Update) means the same clock
    /// drives play mode and the frozen BuildPreview captures — see directed-tells.md.</summary>
    private sealed class FeedbackDirector
    {
        private readonly Dictionary<int, UnitView> _views;
        private readonly List<TellDef> _tells = new List<TellDef>();
        private readonly Func<NumberSpawn, FloatingNumber> _spawnNumber;
        private readonly Action<FloatingNumber> _recycleNumber;
        private readonly Func<int, PlaybackUnit> _unitById;
        private readonly Func<Hex, Vector3> _hexToWorld;
        private readonly Func<Tracer> _getTracer;
        private readonly Action<Tracer> _recycleTracer;
        private readonly Func<Burst> _getBurst;
        private readonly Action<Burst> _recycleBurst;
        private readonly Func<VfxDef, VfxInstance> _getVfx;
        private readonly Action<VfxInstance> _recycleVfx;
        // Ground pulse router — every FieldView covering a hex flares when a `pulseGround` tell
        // lands there. A delegate rather than a field-view list, so the Director keeps knowing
        // nothing about how the board draws its ground.
        private readonly Action<Hex> _pulseGround;
        // Camera-facing rotation for billboard elements — read through a delegate so an orbit yaw
        // change (FrameCamera recomputes it) reaches recipes without rebuilding the Director.
        private readonly Func<Quaternion> _billboard;
        // Story-feed router for `announce` tells — (caster unit id, resolved ability id) at the
        // START of the windup, so the line reads while the sigil is still turning under the caster.
        private readonly Action<int, string> _announce;
        private readonly ImpactTune _impact;
        private readonly NumberTune _numbers;
        private readonly FxTune _fx;
        private readonly Action<string> _playSfx;
        // Motion/windup times are authored at 10 ticks/s; compress on fast-forward so tells stay
        // crisp instead of smearing (flash/punch stay real-seconds, decayed in ReplayPlayer.Update).
        private readonly float _speedScale;

        private struct PendingTell
        {
            public TellDef Def;
            public BattleEvent Event;
            public int SideUid;                // unit the impact payload lands on
            public Vector3 SourcePos, TargetPos; // ground-level world endpoints, captured at handle
            public float StartAt, Windup, Motion; // director-clock start + scaled phase durations
            public bool MotionStarted, Fired;
            // KayKit combat clip fired at StartAt with Animator.speed fitted so the swing fills
            // exactly the windup+motion gap (render-contract §4: the renderer fits its swing
            // animation into the sim's gap). Null on primitives / non-origin tells.
            public string AnimState;
            public bool AnimStarted;
            // The source's resolved ability id, captured at Handle (where it is already looked up
            // for the byAbility match) so the announce line costs nothing extra at StartAt.
            public string Ability;
            // VFX recipes fired at StartAt (source + ground). Sustained holds the source instance so
            // the windup's end can close it — the cast sentence's "release" beat.
            public bool FxStarted;
            public VfxInstance Sustained;
        }

        /// <summary>A number waiting for its lane to clear. Everything but the anchor is decided at
        /// enqueue (it describes the hit); the anchor is read at RELEASE, so a number held for a
        /// beat still appears over the unit's CURRENT hex rather than where it stood when hit.</summary>
        private struct QueuedNumber
        {
            public int Unit;
            public Vector3 Fallback;  // anchor to use if the view is gone by release (death)
            public float Height;      // Vector3.up offset: head clearance + the tell's band
            public float ReleaseAt, Lateral;
            public string Text;
            public Color Color;
            public float Scale, T, LifeMul;
        }

        private readonly List<QueuedNumber> _numberQueue = new List<QueuedNumber>();
        // (unit, lane) → the clock time that lane is free again. This is the whole anti-overlap
        // mechanism on the time axis; ties resolve to the LOWEST lane index so a quiet unit always
        // reuses lane 0 and the eye learns where to look (unlike jitter, which moves every time).
        private readonly Dictionary<(int Unit, int Lane), float> _laneFreeAt = new Dictionary<(int, int), float>();

        private readonly List<PendingTell> _pending = new List<PendingTell>();
        // Units currently mid-arc. A lunge and an arc both write MotionOffset, so a swing landing
        // inside a leap's airtime would fight it frame-by-frame and read as a twitch. The arc is the
        // larger, more meaningful motion (a cross-board leap vs a 0.45-unit jab), so it wins outright.
        private readonly HashSet<int> _arcing = new HashSet<int>();
        private readonly List<Tracer> _activeTracers = new List<Tracer>();
        private readonly List<Burst> _activeBursts = new List<Burst>();
        private readonly List<VfxInstance> _activeVfx = new List<VfxInstance>();
        private readonly List<FloatingNumber> _activeNumbers = new List<FloatingNumber>();
        private readonly Dictionary<int, float> _readyAt = new Dictionary<int, float>();
        // Resolved ability identity per unit id — a chassis+traits fold that never changes inside a
        // fight, so it is computed once per unit rather than per event, and dropped on Reset.
        private readonly Dictionary<int, string> _abilityById = new Dictionary<int, string>();
        // Director-clock time each caster last named itself in the feed — the announce ration's
        // whole state (FxTune.announceCooldownSeconds). Dropped on Reset with everything else.
        private readonly Dictionary<int, float> _announcedAt = new Dictionary<int, float>();
        private float _clock;

        private const float FlightY = 0.8f;   // tracer/spark chest height
        private const float BurstY = 0.5f;    // death-poof body height
        private const float LungeAmp = 0.45f; // world units the source steps into its hit

        public FeedbackDirector(Dictionary<int, UnitView> views, TuningData data,
                                Func<NumberSpawn, FloatingNumber> spawnNumber,
                                Action<FloatingNumber> recycleNumber,
                                Func<int, PlaybackUnit> unitById, Func<Hex, Vector3> hexToWorld,
                                Func<Tracer> getTracer, Action<Tracer> recycleTracer,
                                Func<Burst> getBurst, Action<Burst> recycleBurst,
                                Func<VfxDef, VfxInstance> getVfx, Action<VfxInstance> recycleVfx,
                                Func<Quaternion> billboard, Action<Hex> pulseGround,
                                Action<int, string> announce,
                                float ticksPerSecond, Action<string> playSfx = null)
        {
            _views = views; _spawnNumber = spawnNumber; _recycleNumber = recycleNumber;
            _unitById = unitById; _hexToWorld = hexToWorld; _playSfx = playSfx;
            _getTracer = getTracer; _recycleTracer = recycleTracer;
            _getBurst = getBurst; _recycleBurst = recycleBurst;
            _getVfx = getVfx; _recycleVfx = recycleVfx; _billboard = billboard;
            _pulseGround = pulseGround; _announce = announce;
            _impact = data?.impact ?? new ImpactTune();
            _numbers = data?.numbers ?? new NumberTune();
            _fx = data?.fx ?? new FxTune();
            _speedScale = Mathf.Min(1f, 10f / Mathf.Max(0.01f, ticksPerSecond));
            if (data?.tells != null) _tells.AddRange(data.tells);
        }

        public void Handle(BattleEvent e, float delay = 0f)
        {
            // Ranged is a VIEW fact: the hex distance between the two endpoints at fold-dispatch time.
            // Null when either endpoint is absent from the fold — a ranged-filtered rule then can't
            // match, so a melee lunge and a projectile tracer never fall back to each other.
            var su = _unitById(e.Source);
            var tu = _unitById(e.Target);
            int? distance = (su != null && tu != null) ? Hex.Distance(su.Pos, tu.Pos) : (int?)null;

            // Ability identity is the same class of view context as chassis: resolved from the fold's
            // (ChassisId, Traits) by the shared content DLL, so the client and the sim can never
            // disagree about what "pyro.starfall" means. Null context never matches a byAbility rule.
            string srcAbility = AbilityOf(e.Source, su);

            TellDef best = null;
            int bestSpec = -1;
            foreach (var def in _tells)
            {
                if (!TellMatch.Matches(e, def.eventKind, def.CauseFilter, def.StatusFilter, def.FlavorFilter,
                                       def.RangedFilter, distance, def.ChassisFilter, su?.ChassisId,
                                       ability: def.AbilityFilter, sourceAbility: srcAbility)) continue;
                if (def.Specificity > bestSpec) { best = def; bestSpec = def.Specificity; }
            }
            if (best == null) return;

            int key = e.Root >= 0 ? e.Root : e.Source; // causality root: children read this latch
            float startAt = _clock + delay;            // beat stagger from the dispatcher
            if (_readyAt.TryGetValue(key, out var ready) && ready > startAt) startAt = ready;

            float windup = best.windupSeconds * _speedScale;
            float motion = best.motionSeconds * _speedScale;

            // A Leap is the one event whose endpoints are BOTH on the event: the fold has already
            // teleported the leaper by the time this runs, so its take-off is not recoverable from
            // view state. Target here is the unit it leapt AT, not where it landed — hence the
            // explicit hexes for both ends.
            Vector3 srcPos = e.Kind == EventKind.Leap
                ? _hexToWorld(new Hex(e.Aux2, e.Aux3))               // the hex it left
                : (su != null ? Where(e.Source, su) : Vector3.zero);
            Vector3 tgtPos = e.Kind == EventKind.AttackBlocked || e.Kind == EventKind.Leap
                ? _hexToWorld(new Hex(e.Amount, e.Aux))              // wall hex / landing hex
                : (tu != null ? Where(e.Target, tu) : srcPos);       // Death: victim's retained fold pos

            // Combat clip for the origin moment: per-weapon-class swing on Attack, spellcast on
            // Cast. Chassis → state; primitives (no ModelAnimator) simply never consume it.
            string animState = null;
            if (e.Kind == EventKind.Attack) animState = AttackAnimFor(su);
            else if (e.Kind == EventKind.Cast) animState = "Cast";

            _pending.Add(new PendingTell
            {
                Def = best, Event = e,
                SideUid = best.side == FeedbackSide.Source ? e.Source : e.Target,
                SourcePos = srcPos, TargetPos = tgtPos,
                StartAt = startAt, Windup = windup, Motion = motion,
                AnimState = animState, Ability = srcAbility,
            });

            // Latch write — ORIGIN tells only. A swing/cast stamps its contact time so the victim's
            // flash lands when it connects. Consumers (DamageDealt/Heal/Status/Death) READ but never
            // write, so a 3-victim cast fans out simultaneously instead of stagger-cascading.
            if (best.defer && (e.Kind == EventKind.Attack || e.Kind == EventKind.Cast))
                _readyAt[key] = startAt + windup + ContactOffset(best, motion);

            // Decorative facing: turn the actor toward what it strikes/targets when a tell fires. Cast
            // has Target=-1 (tgtPos falls back to srcPos) → zero-length, skipped. Yaw eases in Update;
            // the fold still owns position, so this never fights the lunge (offset on Root, yaw on Body).
            if ((e.Kind == EventKind.Attack || e.Kind == EventKind.Cast || e.Kind == EventKind.Leap)
                && _views.TryGetValue(e.Source, out var srcView))
            {
                Vector3 d = tgtPos - srcPos;
                if (d.sqrMagnitude > 1e-6f) srcView.TargetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

                // An Arc owns the body outright for its whole life, windup included. Park Smoothed on
                // the landing hex NOW so MotionOffset is the only thing moving it — otherwise the
                // snap-lerp would be racing toward the destination underneath the arc and the two
                // would compound into an overshoot.
                if (best.motion == MotionKind.Arc) { srcView.Smoothed = srcView.Target; _arcing.Add(e.Source); }
            }
        }

        /// <summary>Where a unit IS on screen at this instant — its view's fold-driven target, which
        /// is a point ALONG a committed step while the unit walks. Tells anchor here rather than at
        /// the hex centre, so a tracer leaves the archer's body instead of the tile it is walking off
        /// and a death burst pops on the corpse. Falls back to the hex when the view is gone.</summary>
        private Vector3 Where(int unitId, PlaybackUnit u) =>
            _views.TryGetValue(unitId, out var v) ? v.Target : _hexToWorld(u.Pos);

        /// <summary>The unit's resolved ability id (last SignatureOverride trait, else the chassis),
        /// memoized: a unit's kit can't change mid-fight, so this folds once per unit per fight.</summary>
        private string AbilityOf(int unitId, PlaybackUnit u)
        {
            if (u == null) return null;
            if (_abilityById.TryGetValue(unitId, out var a)) return a;
            a = Warband.Content.AbilityIdentity.Resolve(u.ChassisId, u.Traits);
            _abilityById[unitId] = a;
            return a;
        }

        private static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");
        // Animator state → KayKit clip (for length lookup when fitting the swing into the gap).
        private static readonly Dictionary<string, string> StateClipName = new Dictionary<string, string>
        {
            ["Attack1H"] = "Melee_1H_Attack_Slice_Diagonal",
            ["Attack2H"] = "Melee_2H_Attack_Chop",
            ["AttackDual"] = "Melee_Dualwield_Attack_Stab",
            ["AttackBow"] = "Ranged_Bow_Release",
            ["Cast"] = "Ranged_Magic_Spellcasting",
        };
        private static Dictionary<string, float> _clipLen;

        private static float ClipLength(Animator a, string state)
        {
            if (_clipLen == null)
            {
                _clipLen = new Dictionary<string, float>();
                var rc = a.runtimeAnimatorController;
                if (rc != null) foreach (var c in rc.animationClips) _clipLen[c.name] = c.length;
            }
            return StateClipName.TryGetValue(state, out var cn) && _clipLen.TryGetValue(cn, out var l) ? l : 0f;
        }

        /// <summary>Per-weapon-class swing state for an auto attack. Pyromancer/Cleric autos are
        /// staff work — the spellcast clip reads truer than a sword swing.</summary>
        private static string AttackAnimFor(PlaybackUnit su)
        {
            switch ((su?.ChassisId ?? "").ToLowerInvariant())
            {
                case "berserker": return "Attack2H";
                case "shade": return "AttackDual";
                case "sharpshot": return "AttackBow";
                case "pyromancer": case "cleric": return "Cast";
                case "bulwark": case "phalanx": case "banneret": return "Attack1H";
                default: return null;
            }
        }

        /// <summary>Contact time RELATIVE to the end of windup: lunge connects ~55% through the
        /// out-and-back, a tracer's impact is its arrival, None/Burst land immediately.</summary>
        private static float ContactOffset(TellDef d, float motion)
        {
            switch (d.motion)
            {
                case MotionKind.Lunge: return 0.55f * motion;
                case MotionKind.Tracer: return motion;
                case MotionKind.Arc: return motion;   // a leap "connects" when it touches down
                default: return 0f; // None, Burst
            }
        }

        public void Tick(float dt)
        {
            _clock += dt;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var pt = _pending[i];
                if (_clock < pt.StartAt) continue;
                float local = _clock - pt.StartAt;

                // Beat 1 of the cast sentence (combat-spectacle §2): the sigil lights, the riser
                // starts, and the feed names the cast — all at StartAt, all one frame, so the
                // windup the player then watches has already been explained.
                if (!pt.FxStarted)
                {
                    pt.FxStarted = true;
                    pt.Sustained = StartSourceVfx(pt);
                    StartGroundVfx(pt);
                    if (!string.IsNullOrEmpty(pt.Def.castSound)) _playSfx?.Invoke(pt.Def.castSound);
                    if (pt.Def.announce) TryAnnounce(in pt);
                }

                if (!pt.AnimStarted)
                {
                    pt.AnimStarted = true;
                    if (pt.AnimState != null && _views.TryGetValue(pt.Event.Source, out var av)
                        && av.ModelAnimator != null && av.ModelAnimator.runtimeAnimatorController != null
                        && av.Root.gameObject.activeSelf)
                    {
                        // Fit the clip into the sim's gap (render-contract §4): a Hasted unit's
                        // denser events simply play faster swings.
                        float span = Mathf.Max(0.12f, pt.Windup + pt.Motion + 0.15f);
                        float len = ClipLength(av.ModelAnimator, pt.AnimState);
                        if (len > 0f)
                            av.ModelAnimator.SetFloat(ActionSpeedHash, Mathf.Clamp(len / span, 0.8f, 3.5f));
                        av.ModelAnimator.CrossFadeInFixedTime(pt.AnimState, 0.05f);
                    }
                }

                if (!pt.MotionStarted && local >= pt.Windup)
                {
                    pt.MotionStarted = true;
                    // The windup is over: a sustained source recipe (the cast aura) stops here and
                    // runs out its fade, which IS the release beat of the cast sentence.
                    if (pt.Sustained != null) { pt.Sustained.EndSustain(); pt.Sustained = null; }
                    SpawnMotion(pt);
                }

                if (pt.Def.motion == MotionKind.Lunge)
                    DriveLunge(pt, local);
                else if (pt.Def.motion == MotionKind.Arc)
                    DriveArc(pt, local);

                float contact = pt.Windup + ContactOffset(pt.Def, pt.Motion);
                if (!pt.Fired && local >= contact)
                {
                    pt.Fired = true;
                    ApplyImpact(in pt);
                }

                if (pt.Fired && local >= pt.Windup + pt.Motion)
                {
                    if ((pt.Def.motion == MotionKind.Lunge || pt.Def.motion == MotionKind.Arc)
                        && _views.TryGetValue(pt.Event.Source, out var sv))
                        sv.MotionOffset = Vector3.zero; // land exactly back on the hex
                    if (pt.Def.motion == MotionKind.Arc)  // touchdown puff, at the hex it landed on
                    {
                        _arcing.Remove(pt.Event.Source);
                        PlayBurst(pt.TargetPos + Vector3.up * 0.15f, pt.Def.motionColor,
                                  pt.Def.motionGlow, pt.Def.motionScale * 0.55f, 0.18f);
                    }
                    _pending.RemoveAt(i);
                    continue;
                }
                _pending[i] = pt;
            }

            ReleaseNumbers(); // after impacts, so a number booked this step can still fire this step

            for (int i = _activeTracers.Count - 1; i >= 0; i--)
            {
                var tr = _activeTracers[i];
                if (!tr.Step(dt))
                {
                    // Every tracer arrival pops a small same-color spark — impact spark on hits,
                    // fizzle on blocked shots (one executor rule).
                    PlayBurst(tr.End, tr.Color, tr.Glow, 0.4f * tr.Scale, 0.12f);
                    _activeTracers.RemoveAt(i);
                }
            }
            for (int i = _activeBursts.Count - 1; i >= 0; i--)
                if (!_activeBursts[i].Step(dt)) _activeBursts.RemoveAt(i);
            for (int i = _activeVfx.Count - 1; i >= 0; i--)
                if (!_activeVfx[i].Step(dt)) _activeVfx.RemoveAt(i);
            for (int i = _activeNumbers.Count - 1; i >= 0; i--)
                if (!_activeNumbers[i].Step(dt)) _activeNumbers.RemoveAt(i);
        }

        /// <summary>Name this cast in the feed — unless its caster already did, inside the ration
        /// window. The FIRST cast is the news; the fourth Great Chorus in six seconds is wallpaper,
        /// and there are only four feed slots for it to take from the kills. Per CASTER rather than
        /// per ability, which is the same thing today (a unit has one signature) and stays right if
        /// a unit ever gains a second one. Window in FxTune.announceCooldownSeconds; 0 = no ration.</summary>
        private void TryAnnounce(in PendingTell pt)
        {
            if (_announce == null) return;
            int uid = pt.Event.Source;
            float window = Mathf.Max(0f, _fx.announceCooldownSeconds);
            if (window > 0f && _announcedAt.TryGetValue(uid, out var last) && _clock - last < window) return;
            _announcedAt[uid] = _clock;
            _announce(uid, pt.Ability);
        }

        /// <summary>Particle seed for one recipe firing: a pure function of (tick, side unit, slot),
        /// never of the run. Same events → same pixels, which is what lets the contact sheet be
        /// binary-diffed as a determinism proof.</summary>
        private static uint Seed(int tick, int uid, int slot) =>
            unchecked((uint)(tick * 397) ^ (uint)(uid * 31) ^ (uint)slot);

        /// <summary>Play a recipe id, tinted by the tell (motionColor/motionGlow/motionScale reach
        /// every recipe, so the F1 loop retunes VFX with no recompile). Null when the id isn't
        /// authored yet — the caller then keeps whatever primitive it would have drawn.</summary>
        private VfxInstance PlayVfx(string id, Vector3 pos, Vector3 dir, TellDef def, uint seed, Transform follow)
        {
            var recipe = VfxLibrary.Get(id);
            if (recipe == null || _getVfx == null) return null;
            var fx = _getVfx(recipe);
            if (fx == null) return null;
            fx.Play(pos, dir, def.motionColor, def.motionGlow, def.motionScale, seed,
                    _billboard != null ? _billboard() : Quaternion.identity, follow, _recycleVfx);
            _activeVfx.Add(fx);
            return fx;
        }

        /// <summary>The tell's own recipe at the SOURCE, at StartAt. FollowUnit elements anchor to the
        /// source view's Root, so a cast aura rides a caster that is still walking. Returns the
        /// instance only when it is Sustained — that is the one the caller has to close.</summary>
        private VfxInstance StartSourceVfx(in PendingTell pt)
        {
            if (string.IsNullOrEmpty(pt.Def.vfx)) return null;
            _views.TryGetValue(pt.Event.Source, out var sv);
            var fx = PlayVfx(pt.Def.vfx, pt.SourcePos, pt.TargetPos - pt.SourcePos, pt.Def,
                             Seed(pt.Event.Tick, pt.SideUid, 0), sv != null ? sv.Root : null);
            return fx != null && fx.IsSustained ? fx : null;
        }

        /// <summary>Hex-anchored recipe under the tell's side unit (field-hex anchoring arrives with
        /// FieldView in P2). Snapped to the hex CENTRE, not the body, so a decal lands on the grid.</summary>
        private void StartGroundVfx(in PendingTell pt)
        {
            if (string.IsNullOrEmpty(pt.Def.groundVfx)) return;
            var u = _unitById(pt.SideUid);
            if (u == null) return;
            PlayVfx(pt.Def.groundVfx, _hexToWorld(u.Pos), pt.TargetPos - pt.SourcePos, pt.Def,
                    Seed(pt.Event.Tick, pt.SideUid, 3), null);
        }

        private void SpawnMotion(PendingTell pt)
        {
            switch (pt.Def.motion)
            {
                case MotionKind.Tracer:
                    Vector3 from = pt.SourcePos + Vector3.up * FlightY;
                    Vector3 to = pt.TargetPos + Vector3.up * FlightY;
                    // A projectile recipe REPLACES the cube tracer's visual over the same window, so
                    // ContactOffset (and every latch that reads it) is untouched by the swap. The
                    // recipe then owns its own arrival — it does not inherit the cube's auto spark.
                    var proj = string.IsNullOrEmpty(pt.Def.projectileVfx) ? null : VfxLibrary.Get(pt.Def.projectileVfx);
                    if (proj != null && _getVfx != null)
                    {
                        var fx = _getVfx(proj);
                        if (fx != null)
                        {
                            fx.PlayProjectile(from, to, pt.Motion, pt.Def.motionColor, pt.Def.motionGlow,
                                              pt.Def.motionScale, Seed(pt.Event.Tick, pt.SideUid, 1),
                                              _billboard != null ? _billboard() : Quaternion.identity, _recycleVfx);
                            _activeVfx.Add(fx);
                            break;
                        }
                    }
                    PlayTracer(from, to, pt.Def.motionColor, pt.Def.motionGlow, pt.Def.motionScale, pt.Motion);
                    break;
                case MotionKind.Burst:
                    PlayBurst(pt.TargetPos + Vector3.up * BurstY,
                              pt.Def.motionColor, pt.Def.motionGlow, pt.Def.motionScale * 0.5f, pt.Motion);
                    break;
                case MotionKind.Arc:  // kick-off puff at the hex being vacated — sells the push-off
                    PlayBurst(pt.SourcePos + Vector3.up * 0.15f, pt.Def.motionColor,
                              pt.Def.motionGlow, pt.Def.motionScale * 0.4f, 0.16f);
                    break;
            }
        }

        private void DriveLunge(PendingTell pt, float local)
        {
            if (_arcing.Contains(pt.Event.Source)) return;   // a leap in flight owns the body
            if (!_views.TryGetValue(pt.Event.Source, out var sv)) return;
            float m = pt.Motion > 0f ? Mathf.Clamp01((local - pt.Windup) / pt.Motion) : 1f;
            float amp = m < 0.55f ? m / 0.55f : 1f - (m - 0.55f) / 0.45f; // out to 55%, then back
            Vector3 dir = pt.TargetPos - pt.SourcePos;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
            sv.MotionOffset = dir * (LungeAmp * Mathf.Clamp01(amp));
        }

        /// <summary>
        /// The leap hop. A leap is a genuine sim TELEPORT — ADR 0018 keeps it instant on purpose, so
        /// the fold has already put this unit on its landing hex and every rule already treats it as
        /// there. This is therefore pure decoration inside the tick, in the same class as a lunge or
        /// a tracer: the body covers the distance the sim skipped.
        ///
        /// The offset is measured BACKWARD from the landing hex rather than forward from the take-off,
        /// which is what makes the touchdown exact — it decays to zero at m=1 no matter what, so the
        /// unit can never be left parked off its hex if a frame is long or the tell is cut short.
        /// During the windup m pins to 0, so the body sits at the take-off and crouches there instead
        /// of anticipating at the destination it has not visibly left yet.
        /// </summary>
        private void DriveArc(PendingTell pt, float local)
        {
            if (!_views.TryGetValue(pt.Event.Source, out var sv)) return;
            float m = pt.Motion > 0f ? Mathf.Clamp01((local - pt.Windup) / pt.Motion) : 1f;
            Vector3 back = pt.SourcePos - pt.TargetPos;
            // Height scales with the jump: a hop to the next hex should not launch like a
            // cross-board dive onto the backline, which is the leap the Shade actually makes.
            float height = pt.Def.motionScale * Mathf.Clamp(back.magnitude * 0.25f, 0.4f, 2.5f);
            sv.MotionOffset = back * (1f - m) + Vector3.up * (Mathf.Sin(m * Mathf.PI) * height);
        }

        private void PlayTracer(Vector3 start, Vector3 end, Color color, float glow, float scale, float seconds)
        {
            var tr = _getTracer();
            tr.Play(start, end, color, glow, scale, seconds, _recycleTracer);
            _activeTracers.Add(tr);
        }

        private void PlayBurst(Vector3 pos, Color color, float glow, float size, float seconds)
        {
            var b = _getBurst();
            b.Play(pos, color, glow, size, seconds, _recycleBurst);
            _activeBursts.Add(b);
        }

        /// <summary>The impact payload — flash/punch/number on the tell's side unit, unchanged from
        /// the pre-motion behavior; motion-None tells with no latch fire it the same frame as before.</summary>
        private void ApplyImpact(in PendingTell pt)
        {
            var def = pt.Def;
            var e = pt.Event;
            int sideUid = pt.SideUid;
            if (!_views.TryGetValue(sideUid, out var v)) return;

            // How big this hit reads, 0..1 — the single knob every spectacle channel keys off.
            float t = _impact.Intensity(e.Amount);

            if (!string.IsNullOrEmpty(def.sound) || !string.IsNullOrEmpty(def.critSound))
                _playSfx?.Invoke(e.Crit && !string.IsNullOrEmpty(def.critSound) ? def.critSound : def.sound);
            if (def.flash) { v.FlashColor = e.Crit ? def.critFlashColor : def.flashColor; v.FlashT = 1f; v.FlashDur = def.flashSeconds; }
            if (def.punch) { v.PunchT = 1f; v.PunchDur = def.punchSeconds; v.PunchAmt = def.punchAmount * (1f + _impact.punchBoost * t); }
            // The icon pops at the moment the status LANDS, not when the fold gained it — same law
            // as the flash above, and the same reason the mana bar pulses on its threshold flip.
            // Aux carries the StatusKind (the fold's own decode).
            if (e.Kind == EventKind.StatusApplied) v.Icons?.Pop((StatusKind)e.Aux);
            if (def.number && Mathf.Abs(e.Amount) >= def.minAmount)
            {
                var col = e.Crit ? def.critNumberColor : def.numberColor;
                if (_impact.enabled) col = Color.Lerp(col, _impact.heavyTint, _impact.tintAmount * t);
                float mag = _impact.enabled ? Mathf.Lerp(_impact.minScale, _impact.maxScale, t) : 1f;
                EnqueueNumber(def, sideUid, v, Mathf.Abs(e.Amount).ToString(),
                              col, def.numberScale * mag * (e.Crit ? 1.4f : 1f), t);
            }

            // Contact recipe, at the victim's chest and aimed along the blow so directional sprays
            // throw away from the hit. Empty id leaves today's rendering (flash/punch/number, plus a
            // cube tracer's own arrival spark) exactly as it is.
            if (!string.IsNullOrEmpty(def.impactVfx))
                PlayVfx(def.impactVfx, v.Target + Vector3.up * FlightY, pt.TargetPos - pt.SourcePos,
                        def, Seed(e.Tick, sideUid, 2), null);

            // Flinch, gated on how hard the hit READ (ImpactTune t) so a DoT tick can't spasm the
            // body. The "Hit" state is built in P5; HasState keeps this a silent no-op until then.
            if (def.hitAnim && t >= def.hitAnimMinT && v.ModelAnimator != null
                && v.ModelAnimator.runtimeAnimatorController != null
                && v.Root.gameObject.activeSelf
                && v.ModelAnimator.HasState(0, HitStateHash))
                v.ModelAnimator.CrossFadeInFixedTime("Hit", 0.05f);

            // Ground pulse, at the hex the payload landed on — the field's floor flashes because
            // something HAPPENED on it. Read from the fold rather than the view, so a victim mid-step
            // still credits the hex the sim says it occupies (the field's own membership test).
            if (def.pulseGround && _pulseGround != null)
            {
                var su = _unitById(sideUid);
                if (su != null) _pulseGround(su.Pos);
            }
        }

        private static readonly int HitStateHash = Animator.StringToHash("Hit");

        /// <summary>Book a number into the first free launch lane on this unit. NOTHING is merged or
        /// dropped — every instance keeps its own number; the schedule only decides when and where it
        /// launches. Lane + time are chosen deterministically (no Random anywhere in the path), so a
        /// frozen BuildPreview renders identically every capture and RenderShots can regression-check
        /// crowding.</summary>
        private void EnqueueNumber(TellDef def, int unitId, UnitView v, string text, Color color, float scale, float t)
        {
            int lanes = Mathf.Max(1, _numbers.columns);
            float gap = Mathf.Max(0f, _numbers.releaseGap);

            int lane = 0;
            float release = float.MaxValue;
            for (int c = 0; c < lanes; c++)
            {
                float free = _laneFreeAt.TryGetValue((unitId, c), out var f) ? f : 0f;
                float r = Mathf.Max(_clock, free);
                if (r < release) { release = r; lane = c; }
            }

            // A number that waits too long stops reading as caused by its hit, so past maxHold we
            // fire anyway and accept the overlap — causality outranks tidiness. The lane's free-time
            // still only moves FORWARD, or a clamped burst would cascade every later number early.
            release = Mathf.Min(release, _clock + Mathf.Max(0f, _numbers.maxHold));
            float prevFree = _laneFreeAt.TryGetValue((unitId, lane), out var pf) ? pf : 0f;
            _laneFreeAt[(unitId, lane)] = Mathf.Max(prevFree, release + gap);

            float height = 1.9f + def.numberBand;
            _numberQueue.Add(new QueuedNumber
            {
                Unit = unitId, Fallback = v.Target + Vector3.up * height, Height = height,
                ReleaseAt = release,
                Lateral = (lane - (lanes - 1) * 0.5f) * _numbers.columnSpread,
                Text = text, Color = color, Scale = scale, T = t,
                LifeMul = Mathf.Max(0.01f, def.numberLife),
            });
        }

        /// <summary>Fire every number whose lane has come free. Anchors resolve here (not at enqueue)
        /// so a held number tracks a unit that moved; a unit whose view is gone falls back to the
        /// position captured at impact, which keeps a killing blow's number over the corpse's hex.</summary>
        private void ReleaseNumbers()
        {
            for (int i = _numberQueue.Count - 1; i >= 0; i--)
            {
                var q = _numberQueue[i];
                if (_clock < q.ReleaseAt) continue;
                _numberQueue.RemoveAt(i);
                var fn = _spawnNumber(new NumberSpawn
                {
                    Anchor = _views.TryGetValue(q.Unit, out var v) ? v.Target + Vector3.up * q.Height : q.Fallback,
                    Lateral = q.Lateral, Unit = q.Unit, Text = q.Text, Color = q.Color,
                    Scale = q.Scale, T = q.T, LifeMul = q.LifeMul,
                });
                if (fn != null) _activeNumbers.Add(fn);
            }
        }

        /// <summary>Clear the timeline + latches, zero every lunge offset, and recycle in-flight FX.
        /// Called on loop-wrap, ResetAnim, and before a tuning-driven Director swap so nothing leaks.</summary>
        public void Reset()
        {
            _clock = 0f;
            _pending.Clear();
            _arcing.Clear();     // a leap cut short by a loop-wrap must not veto the next fight's lunges
            _readyAt.Clear();
            _numberQueue.Clear();
            _laneFreeAt.Clear(); // stale lane reservations would hold the next fight's first numbers
            _abilityById.Clear(); // a scenario switch brings different units under the same ids
            _announcedAt.Clear(); // ditto the ration: a loop-wrap's first cast is news again
            foreach (var v in _views.Values) v.MotionOffset = Vector3.zero;
            foreach (var tr in _activeTracers) _recycleTracer(tr);
            _activeTracers.Clear();
            foreach (var b in _activeBursts) _recycleBurst(b);
            _activeBursts.Clear();
            // Stop() hands back the light budget; the pool delegate does the deactivate/push.
            foreach (var fx in _activeVfx) { if (fx != null) fx.Stop(); _recycleVfx(fx); }
            _activeVfx.Clear();
            foreach (var n in _activeNumbers) _recycleNumber(n);
            _activeNumbers.Clear();
        }
    }

    // ---- lifecycle -----------------------------------------------------------

    /// <summary>
    /// Play <see cref="replayFile"/> the moment the scene starts. OFF by default: the board is
    /// driven by whoever owns the game (the run shell, the scenario picker, a capture script), and
    /// a self-starting board meant a stray test fight looped behind every menu — kill feed, banners
    /// and damage numbers included. Turn it on only for a scene whose whole job is watching a
    /// fixture.
    /// </summary>
    public bool autoPlayOnStart;

    private void Start()
    {
        if (!Application.isPlaying) return;
        if (autoPlayOnStart) StartPlayback();
        else Idle();
    }

    /// <summary>
    /// Board present, nothing playing. Builds the empty grid so the scene looks deliberate rather
    /// than blank, but spawns no units and runs no clock.
    /// </summary>
    public void Idle()
    {
        _playing = false;
        _ending = false;
        _endHold = 0f;
        _clock = 0f;
        _fxCursor = 0;
        ClearRecent();
        ShowSnapshot(new List<PlaybackUnit>());
    }

    /// <summary>The play-mode entry: (re)load the replay, rebuild the board + overlay, and restart at
    /// tick 0. Shared by Start() and LoadScenario() so a scenario switch resets the Director, kill
    /// feed, and banner exactly the way a fresh Start would (Build recreates them under _generated).</summary>
    private void StartPlayback()
    {
        if (!Load()) return;
        Build();
        _clock = 0f; _fxCursor = 0; _playing = true; _holdSeconds = 0f;
        _ending = false; _endHold = 0f;
        ClearRecent();  // a fresh fight starts the Events tab clean (no ResetAnim on this path)
        ApplyFold(0);
    }

    /// <summary>Debug-menu scenario switch: point at a new replay (relative to StreamingAssets) and
    /// restart cleanly. In edit mode it just re-scrubs the frozen preview.</summary>
    public void LoadScenario(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        replayFile = relativePath;
        if (Application.isPlaying) StartPlayback();
        else BuildPreview(_lastPreviewTick);
    }

    /// <summary>
    /// Render a placement/encounter snapshot without running a combat clock. The snapshot is the
    /// same Warband.Sim view contract used by normal replays; this is presentation-only.
    /// </summary>
    public void ShowSnapshot(IEnumerable<PlaybackUnit> initial)
    {
        SetReplayData(initial, new List<BattleEvent>(), 0, autoplay: false);
    }

    /// <summary>
    /// Highlight one fielded unit while the Planning host owns interaction. This is presentation
    /// only: the Planning draft remains the source of selection and roster truth.
    /// </summary>
    public void SetPlanningSelection(int selectedUnitId)
    {
        foreach (var pair in _views)
            if (pair.Value.PlanningMarker != null)
                pair.Value.PlanningMarker.gameObject.SetActive(pair.Key == selectedUnitId);
    }

    /// <summary>Play a freshly resolved deterministic battle without serializing it to disk first.</summary>
    public void PlayBattle(BattleResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        loop = false;
        SetReplayData(result.InitialUnits, result.Events, result.EndTick, autoplay: true);
    }

    private void SetReplayData(IEnumerable<PlaybackUnit> initial, IEnumerable<BattleEvent> events,
                               int endTick, bool autoplay)
    {
        _initial = new List<PlaybackUnit>();
        foreach (var u in initial) _initial.Add(u.Clone());
        _events = new List<BattleEvent>(events);
        _endTick = endTick;
        PrepareLoadedData();
        Build();
        _clock = 0f; _fxCursor = 0; _playing = autoplay; _holdSeconds = 0f;
        _ending = false; _endHold = 0f;
        ClearRecent();
        ApplyFold(0);
        if (!autoplay) SnapViews();
    }

    private void Update()
    {
        if (!_playing || _fold == null) return;
        // Hit-stop: a blocking beat (Death, crit) freezes the PLAYHEAD for a few real ms while the
        // Director keeps stepping — decorative FX animate through the hold, sim time stands still.
        // Never Time.timeScale (render-polish law): that would couple juice to Unity's clock.
        if (_holdSeconds > 0f) _holdSeconds -= Time.deltaTime;
        else _clock += Time.deltaTime * ticksPerSecond;
        if (_clock >= _endTick)
        {
            // Reach the end → freeze the playhead and HOLD, showing the win banner + readout, instead
            // of wrapping instantly. Advance the fold to the last tick first so BeginEnding reads the
            // final survivors. When the hold elapses (loop on), wrap as before; loop off → banner stays.
            _clock = _endTick;
            ApplyFold(_endTick); DispatchUpTo(_endTick); // idempotent once consumed; flushes end-tick deaths
            if (!_ending) BeginEnding();
            _endHold += Time.deltaTime;
            if (loop && _endHold >= _data.story.endHoldSeconds)
            { _clock = 0f; _fold = PlaybackState.From(_initial); _fxCursor = 0; ResetAnim(); }
        }
        int tick = Mathf.FloorToInt(_clock);
        ApplyFold(tick, _clock);
        DispatchUpTo(tick);

        float dt = Time.deltaTime;
        StepFx(dt);   // the one FX clock — see StepFx
        TickFeed(dt); // age kill-feed lines toward their fade (play mode only; a frozen preview pins alpha)
        var mo = _data.motion;
        foreach (var v in _views.Values)
        {
            if (v.Root.gameObject.activeSelf)
            {
                // A walk is TAKEN, not chased. Target is already the exact point the sim's step
                // window puts this unit on; smoothing it would draw the body behind its own truth
                // and reintroduce the lag that made steps read as snap-then-wait. The lerp survives
                // only for discontinuities — a teleport, a leap, a freshly rebuilt view.
                v.Smoothed = v.Walking ? v.Target : Vector3.Lerp(v.Smoothed, v.Target, mo.snapSpeed * dt);
                v.Root.position = v.Smoothed + v.MotionOffset + Vector3.up * Footfall(v, mo);
                // Decorative body yaw — eases toward TargetYaw on Body.localRotation, independent of the
                // Root.position lunge, so the two compose. LeanDeg tilts it forward (Shade); a walking
                // unit leans a little further into its stride.
                float lean = v.LeanDeg + (v.Walking ? mo.leanAmount * Mathf.Rad2Deg * 0.1f : 0f);
                var rot = Quaternion.Euler(lean, v.TargetYaw, 0f);
                v.Body.localRotation = Quaternion.Slerp(v.Body.localRotation, rot, Mathf.Clamp01(mo.turnSpeed * dt));
                StyleNameplate(v);
                v.Icons.FaceCamera(_numberFace);
            }
            if (v.FlashT > 0f) v.FlashT -= dt / v.FlashDur;
            if (v.PunchT > 0f) v.PunchT -= dt / v.PunchDur;
            if (v.ManaPulseT > 0f) v.ManaPulseT -= dt / 0.35f;
            if (v.ModelAnimator != null && v.Root.gameObject.activeSelf)
                v.ModelAnimator.SetBool(WalkingHash, v.Walking);
            v.ApplyVisual();
        }
        LayoutStory(false);
    }

    /// <summary>
    /// THE FX clock. Every decorative system advances here and nowhere else: pending tells, lunges,
    /// tracers, bursts, VFX recipes — and, as they land, field views (P2) and death sequences (P5).
    /// Both call sites route through it (Update's real dt, BuildLoadedPreview's fixed 0.01 s loop),
    /// which is what makes a frozen contact sheet reproduce a live frame BY CONSTRUCTION rather than
    /// by two code paths agreeing. Nothing below this line may read Time.deltaTime.
    /// </summary>
    private void StepFx(float dt)
    {
        _director?.Tick(dt);
        // Status rows: the countdown rings drain and any apply-pop runs out on this clock, which is
        // why two frozen captures of the SAME tick at different previewAdvanceSeconds show the
        // clocks at different fills instead of one static ring.
        foreach (var v in _views.Values) v.Icons?.Step(dt);
        if (_fieldViews.Count == 0) return;
        _expiredFields.Clear();
        foreach (var kv in _fieldViews) if (!kv.Value.Step(dt)) _expiredFields.Add(kv.Key);
        foreach (var id in _expiredFields) { _fieldViews[id].Destroy(); _fieldViews.Remove(id); }
    }

    // Scratch for StepFx's finished-view sweep — a dictionary can't be mutated while enumerated.
    private readonly List<int> _expiredFields = new List<int>();

    /// <summary>Drop every field view outright (loop-wrap, board-geometry rebuild). A wrapped replay
    /// must spawn its fields fresh, or the second loop shows glyphs that never traced in.</summary>
    private void ClearFieldViews()
    {
        foreach (var kv in _fieldViews) kv.Value.Destroy();
        _fieldViews.Clear();
    }

    /// <summary>Flare every field covering a hex. The Director calls this at the impact of any tell
    /// authored with `pulseGround`, which is what makes a field's loudest frame an EVENT that
    /// happened on those hexes rather than an animation the field plays to itself.</summary>
    public void PulseFieldsAt(Hex hex)
    {
        foreach (var kv in _fieldViews) if (kv.Value.Covers(hex)) kv.Value.Pulse();
    }

    /// <summary>Editor scrub: freeze the fold at <paramref name="tick"/>, snap the view, and replay
    /// the last couple ticks' tells so a static capture reveals flashes/punches/numbers.</summary>
    public void BuildPreview(int tick)
    {
        if (!Load()) return;
        BuildLoadedPreview(tick);
    }

    /// <summary>
    /// Freeze and render the currently loaded in-memory battle at a tick. Unlike BuildPreview this
    /// does not replace a live skirmish with replayFile, so editor playtests can inspect exact moments.
    /// </summary>
    public void BuildLoadedPreview(int tick)
    {
        _playing = false;
        _lastPreviewTick = tick;
        _fold = PlaybackState.From(_initial);
        _director = MakeDirector();
        Build();
        // Reset BEFORE the fold, not after: ResetAnim drops every decorative object the last scrub
        // left behind (field views included), and ApplyFold is what rebuilds them for THIS tick.
        ResetAnim();
        ApplyFold(tick);
        foreach (var v in _views.Values)
        {
            v.Smoothed = v.Target; v.Root.position = v.Target;
            // Pose the mini for a frozen capture — the Animator doesn't tick in edit mode, so
            // without this every model renders in bind pose. Manual Update() samples the graph.
            // Dead units' roots are inactive: Animator.Update would warn and do nothing.
            if (v.ModelAnimator != null && v.Root.gameObject.activeSelf)
            {
                v.ModelAnimator.SetBool(WalkingHash, v.Walking);
                v.ModelAnimator.Update(0f);
                v.ModelAnimator.Update(0.4f);
            }
        }
        foreach (var e in _events)
            if (e.Tick > tick - 2 && e.Tick <= tick)
            {
                _director.Handle(e);
                RecordEvent(e); // populate the Events tab for edit-mode scrubs too (ResetAnim cleared it)
                if (e.Kind == EventKind.Death) PushKill(e); // feed lines in the window capture too
            }
        // Fast-forward the one FX clock so a frozen capture shows tracers mid-flight and landed
        // flashes — everything is Director-stepped, so this works with no self-Update.
        int steps = Mathf.Max(1, Mathf.RoundToInt(previewAdvanceSeconds / 0.01f));
        for (int i = 0; i < steps; i++) StepFx(0.01f);
        foreach (var v in _views.Values)
        {
            if (v.Root.gameObject.activeSelf)
            {
                v.Root.position = v.Smoothed + v.MotionOffset + Vector3.up * Footfall(v, _data.motion);
                v.Body.localRotation = Quaternion.Euler(v.LeanDeg, v.TargetYaw, 0f); // SNAP (no slerp) so a frozen capture shows facing
                StyleNameplate(v);
                v.Icons.FaceCamera(_numberFace);
            }
            v.ApplyVisual();
        }
        if (tick >= _endTick) BeginEnding();   // a capture at/after the end shows the win banner
        LayoutStory(true);                     // frozen alpha; positions feed + banner for the shot
    }

    /// <summary>Hot-reload entry: re-read the (already-reloaded) tuning and rebuild so it shows.</summary>
    public void ReapplyTuning()
    {
        if (_tuning != null) _data = _tuning.data;
        if (Application.isPlaying)
        {
            if (_data != null && _data.playback != null) ticksPerSecond = _data.playback.ticksPerSecond;
            _director?.Reset();   // recycle in-flight FX + clear offsets before swapping the Director
            _director = MakeDirector();
            ApplyBoardTune();     // hexSize/tileScale live: tiles rebuild, units re-place via ApplyFold
            FrameCamera(); ApplyPost();
        }
        else BuildPreview(_lastPreviewTick);
    }

    /// <summary>Live board-geometry reload: rebuild the tile mesh set at the tuned hexSize/tileScale.
    /// Units re-place themselves on the next ApplyFold (HexToWorld reads the field live) and field
    /// tiles re-position on the next SyncFields pass — only the static tiles need rebuilding.</summary>
    private void ApplyBoardTune()
    {
        var bd = _data != null ? _data.board : null;
        if (bd == null || _generated == null) return;
        bool changed = !Mathf.Approximately(hexSize, bd.hexSize);
        hexSize = bd.hexSize;
        var tiles = _generated.Find("Tiles");
        var slab = _generated.Find("BoardBase");
        if (changed || tiles == null)
        {
            if (tiles != null) DestroyImmediate(tiles.gameObject);
            if (slab != null) DestroyImmediate(slab.gameObject);
            _hexMesh = null; // vertices bake hexSize — force regeneration
            BuildBoard();
            // Field views bake hexSize into their overlay scale — drop them; SyncFields recreates
            // them (replaying the spawn-in, which is the honest read of a rebuilt board).
            ClearFieldViews();
            _boardCenter = (HexToWorld(new Hex(0, 0))
                          + HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1))) * 0.5f;
            RecomputeStoryAnchors();
            if (Application.isPlaying && _fold != null) ApplyFold(Mathf.FloorToInt(_clock), _clock);
        }
    }

    public void ClearGenerated()
    {
        _views.Clear(); _fieldViews.Clear(); _numberPool.Clear();
        _tracerPool.Clear(); _burstPool.Clear(); _vfxPools.Clear();
        // The VFX light budget is a static live count; destroying the pools underneath it would
        // otherwise leak the lights of a board that no longer exists.
        VfxInstance.ResetLightBudget();
        if (_generated != null) DestroyImmediate(_generated.gameObject);
        _generated = null;
    }

    // ---- build ---------------------------------------------------------------

    private bool Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, replayFile);
        if (!File.Exists(path)) { Debug.LogError($"[ReplayPlayer] replay not found: {path}"); return false; }
        using (var fs = File.OpenRead(path))
            (_initial, _events) = Replay.Read(fs);
        _endTick = _events.Count > 0 ? _events[_events.Count - 1].Tick : 0;
        PrepareLoadedData();
        return true;
    }

    private void PrepareLoadedData()
    {
        _fold = PlaybackState.From(_initial);
        _fieldBornAt.Clear();
        foreach (var e in _events)
            if (e.Kind == EventKind.FieldCreated) _fieldBornAt[e.Target] = e.Tick;

        _tuning = FindFirstObjectByType<TuningConfig>();
        if (_tuning != null) { _tuning.LoadFromJson(); _data = _tuning.data; }
        // Battle speed is tuning-owned (persists via F1 save). Apply BEFORE MakeDirector — the
        // Director snapshots its motion speed-scale from ticksPerSecond at construction.
        if (_data != null && _data.playback != null) ticksPerSecond = _data.playback.ticksPerSecond;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _director = MakeDirector();
    }

    private void Build()
    {
        ClearGenerated();
        _generated = new GameObject("~generated").transform;
        _generated.SetParent(transform, false);
        _generated.gameObject.hideFlags = HideFlags.DontSave;

        if (_data != null && _data.board != null) hexSize = _data.board.hexSize;
        _hexMesh = null; // vertices bake hexSize — regenerate per build so a tuned size shows
        BuildBoard();
        foreach (var u in _initial) SpawnView(u);
        // The first rendered frame after a rebuild must already be PLACED and POSED. The shell
        // rebuilds this board inside UI events (every planning click re-snapshots; lock-in swaps
        // to the fight), and without this the frame between Build and the next Update draws every
        // fresh model stacked at the origin in bind pose — Jake's "they all T-pose when I click"
        // and "lock-in teleports everyone to one square".
        foreach (var v in _views.Values)
        {
            v.Root.position = v.Target;
            v.Body.localRotation = Quaternion.Euler(v.LeanDeg, v.TargetYaw, 0f);
            if (v.ModelAnimator != null && v.Root.gameObject.activeSelf)
            {
                v.ModelAnimator.SetBool(WalkingHash, false);
                v.ModelAnimator.Update(0f); // bind + evaluate Idle so no bind-pose frame renders
            }
        }
        BuildStory();
        FrameCamera();
        ApplyPost();

        // Board centre in world space — the anchor for outward number drift. Fixed per board, so
        // cache it once per build rather than reprojecting every number spawn.
        _boardCenter = (HexToWorld(new Hex(0, 0))
                      + HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1))) * 0.5f;
    }

    private Vector3 _boardCenter;
    private static readonly int WalkingHash = Animator.StringToHash("Walking");

    private void ResetAnim()
    {
        foreach (var v in _views.Values) { v.FlashT = 0f; v.PunchT = 0f; v.Icons?.Reset(); }
        _holdSeconds = 0f;  // a hit-stop must not survive a loop-wrap or scenario switch
        _director?.Reset(); // clears timeline/latches, zeros MotionOffsets, recycles in-flight FX
        ClearFieldViews();  // ditto the fields — the next fight traces its own glyphs in
        ClearStory();       // clears kill-feed lines + hides the banner, unlatches _ending
        ClearRecent();      // Events-tab buffer too (loop-wrap + edit-mode scrub route through here)
    }

    /// <summary>Vertical footfall for a walking unit: |sin| across the step, so it hits zero at both
    /// ends and a unit that arrives settles flat on its hex. Purely decorative — it rides on top of
    /// the fold's position and never feeds back into it, which is what keeps arrival exact.</summary>
    private static float Footfall(UnitView v, MotionTune mo)
    {
        if (!v.Walking || mo.bobHeight <= 0f || mo.bobSteps <= 0f) return 0f;
        return Mathf.Abs(Mathf.Sin(v.WalkPhase * Mathf.PI * mo.bobSteps)) * mo.bobHeight;
    }

    private void SnapViews()
    {
        foreach (var v in _views.Values)
        {
            v.Smoothed = v.Target;
            v.Root.position = v.Target + Vector3.up * Footfall(v, _data.motion);
            v.Body.localRotation = Quaternion.Euler(v.LeanDeg, v.TargetYaw, 0f);
            StyleNameplate(v);
            v.Icons.FaceCamera(_numberFace);
            v.ApplyVisual();
        }
    }

    // Hit-stop remaining (real seconds) + per-beat causal-chain ordering scratch. The playhead
    // holds while _holdSeconds drains; the Director keeps ticking so FX animate through it.
    private float _holdSeconds;
    private readonly Dictionary<int, int> _beatRootOrder = new Dictionary<int, int>();

    private void DispatchUpTo(int tick)
    {
        var bt = _data != null ? _data.beats : null;
        bool beats = bt != null && bt.enabled;
        // Same compression law as tell motion: authored at 10 t/s, tighter on fast-forward.
        float scale = Mathf.Min(1f, 10f / Mathf.Max(0.01f, ticksPerSecond));

        while (_fxCursor < _events.Count && _events[_fxCursor].Tick <= tick)
        {
            // One BEAT = every event of a single tick. Distinct causal chains (Root groups) get a
            // small stagger so simultaneous chains don't visually cancel; events INSIDE a chain keep
            // the same offset — their internal ordering already comes from the Director's impact
            // latch, and staggering them individually would fight it.
            int beatTick = _events[_fxCursor].Tick;
            _beatRootOrder.Clear();
            while (_fxCursor < _events.Count && _events[_fxCursor].Tick == beatTick)
            {
                var e = _events[_fxCursor++];
                float delay = 0f;
                if (beats && bt.stagger > 0f)
                {
                    int chain = e.Root >= 0 ? e.Root : e.Source;
                    if (!_beatRootOrder.TryGetValue(chain, out int order))
                    { order = _beatRootOrder.Count; _beatRootOrder[chain] = order; }
                    delay = order * bt.stagger * scale;
                }
                _director.Handle(e, delay);
                RecordEvent(e); // ring buffer for the debug Events tab
                // Kill feed hooks Death at dispatch level — unconditional, independent of whether a Death
                // tell is authored in tuning.json. Fold is already advanced this frame, so names resolve.
                if (e.Kind == EventKind.Death) PushKill(e);
                // Blocking events hold the playhead. Play mode only — a frozen BuildPreview has no
                // playhead to hold, and a stale hold must not leak into the next scrub.
                if (beats && Application.isPlaying)
                {
                    if (e.Kind == EventKind.Death) _holdSeconds = Mathf.Max(_holdSeconds, bt.deathHold * scale);
                    else if (e.Crit) _holdSeconds = Mathf.Max(_holdSeconds, bt.critHold * scale);
                }
            }
        }
    }

    /// <summary>Append a dispatched event to the Events-tab ring buffer (tail = newest, capped),
    /// bumping EventSeq so the debug menu can cheaply tell what's new. See the field comment.</summary>
    private void RecordEvent(BattleEvent e)
    {
        _recent.Add(e);
        if (_recent.Count > RecentCap) _recent.RemoveAt(0);
        EventSeq++;
    }

    /// <summary>Drop the ring buffer and reset the sequence, so a stale fight's lines can't survive a
    /// scenario switch / loop-wrap. The menu detects the reset via EventSeq dropping to 0.</summary>
    private void ClearRecent()
    {
        _recent.Clear();
        EventSeq = 0;
    }

    /// <summary>Fold name for a unit id, or null when it isn't in the fold — the Events tab resolves
    /// names through this (and supplies its own "storm"/"#id" fallbacks).</summary>
    public string UnitName(int id) => _fold?.ById(id)?.Name;

    /// <summary>Fire a scheduled number. The Director already picked the lane; this resolves lateral
    /// placement against CAMERA-right so numbers separate on screen at any orbit yaw, then adds the
    /// two cross-unit pushes (outward-from-centre + stable per-unit) to launch position AND velocity
    /// so trajectories DIVERGE over life (world-up is screen-up here, so parallel columns would
    /// otherwise stay welded). Size/life come from tuning each spawn so live edits reach pooled
    /// numbers too.</summary>
    private FloatingNumber SpawnNumber(NumberSpawn s)
    {
        var fn = _numberPool.Count > 0 ? _numberPool.Pop() : FloatingNumber.Create(_generated, _font);
        var n = _data.numbers;
        Vector3 right = _numberFace * Vector3.right;

        // Screen-x of the unit relative to board centre, normalized by board half-span so the push is
        // scale-free: -1 = far screen-left of centre, +1 = far screen-right. This is what fans the
        // two flanks apart; a tight central scrum has small values, so unitJitter carries it there.
        Vector3 flatOut = s.Anchor - _boardCenter; flatOut.y = 0f;
        float halfSpan = Mathf.Max(0.01f, Battle.BoardCols * 0.5f * hexSize);
        float outward = Mathf.Clamp(Vector3.Dot(flatOut, right) / halfSpan, -1f, 1f);
        // Stable per-unit offset in [-1,1] hashed off the unit id — deterministic (frozen captures
        // stay reproducible) yet distinct per unit, so two neighbours on the same flank still split.
        float jitter = ((s.Unit * 2654435761u) % 2000u) / 1000f - 1f;

        float bias = outward * n.outwardBias + jitter * n.unitJitter;
        float rise = n.riseSpeed * (1f + _data.impact.riseBoost * s.T);
        // Lane splay parts within a unit; bias parts across units. Both ride into velocity too, so
        // separation grows over the number's life instead of holding a fixed initial gap.
        Vector3 vel = Vector3.up * rise + right * (s.Lateral * n.columnDrift + bias);
        fn.Play(s.Anchor + right * (s.Lateral + bias), vel, rise * 1.5f, s.Text, s.Color, s.Scale, _numberFace,
                n.lifeSeconds * s.LifeMul * (1f + _data.impact.lifeBoost * s.T),
                n.characterSize, n.fontSize,
                RecycleNumber);
        return fn;
    }

    private void RecycleNumber(FloatingNumber n)
    { if (n != null) { n.gameObject.SetActive(false); _numberPool.Push(n); } }

    // The Director reaches truth + the pools through delegates. `id => _fold.ById(id)` reads the
    // FIELD live, so a loop-restart fold swap stays wired without rebuilding the Director.
    private FeedbackDirector MakeDirector() => new FeedbackDirector(
        _views, _data, SpawnNumber, RecycleNumber,
        id => _fold.ById(id), HexToWorld,
        GetTracer, RecycleTracer, GetBurst, RecycleBurst,
        GetVfx, RecycleVfx, () => _numberFace, PulseFieldsAt, PushAnnounce, ticksPerSecond, PlaySfx);

    // ---- combat SFX (fight-legibility: audio is the only free channel) ---------
    private AudioSource _audio;
    private readonly Dictionary<string, AudioClip> _sfxCache = new Dictionary<string, AudioClip>();

    /// <summary>One-shot sting by clip name under Resources/Board/SFX. Missing clip = silent no-op,
    /// so tells can be authored before audio exists. Single source + PlayOneShot handles overlap.</summary>
    private void PlaySfx(string name)
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(name)) return;
        if (!_sfxCache.TryGetValue(name, out var clip))
            _sfxCache[name] = clip = Resources.Load<AudioClip>("Board/SFX/" + name);
        if (clip == null) return;
        if (_audio == null)
        {
            var go = new GameObject("~sfx");
            go.transform.SetParent(transform, false);
            _audio = go.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0f;
        }
        _audio.PlayOneShot(clip, 0.85f);
    }

    private Tracer GetTracer() => _tracerPool.Count > 0 ? _tracerPool.Pop() : Tracer.Create(_generated);
    private void RecycleTracer(Tracer t) { if (t != null) { t.gameObject.SetActive(false); _tracerPool.Push(t); } }
    private Burst GetBurst() => _burstPool.Count > 0 ? _burstPool.Pop() : Burst.Create(_generated);
    private void RecycleBurst(Burst b) { if (b != null) { b.gameObject.SetActive(false); _burstPool.Push(b); } }

    private VfxInstance GetVfx(VfxDef def)
    {
        if (def == null || _generated == null) return null;
        if (_vfxPools.TryGetValue(def.Id, out var pool) && pool.Count > 0) return pool.Pop();
        return VfxInstance.Create(_generated, def);
    }

    private void RecycleVfx(VfxInstance fx)
    {
        if (fx == null) return;
        fx.gameObject.SetActive(false);
        if (!_vfxPools.TryGetValue(fx.Id, out var pool)) _vfxPools[fx.Id] = pool = new Stack<VfxInstance>();
        pool.Push(fx);
    }

    // ---- fight story: kill feed + win banner ---------------------------------

    /// <summary>Create the overlay's world-text objects under _generated and anchor them from the
    /// board bounds (mirrors FrameCamera's min/max). Called each Build, so a rebuild/scenario-switch
    /// gives a fresh, empty feed + hidden banner.</summary>
    private void BuildStory()
    {
        RecomputeStoryAnchors();

        _feedSlots = new TextMesh[FeedSlots];
        for (int i = 0; i < FeedSlots; i++) _feedSlots[i] = MakeWorldText(TextAnchor.UpperLeft);
        _bannerText = MakeWorldText(TextAnchor.MiddleCenter);
        _readoutText = MakeWorldText(TextAnchor.UpperCenter);
        _feedLines.Clear();
        _ending = false; _endHold = 0f;
    }

    /// <summary>Story overlay anchors from the board bounds (mirrors FrameCamera's min/max) —
    /// separate from BuildStory so a live board-size reload can re-anchor without recreating texts.</summary>
    private void RecomputeStoryAnchors()
    {
        Vector3 min = HexToWorld(new Hex(0, 0));
        Vector3 max = HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1));
        Vector3 center = (min + max) * 0.5f;
        _feedAnchor = new Vector3(max.x + 1.6f * hexSize, 3.0f, center.z);  // to the board's +X side
        _bannerAnchor = new Vector3(center.x, 3.7f, center.z);              // floating above center
        _readoutAnchor = new Vector3(center.x, 2.7f, center.z);            // just under the banner
    }

    /// <summary>A pooled-free world-space TextMesh (FloatingNumber font path), styled + positioned
    /// live each frame in LayoutStory. Left-anchored lines stack; centered ones head the banner.</summary>
    private TextMesh MakeWorldText(TextAnchor anchor)
    {
        var go = new GameObject("storytext");
        go.transform.SetParent(_generated, false);
        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
        tm.anchor = anchor;
        tm.alignment = anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.fontSize = 180; // high-res glyph texture; world size comes from characterSize (crisp text fix)
        tm.text = "";
        go.SetActive(false);
        return tm;
    }

    /// <summary>Push a kill-feed line for a Death event (newest first, capped to FeedSlots). Names come
    /// from the fold; killer -1 is the storm. Fired unconditionally at dispatch — no tell dependency.</summary>
    private void PushKill(BattleEvent e)
    {
        string victim = _fold.ById(e.Target)?.Name ?? "?";
        string line;
        if (e.Source < 0) line = $"The storm claimed «{victim}»";
        else
        {
            string killer = _fold.ById(e.Source)?.Name ?? "?";
            line = $"«{killer}» felled «{victim}»";
            if (e.Amount > 0) line += $" — overkill {e.Amount}";
        }
        PushFeedLine(line);
    }

    /// <summary>Push the cast line for an `announce` tell, at the START of its windup — the Director
    /// calls this while the sigil is turning, so the name arrives with the telegraph rather than
    /// with the damage. The display name comes from the same content DLL that resolved the id, so
    /// the feed can never disagree with the tell that fired. Announce lines share the kill-feed
    /// slots (fx-runtime "Cast choreography"), which is what rations them: a cast line ages out
    /// like a kill and a busy fight simply pushes the oldest line off the bottom.</summary>
    private void PushAnnounce(int unitId, string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return;
        string caster = _fold?.ById(unitId)?.Name;
        if (string.IsNullOrEmpty(caster)) return;
        PushFeedLine($"«{caster}» casts {Warband.Content.AbilityIdentity.DisplayName(abilityId)}");
    }

    /// <summary>Newest line first, capped at the slot count — the one place feed lines are added, so
    /// kills and cast announces can never grow the feed past what LayoutStory can draw.</summary>
    private void PushFeedLine(string line)
    {
        _feedLines.Insert(0, (line, 0f));
        while (_feedLines.Count > FeedSlots) _feedLines.RemoveAt(_feedLines.Count - 1);
    }

    /// <summary>Age the kill-feed lines and drop any past their lifetime (play mode only — a frozen
    /// preview shows them at full alpha via LayoutStory(true)).</summary>
    private void TickFeed(float dt)
    {
        float life = _data.story.feedLifeSeconds;
        for (int i = _feedLines.Count - 1; i >= 0; i--)
        {
            var l = _feedLines[i]; l.Age += dt; _feedLines[i] = l;
            if (l.Age >= life) _feedLines.RemoveAt(i);
        }
    }

    /// <summary>Position + style the feed lines and (when ending) the banner/readout every frame.
    /// <paramref name="frozen"/> pins feed alpha to 1 for a static capture; play mode fades by age.</summary>
    private void LayoutStory(bool frozen)
    {
        if (_feedSlots == null) return;
        var st = _data.story;
        float gap = st.feedSize * 8f;                 // world spacing scales with text size
        float fade = Mathf.Max(0.01f, Mathf.Min(1f, st.feedLifeSeconds)); // fade over the last ≤1 s

        for (int i = 0; i < _feedSlots.Length; i++)
        {
            var slot = _feedSlots[i];
            if (slot == null) continue;
            bool on = st.feedShow && i < _feedLines.Count;
            var go = slot.gameObject;
            if (go.activeSelf != on) go.SetActive(on);
            if (!on) continue;
            var (text, age) = _feedLines[i];
            slot.text = text;
            slot.characterSize = st.feedSize;
            var c = FeedColor;
            c.a = frozen ? 1f : Mathf.Clamp01((st.feedLifeSeconds - age) / fade);
            slot.color = c;
            slot.transform.position = _feedAnchor + Vector3.down * (gap * i);
            slot.transform.rotation = _numberFace;
        }

        bool bannerOn = _ending;
        if (_bannerText != null)
        {
            if (_bannerText.gameObject.activeSelf != bannerOn) _bannerText.gameObject.SetActive(bannerOn);
            if (bannerOn)
            {
                _bannerText.characterSize = st.bannerSize;
                _bannerText.transform.position = _bannerAnchor;
                _bannerText.transform.rotation = _numberFace;
            }
        }
        if (_readoutText != null)
        {
            if (_readoutText.gameObject.activeSelf != bannerOn) _readoutText.gameObject.SetActive(bannerOn);
            if (bannerOn)
            {
                _readoutText.characterSize = st.bannerSize * 0.4f;
                _readoutText.transform.position = _readoutAnchor;
                _readoutText.transform.rotation = _numberFace;
            }
        }
    }

    /// <summary>Latch the fight-end hold: decide the surviving team and fold the event log ONCE via
    /// Warband.Sim.FightStats for the readout (top-3 damage dealers + kills per side). Idempotent —
    /// _ending guards a re-fold. Winner + kills read the fold at the end tick (all deaths applied).</summary>
    private void BeginEnding()
    {
        if (_ending) return;
        _ending = true; _endHold = 0f;

        int alive0 = 0, alive1 = 0;
        foreach (var u in _fold.Units)
            if (!u.Dead) { if (u.Team == 0) alive0++; else alive1++; }
        bool blueWins = alive0 > 0 && alive1 == 0, redWins = alive1 > 0 && alive0 == 0;
        string title = blueWins ? "BLUE WINS" : redWins ? "RED WINS" : "DRAW";
        Color titleCol = blueWins ? Team0 : redWins ? Team1 : Color.white;

        // Post-fight comprehension via the sim's own fold (no client damage math) — FightSummary
        // ranks units by damage with team share, and carries killed-by attribution per death
        // (killing-blow credit; storm/ownerless damage credits nobody). fight-legibility Phase 4.
        var summary = FightSummary.Build(new BattleResult { Events = _events, EndTick = _endTick, InitialUnits = _initial });
        var lines = new List<string>();
        foreach (var s in summary.Units)
        {
            if (s.DamageDealt <= 0) continue;
            lines.Add($"{s.Name}  {s.DamageDealt} dmg ({s.DamagePctOfTeam:0}%)");
            if (lines.Count >= 3) break;
        }
        // The causality safety net (LTD2 law: when the eye missed it, say it in words): who fell,
        // to whom, when. Cause stands in for the killer on unowned deaths (storm, hazard fields).
        int deathLines = 0;
        foreach (var b in summary.Beats)
        {
            if (deathLines++ >= 3) break;
            string victim = _fold.ById(b.Victim)?.Name ?? "?";
            string killer = b.Killer >= 0 ? (_fold.ById(b.Killer)?.Name ?? "?") : b.Cause.ToString();
            lines.Add($"{victim} fell to {killer} at {b.Tick / 10f:0.0}s");
        }
        // Kills per side = Death events grouped by the killer's team (the sim's own kill attribution,
        // matching the feed 1:1); FightStats.Kills is participation-counted and would over-count here.
        int kills0 = 0, kills1 = 0;
        foreach (var e in _events)
            if (e.Kind == EventKind.Death && e.Source >= 0)
            {
                var killer = _fold.ById(e.Source);
                if (killer != null) { if (killer.Team == 0) kills0++; else kills1++; }
            }
        lines.Add($"Kills   BLUE {kills0}   RED {kills1}");

        if (_bannerText != null) _bannerText.text = title;
        if (_bannerText != null) _bannerText.color = titleCol;
        if (_readoutText != null) { _readoutText.text = string.Join("\n", lines); _readoutText.color = FeedColor; }
        if (Application.isPlaying) PlaybackEnded?.Invoke();
    }

    /// <summary>Clear the whole story overlay: empty the kill feed and hide the banner/readout, and
    /// unlatch the end hold. Called on loop-wrap / ResetAnim / rebuild so nothing leaks across fights.</summary>
    private void ClearStory()
    {
        _feedLines.Clear();
        _ending = false; _endHold = 0f;
        if (_feedSlots != null)
            foreach (var slot in _feedSlots)
                if (slot != null) slot.gameObject.SetActive(false);
        if (_bannerText != null) _bannerText.gameObject.SetActive(false);
        if (_readoutText != null) _readoutText.gameObject.SetActive(false);
    }

    /// <summary>Screen-space nearest live unit within <paramref name="maxPixels"/> of a screen point,
    /// or null — the Tooltip's only window into the fold. Null in edit mode, without a camera, during
    /// the fight-end hold, or when nothing is close. No colliders: MakePrimitive strips them.</summary>
    public PlaybackUnit PickUnit(Vector2 screenPos, float maxPixels)
    {
        if (!Application.isPlaying || _fold == null || _ending) return null;
        var cam = Camera.main;
        if (cam == null) return null;
        int best = -1; float bestSq = maxPixels * maxPixels;
        foreach (var kv in _views)
        {
            var v = kv.Value;
            if (v.Root == null || !v.Root.gameObject.activeSelf) continue;
            Vector3 sp = cam.WorldToScreenPoint(v.Root.position);
            if (sp.z <= 0f) continue; // behind the camera
            float dsq = (new Vector2(sp.x, sp.y) - screenPos).sqrMagnitude;
            if (dsq < bestSq) { bestSq = dsq; best = kv.Key; }
        }
        return best >= 0 ? _fold.ById(best) : null;
    }

    /// <summary>
    /// Project a screen point onto the board plane and return its nearest legal hex. Placement owns
    /// which rows are allowed; the renderer owns only geometry and camera projection.
    /// </summary>
    public bool TryScreenToHex(Vector2 screenPos, out Hex hex)
    {
        hex = default;
        var cam = Camera.main;
        if (cam == null) return false;
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(cam.ScreenPointToRay(screenPos), out float enter)) return false;

        Vector3 point = cam.ScreenPointToRay(screenPos).GetPoint(enter);
        float bestSq = hexSize * hexSize;
        bool found = false;
        for (int row = 0; row < Battle.BoardRows; row++)
            for (int col = 0; col < Battle.BoardCols; col++)
            {
                var candidate = Hex.FromRowCol(row, col);
                float sq = (HexToWorld(candidate) - point).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    hex = candidate;
                    found = true;
                }
            }
        return found;
    }

    private void ApplyPost()
    {
        var vol = FindFirstObjectByType<Volume>();
        if (vol == null || vol.sharedProfile == null) return;
        var p = vol.sharedProfile;
        if (p.TryGet<Bloom>(out var b)) { b.intensity.value = _data.post.bloomIntensity; b.threshold.value = _data.post.bloomThreshold; }
        if (p.TryGet<Vignette>(out var v)) v.intensity.value = _data.post.vignette;
        if (p.TryGet<ColorAdjustments>(out var ca)) ca.saturation.value = _data.post.saturation;
        if (p.TryGet<DepthOfField>(out var d))
        {
            d.active = _data.post.dofEnabled; // see PostTune: transparent text inherits background depth
            d.gaussianStart.value = _data.post.dofStart;
            d.gaussianEnd.value = _data.post.dofEnd;
        }
    }

    // ---- board (hex grid) ----------------------------------------------------

    private void BuildBoard()
    {
        var baseSlab = GameObject.CreatePrimitive(PrimitiveType.Plane);
        baseSlab.name = "BoardBase";
        baseSlab.transform.SetParent(_generated, false);
        Vector3 min = HexToWorld(new Hex(0, 0));
        Vector3 max = HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1));
        Vector3 center = (min + max) * 0.5f;
        float spanX = Mathf.Abs(max.x - min.x) + 4f * hexSize;
        float spanZ = Mathf.Abs(max.z - min.z) + 4f * hexSize;
        baseSlab.transform.position = new Vector3(center.x, -0.04f, center.z);
        baseSlab.transform.localScale = new Vector3(spanX / 10f, 1f, spanZ / 10f);
        Paint(baseSlab.GetComponent<Renderer>(), BaseDark);
        DestroyImmediate(baseSlab.GetComponent<Collider>());

        var tiles = new GameObject("Tiles").transform;
        tiles.SetParent(_generated, false);
        for (int row = 0; row < Battle.BoardRows; row++)
            for (int col = 0; col < Battle.BoardCols; col++)
            {
                var tile = new GameObject($"tile_{row}_{col}");
                tile.transform.SetParent(tiles, false);
                tile.transform.position = HexToWorld(Hex.FromRowCol(row, col));
                float ts = _data != null && _data.board != null ? _data.board.tileScale : 0.9f;
                tile.transform.localScale = new Vector3(ts, 1f, ts);
                tile.AddComponent<MeshFilter>().sharedMesh = HexMesh();
                Color c = row <= 2 ? TileTeam0 : row >= Battle.BoardRows - 3 ? TileTeam1 : TileNeutral;
                PaintTile(tile.AddComponent<MeshRenderer>(), c);
            }
    }

    private Mesh HexMesh()
    {
        if (_hexMesh != null) return _hexMesh;
        var m = new Mesh { name = "hex" };
        var verts = new Vector3[7]; var norms = new Vector3[7];
        verts[0] = Vector3.zero; norms[0] = Vector3.up;
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Deg2Rad * (60f * i + 30f);
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * hexSize;
            norms[i + 1] = Vector3.up;
        }
        var tris = new int[18];
        for (int i = 0; i < 6; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = 1 + (i + 1) % 6; tris[i * 3 + 2] = 1 + i; }
        m.vertices = verts; m.normals = norms; m.triangles = tris; m.RecalculateBounds();
        _hexMesh = m;
        return m;
    }

    // ---- units ---------------------------------------------------------------

    private void SpawnView(PlaybackUnit u)
    {
        var root = new GameObject($"unit_{u.Id}_{u.Name}").transform;
        root.SetParent(_generated, false);

        Color team = u.Team == 0 ? Team0 : Team1;
        var planningMarker = MakePrimitive(
            PrimitiveType.Cylinder,
            root,
            new Vector3(0f, 0.035f, 0f),
            new Vector3(0.74f, 0.015f, 0.74f),
            new Color(0.96f, 0.69f, 0.20f));
        planningMarker.name = "planning-selection";
        planningMarker.gameObject.SetActive(false);

        // Body is a scale/rotation CONTAINER (localPos 0), not the visible mesh: punch scales it and
        // facing yaws it, so the torso + every chassis accessory (its children) inherit both. The
        // fold's lunge stays on Root.position, so MotionOffset and body yaw compose without fighting.
        var body = new GameObject("body").transform;
        body.SetParent(root, false);
        // Model first (fight-legibility Phase 2, KayKit shared-rig minis), primitives as the
        // automatic fallback — a missing model/chassis can never break the board. Key on the
        // stable content id (replay v3+); Name is the fallback for older fixtures.
        float barOff, leanDeg;
        var modelRend = TryBuildModel(body, u, team, out barOff, out leanDeg);
        var torso = modelRend != null
            ? modelRend
            : BuildSilhouette(body, string.IsNullOrEmpty(u.ChassisId) ? u.Name : u.ChassisId,
                              team, out barOff, out leanDeg);

        float barY = 1.55f + barOff, manaY = 1.40f + barOff, pipY = 1.72f + barOff;
        var bars = _data != null ? _data.bars : new BarsTune();
        MakeBarBack(root, barY);
        var hp = MakeFill(root, barY, u.Team == 0 ? bars.allyHp : bars.enemyHp);
        var shield = MakeFill(root, barY, new Color(0.55f, 0.80f, 1.00f));
        shield.localPosition += new Vector3(0f, 0f, -0.04f);
        MakeBarBack(root, manaY, 0.09f);
        var mana = MakeFill(root, manaY, bars.mana, 0.06f);
        // Segment ticks every hpPerSegment (TFT: one divider per fixed HP) — absolute magnitude
        // readable at a glance with no text. Capped so huge health pools don't turn into a comb.
        if (bars.hpPerSegment > 0 && u.MaxHp > bars.hpPerSegment)
        {
            int marks = Mathf.Min(11, u.MaxHp / bars.hpPerSegment);
            for (int m = 1; m <= marks; m++)
            {
                float fx = (float)(m * bars.hpPerSegment) / u.MaxHp;
                if (fx >= 0.999f) break;
                MakePrimitive(PrimitiveType.Cube, root,
                    new Vector3(-BarWidth * 0.5f + BarWidth * fx, barY, -0.06f),
                    new Vector3(0.02f, 0.13f, 0.02f), new Color(0.05f, 0.05f, 0.06f));
            }
        }

        // Status icons take over the old pip anchor (and its barOff law, so a Banneret's row still
        // clears its own flag). The row centres itself on the bar and lifts clear of it — see
        // StatusIconRow.Layout — hence the anchor x of 0 where the pip strip started at its left end.
        var icons = StatusIconRow.Create(root, pipY, _font, StatusColor);

        var nameplate = MakeNameplate(root, pipY + 0.30f, u.Name);

        // Face the enemy at spawn so shields/spears/bows read right in a tick-0 capture (team0 marches
        // to +Z, team1 to -Z); movement + attacks retarget the yaw from there via the Director/fold.
        float yaw0 = u.Team == 0 ? 0f : 180f;
        body.localRotation = Quaternion.Euler(leanDeg, yaw0, 0f);

        _views[u.Id] = new UnitView
        {
            Root = root, Body = body, BodyRenderer = torso, BodyBaseScale = Vector3.one,
            PlanningMarker = planningMarker,
            HpFill = hp, ShieldFill = shield, ManaFill = mana, Icons = icons, Nameplate = nameplate,
            ManaFillBaseH = 0.06f, ManaPulse = bars.manaReadyPulse,
            // Models flash off WHITE (a team tint would permanently recolor the texture; team reads
            // via the ground disc + ally/enemy bars). Primitives keep the team-colored torso.
            MaxHp = u.MaxHp, ManaMax = u.ManaMax, TeamColor = modelRend != null ? Color.white : team,
            Target = HexToWorld(u.Pos), Smoothed = HexToWorld(u.Pos),
            TargetYaw = yaw0, LeanDeg = leanDeg,
            ModelAnimator = modelRend != null ? body.GetComponentInChildren<Animator>() : null,
        };
    }

    // ---- KayKit board models (fight-legibility Phase 2) -----------------------
    // 6 of 8 chassis get their own body; phalanx/banneret are Knight kitbashes with a prop on the
    // rig's dedicated handslot bone; cleric reuses the Mage body (weak seat — $7.95 EXTRA tier adds
    // a Druid if it reads poorly). All bodies share one 23-joint Rig_Medium skeleton.
    private static readonly Dictionary<string, string> ModelByChassis = new Dictionary<string, string>
    {
        ["bulwark"] = "Knight", ["berserker"] = "Barbarian", ["pyromancer"] = "Mage",
        ["sharpshot"] = "Ranger", ["shade"] = "Rogue_Hooded", ["cleric"] = "Mage",
        ["phalanx"] = "Knight", ["banneret"] = "Knight",
    };
    private static readonly Dictionary<string, string> PropByChassis = new Dictionary<string, string>
    {
        ["bulwark"] = "shield_A", ["berserker"] = "axe_C", ["pyromancer"] = "staff_A",
        ["sharpshot"] = "bow_A_withString", ["shade"] = "dagger_A", ["cleric"] = "hammer_A",
        ["phalanx"] = "spear_A", ["banneret"] = "banner_triple_white",
    };

    /// <summary>Instantiate the chassis' KayKit mini under the body container. Returns the flash
    /// renderer, or null → caller falls back to the primitive silhouette (missing model, unknown
    /// chassis, models disabled). Contracts preserved: Body stays the scale/yaw container, Root owns
    /// position, no colliders, one renderer carries the flash. The view's TeamColor is set WHITE by
    /// the caller for models (flash lerps off the texture, not a tint); team reads via a ground disc
    /// + the ally/enemy bars.</summary>
    private Renderer TryBuildModel(Transform body, PlaybackUnit u, Color team, out float barOff, out float leanDeg)
    {
        barOff = 0f; leanDeg = 0f;
        var mt = _data != null ? _data.models : null;
        if (mt == null || !mt.enabled) return null;
        string chassis = (string.IsNullOrEmpty(u.ChassisId) ? u.Name : u.ChassisId).ToLowerInvariant();
        if (!ModelByChassis.TryGetValue(chassis, out var modelName)) return null;
        var model = Resources.Load<GameObject>("Board/KayKit/Characters/" + modelName);
        if (model == null) return null;

        var inst = Instantiate(model, body, false);
        inst.name = "model";
        inst.transform.localScale = Vector3.one * mt.scale;
        var rend = inst.GetComponentInChildren<SkinnedMeshRenderer>();
        if (rend == null) { DestroyImmediate(inst); return null; }
        foreach (var col in inst.GetComponentsInChildren<Collider>()) DestroyImmediate(col); // picking is screen-space, no physics

        // One shared controller (Idle/Run) drives every body — the shared-rig payoff. Missing
        // controller just means bind pose; the board still renders.
        var animator = inst.GetComponent<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        var controller = Resources.Load<RuntimeAnimatorController>("Board/KayKit/BoardUnit");
        if (controller != null) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false; // the sim owns position (render-contract §1, ADR 0018)
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // unfocused remote editor still animates

        // Kitbash prop on the rig's dedicated attach bone.
        if (PropByChassis.TryGetValue(chassis, out var propName))
        {
            var prop = Resources.Load<GameObject>("Board/KayKit/Props/" + propName);
            var slot = FindDeep(inst.transform, "handslot.r") ?? FindDeep(inst.transform, "handslot_r");
            if (prop != null && slot != null)
            {
                var p = Instantiate(prop, slot, false);
                p.name = "prop_" + propName;
                // The banner is a dungeon WALL prop, sized for architecture — hand-held it dwarfs
                // the mini (Jake 2026-07-25). Weapons Bits props are hand-scaled already.
                if (propName.StartsWith("banner")) p.transform.localScale = Vector3.one * 0.38f;
                foreach (var c in p.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
            }
        }

        // Team read: a flat disc at the feet (textured minis can't carry a torso tint honestly).
        MakePrimitive(PrimitiveType.Cylinder, body, new Vector3(0f, 0.015f, 0f),
                      new Vector3(0.66f, 0.012f, 0.66f), team).name = "teamdisc";

        barOff = mt.barLift;
        return rend;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = FindDeep(t.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>Builds the chassis-specific primitive silhouette under the body container: a torso
    /// capsule (team color) plus desaturated-neutral accessories — palette law reserves bright/
    /// saturated for VFX. Keyed on the unit name case-insensitively via Contains, so "feral X" enemy
    /// scaffolding matches the same shape; unknown names fall back to the plain capsule. Returns the
    /// torso Renderer (the flash target). <paramref name="barOff"/> lifts the bars/pips clear of a
    /// tall accessory; <paramref name="leanDeg"/> is a forward body tilt baked into the facing target.</summary>
    private Renderer BuildSilhouette(Transform body, string name, Color team, out float barOff, out float leanDeg)
    {
        barOff = 0f; leanDeg = 0f;
        string n = (name ?? "").ToLowerInvariant();

        // Torso: a capsule scaled (w, h, w). Center y == h drops the feet onto the board (a Unity
        // capsule is 2 tall at scale 1); X == Z keeps body yaw undistorted.
        Transform Torso(float w, float h) =>
            MakePrimitive(PrimitiveType.Capsule, body, new Vector3(0f, h, 0f), new Vector3(w, h, w), team);
        // Accessory: authored directly in body-local units (the container is scale 1), tinted neutral.
        void Acc(PrimitiveType t, Vector3 pos, Vector3 scale, Vector3 euler, Color c) =>
            MakePrimitive(t, body, pos, scale, c).localRotation = Quaternion.Euler(euler);

        Transform torso;
        if (n.Contains("bulwark"))            // wide/squat tank + front shield slab
        {
            torso = Torso(0.80f, 0.44f);
            Acc(PrimitiveType.Cube, new Vector3(0f, 0.48f, 0.44f), new Vector3(0.72f, 0.9f, 0.12f), Vector3.zero, AccSteel);
        }
        else if (n.Contains("phalanx"))       // wide-ish + long spear angled forward
        {
            torso = Torso(0.66f, 0.5f);
            Acc(PrimitiveType.Cylinder, new Vector3(0.16f, 0.6f, 0.34f), new Vector3(0.05f, 0.8f, 0.05f), new Vector3(62f, 0f, 0f), AccGun);
        }
        else if (n.Contains("berserker"))     // bulky + two angled shoulder blades
        {
            torso = Torso(0.72f, 0.52f);
            Acc(PrimitiveType.Cube, new Vector3(-0.34f, 0.78f, 0f), new Vector3(0.26f, 0.14f, 0.32f), new Vector3(0f, 0f, 34f), AccSteel);
            Acc(PrimitiveType.Cube, new Vector3( 0.34f, 0.78f, 0f), new Vector3(0.26f, 0.14f, 0.32f), new Vector3(0f, 0f, -34f), AccSteel);
        }
        else if (n.Contains("shade"))         // slim + forward lean + hip daggers
        {
            torso = Torso(0.46f, 0.5f);
            leanDeg = 12f;
            Acc(PrimitiveType.Cube, new Vector3(-0.24f, 0.34f, 0.08f), new Vector3(0.06f, 0.34f, 0.1f), new Vector3(18f, 0f, 0f), AccGun);
            Acc(PrimitiveType.Cube, new Vector3( 0.24f, 0.34f, 0.08f), new Vector3(0.06f, 0.34f, 0.1f), new Vector3(18f, 0f, 0f), AccGun);
        }
        else if (n.Contains("sharpshot"))     // slim + bow stave held out front (vertical stave → reads as a bow)
        {
            torso = Torso(0.46f, 0.5f);
            Acc(PrimitiveType.Cylinder, new Vector3(0f, 0.62f, 0.34f), new Vector3(0.05f, 0.58f, 0.05f), Vector3.zero, AccGun);
        }
        else if (n.Contains("pyromancer"))    // tall/thin + staff with an orb at the tip
        {
            torso = Torso(0.44f, 0.6f);
            barOff = 0.35f;
            Acc(PrimitiveType.Cylinder, new Vector3(0.28f, 0.8f, 0.08f), new Vector3(0.05f, 0.9f, 0.05f), Vector3.zero, AccGun);
            Acc(PrimitiveType.Sphere,   new Vector3(0.28f, 1.72f, 0.08f), new Vector3(0.18f, 0.18f, 0.18f), Vector3.zero, AccSteel);
        }
        else if (n.Contains("cleric"))        // mid + flat halo disc over the head
        {
            torso = Torso(0.56f, 0.52f);
            barOff = 0.12f;
            Acc(PrimitiveType.Cylinder, new Vector3(0f, 1.28f, 0f), new Vector3(0.42f, 0.03f, 0.42f), Vector3.zero, AccSteel);
        }
        else if (n.Contains("banneret"))      // mid + banner pole & flag — the tallest thing on the board
        {
            torso = Torso(0.54f, 0.52f);
            barOff = 0.5f;
            Acc(PrimitiveType.Cylinder, new Vector3(0.28f, 0.9f, 0.06f), new Vector3(0.05f, 0.95f, 0.05f), Vector3.zero, AccGun);
            Acc(PrimitiveType.Cube,     new Vector3(0.5f, 1.55f, 0.06f), new Vector3(0.5f, 0.32f, 0.03f), Vector3.zero, AccSteel);
        }
        else                                  // unknown → today's plain capsule, unchanged
        {
            torso = Torso(0.6f, 0.5f);
        }
        return torso.GetComponent<Renderer>();
    }

    /// <summary>Per-unit world-space nameplate (chassis name), pooled-free — one lives with the view.
    /// Styled + billboarded live from <see cref="TuningData.nameplates"/> in Update/BuildPreview via
    /// <see cref="StyleNameplate"/>, so a hot-reload resizes/recolors/hides it with no rebuild.</summary>
    private TextMesh MakeNameplate(Transform parent, float y, string text)
    {
        var go = new GameObject("nameplate");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
        tm.anchor = TextAnchor.LowerCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.fontSize = 180; // high-res glyph texture; world size comes from characterSize (crisp text fix)
        tm.text = text;
        var np = _data.nameplates;
        tm.characterSize = np.characterSize;
        tm.color = np.color;
        go.SetActive(np.show);
        return tm;
    }

    /// <summary>Re-reads nameplate tuning and billboards toward the camera every frame (fields-style
    /// live re-apply). Call only for a visible view; a dead unit's Root is inactive, hiding it.</summary>
    private void StyleNameplate(UnitView v)
    {
        if (v.Nameplate == null) return;
        var np = _data.nameplates;
        var go = v.Nameplate.gameObject;
        if (go.activeSelf != np.show) go.SetActive(np.show);
        if (!np.show) return;
        v.Nameplate.characterSize = np.characterSize;
        v.Nameplate.color = np.color;
        v.Nameplate.transform.rotation = _numberFace;
    }

    public void ApplyFold(int tick) => ApplyFold(tick, tick);

    /// <summary>
    /// Advance the fold and place every unit. <paramref name="clock"/> is the FRACTIONAL playhead;
    /// it exists solely so a committed step can be resolved to a point between two hexes.
    ///
    /// Movement law (sim-side): a unit departs at StepStart and arrives at StepEnd, and its logical
    /// position stays the origin the whole way. So the honest thing to draw is the straight line
    /// between the two, parameterised by the sim's own window — not an easing the client invented.
    /// Arrival is exact: at StepEnd the unit is on the hex the sim says it is on, to the frame.
    /// </summary>
    public void ApplyFold(int tick, float clock)
    {
        _fold.AdvanceToTick(_events, tick);
        foreach (var u in _fold.Units)
        {
            if (!_views.TryGetValue(u.Id, out var v)) continue;
            v.Root.gameObject.SetActive(!u.Dead);
            if (u.Dead) { v.Walking = false; continue; }

            Vector3 nt;
            if (u.Walking)
            {
                float span = Mathf.Max(1, u.StepEnd - u.StepStart);
                v.WalkPhase = Mathf.Clamp01((clock - u.StepStart) / span);
                Vector3 from = HexToWorld(u.Pos), to = HexToWorld(u.StepTo);
                nt = Vector3.Lerp(from, to, v.WalkPhase);
                // Face the path, not the last frame's drift: the walk vector is known exactly, so
                // this is stable instead of chasing a sub-pixel delta. A unit that stops walking
                // keeps whatever yaw it had, which lets an Attack tell's facing survive.
                Vector3 path = to - from;
                if (path.sqrMagnitude > 1e-6f) v.TargetYaw = Mathf.Atan2(path.x, path.z) * Mathf.Rad2Deg;
            }
            else
            {
                v.WalkPhase = 0f;
                nt = HexToWorld(u.Pos);
                Vector3 mv = nt - v.Target;   // a teleport (Leap) still turns the body toward its landing
                if (mv.sqrMagnitude > 1e-5f) v.TargetYaw = Mathf.Atan2(mv.x, mv.z) * Mathf.Rad2Deg;
            }
            v.Walking = u.Walking;
            v.Target = nt;
            SetFill(v.HpFill, v.MaxHp > 0 ? (float)u.Hp / v.MaxHp : 0f);
            SetFill(v.ShieldFill, v.MaxHp > 0 ? Mathf.Clamp01((float)u.Shield / v.MaxHp) : 0f);
            SetFill(v.ManaFill, v.ManaMax > 0 ? (float)u.Mana / v.ManaMax : 0f);
            // The cast sentence's first word: "about to cast" is a discrete FLIP (color change +
            // one pulse at full), not an analog quantity the viewer has to measure (Underlords law).
            bool ready = v.ManaMax > 0 && u.Mana >= v.ManaMax;
            if (ready != v.ManaReady)
            {
                v.ManaReady = ready;
                if (ready) v.ManaPulseT = 1f;
                var bars = _data != null ? _data.bars : new BarsTune();
                Paint(v.ManaFill.GetComponent<Renderer>(), ready ? bars.manaReady : bars.mana);
            }
            SetStatusTint(v, u);
            // Detailed read: which statuses, how many, how long left. Cheap on the unchanged path
            // (an element-wise compare against the last multiset), so it can run every frame.
            v.Icons.Sync(u, clock, ticksPerSecond, _data != null ? _data.fx : null);
        }
        SyncFields(clock);
    }

    /// <summary>Status-as-material (Underlords: frozen units LOOK stony, silenced units wear the
    /// mask). At autobattler zoom the body is the only surface big enough to carry a status, so the
    /// heaviest active status tints the whole torso; the icon row stays the detailed secondary read.
    /// Priority: hard control (grey, the unit is OFF) > Phase (icy, the unit is elsewhere) >
    /// burning (ember). A tell's flash still rides on top.</summary>
    private static void SetStatusTint(UnitView v, PlaybackUnit u)
    {
        bool control = false, phase = false, burning = false;
        foreach (var s in u.Statuses)
        {
            switch (s.Kind)
            {
                case StatusKind.Stun: case StatusKind.Root: control = true; break;
                case StatusKind.Phase: phase = true; break;
                case StatusKind.Burn: case StatusKind.Dot: burning = true; break;
            }
        }
        if (control) { v.StatusTint = new Color(0.47f, 0.47f, 0.52f); v.StatusTintAmt = 0.65f; }
        else if (phase) { v.StatusTint = new Color(0.75f, 0.95f, 1.00f); v.StatusTintAmt = 0.60f; }
        else if (burning) { v.StatusTint = new Color(1.00f, 0.45f, 0.20f); v.StatusTintAmt = 0.40f; }
        else v.StatusTintAmt = 0f;
    }

    /// <summary>Point every FieldView at the fold's current fields, creating and retiring views as
    /// the fold gains and loses them. Runs on EVERY ApplyFold, so creation is idempotent per id and
    /// re-pointing an unchanged footprint has to be free (FieldView.SetFootprint bails on a match).
    ///
    /// An ATTACHED field (an aura) derives its hexes from its anchor every pass exactly as it always
    /// did — that is what walks a Dread cloud along with the banneret; the view rebuilds its overlay
    /// geometry on the change without replaying the spawn-in.
    ///
    /// Removal is a FADE, not a delete: a field the fold dropped is flagged BeginExpire and lives
    /// until its Step says it is done. A frozen scrub past a field's expiry simply never builds it —
    /// the fold has no entry, and decoration never resurrects what truth has forgotten.</summary>
    private void SyncFields(float clock)
    {
        foreach (var f in _fold.Fields)
        {
            if (!_fieldViews.TryGetValue(f.Id, out var view))
            {
                // FX seconds this field has already existed — 0 in live play (it appeared this
                // tick), the real elapsed time on a scrub that jumped past its creation.
                float born = _fieldBornAt.TryGetValue(f.Id, out var t) ? t : clock;
                float age = Mathf.Max(0f, (clock - born) / Mathf.Max(0.01f, ticksPerSecond));
                view = FieldView.Create(_generated, f.Id, f.IsWall, f.Flavor, HexToWorld,
                                        () => _data, hexSize, HexMesh(), PaintTile, age);
                _fieldViews[f.Id] = view;
            }
            view.SetFootprint(f.AttachedTo >= 0
                ? (_fold.ById(f.AttachedTo) is PlaybackUnit a && !a.Dead
                    ? Hex.Range(a.Pos, f.Radius) : new List<Hex>())
                : f.Hexes);
        }
        foreach (var kv in _fieldViews)
        {
            bool live = false;
            foreach (var f in _fold.Fields) if (f.Id == kv.Key) { live = true; break; }
            if (!live) kv.Value.BeginExpire();
        }
    }

    // ---- camera / helpers ----------------------------------------------------

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 min = HexToWorld(new Hex(0, 0));
        Vector3 max = HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1));
        Vector3 center = (min + max) * 0.5f;
        float span = Mathf.Max(Mathf.Abs(max.x - min.x), Mathf.Abs(max.z - min.z));
        var offset = Quaternion.Euler(_data.camera.pitch, _data.camera.yaw, 0f) * Vector3.back * (span * _data.camera.distance);
        cam.transform.position = center + offset;
        cam.transform.LookAt(center);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = _data.camera.background;
        _numberFace = cam.transform.rotation;
    }

    private Vector3 HexToWorld(Hex h) =>
        new Vector3(hexSize * Mathf.Sqrt(3f) * (h.Q + h.R / 2f), 0f, hexSize * 1.5f * h.R);

    private const float BarWidth = 0.9f;
    private void MakeBarBack(Transform parent, float y, float h = 0.13f) =>
        MakePrimitive(PrimitiveType.Cube, parent, new Vector3(0f, y, 0.02f), new Vector3(BarWidth, h, 0.05f), new Color(0.05f, 0.05f, 0.06f));
    private Transform MakeFill(Transform parent, float y, Color c, float h = 0.1f) =>
        MakePrimitive(PrimitiveType.Cube, parent, new Vector3(0f, y, 0f), new Vector3(BarWidth, h, 0.06f), c);
    private void SetFill(Transform fill, float frac)
    {
        frac = Mathf.Clamp01(frac);
        var s = fill.localScale; s.x = BarWidth * frac; fill.localScale = s;
        var p = fill.localPosition; p.x = -BarWidth * 0.5f * (1f - frac); fill.localPosition = p;
    }

    private Transform MakePrimitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color c)
    {
        var t = GameObject.CreatePrimitive(type).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos; t.localScale = localScale;
        var col = t.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        Paint(t.GetComponent<Renderer>(), c);
        return t;
    }

    /// <summary>Pip color per status FAMILY — every one of the 27 kinds gets a meaningful family
    /// color (22 used to fall through to grey). Families follow the tell color language: control =
    /// purple, offense-up = warm red, defense = cyan, DoT = ember, heal = green, Phase = ice,
    /// CheatDeath = white-hot (gold stays reserved for crit).</summary>
    private static Color StatusColor(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Dot: case StatusKind.Burn: case StatusKind.BurnAmp:
                return new Color(0.95f, 0.45f, 0.15f);
            case StatusKind.Haste: return new Color(0.95f, 0.90f, 0.35f);
            case StatusKind.Slow: case StatusKind.AttackDown:
                return new Color(0.55f, 0.62f, 0.78f);
            case StatusKind.AttackUp: case StatusKind.CritUp: case StatusKind.CritMultUp:
            case StatusKind.MultiShotRamp: case StatusKind.MultiShotWindow:
            case StatusKind.SwingAmpPct: case StatusKind.Frenzied: case StatusKind.NextSwingCrit:
                return new Color(0.95f, 0.30f, 0.30f);
            case StatusKind.Root: case StatusKind.Silence: case StatusKind.Disarm:
            case StatusKind.Stun: case StatusKind.Taunt:
                return new Color(0.55f, 0.35f, 0.75f);
            case StatusKind.Regen: case StatusKind.OverhealToShield:
                return new Color(0.40f, 0.85f, 0.45f);
            case StatusKind.DamageTakenDown: case StatusKind.CounterCharge:
                return new Color(0.40f, 0.80f, 0.95f);
            case StatusKind.DamageTakenUp: return new Color(0.85f, 0.30f, 0.50f);
            case StatusKind.Phase: return new Color(0.50f, 0.90f, 1.00f);
            case StatusKind.CheatDeath: return new Color(1.00f, 0.95f, 0.85f);
            case StatusKind.Mark: return new Color(0.90f, 0.40f, 0.70f);
            default: return new Color(0.8f, 0.8f, 0.8f);
        }
    }

    private static readonly Dictionary<Color, Material> _matCache = new Dictionary<Color, Material>();
    private static Material CachedMat(Renderer r, Color c, bool doubleSided)
    {
        var key = doubleSided ? c + new Color(0.001f, 0f, 0f) : c;
        if (!_matCache.TryGetValue(key, out var mat) || mat == null)
        {
            var shader = r.sharedMaterial != null ? r.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            if (doubleSided && mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            _matCache[key] = mat;
        }
        return mat;
    }
    private static void Paint(Renderer r, Color c) => r.sharedMaterial = CachedMat(r, c, false);
    private static void PaintTile(Renderer r, Color c) => r.sharedMaterial = CachedMat(r, c, true);
}
