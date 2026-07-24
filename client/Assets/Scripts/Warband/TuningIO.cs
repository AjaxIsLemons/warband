using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

/// <summary>
/// Loads/saves the tuning config as JSON in StreamingAssets — the source of truth. Newtonsoft
/// (not JsonUtility) so enums serialize as STRINGS and colors as HEX ("#RRGGBBAA"), which keeps
/// the file readable and diff-friendly for hand/agent edits. PopulateObject overlays JSON onto a
/// fully-defaulted object, so a partial patch only overrides the fields it names.
/// </summary>
public static class TuningIO
{
    public const string FileName = "tuning.json";
    public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, FileName);

    private static JsonSerializerSettings Settings() => new JsonSerializerSettings
    {
        Converters = { new StringEnumConverter(), new ColorHexConverter() },
        Culture = CultureInfo.InvariantCulture,
        Formatting = Formatting.Indented,
        MissingMemberHandling = MissingMemberHandling.Error,        // catch typo'd/stale keys loudly
        ObjectCreationHandling = ObjectCreationHandling.Replace,    // replace lists on reload, don't append
        FloatFormatHandling = FloatFormatHandling.DefaultValue,
    };

    public static bool Load(TuningData into)
    {
        if (!File.Exists(Path)) { Debug.LogWarning($"[Tuning] no file at {Path}; using defaults"); return false; }
        try { JsonConvert.PopulateObject(File.ReadAllText(Path), into, Settings()); return true; }
        catch (Exception e) { Debug.LogError($"[Tuning] parse FAILED ({e.Message}); keeping current values"); return false; }
    }

    public static void Save(TuningData data)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, JsonConvert.SerializeObject(data, Settings()));
    }
}

/// <summary>Color as "#RRGGBBAA" hex — readable for hand/agent edits (vs JsonUtility's {r,g,b,a}).</summary>
public class ColorHexConverter : JsonConverter<Color>
{
    public override void WriteJson(JsonWriter w, Color c, JsonSerializer s) =>
        w.WriteValue("#" + ColorUtility.ToHtmlStringRGBA(c));

    public override Color ReadJson(JsonReader r, Type t, Color existing, bool hasExisting, JsonSerializer s) =>
        r.Value is string str && ColorUtility.TryParseHtmlString(str, out var c) ? c : existing;
}
