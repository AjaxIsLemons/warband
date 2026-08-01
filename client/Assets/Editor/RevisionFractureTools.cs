#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Warband.Sim;

/// <summary>
/// Reproducible installer for the two serialized URP renderer assets. This is the only supported
/// way to wire RevisionFractureRendererFeature: Unity creates/saves the sub-assets and their local
/// ids instead of a text editor guessing at managed YAML.
/// </summary>
public static class RevisionFractureRendererInstaller
{
    private const string ShaderPath =
        "Assets/Shaders/Warband/WarbandRevisionFracture.shader";
    private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
    private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";

    [MenuItem("Warband/Revision/Install Temporal Fault Renderer")]
    public static void Install()
    {
        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceUpdate);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
            throw new InvalidOperationException(
                $"Revision fracture shader did not import at {ShaderPath}");

        InstallInto(PcRendererPath, shader, RevisionFractureQuality.Full);
        InstallInto(MobileRendererPath, shader, RevisionFractureQuality.Mobile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RevisionFracture] renderer feature installed: PC=Full, Mobile=Mobile");
    }

    public static string VerifyInstallation()
    {
        string pc = Describe(PcRendererPath);
        string mobile = Describe(MobileRendererPath);
        string result = $"PC[{pc}] Mobile[{mobile}]";
        Debug.Log($"[RevisionFracture] {result}");
        return result;
    }

    private static void InstallInto(
        string path,
        Shader shader,
        RevisionFractureQuality quality)
    {
        UniversalRendererData data =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (data == null)
            throw new InvalidOperationException(
                $"No UniversalRendererData at {path}");

        RevisionFractureRendererFeature feature = null;
        foreach (ScriptableRendererFeature candidate in data.rendererFeatures)
        {
            if (candidate is RevisionFractureRendererFeature found)
            {
                feature = found;
                break;
            }
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<RevisionFractureRendererFeature>();
            feature.name = "Revision Temporal Fault";
            Undo.RegisterCreatedObjectUndo(feature, "Install Revision Temporal Fault");
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                feature, out _, out long localId);

            var serialized = new SerializedObject(data);
            SerializedProperty features =
                serialized.FindProperty("m_RendererFeatures");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1)
                .objectReferenceValue = feature;
            SerializedProperty featureMap =
                serialized.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1)
                .longValue = localId;
            serialized.ApplyModifiedProperties();
        }

        feature.settings.shader = shader;
        feature.settings.quality = quality;
        feature.settings.injectionPoint =
            RenderPassEvent.BeforeRenderingPostProcessing;
        feature.SetActive(true);
        feature.Create();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(data);
    }

    private static string Describe(string path)
    {
        UniversalRendererData data =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (data == null) return "renderer missing";
        foreach (ScriptableRendererFeature candidate in data.rendererFeatures)
        {
            if (candidate is not RevisionFractureRendererFeature feature) continue;
            string shader = feature.settings.shader == null
                ? "shader missing"
                : feature.settings.shader.name;
            return $"active={feature.isActive}, quality={feature.settings.quality}, shader={shader}";
        }
        return "feature missing";
    }
}

/// <summary>
/// Review-first laboratory for the flagship screen moment. It drives presentation-only state over
/// the current ReplayPlayer, never run state or simulation. The stable static methods are designed
/// for unity-mcp; the window is the human tuning surface.
/// </summary>
public sealed class RevisionFractureLab : EditorWindow
{
    private EnumField _phase;
    private Slider _progress;
    private EnumField _lineage;
    private Toggle _fullRupture;
    private Toggle _reducedMotion;
    private IntegerField _presentTick;
    private IntegerField _branchTick;
    private Label _status;

