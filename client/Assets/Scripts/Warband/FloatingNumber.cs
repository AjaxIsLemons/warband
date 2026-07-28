using System;
using UnityEngine;

/// <summary>
/// Pooled world-space combat number. Spawns at final size (P7 motion law — only a CRIT pops),
/// rises, luminance-decays toward dark over its back half (Hades' white→black law, Jake's
/// 2026-07-28 luminance-over-alpha call), and alpha-fades only in the last quarter — brightness
/// says "spent", alpha says "gone". Returns itself to the pool on complete. Legacy TextMesh keeps
/// it dependency-free (no TMP essentials import). Size/timing come from the tuning config. Swap
/// to TextMeshPro later for outlines (ui-review P2).
/// </summary>
[RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
public class FloatingNumber : MonoBehaviour
{
    private TextMesh _text;
    private Quaternion _face;
    private Vector3 _vel;
    private float _t, _life, _baseSize, _gravity, _pop, _endLum;
    private Color _color;
    private Action<FloatingNumber> _release;

    // Crit pop window in ABSOLUTE seconds (WoW's shipped band: overshoot ~50–60 ms, settled by
    // ~200 ms) — a long-lived heavy crit must not stretch its pop with its lifetime.
    private const float PopIn = 0.06f, PopSettle = 0.2f;

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
                     Quaternion face, float life, float characterSize, int fontSize,
                     float popAmount, float endLuminance, Action<FloatingNumber> release)
    {
        _release = release; _face = face; _color = color; _baseSize = scale; _gravity = gravity; _life = life;
        _pop = popAmount; _endLum = endLuminance;
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
        // P7 motion law (research round 2 §6): no spawn overshoot at warband's number density —
        // Hades, the density-matched reference, spawns at final size. Only a crit pops.
        float pop = 1f;
        if (_pop > 0f)
            pop = _t < PopIn ? Mathf.Lerp(1f, 1f + _pop, _t / PopIn)
                : _t < PopSettle ? Mathf.Lerp(1f + _pop, 1f, (_t - PopIn) / (PopSettle - PopIn))
                : 1f;
        transform.localScale = Vector3.one * (_baseSize * pop);

        // Luminance decay over the back half, alpha only in the last quarter — spent numbers
        // darken out of the frame's attention instead of ghosting translucent over it.
        float lum = k < 0.5f ? 1f : Mathf.Lerp(1f, _endLum, (k - 0.5f) * 2f);
        var c = _color;
        c.r *= lum; c.g *= lum; c.b *= lum;
        c.a = 1f - Mathf.Clamp01((k - 0.75f) / 0.25f);
        _text.color = c;

        if (_t >= _life) { _release?.Invoke(this); return false; }
        return true;
    }
}
