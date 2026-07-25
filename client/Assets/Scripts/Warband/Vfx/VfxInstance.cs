using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One playing VFX recipe. Pooled per recipe id and Director-STEPPED, exactly like Tracer/Burst/
/// FloatingNumber: no Update, no coroutine, no Time.deltaTime, no TrailRenderer (all four are
/// wall-clock, and a frozen BuildPreview has no wall clock). ParticleSystems advance through
/// Simulate() with a fixed seed, so the same events produce the same pixels and a contact sheet
/// diffs clean run to run.
///
/// The hierarchy an instance owns:
///   vfx_&lt;id&gt;          root — moves along a projectile path, faces the play direction
///   ├── world          elements anchored where they were played
///   └── follow         elements anchored to a unit, re-positioned every Step
/// </summary>
public sealed class VfxInstance : MonoBehaviour
{
    /// <summary>Board-wide cap on live VFX point lights (fx-runtime: LightElement is seasoning, and
    /// URP culls per-object additional lights anyway). An instance that can't get one plays without.</summary>
    public const int MaxLights = 4;
    private static int _liveLights;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int ArcFillId = Shader.PropertyToID("_ArcFill");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");

    // The HDR tint rides a property block on the RENDERER, not the system's start color: the
    // particle vertex-color stream is packed LDR, so an authored tier above 1.0 would clamp there
    // and quietly lose its bloom. Start color stays white + the alpha ramp; _Color carries the lane.
    private sealed class ParticlePart
    {
        public ParticleElement E; public ParticleSystem Ps;
        public Renderer R; public MaterialPropertyBlock Mpb;
    }
    private sealed class QuadPart
    {
        public QuadElement E; public Transform T; public Renderer R;
        public MaterialPropertyBlock Mpb;
    }
    private sealed class LightPart { public LightElement E; public Light L; public bool Held; }

    private VfxDef _def;
    private Transform _world, _follow;
    private readonly List<ParticlePart> _particles = new List<ParticlePart>();
    private readonly List<QuadPart> _quads = new List<QuadPart>();
    private readonly List<LightPart> _lights = new List<LightPart>();

    private Action<VfxInstance> _release;
    private Transform _followTarget;
    private Quaternion _billboard = Quaternion.identity, _facing = Quaternion.identity;
    private Vector3 _start, _end;
    private Color _color = Color.white;
    private float _glow = VfxLibrary.GlowRef, _scale = 1f, _t, _life;
    private bool _projectile, _sustaining;

    public string Id => _def != null ? _def.Id : "";
    public bool IsSustained => _def != null && _def.Sustained;

    /// <summary>Build the object graph for one recipe. The instance is bound to this def for life —
    /// pools are keyed by recipe id, so a recycled instance always replays the same recipe.</summary>
    public static VfxInstance Create(Transform parent, VfxDef def)
    {
        var go = new GameObject("vfx_" + def.Id);
        go.SetActive(false);              // build the graph before anything can Awake/auto-play
        go.transform.SetParent(parent, false);
        go.hideFlags = HideFlags.DontSave;

        var fx = go.AddComponent<VfxInstance>();
        fx._def = def;
        fx._world = new GameObject("world").transform;
        fx._world.SetParent(go.transform, false);
        fx._follow = new GameObject("follow").transform;
        fx._follow.SetParent(go.transform, false);

        for (int i = 0; i < def.Elements.Length; i++)
        {
            var e = def.Elements[i];
            var group = e.Anchor == VfxAnchor.FollowUnit ? fx._follow : fx._world;
            if (e is ParticleElement pe) fx.BuildParticle(pe, group, i);
            else if (e is QuadElement qe) fx.BuildQuad(qe, group, i);
            else if (e is LightElement le) fx.BuildLight(le, group, i);
        }
        return fx;
    }

