#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Warband.Sim;

internal static class VfxLabPaths
{
    internal const string Scene = "Assets/Scenes/VfxLab.unity";
    internal const string RecipeFolder = "Assets/Resources/VFX/Recipes";
    internal const string StyleSheet = "Assets/Editor/VfxLab/VfxLabWindow.uss";
}

/// <summary>All serialized asset writes for the Lab live here and happen only on explicit Apply.</summary>
internal static class VfxLabAssetTools
{
    internal static VfxRecipeAsset ApplyDraft(VfxRecipeAsset draft)
    {
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        var errors = new List<string>();
        if (!draft.Validate(errors))
            throw new InvalidOperationException(string.Join("\n", errors));

        EnsureFolder(VfxLabPaths.RecipeFolder);
        string safeName = Sanitize(draft.recipeId);
        string path = $"{VfxLabPaths.RecipeFolder}/{safeName}.asset";
        VfxRecipeAsset asset = AssetDatabase.LoadAssetAtPath<VfxRecipeAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<VfxRecipeAsset>();
            asset.name = safeName;
            asset.CopyFrom(draft.Compile());
            asset.enabledOverride = true;
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create VFX Recipe Override");
        }
        else
        {
            Undo.RecordObject(asset, "Apply VFX Recipe Draft");
            asset.CopyFrom(draft.Compile());
            asset.enabledOverride = draft.enabledOverride;
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        VfxLibrary.ReloadAssetOverrides();
        return asset;
    }

    internal static VfxRecipeAsset CreateNewAtPath(string path, string id)
    {
        EnsureFolder(VfxLabPaths.RecipeFolder);
        var asset = ScriptableObject.CreateInstance<VfxRecipeAsset>();
        asset.name = Path.GetFileNameWithoutExtension(path);
        asset.recipeId = id;
        asset.duration = 0.5f;
        asset.elements.Add(VfxRecipeElementData.Default(VfxRecipeElementKind.Quad));
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        VfxLibrary.ReloadAssetOverrides();
        Selection.activeObject = asset;
        return asset;
    }

