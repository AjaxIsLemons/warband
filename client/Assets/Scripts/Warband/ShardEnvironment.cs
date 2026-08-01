using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The battle's environment frame (item 35 Stage 1, `Design/theme.md` "salvage spine"): the fight
/// happens on the last coherent shard of a dying era, floating in the void outside time. This
/// builds that read — a fractured cliff skirt under the board, a gradient void dome, drifting
/// debris beneath, the Tower's silhouette rising from the deep (ADR 0010: the Tower is the
/// constant), and a fill/rim light rig.
///
/// Laws this must keep:
/// - Pure dressing under one "Shard" root inside ~generated: ClearGenerated disposes it, the sim
///   never knows it exists, and nothing here carries a collider (TryScreenToHex raycasts the
///   y=0 plane; unit picking is screen-space — the environment must stay invisible to both).
/// - Deterministic: every vertex is a function of (tuning, seed) — no Time, no UnityEngine.Random —
///   so contact-sheet runs stay byte-identical.
/// - The environment stays desaturated (render-polish palette law: saturation belongs to gameplay
///   VFX). The one warmth is the Sand rim light.
/// - Light objects are named "~env-*": ReplayPlayer.KeyLight skips the prefix so the Deathless dim
///   keeps grabbing the scene's authored key and never the rig.
/// </summary>
internal static class ShardEnvironment
{
    public const string RootName = "Shard";
    public const string EnvLightPrefix = "~env-";
    private const int HallLayer = 30; // HallEnvironmentController's isolation layer — never light it

    public static void Build(Transform parent, EnvironmentTune env, Vector3 boardMin, Vector3 boardMax, float hexSize)
    {
        if (parent == null || env == null || !env.enabled) return;
        var root = new GameObject(RootName).transform;
        root.SetParent(parent, false);

        Vector3 center = (boardMin + boardMax) * 0.5f;
        center.y = 0f;
        // min/max are corner-hex CENTERS; tile geometry reaches ~hexSize past them.
        float halfX = Mathf.Abs(boardMax.x - boardMin.x) * 0.5f + hexSize + 0.2f;
        float halfZ = Mathf.Abs(boardMax.z - boardMin.z) * 0.5f + hexSize + 0.2f;

        BuildShardMesh(root, env, center, halfX, halfZ, hexSize);
        BuildVoidDome(root, env, center);
        BuildVoidArt(root, env, center);
        BuildDebris(root, env, center);
        if (env.towerEnabled) BuildTower(root, env, center);
        RimDressing.Build(root, env, center, halfX, halfZ, hexSize); // Stage 2: the era's kit on the shelf
        BuildLights(root, env);
    }

    /// <summary>Deterministic 0..1 hash — the environment's only randomness source.</summary>
    internal static float Hash01(uint seed, int i)
    {
        uint h = seed * 747796405u + (uint)i * 2891336453u + 0x9E3779B9u;
        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
        return (h & 0xFFFFFF) / 16777215f;
    }

    private static float Signed(uint seed, int i) => Hash01(seed, i) * 2f - 1f;

    // ---- the shard ----------------------------------------------------------

    /// <summary>Distance from center to the board's rectangular envelope along direction t —
    /// the outline is the true board rectangle (a superellipse pulls its corners inside the
    /// corner tiles), roughened per-vertex by the seed. Internal so Stage 2's rim dressing beds
    /// props on the same envelope the cliff is fitted to.</summary>
    internal static float RectRadius(float t, float halfX, float halfZ)
    {
        float c = Mathf.Abs(Mathf.Cos(t)), s = Mathf.Abs(Mathf.Sin(t));
        return 1f / Mathf.Max(c / halfX, s / halfZ);
    }

