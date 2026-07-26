using UnityEngine;

/// <summary>
/// In-scene projection of the JSON tuning (source of truth = StreamingAssets/tuning.json).
/// The custom Inspector (Editor/TuningConfigEditor) adds Reload-from-JSON / Write-to-JSON /
/// Reload+Apply buttons so the human gets sliders + color pickers while JSON stays canonical.
/// ReloadAndApply() is MCP-callable (agent's tight loop) and also a menu item.
/// </summary>
[ExecuteAlways]
public class TuningConfig : MonoBehaviour
{
    public TuningData data = new TuningData();

    /// <summary>Bumped on every reload. Live UI built by reflecting over <see cref="data"/> watches
    /// this and rebuilds: a reload replaces the tells list, so cached rows would edit orphans.</summary>
    public int Version { get; private set; }

    public void LoadFromJson() { TuningIO.Load(data); Version++; }
    public void WriteToJson() => TuningIO.Save(data);

    /// <summary>Reload every TuningConfig from JSON, then rebuild the ReplayPlayer so the change shows.</summary>
    public static void ReloadAndApply()
    {
        foreach (var c in FindObjectsByType<TuningConfig>(FindObjectsSortMode.None))
            c.LoadFromJson();
        var rp = FindFirstObjectByType<ReplayPlayer>();
        if (rp != null) rp.ReapplyTuning();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Warband/Reload Tuning &r")]
    private static void MenuReload() => ReloadAndApply();
#endif
}
