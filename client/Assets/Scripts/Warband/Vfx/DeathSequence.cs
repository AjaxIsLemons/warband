using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One unit's death, from the killing blow to the mark it leaves behind — combat-spectacle §7.1
/// ("ash-death & the board remembers") under the fx-runtime "Death presentation" law.
///
/// The sentence is: hit-freeze (the board holds its breath) → the Death clip, its ActionSpeed
/// fitted so the slump fills FxTune.deathLingerSeconds → a dissolve up the body over the last
/// dissolveSeconds → the corpse is gone and the HEX keeps it: an ash silhouette and, if the mini
/// carried one, its weapon dropped as a grave marker. By the end of a fight the board IS the
/// history of that fight.
///
/// Two entry points, because a death is dispatched before it is struck: <see cref="Begin"/> runs at
/// DISPATCH (the fold already says Dead, so without this the body would blink out for the length of
/// the killing swing) and only holds the corpse visible; <see cref="Trigger"/> runs at the Death
/// tell's IMPACT and starts the sentence. A death whose tell never lands — the row filtered out of
/// tuning.json, or an F1 reload swapping the Director out from under the pending list — self-starts
/// after <see cref="WaitCapSeconds"/> rather than lingering forever.
///
/// Same laws as the rest of the FX runtime: everything advances from <see cref="Step"/> (no Update,
/// no coroutine, no Time.deltaTime), variation hashes off the unit id instead of Random, and every
/// piece of view state it mutates — materials, the animator's speed, the body scale, the chrome, the
/// weapon prop — is restored by <see cref="Abort"/> so a loop-wrap cannot leave ghost corpses.
/// </summary>
internal sealed class DeathSequence
{
    /// <summary>The board's held breath before the slump. Real FX seconds, deliberately short: the
    /// beat is the Death tell's own hit-stop, this only stops the BODY inside it.</summary>
    private const float FreezeSeconds = 0.1f;
    /// <summary>How long a pending death waits for its tell's impact before starting anyway.</summary>
    private const float WaitCapSeconds = 0.75f;
    /// <summary>Primitive fallback: scale-down-and-sink. A death must never read as a silent vanish,
    /// models on or off, so the silhouette path gets its own (shorter, cruder) exit.</summary>
    private const float SinkSeconds = 0.4f;
    /// <summary>Ember → ash-grey on the ground mark. After this the mark is static forever: T0, no
    /// shader clock, no per-frame cost — the board can accumulate one per death without paying.</summary>
    private const float AshCoolSeconds = 2f;
    private const float MarkY = 0.02f;

    private static readonly Color AshEmber = new Color(0.494f, 0.227f, 0.114f); // #7E3A1D, still warm
    private static readonly Color AshCold = new Color(0.150f, 0.145f, 0.140f);  // cold ash
    /// <summary>Burning edge of the dissolve — EmberHot (#FF9E45) at T3 glow. The ONE loud frame of
    /// the whole sequence, and it travels up the body rather than sitting still.</summary>
    private static readonly Color EmberEdge = new Color(3.00f, 1.86f, 0.81f, 1f);

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");
    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly int DeathStateHash = Animator.StringToHash("Death");

    private enum Phase { Waiting, Running, Cooling }

    private ReplayPlayer.UnitView _v;
    private int _uid;
    private Vector3 _hexWorld;      // the death hex, centre — where the mark and the grave land
    private Transform _marks;
    private float _hexSize;
    private Func<TuningData> _tuning;

    private Phase _phase;
    private float _wait, _t, _cool;
    private bool _animStarted, _dissolving;

    // The model path: dissolve the skinned meshes. Empty ⇒ the primitive scale-and-sink instead.
    private readonly List<SkinnedMeshRenderer> _skins = new List<SkinnedMeshRenderer>();
    private readonly List<Material[]> _saved = new List<Material[]>();
    private readonly List<Transform> _hidden = new List<Transform>();
    private MaterialPropertyBlock _mpb;
    private bool _useAnim;

    private Renderer _markR;
    private MaterialPropertyBlock _markMpb;

    /// <summary>The mini's weapon prop, hidden the moment its clone became a grave marker. Handed to
    /// the owner when the sequence ends so a loop-wrap can hand the unit its weapon back — the corpse
    /// itself is gone by then, so the sequence can no longer be the one holding that promise.</summary>
    public Transform HiddenProp { get; private set; }

