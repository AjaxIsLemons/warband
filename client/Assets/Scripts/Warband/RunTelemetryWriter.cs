using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Item 19's host half: <c>Warband.Run.RunTelemetry</c> formats the lines, this owns the bytes —
/// append beside run.save, POST the finished run to the site. Telemetry must never break the
/// game, so every IO path here is fail-silent by design: the worst outcome of a full disk or a
/// dead network is a quieter log, never an exception in a purchase.
/// </summary>
internal sealed class RunTelemetryWriter
{
    private const string FileName = "runlog.jsonl";
    private const string UploadUrl = "https://warband.inhouseboyz.com/api/runlog";
    // A spam gate, not a secret — anyone who extracts it can post lines, which is the same trust
    // we extend by accepting playtest logs at all. It exists to stop drive-by scanner traffic.
    private const string UploadKey = "warband-playtest-1";

    // This run's lines, kept for the end-of-run upload. The FILE accumulates every run on the
    // machine; the POST carries exactly one finished run.
    private readonly List<string> _lines = new List<string>();

    public static string PathOnDisk =>
        Path.Combine(Application.persistentDataPath, FileName);

    public void Append(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        _lines.Add(line);
        try { File.AppendAllText(PathOnDisk, line + "\n"); }
        catch (Exception) { /* fail-silent by design */ }
    }

    /// <summary>Fire-and-forget: the local file is the source of truth, the POST is a copy.
    /// No retry, no queue — a lost upload is recoverable from the friend's disk if it matters.</summary>
    public IEnumerator Upload()
    {
        if (_lines.Count == 0) yield break;
        byte[] body = Encoding.UTF8.GetBytes(string.Join("\n", _lines));
        using (var req = new UnityWebRequest(UploadUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-ndjson");
            req.SetRequestHeader("X-Warband-Key", UploadKey);
            req.timeout = 10;
            yield return req.SendWebRequest();
        }
    }
}
