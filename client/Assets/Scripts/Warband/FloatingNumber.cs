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
    private float _t, _life, _baseSize, _rise;
    private Color _color;
    private Action<FloatingNumber> _release;

    public static FloatingNumber Create(Transform parent, Font font, float characterSize, int fontSize)
    {
        var go = new GameObject("number");
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        var fn = go.AddComponent<FloatingNumber>();
        fn._text = tm;
        go.SetActive(false);
        return fn;
    }

    public void Play(Vector3 pos, string s, Color color, float scale, Quaternion face, float rise, float life, Action<FloatingNumber> release)
    {
        _release = release; _face = face; _color = color; _baseSize = scale; _rise = rise; _life = life;
        _t = 0f;
        _vel = new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), rise, 0f);
        _text.text = s;
        _text.color = color;
        transform.SetPositionAndRotation(pos, face);
        transform.localScale = Vector3.one * scale;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!Application.isPlaying) return; // frozen in edit-mode previews
        _t += Time.deltaTime;
        transform.position += _vel * Time.deltaTime;
        _vel.y -= _rise * 1.5f * Time.deltaTime;
        transform.rotation = _face;

        float k = _t / _life;
        float pop = k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f);
        transform.localScale = Vector3.one * (_baseSize * pop);
        _color.a = 1f - Mathf.Clamp01((k - 0.45f) / 0.55f);
        _text.color = _color;

        if (_t >= _life) { gameObject.SetActive(false); _release?.Invoke(this); }
    }
}
