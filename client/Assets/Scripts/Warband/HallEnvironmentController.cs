using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lightweight 2.5D world beneath the Management UI. It owns only presentation: one isolated
/// camera, procedural Tower geometry, bounded particles, and station response. Run truth and UI
/// routing remain in RunShell.
/// </summary>
internal sealed class HallEnvironmentController : MonoBehaviour
{
    private const int HallLayer = 30;
    private static readonly Color Ink = new Color(0.018f, 0.028f, 0.045f, 1f);
    private static readonly Color Obsidian = new Color(0.038f, 0.055f, 0.078f, 1f);
    private static readonly Color Slate = new Color(0.075f, 0.11f, 0.155f, 1f);
    private static readonly Color Iron = new Color(0.18f, 0.25f, 0.34f, 1f);
    private static readonly Color Sand = new Color(0.86f, 0.58f, 0.17f, 1f);
    private static readonly Color TowerBlue = new Color(0.20f, 0.48f, 0.82f, 1f);

    private readonly Dictionary<HallStation, StationVisual> _stations =
        new Dictionary<HallStation, StationVisual>();
    private readonly List<Material> _materials = new List<Material>();
    private readonly List<Mesh> _meshes = new List<Mesh>();

    private sealed class StationVisual
    {
        public Transform Anchor;
        public Material Ring;
        public Material Channel;
        public float PulseUntil;
        public float PulseStrength;
    }

    private HubPresentationConfig _config;
    private GameObject _environment;
    private Camera _camera;
    private Camera _boardCamera;
    private Light _key;
    private Light _sandLight;
    private Transform _outerRing;
    private Transform _innerRing;
    private ParticleSystem _motes;
    private UiAudioDirector _audio;
    private UiFeedbackServices _services;
    private Vector3 _cameraFrom;
    private Vector3 _cameraTo;
    private Quaternion _rotationFrom;
    private Quaternion _rotationTo;
    private float _cameraStarted;
    private float _cameraDuration;
    private float _routePulseUntil;
    private bool _visible;
    private bool _reducedMotion;
    private HallStation _station = HallStation.Overview;

    public UiFeedbackServices Services => _services ??
        new UiFeedbackServices(null, null);

    public static HallEnvironmentController Create(Camera boardCamera,
                                                    HubPresentationConfig config)
    {
        var owner = new GameObject("~HallEnvironment");
        var controller = owner.AddComponent<HallEnvironmentController>();
        controller.Initialize(boardCamera, config);
        return controller;
    }

    private void Initialize(Camera boardCamera, HubPresentationConfig config)
    {
        _boardCamera = boardCamera;
        _config = config ?? HubPresentationConfig.Load();
        BuildEnvironment();
        _audio = gameObject.AddComponent<UiAudioDirector>();
        _audio.Initialize(_config);
        _services = new UiFeedbackServices(_audio, new PlatformUiHaptics(_config));
        UiPolishSignals.Emitted += OnFeedback;
        HubPresentationConfig.Changed += OnConfigChanged;
        SetVisible(false, HallStation.Overview, true);
    }

    public void SetVisible(bool visible, HallStation station, bool reducedMotion)
    {
        _reducedMotion = reducedMotion;
        if (_environment == null || _camera == null) return;
        bool changed = _visible != visible;
        _visible = visible && _config.environment.enabled;
        _environment.SetActive(_visible);
        _camera.enabled = _visible;
        _audio?.SetHallActive(_visible);
        if (_boardCamera != null) _boardCamera.enabled = !_visible;

        if (!_visible) return;
        if (changed || _station != station)
            SetStation(station, changed || reducedMotion);
        ConfigureMotes();
    }

    public void SetStation(HallStation station, bool immediate)
    {
        if (station == HallStation.Breach) station = HallStation.Overview;
        _station = station;
        Pose pose = HallStationPresentationCatalog.Shared.PoseFor(station);
        _cameraFrom = _camera.transform.position;
        _rotationFrom = _camera.transform.rotation;
        _cameraTo = pose.position;
        _rotationTo = pose.rotation;
        _cameraStarted = Time.unscaledTime;
        _cameraDuration = Mathf.Max(0.08f, _config.environment.cameraDurationMs / 1000f);
        if (immediate || _reducedMotion)
        {
            _camera.transform.SetPositionAndRotation(_cameraTo, _rotationTo);
            _cameraStarted = -100f;
        }
    }

