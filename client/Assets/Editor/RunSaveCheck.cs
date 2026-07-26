using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Warband.Content;
using Warband.Run;
using Warband.Sim;

/// <summary>
/// Committed verification harness for run save/resume (roadmap item 7). Editor-only, MCP-drivable
/// via `ExecuteMenuItem("Warband/Verify Run Save")`.
///
/// **Why this exists rather than a unit test.** The headless suite already proves the FORMAT (write ⇄
/// read, identical playout, refusal of corrupt saves). What it cannot reach is the platform: the real
/// `Application.persistentDataPath` on Windows, real `File` IO through it, and whether Windows text
/// handling perturbs a format that separates records with `\n`. Those are exactly the things that
/// look fine on Linux and break in a player build.
///
/// It deliberately does NOT enter Play Mode: `Unity_RunCommand` cannot reference Warband plugin
/// types (hence a real Editor script), and an unfocused editor idles the player loop, so anything
/// frame-driven is unverifiable unattended. This checks the save layer, not the shell wiring —
/// the menu button and the autosave hook still need a human click-through.
/// </summary>
public static class RunSaveCheck
{
    [MenuItem("Warband/Verify Run Save")]
    public static void Verify()
    {
        var log = new List<string>();
        bool ok = true;

        void Check(string what, bool passed, string detail = "")
        {
            ok &= passed;
            log.Add($"{(passed ? "PASS" : "FAIL")}  {what}{(detail.Length > 0 ? " — " + detail : "")}");
        }

        string dir = Application.persistentDataPath;
        string path = Path.Combine(dir, "run.save.verify");
        string temp = path + ".tmp";

        try
        {
            var cat = new Catalog();
            var cfg = new RunConfig();

            // A real run, driven a couple of beats so the state has heroes, offers and growth in it.
            var run = new RunController(41, cat, RunHarness.StarterWarband(cat, cfg), cfg);
            int beats = 0;
            while (run.State.Phase == RunPhase.Planning && beats < 2)
            {
                if (run.State.PendingSpec != null) { run.ChooseSpec(0); continue; }
                switch (run.CurrentNodeKind)
                {
                    case NodeKind.Event: run.ResolveInterlude(InterludePath.Treasury); break;
                    case NodeKind.Fight: run.ResolveFight(FightTier.Stable, FrontPlacement(run)); break;
                    case NodeKind.Boss: run.ResolveBoss(FrontPlacement(run)); break;
                }
                beats++;
            }
            log.Add($"      run at act {run.State.Act} beat {run.State.NodeIndex} " +
                    $"phase {run.State.Phase}, {run.State.Field.Count} heroes, {run.State.Sand} Sand");

            // --- the real file path, the real IO, the real atomic write ------------------
            string text = RunSave.Write(run.State);
            Directory.CreateDirectory(dir);
            File.WriteAllText(temp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);

            Check("save file exists at persistentDataPath", File.Exists(path), path);
            Check("temp file was consumed by the move", !File.Exists(temp));
            var info = new FileInfo(path);
            Check("save is non-trivial", info.Length > 200, $"{info.Length} bytes");

            string readBack = File.ReadAllText(path);
            Check("bytes survive Windows text IO unchanged", readBack == text,
                readBack == text ? "" : $"wrote {text.Length} chars, read {readBack.Length}");
            Check("no CR was injected into the record separator", !readBack.Contains("\r"),
                readBack.Contains("\r") ? "found CR — Read() normalizes, but the writer should not emit it" : "");

            // --- resume through the real DLLs -------------------------------------------
            var resumed = RunController.Resume(RunSave.Read(readBack), cat, cfg);
            Check("resumed act/beat/phase match", resumed.State.Act == run.State.Act
                && resumed.State.NodeIndex == run.State.NodeIndex
                && resumed.State.Phase == run.State.Phase);
            Check("resumed Sand matches", resumed.State.Sand == run.State.Sand,
                $"{resumed.State.Sand} vs {run.State.Sand}");
            Check("resumed warband matches",
                resumed.State.Field.Select(h => h.ChassisId + "/" + h.Rank)
                    .SequenceEqual(run.State.Field.Select(h => h.ChassisId + "/" + h.Rank)),
                string.Join(",", resumed.State.Field.Select(h => h.ChassisId + "/" + h.Rank)));
            Check("resumed shop stock matches",
                resumed.State.ShopOffers.Select(o => o == null ? "-" : o.Kind + ":" + o.Id)
                    .SequenceEqual(run.State.ShopOffers.Select(o => o == null ? "-" : o.Kind + ":" + o.Id)));

            // The encounter the player was looking at must not have been re-rolled.
            if (run.State.Phase == RunPhase.Planning && run.CurrentNodeKind != NodeKind.Event)
            {
                var a = run.PreviewBrief(FightTier.Fraying);
                var b = resumed.PreviewBrief(FightTier.Fraying);
                Check("resumed encounter is the same encounter", a.Id == b.Id, $"{a.Id} vs {b.Id}");
                Check("resumed encounter roster is identical",
                    a.Units.Select(u => u.Name + u.MaxHp).SequenceEqual(b.Units.Select(u => u.Name + u.MaxHp)));
            }

            // --- a save this build cannot read must be refused, not half-loaded ---------
            try
            {
                RunSave.Read("warband-run-save v99\nact=1\n");
                Check("a future save format is refused", false, "it loaded");
            }
            catch (RunSaveException) { Check("a future save format is refused", true); }

            File.Delete(path);
            Check("save file cleans up", !File.Exists(path));
        }
        catch (Exception ex)
        {
            ok = false;
            log.Add($"FAIL  threw: {ex.GetType().Name}: {ex.Message}");
        }

        string report = "[RunSaveCheck]\n" + string.Join("\n", log);
        if (ok) Debug.Log(report + "\n=> ALL PASS");
        else Debug.LogError(report + "\n=> FAILURES ABOVE");
    }

    /// <summary>Front rank first, then back — enough to resolve a fight; this harness is not
    /// measuring placement quality.</summary>
    private static List<Hex> FrontPlacement(RunController run)
    {
        var slots = new List<Hex>();
        int n = run.State.Field.Count;
        for (int i = 0; i < n; i++)
            slots.Add(Hex.FromRowCol(i == 0 ? 3 : 1, 1 + i));
        return slots;
    }
}
