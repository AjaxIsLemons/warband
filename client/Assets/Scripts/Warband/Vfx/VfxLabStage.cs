using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Warband.Sim;

public enum VfxLabStageMode
{
    Recipe,
    CombatFixture,
    Revision,
}

/// <summary>
/// Scene-side substrate for the PC editor VFX Lab. It deliberately uses the shipping
/// ReplayPlayer/VfxInstance/RevisionScreenEffect/SfxPlayer paths: the Lab is a lens over the game,
/// not a second renderer that can drift from it.
///
/// The EditorWindow owns transport time and calls Evaluate/Advance. This component has no Update
/// and creates only HideAndDontSave preview objects, so opening the scene cannot serialize a played
/// effect into source control.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VfxLabStage : MonoBehaviour
{
    private const float SustainedHoldSeconds = 1.4f;
    private const float FixedPreviewStep = 1f / 60f;

    [SerializeField] private Camera previewCamera;
    [SerializeField] private ReplayPlayer replayPlayer;
    [SerializeField] private TuningConfig tuning;

    private RenderTexture _renderTarget;
    private Transform _labRoot;
    private Transform _effectRoot;
    private Transform _sourceProxy;
    private Transform _targetProxy;
    private GameObject _neutralPlatform;
    private VfxInstance _directEffect;
    private VfxDef _directDef;
    private VfxLabRecipeContext _directContext;
    private Color _directColor = Color.white;
    private float _directGlow = VfxLibrary.GlowRef;
    private float _directScale = 1f;
    private float _time;
    private int _lastFixtureTick = -1;
    private string _fixturePath = "replays/weaponry.bytes";
    private float _fixtureDuration;

    private bool _revisionReady;
    private string _revisionFixture = "replays/hourstone.bytes";
    private RevisionEffectKind _revisionLineage = RevisionEffectKind.BorrowedFuture;
    private bool _revisionFullRupture = true;
    private bool _revisionReducedMotion;
    private int _revisionPresentTick = 40;
    private int _revisionBranchTick = 20;
    private RevisionPresentationPhase _lastRevisionPhase = RevisionPresentationPhase.None;
    private readonly List<int> _revisionTargetIds = new List<int>();
    private RevisionPresentationTune _revisionTuneOverride;

    public Camera PreviewCamera => previewCamera;
    public ReplayPlayer ReplayPlayer => replayPlayer;
    public TuningConfig Tuning => tuning;
    public VfxLabStageMode Mode { get; private set; } = VfxLabStageMode.Recipe;
    public VfxLabEnvironmentMode EnvironmentMode { get; private set; } =
        VfxLabEnvironmentMode.ProductionShard;
    public float CurrentTime => _time;
    public bool HasDirectEffect =>
        _directEffect != null && _directEffect.gameObject.activeSelf;

    public float Duration
    {
        get
        {
            switch (Mode)
            {
                case VfxLabStageMode.CombatFixture:
                    return Mathf.Max(0.1f, _fixtureDuration);
                case VfxLabStageMode.Revision:
                    return RevisionDuration();
                default:
                    if (_directDef == null) return 0.5f;
                    return _directDef.Sustained
                        ? SustainedHoldSeconds + Mathf.Max(0.02f, _directDef.Duration)
                        : Mathf.Max(0.02f, _directDef.Duration);
            }
        }
    }

    public void Configure(
        Camera camera,
        ReplayPlayer player,
        TuningConfig tuningConfig)
    {
        previewCamera = camera;
        replayPlayer = player;
        tuning = tuningConfig;
    }

    public void Initialize()
    {
        if (previewCamera == null) previewCamera = Camera.main;
        if (replayPlayer == null) replayPlayer = GetComponent<ReplayPlayer>();
        if (tuning == null) tuning = GetComponent<TuningConfig>();
        EnsureLabObjects();
        if (replayPlayer != null && replayPlayer.transform.Find("~generated") == null)
            replayPlayer.Idle();
        SetEnvironment(EnvironmentMode);
    }

    public void AttachRenderTarget(RenderTexture target)
    {
        Initialize();
        _renderTarget = target;
        if (previewCamera == null) return;
        previewCamera.targetTexture = target;
        if (target != null && target.height > 0)
            previewCamera.aspect = (float)target.width / target.height;
    }

    public void DetachRenderTarget(RenderTexture target)
    {
        if (previewCamera != null && previewCamera.targetTexture == target)
            previewCamera.targetTexture = null;
        if (_renderTarget == target) _renderTarget = null;
    }

    public void RenderNow()
    {
        if (previewCamera == null || _renderTarget == null) return;
        if (!Application.isPlaying) previewCamera.Render();
    }

    public void SelectRecipe(
        VfxDef definition,
        VfxLabRecipeContext context,
        Color motionColor,
        float motionGlow,
        float motionScale)
    {
        if (definition == null) return;
        Initialize();
        bool enteringRecipeMode = Mode != VfxLabStageMode.Recipe;
        Mode = VfxLabStageMode.Recipe;
        ClearRevision();
        _directDef = definition;
        _directContext = context;
        _directColor = motionColor;
        _directGlow = Mathf.Max(0f, motionGlow);
        _directScale = Mathf.Max(0.01f, motionScale);
        _time = 0f;
        if (replayPlayer != null &&
            (enteringRecipeMode || replayPlayer.transform.Find("~generated") == null))
            replayPlayer.Idle();
        SetEnvironment(EnvironmentMode);
        EvaluateRecipe(0f);
    }

    public void UpdateRecipeContext(
        VfxLabRecipeContext context,
        Color motionColor,
        float motionGlow,
        float motionScale)
    {
        _directContext = context;
        _directColor = motionColor;
        _directGlow = Mathf.Max(0f, motionGlow);
        _directScale = Mathf.Max(0.01f, motionScale);
        if (Mode == VfxLabStageMode.Recipe && _directDef != null)
            EvaluateRecipe(_time);
    }

    public void SelectFixture(string relativePath, int tick = 0)
    {
        Initialize();
        Mode = VfxLabStageMode.CombatFixture;
        ClearDirectEffect();
        ClearRevision();
        _fixturePath = NormalizeFixture(relativePath);
        _lastFixtureTick = -1;
        if (replayPlayer == null) return;
        replayPlayer.replayFile = _fixturePath;
        replayPlayer.BuildPreview(Mathf.Max(0, tick));
        _fixtureDuration = replayPlayer.EndTick /
            Mathf.Max(0.01f, CurrentTicksPerSecond());
        _time = tick / Mathf.Max(0.01f, CurrentTicksPerSecond());
        _lastFixtureTick = tick;
        SetEnvironment(EnvironmentMode);
    }

    public void ConfigureRevision(
        string fixturePath,
        RevisionEffectKind lineage,
        bool fullRupture,
        bool reducedMotion,
        int witnessedTick,
        int branchTick)
    {
        Initialize();
        Mode = VfxLabStageMode.Revision;
        ClearDirectEffect();
        ClearRevision();
        _revisionFixture = NormalizeFixture(fixturePath);
        _revisionLineage = lineage;
        _revisionFullRupture = fullRupture;
        _revisionReducedMotion = reducedMotion;
        _revisionPresentTick = Mathf.Max(0, witnessedTick);
        _revisionBranchTick = Mathf.Clamp(branchTick, 0, _revisionPresentTick);
        _time = 0f;
        BeginRevision();
        EvaluateRevision(0f);
    }

    public void SelectScenario(VfxLabScenarioAsset scenario)
    {
        if (scenario == null) return;
        SetEnvironment(scenario.environment);
        switch (scenario.kind)
        {
            case VfxLabScenarioKind.CombatFixture:
                SelectFixture(scenario.fixturePath, scenario.tick);
                break;
            case VfxLabScenarioKind.Revision:
                ConfigureRevision(
                    scenario.fixturePath,
                    scenario.lineage,
                    scenario.fullRupture,
                    scenario.reducedMotion,
                    scenario.witnessedTick,
                    scenario.branchTick);
                break;
            default:
                SelectRecipe(
                    VfxLibrary.Get(scenario.recipeId),
                    scenario.recipeContext,
                    scenario.motionColor,
                    scenario.motionGlow,
                    scenario.motionScale);
                break;
        }
        if (!string.IsNullOrWhiteSpace(scenario.audioCue))
            AuditionSfx(scenario.audioCue, scenario.audioBus, scenario.audioVolume);
    }

    public void SetRevisionTunePreview(RevisionPresentationTune tune)
    {
        _revisionTuneOverride = tune;
        if (Mode == VfxLabStageMode.Revision)
        {
            _revisionReady = false;
            EvaluateRevision(Mathf.Min(_time, Duration));
        }
    }

    public void ClearRevisionTunePreview()
    {
        _revisionTuneOverride = null;
    }

    public void Evaluate(float seconds)
    {
        float duration = Duration;
        _time = Mathf.Clamp(seconds, 0f, duration);
        switch (Mode)
        {
            case VfxLabStageMode.CombatFixture:
                EvaluateFixture(_time);
                break;
            case VfxLabStageMode.Revision:
                EvaluateRevision(_time);
                break;
            default:
                EvaluateRecipe(_time);
                break;
        }
    }

    public void Advance(float deltaSeconds, bool loop)
    {
        float duration = Mathf.Max(0.02f, Duration);
        float next = _time + Mathf.Max(0f, deltaSeconds);
        if (loop && next >= duration)
            next %= duration;
        else
            next = Mathf.Min(next, duration);
        Evaluate(next);
    }

    public void Restart() => Evaluate(0f);

    public void Stop()
    {
        ClearDirectEffect();
        SfxPlayer.StopRevisionLoop();
        if (Mode == VfxLabStageMode.Revision)
        {
            replayPlayer?.ClearRevisionRewindEchoes();
            replayPlayer?.SetRevisionFreeze(false);
            RevisionScreenEffect.Clear();
            _revisionReady = false;
        }
    }

    public void SetEnvironment(VfxLabEnvironmentMode mode)
    {
        InitializeReferencesOnly();
        EnvironmentMode = mode;
        EnsureLabObjects();

        Transform generated = replayPlayer != null
            ? replayPlayer.transform.Find("~generated")
            : null;
        bool production = mode == VfxLabEnvironmentMode.ProductionShard;
        if (generated != null)
        {
            SetChildActive(generated, "Shard", production);
            SetChildActive(generated, "Tiles", production);
            SetChildActive(generated, "BoardBase", production);
        }

        if (_neutralPlatform != null)
            _neutralPlatform.SetActive(mode == VfxLabEnvironmentMode.NeutralStudio);
        if (previewCamera != null)
        {
            previewCamera.backgroundColor = mode == VfxLabEnvironmentMode.Isolation
                ? new Color(0.008f, 0.009f, 0.013f)
                : CurrentCameraBackground();
        }
    }

    public void AuditionSfx(string id, VfxLabAudioBus bus, float volume)
    {
        SfxPlayer.Play(id, ToRuntimeBus(bus), Mathf.Clamp01(volume));
    }

    public void StartRevisionLoop(string id, float volume) =>
        SfxPlayer.StartRevisionLoop(id, Mathf.Clamp01(volume));

    public void StopRevisionLoop() => SfxPlayer.StopRevisionLoop();

    private void EvaluateRecipe(float seconds)
    {
        if (_directDef == null) return;
        EnsureLabObjects();
        ClearDirectEffect();

        _effectRoot = new GameObject("Effect").transform;
        _effectRoot.SetParent(_labRoot, false);
        _effectRoot.gameObject.hideFlags = HideFlags.HideAndDontSave;
        _directEffect = VfxInstance.Create(_effectRoot, _directDef);

        Vector3 source = _sourceProxy.position + Vector3.up * 0.85f;
        Vector3 target = _targetProxy.position + Vector3.up * 0.85f;
        Vector3 at;
        Transform follow = null;
        switch (_directContext)
        {
            case VfxLabRecipeContext.AtSource:
                at = source;
                break;
            case VfxLabRecipeContext.GroundTarget:
                at = new Vector3(_targetProxy.position.x, 0.06f, _targetProxy.position.z);
                break;
            case VfxLabRecipeContext.FollowSource:
                at = source;
                follow = _sourceProxy;
                break;
            default:
                at = target;
                break;
        }

        uint seed = 0x5646584Cu;
        Quaternion billboard = previewCamera != null
            ? previewCamera.transform.rotation
            : Quaternion.identity;
        if (_directContext == VfxLabRecipeContext.Projectile)
        {
            float travel = Mathf.Max(0.12f, _directDef.Duration);
            _directEffect.PlayProjectile(
                source,
                target,
                travel,
                _directColor,
                _directGlow,
                _directScale,
                seed,
                billboard,
                null);
        }
        else
        {
            _directEffect.Play(
                at,
                target - source,
                _directColor,
                _directGlow,
                _directScale,
                seed,
                billboard,
                follow,
                null);
        }

        float remaining = Mathf.Max(0f, seconds);
        if (_directDef.Sustained && remaining > SustainedHoldSeconds)
        {
            StepDirect(SustainedHoldSeconds);
            remaining -= SustainedHoldSeconds;
            _directEffect?.EndSustain();
            StepDirect(remaining);
        }
        else
        {
            StepDirect(Mathf.Min(remaining, Mathf.Max(0f, Duration - 0.0001f)));
        }
    }

    private void StepDirect(float seconds)
    {
        while (_directEffect != null && seconds > 0f)
        {
            float step = Mathf.Min(FixedPreviewStep, seconds);
            if (!_directEffect.Step(step))
            {
                _directEffect.gameObject.SetActive(false);
                break;
            }
            seconds -= step;
        }
    }

    private void EvaluateFixture(float seconds)
    {
        if (replayPlayer == null) return;
        int tick = Mathf.Clamp(
            Mathf.RoundToInt(seconds * CurrentTicksPerSecond()),
            0,
            replayPlayer.EndTick);
        if (tick == _lastFixtureTick) return;
        replayPlayer.replayFile = _fixturePath;
        replayPlayer.BuildPreview(tick);
        _lastFixtureTick = tick;
        SetEnvironment(EnvironmentMode);
    }

    private void BeginRevision()
    {
        if (replayPlayer == null) return;
        replayPlayer.replayFile = _revisionFixture;
        replayPlayer.BuildPreview(_revisionPresentTick);
        ReadRevisionTargets();
#if UNITY_EDITOR
        RevisionScreenEffect.DebugBegin(
            _revisionLineage,
            _revisionFullRupture,
            _revisionReducedMotion,
            CurrentRevisionTune());
#endif
        RevisionScreenEffect.SetTargetViewportPositions(
            new Vector4(0.34f, 0.58f, 0.70f, 0.42f),
            Mathf.Clamp(_revisionTargetIds.Count, 0, 2));
        RevisionScreenEffect.RequestWitnessedFutureCapture();
        replayPlayer.SetRevisionReducedMotion(_revisionReducedMotion);
        replayPlayer.SetRevisionFreeze(0f);
        RenderNow(); // gives the dual-time compositor one witnessed-future frame before scrubbing
        _revisionReady = true;
        _lastRevisionPhase = RevisionPresentationPhase.None;
        SetEnvironment(EnvironmentMode);
    }

    private void EvaluateRevision(float seconds)
    {
        if (!_revisionReady) BeginRevision();
        if (replayPlayer == null) return;
        ClearDirectEffect();
        RevisionPresentationTune tune = CurrentRevisionTune();
        float cursor = Mathf.Max(0f, seconds);

        if (TakePhase(
                ref cursor,
                tune.firstOpenSeconds,
                RevisionPresentationPhase.Opening,
                out float progress))
        {
            replayPlayer.RenderRevisionFrame(_revisionPresentTick);
            replayPlayer.SetRevisionFreeze(progress);
            SetRevisionPhase(RevisionPresentationPhase.Opening, progress);
            return;
        }
        if (TakePhase(
                ref cursor,
                0.32f,
                RevisionPresentationPhase.Held,
                out progress))
        {
            replayPlayer.RenderRevisionFrame(_revisionPresentTick);
            replayPlayer.SetRevisionFreeze(true);
            SetRevisionPhase(RevisionPresentationPhase.Held, progress);
            return;
        }
        if (TakePhase(
                ref cursor,
                tune.tearSeconds,
                RevisionPresentationPhase.Tear,
                out progress))
        {
            replayPlayer.RenderRevisionFrame(_revisionPresentTick);
            replayPlayer.SetRevisionFreeze(true);
            SetRevisionPhase(RevisionPresentationPhase.Tear, progress);
            return;
        }
        float rewindSeconds = RevisionRewindSeconds(tune);
        if (TakePhase(
                ref cursor,
                rewindSeconds,
                RevisionPresentationPhase.Rewind,
                out progress))
        {
            float clock = Mathf.Lerp(
                _revisionPresentTick,
                _revisionBranchTick,
                Smooth01(progress));
            replayPlayer.RenderRevisionFrame(clock);
            replayPlayer.SetRevisionFreeze(true);
            replayPlayer.SetRevisionRewindEchoes(
                _revisionTargetIds,
                clock,
                _revisionPresentTick,
                _revisionLineage,
                progress);
            SetRevisionPhase(RevisionPresentationPhase.Rewind, progress);
            return;
        }
        replayPlayer.ClearRevisionRewindEchoes();
        if (TakePhase(
                ref cursor,
                tune.vacuumSeconds,
                RevisionPresentationPhase.Vacuum,
                out progress))
        {
            replayPlayer.RenderRevisionFrame(_revisionBranchTick);
            replayPlayer.SetRevisionFreeze(true);
            SetRevisionPhase(RevisionPresentationPhase.Vacuum, progress);
            return;
        }
        if (TakePhase(
                ref cursor,
                tune.landingSeconds,
                RevisionPresentationPhase.Landing,
                out progress))
        {
            replayPlayer.RenderRevisionFrame(_revisionBranchTick);
            replayPlayer.SetRevisionFreeze(1f - progress);
            SetRevisionPhase(RevisionPresentationPhase.Landing, progress);
            StepRevisionLanding(progress, tune.landingSeconds);
            return;
        }

        float receipt = Mathf.Max(0.02f, tune.receiptSeconds + tune.receiptTailSeconds);
        TakePhase(
            ref cursor,
            receipt,
            RevisionPresentationPhase.Receipt,
            out progress);
        replayPlayer.RenderRevisionFrame(_revisionBranchTick);
        replayPlayer.SetRevisionFreeze(0f);
        SetRevisionPhase(RevisionPresentationPhase.Receipt, progress);
    }

    private void StepRevisionLanding(float progress, float landingSeconds)
    {
        string id = _revisionLineage == RevisionEffectKind.BorrowedFuture
            ? "revision-land-future"
            : "revision-land-recall";
        VfxDef landing = VfxLibrary.Get(id);
        if (landing == null) return;

        VfxDef previous = _directDef;
        VfxLabRecipeContext previousContext = _directContext;
        Color previousColor = _directColor;
        float previousGlow = _directGlow;
        float previousScale = _directScale;
        _directDef = landing;
        _directContext = VfxLabRecipeContext.GroundTarget;
        _directColor = Color.white;
        _directGlow = VfxLibrary.GlowRef;
        _directScale = 1f;
        EvaluateRecipe(Mathf.Clamp01(progress) * Mathf.Max(0.02f, landingSeconds));
        _directDef = previous;
        _directContext = previousContext;
        _directColor = previousColor;
        _directGlow = previousGlow;
        _directScale = previousScale;
    }

    private void SetRevisionPhase(RevisionPresentationPhase phase, float progress)
    {
        RevisionScreenEffect.SetPhase(phase, progress);
        if (phase == _lastRevisionPhase) return;
        _lastRevisionPhase = phase;
        if (!Application.isPlaying) return;

        switch (phase)
        {
            case RevisionPresentationPhase.Opening:
                SfxPlayer.StopBoardVoices();
                SfxPlayer.Play("revision_split", SfxBus.Revision);
                break;
            case RevisionPresentationPhase.Tear:
                SfxPlayer.Play("revision_tear", SfxBus.Revision);
                break;
            case RevisionPresentationPhase.Rewind:
                SfxPlayer.StartRevisionLoop("revision_rewind_bed", 0.72f);
                break;
            case RevisionPresentationPhase.Vacuum:
                SfxPlayer.ShapeRevisionLoop(0.12f, 0.72f);
                break;
            case RevisionPresentationPhase.Landing:
                SfxPlayer.StopRevisionLoop();
                SfxPlayer.Play(
                    _revisionLineage == RevisionEffectKind.BorrowedFuture
                        ? "revision_land_borrowed"
                        : "revision_land_recall",
                    SfxBus.Revision);
                break;
            case RevisionPresentationPhase.Receipt:
                SfxPlayer.StopRevisionLoop();
                break;
        }
    }

    private void ClearRevision()
    {
        if (replayPlayer != null)
        {
            replayPlayer.ClearRevisionRewindEchoes();
            replayPlayer.SetRevisionFreeze(false);
        }
        SfxPlayer.StopRevisionLoop();
        RevisionScreenEffect.Clear();
        _revisionReady = false;
        _lastRevisionPhase = RevisionPresentationPhase.None;
    }

    private void ReadRevisionTargets()
    {
        _revisionTargetIds.Clear();
        string path = Path.Combine(Application.streamingAssetsPath, _revisionFixture);
        if (!File.Exists(path)) return;
        try
        {
            using FileStream stream = File.OpenRead(path);
            var loaded = Replay.Read(stream);
            foreach (PlaybackUnit unit in loaded.Initial)
            {
                if (unit.Team != 0) continue;
                _revisionTargetIds.Add(unit.Id);
                if (_revisionTargetIds.Count >= 2) break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[VfxLab] Could not inspect Revision targets: {exception.Message}");
        }
    }

    private float RevisionDuration()
    {
        RevisionPresentationTune tune = CurrentRevisionTune();
        return Mathf.Max(
            0.1f,
            tune.firstOpenSeconds +
            0.32f +
            tune.tearSeconds +
            RevisionRewindSeconds(tune) +
            tune.vacuumSeconds +
            tune.landingSeconds +
            tune.receiptSeconds +
            tune.receiptTailSeconds);
    }

    private float RevisionRewindSeconds(RevisionPresentationTune tune)
    {
        if (_revisionReducedMotion) return Mathf.Max(0.1f, tune.reducedRewindSeconds);
        float secondsBack = Mathf.Max(
            0f,
            (_revisionPresentTick - _revisionBranchTick) /
            Mathf.Max(0.01f, CurrentTicksPerSecond()));
        return Mathf.Min(
            tune.rewindMaxSeconds,
            tune.rewindBaseSeconds + secondsBack * tune.rewindPerSecond);
    }

    private static bool TakePhase(
        ref float cursor,
        float duration,
        RevisionPresentationPhase phase,
        out float progress)
    {
        duration = Mathf.Max(0.0001f, duration);
        if (cursor <= duration)
        {
            progress = Mathf.Clamp01(cursor / duration);
            return true;
        }
        cursor -= duration;
        progress = phase == RevisionPresentationPhase.Receipt ? 1f : 0f;
        return false;
    }

    private void ClearDirectEffect()
    {
        if (_directEffect != null)
        {
            _directEffect.Stop();
            _directEffect = null;
        }
        if (_effectRoot != null)
        {
            DestroyPreviewObject(_effectRoot.gameObject);
            _effectRoot = null;
        }
    }

    private void EnsureLabObjects()
    {
        InitializeReferencesOnly();
        if (_labRoot == null)
        {
            var root = new GameObject("~VfxLabPreview");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(transform, false);
            _labRoot = root.transform;
        }

        float hexSize = tuning != null && tuning.data?.board != null
            ? tuning.data.board.hexSize
            : 1.15f;
        Vector3 center = new Vector3(
            hexSize * Mathf.Sqrt(3f) * (Battle.BoardCols - 1) * 0.5f,
            0f,
            hexSize * 1.5f * (Battle.BoardRows - 1) * 0.5f);

        if (_sourceProxy == null)
            _sourceProxy = MakeProxy(
                "Source",
                center + new Vector3(-1.75f, 0f, -0.35f),
                new Color(0.22f, 0.50f, 0.66f));
        if (_targetProxy == null)
            _targetProxy = MakeProxy(
                "Target",
                center + new Vector3(1.75f, 0f, 0.35f),
                new Color(0.58f, 0.28f, 0.30f));
        if (_neutralPlatform == null)
        {
            _neutralPlatform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _neutralPlatform.name = "Neutral Studio";
            _neutralPlatform.hideFlags = HideFlags.HideAndDontSave;
            _neutralPlatform.transform.SetParent(_labRoot, false);
            _neutralPlatform.transform.position = center + Vector3.down * 0.10f;
            _neutralPlatform.transform.localScale = new Vector3(6.4f, 0.06f, 5.0f);
            DestroyPreviewObject(_neutralPlatform.GetComponent<Collider>());
            _neutralPlatform.GetComponent<Renderer>().sharedMaterial =
                ReplayPlayer.CachedMat(new Color(0.105f, 0.115f, 0.135f), false);
        }
        _neutralPlatform.SetActive(EnvironmentMode == VfxLabEnvironmentMode.NeutralStudio);
    }

    private Transform MakeProxy(string label, Vector3 position, Color color)
    {
        var proxy = new GameObject(label);
        proxy.hideFlags = HideFlags.HideAndDontSave;
        proxy.transform.SetParent(_labRoot, false);
        proxy.transform.position = position;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.hideFlags = HideFlags.HideAndDontSave;
        body.transform.SetParent(proxy.transform, false);
        body.transform.localPosition = Vector3.up * 0.68f;
        body.transform.localScale = new Vector3(0.72f, 0.68f, 0.72f);
        DestroyPreviewObject(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = ReplayPlayer.CachedMat(color, false);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = label + " Ring";
        ring.hideFlags = HideFlags.HideAndDontSave;
        ring.transform.SetParent(proxy.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.018f, 0f);
        ring.transform.localScale = new Vector3(1.15f, 0.025f, 1.15f);
        DestroyPreviewObject(ring.GetComponent<Collider>());
        ring.GetComponent<Renderer>().sharedMaterial =
            ReplayPlayer.CachedMat(color * 1.25f, false);
        return proxy.transform;
    }

    private void InitializeReferencesOnly()
    {
        if (previewCamera == null) previewCamera = Camera.main;
        if (replayPlayer == null) replayPlayer = GetComponent<ReplayPlayer>();
        if (tuning == null) tuning = GetComponent<TuningConfig>();
    }

    private float CurrentTicksPerSecond()
    {
        if (tuning != null && tuning.data?.playback != null)
            return tuning.data.playback.ticksPerSecond;
        return replayPlayer != null ? replayPlayer.ticksPerSecond : 10f;
    }

    private RevisionPresentationTune CurrentRevisionTune() =>
        _revisionTuneOverride ??
        (tuning != null && tuning.data?.revision != null
            ? tuning.data.revision
            : new RevisionPresentationTune());

    private Color CurrentCameraBackground() =>
        tuning != null && tuning.data?.camera != null
            ? tuning.data.camera.background
            : new Color(0.055f, 0.06f, 0.08f);

    private static string NormalizeFixture(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return "replays/weaponry.bytes";
        return relativePath.Replace('\\', '/').TrimStart('/');
    }

    private static void SetChildActive(Transform parent, string name, bool active)
    {
        Transform child = parent.Find(name);
        if (child != null) child.gameObject.SetActive(active);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static SfxBus ToRuntimeBus(VfxLabAudioBus bus)
    {
        switch (bus)
        {
            case VfxLabAudioBus.Ui: return SfxBus.Ui;
            case VfxLabAudioBus.Revision: return SfxBus.Revision;
            case VfxLabAudioBus.Decisive: return SfxBus.Decisive;
            case VfxLabAudioBus.Cast: return SfxBus.Cast;
            case VfxLabAudioBus.Impact: return SfxBus.Impact;
            default: return SfxBus.State;
        }
    }

    private static void DestroyPreviewObject(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private void OnDisable()
    {
        ClearDirectEffect();
        ClearRevision();
        if (_renderTarget != null) DetachRenderTarget(_renderTarget);
        if (_labRoot != null)
        {
            DestroyPreviewObject(_labRoot.gameObject);
            _labRoot = null;
        }
    }
}