    private void BuildParticle(ParticleElement e, Transform group, int index)
    {
        var go = new GameObject("p" + index);
        go.transform.SetParent(group, false);
        go.transform.localPosition = e.Offset;

        var ps = go.AddComponent<ParticleSystem>();
        ps.useAutoRandomSeed = false;                   // determinism: the seed comes from the event
        var main = ps.main;
        main.playOnAwake = false;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpace = e.Local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(e.LifeMin, e.LifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(e.SpeedMin, e.SpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(e.SizeMin, e.SizeMax);
        main.gravityModifier = e.Gravity;
        main.maxParticles = e.MaxParticles;
        main.loop = e.Rate > 0f;
        main.duration = Mathf.Max(0.05f, Mathf.Max(_def.Duration, e.LifeMax));
        if (!main.loop) main.startDelay = e.Delay;      // Unity forbids startDelay on looping systems

        var em = ps.emission;
        em.enabled = e.Burst > 0 || e.Rate > 0f;
        em.rateOverTime = e.Rate;
        em.SetBursts(e.Burst > 0
            ? new[] { new ParticleSystem.Burst(0f, (short)e.Burst) }
            : new ParticleSystem.Burst[0]);

        var sh = ps.shape;
        sh.enabled = e.Shape != ParticleShape.Point;
        switch (e.Shape)
        {
            case ParticleShape.Cone: sh.shapeType = ParticleSystemShapeType.Cone; break;
            case ParticleShape.Sphere: sh.shapeType = ParticleSystemShapeType.Sphere; break;
            case ParticleShape.Hemisphere: sh.shapeType = ParticleSystemShapeType.Hemisphere; break;
            case ParticleShape.Circle: sh.shapeType = ParticleSystemShapeType.Circle; break;
        }
        sh.angle = e.ShapeAngle;
        sh.radius = Mathf.Max(0.01f, e.ShapeRadius);
        sh.rotation = e.ShapeRotation;                  // (-90,0,0) turns a cone from "forward" to "up"

        if (e.Drag > 0f)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = new ParticleSystem.MinMaxCurve(0.01f);
            lv.dampen = e.Drag;
        }

        if (e.SizeOverLife != null)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, e.SizeOverLife);
        }

