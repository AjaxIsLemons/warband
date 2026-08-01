using System;
using System.Collections.Generic;
using UnityEngine;
using Warband.Sim;

/// <summary>
/// Serializable override for one code-authored <see cref="VfxDef"/>.
///
/// The C# library remains the zero-asset fallback. The VFX Lab only creates one of these after an
/// explicit Apply, and <see cref="VfxLibrary"/> resolves preview draft → enabled asset override →
/// built-in recipe. A flat union is intentional: Unity can serialize every field and the Lab can
/// show only the section for <see cref="kind"/> without relying on fragile SerializeReference type
/// names.
/// </summary>
[CreateAssetMenu(fileName = "vfx-recipe", menuName = "Warband/VFX/Recipe Override")]
public sealed class VfxRecipeAsset : ScriptableObject
{
    [Tooltip("Disabled overrides stay available for comparison but the built-in recipe wins.")]
    public bool enabledOverride = true;
    [Tooltip("Stable recipe id used by tell bindings and VfxLibrary.")]
    public string recipeId = "";
    [Min(0.02f)] public float duration = 0.5f;
    [Tooltip("Runs until EndSustain, then uses duration as its fade-out time.")]
    public bool sustained;
    public List<VfxRecipeElementData> elements = new List<VfxRecipeElementData>();

    public VfxDef Compile()
    {
        var compiled = new VfxElement[elements != null ? elements.Count : 0];
        for (int i = 0; i < compiled.Length; i++)
            compiled[i] = (elements[i] ?? VfxRecipeElementData.Default(
                VfxRecipeElementKind.Particle)).Compile();
        return new VfxDef
        {
            Id = recipeId ?? "",
            Duration = Mathf.Max(0.02f, duration),
            Sustained = sustained,
            Elements = compiled,
        };
    }

    public void CopyFrom(VfxDef source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        recipeId = source.Id ?? "";
        duration = Mathf.Max(0.02f, source.Duration);
        sustained = source.Sustained;
        elements = new List<VfxRecipeElementData>(source.Elements?.Length ?? 0);
        if (source.Elements == null) return;
        foreach (VfxElement element in source.Elements)
            elements.Add(VfxRecipeElementData.From(element));
    }

    public static VfxRecipeAsset CreateDraft(VfxDef source)
    {
        var draft = CreateInstance<VfxRecipeAsset>();
        draft.name = source != null ? $"{source.Id} (Draft)" : "New VFX Recipe (Draft)";
        draft.hideFlags = HideFlags.HideAndDontSave;
        if (source != null) draft.CopyFrom(source);
        return draft;
    }

    public bool Validate(List<string> errors, List<string> warnings = null)
    {
        if (errors == null) throw new ArgumentNullException(nameof(errors));
        errors.Clear();
        warnings?.Clear();
        if (string.IsNullOrWhiteSpace(recipeId))
            errors.Add("Recipe id is required.");
        if (duration < 0.02f)
            errors.Add("Duration must be at least 0.02 seconds.");
        if (elements == null || elements.Count == 0)
            errors.Add("A recipe needs at least one element.");
        if (elements != null)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i] == null)
                {
                    errors.Add($"Element {i + 1} is missing.");
                    continue;
                }
                elements[i].Validate(i, errors, warnings);
            }
        }
        return errors.Count == 0;
    }
}

public enum VfxRecipeElementKind
{
    Particle,
    Quad,
    Light,
}

/// <summary>Unity-serializable union of every current VfxElement field.</summary>
[Serializable]
public sealed class VfxRecipeElementData
{
    [Header("Common")]
    public VfxRecipeElementKind kind;
    public VfxAnchor anchor = VfxAnchor.World;
    public Vector3 offset;
    [Min(0f)] public float delay;
    [Min(0f)] public float tier = 1f;
    public bool overrideTint;
    [ColorUsage(true, true)] public Color tint = Color.white;

