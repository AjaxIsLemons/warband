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
/// Cue → family adapter for the Hall's UI audio. All playback, pooling, bus routing and limiting
/// live in <see cref="SfxPlayer"/> (`Design/audio.md` §5.0) — this type only decides WHICH of the
/// six families a semantic feedback event belongs to, and pushes tuning changes through.
///
/// Two laws are enforced here rather than in the mixer (§5.1):
///
/// 1. <b>Clicks only.</b> Hover, focus, tooltip and drag-projection are SILENT. They used to fire
///    the `preview` family behind a 45 ms cooldown; Jake cut that (2026-07-27) and the cooldown
///    went with it, because there is nothing left to rate-limit.
/// 2. <b>Sound is opt-in for cues, automatic for transactions.</b> An unmapped <i>cue</i> is
///    silent, so a new ambient signal can never start clicking by default. An unmapped
///    <i>transaction</i> falls back to `commit`, because a transaction is by definition something
///    the player just committed to and it should always answer.
///
/// The synthesized fallbacks are gone. They existed so the Hall made noise before clips were
/// authored; the clips exist now, and a missing one is a silent no-op inside `SfxPlayer` anyway.
/// </summary>
internal sealed class UiAudioDirector : MonoBehaviour, IUiAudioFeedback
{
    // A UI `major` (result, reward, rank-up) ducks the board so the payoff lands clean. Depth and
    // timing per §5.3; the board bus is what steps back, and `Decisive` sits outside it.
    private const float MajorDuckDb = 4f;
    private const float MajorDuckHold = 0.15f;
    private const float MajorDuckRelease = 0.25f;

    private HubPresentationConfig _config;

    public void Initialize(HubPresentationConfig config)
    {
        _config = config ?? HubPresentationConfig.Load();
        RefreshTuning();
    }

    /// <summary>Push `HubPresentation.json → audio` into the shared player. Called on load and on
    /// every hot-reload, so the F1 cockpit retunes audio live like everything else.</summary>
    public void RefreshTuning()
    {
        UiAudioTuning a = _config?.audio;
        if (a == null) return;
        SfxPlayer.Volume = a.volume;
        SfxPlayer.PitchVariance = a.pitchVariance;
    }

    public void Play(UiFeedbackEvent feedback)
    {
        // This surface owns its own switch — see the note in SfxPlayer on why there is no shared
        // global mute.
        if (_config?.audio == null || !_config.audio.enabled) return;
        string family = Family(feedback);
        if (family == null) return;   // silent by law — hover, tooltips, drag projection
        SfxPlayer.Play(family, SfxBus.Ui);
        if (family == "major")
            SfxDucker.Duck(MajorDuckDb, MajorDuckHold, MajorDuckRelease);
    }

    /// <summary>Six families (§5.1.5). Returns null for "make no sound".</summary>
    private static string Family(UiFeedbackEvent feedback)
    {
        // --- transactions: always a commit of some kind, so always audible ---
        switch (feedback.Transaction)
        {
            case UiTransactionKind.BuyRank:
            case UiTransactionKind.RankChoice:
            case UiTransactionKind.BindInscription:
                return "bind";
            case UiTransactionKind.Reforge:
                return "major";
            case UiTransactionKind.MusterSelect:
            case UiTransactionKind.MusterDeselect:
                return "tick";
            case UiTransactionKind.None:
                break;
            default:
                // BuyRecruit, BuyWeapon, BuyTrinket, BuyCapacity, Equip — and anything added later.
                return "commit";
        }

        // --- cues: opt-in, so an unmapped one stays silent ---
        switch (feedback.Cue)
        {
            // Clicks-only law: these are hover/preview/projection, not actions.
            case UiPolishSignals.Cue.Preview:
            case UiPolishSignals.Cue.TooltipReveal:
            case UiPolishSignals.Cue.TooltipDismiss:
            case UiPolishSignals.Cue.ProjectedTarget:
            case UiPolishSignals.Cue.Attention:
                return null;

            case UiPolishSignals.Cue.Select:
            case UiPolishSignals.Cue.Tab:
            case UiPolishSignals.Cue.Pin:
            case UiPolishSignals.Cue.Unpin:
            case UiPolishSignals.Cue.SocketWake:
                return "tick";

            case UiPolishSignals.Cue.Reroll:
            case UiPolishSignals.Cue.Reveal:
            case UiPolishSignals.Cue.DrawerExpand:
            case UiPolishSignals.Cue.DrawerCollapse:
                return "deal";

            case UiPolishSignals.Cue.Confirm:
            case UiPolishSignals.Cue.Purchase:
            case UiPolishSignals.Cue.Route:
                return "commit";

            case UiPolishSignals.Cue.Error:
                return "error";

            case UiPolishSignals.Cue.RankUp:
            case UiPolishSignals.Cue.Result:
                return "major";

            // An Hourstone reward is a Bind in everything but name; other rewards are a result.
            case UiPolishSignals.Cue.Reward:
                return feedback.TargetId != null &&
                       feedback.TargetId.IndexOf("hourstone", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "bind"
                    : "major";
        }

        // A Major tone on an otherwise-unmapped cue still deserves the ceremony.
        return feedback.Tone == UiFeedbackTone.Major ? "major" : null;
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