    /// <summary>Hold this unit's corpse on the board from the moment the fold killed it. The
    /// sentence itself waits for <see cref="Trigger"/>.</summary>
    public static DeathSequence Begin(ReplayPlayer.UnitView v, int unitId, Vector3 hexWorld,
                                      Transform marks, float hexSize, Func<TuningData> tuning)
    {
        var d = new DeathSequence
        {
            _v = v, _uid = unitId, _hexWorld = hexWorld, _marks = marks,
            _hexSize = hexSize, _tuning = tuning, _phase = Phase.Waiting,
        };
        v.Lingering = true;
        v.Walking = false;
        v.MotionOffset = Vector3.zero;
        // The corpse is the story now — bars, nameplate, status roster and team disc all go, or the
        // board reads as "a unit standing at 0 HP" for the length of the linger.
        d.HideChrome();
        // ApplyFold already ran this frame and switched the root off (the fold says Dead). Nothing
        // has rendered between the two calls, so flipping it back here is invisible rather than a pop.
        if (!v.Root.gameObject.activeSelf) v.Root.gameObject.SetActive(true);
        d.CollectSkins();
        // A frozen scrub poses its minis by hand because the editor never ticks an Animator — and it
        // does that BEFORE the events replay, when this unit's root was still switched off. Pose the
        // corpse here or it stands in bind pose until the slump starts.
        if (!Application.isPlaying && v.ModelAnimator != null
            && v.ModelAnimator.runtimeAnimatorController != null)
        {
            v.ModelAnimator.SetBool(WalkingHash, false);
            v.ModelAnimator.Update(0f);
            v.ModelAnimator.Update(0.4f);
        }
        return d;
    }

    /// <summary>The killing blow landed — start the sentence. Idempotent (a Death tell that somehow
    /// fires twice must not restart the slump).</summary>
    public void Trigger()
    {
        if (_phase != Phase.Waiting) return;
        _phase = Phase.Running;
        _t = 0f;
        if (_useAnim) _v.ModelAnimator.speed = 0f; // hit-freeze: the BODY stops, the board does not
    }

    /// <summary>Advance the sequence; false when it is finished and the owner should drop it. The
    /// ash mark it spawned outlives the return — that is the whole point of the mark.</summary>
    public bool Step(float dt)
    {
        switch (_phase)
        {
            case Phase.Waiting:
                _wait += dt;
                if (_wait >= WaitCapSeconds) Trigger();
                return true;

            case Phase.Running:
                _t += dt;
                if (_skins.Count > 0) StepModel(dt, Fx());
                else StepPrimitive();
                return true;

            default:
                _cool += dt;
                ApplyMark(Mathf.Clamp01(_cool / AshCoolSeconds));
                return _cool < AshCoolSeconds;
        }
    }

    /// <summary>Loop-wrap / scenario switch / a scrub to another tick: put everything back the way a
    /// living unit needs it, right now. Every field this class writes is undone here — miss one and
    /// the second loop plays with a half-dissolved body, a frozen animator or a unit with no bars.</summary>
    public void Abort()
    {
        if (_v == null || _v.Root == null) return;
        RestoreBody();
        ShowChrome();
        if (HiddenProp != null) { HiddenProp.gameObject.SetActive(true); HiddenProp = null; }
        _v.Lingering = false;
        _phase = Phase.Cooling;
        _cool = AshCoolSeconds;
    }

    // ---- the model path ------------------------------------------------------

    private void StepModel(float dt, FxTune fx)
    {
        float linger = Mathf.Max(FreezeSeconds + 0.1f, fx.deathLingerSeconds);
        float dissolve = Mathf.Clamp(fx.dissolveSeconds, 0.05f, linger - FreezeSeconds);
        float dissolveAt = linger - dissolve;

        if (!_animStarted && _t >= FreezeSeconds)
        {
            _animStarted = true;
            if (_useAnim)
            {
                var a = _v.ModelAnimator;
                a.speed = 1f;
                // Fit the clip to the linger exactly as a swing is fitted to the sim's gap
                // (render-contract §4): a 1 s slump stretched over a 1.6 s corpse, not a fast slump
                // followed by a second of a body lying still in a pose the eye already finished.
                float len = ClipLength(a, "Death_A");
                float span = Mathf.Max(0.1f, linger - FreezeSeconds);
                a.SetFloat(ActionSpeedHash, len > 0f ? Mathf.Clamp(len / span, 0.2f, 3f) : 1f);
                a.CrossFadeInFixedTime("Death", 0.08f);
            }
        }

        // Edit mode has no animator tick at all — the frozen preview poses its minis by hand for the
        // same reason (BuildLoadedPreview). Without this a probe advanced into the slump would
        // capture a corpse standing in Idle.
        if (_useAnim && _animStarted && !Application.isPlaying) _v.ModelAnimator.Update(dt);

        if (!_dissolving && _t >= dissolveAt) BeginDissolve();
        if (_dissolving) SetCutoff(Mathf.Clamp01((_t - dissolveAt) / dissolve));
        if (_t >= linger) Finish();
    }