    [Header("Particle")]
    [Min(0)] public int burst = 10;
    [Min(0f)] public float rate;
    [Min(0.01f)] public float lifeMin = 0.25f;
    [Min(0.01f)] public float lifeMax = 0.45f;
    public float speedMin = 1.2f;
    public float speedMax = 2.2f;
    [Min(0.001f)] public float sizeMin = 0.08f;
    [Min(0.001f)] public float sizeMax = 0.16f;
    public float gravity;
    [Range(0f, 1f)] public float drag;
    public ParticleShape shape = ParticleShape.Cone;
    [Range(0f, 180f)] public float shapeAngle = 25f;
    [Min(0f)] public float shapeRadius = 0.1f;
    public Vector3 shapeRotation;
    public bool local;
    public bool stretch;
    [Min(0f)] public float stretchScale = 2f;
    public bool fade = true;
    public AnimationCurve sizeOverLife;
    public bool trails;
    [Range(0f, 1f)] public float trailRatio = 1f;
    [Min(0.01f)] public float trailLifetime = 0.25f;
    [Min(0f)] public float trailWidth = 0.4f;
    public string particleTexture = "";
    [Min(1)] public int tilesX = 1;
    [Min(1)] public int tilesY = 1;
    [Min(1)] public int maxParticles = 64;

    [Header("Quad")]
    public string shader = VfxLibrary.ShaderGlow;
    public bool hex;
    public QuadOrientation orientation = QuadOrientation.Billboard;
    [Min(0.001f)] public float quadSize = 1f;
    [Min(0f)] public float thickness = 0.12f;
    [Min(0f)] public float softness = 0.15f;
    [Min(0f)] public float intensity = 1f;
    [Range(0f, 1f)] public float edgeFade = 0.35f;
    [Min(0.01f)] public float falloff = 2.5f;
    public string quadTexture = "";
    public bool requireTexture;
    public bool noise;
    public AnimationCurve radius;
    public AnimationCurve arc;
    public AnimationCurve alpha;
    public AnimationCurve rotation;
    public AnimationCurve phase;
    public AnimationCurve scale;

    [Header("Light")]
    [Min(0f)] public float range = 4f;
    public AnimationCurve lightIntensity;

    public VfxElement Compile()
    {
        VfxElement element;
        switch (kind)
        {
            case VfxRecipeElementKind.Quad:
                element = new QuadElement
                {
                    Shader = string.IsNullOrWhiteSpace(shader)
                        ? VfxLibrary.ShaderGlow
                        : shader.Trim(),
                    Hex = hex,
                    Orientation = orientation,
                    Size = Mathf.Max(0.001f, quadSize),
                    Thickness = Mathf.Max(0f, thickness),
                    Softness = Mathf.Max(0f, softness),
                    Intensity = Mathf.Max(0f, intensity),
                    EdgeFade = Mathf.Clamp01(edgeFade),
                    Falloff = Mathf.Max(0.01f, falloff),
                    Texture = Clean(quadTexture),
                    RequireTexture = requireTexture,
                    Noise = noise,
                    Radius = Clone(radius),
                    Arc = Clone(arc),
                    Alpha = Clone(alpha),
                    Rotation = Clone(rotation),
                    Phase = Clone(phase),
                    Scale = Clone(scale),
                };
                break;
            case VfxRecipeElementKind.Light:
                element = new LightElement
                {
                    Range = Mathf.Max(0f, range),
                    Intensity = Clone(lightIntensity),
                };
                break;
            default:
                element = new ParticleElement
                {
                    Burst = Mathf.Max(0, burst),
                    Rate = Mathf.Max(0f, rate),
                    LifeMin = Mathf.Max(0.01f, Mathf.Min(lifeMin, lifeMax)),
                    LifeMax = Mathf.Max(0.01f, Mathf.Max(lifeMin, lifeMax)),
                    SpeedMin = Mathf.Min(speedMin, speedMax),
                    SpeedMax = Mathf.Max(speedMin, speedMax),
                    SizeMin = Mathf.Max(0.001f, Mathf.Min(sizeMin, sizeMax)),
                    SizeMax = Mathf.Max(0.001f, Mathf.Max(sizeMin, sizeMax)),
                    Gravity = gravity,
                    Drag = Mathf.Clamp01(drag),
                    Shape = shape,
                    ShapeAngle = Mathf.Clamp(shapeAngle, 0f, 180f),
                    ShapeRadius = Mathf.Max(0f, shapeRadius),
                    ShapeRotation = shapeRotation,
                    Local = local,
                    Stretch = stretch,
                    StretchScale = Mathf.Max(0f, stretchScale),
                    Fade = fade,
                    SizeOverLife = Clone(sizeOverLife),
                    Trails = trails,
                    TrailRatio = Mathf.Clamp01(trailRatio),
                    TrailLifetime = Mathf.Max(0.01f, trailLifetime),
                    TrailWidth = Mathf.Max(0f, trailWidth),
                    Texture = Clean(particleTexture),
                    TilesX = Mathf.Max(1, tilesX),
                    TilesY = Mathf.Max(1, tilesY),
                    MaxParticles = Mathf.Max(1, maxParticles),
                };
                break;
        }

        element.Anchor = anchor;
        element.Offset = offset;
        element.Delay = Mathf.Max(0f, delay);
        element.Tier = Mathf.Max(0f, tier);
        element.Tint = overrideTint ? tint : (Color?)null;
        return element;
    }

