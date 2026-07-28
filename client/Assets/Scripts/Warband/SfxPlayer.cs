using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Which bus a sound rides, and how it ranks when voices run out. See `Design/audio.md` §5.0/§5.3.
/// The ordering in <see cref="SfxPlayer.Priority"/> is the whole point: a death sting must never
/// lose to four Burn ticks, which is exactly what today's single-`AudioSource` path does — past 32
/// real voices Unity culls by AUDIBILITY, so the loudest noise wins rather than the most important
/// event.
/// </summary>
internal enum SfxBus { Ui, Decisive, Cast, Impact, State }

/// <summary>
/// Pooled, bus-routed, priority-limited one-shot player — the single substrate under both audio
/// systems (`Design/audio.md` §5.0). UI and board differ in POLICY, not plumbing: they compete for
/// the same 32 real voices and the same ears, and a UI commit has to be able to duck the board.
///
/// Ported down from Shoota's `SfxPlayer` (pool, category→group routing, priority stealing,
/// same-clip cap, duck envelope) with everything FPS-specific dropped — no 3D, no distance
/// low-pass, no occlusion raycasts, no ally/enemy split. warband's board is a diorama on a fixed
/// camera; every sound is 2D.
///
/// Degrades on every axis, because audio must never be able to break a transaction or a replay:
/// no mixer asset → sounds still play, unrouted, one warning · missing clip → silent no-op, warned
/// once (the property that lets tell rows name clips before they exist) · batch mode → allocates
/// nothing at all.
///
/// Presentation only. Nothing here touches sim state or consumes sim RNG, so a replay sounds
/// different at different volumes and simulates identically.
/// </summary>
internal static class SfxPlayer
{
    // 21 is the sum of the per-bus caps below; the spare 3 absorb a burst arriving in one frame
    // before the sweep reclaims finished voices.
    private const int MaxVoices = 24;

    // Same-id coalescing: five simultaneous sword hits should be ONE LOUDER sword, not five voices.
    // At 5 tps a tick is 200 ms, so this window collapses a whole tick's worth of identical impacts.
    private const float SameIdWindow = 0.07f;
    private const int SameIdMax = 2;
    private const float SameIdBoost = 1.25f;

    private const string MixerPath = "Audio/GameMixer";

    private sealed class Voice
    {
        public AudioSource Src;
        public string Id;
        public SfxBus Bus;
        public float StartTime;   // unscaled: the windows here are about ears, not sim ticks
        public bool Active;
    }

    private static Voice[] _voices;
    private static bool _poolReady;
    private static Transform _root;
    private static readonly Dictionary<string, AudioClip[]> _cache =
        new Dictionary<string, AudioClip[]>(StringComparer.Ordinal);
    private static readonly HashSet<string> _missingWarned = new HashSet<string>(StringComparer.Ordinal);

    // There is deliberately NO global `Muted` flag here. The two surfaces have separate enable
    // switches — `HubPresentation.json → audio.enabled` for the Hall, `tuning.json → audio.enabled`
    // (F1-hot-reloadable) for the board — and funnelling both into one shared static made the board
    // depend on the Hall having initialised first: in a fight scene with no Hall, nothing would ever
    // have written the flag and the board would have been silent forever with no clue why.
    // Each caller gates itself; this type just plays what it is asked to.

    /// <summary>Master gain applied on top of the mixer, from `audio.volume`.</summary>
    internal static float Volume { get; set; } = 1f;

    /// <summary>Random pitch spread per shot, from `audio.pitchVariance`. 0 = off.</summary>
    internal static float PitchVariance { get; set; }

    /// <summary>Sounds dropped because every voice was equal-or-higher priority. Debug telemetry —
    /// a healthy mix keeps this near zero, which makes it a MEASURABLE design target rather than a
    /// vibe (`Design/audio.md` §5.3).</summary>
    internal static int DroppedVoices { get; private set; }

    // --- buses ---------------------------------------------------------------------------------

