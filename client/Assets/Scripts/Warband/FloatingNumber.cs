using System;
using UnityEngine;

/// <summary>
/// Pooled world-space combat number. Rises, pops on spawn, then fades — self-animating, returns
/// itself to the pool on complete. Legacy TextMesh keeps it dependency-free (no TMP essentials
/// import). Size/timing come from the tuning config. Swap to TextMeshPro later for outlines.
/// </summary>
[RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
public class FloatingNumber : MonoBehaviour
{
    private TextMesh _text;
    private Quaternion _face;
    private Vector3 _vel;
    private float _t, _life, _baseSize, _gravity;
    private Color _color;
    private Action<FloatingNumber> _release;

    public static FloatingNumber Create(Transform parent, Font font)
    {
        var go = new GameObject("number");
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        var fn = go.AddComponent<FloatingNumber>();
        fn._text = tm;
        go.SetActive(false);
        return fn;
    }

    /// <summary>Size is applied per-play, not at Create: these are pooled, so baking it at
    /// construction froze recycled numbers at whatever the tuning was when they were first made.
    /// <paramref name="velocity"/> is the FULL launch vector (column splay + rise) and
    /// <paramref name="gravity"/> the downward decel — the caller owns the trajectory. This used to
    /// jitter x by Random, which both read as noise and made frozen RenderShots captures
    /// irreproducible; placement is now scheduled upstream, so the same tick renders the same way.</summary>
    public void Play(Vector3 pos, Vector3 velocity, float gravity, string s, Color color, float scale,
                     Quaternion face, float life, float characterSize, int fontSize, Action<FloatingNumber> release)
    {
        _release = release; _face = face; _color = color; _baseSize = scale; _gravity = gravity; _life = life;
        _t = 0f;
        _vel = velocity;
        _text.characterSize = characterSize;
        _text.fontSize = fontSize;
        _text.text = s;
        _text.color = color;
        transform.SetPositionAndRotation(pos, face);
        transform.localScale = Vector3.one * scale;
        gameObject.SetActive(true);
    }

    /// <summary>Advance one clock step; returns false once expired (then it's back in the pool).
    /// Director-STEPPED like Tracer/Burst — this used to self-Update, which froze numbers in
    /// edit-mode previews and so made the RenderShots contact sheet unable to show combat text in
    /// flight. One clock now drives play mode and the frozen captures alike.</summary>
    public bool Step(float dt)
    {
        _t += dt;
        transform.position += _vel * dt;
        _vel.y -= _gravity * dt;
        transform.rotation = _face;

        float k = _t / _life;
        float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f);
        transform.localScale = Vector3.one * (_baseSize * pop);
        _color.a = 1f - Mathf.Clamp01((k - 0.45f) / 0.55f);
        _text.color = _color;

        if (_t >= _life) { _release?.Invoke(this); return false; }
        return true;
    }
}