    public static VfxRecipeElementData From(VfxElement source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var data = new VfxRecipeElementData
        {
            anchor = source.Anchor,
            offset = source.Offset,
            delay = source.Delay,
            tier = source.Tier,
            overrideTint = source.Tint.HasValue,
            tint = source.Tint ?? Color.white,
        };

        if (source is ParticleElement p)
        {
            data.kind = VfxRecipeElementKind.Particle;
            data.burst = p.Burst;
            data.rate = p.Rate;
            data.lifeMin = p.LifeMin;
            data.lifeMax = p.LifeMax;
            data.speedMin = p.SpeedMin;
            data.speedMax = p.SpeedMax;
            data.sizeMin = p.SizeMin;
            data.sizeMax = p.SizeMax;
            data.gravity = p.Gravity;
            data.drag = p.Drag;
            data.shape = p.Shape;
            data.shapeAngle = p.ShapeAngle;
            data.shapeRadius = p.ShapeRadius;
            data.shapeRotation = p.ShapeRotation;
            data.local = p.Local;
            data.stretch = p.Stretch;
            data.stretchScale = p.StretchScale;
            data.fade = p.Fade;
            data.sizeOverLife = Clone(p.SizeOverLife);
            data.trails = p.Trails;
            data.trailRatio = p.TrailRatio;
            data.trailLifetime = p.TrailLifetime;
            data.trailWidth = p.TrailWidth;
            data.particleTexture = p.Texture ?? "";
            data.tilesX = p.TilesX;
            data.tilesY = p.TilesY;
            data.maxParticles = p.MaxParticles;
        }
        else if (source is QuadElement q)
        {
            data.kind = VfxRecipeElementKind.Quad;
            data.shader = q.Shader ?? VfxLibrary.ShaderGlow;
            data.hex = q.Hex;
            data.orientation = q.Orientation;
            data.quadSize = q.Size;
            data.thickness = q.Thickness;
            data.softness = q.Softness;
            data.intensity = q.Intensity;
            data.edgeFade = q.EdgeFade;
            data.falloff = q.Falloff;
            data.quadTexture = q.Texture ?? "";
            data.requireTexture = q.RequireTexture;
            data.noise = q.Noise;
            data.radius = Clone(q.Radius);
            data.arc = Clone(q.Arc);
            data.alpha = Clone(q.Alpha);
            data.rotation = Clone(q.Rotation);
            data.phase = Clone(q.Phase);
            data.scale = Clone(q.Scale);
        }
        else if (source is LightElement l)
        {
            data.kind = VfxRecipeElementKind.Light;
            data.range = l.Range;
            data.lightIntensity = Clone(l.Intensity);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(source), source.GetType().FullName, "Unsupported VFX element type.");
        }
        return data;
    }

    public static VfxRecipeElementData Default(VfxRecipeElementKind elementKind)
    {
        var data = new VfxRecipeElementData { kind = elementKind };
        if (elementKind == VfxRecipeElementKind.Quad)
        {
            data.shader = VfxLibrary.ShaderGlow;
            data.alpha = AnimationCurve.EaseInOut(0f, 1f, 0.5f, 0f);
            data.scale = AnimationCurve.EaseInOut(0f, 0.2f, 0.2f, 1f);
        }
        else if (elementKind == VfxRecipeElementKind.Light)
        {
            data.tier = 1.4f;
            data.lightIntensity = AnimationCurve.EaseInOut(0f, 0f, 0.1f, 1f);
        }
        return data;
    }

    public VfxRecipeElementData DeepCopy()
    {
        VfxRecipeElementData copy = From(Compile());
        copy.kind = kind;
        return copy;
    }

    public void Validate(int index, List<string> errors, List<string> warnings)
    {
        string label = $"Element {index + 1} ({kind})";
        if (tier <= 0f) warnings?.Add($"{label} has zero intensity tier.");
        if (kind == VfxRecipeElementKind.Particle)
        {
            if (burst <= 0 && rate <= 0f) warnings?.Add($"{label} emits no particles.");
            if (maxParticles < 1) errors.Add($"{label} maxParticles must be positive.");
            if (lifeMin <= 0f || lifeMax <= 0f) errors.Add($"{label} lifetime must be positive.");
            if (tilesX < 1 || tilesY < 1) errors.Add($"{label} flipbook dimensions must be positive.");
        }
        else if (kind == VfxRecipeElementKind.Quad)
        {
            if (string.IsNullOrWhiteSpace(shader)) errors.Add($"{label} needs a shader.");
            if (quadSize <= 0f) errors.Add($"{label} size must be positive.");
            if (requireTexture && string.IsNullOrWhiteSpace(quadTexture))
                warnings?.Add($"{label} requires a texture but has no Resources path.");
        }
        else if (range <= 0f)
        {
            warnings?.Add($"{label} has no range.");
        }
    }

    private static string Clean(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AnimationCurve Clone(AnimationCurve source)
    {
        if (source == null) return null;
        var curve = new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode,
        };
        return curve;
    }
}

