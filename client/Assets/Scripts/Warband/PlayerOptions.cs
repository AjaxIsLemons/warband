using UnityEngine;

/// <summary>
/// Player-facing settings (roadmap item 9). One law: every value here is a player override on a
/// seam that already exists — the store never invents behavior. PlayerPrefs is the persistence;
/// <see cref="ApplyAudio"/> pushes the audio values into the mixer, while the two consumers that
/// cannot be pushed read the properties at their own use sites (reduced motion when the shell
/// rebuilds, battle speed when a fight starts or tuning hot-reloads).
///
/// The audio switch drives MasterVol rather than the per-surface enables in tuning.json /
/// HubPresentation.json — those are SHIPPED defaults (and the F1 cockpit's levers), not player
/// state. If the mixer asset is missing, SetBusVolume is a warned no-op and sounds play unrouted
/// at authored volume — degraded the same way SfxPlayer already degrades, never broken.
/// </summary>
internal static class PlayerOptions
{
    // ui.reducedMotion predates this store (the Flow Lab toggle wrote it); keep the key so a
    // dev-toggled preference and a player-toggled one are the same preference.
    private const string KeyReducedMotion = "ui.reducedMotion";
    private const string KeyAudioEnabled = "options.audioEnabled";
    private const string KeyMasterVolume = "options.masterVolume";
    private const string KeyUiVolume = "options.uiVolume";
    private const string KeyBoardVolume = "options.boardVolume";
    private const string KeyBattleSpeed = "options.battleSpeed";

    public const float MinBattleSpeed = 0.5f;
    public const float MaxBattleSpeed = 2f;

    public static bool AudioEnabled
    {
        get => PlayerPrefs.GetInt(KeyAudioEnabled, 1) != 0;
        set { PlayerPrefs.SetInt(KeyAudioEnabled, value ? 1 : 0); ApplyAudio(); }
    }

    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMasterVolume, 1f));
        set { PlayerPrefs.SetFloat(KeyMasterVolume, Mathf.Clamp01(value)); ApplyAudio(); }
    }

    public static float UiVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyUiVolume, 1f));
        set { PlayerPrefs.SetFloat(KeyUiVolume, Mathf.Clamp01(value)); ApplyAudio(); }
    }

    public static float BoardVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyBoardVolume, 1f));
        set { PlayerPrefs.SetFloat(KeyBoardVolume, Mathf.Clamp01(value)); ApplyAudio(); }
    }

    /// <summary>Multiplier over tuning.json's playback.ticksPerSecond, applied where ReplayPlayer
    /// reads it. Presentation pacing only — the resolved fight is the same fight at any speed.</summary>
    public static float BattleSpeed
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyBattleSpeed, 1f), MinBattleSpeed, MaxBattleSpeed);
        set => PlayerPrefs.SetFloat(KeyBattleSpeed, Mathf.Clamp(value, MinBattleSpeed, MaxBattleSpeed));
    }

    public static bool ReducedMotion
    {
        get => PlayerPrefs.GetInt(KeyReducedMotion, 0) != 0;
        set => PlayerPrefs.SetInt(KeyReducedMotion, value ? 1 : 0);
    }

    /// <summary>Push the audio values into the mixer. Master doubles as the mute so one switch
    /// silences both surfaces without touching their own enable seams. Call once at boot (mixer
    /// params reset per session) and after any setter (the setters do it themselves).</summary>
    public static void ApplyAudio()
    {
        SfxPlayer.SetBusVolume("MasterVol", AudioEnabled ? MasterVolume : 0f);
        SfxPlayer.SetBusVolume("UiVol", UiVolume);
        SfxPlayer.SetBusVolume("BoardVol", BoardVolume);
    }
}
