#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Editor-only offscreen capture seam for runtime UI Toolkit panels. Remote verification must
/// not steal desktop focus just to make a hidden Game view repaint.
/// </summary>
public static class UiVerificationCapture
{
    private const BindingFlags StaticMethods =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static UIDocument s_document;
    private static RenderTexture s_target;
    private static RenderTexture s_previousTarget;

    public static string RuntimeUtilityMethods()
    {
        Type utility = RuntimeUtilityType();
        return utility == null
            ? "UIElementsRuntimeUtility not found"
            : string.Join(", ", utility.GetMethods(StaticMethods)
                .Select(method => method.Name)
                .Distinct()
                .OrderBy(name => name));
    }

    public static string RuntimeUtilitySignatures()
    {
        Type utility = RuntimeUtilityType();
        return utility == null
            ? "UIElementsRuntimeUtility not found"
            : string.Join("\n", utility.GetMethods(StaticMethods)
                .Where(method => method.Name.Contains("Panel") ||
                                 method.Name.Contains("Render"))
                .Select(method => method.ToString())
                .OrderBy(signature => signature));
    }

    public static string SelectGameViewResolution(int width, int height)
    {
        width = Mathf.Max(320, width);
        height = Mathf.Max(180, height);
        Assembly editor = typeof(EditorWindow).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        Type sizeType = editor.GetType("UnityEditor.GameViewSize");
        Type sizeKindType = editor.GetType("UnityEditor.GameViewSizeType");
        Type viewType = editor.GetType("UnityEditor.GameView");
        const BindingFlags members =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo instanceProperty = sizesType?.GetProperty(
            "instance", StaticMethods | BindingFlags.FlattenHierarchy) ??
            sizesType?.BaseType?.GetProperty(
                "instance", StaticMethods | BindingFlags.FlattenHierarchy);
        object sizes = instanceProperty?.GetValue(null);
        if (sizes == null || sizeType == null ||
            sizeKindType == null || viewType == null)
            return "Unity Game View reflection API unavailable";
        object group = sizesType?.GetMethod(
            "GetGroup", members)?.Invoke(
                sizes, new object[] { GameViewSizeGroupType.Standalone });
        if (group == null)
            return "Unity Game View reflection API unavailable";

        Type groupType = group.GetType();
        int builtin = (int)groupType.GetMethod(
            "GetBuiltinCount", members).Invoke(group, null);
        int custom = (int)groupType.GetMethod(
            "GetCustomCount", members).Invoke(group, null);
        MethodInfo getSize = groupType.GetMethod("GetGameViewSize", members);
        PropertyInfo widthProperty = sizeType.GetProperty("width", members);
        PropertyInfo heightProperty = sizeType.GetProperty("height", members);
        int selected = -1;
        for (int index = 0; index < builtin + custom; index++)
        {
            object size = getSize.Invoke(group, new object[] { index });
            if ((int)widthProperty.GetValue(size) == width &&
                (int)heightProperty.GetValue(size) == height)
            {
                selected = index;
                break;
            }
        }

        if (selected < 0)
        {
            object size = Activator.CreateInstance(
                sizeType, members, null,
                new object[]
                {
                    Enum.ToObject(sizeKindType, 1), width, height,
                    $"{width}x{height} Workbench QA",
                },
                null);
            groupType.GetMethod("AddCustomSize", members)
                .Invoke(group, new[] { size });
            selected = builtin + custom;
        }

        EditorWindow view = Resources.FindObjectsOfTypeAll(viewType)
            .OfType<EditorWindow>()
            .FirstOrDefault();
        if (view == null) return "No Game View exists";
        viewType.GetProperty("selectedSizeIndex", members)
            .SetValue(view, selected);
        view.Repaint();
        return $"Selected fixed {width}x{height} Game View index {selected}";
    }

    public static string RepaintRuntimePanels()
    {
        Type utility = RuntimeUtilityType();
        if (utility == null) return "UIElementsRuntimeUtility not found";
        foreach (string name in new[]
                 {
                     "SetPanelOrderingDirty",
                     "SetPanelsDrawInCameraDirty",
                     "UpdateRuntimePanels",
                     "RepaintOffscreenPanels",
                     "RepaintOverlayPanels",
                     "UpdatePanels",
                     "RenderOffscreenPanels",
                     "RepaintPanels",
                 })
        {
            MethodInfo method = utility.GetMethod(
                name, StaticMethods, null, Type.EmptyTypes, null);
            method?.Invoke(null, null);
        }
        MethodInfo repaint = utility.GetMethod(
            "RepaintPanels", StaticMethods, null, new[] { typeof(bool) }, null);
        repaint?.Invoke(null, new object[] { true });
        return "Runtime UI panels updated and repainted";
    }

    public static string BeginCapture(UIDocument document, int width, int height)
    {
        CleanupPendingCapture();
        if (document == null || document.panelSettings == null)
            return "UIDocument or PanelSettings missing";
        width = Mathf.Max(320, width);
        height = Mathf.Max(180, height);
        s_document = document;
        s_previousTarget = document.panelSettings.targetTexture;
        s_target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "UiVerificationCaptureTarget",
        };
        s_target.Create();
        document.panelSettings.targetTexture = s_target;
        RepaintRuntimePanels();
        return $"Capture target prepared at {width}x{height}";
    }

    public static string FinishCapture(string fileName)
    {
        if (s_document == null || s_target == null)
            return "No pending UI capture";
        RepaintRuntimePanels();
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = s_target;
        var image = new Texture2D(
            s_target.width, s_target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, s_target.width, s_target.height), 0, 0);
        image.Apply();
        string folder = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TempCaptures"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previousActive;
        UnityEngine.Object.DestroyImmediate(image);
        CleanupPendingCapture();
        RepaintRuntimePanels();
        return path;
    }

    private static void CleanupPendingCapture()
    {
        if (s_document != null && s_document.panelSettings != null)
            s_document.panelSettings.targetTexture = s_previousTarget;
        if (s_target != null)
        {
            s_target.Release();
            UnityEngine.Object.DestroyImmediate(s_target);
        }
        s_document = null;
        s_target = null;
        s_previousTarget = null;
    }

    private static Type RuntimeUtilityType() =>
        typeof(UIDocument).Assembly.GetType(
            "UnityEngine.UIElements.UIElementsRuntimeUtility");
}
#endif