    public void PreviewRoute(HallStation station)
    {
        if (_stations.TryGetValue(station, out StationVisual visual))
        {
            visual.PulseUntil = Time.unscaledTime + 0.28f;
            visual.PulseStrength = 0.65f;
        }
    }

    private void Update()
    {
        if (!_visible || _camera == null) return;
        float now = Time.unscaledTime;
        float elapsed = now - _cameraStarted;
        if (elapsed >= 0f && elapsed < _cameraDuration)
        {
            float t = Mathf.Clamp01(elapsed / _cameraDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = _config.environment.cameraOvershoot *
                              Mathf.Sin(Mathf.PI * t) * (1f - t);
            _camera.transform.position = Vector3.LerpUnclamped(_cameraFrom, _cameraTo,
                eased + overshoot);
            _camera.transform.rotation = Quaternion.Slerp(_rotationFrom, _rotationTo, eased);
        }
        else if (elapsed >= _cameraDuration)
        {
            _camera.transform.SetPositionAndRotation(_cameraTo, _rotationTo);
        }

        float ambient = _config.ambientMotion && !_reducedMotion
            ? _config.environment.ambientRingDegreesPerSecond * Time.unscaledDeltaTime
            : 0f;
        if (_outerRing != null) _outerRing.Rotate(Vector3.up, ambient, Space.Self);
        if (_innerRing != null) _innerRing.Rotate(Vector3.up, -ambient * 1.7f, Space.Self);

        float route = Mathf.Clamp01((_routePulseUntil - now) / 0.36f);
        float breathe = _config.ambientMotion && !_reducedMotion
            ? (Mathf.Sin(now * 1.05f) * 0.5f + 0.5f) * _config.environment.ambientPulse
            : 0f;
        if (_sandLight != null)
            _sandLight.intensity = 0.55f + breathe + route * _config.environment.routePulse;

        foreach (var pair in _stations)
        {
            StationVisual visual = pair.Value;
            float pulse = Mathf.Clamp01((visual.PulseUntil - now) / 0.34f) *
                          Mathf.Max(0f, visual.PulseStrength);
            bool selected = pair.Key == _station && _station != HallStation.Overview;
            Color ring = Color.Lerp(new Color(TowerBlue.r, TowerBlue.g, TowerBlue.b, 0.24f),
                Sand * (1.35f + pulse * 1.8f), selected ? 0.72f : pulse);
            ring.a = 0.52f + pulse * 0.35f;
            visual.Ring.SetColor("_Color", ring);
            SetEmission(visual.Channel, Sand * (0.25f + pulse * 2f +
                (selected ? 0.32f : 0f)));
        }
    }

    private void OnFeedback(UiFeedbackEvent feedback)
    {
        if (!_visible) return;
        if (feedback.Cue == UiPolishSignals.Cue.Preview)
        {
            HallStation preview = StationFromTarget(feedback.TargetId);
            if (preview != HallStation.Overview) PreviewRoute(preview);
            return;
        }
        if (feedback.Cue == UiPolishSignals.Cue.Route)
        {
            HallStation destination = StationFromTarget(feedback.TargetId);
            if (feedback.TargetId == "hub-workspace") destination = HallStation.Overview;
            if (destination != HallStation.Overview) PreviewRoute(destination);
            _routePulseUntil = Time.unscaledTime + 0.42f;
            SetStation(destination, _reducedMotion);
            return;
        }

        if (feedback.Cue == UiPolishSignals.Cue.Purchase ||
            feedback.Cue == UiPolishSignals.Cue.Reward ||
            feedback.Cue == UiPolishSignals.Cue.Confirm ||
            feedback.Cue == UiPolishSignals.Cue.Attention)
        {
            HallStation target = StationFromTarget(feedback.TargetId);
            if (_stations.TryGetValue(target, out StationVisual visual))
            {
                visual.PulseUntil = Time.unscaledTime + 0.48f;
                visual.PulseStrength = feedback.Tone == UiFeedbackTone.Major ? 1.5f : 1f;
                BurstAt(visual.Anchor.position, feedback.Tone);
            }
        }
    }

    private void OnConfigChanged()
    {
        if (_key != null) _key.shadows = _config.environment.highQuality
            ? LightShadows.Soft
            : LightShadows.None;
        _audio?.RefreshTuning();
        ConfigureMotes();
    }

    private void BuildEnvironment()
    {
        _environment = new GameObject("Hourstone Hall World");
        _environment.transform.SetParent(transform, false);
        SetLayer(_environment);

        var cameraObject = new GameObject("Hall Camera");
        cameraObject.transform.SetParent(_environment.transform, false);
        SetLayer(cameraObject);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Ink;
        _camera.fieldOfView = 35f;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 80f;
        _camera.depth = 600f;
        _camera.cullingMask = 1 << HallLayer;
        _camera.allowHDR = true;
        _camera.allowMSAA = true;
        var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        additional.renderPostProcessing = false;
        additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

        // Generated materials are opt-in only after a contact-sheet review. The first
        // obsidian/slate/dark-iron candidates baked large focal seams into their albedo, which
        // reads as obvious tiling across the Table. Keep the clean procedural surfaces until an
        // authored candidate passes at gameplay scale.
        Material obsidian = Lit("Hall Obsidian", Obsidian, 0.12f, 0.48f);
        Material slate = Lit("Hall Slate", Slate, 0.28f, 0.62f);
        Material iron = Lit("Hall Iron", Iron, 0.72f, 0.38f,
            resourcePath: "UI/Hall/Materials/hall_iron");
        Material darkIron = Lit("Hall Dark Iron", Iron * 0.48f, 0.78f, 0.32f);
        Material sand = Lit("Hall Living Sand", Sand * 0.52f, 0.34f, 0.50f,
            Sand * 1.45f, "UI/Hall/Materials/hall_living_sand_v2");

        CreatePrimitive("Void Floor", PrimitiveType.Cube, new Vector3(0f, -1.2f, 1.4f),
            new Vector3(28f, 0.4f, 22f), obsidian);
        CreatePrimitive("Table Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.38f, 0f),
            new Vector3(13.9f, 0.22f, 10.4f), darkIron);
        CreatePrimitive("Obsidian Table", PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f),
            new Vector3(13.3f, 0.24f, 9.8f), obsidian);
        CreatePrimitive("Iron Table Rim", PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f),
            new Vector3(13.45f, 0.055f, 9.95f), iron);
        CreatePrimitive("Raised Table Bed", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f),
            new Vector3(12.65f, 0.065f, 9.15f), slate);
        CreatePrimitive("Inner Obsidian", PrimitiveType.Cylinder, new Vector3(0f, 0.13f, 0f),
            new Vector3(12.12f, 0.052f, 8.64f), obsidian);

        _outerRing = CreateWorldRing("Outer Time Ring", 5.15f, 0.72f, 0.022f,
            new Color(Sand.r, Sand.g, Sand.b, 0.40f));
        _innerRing = CreateWorldRing("Inner Time Ring", 3.25f, 0.74f, 0.035f,
            new Color(TowerBlue.r, TowerBlue.g, TowerBlue.b, 0.34f));

        CreateHourstone(sand, iron, obsidian);
        CreateStation(HallStation.Breach, new Vector3(0f, 0.23f, 3.35f), iron, darkIron);
        CreateStation(HallStation.Market, new Vector3(-4.45f, 0.23f, 0f), iron, darkIron);
        CreateStation(HallStation.Armory, new Vector3(4.45f, 0.23f, 0f), iron, darkIron);
        CreateStation(HallStation.Warband, new Vector3(0f, 0.23f, -3.30f), iron, darkIron);
        CreateStation(HallStation.Hourstone, new Vector3(0f, 0.28f, 0f), iron, darkIron, false);

        CreateBackdrop(darkIron, obsidian, iron);
        CreateLights();
        CreateMotes();

        Pose initial = HallStationPresentationCatalog.Shared.PoseFor(HallStation.Overview);
        _camera.transform.SetPositionAndRotation(initial.position, initial.rotation);
    }

    private void CreateHourstone(Material sand, Material iron, Material obsidian)
    {
        var pedestal = CreatePrimitive("Hourstone Pedestal", PrimitiveType.Cylinder,
            new Vector3(0f, 0.26f, 0f), new Vector3(2.3f, 0.17f, 2.3f), iron);
        pedestal.transform.rotation = Quaternion.Euler(0f, 22.5f, 0f);
        CreatePrimitive("Hourstone Socket", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f),
            new Vector3(1.72f, 0.18f, 1.72f), obsidian);

        if (!TryCreateAuthoredHourstone(sand))
        {
            Mesh diamond = CreateOctahedron();
            _meshes.Add(diamond);
            var core = new GameObject("Living Hourstone");
            core.transform.SetParent(_environment.transform, false);
            core.transform.position = new Vector3(0f, 1.22f, 0f);
            core.transform.localScale = new Vector3(0.58f, 1.35f, 0.58f);
            core.AddComponent<MeshFilter>().sharedMesh = diamond;
            core.AddComponent<MeshRenderer>().sharedMaterial = sand;
            SetLayer(core);
        }

        for (int i = 0; i < 4; i++)
        {
            var brace = CreatePrimitive("Hourstone Brace", PrimitiveType.Cube,
                new Vector3(0f, 0.86f, 0f), new Vector3(0.10f, 1.22f, 0.10f), iron);
            float angle = i * 90f + 45f;
            brace.transform.position += new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f,
                Mathf.Sin(angle * Mathf.Deg2Rad)) * 0.66f;
            brace.transform.rotation = Quaternion.Euler(0f, -angle, 14f);
        }
    }

    private void CreateStation(HallStation station, Vector3 position, Material iron,
                               Material darkIron, bool channel = true)
    {
        var anchor = new GameObject(station + " Anchor");
        anchor.transform.SetParent(_environment.transform, false);
        anchor.transform.position = position;
        SetLayer(anchor);

        var socket = CreatePrimitive(station + " Socket", PrimitiveType.Cylinder, position,
            station == HallStation.Hourstone
                ? new Vector3(1.9f, 0.10f, 1.9f)
                : new Vector3(1.55f, 0.10f, 1.55f), darkIron);
        socket.transform.rotation = Quaternion.Euler(0f, station == HallStation.Market ||
            station == HallStation.Armory ? 45f : 0f, 0f);

        Material ringMaterial = RingMaterial(station + " Response Ring",
            new Color(TowerBlue.r, TowerBlue.g, TowerBlue.b, 0.28f));
        var ring = CreateQuad(station + " Response", position + Vector3.up * 0.09f,
            station == HallStation.Hourstone ? 1.75f : 1.42f, ringMaterial);
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Material channelMaterial = Lit(station + " Sand Channel",
            Sand * 0.18f, 0.35f, 0.45f, Sand * 0.18f);
        if (channel && position.sqrMagnitude > 0.5f)
            CreateBeam(station + " Channel", Vector3.up * 0.19f,
                position + Vector3.up * 0.19f, 0.055f, 0.025f, channelMaterial);

        _stations[station] = new StationVisual
        {
            Anchor = anchor.transform,
            Ring = ringMaterial,
            Channel = channelMaterial,
        };
    }

    private void CreateBackdrop(Material darkIron, Material obsidian, Material iron)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            CreatePrimitive("Tower Pier", PrimitiveType.Cube,
                new Vector3(side * 8.2f, 2.8f, 4.9f),
                new Vector3(1.15f, 7.8f, 1.25f), darkIron);
            CreatePrimitive("Tower Blade", PrimitiveType.Cube,
                new Vector3(side * 7.1f, 2.0f, 5.4f),
                new Vector3(0.18f, 5.4f, 3.8f), iron);
        }
        CreatePrimitive("Tower Horizon", PrimitiveType.Cube, new Vector3(0f, 3.5f, 8.2f),
            new Vector3(19f, 8f, 0.75f), obsidian);
        for (int i = -3; i <= 3; i++)
            CreatePrimitive("Horizon Rib", PrimitiveType.Cube, new Vector3(i * 2.35f, 3.1f, 7.75f),
                new Vector3(0.12f, 5.8f, 0.20f), iron);
    }

    private void CreateLights()
    {
        var keyObject = new GameObject("Hall Cool Key");
        keyObject.transform.SetParent(_environment.transform, false);
        keyObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
        SetLayer(keyObject);
        _key = keyObject.AddComponent<Light>();
        _key.type = LightType.Directional;
        _key.color = new Color(0.43f, 0.59f, 0.78f);
        _key.intensity = 1.05f;
        _key.shadows = _config.environment.highQuality ? LightShadows.Soft : LightShadows.None;
        _key.cullingMask = 1 << HallLayer;

        var sandObject = new GameObject("Living Sand Light");
        sandObject.transform.SetParent(_environment.transform, false);
        sandObject.transform.position = new Vector3(0f, 2.0f, 0f);
        SetLayer(sandObject);
        _sandLight = sandObject.AddComponent<Light>();
        _sandLight.type = LightType.Point;
        _sandLight.color = Sand;
        _sandLight.range = 8.5f;
        _sandLight.intensity = 0.65f;
        _sandLight.shadows = LightShadows.None;
        _sandLight.cullingMask = 1 << HallLayer;
    }

    private void CreateMotes()
    {
        var owner = new GameObject("Living Sand Motes");
        owner.transform.SetParent(_environment.transform, false);
        owner.transform.position = new Vector3(0f, 0.4f, 0f);
        SetLayer(owner);
        _motes = owner.AddComponent<ParticleSystem>();
        var main = _motes.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.052f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(Sand.r, Sand.g, Sand.b, 0.10f),
            new Color(Sand.r, Sand.g, Sand.b, 0.42f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        var shape = _motes.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(10f, 1.1f, 7f);
        var velocity = _motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = new ParticleSystem.MinMaxCurve(0.025f, 0.10f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
        // Unity requires all three axes in the SAME MinMaxCurve mode; leaving z at its
        // default (Constant) alongside TwoConstants x/y logs "Particle Velocity curves
        // must all be in the same mode" every play session.
        velocity.z = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
        var noise = _motes.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.05f;

        var renderer = owner.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = ParticleMaterial();
        ConfigureMotes();
    }

    private void ConfigureMotes()
    {
        if (_motes == null || _config?.environment == null) return;
        var emission = _motes.emission;
        emission.rateOverTime = _visible && _config.ambientMotion && !_reducedMotion
            ? Mathf.Clamp(_config.environment.ambientMotes, 0, 64) / 4.5f
            : 0f;
        var main = _motes.main;
        main.maxParticles = Mathf.Max(1, _config.environment.highQuality
            ? _config.environment.ambientMotes
            : Mathf.Min(16, _config.environment.ambientMotes));
    }

    private void BurstAt(Vector3 position, UiFeedbackTone tone)
    {
        if (_motes == null || _reducedMotion) return;
        var emit = new ParticleSystem.EmitParams
        {
            position = position + Vector3.up * 0.12f,
            velocity = Vector3.up * 0.22f,
            startLifetime = 0.55f,
            startSize = tone == UiFeedbackTone.Major ? 0.075f : 0.048f,
            startColor = tone == UiFeedbackTone.Positive
                ? new Color(0.40f, 0.79f, 0.60f, 0.85f)
                : new Color(Sand.r, Sand.g, Sand.b, 0.86f),
        };
        int count = Mathf.Clamp(_config.environment.transactionMotes, 0,
            _config.environment.highQuality ? 32 : 16);
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / Mathf.Max(1, count);
            emit.velocity = new Vector3(Mathf.Cos(angle), 1.2f,
                Mathf.Sin(angle)) * UnityEngine.Random.Range(0.12f, 0.30f);
            _motes.Emit(emit, 1);
        }
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position,
                                       Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(_environment.transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        SetLayer(go);
        return go;
    }

    private Transform CreateWorldRing(string name, float size, float radius, float thickness,
                                      Color color)
    {
        Material material = RingMaterial(name + " Material", color);
        material.SetFloat("_Radius", radius);
        material.SetFloat("_Thickness", thickness);
        material.SetFloat("_Softness", 0.12f);
        GameObject ring = CreateQuad(name, new Vector3(0f, 0.215f, 0f), size, material);
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return ring.transform;
    }

    private GameObject CreateQuad(string name, Vector3 position, float size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(_environment.transform, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size, size, size);
        go.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        SetLayer(go);
        return go;
    }

    private GameObject CreateBeam(string name, Vector3 from, Vector3 to, float width,
                                  float height, Material material)
    {
        Vector3 direction = to - from;
        GameObject beam = CreatePrimitive(name, PrimitiveType.Cube,
            Vector3.Lerp(from, to, 0.5f), new Vector3(width, height, direction.magnitude), material);
        beam.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return beam;
    }

    private bool TryCreateAuthoredHourstone(Material sand)
    {
        GameObject source = Resources.Load<GameObject>("UI/Hall/Meshes/hourstone_core");
        if (source == null) return false;

        GameObject core = Instantiate(source, _environment.transform);
        core.name = "Living Hourstone · Authored";
        core.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        core.transform.localScale = Vector3.one;
        var renderers = core.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(core);
            return false;
        }
        foreach (var renderer in renderers) renderer.sharedMaterial = sand;
        foreach (var collider in core.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        SetLayer(core);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float scale = 2.7f / Mathf.Max(0.01f, bounds.size.y);
        core.transform.localScale = Vector3.one * scale;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        core.transform.position += new Vector3(0f, 1.22f, 0f) - bounds.center;
        return true;
    }

    private Material Lit(string name, Color color, float metallic, float smoothness,
                         Color? emission = null, string resourcePath = null)
    {
        Material source = string.IsNullOrEmpty(resourcePath)
            ? null
            : Resources.Load<Material>(resourcePath);
        Material material;
        if (source != null)
        {
            material = new Material(source) { name = name + " · Authored" };
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }
        if (emission.HasValue) SetEmission(material, emission.Value);
        material.enableInstancing = true;
        _materials.Add(material);
        return material;
    }

    private Material RingMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Warband/Ring");
        var material = new Material(shader) { name = name };
        material.SetColor("_Color", color);
        material.SetFloat("_Radius", 0.75f);
        material.SetFloat("_Thickness", 0.035f);
        material.SetFloat("_Softness", 0.14f);
        material.SetFloat("_ArcFill", 1f);
        _materials.Add(material);
        return material;
    }

    private Material ParticleMaterial()
    {
        Shader shader = Shader.Find("Warband/Particle");
        var material = new Material(shader) { name = "Hall Sand Motes" };
        material.SetColor("_Color", Color.white);
        _materials.Add(material);
        return material;
    }

    private static void SetEmission(Material material, Color color)
    {
        if (material == null || !material.HasProperty("_EmissionColor")) return;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color);
    }

    private static Mesh CreateOctahedron()
    {
        var mesh = new Mesh { name = "Procedural Hourstone" };
        mesh.vertices = new[]
        {
            new Vector3(0f, 1f, 0f), new Vector3(0f, -1f, 0f),
            new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f),
        };
        mesh.triangles = new[]
        {
            0, 5, 3, 0, 3, 4, 0, 4, 2, 0, 2, 5,
            1, 3, 5, 1, 4, 3, 1, 2, 4, 1, 5, 2,
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static HallStation StationFromTarget(string target)
    {
        if (string.IsNullOrEmpty(target)) return HallStation.Overview;
        if (target.IndexOf("market", StringComparison.OrdinalIgnoreCase) >= 0)
            return HallStation.Market;
        if (target.IndexOf("warband", StringComparison.OrdinalIgnoreCase) >= 0)
            return HallStation.Warband;
        if (target.IndexOf("armory", StringComparison.OrdinalIgnoreCase) >= 0)
            return HallStation.Armory;
        if (target.IndexOf("hourstone", StringComparison.OrdinalIgnoreCase) >= 0)
            return HallStation.Hourstone;
        if (target.IndexOf("breach", StringComparison.OrdinalIgnoreCase) >= 0)
            return HallStation.Breach;
        return HallStation.Overview;
    }

    private static void SetLayer(GameObject go)
    {
        go.layer = HallLayer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayer(go.transform.GetChild(i).gameObject);
    }

    private void OnDestroy()
    {
        UiPolishSignals.Emitted -= OnFeedback;
        HubPresentationConfig.Changed -= OnConfigChanged;
        if (_boardCamera != null) _boardCamera.enabled = true;
        foreach (Material material in _materials)
            if (material != null) Destroy(material);
        foreach (Mesh mesh in _meshes)
            if (mesh != null) Destroy(mesh);
        _materials.Clear();
        _meshes.Clear();
    }
}
