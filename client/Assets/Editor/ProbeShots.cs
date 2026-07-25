using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Targeted companion to RenderShots: captures specific (fixture, tick) moments instead of the
/// fixed contact-sheet grid, so in-flight FX (cast auras, projectiles, death bursts) can be
/// verified at exactly the ticks where the events fire. Probe list lives OUTSIDE the Syncthing
/// tree in %USERPROFILE%/warband-shots/probes.txt — one line per capture:
///   fixture tick [advanceSeconds]
/// e.g. "castfest 28 0.15". advanceSeconds overrides previewAdvanceSeconds for that capture.
/// MCP-drivable via ExecuteMenuItem("Warband/Render Probe Shots"); PNGs land beside the sheets
/// as {fixture}_t{tick}_probe.png.
/// </summary>
public static class ProbeShots
{
    [MenuItem("Warband/Render Probe Shots")]
    public static void RenderProbeShots()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<ReplayPlayer>();
        if (player == null) { Debug.LogError("[ProbeShots] no ReplayPlayer in the scene"); return; }
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[ProbeShots] no Camera.main to render from"); return; }

        string outDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "warband-shots");
        Directory.CreateDirectory(outDir);
        string listPath = Path.Combine(outDir, "probes.txt");
        if (!File.Exists(listPath)) { Debug.LogError($"[ProbeShots] no probe list: {listPath}"); return; }

        string original = player.replayFile;
        float originalAdvance = player.previewAdvanceSeconds;
        try
        {
            foreach (string raw in File.ReadAllLines(listPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[1], out int tick))
                {
                    Debug.LogWarning($"[ProbeShots] skipping malformed line: {line}");
                    continue;
                }
                player.previewAdvanceSeconds = parts.Length >= 3 &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float adv)
                    ? adv : originalAdvance;
                player.replayFile = "replays/" + parts[0] + ".bytes";
                player.BuildPreview(tick);
                string outPath = Path.Combine(outDir, $"{parts[0]}_t{tick}_probe.png");
                RenderTo(cam, outPath, 1600, 900);
                Debug.Log($"[ProbeShots] wrote {outPath}");
            }
        }
        finally
        {
            player.replayFile = original;
            player.previewAdvanceSeconds = originalAdvance;
            player.BuildPreview(24);
        }
    }

    private static void RenderTo(Camera cam, string path, int w, int h)
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