public enum VfxLabEnvironmentMode
{
    ProductionShard,
    NeutralStudio,
    Isolation,
}

public enum VfxLabRecipeContext
{
    AtSource,
    AtTarget,
    GroundTarget,
    FollowSource,
    Projectile,
}

public enum VfxLabScenarioKind
{
    Recipe,
    CombatFixture,
    Revision,
}

public enum VfxLabAudioBus
{
    State,
    Impact,
    Cast,
    Decisive,
    Revision,
    Ui,
}

/// <summary>
/// Optional authored bookmark for a composition that needs more context than one recipe id. The
/// Lab discovers these automatically; new fixtures and recipes require no scenario asset.
/// </summary>
[CreateAssetMenu(fileName = "vfx-lab-scenario", menuName = "Warband/VFX/Lab Scenario")]
public sealed class VfxLabScenarioAsset : ScriptableObject
{
    public string displayName = "New VFX Scenario";
    [TextArea(2, 5)] public string notes = "";
    public VfxLabScenarioKind kind;
    public VfxLabEnvironmentMode environment = VfxLabEnvironmentMode.ProductionShard;

    [Header("Recipe")]
    public string recipeId = "";
    public VfxLabRecipeContext recipeContext = VfxLabRecipeContext.AtTarget;
    [ColorUsage(true, true)] public Color motionColor = Color.white;
    [Range(0f, 8f)] public float motionGlow = VfxLibrary.GlowRef;
    [Range(0.2f, 4f)] public float motionScale = 1f;

    [Header("Combat fixture")]
    public string fixturePath = "replays/weaponry.bytes";
    [Min(0)] public int tick;

    [Header("Revision")]
    public RevisionEffectKind lineage = RevisionEffectKind.BorrowedFuture;
    public bool fullRupture = true;
    public bool reducedMotion;
    [Min(0)] public int witnessedTick = 40;
    [Min(0)] public int branchTick = 20;

    [Header("Optional audio audition")]
    public string audioCue = "";
    public VfxLabAudioBus audioBus = VfxLabAudioBus.State;
    [Range(0f, 1f)] public float audioVolume = 1f;
}