    // Decisive is never stolen and never ducked. State is cheapest and goes first. UI outranks the
    // board because it is direct feedback to something the player just did.
    private static int Priority(SfxBus b) => b switch
    {
        SfxBus.Decisive => 4,
        SfxBus.Ui => 3,
        SfxBus.Cast => 2,
        SfxBus.Impact => 1,
        _ => 0,
    };

    // Per-bus concurrency caps (§5.3). A bus at its cap steals from ITSELF, so one loud class can
    // never crowd out another — the cap is the mix decision, the priority ladder is only the
    // tie-breaker for the shared pool.
    private static int Cap(SfxBus b) => b switch
    {
        SfxBus.Decisive => 4,
        SfxBus.Ui => 4,
        SfxBus.Cast => 4,
        SfxBus.Impact => 6,
        _ => 3,
    };

    private static string GroupName(SfxBus b) => b switch
    {
        SfxBus.Ui => "UI",
        SfxBus.Decisive => "Decisive",
        SfxBus.Cast => "Cast",
        SfxBus.Impact => "Impact",
        _ => "State",
    };

    // Board clips live under Resources/Board/SFX, UI cues under Resources/UI/SFX.
    private static string Folder(SfxBus b) => b == SfxBus.Ui ? "UI/SFX/" : "Board/SFX/";

    // --- mixer ---------------------------------------------------------------------------------

    private static AudioMixer _mixer;
    private static bool _mixerLoaded, _mixerWarned;
    private static readonly Dictionary<SfxBus, AudioMixerGroup> _groups =
        new Dictionary<SfxBus, AudioMixerGroup>();

    private static AudioMixer Mixer()
    {
        if (_mixerLoaded) return _mixer;
        _mixerLoaded = true;
        _mixer = Resources.Load<AudioMixer>(MixerPath);
        if (_mixer == null && !_mixerWarned)
        {
            _mixerWarned = true;
            Debug.LogWarning($"[SfxPlayer] No AudioMixer at Resources/{MixerPath} — sounds play " +
                             "unrouted (no buses, no duck, no volume sliders). " +
                             "Run Warband → Audio → Create Game Mixer.");
        }
        return _mixer;
    }

    private static AudioMixerGroup GroupFor(SfxBus bus)
    {
        if (_groups.TryGetValue(bus, out AudioMixerGroup g)) return g;
        AudioMixer m = Mixer();
        AudioMixerGroup[] found = m != null ? m.FindMatchingGroups(GroupName(bus)) : null;
        g = found != null && found.Length > 0 ? found[0] : null;
        _groups[bus] = g;
        return g;
    }

    /// <summary>Set an exposed volume param (MasterVol / UiVol / BoardVol) from 0..1, in dB.
    /// This is what roadmap item 9's options sliders drive.</summary>
    internal static void SetBusVolume(string exposedParam, float linear01)
    {
        AudioMixer m = Mixer();
        if (m == null) return;
        m.SetFloat(exposedParam, linear01 <= 0.0001f ? -80f : Mathf.Log10(linear01) * 20f);
    }

    internal static void SetDuckDb(float db)
    {
        AudioMixer m = Mixer();
        if (m != null) m.SetFloat("BoardDuck", db);
    }

    // --- play ----------------------------------------------------------------------------------

    /// <summary>Fire a one-shot. Empty or missing id is a silent no-op — authoring may lead audio.
    /// <paramref name="volume"/> is the caller's own emphasis, multiplied by <see cref="Volume"/>.</summary>
    internal static void Play(string id, SfxBus bus, float volume = 1f)
    {
        if (string.IsNullOrEmpty(id) || Application.isBatchMode) return;
        AudioClip[] variants = Load(id, bus);
        if (variants == null || variants.Length == 0) return;

        Voice v = Acquire(id, bus);
        if (v == null) return;  // coalesced into a louder instance, or dropped

        AudioSource src = v.Src;
        src.clip = variants.Length == 1
            ? variants[0]
            : variants[UnityEngine.Random.Range(0, variants.Length)];
        src.outputAudioMixerGroup = GroupFor(bus);
        src.pitch = PitchVariance > 0f
            ? 1f + UnityEngine.Random.Range(-PitchVariance, PitchVariance)
            : 1f;
        src.volume = Mathf.Clamp01(volume * Volume);

        v.Id = id;
        v.Bus = bus;
        v.StartTime = Time.unscaledTime;
        v.Active = true;
        src.Play();
    }