    private static void BuildShardMesh(Transform root, EnvironmentTune env, Vector3 center, float halfX, float halfZ, float hexSize)
    {
        const int N = 40;
        float margin = env.shardMargin * hexSize;
        uint seed = (uint)Mathf.Max(1, env.shardSeed);

        // Ring profile: outward reach beyond the envelope, radial scale, depth, jitter share.
        // Ring 0 hugs the board apron nearly clean (a torn top edge would open a gap under the
        // BoardBase plane); the fracture grows with depth so the keel reads torn, not turned.
        var rings = new (float extra, float scale, float y, float jitter)[]
        {
            // Ring 0 tucks UNDER the BoardBase plane (-0.04): starting above it left a thin
            // jittered strip of near-horizontal rim catching the key light as an uneven bright
            // trim line around the tiles (probe round 2).
            (margin * 0.2f, 1.00f, -0.06f, 0.06f),
            (margin,        1.00f, -0.22f, 0.35f),
            (margin,        0.88f, -env.shardDepth * 0.45f, 0.8f),
            (margin,        0.62f, -env.shardDepth * 0.88f, 1.0f),
        };

        var pts = new Vector3[rings.Length, N];
        for (int r = 0; r < rings.Length; r++)
            for (int k = 0; k < N; k++)
            {
                float step = Mathf.PI * 2f / N;
                // Angular jitter capped well under half a step so segments can never cross.
                float t = k * step + Signed(seed, r * 977 + k) * step * 0.3f * env.shardJagged * rings[r].jitter;
                float radius = (RectRadius(t, halfX, halfZ) + rings[r].extra) * rings[r].scale
                             + Signed(seed, r * 131 + k) * env.shardJagged * hexSize * 0.8f * rings[r].jitter;
                float y = rings[r].y
                        + (r >= 2 ? Signed(seed, r * 613 + k) * env.shardJagged * 0.12f * env.shardDepth * rings[r].jitter : 0f);
                pts[r, k] = center + new Vector3(Mathf.Cos(t) * radius, y, Mathf.Sin(t) * radius);
            }
        Vector3 keel = center + new Vector3(Signed(seed, 7001) * hexSize, -env.shardDepth * 1.18f, Signed(seed, 7002) * hexSize);

        // Flat shading: every quad/tri owns its verts, RecalculateNormals gives per-face normals.
        var verts = new List<Vector3>(rings.Length * N * 4 + N * 3);
        var sub = new[] { new List<int>(), new List<int>(), new List<int>() };
        void Quad(int s, Vector3 upperK, Vector3 upperK1, Vector3 lowerK1, Vector3 lowerK)
        {
            int i = verts.Count;
            verts.Add(upperK); verts.Add(upperK1); verts.Add(lowerK1); verts.Add(lowerK);
            sub[s].AddRange(new[] { i, i + 2, i + 3, i, i + 1, i + 2 }); // outward-facing winding
        }
        for (int r = 0; r < rings.Length - 1; r++)
            for (int k = 0; k < N; k++)
                Quad(r == 0 ? 0 : 1, pts[r, k], pts[r, (k + 1) % N], pts[r + 1, (k + 1) % N], pts[r + 1, k]);
        for (int k = 0; k < N; k++)
        {
            int i = verts.Count;
            verts.Add(pts[rings.Length - 1, k]); verts.Add(pts[rings.Length - 1, (k + 1) % N]); verts.Add(keel);
            sub[2].AddRange(new[] { i, i + 1, i + 2 });
        }

        var mesh = new Mesh { name = "shard" };
        mesh.SetVertices(verts);
        mesh.subMeshCount = 3;
        for (int s = 0; s < 3; s++) mesh.SetTriangles(sub[s], s);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Cliff");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[]
        {
            ReplayPlayer.CachedMat(env.shardRim, true),
            ReplayPlayer.CachedMat(env.shardCliff, true),
            ReplayPlayer.CachedMat(env.shardKeel, true),
        };
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---- the void -----------------------------------------------------------

    private static readonly Dictionary<(Color, Color, float, float), Material> _gradientCache = new();

    private static void BuildVoidDome(Transform root, EnvironmentTune env, Vector3 center)
    {
        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "VoidDome";
        dome.transform.SetParent(root, false);
        dome.transform.position = center;
        dome.transform.localScale = Vector3.one * 440f; // radius 220 — inside the camera's 1000 far plane
        Object.DestroyImmediate(dome.GetComponent<Collider>());
        var mr = dome.GetComponent<MeshRenderer>();
        Quiet(mr);
        mr.sharedMaterial = GradientMat(env.voidTop, env.voidGlow, env.voidGlowHeight, env.voidGlowWidth);
    }

    private static Material GradientMat(Color top, Color glow, float glowHeight, float glowWidth)
    {
        var key = (top, glow, glowHeight, glowWidth);
        if (_gradientCache.TryGetValue(key, out var cached) && cached != null) return cached;

        float centerV = 0.5f + glowHeight * 0.5f;
        var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
            name = "~shard_void_gradient",
        };
        for (int y = 0; y < 256; y++)
        {
            float v = y / 255f;
            float t = Mathf.Clamp01(1f - Mathf.Abs(v - centerV) / Mathf.Max(glowWidth, 1e-4f));
            tex.SetPixel(0, y, Color.Lerp(top, glow, t * t * (3f - 2f * t)));
        }
        tex.Apply(false, true);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
        mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Front); // camera sits inside
        _gradientCache[key] = mat;
        return mat;
    }

    /// <summary>The authored void backdrop: one feathered, double-sided billboard hung in the deep,
    /// facing the board. Swapping an era's backdrop is a `voidArt` path edit — no code. Kept as a
    /// quad rather than a dome texture because the art is a vertical depth composition; mapping it
    /// onto the dome's latitude band flattens exactly the depth it exists to provide.</summary>
    private static void BuildVoidArt(Transform root, EnvironmentTune env, Vector3 center)
    {
        if (string.IsNullOrEmpty(env.voidArt) || env.voidArtOpacity <= 0f) return;
        var tex = Resources.Load<Texture2D>("Board/" + env.voidArt);
        if (tex == null) return; // art not vendored — the gradient dome still reads, no error

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "VoidArt";
        go.transform.SetParent(root, false);
        Object.DestroyImmediate(go.GetComponent<Collider>());

        float yaw = env.voidArtYawDeg * Mathf.Deg2Rad;
        var outward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        go.transform.position = center + outward * env.voidArtDistance
                              + new Vector3(0f, env.voidArtCenterY - center.y, 0f);
        go.transform.rotation = Quaternion.LookRotation(outward); // material is cull-off, so facing can't hide it
        float h = env.voidArtWidth * tex.height / Mathf.Max(1, tex.width);
        go.transform.localScale = new Vector3(env.voidArtWidth, h, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        Quiet(mr);
        mr.sharedMaterial = VoidArtMat(tex, env.voidArtOpacity);
    }

    private static readonly Dictionary<(Texture2D, float), Material> _voidArtCache = new();

    /// <summary>Unlit transparent, depth-write off, culling off. Transparent draws after every
    /// opaque piece, so the quad composites over the dome while the nearer Tower still occludes it.</summary>
    private static Material VoidArtMat(Texture2D tex, float opacity)
    {
        var key = (tex, opacity);
        if (_voidArtCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
        mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Clamp01(opacity)));
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // 0 = Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        _voidArtCache[key] = mat;
        return mat;
    }

    // ---- debris + the Tower -------------------------------------------------

    private static void BuildDebris(Transform root, EnvironmentTune env, Vector3 center)
    {
        uint seed = (uint)Mathf.Max(1, env.shardSeed);
        for (int i = 0; i < env.debrisCount; i++)
        {
            // Far-side arc only (±75° around +Z): the camera lives on -Z, and a seeded piece in
            // its near quadrant lands between lens and board as a giant black wedge (seen in the
            // first probe round).
            float angle = (90f + Mathf.Lerp(-75f, 75f, Hash01(seed, 900 + i * 7))) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(28f, 65f, Hash01(seed, 901 + i * 7));
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = $"debris_{i}";
            piece.transform.SetParent(root, false);
            piece.transform.position = center + new Vector3(
                Mathf.Cos(angle) * radius,
                -Mathf.Lerp(12f, 32f, Hash01(seed, 902 + i * 7)),
                Mathf.Sin(angle) * radius);
            piece.transform.rotation = Quaternion.Euler(
                Hash01(seed, 903 + i * 7) * 360f, Hash01(seed, 904 + i * 7) * 360f, Hash01(seed, 905 + i * 7) * 360f);
            float s = Mathf.Lerp(0.8f, 2.8f, Hash01(seed, 906 + i * 7));
            piece.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.3f, 0.7f, Hash01(seed, 907 + i * 7)), s * Mathf.Lerp(0.5f, 1.4f, Hash01(seed, 908 + i * 7)));
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            var mr = piece.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.sharedMaterial = ReplayPlayer.CachedMat(env.shardCliff, false);
        }
    }

    private static void BuildTower(Transform root, EnvironmentTune env, Vector3 center)
    {
        var tower = new GameObject("Tower").transform;
        tower.SetParent(root, false);
        float yaw = env.towerYawDeg * Mathf.Deg2Rad;
        tower.position = center + new Vector3(Mathf.Sin(yaw) * env.towerDistance, env.towerTopY - env.towerHeight, Mathf.Cos(yaw) * env.towerDistance);
        tower.rotation = Quaternion.Euler(0f, env.towerYawDeg + 18f, 0f); // quarter-turned silhouette
        float h = env.towerHeight;
        // Stacked tapering blocks + two off-center piers: silhouette-first (unlit), refined by a
        // Stage 2 art job if the shape earns it.
        Block(tower, env.towerColor, new Vector3(0f, h * 0.17f, 0f), new Vector3(h * 0.16f, h * 0.34f, h * 0.16f));
        Block(tower, env.towerColor, new Vector3(0f, h * 0.46f, 0f), new Vector3(h * 0.115f, h * 0.30f, h * 0.115f));
        Block(tower, env.towerColor, new Vector3(0f, h * 0.72f, 0f), new Vector3(h * 0.075f, h * 0.26f, h * 0.075f));
        Block(tower, env.towerColor, new Vector3(0f, h * 0.93f, 0f), new Vector3(h * 0.030f, h * 0.22f, h * 0.030f));
        Block(tower, env.towerColor, new Vector3(h * 0.105f, h * 0.15f, h * 0.02f), new Vector3(h * 0.05f, h * 0.30f, h * 0.05f));
        Block(tower, env.towerColor, new Vector3(-h * 0.09f, h * 0.11f, -h * 0.03f), new Vector3(h * 0.045f, h * 0.22f, h * 0.045f));
    }

    private static void Block(Transform parent, Color c, Vector3 localPos, Vector3 localScale)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.transform.SetParent(parent, false);
        b.transform.localPosition = localPos;
        b.transform.localScale = localScale;
        Object.DestroyImmediate(b.GetComponent<Collider>());
        var mr = b.GetComponent<MeshRenderer>();
        Quiet(mr);
        mr.sharedMaterial = UnlitMat(c);
    }

    private static readonly Dictionary<Color, Material> _unlitCache = new();

    private static Material UnlitMat(Color c)
    {
        if (_unlitCache.TryGetValue(c, out var cached) && cached != null) return cached;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
        _unlitCache[c] = mat;
        return mat;
    }

    private static void Quiet(MeshRenderer mr)
    {
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // ---- lights -------------------------------------------------------------

    private static void BuildLights(Transform root, EnvironmentTune env)
    {
        // Scene key is authored at (48, -30): front-left. Fill answers from front-right, the Sand
        // rim comes from behind-above to cut silhouettes out of the dark void band.
        if (env.fillIntensity > 0f) MakeDirectional(root, "fill", new Vector3(45f, 42f, 0f), env.fillColor, env.fillIntensity);
        // Rim elevation 38°, not grazing: at 26° the Sand light raked the whole far tile field
        // into an orange wash that fought the enemy-side team tint (first probe round).
        if (env.rimIntensity > 0f) MakeDirectional(root, "rim", new Vector3(38f, 192f, 0f), env.rimColor, env.rimIntensity);
    }

    private static void MakeDirectional(Transform root, string kind, Vector3 euler, Color c, float intensity)
    {
        var go = new GameObject(EnvLightPrefix + kind);
        go.transform.SetParent(root, false);
        go.transform.rotation = Quaternion.Euler(euler);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = c;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.cullingMask = ~(1 << HallLayer); // the Hall runs its own rig on its own layer
    }
}
