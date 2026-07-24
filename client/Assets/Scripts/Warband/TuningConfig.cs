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

    public void LoadFromJson() => TuningIO.Load(data);
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