    private void CollectSkins()
    {
        _v.Body.GetComponentsInChildren(true, _skins);
        _useAnim = _v.ModelAnimator != null
                   && _v.ModelAnimator.runtimeAnimatorController != null
                   && _v.ModelAnimator.HasState(0, DeathStateHash);
        // No skinned mesh (primitive silhouettes, models.enabled=false) ⇒ the sink path. A model
        // whose controller has no Death state yet still dissolves; it simply slumps in Idle.
    }

    private void BeginDissolve()
    {
        _dissolving = true;
        DropGrave();
        _saved.Clear();
        for (int i = 0; i < _skins.Count; i++)
        {
            var orig = _skins[i].sharedMaterials;
            _saved.Add(orig);
            var swap = new Material[orig.Length];
            for (int j = 0; j < orig.Length; j++) swap[j] = DissolveFor(orig[j]) ?? orig[j];
            _skins[i].sharedMaterials = swap;
        }
    }

    private void SetCutoff(float c)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < _skins.Count; i++)
        {
            if (_skins[i] == null) continue;
            _skins[i].GetPropertyBlock(_mpb);
            _mpb.SetFloat(CutoffId, c);
            _skins[i].SetPropertyBlock(_mpb);
        }
    }

    /// <summary>One dissolve material per source TEXTURE, not per unit: eight bulwarks share one
    /// material and differ only by the MPB cutoff their own sequence writes.</summary>
    private static readonly Dictionary<int, Material> _dissolveMats = new Dictionary<int, Material>();

    private static Material DissolveFor(Material src)
    {
        Texture tex = src != null && src.HasProperty(BaseMapId) ? src.GetTexture(BaseMapId) : null;
        int key = tex != null ? tex.GetInstanceID() : 0;
        if (_dissolveMats.TryGetValue(key, out var m) && m != null) return m;
        var shader = Shader.Find(VfxLibrary.ShaderDissolve);
        if (shader == null) return null;   // shader missing ⇒ the corpse just hides at the end
        m = new Material(shader) { name = "~dissolve", hideFlags = HideFlags.DontSave };
        if (tex != null) m.SetTexture(BaseMapId, tex);
        m.SetTexture(NoiseTexId, VfxLibrary.Noise);
        m.SetColor(EdgeColorId, EmberEdge);
        _dissolveMats[key] = m;
        return m;
    }

    // ---- the primitive path --------------------------------------------------

    /// <summary>Scale down and sink through the board. Both channels go through view state the rest
    /// of the renderer already respects — BodyBaseScale (ApplyVisual multiplies it) and MotionOffset
    /// (Update adds it to the fold's position) — so nothing here fights the per-frame passes.</summary>
    private void StepPrimitive()
    {
        float k = Mathf.Clamp01(_t / SinkSeconds);
        _v.BodyBaseScale = Vector3.one * (1f - 0.85f * k);
        _v.MotionOffset = Vector3.down * (0.9f * k);
        if (_t >= SinkSeconds) Finish();
    }

    // ---- the board remembers -------------------------------------------------

    private void Finish()
    {
        RestoreBody();
        // Chrome comes back while the root is still off: the pieces are switched individually, so a
        // corpse hidden with its bars still disabled would wrap into the next loop with no bars.
        ShowChrome();
        _v.Root.gameObject.SetActive(false);
        _v.Lingering = false;
        SpawnAshMark();
        _phase = Phase.Cooling;
        _cool = 0f;
    }

    private void RestoreBody()
    {
        RestoreMaterials();
        _v.BodyBaseScale = Vector3.one;
        _v.MotionOffset = Vector3.zero;
        if (_v.ModelAnimator == null) return;
        _v.ModelAnimator.speed = 1f;
        // The Death state has no way OUT by design (it parks on the clip's last frame), so the
        // controller has to be walked back to Idle by hand. Miss this and a loop-wrap stands the unit
        // up alive, still lying down: the state survives the root being switched off and on.
        if (_useAnim && _v.Root.gameObject.activeInHierarchy) _v.ModelAnimator.Play("Idle", 0, 0f);
    }

    private void RestoreMaterials()
    {
        if (!_dissolving) return;
        _dissolving = false;
        for (int i = 0; i < _skins.Count && i < _saved.Count; i++)
        {
            if (_skins[i] == null) continue;
            _skins[i].sharedMaterials = _saved[i];
        }
        SetCutoff(0f);   // the Lit material ignores it, but a stale block is a trap for the next pass
        _saved.Clear();
    }

    /// <summary>The ash silhouette: a dark, desaturated ground stain on the death hex, warm at first
    /// and cold within a couple of seconds. T0 forever — it never blooms, never animates again, and
    /// carries no legibility cost, which is what lets every death leave one.</summary>
    private void SpawnAshMark()
    {
        if (_marks == null) return;
        var go = new GameObject($"ashmark_{_uid}");
        go.transform.SetParent(_marks, false);
        go.transform.position = _hexWorld + new Vector3(0f, MarkY, 0f);
        // A square stain, spun by the unit's own hash: a hex-shaped mark would read as another tile
        // overlay, and the rotation is what keeps two deaths on the same hex from stamping identically.
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f)
                              * Quaternion.Euler(0f, 0f, Hash01(_uid, 7) * 360f);
        float span = 2f * _hexSize * 0.72f;
        go.transform.localScale = new Vector3(span, span, span);
        go.AddComponent<MeshFilter>().sharedMesh = VfxLibrary.QuadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = VfxLibrary.MaterialFor(VfxLibrary.ShaderGroundFill, null);
        _markR = mr;
        _markMpb = new MaterialPropertyBlock();
        ApplyMark(0f);
    }

    private void ApplyMark(float cool)
    {
        if (_markR == null) return;
        var c = Color.Lerp(AshEmber, AshCold, cool);
        c.a = Fx().ashMarkAlpha;
        _markMpb.SetColor(ColorId, c);
        // Phase is a scroll offset in the noise: fixed per unit, it picks a DIFFERENT patch of the
        // same texture for every mark, so the stains have different shapes instead of one stamp.
        _markMpb.SetFloat(PhaseId, Hash01(_uid, 11) * 8f);
        _markMpb.SetFloat(IntensityId, 1.1f);
        _markMpb.SetFloat(EdgeFadeId, 0.5f);
        _markMpb.SetTexture(NoiseTexId, VfxLibrary.Noise);
        _markR.SetPropertyBlock(_markMpb);
    }

    /// <summary>Drop the mini's weapon where it fell. The clone (not the original) becomes the grave
    /// marker so a loop-wrap can hand the weapon back to a unit that is alive again; detaching it
    /// from the hand bone is also what strips its animation.</summary>
    private void DropGrave()
    {
        if (_marks == null) return;
        var prop = FindProp(_v.Body);
        if (prop == null) return;   // bare chassis (silhouettes, unkitbashed models) — ash alone

        var clone = UnityEngine.Object.Instantiate(prop.gameObject, prop.parent);
        clone.name = "grave_" + prop.name;
        clone.transform.SetParent(_marks, true);   // built under the bone, so world scale survives
        float angle = Hash01(_uid, 13) * 360f;
        float dist = (0.18f + 0.16f * Hash01(_uid, 17)) * _hexSize;
        var dir = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad));
        clone.transform.position = _hexWorld + dir * dist + new Vector3(0f, 0.06f, 0f);
        clone.transform.rotation = Quaternion.AngleAxis(angle, Vector3.up)
                                 * Quaternion.Euler(Fx().graveTilt, 0f, 0f);

        prop.gameObject.SetActive(false);
        HiddenProp = prop;
    }

    private static Transform FindProp(Transform t)
    {
        if (t.name.StartsWith("prop_", StringComparison.Ordinal)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = FindProp(t.GetChild(i));
            if (hit != null) return hit;
        }
        return null;
    }

    // ---- chrome --------------------------------------------------------------

    /// <summary>Everything on the unit that is UI rather than body — bars, segment ticks, nameplate,
    /// the status row, the team disc. Recorded as it is switched off, so restoring can put back
    /// exactly what this sequence hid and nothing else (the planning marker is already off, and must
    /// stay off).</summary>
    private void HideChrome()
    {
        for (int i = 0; i < _v.Root.childCount; i++)
        {
            var c = _v.Root.GetChild(i);
            if (c == _v.Body || !c.gameObject.activeSelf) continue;
            c.gameObject.SetActive(false);
            _hidden.Add(c);
        }
        var disc = _v.Body.Find("teamdisc");
        if (disc != null && disc.gameObject.activeSelf) { disc.gameObject.SetActive(false); _hidden.Add(disc); }
    }

    private void ShowChrome()
    {
        for (int i = 0; i < _hidden.Count; i++)
            if (_hidden[i] != null) _hidden[i].gameObject.SetActive(true);
        _hidden.Clear();
    }

    // ---- helpers -------------------------------------------------------------

    private FxTune Fx()
    {
        var data = _tuning != null ? _tuning() : null;
        return data != null && data.fx != null ? data.fx : DefaultFx;
    }

    private static readonly FxTune DefaultFx = new FxTune();

    private static float ClipLength(Animator a, string clipName)
    {
        var rc = a.runtimeAnimatorController;
        if (rc == null) return 0f;
        var clips = rc.animationClips;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].name == clipName) return clips[i].length;
        return 0f;
    }

    /// <summary>A stable 0..1 per (unit, channel) — the same no-Random law the numbers and the field
    /// flicker follow, so two captures of one death are the same pixels.</summary>
    private static float Hash01(int id, int salt) =>
        (unchecked((uint)(id * 73856093) ^ (uint)(salt * 19349663)) % 1000u) / 1000f;
}
