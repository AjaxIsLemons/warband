using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Concrete presentation outputs shared by every Hall view. Keeping these behind the existing
/// interfaces means UI motion still works when audio or a platform haptic API is unavailable.
/// </summary>
internal sealed class UiFeedbackServices
{
    public readonly IUiAudioFeedback Audio;
    public readonly IUiHaptics Haptics;

    public UiFeedbackServices(IUiAudioFeedback audio, IUiHaptics haptics)
    {
        Audio = audio ?? new NullUiAudioFeedback();
        Haptics = haptics ?? new NullUiHaptics();
    }
}

/// <summary>
/// Small pooled 2D UI sound player. Authored clips under Resources/UI/SFX replace the compact
/// synthesized fallback automatically; missing art can never make feedback or a transaction fail.
/// </summary>
internal sealed class UiAudioDirector : MonoBehaviour, IUiAudioFeedback
{
    private const int SourceCount = 4;
    private readonly List<AudioSource> _sources = new List<AudioSource>(SourceCount);
    private readonly Dictionary<string, AudioClip[]> _families =
        new Dictionary<string, AudioClip[]>(StringComparer.Ordinal);
    private readonly List<AudioClip> _generated = new List<AudioClip>();
    private HubPresentationConfig _config;
    private AudioSource _ambienceSource;
    private int _nextSource;
    private float _lastPreviewAt = -10f;
    private float _duckUntil = -10f;

    public void Initialize(HubPresentationConfig config)
    {
        _config = config ?? HubPresentationConfig.Load();
        for (int i = 0; i < SourceCount; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            _sources.Add(source);
        }
        _ambienceSource = gameObject.AddComponent<AudioSource>();
        _ambienceSource.playOnAwake = false;
        _ambienceSource.loop = true;
        _ambienceSource.spatialBlend = 0f;
        _ambienceSource.dopplerLevel = 0f;
        _ambienceSource.clip = Resources.Load<AudioClip>("UI/SFX/hall_ambience");
        if (_ambienceSource.clip == null)
        {
            _ambienceSource.clip = SynthesizeAmbience();
            _generated.Add(_ambienceSource.clip);
        }
        RefreshTuning();

        Register("preview", 0.055f, 680f, 980f, 0.26f);
        Register("select", 0.085f, 390f, 760f, 0.38f);
        Register("route", 0.22f, 210f, 620f, 0.34f);
        Register("deal", 0.13f, 520f, 310f, 0.32f);
        Register("purchase", 0.28f, 340f, 820f, 0.46f);
        Register("seat", 0.18f, 170f, 280f, 0.44f);
        Register("bind", 0.40f, 310f, 1040f, 0.42f);
        Register("commit", 0.28f, 260f, 840f, 0.46f);
        Register("major", 0.42f, 150f, 680f, 0.52f);
        Register("error", 0.16f, 185f, 92f, 0.42f);
    }

    public void SetHallActive(bool active)
    {
        if (_ambienceSource == null) return;
        RefreshTuning();
        if (active && _config.audio.enabled)
        {
            if (!_ambienceSource.isPlaying) _ambienceSource.Play();
        }
        else if (_ambienceSource.isPlaying) _ambienceSource.Stop();
    }

    public void RefreshTuning()
    {
        if (_ambienceSource == null || _config?.audio == null) return;
        _ambienceSource.mute = !_config.audio.enabled;
        _ambienceSource.volume = _config.audio.volume * _config.audio.ambienceVolume;
    }

    public void Play(UiFeedbackEvent feedback)
    {
        if (_config?.audio == null || !_config.audio.enabled || _sources.Count == 0) return;

        string family = Family(feedback);
        if (family == "preview")
        {
            float cooldown = _config.audio.hoverCooldownMs / 1000f;
            if (Time.unscaledTime - _lastPreviewAt < cooldown) return;
            _lastPreviewAt = Time.unscaledTime;
        }

        if (!_families.TryGetValue(family, out AudioClip[] clips) || clips.Length == 0) return;
        AudioSource source = _sources[_nextSource++ % _sources.Count];
        source.pitch = 1f + UnityEngine.Random.Range(-_config.audio.pitchVariance,
            _config.audio.pitchVariance);
        source.volume = _config.audio.volume;
        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        source.PlayOneShot(clip);
        if (family != "preview" && family != "select" && family != "route" &&
            family != "deal")
            _duckUntil = Mathf.Max(_duckUntil,
                Time.unscaledTime + Mathf.Min(0.52f, clip.length + 0.08f));
    }

    private void Update()
    {
        if (_ambienceSource == null || _config?.audio == null) return;
        float baseVolume = _config.audio.volume * _config.audio.ambienceVolume;
        float target = Time.unscaledTime < _duckUntil
            ? baseVolume * _config.audio.commitDuck
            : baseVolume;
        _ambienceSource.volume = Mathf.MoveTowards(_ambienceSource.volume, target,
            Time.unscaledDeltaTime * Mathf.Max(0.05f, baseVolume) * 8f);
    }

    private void Register(string family, float duration, float startHz, float endHz, float gain)
    {
        var authored = new List<AudioClip>();
        for (int i = 1; i <= 3; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>($"UI/SFX/{family}_{i}");
            if (clip != null) authored.Add(clip);
        }
        if (authored.Count > 0)
        {
            _families[family] = authored.ToArray();
            return;
        }

        AudioClip fallback = Synthesize("ui-" + family, duration, startHz, endHz, gain);
        _generated.Add(fallback);
        _families[family] = new[] { fallback };
    }