    private static Voice Acquire(string id, SfxBus bus)
    {
        EnsurePool();
        if (_voices == null) return null;

        float now = Time.unscaledTime;
        int prio = Priority(bus);

        // 1. Same-id coalescing — "bigger, not more". Past the cap, lift the newest instance
        //    instead of spending another voice on a sound already in the air.
        int same = 0, inBus = 0;
        Voice newestSame = null, oldestInBus = null;
        for (int i = 0; i < _voices.Length; i++)
        {
            Voice v = _voices[i];
            // `Active` is only cleared by the next frame's sweep, so a voice whose clip has already
            // finished still carries the flag. Counting those would steal from a bus that is not
            // actually full and coalesce against a sound nobody can hear any more — ask the source.
            if (!v.Active || !v.Src.isPlaying) continue;
            if (v.Bus == bus)
            {
                inBus++;
                if (oldestInBus == null || v.StartTime < oldestInBus.StartTime) oldestInBus = v;
            }
            if (v.Id == id && now - v.StartTime < SameIdWindow)
            {
                same++;
                if (newestSame == null || v.StartTime > newestSame.StartTime) newestSame = v;
            }
        }
        if (same >= SameIdMax)
        {
            if (newestSame != null)
                newestSame.Src.volume = Mathf.Clamp01(newestSame.Src.volume * SameIdBoost);
            return null;
        }

        // 2. Bus already at its cap: steal from ITSELF, oldest first. Keeps one class from
        //    crowding out another no matter how dense it gets.
        if (inBus >= Cap(bus))
        {
            if (oldestInBus == null) return null;
            oldestInBus.Src.Stop();
            return oldestInBus;
        }

        // 3. A genuinely free voice.
        for (int i = 0; i < _voices.Length; i++)
            if (!_voices[i].Active || !_voices[i].Src.isPlaying)
                return _voices[i];

        // 4. Pool full: steal the oldest voice of the lowest priority <= ours. Decisive outranks
        //    everything, so it can never be stolen by an impact.
        Voice victim = null;
        for (int i = 0; i < _voices.Length; i++)
        {
            Voice v = _voices[i];
            if (Priority(v.Bus) > prio) continue;
            if (victim == null || Priority(v.Bus) < Priority(victim.Bus)
                || (Priority(v.Bus) == Priority(victim.Bus) && v.StartTime < victim.StartTime))
                victim = v;
        }
        if (victim != null)
        {
            victim.Src.Stop();
            return victim;
        }
        DroppedVoices++;
        return null;
    }

    /// <summary>Release voices whose clip finished. Driven per frame by <see cref="SfxDriver"/> —
    /// replaces per-sound GameObject churn entirely.</summary>
    internal static void SweepVoices()
    {
        if (_voices == null) return;
        for (int i = 0; i < _voices.Length; i++)
        {
            Voice v = _voices[i];
            if (v.Active && !v.Src.isPlaying)
            {
                v.Active = false;
                v.Id = null;
            }
        }
    }

