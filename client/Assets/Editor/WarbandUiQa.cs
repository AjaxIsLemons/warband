#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Background-safe, deterministic UI matrix runner. Actual Game View pixels are evidence for
/// human review; UiLayoutContract is the automated gate. State survives the Play Mode domain
/// reload so a menu command can run unattended without focusing the Windows editor.
/// </summary>
[InitializeOnLoad]
public static class WarbandUiQa
{
    [Serializable]
    private sealed class WorkItem
    {
        public string Surface = "workbench";
        public string Fixture;
        public int Width;
        public int Height;
        public bool ExpandedText;
        public bool ForcePhone;
    }

    [Serializable]
    private sealed class Result
    {
        public string Surface;
        public string Fixture;
        public string Viewport;
        public bool ExpandedText;
        public bool ForcePhone;
        public string Layout;
        public string Diagnostics;
        public string Image;
    }

    [Serializable]
    private sealed class State
    {
        public bool Active;
        public string RunId;
        public string Mode;
        public string Phase = "waiting-for-play";
        public int Index;
        public int Frames;
        public string CapturePath = "";
        public string OutputDirectory = "";
        public bool OffscreenCapture;
        public bool PreferOffscreenCapture;
        public string LiveRankUpRegression = "";
        public List<WorkItem> Items = new List<WorkItem>();
        public List<Result> Results = new List<Result>();
    }

    private const string SessionKey = "warband.ui-qa.state";
    private static State s_state;
    private static double s_lastTick;