    internal static VfxRecipeAsset FindOverride(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) return null;
        if (!AssetDatabase.IsValidFolder(VfxLabPaths.RecipeFolder)) return null;
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:VfxRecipeAsset",
                     new[] { VfxLabPaths.RecipeFolder }))
        {
            VfxRecipeAsset asset = AssetDatabase.LoadAssetAtPath<VfxRecipeAsset>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null &&
                string.Equals(asset.recipeId, recipeId, StringComparison.Ordinal))
                return asset;
        }
        return null;
    }

    internal static string Sanitize(string id)
    {
        string value = (id ?? "new-effect").Trim().ToLowerInvariant();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '-');
        value = value.Replace(' ', '-').Replace('_', '-');
        while (value.IndexOf("--", StringComparison.Ordinal) >= 0)
            value = value.Replace("--", "-");
        return string.IsNullOrWhiteSpace(value) ? "new-effect" : value;
    }

    internal static void EnsureFolder(string fullFolder)
    {
        string[] parts = fullFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

/// <summary>Idempotent creator for the Unity-managed VfxLab scene.</summary>
public static class VfxLabSceneTools
{
    [MenuItem("Warband/VFX Lab/Open Lab Scene", priority = 100)]
    public static void OpenScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? "",
                VfxLabPaths.Scene)))
            BuildScene();
        EditorSceneManager.OpenScene(VfxLabPaths.Scene, OpenSceneMode.Single);
        VfxLabWindow.ShowWindow();
    }

    [MenuItem("Warband/VFX Lab/Rebuild Lab Scene", priority = 101)]
    public static void RebuildSceneMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild VFX Lab scene?",
                "This replaces only Assets/Scenes/VfxLab.unity. Source recipes and tuning are untouched.",
                "Rebuild",
                "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
        VfxLabWindow.ShowWindow();
    }

    /// <summary>Unity-MCP entry: build and open the Lab without hand-editing scene YAML.</summary>
    public static string McpBuildScene()
    {
        BuildScene();
        string result = $"VFX Lab scene ready at {VfxLabPaths.Scene}";
        Debug.Log($"[VfxLab] {result}");
        return result;
    }

    public static string McpVerifyContract()
    {
        var failures = new List<string>();
        if (VfxLibrary.AllIds.Count < 38)
            failures.Add($"Expected at least 38 recipes, found {VfxLibrary.AllIds.Count}.");
        foreach (string id in VfxLibrary.AllIds)
        {
            VfxDef def = VfxLibrary.Get(id);
            if (def == null) failures.Add($"{id}: no resolved recipe");
            else if (def.Elements == null || def.Elements.Length == 0)
                failures.Add($"{id}: no elements");
            else
            {
                VfxRecipeAsset draft = VfxRecipeAsset.CreateDraft(def);
                VfxDef roundTrip = draft.Compile();
                if (roundTrip.Elements.Length != def.Elements.Length)
                    failures.Add($"{id}: round-trip changed element count");
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        VfxLabStage stage = UnityEngine.Object.FindFirstObjectByType<VfxLabStage>();
        if (scene.path == VfxLabPaths.Scene && stage == null)
            failures.Add("VfxLab scene has no VfxLabStage.");

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "VFX Lab contract failed:\n- " + string.Join("\n- ", failures));
        string result =
            $"PASS: {VfxLibrary.AllIds.Count} recipes resolve and round-trip; scene={scene.path}";
        Debug.Log($"[VfxLab] {result}");
        return result;
    }

    public static string McpSmokeModes()
    {
        VfxLabStage stage =
            UnityEngine.Object.FindFirstObjectByType<VfxLabStage>();
        if (stage == null)
            throw new InvalidOperationException("No VfxLabStage in the active scene.");

        stage.SetEnvironment(VfxLabEnvironmentMode.NeutralStudio);
        stage.SelectRecipe(
            VfxLibrary.Get("fire-release"),
            VfxLabRecipeContext.AtTarget,
            new Color(1f, 0.5f, 0.2f),
            VfxLibrary.GlowRef,
            1f);
        stage.Evaluate(0.18f);
        if (!stage.HasDirectEffect)
            throw new InvalidOperationException(
                "Direct recipe did not create a VfxInstance.");

        stage.SetEnvironment(VfxLabEnvironmentMode.Isolation);
        if (stage.EnvironmentMode != VfxLabEnvironmentMode.Isolation)
            throw new InvalidOperationException("Isolation context did not stick.");
        stage.SetEnvironment(VfxLabEnvironmentMode.ProductionShard);

        stage.SelectFixture("replays/statusstorm.bytes", 0);
        if (stage.Mode != VfxLabStageMode.CombatFixture ||
            stage.Duration <= 0.1f ||
            stage.ReplayPlayer.EndTick <= 0)
            throw new InvalidOperationException("Fixture driver did not load.");
        stage.Evaluate(Mathf.Min(1f, stage.Duration));

        stage.ConfigureRevision(
            "replays/hourstone.bytes",
            RevisionEffectKind.BorrowedFuture,
            true,
            false,
            40,
            20);
        RevisionPresentationTune tune = stage.Tuning.data.revision;
        float rewindSample =
            tune.firstOpenSeconds + 0.32f + tune.tearSeconds + 0.1f;
        stage.Evaluate(rewindSample);
        if (RevisionScreenEffect.Current.Phase !=
            RevisionPresentationPhase.Rewind)
            throw new InvalidOperationException(
                $"Expected Rewind, got {RevisionScreenEffect.Current.Phase}.");
        stage.Evaluate(stage.Duration - 0.01f);
        if (RevisionScreenEffect.Current.Phase !=
            RevisionPresentationPhase.Receipt)
            throw new InvalidOperationException(
                "Revision did not land at Receipt.");

        string result =
            $"PASS: recipe + three environments + fixture + Revision; recipes={VfxLibrary.AllIds.Count}";
        Debug.Log($"[VfxLab] {result}");
        return result;
    }

    private static void BuildScene()
    {
        VfxLabAssetTools.EnsureFolder("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        var root = new GameObject("VFX Lab Stage");
        var tuning = root.AddComponent<TuningConfig>();
        tuning.LoadFromJson();
        var replay = root.AddComponent<ReplayPlayer>();
        replay.replayFile = "replays/weaponry.bytes";
        replay.autoPlayOnStart = false;
        replay.loop = false;
        replay.previewAdvanceSeconds = 0.12f;

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = tuning.data.camera.background;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<UniversalAdditionalCameraData>();

        var keyObject = new GameObject("Key Light");
        keyObject.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
        var key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.91f, 0.78f);
        key.intensity = 1.15f;
        key.shadows = LightShadows.Soft;

        var volumeObject = new GameObject("Global Volume");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
            "Assets/Settings/DioramaVolume.asset");

        var stage = root.AddComponent<VfxLabStage>();
        stage.Configure(camera, replay, tuning);
        stage.Initialize();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, VfxLabPaths.Scene))
            throw new InvalidOperationException(
                $"Unity could not save {VfxLabPaths.Scene}");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

internal sealed class VfxLabFixtureInfo
{
    internal string RelativePath;
    internal string DisplayName;
    internal int UnitCount;
    internal int EventCount;
    internal int EndTick;
    internal List<ReplayInspector.Stat> Signatures = new List<ReplayInspector.Stat>();

