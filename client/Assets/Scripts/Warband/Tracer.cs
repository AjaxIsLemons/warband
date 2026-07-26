using System;
using UnityEngine;

/// <summary>
/// Pooled one-shot tracer: a thin stretched bar flying start→end as an HDR-emissive streak — the
/// projectile / spell shot of directed-tells. Emissive is Unlit + an HDR base color (motionColor *
/// glow, over bloomThreshold 0.9) — a MaterialPropertyBlock can't toggle _EMISSION, so we don't try.
/// Director-STEPPED (no self-Update): one clock drives play mode and the frozen editor previews alike.
/// </summary>
public class Tracer : MonoBehaviour
{
    private static Material _mat;
    private MaterialPropertyBlock _mpb;
    private Renderer _renderer;
    private Vector3 _start, _end;
    private Color _color;
    private float _glow, _scale, _t, _life;
    private Action<Tracer> _release;

    // Read by the Director when the tracer arrives, to pop the same-color spark at the endpoint.
    public Vector3 End => _end;
    public Color Color => _color;
    public float Glow => _glow;
    public float Scale => _scale;

    public static Tracer Create(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "tracer";
        go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        if (_mat == null)
            _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = _mat;
        var tr = go.AddComponent<Tracer>();
        tr._renderer = r;
        tr._mpb = new MaterialPropertyBlock();
        go.SetActive(false);
        return tr;
    }

    /// <summary>Configure + activate. Endpoints are passed already lifted to flight height.
    /// <paramref name="release"/> returns this to the pool on completion (or a Director reset).</summary>
    public void Play(Vector3 start, Vector3 end, Color color, float glow, float scale, float seconds, Action<Tracer> release)
    {
        _start = start; _end = end; _color = color; _glow = glow; _scale = Mathf.Max(0.05f, scale);
        _life = Mathf.Max(0.02f, seconds); _t = 0f; _release = release;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", color * glow); // HDR intensity beyond 1 is what makes Bloom pick it up
        _renderer.SetPropertyBlock(_mpb);

        var dir = end - start;
        transform.localScale = new Vector3(0.08f, 0.08f, 0.45f) * _scale;
        transform.SetPositionAndRotation(start,
            dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : Quaternion.identity);
        gameObject.SetActive(true);
    }

    /// <summary>Advance one clock step; returns false once arrived (then it's back in the pool).</summary>
    public bool Step(float dt)
    {
        _t += dt;
        float k = Mathf.Clamp01(_t / _life);
        transform.position = Vector3.Lerp(_start, _end, k);
        if (_t >= _life) { _release?.Invoke(this); return false; }
        return true;
    }
}