        if (e.Fade)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        if (e.TilesX > 1 || e.TilesY > 1)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.numTilesX = e.TilesX;
            tsa.numTilesY = e.TilesY;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
        }

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        var mat = VfxLibrary.MaterialFor(VfxLibrary.ShaderParticle, VfxLibrary.ParticleTexture(e.Texture));
        rend.sharedMaterial = mat;
        rend.renderMode = e.Stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (e.Stretch) { rend.lengthScale = e.StretchScale; rend.velocityScale = 0f; }
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = LightProbeUsage.Off;
        rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (e.Trails)
        {
            // The ParticleSystem Trails MODULE, never TrailRenderer — trails simulated inside
            // Simulate() step off our clock; a TrailRenderer reads the wall clock (fx-runtime law).
            var tr = ps.trails;
            tr.enabled = true;
            tr.ratio = e.TrailRatio;
            tr.lifetime = new ParticleSystem.MinMaxCurve(e.TrailLifetime);
            tr.widthOverTrail = new ParticleSystem.MinMaxCurve(e.TrailWidth);
            tr.dieWithParticles = true;
            tr.inheritParticleColor = true;
            rend.trailMaterial = mat;
        }

        _particles.Add(new ParticlePart { E = e, Ps = ps, R = rend, Mpb = new MaterialPropertyBlock() });
    }

    private void BuildQuad(QuadElement e, Transform group, int index)
    {
        var go = new GameObject("q" + index);
        go.transform.SetParent(group, false);
        go.transform.localPosition = e.Offset;

        go.AddComponent<MeshFilter>().sharedMesh = e.Hex ? VfxLibrary.HexMesh : VfxLibrary.QuadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = VfxLibrary.MaterialFor(e.Shader, null);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        _quads.Add(new QuadPart { E = e, T = go.transform, R = mr, Mpb = new MaterialPropertyBlock() });
    }

    private void BuildLight(LightElement e, Transform group, int index)
    {
        var go = new GameObject("l" + index);
        go.transform.SetParent(group, false);
        go.transform.localPosition = e.Offset;

        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.shadows = LightShadows.None;
        l.enabled = false;

        _lights.Add(new LightPart { E = e, L = l });
    }

    /// <summary>Play the recipe at a point. <paramref name="dir"/> orients directional elements
    /// (source→target); <paramref name="color"/>/<paramref name="glow"/> are the tell's motionColor/
    /// motionGlow, which tint any element that didn't author its own lane Tint;
    /// <paramref name="seed"/> makes the particle pattern a function of the EVENT, not the run.</summary>
    public void Play(Vector3 pos, Vector3 dir, Color color, float glow, float scale, uint seed,
                     Quaternion billboardRot, Transform followTarget, Action<VfxInstance> release)
    {
        _projectile = false;
        _life = Mathf.Max(0.02f, _def.Duration);
        Begin(pos, dir, color, glow, scale, seed, billboardRot, followTarget, release);
    }

    /// <summary>Play the recipe as a projectile: the root translates start→end over
    /// <paramref name="seconds"/>, which is the SAME window the cube Tracer used, so the impact
    /// latch (ContactOffset) is untouched by the swap.</summary>
    public void PlayProjectile(Vector3 start, Vector3 end, float seconds, Color color, float glow,
                               float scale, uint seed, Quaternion billboardRot, Action<VfxInstance> release)
    {
        _projectile = true;
        _start = start; _end = end;
        _life = Mathf.Max(0.02f, seconds);
        Begin(start, end - start, color, glow, scale, seed, billboardRot, null, release);
    }

    private void Begin(Vector3 pos, Vector3 dir, Color color, float glow, float scale, uint seed,
                       Quaternion billboardRot, Transform followTarget, Action<VfxInstance> release)
    {
        _release = release; _followTarget = followTarget; _billboard = billboardRot;
        _color = color; _glow = glow; _scale = Mathf.Max(0.01f, scale);
        _t = 0f;
        _sustaining = _def.Sustained;
        if (_sustaining) _life = float.MaxValue;

        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        _facing = flat.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(flat.normalized, Vector3.up)
            : Quaternion.identity;
        transform.SetPositionAndRotation(pos, _facing);
        _world.localPosition = Vector3.zero;
        _follow.position = _followTarget != null ? _followTarget.position : pos;

        gameObject.SetActive(true);

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            var main = p.Ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            main.startSize = new ParticleSystem.MinMaxCurve(p.E.SizeMin * _scale, p.E.SizeMax * _scale);
            p.Mpb.SetColor(ColorId, Tinted(p.E, 1f));
            p.R.SetPropertyBlock(p.Mpb);
            var em = p.Ps.emission;
            em.enabled = p.E.Burst > 0 || p.E.Rate > 0f;   // re-enable after a sustained EndSustain

            // The seeding law: clear, stamp the seed, then settle at t=0 so the very first rendered
            // frame is already correct (a frozen capture may never get a second Step).
            p.Ps.Clear(true);
            p.Ps.randomSeed = seed + (uint)(i * 7919);
            p.Ps.Simulate(0f, true, true, false);
        }

        for (int i = 0; i < _quads.Count; i++) SetupQuad(_quads[i]);
        for (int i = 0; i < _lights.Count; i++)
        {
            var lp = _lights[i];
            if (!lp.Held && _liveLights < MaxLights) { lp.Held = true; _liveLights++; }
            lp.L.enabled = lp.Held;
            lp.L.range = lp.E.Range * _scale;
        }

        ApplyTracks();   // t = 0 pose, so a frozen preview at the play instant is already right
    }

    /// <summary>Static per-play shader inputs + the fixed world rotation of a quad. The animated
    /// tracks are written every Step in <see cref="ApplyTracks"/>.</summary>
    private void SetupQuad(QuadPart q)
    {
        var e = q.E;
        q.Mpb.SetFloat(ThicknessId, e.Thickness);
        q.Mpb.SetFloat(SoftnessId, e.Softness);
        q.Mpb.SetFloat(IntensityId, e.Intensity);
        q.Mpb.SetFloat(EdgeFadeId, e.EdgeFade);
        q.Mpb.SetFloat(FalloffId, e.Falloff);
        if (e.Noise && VfxLibrary.Noise != null) q.Mpb.SetTexture(NoiseTexId, VfxLibrary.Noise);
        var tex = VfxLibrary.SigilTexture(e.Texture);
        if (tex != null) q.Mpb.SetTexture(MainTexId, tex);

        switch (e.Orientation)
        {
            // Grid-locked on purpose: a Ground decal must land square on the board's hexes, so it
            // ignores the play direction. Directionality on the floor comes from the shader's
            // _Rotation track instead.
            case QuadOrientation.Ground:
                q.T.rotation = Quaternion.Euler(90f, 0f, 0f);
                break;
            case QuadOrientation.UpFacing:
                q.T.rotation = _facing;
                break;
            default:
                q.T.rotation = _billboard;
                break;
        }
    }

    /// <summary>Stop a sustained recipe: emission ends, live particles run out, and the instance
    /// dies Duration seconds later (that is what Duration MEANS on a sustained def).</summary>
    public void EndSustain()
    {
        if (!_sustaining) return;
        _sustaining = false;
        _life = _t + Mathf.Max(0.02f, _def.Duration);
        foreach (var p in _particles) { var em = p.Ps.emission; em.enabled = false; }
    }

    /// <summary>Advance one Director step; false once finished (already handed back to the pool).</summary>
    public bool Step(float dt)
    {
        _t += dt;

        if (_projectile) transform.position = Vector3.Lerp(_start, _end, Mathf.Clamp01(_t / _life));
        if (_followTarget != null) _follow.position = _followTarget.position;

        // Root-only Simulate: each element is its own top-level system (siblings, never nested), so
        // one call per element steps each system exactly once. withChildren stays true to cover the
        // Trails module's internals; restart:false continues the seeded sequence; fixedTimeStep:false
        // consumes OUR dt instead of Unity's fixed step.
        for (int i = 0; i < _particles.Count; i++)
            _particles[i].Ps.Simulate(dt, true, false, false);

        ApplyTracks();

        if (_t >= _life)
        {
            Stop();
            _release?.Invoke(this);
            return false;
        }
        return true;
    }

    private void ApplyTracks()
    {
        for (int i = 0; i < _quads.Count; i++)
        {
            var q = _quads[i];
            var e = q.E;
            float lt = _t - e.Delay;
            if (lt < 0f) { if (q.R.enabled) q.R.enabled = false; continue; }
            if (!q.R.enabled) q.R.enabled = true;

            float alpha = e.Alpha != null ? e.Alpha.Evaluate(lt) : 1f;
            q.Mpb.SetColor(ColorId, Tinted(e, alpha));
            q.Mpb.SetFloat(AlphaId, alpha);
            if (e.Radius != null) q.Mpb.SetFloat(RadiusId, e.Radius.Evaluate(lt));
            if (e.Arc != null) q.Mpb.SetFloat(ArcFillId, e.Arc.Evaluate(lt));
            if (e.Rotation != null) q.Mpb.SetFloat(RotationId, e.Rotation.Evaluate(lt));
            if (e.Phase != null) q.Mpb.SetFloat(PhaseId, e.Phase.Evaluate(lt));
            q.R.SetPropertyBlock(q.Mpb);

            float s = e.Size * _scale * (e.Scale != null ? e.Scale.Evaluate(lt) : 1f);
            q.T.localScale = new Vector3(s, s, s);
        }

        for (int i = 0; i < _lights.Count; i++)
        {
            var lp = _lights[i];
            if (!lp.Held) continue;
            float lt = _t - lp.E.Delay;
            float k = lt < 0f ? 0f : (lp.E.Intensity != null ? lp.E.Intensity.Evaluate(lt) : 1f);
            var c = lp.E.Tint ?? _color;
            lp.L.color = new Color(c.r, c.g, c.b, 1f);
            lp.L.intensity = Mathf.Max(0f, k * lp.E.Tier * _glow / VfxLibrary.GlowRef);
        }
    }

    /// <summary>The element's HDR color: its authored lane Tint (or the tell's motionColor when it
    /// has none), lifted to its intensity tier and scaled by how far the tell's motionGlow sits from
    /// the authoring reference — so retuning a tell in the F1 cockpit still moves its recipe.</summary>
    private Color Tinted(VfxElement e, float alpha)
    {
        var c = e.Tint ?? _color;
        float k = e.Tier * _glow / VfxLibrary.GlowRef;
        return new Color(c.r * k, c.g * k, c.b * k, alpha);
    }

    /// <summary>Give back the light budget and go quiet, WITHOUT invoking the release callback —
    /// for a Director Reset that is recycling everything itself.</summary>
    public void Stop()
    {
        for (int i = 0; i < _lights.Count; i++)
        {
            var lp = _lights[i];
            lp.L.enabled = false;
            if (lp.Held) { lp.Held = false; _liveLights--; }
        }
        _sustaining = false;
        _followTarget = null;
    }

    /// <summary>Called when every pooled instance is about to be destroyed (ClearGenerated) — the
    /// budget counter is static and would otherwise leak the lights of a discarded board.</summary>
    public static void ResetLightBudget() => _liveLights = 0;
}