    internal static VfxLabFixtureInfo Load(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        string absolute = Path.Combine(
            Application.streamingAssetsPath,
            normalized);
        using FileStream stream = File.OpenRead(absolute);
        (List<PlaybackUnit> initial, List<BattleEvent> events) = Replay.Read(stream);
        return new VfxLabFixtureInfo
        {
            RelativePath = normalized,
            DisplayName = Path.GetFileNameWithoutExtension(normalized),
            UnitCount = initial.Count,
            EventCount = events.Count,
            EndTick = events.Count > 0 ? events[events.Count - 1].Tick : 0,
            Signatures = ReplayInspector.Summarize(events),
        };
    }
}

internal sealed class VfxLabTellDraft : ScriptableObject
{
    public int sourceIndex = -1;
    public TellDef tell = new TellDef();

    internal void Load(int index, TellDef source)
    {
        sourceIndex = index;
        tell = VfxLabTellTools.Clone(source);
    }
}

internal sealed class VfxLabRevisionDraft : ScriptableObject
{
    public RevisionPresentationTune tune = new RevisionPresentationTune();

    internal void Load(RevisionPresentationTune source)
    {
        tune = VfxLabRevisionTuneTools.Clone(source);
    }
}

internal static class VfxLabRevisionTuneTools
{
    internal static RevisionPresentationTune Clone(RevisionPresentationTune source)
    {
        source ??= new RevisionPresentationTune();
        var copy = new RevisionPresentationTune();
        foreach (FieldInfo field in typeof(RevisionPresentationTune).GetFields(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (!field.IsInitOnly) field.SetValue(copy, field.GetValue(source));
        }
        return copy;
    }

    internal static void Apply(TuningConfig config, VfxLabRevisionDraft draft)
    {
        if (config == null || draft == null)
            throw new InvalidOperationException("Revision tuning draft has no target.");
        Undo.RecordObject(config, "Apply Revision Presentation Draft");
        config.data.revision = Clone(draft.tune);
        config.WriteToJson();
        EditorUtility.SetDirty(config);
        AssetDatabase.Refresh();
    }
}

internal static class VfxLabTellTools
{
    internal static TellDef Clone(TellDef source)
    {
        if (source == null) return new TellDef();
        var copy = new TellDef();
        foreach (FieldInfo field in typeof(TellDef).GetFields(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.IsInitOnly) continue;
            field.SetValue(copy, field.GetValue(source));
        }
        return copy;
    }

    internal static void Apply(TuningConfig config, VfxLabTellDraft draft)
    {
        if (config == null || draft == null ||
            draft.sourceIndex < 0 ||
            draft.sourceIndex >= config.data.tells.Count)
            throw new InvalidOperationException("The selected tell no longer exists.");
        Undo.RecordObject(config, "Apply VFX Tell Draft");
        config.data.tells[draft.sourceIndex] = Clone(draft.tell);
        config.WriteToJson();
        EditorUtility.SetDirty(config);
        AssetDatabase.Refresh();
    }

    internal static string Describe(TellDef tell, int index)
    {
        if (tell == null) return $"#{index}";
        var filters = new List<string>();
        if (tell.byCause) filters.Add(tell.cause.ToString());
        if (tell.byStatus) filters.Add(tell.status.ToString());
        if (tell.byAbility) filters.Add(tell.ability);
        if (tell.byWeapon) filters.Add(tell.weapon);
        if (tell.byChassis) filters.Add(tell.chassis);
        if (tell.byRule) filters.Add(tell.rule);
        string suffix = filters.Count > 0 ? $" · {string.Join(" / ", filters)}" : "";
        return $"#{index:00} {tell.eventKind}{suffix}";
    }
}

internal static class VfxLabAudioPreview
{
    private static MethodInfo _play;
    private static MethodInfo _stop;

    internal static bool PlayRaw(AudioClip clip)
    {
        if (clip == null) return false;
        Resolve();
        if (_play == null) return false;
        ParameterInfo[] parameters = _play.GetParameters();
        object[] args = parameters.Length switch
        {
            1 => new object[] { clip },
            2 => new object[] { clip, 0 },
            _ => new object[] { clip, 0, false },
        };
        _play.Invoke(null, args);
        return true;
    }

    internal static void StopAll()
    {
        Resolve();
        _stop?.Invoke(null, Array.Empty<object>());
    }

    private static void Resolve()
    {
        if (_play != null || _stop != null) return;
        Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        if (audioUtil == null) return;
        _play = audioUtil.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
                method.Name == "PlayPreviewClip" &&
                method.GetParameters().Length >= 1 &&
                method.GetParameters()[0].ParameterType == typeof(AudioClip));
        _stop = audioUtil.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
                method.Name == "StopAllPreviewClips" &&
                method.GetParameters().Length == 0);
    }
}
#endif
