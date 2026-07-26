using System;
using UnityEngine;

/// <summary>
/// Pooled one-shot burst: a sphere that pops up then collapses, cooling toward black over the back
/// half (reads as energy dissipating — no alpha juggling on the opaque Unlit material). The death
/// poof, and every tracer's arrival spark. Same Unlit + HDR-emissive trick as Tracer; Director-STEPPED,
/// so one clock drives play mode and the frozen editor previews alike.
/// </summary>
public class Burst : MonoBehaviour
{
    private static Material _mat;
    private MaterialPropertyBlock _mpb;
    private Renderer _renderer;
    private Color _color;
    private float _glow, _size, _t, _life;
    private Action<Burst> _release;

    public static Burst Create(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "burst";
        go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        if (_mat == null)
            _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = _mat;
        var b = go.AddComponent<Burst>();
        b._renderer = r;
        b._mpb = new MaterialPropertyBlock();
        go.SetActive(false);
        return b;
    }

    /// <summary><paramref name="size"/> is the full-pop world diameter; <paramref name="release"/>
    /// returns this to the pool on completion (or a Director reset).</summary>
    public void Play(Vector3 pos, Color color, float glow, float size, float seconds, Action<Burst> release)
    {
        _color = color; _glow = glow; _size = Mathf.Max(0.01f, size);
        _life = Mathf.Max(0.02f, seconds); _t = 0f; _release = release;
        transform.position = pos;
        transform.localScale = Vector3.one * (_size * 0.2f);
        Emit(1f);
        gameObject.SetActive(true);
    }

    /// <summary>Advance one clock step; returns false once collapsed (then it's back in the pool).</summary>
    public bool Step(float dt)
    {
        _t += dt;
        float k = Mathf.Clamp01(_t / _life);
        // Pop up (0.2→1) over the first 35%, then collapse to zero.
        float s = k < 0.35f ? Mathf.Lerp(0.2f, 1f, k / 0.35f) : Mathf.Lerp(1f, 0f, (k - 0.35f) / 0.65f);
        transform.localScale = Vector3.one * (_size * s);
        // Cool toward black over the back half so it reads as fading, not popping out.
        float cool = k < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.5f) / 0.5f);
        Emit(cool);
        if (_t >= _life) { _release?.Invoke(this); return false; }
        return true;
    }

    private void Emit(float cool)
    {
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", _color * (_glow * cool)); // HDR intensity is what makes Bloom bite
        _renderer.SetPropertyBlock(_mpb);
    }
}