    [MenuItem("Warband/Revision/Fracture Lab")]
    public static void ShowWindow()
    {
        RevisionFractureLab window = GetWindow<RevisionFractureLab>();
        window.titleContent = new GUIContent("Revision Fracture");
        window.minSize = new Vector2(370f, 420f);
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.style.paddingLeft = 12f;
        root.style.paddingRight = 12f;
        root.style.paddingTop = 10f;

        var title = new Label("TEMPORAL FAULT LAB");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 16f;
        root.Add(title);
        root.Add(new Label(
            "Capture the witnessed present, render an earlier fold, then scrub the fault."));

        _phase = new EnumField(
            "Phase", RevisionPresentationPhase.Opening);
        _progress = new Slider("Progress", 0f, 1f) { value = 0.55f };
        _lineage = new EnumField(
            "Lineage", RevisionEffectKind.BorrowedFuture);
        _fullRupture = new Toggle("First full rupture") { value = true };
        _reducedMotion = new Toggle("Reduced motion");
        _presentTick = new IntegerField("Witnessed present tick") { value = 40 };
        _branchTick = new IntegerField("Earlier branch tick") { value = 20 };
        root.Add(_phase);
        root.Add(_progress);
        root.Add(_lineage);
        root.Add(_fullRupture);
        root.Add(_reducedMotion);
        root.Add(_presentTick);
        root.Add(_branchTick);

        _phase.RegisterValueChangedCallback(_ => ApplyControls());
        _progress.RegisterValueChangedCallback(_ => ApplyControls());
        _lineage.RegisterValueChangedCallback(_ => BeginFromControls());
        _fullRupture.RegisterValueChangedCallback(_ => BeginFromControls());
        _reducedMotion.RegisterValueChangedCallback(_ => BeginFromControls());

        root.Add(new Button(BeginFromControls) { text = "BEGIN / RESET FAULT" });
        root.Add(new Button(() =>
        {
            string result = McpPrepareDualTime(
                _presentTick.value,
                _branchTick.value,
                (RevisionEffectKind)_lineage.value ==
                    RevisionEffectKind.BorrowedFuture,
                _reducedMotion.value);
            _status.text = result;
        })
        {
            text = "CAPTURE PRESENT → RENDER BRANCH",
        });
        root.Add(new Button(() =>
        {
            string path = McpCaptureCurrent(
                $"lab-{_phase.value}-{_progress.value:0.00}", 1600, 900);
            _status.text = path;
        })
        {
            text = "CAPTURE CURRENT FRAME",
        });
        root.Add(new Button(() =>
        {
            RevisionScreenEffect.Clear();
            QueueRepaint();
            _status.text = "Fault cleared.";
        })
        {
            text = "CLEAR",
        });

        _status = new Label("Ready.");
        _status.style.whiteSpace = WhiteSpace.Normal;
        _status.style.marginTop = 8f;
        root.Add(_status);
    }

    private void BeginFromControls()
    {
        RevisionScreenEffect.DebugBegin(
            (RevisionEffectKind)_lineage.value,
            _fullRupture.value,
            _reducedMotion.value,
            CurrentTune());
        RevisionScreenEffect.SetTargetViewportPositions(
            new Vector4(0.31f, 0.62f, 0.76f, 0.35f), 2);
        ApplyControls();
    }

    private void ApplyControls()
    {
        RevisionScreenEffect.SetPhase(
            (RevisionPresentationPhase)_phase.value,
            _progress.value);
        QueueRepaint();
    }

    public static string McpPrepareDualTime(
        int presentTick,
        int branchTick,
        bool borrowedFuture,
        bool reducedMotion)
    {
        ReplayPlayer player =
            UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        Camera camera = Camera.main;
        if (player == null || camera == null)
            return "ReplayPlayer or Camera.main missing";

        RevisionEffectKind lineage = borrowedFuture
            ? RevisionEffectKind.BorrowedFuture
            : RevisionEffectKind.RecallToFormation;
        player.RenderRevisionFrame(Mathf.Max(0, presentTick));
        RevisionScreenEffect.DebugBegin(
            lineage, true, reducedMotion, CurrentTune());
        RevisionScreenEffect.SetTargetViewportPositions(
            new Vector4(0.31f, 0.62f, 0.76f, 0.35f), 2);
        RevisionScreenEffect.RequestWitnessedFutureCapture();
        RevisionScreenEffect.SetPhase(RevisionPresentationPhase.Tear, 0.72f);

        string warmup = Path.Combine(
            Path.GetTempPath(), "warband-revision-fracture-warmup.png");
        RenderShots.RenderTo(camera, warmup, 1600, 900);
        if (File.Exists(warmup)) File.Delete(warmup);

        player.RenderRevisionFrame(Mathf.Max(0, branchTick));
        RevisionScreenEffect.SetPhase(RevisionPresentationPhase.Rewind, 0.5f);
        QueueRepaint();
        string result =
            $"dual-time ready: present={presentTick}, branch={branchTick}, lineage={lineage}";
        Debug.Log($"[RevisionFractureLab] {result}");
        return result;
    }

    public static string McpSetState(int phase, float progress)
    {
        RevisionPresentationPhase parsed = (RevisionPresentationPhase)Mathf.Clamp(
            phase,
            (int)RevisionPresentationPhase.None,
            (int)RevisionPresentationPhase.Receipt);
        RevisionScreenEffect.SetPhase(parsed, progress);
        QueueRepaint();
        return $"phase={parsed}; progress={Mathf.Clamp01(progress):0.000}";
    }

