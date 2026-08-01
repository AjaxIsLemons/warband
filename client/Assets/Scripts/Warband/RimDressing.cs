using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Rim dressing (item 35 Stage 2): the era's kit planted along the shard's fracture shelf, so the
/// board's edge reads as ground someone fought over instead of a clean cut.
///
/// This is a KIT SYSTEM, not a prop list. Every model, weight, scale, pitch and bed depth lives in
/// tuning.json under `environment.rim.props`, addressed by Resources path — so the next era's
/// dressing (KayKit Medieval Hexagon, CC0, Jake approved 2026-07-30) is a data edit and never a
/// code change. Act-scoped kits (Stage 3) swap the same list.
///
/// Laws inherited from <see cref="ShardEnvironment"/>:
/// - Built under the Shard root inside ~generated, so ClearGenerated disposes it and the sim never
///   knows it exists.
/// - No colliders anywhere: TryScreenToHex raycasts the y=0 plane and unit picking is screen-space,
///   so a prop with a collider would eat clicks meant for the board.
/// - Deterministic: every placement is a function of (tuning, seed) — no Time, no UnityEngine.Random
///   — because the byte-identical contact sheet is the commit gate.
/// - Desaturated: the kit tint multiplies the source texture down. Saturation belongs to gameplay VFX.
/// - A missing model yields FEWER props, never an exception. Decorative failure must not touch the
///   fight (a VFX throw inside a ceremony coroutine once froze a whole battle).
/// </summary>
internal static class RimDressing
{
    public const string RootName = "RimDressing";

    public static void Build(Transform shardRoot, EnvironmentTune env, Vector3 center, float halfX, float halfZ, float hexSize)
    {
        var rim = env?.rim;
        if (shardRoot == null || rim == null || !rim.enabled || rim.count <= 0) return;

        float totalWeight = 0f;
        if (rim.props != null)
            foreach (var p in rim.props)
                if (p != null && !string.IsNullOrEmpty(p.model) && p.weight > 0f) totalWeight += p.weight;
        if (totalWeight <= 0f) return;

        var root = new GameObject(RootName).transform;
        root.SetParent(shardRoot, false);

        uint seed = (uint)Mathf.Max(1, rim.seed);
        float margin = env.shardMargin * hexSize;

        // The clear arc is centred on the camera's bearing (-Z). Sampling the REMAINDER directly
        // (rather than rejecting draws inside the gap) keeps the seed→placement mapping stable when
        // nearGapDeg is dialled, so widening the gap slides props instead of reshuffling the ring.
        float gap = Mathf.Clamp(rim.nearGapDeg, 0f, 359f) * Mathf.Deg2Rad;
        float arcStart = -Mathf.PI * 0.5f + gap * 0.5f;
        float arcSpan = Mathf.PI * 2f - gap;

        var block = new MaterialPropertyBlock();
        for (int i = 0; i < rim.count; i++)
        {
            var def = Pick(rim, totalWeight, ShardEnvironment.Hash01(seed, 400 + i * 11));
            if (def == null) continue;
            var model = Resources.Load<GameObject>("Board/" + def.model);
            if (model == null) continue; // kit not vendored yet — fewer props, no error

            float t = arcStart + arcSpan * ShardEnvironment.Hash01(seed, 401 + i * 11);
            float band = Mathf.Lerp(rim.bandInner, rim.bandOuter, ShardEnvironment.Hash01(seed, 402 + i * 11));
            float radius = ShardEnvironment.RectRadius(t, halfX, halfZ) + margin * band;
            // Ring 0 of the cliff sits at y=-0.06 (band 0.2), ring 1 at y=-0.22 (band 1.0): bed the
            // prop on that same slope so it stands ON the shelf rather than floating over the tear.
            float shelfY = Mathf.Lerp(-0.06f, -0.22f, Mathf.InverseLerp(0.2f, 1f, band)) + def.sink;

            var inst = Object.Instantiate(model, root, false);
            inst.name = $"rim_{i}";
            foreach (var col in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            // Face outward from the board, jittered, leaning off vertical in a seeded direction.
            // Rotation goes on BEFORE measuring so the bounds below are the ones actually rendered.
            float outward = -t * Mathf.Rad2Deg + 90f;
            float yaw = outward + Signed(seed, 403 + i * 11) * rim.yawJitterDeg;
            float lean = Signed(seed, 404 + i * 11) * rim.leanDeg;
            float leanAxis = ShardEnvironment.Hash01(seed, 405 + i * 11) * 360f;
            inst.transform.position = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            inst.transform.rotation =
                Quaternion.Euler(0f, yaw, 0f)
                * Quaternion.AngleAxis(lean, Quaternion.Euler(0f, leanAxis, 0f) * Vector3.forward)
                * Quaternion.Euler(def.pitchDeg, 0f, 0f);

            // Measure the kit's own scale, then fit it to targetSize. Two passes on the live
            // instance rather than prefab math: bounds already carry rotation and every child mesh.
            if (!TryBounds(inst, out var raw)) { Object.DestroyImmediate(inst); continue; }
            float longest = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
            if (longest <= 1e-5f) { Object.DestroyImmediate(inst); continue; }
            float want = def.targetSize * (1f + Signed(seed, 406 + i * 11) * def.sizeJitter);
            inst.transform.localScale = Vector3.one * Mathf.Max(1e-4f, want / longest);

            // Seat it: put the measured BASE on the shelf, then bed it in by `sink`. Placing the
            // ORIGIN there instead buries any prop whose pivot is mid-mesh (most of the weapons).
            if (!TryBounds(inst, out var fitted)) { Object.DestroyImmediate(inst); continue; }
            Vector3 foot = center + new Vector3(Mathf.Cos(t) * radius, shelfY, Mathf.Sin(t) * radius);
            inst.transform.position += new Vector3(
                foot.x - fitted.center.x,
                foot.y - fitted.min.y - def.sink,
                foot.z - fitted.center.z);
            block.SetColor("_BaseColor", rim.tint);
            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mr.SetPropertyBlock(block); // tint without cloning the kit's shared materials
            }
        }
    }

    /// <summary>Weighted pick over the kit. Entries with no model or no weight are skipped by the
    /// same rule that built <paramref name="totalWeight"/>, so the roll can't land on one.</summary>
    private static RimPropTune Pick(RimDressTune rim, float totalWeight, float roll)
    {
        float target = roll * totalWeight;
        RimPropTune last = null;
        foreach (var p in rim.props)
        {
            if (p == null || string.IsNullOrEmpty(p.model) || p.weight <= 0f) continue;
            last = p;
            target -= p.weight;
            if (target <= 0f) return p;
        }
        return last; // float slop at the top of the range
    }

    /// <summary>Combined world bounds of every renderer under the instance, or false if it has
    /// none (a kit entry pointing at an empty GameObject shouldn't take a slot in the ring).</summary>
    private static bool TryBounds(GameObject inst, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (any) bounds.Encapsulate(mr.bounds);
            else { bounds = mr.bounds; any = true; }
        }
        return any;
    }

    private static float Signed(uint seed, int i) => ShardEnvironment.Hash01(seed, i) * 2f - 1f;
}
