using UnityEditor;
using UnityEngine;

/// <summary>
/// Hybrid tuning surface: the default inspector draws TuningData with sliders + color pickers,
/// and the buttons bridge to the canonical JSON. Human edits sliders → Write to JSON; agent
/// edits JSON → Reload. JSON in StreamingAssets stays the source of truth either way.
/// </summary>
[CustomEditor(typeof(TuningConfig))]
public class TuningConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var cfg = (TuningConfig)target;

        EditorGUILayout.HelpBox($"Source of truth: {TuningIO.Path}", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("↺ Reload from JSON")) { Undo.RecordObject(cfg, "Reload Tuning"); cfg.LoadFromJson(); EditorUtility.SetDirty(cfg); }
            if (GUILayout.Button("💾 Write to JSON")) { cfg.WriteToJson(); AssetDatabase.Refresh(); }
            if (GUILayout.Button("▶ Reload + Apply")) { TuningConfig.ReloadAndApply(); }
        }
        EditorGUILayout.Space();

        DrawDefaultInspector(); // sliders + color pickers for TuningData
    }
}
