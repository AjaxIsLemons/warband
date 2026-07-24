using System;
using System.Collections.Generic;
using UnityEngine;
using Warband.Sim;

public enum FeedbackSide { Source, Target }

/// <summary>
/// The full presentation tuning surface. Plain [Serializable] data so it BOTH draws in the
/// Inspector (the human's surface) AND round-trips through Newtonsoft JSON (the source of
/// truth in StreamingAssets/tuning.json, editable by hand or by an agent). See render-polish.md.
/// </summary>
[Serializable]
public class TuningData
{
    public CameraTune camera = new CameraTune();
    public PostTune post = new PostTune();
    public NumberTune numbers = new NumberTune();
    public List<TellDef> tells = new List<TellDef>();
}

[Serializable]
public class CameraTune
{
    [Range(-180f, 180f)] public float yaw = 0f;       // orbit around the board
    [Range(5f, 89f)] public float pitch = 52f;        // elevation angle
    [Range(0.4f, 3f)] public float distance = 1.25f;  // multiple of board span
    public Color background = new Color(0.055f, 0.06f, 0.08f);
}

[Serializable]
public class PostTune
{
    [Range(0f, 3f)] public float bloomIntensity = 0.7f;
    [Range(0f, 2f)] public float bloomThreshold = 0.9f;
    [Range(0f, 1f)] public float vignette = 0.30f;
    [Range(-60f, 60f)] public float saturation = 14f;
    public float dofStart = 16f;
    public float dofEnd = 44f;
}

[Serializable]
public class NumberTune
{
    [Range(0.01f, 0.25f)] public float characterSize = 0.06f; // global readability base
    public int fontSize = 72;
    [Range(0.5f, 4f)] public float riseSpeed = 2.4f;
    [Range(0.2f, 2f)] public float lifeSeconds = 0.8f;
}

[Serializable]
public class TellDef
{
    public EventKind eventKind = EventKind.DamageDealt;
    public FeedbackSide side = FeedbackSide.Target;

    // Optional signature filters. A tell fires only on events that match, and the MOST specific
    // matching tell wins — so a filterless "DamageDealt" is the fallback and a "cause: Burn" tell
    // overrides it for burn ticks. Matching/precedence live in Warband.Sim.TellMatch (tested).
    public bool byCause = false;
    public Cause cause = Cause.Attack;
    public bool byStatus = false;         // for StatusApplied/StatusExpired
    public StatusKind status = StatusKind.Burn;

    [Newtonsoft.Json.JsonIgnore] public Cause? CauseFilter => byCause ? cause : (Cause?)null;
    [Newtonsoft.Json.JsonIgnore] public StatusKind? StatusFilter => byStatus ? status : (StatusKind?)null;
    [Newtonsoft.Json.JsonIgnore] public int Specificity => TellMatch.Specificity(CauseFilter, StatusFilter);

    public bool flash = true;
    public Color flashColor = Color.white;
    public Color critFlashColor = new Color(1f, 0.85f, 0.25f);
    [Min(0.01f)] public float flashSeconds = 0.2f;

    public bool punch = false;
    [Range(0f, 1f)] public float punchAmount = 0.25f;
    [Min(0.01f)] public float punchSeconds = 0.18f;

    public bool number = false;
    public Color numberColor = new Color(1f, 0.5f, 0.4f);
    public Color critNumberColor = new Color(1f, 0.85f, 0.25f);
    public int minAmount = 1;
    [Range(0.2f, 3f)] public float numberScale = 1f; // relative to the global character size
}
