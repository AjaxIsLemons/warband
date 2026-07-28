using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Sim;

/// <summary>
/// Committed verification harness (directed-tells §7): BuildPreview every replay fixture in
/// StreamingAssets/replays at a few ticks and render Camera.main to PNGs. Output lands in
/// %USERPROFILE%/warband-shots/ — OUTSIDE the Syncthing tree on purpose, so captures never sync
/// back as repo changes. Editor-only, and MCP-drivable via ExecuteMenuItem("Warband/Render Contact Sheet").
/// </summary>
public static class RenderShots
{
    private static readonly int[] Ticks = { 4, 12, 24, 40 };

    [MenuItem("Warband/Render Contact Sheet")]
    public static void RenderContactSheet()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        if (player == null) { Debug.LogError("[RenderShots] no ReplayPlayer in the scene"); return; }
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[RenderShots] no Camera.main to render from"); return; }

        string outDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "warband-shots");
        Directory.CreateDirectory(outDir);

        string replayDir = Path.Combine(Application.streamingAssetsPath, "replays");
        if (!Directory.Exists(replayDir)) { Debug.LogError($"[RenderShots] no replays dir: {replayDir}"); return; }
        var files = Directory.GetFiles(replayDir, "*.bytes");
        Array.Sort(files, StringComparer.Ordinal);

        string original = player.replayFile;
        try
        {
            foreach (var path in files)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                player.replayFile = "replays/" + Path.GetFileName(path);
                foreach (int tick in Ticks)
                {
                    player.BuildPreview(tick);
                    string outPath = Path.Combine(outDir, $"{name}_t{tick}.png");
                    RenderTo(cam, outPath, 1600, 900);
                    Debug.Log($"[RenderShots] wrote {outPath}");
                }
            }
        }
        finally
        {
            player.replayFile = original;
            player.BuildPreview(24); // leave the scene showing the original replay again
        }
    }

    // ---- MCP playtest bridge -------------------------------------------------
    // Unity_RunCommand should only need to call these stable public methods. That keeps dynamic
    // commands tiny and avoids the relay sandbox's reflection restriction. Game View captures land
    // in a synced but git-ignored directory so the homeserv agent can inspect the UI as well as the
    // camera render; contact-sheet captures above intentionally remain outside Syncthing.

    [MenuItem("Warband/MCP/Enter Play Mode")]
    public static void McpEnterPlayMode()
    {
        KeepPlayModeBackgrounded();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    private static void KeepPlayModeBackgrounded()
    {
        // Unity 6 stores Play Focused/Maximized/Unfocused on each internal PlayModeView. Set every
        // existing Game/Simulator view to PlayUnfocused without opening or selecting a window.
        Type playModeViewType = Type.GetType("UnityEditor.PlayModeView,UnityEditor");
        PropertyInfo behavior = playModeViewType?.GetProperty(
            "enterPlayModeBehavior",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (playModeViewType == null || behavior == null) return;

        object unfocused = Enum.Parse(behavior.PropertyType, "PlayUnfocused");
        foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(playModeViewType))
            behavior.SetValue(view, unfocused);
    }

    [MenuItem("Warband/MCP/Advance Skirmish")]
    public static void McpAdvanceSkirmish()
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        if (skirmish == null) { Debug.LogError("[WarbandMCP] no SkirmishController in Play Mode"); return; }
        skirmish.EditorAdvance();
        QueueGameViewFrame();
        Debug.Log($"[WarbandMCP] advanced: {skirmish.EditorStateSummary()}");
    }

    public static bool McpSelectWeapon(int heroIndex, string weaponId)
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        bool selected = skirmish != null && skirmish.EditorSelectWeapon(heroIndex, weaponId);
        QueueGameViewFrame();
        Debug.Log($"[WarbandMCP] select hero={heroIndex} weapon={weaponId}: {selected}");
        return selected;
    }

    public static bool McpPlaceHero(int heroIndex, int row, int col)
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        bool placed = skirmish != null && skirmish.EditorPlaceHero(heroIndex, row, col);
        QueueGameViewFrame();
        Debug.Log($"[WarbandMCP] place hero={heroIndex} row={row} col={col}: {placed}");
        return placed;
    }

    public static bool McpSwapReserve(int fieldHeroIndex, int reserveHeroIndex)
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        bool swapped =
            skirmish != null &&
            skirmish.EditorSwapReserve(fieldHeroIndex, reserveHeroIndex);
        QueueGameViewFrame();
        Debug.Log(
            $"[WarbandMCP] swap field={fieldHeroIndex} reserve={reserveHeroIndex}: {swapped}");
        return swapped;
    }

    public static void McpTogglePlanningDrawer()
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        if (skirmish == null) return;
        skirmish.EditorToggleDrawer();
        QueueGameViewFrame();
        Debug.Log($"[WarbandMCP] drawer toggled: {skirmish.EditorStateSummary()}");
    }

    public static bool McpUndoPlanning()
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        bool undone = skirmish != null && skirmish.EditorUndo();
        QueueGameViewFrame();
        Debug.Log($"[WarbandMCP] undo Planning: {undone}");
        return undone;
    }

    public static bool McpVerifyBoardPick(int row, int col)
    {
        var player = UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        var cam = Camera.main;
        if (player == null || cam == null) return false;

        var expected = Hex.FromRowCol(row, col);
        Vector3 world = new Vector3(
            player.hexSize * Mathf.Sqrt(3f) * (expected.Q + expected.R / 2f),
            0f,
            player.hexSize * 1.5f * expected.R);
        Vector3 screen = cam.WorldToScreenPoint(world);
        bool picked = player.TryScreenToHex(new Vector2(screen.x, screen.y), out var actual) &&
                      actual.Equals(expected);
        Debug.Log($"[WarbandMCP] board pick row={row} col={col}: {picked}");
        return picked;
    }

    public static int McpPreviewEnrage()
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        int tick = skirmish == null ? -1 : skirmish.EditorPreviewEnrage();
        Debug.Log($"[WarbandMCP] Enrage preview tick={tick}");
        return tick;
    }

    public static void McpSetPlaybackSpeed(float ticksPerSecond)
    {
        var player = UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        if (player == null) { Debug.LogError("[WarbandMCP] no ReplayPlayer"); return; }
        player.ticksPerSecond = Mathf.Max(0.01f, ticksPerSecond);
        Debug.Log($"[WarbandMCP] playback speed={player.ticksPerSecond}");
    }

    /// <summary>
    /// Advance exactly one remote Play Mode frame. The unattended Game View does not free-run, so
    /// accelerated playback and end-of-frame captures use this deterministic stepping seam.
    /// </summary>
    [MenuItem("Warband/MCP/Step Play Mode")]
    public static void McpStepPlayMode()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.isPaused = true;
        EditorApplication.Step();
    }

    public static string McpInspect()
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        var player = UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        string state = skirmish == null ? "skirmish=missing" : skirmish.EditorStateSummary();
        state += player == null ? "; replay=missing" : $"; replayEnding={player.IsEnding}";
        Debug.Log($"[WarbandMCP] {state}");
        return state;
    }

    /// <summary>
    /// Inspect semantic UXML state rather than pixel coordinates. This remains stable when USS,
    /// responsive layout, animation, or artwork changes around the preparation flow.
    /// </summary>
    public static string McpInspectSkirmishUi()
    {
        var skirmish = UnityEngine.Object.FindFirstObjectByType<SkirmishController>();
        var document = skirmish == null ? null : skirmish.GetComponent<UIDocument>();
        if (document == null)
        {
            Debug.LogError("[WarbandMCP] no skirmish UIDocument");
            return "ui=missing";
        }

        var root = document.rootVisualElement;
        string step = root.Q<Label>("step-label")?.text ?? "missing";
        string hero = root.Q<Label>("loadout-hero-label")?.text ?? "hidden";
        string mastery = root.Q<Label>("mastery-name-label")?.text ?? "hidden";
        string primary = root.Q<Button>("primary-button")?.text ?? "missing";
        int selectedHeroes =
            root.Query<VisualElement>(className: "hero-card--selected").ToList().Count;
        int selectedWeapons =
            root.Query<Button>(className: "weapon-card--selected").ToList().Count;
        string roster = root.Q<Label>("drawer-count-label")?.text ?? "missing";
        string summary =
            $"step={step}; hero={hero}; mastery={mastery}; selectedHeroes={selectedHeroes}; " +
            $"selectedWeapons={selectedWeapons}; roster={roster}; primary={primary}";
        Debug.Log($"[WarbandMCP] UI {summary}");
        return summary;
    }

    /// <summary>Clear the shared Editor console before an isolated MCP verification pass.</summary>
    [MenuItem("Warband/MCP/Clear Console")]
    public static void McpClearConsole()
    {
        var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor");
        var clear = logEntries?.GetMethod(
            "Clear",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        clear?.Invoke(null, null);
    }

    [MenuItem("Warband/MCP/Capture Game View")]
    public static void CaptureGameViewMenu() => McpCaptureGameView("skirmish");

    /// <summary>
    /// Follow a queued capture with this from the next Unity_RunCommand. The remote editor idles its
    /// Game View between MCP calls; this explicit second frame lets WaitForEndOfFrame write the PNG.
    /// </summary>
    [MenuItem("Warband/MCP/Flush Game View Capture")]
    public static void McpFlushGameViewCapture()
    {
        RepaintGameViewOnce();
    }

    /// <summary>
    /// Capture the actual Play Mode Game View (including UI Toolkit overlays) at end-of-frame.
    /// Returns the absolute Windows path immediately; the PNG is written on the queued frame.
    /// </summary>
    public static string McpCaptureGameView(string label)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[WarbandMCP] Game View capture requires Play Mode");
            return "";
        }

        string safe = string.IsNullOrWhiteSpace(label) ? "capture" : label;
        foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../McpCaptures"));
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, safe + ".png");
        if (File.Exists(outPath)) File.Delete(outPath);

        var runner = new GameObject("~WarbandGameViewCapture").AddComponent<WarbandGameViewCapture>();
        runner.OutputPath = outPath;
        QueueGameViewFrame();
        Debug.Log($"[WarbandMCP] queued Game View capture: {outPath}");
        return outPath;
    }

    /// <summary>
    /// Compatibility seam for older MCP scripts. Foreground focus is intentionally disabled:
    /// verification must not interrupt whoever is using the Windows desktop.
    /// </summary>
    public static bool McpFocusEditor()
    {
        RepaintGameViewOnce();
        Debug.LogWarning(
            "[WarbandMCP] foreground focus is disabled; use background-safe capture or inspection");
        return false;
    }

    /// <summary>
    /// Compatibility seam for the old foreground-only window capture. Reading compositor pixels is
    /// disabled because it requires stealing desktop focus; use McpCaptureGameView instead.
    /// </summary>
    public static string McpCaptureGameViewWindow(string label)
    {
        Debug.LogError(
            "[WarbandMCP] foreground Game View capture is disabled; use McpCaptureGameView");
        return "";
    }

    private static void QueueGameViewFrame()
    {
        RepaintGameViewOnce();
        // Frame 1 starts the capture coroutine; WaitForEndOfFrame completes on frame 2. The remote
        // editor does not continuously repaint while unattended, so explicitly queue that second
        // frame through delayCall instead of requiring a second MCP command as a hidden ritual.
        EditorApplication.delayCall += RepaintGameViewOnce;
    }

    private static void RepaintGameViewOnce()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null) return;

        // Repaint an existing Game View without GetWindow/Focus: opening or selecting Editor
        // windows from an unattended MCP session must never disturb the active desktop.
        UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
        if (gameViews.Length > 0 && gameViews[0] is EditorWindow gameView)
            gameView.Repaint();
    }

    internal static void RenderTo(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

}

/// <summary>Ephemeral Play Mode writer used by RenderShots.McpCaptureGameView.</summary>
internal sealed class WarbandGameViewCapture : MonoBehaviour
{
    internal string OutputPath = "";

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
        Debug.Log($"[WarbandMCP] wrote {OutputPath}");
        UnityEngine.Object.Destroy(texture);
        UnityEngine.Object.Destroy(gameObject);
    }
}