    static WarbandUiQa()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Load();
    }

    [MenuItem("Warband/UI QA/Run Responsive Smoke Matrix")]
    public static void RunSmoke() => Begin("smoke");

    public static void RunSmokeHeadless() => Begin("smoke", preferOffscreen: true);

    [MenuItem("Warband/UI QA/Run Responsive Full Matrix")]
    public static void RunFull() => Begin("full");

    public static void RunFullHeadless() => Begin("full", preferOffscreen: true);

    [MenuItem("Warband/UI QA/Run Workbench Full Matrix")]
    public static void RunWorkbenchFull() => Begin("workbench-full");

    public static void RunWorkbenchFullHeadless() =>
        Begin("workbench-full", preferOffscreen: true);

    [MenuItem("Warband/UI QA/Cancel Active Run")]
    public static void Cancel()
    {
        Load();
        if (s_state == null || !s_state.Active) return;
        s_state.Active = false;
        Save();
        Debug.LogWarning("[UI QA] Active responsive matrix cancelled.");
    }

    /// <summary>
    /// Remote editors can idle immediately after the final capture. This gives MCP callers an
    /// explicit, deterministic finalization seam instead of depending on one more editor update.
    /// </summary>
    public static bool FinalizeIfComplete()
    {
        Load();
        if (s_state == null || !s_state.Active ||
            s_state.Index < s_state.Items.Count)
            return false;
        RunShell shell = UnityEngine.Object.FindFirstObjectByType<RunShell>();
        if (shell == null) return false;
        Finish(shell);
        return true;
    }

    public static string Status()
    {
        Load();
        if (s_state == null || !s_state.Active)
            return "UI QA idle";
        return $"UI QA {s_state.Mode}: {s_state.Index + 1}/{s_state.Items.Count} " +
               $"{s_state.Phase}";
    }

    /// <summary>
    /// Headless MCP sessions can advance Play Mode while EditorApplication.update remains idle.
    /// Keep the same state machine, but expose one deterministic, background-safe tick.
    /// </summary>
    public static void Pump()
    {
        s_lastTick = 0d;
        Tick();
    }

    private static void Begin(string mode, bool preferOffscreen = false)
    {
        if (s_state != null && s_state.Active)
        {
            Debug.LogWarning("[UI QA] A matrix run is already active. " + Status());
            return;
        }

        string runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string output = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "TempCaptures", "ui-qa", runId));
        Directory.CreateDirectory(output);
        s_state = new State
        {
            Active = true,
            RunId = runId,
            Mode = mode,
            OutputDirectory = output,
            PreferOffscreenCapture = preferOffscreen,
            Items = BuildItems(mode),
        };
        Save();
        Debug.Log($"[UI QA] Starting {mode} responsive matrix: " +
                  $"{s_state.Items.Count} deterministic captures → {output}");
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            RenderShots.McpEnterPlayMode();
            return;
        }
        s_state.Phase = "load";
        Save();
    }

    private static List<WorkItem> BuildItems(string mode)
    {
        var items = new List<WorkItem>();
        bool full = mode == "full" || mode == "workbench-full";
        bool workbenchOnly = mode == "workbench-full";
        var viewports = full
            ? new[]
            {
                new Vector2Int(1024, 768),
                new Vector2Int(1280, 720),
                new Vector2Int(1600, 900),
                new Vector2Int(2556, 1317),
                new Vector2Int(3440, 1440),
            }
            : new[]
            {
                new Vector2Int(1280, 720),
                new Vector2Int(1600, 900),
            };
        string[] fixtures = full
            ? WorkbenchFixtures.Ids
            : new[]
            {
                "market-recruit",
                "market-rankup-long",
                "armory-full",
                "rail-full",
                "tooltip-keyword",
                "tooltip-equipment",
            };
        foreach (Vector2Int viewport in viewports)
            foreach (string fixture in fixtures)
                items.Add(new WorkItem
                {
                    Surface = "workbench",
                    Fixture = fixture,
                    Width = viewport.x,
                    Height = viewport.y,
                    ExpandedText = fixture == "market-rankup-long",
                });
        if (full)
            foreach (Vector2Int viewport in viewports)
                items.Add(new WorkItem
                {
                    Surface = "workbench",
                    Fixture = "market-recruit",
                    Width = viewport.x,
                    Height = viewport.y,
                    ExpandedText = true,
                });
        if (workbenchOnly) return items;

        Vector2Int[] routeViewports = mode == "full"
            ? new[]
            {
                new Vector2Int(1024, 768),
                new Vector2Int(1280, 720),
                new Vector2Int(1600, 900),
                new Vector2Int(3440, 1440),
            }
            : new[] { new Vector2Int(1280, 720) };
        foreach (Vector2Int viewport in routeViewports)
            foreach (string surface in new[] { "wager", "deploy", "result" })
                items.Add(new WorkItem
                {
                    Surface = surface,
                    Fixture = surface + "-nominal",
                    Width = viewport.x,
                    Height = viewport.y,
                    ExpandedText = mode == "full" && viewport.x == 1024,
                });
        if (mode == "full")
        {
            foreach (string surface in new[] { "workbench", "wager", "deploy", "result" })
                items.Add(new WorkItem
                {
                    Surface = surface,
                    Fixture = surface == "workbench" ? "market-recruit" : surface + "-phone",
                    Width = 1920,
                    Height = 1080,
                    ForcePhone = true,
                    ExpandedText = true,
                });
            items.Add(new WorkItem
            {
                Surface = "rotation",
                Fixture = "portrait-guard",
                Width = 390,
                Height = 844,
                ForcePhone = true,
            });
        }
        return items;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - s_lastTick < 0.05d) return;
        s_lastTick = EditorApplication.timeSinceStartup;
        if (s_state == null) Load();
        if (s_state == null || !s_state.Active) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                RenderShots.McpEnterPlayMode();
            return;
        }

        RunShell shell = UnityEngine.Object.FindFirstObjectByType<RunShell>();
        if (shell == null || !shell.EditorEnsureReadyForFixtures()) return;
        if (s_state.Index >= s_state.Items.Count)
        {
            Finish(shell);
            return;
        }

        WorkItem item = s_state.Items[s_state.Index];
        switch (s_state.Phase)
        {
            case "waiting-for-play":
                s_state.Phase = "load";
                Save();
                break;
            case "load":
                LoadItem(shell, item);
                break;
            case "settle":
                Settle(shell, item);
                break;
            case "capture":
                PollCapture(item);
                break;
            default:
                s_state.Phase = "load";
                Save();
                break;
        }
    }

    private static void LoadItem(RunShell shell, WorkItem item)
    {
        string selected =
            UiVerificationCapture.SelectGameViewResolution(item.Width, item.Height);
        s_state.OffscreenCapture = s_state.PreferOffscreenCapture ||
            selected.Contains("No Game View") ||
            selected.Contains("reflection API unavailable");
        if (s_state.OffscreenCapture)
        {
            UIDocument document = shell.GetComponent<UIDocument>();
            selected += "; " + UiVerificationCapture.BeginCapture(
                document, item.Width, item.Height);
        }
        shell.EditorForcePhoneLayout(item.ForcePhone);
        bool loaded = item.Surface switch
        {
            "wager" => shell.EditorLoadWagerFixture(
                item.ExpandedText, reducedMotion: false),
            "deploy" => shell.EditorLoadDeployFixture(
                item.ExpandedText, reducedMotion: false),
            "result" => shell.EditorLoadResultFixture(
                item.ExpandedText, reducedMotion: false),
            "rotation" => shell.EditorLoadWorkbenchFixture(
                "market-recruit", item.ExpandedText, reducedMotion: false),
            _ => shell.EditorLoadWorkbenchFixture(
                item.Fixture, item.ExpandedText, reducedMotion: false),
        };
        if (!loaded)
        {
            Record(item, $"{item.Surface}: FAIL · fixture could not be loaded", "");
            Advance();
            return;
        }
        s_state.Frames = 0;
        s_state.Phase = "settle";
        Save();
        Debug.Log($"[UI QA] {selected}; loaded {Label(item)}");
        QueueFrame();
    }

    private static void Settle(RunShell shell, WorkItem item)
    {
        s_state.Frames++;
        if (s_state.OffscreenCapture) UiVerificationCapture.RepaintRuntimePanels();
        QueueFrame();
        if (s_state.Frames == 4)
        {
            if (item.Surface == "workbench" && item.Fixture == "tooltip-keyword")
                shell.EditorShowWorkbenchKeywordTooltip();
            else if (item.Surface == "workbench" && item.Fixture == "tooltip-equipment")
                shell.EditorShowWorkbenchEquipmentTooltip();
            else if (item.Surface == "workbench" && item.Fixture == "tooltip-rank-tier")
                shell.EditorShowWorkbenchRankTierTooltip();
        }
        if (s_state.Frames < 10)
        {
            Save();
            return;
        }

        string layout = Layout(shell, item);
        string label = "ui-qa-" + s_state.RunId + "-" +
                       item.Width + "x" + item.Height + "-" +
                       item.Surface + "-" + item.Fixture +
                       (item.ForcePhone ? "-phone" : "") +
                       (item.ExpandedText ? "-expanded" : "");
        if (s_state.OffscreenCapture)
        {
            string offscreen = UiVerificationCapture.FinishCapture(label + ".png");
            string destination = CopyToOutput(offscreen);
            Record(item, layout + "; capture=offscreen-panel-fallback", destination);
            Advance();
            return;
        }
        s_state.CapturePath = RenderShots.McpCaptureGameView(label);
        s_state.Phase = "capture";
        s_state.Frames = 0;
        Save();
        if (!Passed(layout))
            Debug.LogError("[UI QA] " + Label(item) + " · " + layout);
    }

    private static void PollCapture(WorkItem item)
    {
        s_state.Frames++;
        QueueFrame();
        if (!string.IsNullOrWhiteSpace(s_state.CapturePath) &&
            File.Exists(s_state.CapturePath) &&
            new FileInfo(s_state.CapturePath).Length > 256)
        {
            string destination = CopyToOutput(s_state.CapturePath);
            RunShell shell = UnityEngine.Object.FindFirstObjectByType<RunShell>();
            Record(item, shell == null
                ? $"{item.Surface}: FAIL · shell disappeared"
                : Layout(shell, item), destination);
            Advance();
            return;
        }
        if (s_state.Frames < 240)
        {
            Save();
            return;
        }
        Record(item, $"{item.Surface}: FAIL · actual Game View capture timed out", "");
        Advance();
    }

    private static void Record(WorkItem item, string layout, string image)
    {
        s_state.Results.Add(new Result
        {
            Surface = item.Surface,
            Fixture = item.Fixture,
            Viewport = item.Width + "x" + item.Height,
            ExpandedText = item.ExpandedText,
            ForcePhone = item.ForcePhone,
            Layout = layout,
            Diagnostics = item.Surface == "workbench"
                ? UnityEngine.Object.FindFirstObjectByType<RunShell>()
                    ?.EditorWorkbenchSemanticSnapshot() ?? "shell=missing"
                : "",
            Image = image,
        });
    }

    private static void Advance()
    {
        s_state.Index++;
        s_state.Frames = 0;
        s_state.CapturePath = "";
        s_state.Phase = "load";
        Save();
    }

    private static string CopyToOutput(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return "";
        string destination = Path.Combine(
            s_state.OutputDirectory, Path.GetFileName(source));
        File.Copy(source, destination, true);
        return destination;
    }

    private static void Finish(RunShell shell)
    {
        try
        {
            s_state.LiveRankUpRegression =
                shell.EditorVerifyPendingForkMarketRebuild();
        }
        catch (Exception ex)
        {
            s_state.LiveRankUpRegression =
                $"FAIL · pending-fork Market rebuild threw {ex.GetType().Name}: {ex.Message}";
        }
        shell.EditorForcePhoneLayout(false);
        shell.EditorClearWorkbenchFixture();
        WriteReport();
        WriteContactSheet();
        int failed = 0;
        foreach (Result result in s_state.Results)
            if (!Passed(result.Layout)) failed++;
        if (!s_state.LiveRankUpRegression.StartsWith(
                "PASS", StringComparison.Ordinal))
            failed++;
        string directory = s_state.OutputDirectory;
        string mode = s_state.Mode;
        int count = s_state.Results.Count;
        s_state.Active = false;
        Save();
        if (failed == 0)
            Debug.Log($"[UI QA] {mode} matrix PASS · {count} captures → {directory}");
        else
            Debug.LogError($"[UI QA] {mode} matrix FAIL · {failed}/{count} structural " +
                           $"failures → {directory}");
    }

    private static void WriteReport()
    {
        string json = JsonUtility.ToJson(s_state, true);
        File.WriteAllText(Path.Combine(s_state.OutputDirectory, "report.json"), json);
        var lines = new List<string>
        {
            "# Responsive UI QA",
            "",
            $"- Run: `{s_state.RunId}`",
            $"- Matrix: `{s_state.Mode}`",
            $"- Captures: {s_state.Results.Count}",
            $"- Live rank-up regression: {s_state.LiveRankUpRegression}",
            "- Pixel captures require human review; structural layout is the automated gate.",
            "- Captures marked `offscreen-panel-fallback` were rendered to the exact target " +
            "without opening or focusing a Game View window.",
            "",
            "| Surface | Fixture | Viewport | Phone | Copy stress | Layout | Capture |",
            "|---|---|---:|:---:|:---:|---|---|",
        };
        foreach (Result result in s_state.Results)
        {
            string image = string.IsNullOrWhiteSpace(result.Image)
                ? "missing"
                : Path.GetFileName(result.Image);
            lines.Add($"| {result.Surface} | {result.Fixture} | {result.Viewport} | " +
                      $"{(result.ForcePhone ? "yes" : "no")} | " +
                      $"{(result.ExpandedText ? "130%" : "nominal")} | " +
                      $"{result.Layout.Replace("|", "/")} | {image} |");
        }
        File.WriteAllLines(Path.Combine(s_state.OutputDirectory, "report.md"), lines);
    }

    private static void WriteContactSheet()
    {
        var images = new List<Texture2D>();
        foreach (Result result in s_state.Results)
        {
            if (string.IsNullOrWhiteSpace(result.Image) || !File.Exists(result.Image)) continue;
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (image.LoadImage(File.ReadAllBytes(result.Image))) images.Add(image);
            else UnityEngine.Object.DestroyImmediate(image);
        }
        if (images.Count == 0) return;

        const int cellWidth = 400;
        const int cellHeight = 230;
        const int columns = 3;
        int rows = Mathf.CeilToInt(images.Count / (float)columns);
        var sheet = new Texture2D(
            cellWidth * columns, cellHeight * rows, TextureFormat.RGB24, false);
        Color32[] background = new Color32[sheet.width * sheet.height];
        for (int i = 0; i < background.Length; i++)
            background[i] = new Color32(8, 12, 18, 255);
        sheet.SetPixels32(background);

        for (int i = 0; i < images.Count; i++)
        {
            Texture2D source = images[i];
            float scale = Mathf.Min(
                cellWidth / (float)source.width, cellHeight / (float)source.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            int column = i % columns;
            int row = rows - 1 - i / columns;
            int x0 = column * cellWidth + (cellWidth - width) / 2;
            int y0 = row * cellHeight + (cellHeight - height) / 2;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color pixel = source.GetPixelBilinear(
                        (x + 0.5f) / width, (y + 0.5f) / height);
                    sheet.SetPixel(x0 + x, y0 + y, pixel);
                }
        }
        sheet.Apply();
        File.WriteAllBytes(
            Path.Combine(s_state.OutputDirectory, "contact-sheet.png"),
            sheet.EncodeToPNG());
        foreach (Texture2D image in images) UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(sheet);
    }

    private static string Label(WorkItem item) =>
        $"{item.Surface}/{item.Fixture} @ {item.Width}x{item.Height}" +
        (item.ForcePhone ? " · phone" : "") +
        (item.ExpandedText ? " · 130% copy" : "");

    private static string Layout(RunShell shell, WorkItem item) =>
        item.Surface switch
        {
            "wager" => shell.EditorWagerLayoutReport(),
            "deploy" => shell.EditorDeployLayoutReport(),
            "result" => shell.EditorResultLayoutReport(),
            "rotation" => shell.EditorRotationGuardReport(),
            _ => shell.EditorWorkbenchLayoutReport(),
        };

    private static bool Passed(string layout) =>
        !string.IsNullOrWhiteSpace(layout) &&
        (layout.StartsWith("Workbench QA: PASS", StringComparison.Ordinal) ||
         layout.StartsWith("Wager: PASS", StringComparison.Ordinal) ||
         layout.StartsWith("Deploy: PASS", StringComparison.Ordinal) ||
         layout.StartsWith("Result gate: PASS", StringComparison.Ordinal) ||
         layout.StartsWith("Rotation guard: PASS", StringComparison.Ordinal));

    private static void QueueFrame()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void Load()
    {
        string json = SessionState.GetString(SessionKey, "");
        s_state = string.IsNullOrWhiteSpace(json)
            ? new State()
            : JsonUtility.FromJson<State>(json) ?? new State();
    }

    private static void Save() =>
        SessionState.SetString(SessionKey, JsonUtility.ToJson(s_state));
}
#endif
