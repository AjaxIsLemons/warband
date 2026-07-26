using System;
using System.IO;
using UnityEngine;
using Warband.Run;

/// <summary>
/// The host half of run persistence (roadmap item 7). `Warband.Run.RunSave` converts state ⇄ text
/// and is forbidden to touch the filesystem (ADR 0008: the run layer is pure); this owns the bytes,
/// the path, and every way writing to a real disk can fail.
///
/// **Atomic by construction.** A save is written to a temp file and then moved over the real one, so
/// a crash or a kill mid-write leaves the PREVIOUS good save intact rather than a half-written file.
/// That matters more here than it looks: losses are terminal (ADR 0019), so a corrupted save is not
/// an inconvenience, it is a destroyed run.
///
/// **Never throws at the caller.** A game that crashes because it could not save is worse than one
/// that could not save. Failures are logged and reported through the return value; the run keeps
/// going in memory.
/// </summary>
internal static class RunSaveFile
{
    private const string FileName = "run.save";
    private const string TempName = "run.save.tmp";

    private static string Dir => Application.persistentDataPath;
    private static string Path => System.IO.Path.Combine(Dir, FileName);
    private static string TempPath => System.IO.Path.Combine(Dir, TempName);

    /// <summary>Is there something to offer CONTINUE for? Cheap — does not parse.</summary>
    public static bool Exists()
    {
        try { return File.Exists(Path); }
        catch (Exception ex) { Debug.LogWarning($"[save] could not check for a save: {ex.Message}"); return false; }
    }

    public static bool Save(RunState state)
    {
        try
        {
            string text = RunSave.Write(state);
            Directory.CreateDirectory(Dir);
            File.WriteAllText(TempPath, text);
            // File.Move refuses an existing destination on some platforms; Replace needs the
            // destination to exist. Delete-then-move is the portable form, and the window it opens
            // is covered by TempPath still holding the new content.
            if (File.Exists(Path)) File.Delete(Path);
            File.Move(TempPath, Path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[save] could not write the run: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load and rebuild a controller, or null with a player-facing reason. A save this build cannot
    /// read is DELETED, deliberately: leaving it would offer CONTINUE forever and fail every time.
    /// </summary>
    public static RunController Load(IRunContent content, RunConfig cfg, out string problem)
    {
        problem = "";
        if (!Exists()) { problem = "No saved run."; return null; }
        try
        {
            var state = RunSave.Read(File.ReadAllText(Path));
            return RunController.Resume(state, content, cfg);
        }
        catch (RunSaveException ex)
        {
            // Expected class of failure: an older/newer format, or content that has since changed.
            Debug.LogWarning($"[save] discarding an unreadable save: {ex.Message}");
            problem = "Your saved run was made by a different build and could not be loaded.";
            Delete();
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[save] could not read the run: {ex.Message}");
            problem = "Your saved run could not be read.";
            Delete();
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(Path)) File.Delete(Path);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (Exception ex) { Debug.LogWarning($"[save] could not delete the save: {ex.Message}"); }
    }
}