    private static AudioClip Synthesize(string name, float duration, float startHz, float endHz,
                                        float gain)
    {
        const int sampleRate = 22050;
        int count = Mathf.Max(64, Mathf.CeilToInt(duration * sampleRate));
        var samples = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Mathf.Max(1, count - 1);
            float hz = Mathf.Lerp(startHz, endHz, t);
            phase += hz / sampleRate * Mathf.PI * 2f;
            float envelope = Mathf.Pow(1f - t, 2.4f) * Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(t * 18f));
            float body = Mathf.Sin(phase) * 0.72f + Mathf.Sin(phase * 2.01f) * 0.20f +
                         Mathf.Sin(phase * 0.49f) * 0.08f;
            samples[i] = body * envelope * gain;
        }
        AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeAmbience()
    {
        const int sampleRate = 22050;
        const float duration = 4f;
        int count = Mathf.CeilToInt(duration * sampleRate);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float breath = 0.72f + 0.28f * Mathf.Sin(t * Mathf.PI * 0.5f);
            float body = Mathf.Sin(t * Mathf.PI * 2f * 55f) * 0.52f +
                         Mathf.Sin(t * Mathf.PI * 2f * 110f) * 0.20f +
                         Mathf.Sin(t * Mathf.PI * 2f * 220f) * 0.06f;
            samples[i] = body * breath * 0.035f;
        }
        AudioClip clip = AudioClip.Create("ui-hall-ambience", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static string Family(UiFeedbackEvent feedback)
    {
        if (feedback.Transaction == UiTransactionKind.BuyRank ||
            feedback.Transaction == UiTransactionKind.RankChoice ||
            feedback.Transaction == UiTransactionKind.BindInscription)
            return "bind";
        if (feedback.Transaction == UiTransactionKind.Equip) return "seat";
        if (feedback.Transaction == UiTransactionKind.Reforge) return "major";
        if (feedback.Transaction == UiTransactionKind.BuyWeapon ||
            feedback.Transaction == UiTransactionKind.BuyTrinket)
            return "purchase";
        if (feedback.Cue == UiPolishSignals.Cue.Preview) return "preview";
        if (feedback.Cue == UiPolishSignals.Cue.Select ||
            feedback.Cue == UiPolishSignals.Cue.Tab) return "select";
        if (feedback.Cue == UiPolishSignals.Cue.Route) return "route";
        if (feedback.Cue == UiPolishSignals.Cue.Reroll ||
            feedback.Cue == UiPolishSignals.Cue.Reveal) return "deal";
        if (feedback.Cue == UiPolishSignals.Cue.Purchase) return "purchase";
        if (feedback.Cue == UiPolishSignals.Cue.Confirm) return "seat";
        if (feedback.Cue == UiPolishSignals.Cue.RankUp ||
            feedback.Cue == UiPolishSignals.Cue.Reward &&
            feedback.TargetId.IndexOf("hourstone", StringComparison.OrdinalIgnoreCase) >= 0)
            return "bind";
        if (feedback.Cue == UiPolishSignals.Cue.Error) return "error";
        if (feedback.Tone == UiFeedbackTone.Major ||
            feedback.Cue == UiPolishSignals.Cue.Result) return "major";
        return "commit";
    }

    private void OnDestroy()
    {
        foreach (AudioClip clip in _generated)
            if (clip != null) Destroy(clip);
        _generated.Clear();
    }
}

internal sealed class PlatformUiHaptics : IUiHaptics
{
    private readonly HubPresentationConfig _config;

    public PlatformUiHaptics(HubPresentationConfig config)
    {
        _config = config ?? HubPresentationConfig.Load();
    }

    public void Play(UiFeedbackEvent feedback)
    {
        if (_config?.haptics == null || !_config.haptics.enabled) return;
        int duration =
            feedback.Cue == UiPolishSignals.Cue.Error ? _config.haptics.errorMs :
            feedback.Cue == UiPolishSignals.Cue.Select ? _config.haptics.selectMs :
            feedback.Transaction == UiTransactionKind.Reforge ||
            feedback.Transaction == UiTransactionKind.RankChoice ||
            feedback.Cue == UiPolishSignals.Cue.Purchase ||
            feedback.Cue == UiPolishSignals.Cue.Confirm ||
            feedback.Cue == UiPolishSignals.Cue.Reward ? _config.haptics.successMs : 0;
        if (duration <= 0) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(duration, feedback.Cue == UiPolishSignals.Cue.Error ? 150 : 70);
#elif UNITY_IOS && !UNITY_EDITOR
        // Handheld.Vibrate is intentionally reserved for meaningful commits on the no-plugin
        // fallback. It is too coarse for hover/focus or every ordinary selection.
        if (feedback.Cue != UiPolishSignals.Cue.Select) Handheld.Vibrate();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void VibrateAndroid(long milliseconds, int amplitude)
    {
        try
        {
            using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
            using AndroidJavaObject vibrator =
                activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            int sdk = version.GetStatic<int>("SDK_INT");
            if (sdk >= 26)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255));
                vibrator.Call("vibrate", effect);
            }
            else vibrator.Call("vibrate", milliseconds);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UI Haptics] Android vibration unavailable. {ex.Message}");
        }
    }
#endif
}