    public static string McpCaptureCurrent(string label, int width, int height)
    {
        Camera camera = Camera.main;
        if (camera == null) return "Camera.main missing";
        string safe = string.IsNullOrWhiteSpace(label) ? "frame" : label;
        foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "warband-shots",
            "revision-fracture");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, safe + ".png");
        RenderShots.RenderTo(
            camera,
            path,
            Mathf.Max(320, width),
            Mathf.Max(180, height));
        Debug.Log($"[RevisionFractureLab] wrote {path}");
        return path;
    }

    public static string McpCaptureMatrix(
        int presentTick,
        int branchTick,
        bool reducedMotion)
    {
        ReplayPlayer player =
            UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        Camera camera = Camera.main;
        if (player == null || camera == null)
            return "ReplayPlayer or Camera.main missing";

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "warband-shots",
            "revision-fracture");
        Directory.CreateDirectory(directory);
        int written = 0;
        foreach (RevisionEffectKind lineage in new[]
                 {
                     RevisionEffectKind.BorrowedFuture,
                     RevisionEffectKind.RecallToFormation,
                 })
        {
            string prefix = lineage == RevisionEffectKind.BorrowedFuture
                ? "borrowed"
                : "recall";
            player.RenderRevisionFrame(Mathf.Max(0, presentTick));
            RevisionScreenEffect.DebugBegin(
                lineage, true, reducedMotion, CurrentTune());
            RevisionScreenEffect.SetTargetViewportPositions(
                new Vector4(0.31f, 0.62f, 0.76f, 0.35f), 2);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Opening, 0.15f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Opening, 0.55f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Opening, 0.95f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Held, 1f);

            RevisionScreenEffect.RequestWitnessedFutureCapture();
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Tear, 0.5f);
            player.RenderRevisionFrame(Mathf.Max(0, branchTick));
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Rewind, 0.25f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Rewind, 0.65f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Rewind, 0.95f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Vacuum, 0.5f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Landing, 0.2f);
            written += Capture(
                camera, directory, prefix, RevisionPresentationPhase.Landing, 0.8f);
        }
        RevisionScreenEffect.Clear();
        QueueRepaint();
        string result = $"wrote {written} fracture-lab frames to {directory}";
        Debug.Log($"[RevisionFractureLab] {result}");
        return result;
    }

    public static string McpVerifyContract()
    {
        try
        {
            RevisionScreenEffect.Clear();
            int before = RevisionScreenEffect.Current.ResetVersion;
            RevisionScreenEffect.DebugBegin(
                RevisionEffectKind.BorrowedFuture,
                true,
                false,
                new RevisionPresentationTune());
            RevisionScreenEffect.Frame opening = RevisionScreenEffect.Current;
            Require(opening.Active, "Opening must activate the renderer.");
            Require(opening.FullRupture, "First opening must retain full-rupture context.");
            Require(
                opening.CaptureRequest == 0,
                "Opening must not pretend a future capture exists.");

            RevisionScreenEffect.SetTargetViewportPositions(
                new Vector4(-2f, 4f, 0.8f, 0.2f), 8);
            RevisionScreenEffect.Frame targets = RevisionScreenEffect.Current;
            Require(targets.TargetCount == 2, "Target count must clamp to two.");
            Require(
                targets.TargetViewportPositions.x == 0f &&
                targets.TargetViewportPositions.y == 1f,
                "Target viewport coordinates must clamp.");

            RevisionScreenEffect.RequestWitnessedFutureCapture();
            int request = RevisionScreenEffect.Current.CaptureRequest;
            Require(request > 0, "Capture request must be monotonic and non-zero.");
            RevisionScreenEffect.SetPhase(RevisionPresentationPhase.Rewind, 0.4f);
            Require(
                RevisionScreenEffect.Current.CaptureRequest == request,
                "Rewind must retain the witnessed-future request.");
            RevisionScreenEffect.SetPhase(RevisionPresentationPhase.Receipt, 0f);
            RevisionScreenEffect.Frame receipt = RevisionScreenEffect.Current;
            Require(!receipt.Active, "Receipt must release the fullscreen renderer.");
            Require(receipt.CaptureRequest == 0, "Receipt must release the captured future.");
            Require(
                receipt.ResetVersion > before,
                "Receipt must advance the renderer cleanup version.");

            RevisionScreenEffect.Clear();
            Require(!RevisionScreenEffect.Current.Active, "Clear must deactivate the renderer.");
            Debug.Log("[RevisionFractureLab] contract PASS");
            return "PASS";
        }
        catch (Exception exception)
        {
            RevisionScreenEffect.Clear();
            Debug.LogException(exception);
            throw;
        }
    }

    private static int Capture(
        Camera camera,
        string directory,
        string prefix,
        RevisionPresentationPhase phase,
        float progress)
    {
        RevisionScreenEffect.SetPhase(phase, progress);
        string path = Path.Combine(
            directory,
            $"{prefix}-{phase.ToString().ToLowerInvariant()}-{progress:0.00}.png");
        RenderShots.RenderTo(camera, path, 1600, 900);
        return 1;
    }

    private static RevisionPresentationTune CurrentTune()
    {
        TuningConfig tuning =
            UnityEngine.Object.FindFirstObjectByType<TuningConfig>();
        return tuning?.data?.revision ?? new RevisionPresentationTune();
    }

    private static void QueueRepaint()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