    private static void EnsurePool()
    {
        if (_poolReady) return;
        _poolReady = true;
        if (Application.isBatchMode) return;
        Transform root = Root();
        _voices = new Voice[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(root, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;   // 2D: fixed diorama camera, no positional audio
            src.dopplerLevel = 0f;
            _voices[i] = new Voice { Src = src };
        }
    }

    private static Transform Root()
    {
        if (_root == null)
        {
            var go = new GameObject("~SfxPlayer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SfxDriver>();
            _root = go.transform;
        }
        return _root;
    }

    /// <summary>Resolve `{id}_1..n` variants, else the bare `{id}`. Missing ids warn once and cache
    /// as empty so a hot path can't spam the console every tick.</summary>
    private static AudioClip[] Load(string id, SfxBus bus)
    {
        string key = Folder(bus) + id;
        if (_cache.TryGetValue(key, out AudioClip[] hit)) return hit;

        var found = new List<AudioClip>();
        for (int i = 1; i <= 4; i++)
        {
            AudioClip c = Resources.Load<AudioClip>(key + "_" + i);
            if (c == null) break;
            found.Add(c);
        }
        if (found.Count == 0)
        {
            AudioClip single = Resources.Load<AudioClip>(key);
            if (single != null) found.Add(single);
        }
        if (found.Count == 0 && _missingWarned.Add(key))
            Debug.LogWarning($"[SfxPlayer] No clip for '{id}' (Resources/{key}). Silent no-op.");

        AudioClip[] arr = found.ToArray();
        _cache[key] = arr;
        return arr;
    }

    /// <summary>Editor/dev: drop cached clips so a re-baked WAV is picked up without a domain
    /// reload (Warband → Audio → Refresh SFX).</summary>
    internal static void ClearCache()
    {
        _cache.Clear();
        _missingWarned.Clear();
        _groups.Clear();
        _mixerLoaded = false;
    }
}

/// <summary>
/// Sidechain-style duck on the board's `Ducked` sub-bus (exposed param "BoardDuck"). `Decisive`
/// is a SIBLING of that bus, not a child, so death/crit ride over the duck untouched — the
/// "what steps back when density rises" half of Riot's adaptive-mix test (`Design/audio.md` §5.3).
/// </summary>
internal static class SfxDucker
{
    private const float AttackSeconds = 0.03f;   // fast grab so the triggering sound punches through

    private enum Phase { Idle, Attack, Hold, Release }
    private static Phase _phase = Phase.Idle;
    private static float _current;   // current attenuation, dB <= 0
    private static float _depth, _hold, _release, _holdLeft;

    internal static float CurrentDb => _current;

    /// <summary>Duck by <paramref name="depthDb"/>, hold, then release. Re-arms if already ducking.</summary>
    internal static void Duck(float depthDb, float holdSeconds, float releaseSeconds)
    {
        if (Application.isBatchMode) return;
        _depth = Mathf.Abs(depthDb);
        _hold = Mathf.Max(0f, holdSeconds);
        _release = Mathf.Max(0.01f, releaseSeconds);
        _phase = Phase.Attack;
    }

    internal static void Tick(float dt)
    {
        switch (_phase)
        {
            case Phase.Idle:
                return;
            case Phase.Attack:
                _current = Mathf.MoveTowards(_current, -_depth, _depth / AttackSeconds * dt);
                if (Mathf.Approximately(_current, -_depth))
                {
                    _phase = Phase.Hold;
                    _holdLeft = _hold;
                }
                break;
            case Phase.Hold:
                _holdLeft -= dt;
                if (_holdLeft <= 0f) _phase = Phase.Release;
                break;
            case Phase.Release:
                _current = Mathf.MoveTowards(_current, 0f, _depth / _release * dt);
                if (_current >= 0f)
                {
                    _current = 0f;
                    _phase = Phase.Idle;
                }
                break;
        }
        SfxPlayer.SetDuckDb(_current);
    }
}

/// <summary>Lives on the persistent `~SfxPlayer` root. Sweeps finished voices and advances the duck
/// envelope — the only per-frame audio cost in the game.</summary>
internal sealed class SfxDriver : MonoBehaviour
{
    private void Update()
    {
        SfxPlayer.SweepVoices();
        SfxDucker.Tick(Time.unscaledDeltaTime);
    }
}
