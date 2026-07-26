using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Warband.Content;
using Debug = UnityEngine.Debug;

/// <summary>
/// Windows player builds for the friends playtest (roadmap item 8). Modelled directly on Shoota's
/// `ServerBuild.cs` — same self-describing-manifest handoff, same "manifest written LAST" rule — so
/// homeserv never has to wait for a synced ProjectSettings.asset to learn which version it is
/// publishing. warband has no server, so this is client-only.
///
/// **Artifacts land OUTSIDE the Syncthing tree** (`%USERPROFILE%/warband-builds/`), same rule as the
/// render captures: a multi-hundred-MB player build inside the synced repo would sync straight back
/// to homeserv and into git status. `deploy/ship-release.sh` scps them from there.
///
/// **The self-healing bit that matters.** Runtime-created board materials use URP Lit/Unlit and the
/// six hand-written Warband shaders. None has a reliable serialized material reference, so a player
/// build may strip them even though all eight resolve in the Editor. The first shipped build proved
/// URP Unlit was missing when every tracer/burst threw `new Material(null)`. A separate Unity player
/// behavior (UUM-136536) replaces CreatePrimitive's default material with InternalErrorShader, so
/// ReplayPlayer must explicitly replace it with registered URP Lit. Register every intended runtime
/// shader on every build and log additions. A build step cannot be forgotten; a wiki note can.
/// </summary>
public static class WarbandBuild
{
    private const string BootScene = "Assets/Scenes/Boot.unity";
    private const string GameScene = "Assets/Scenes/Game.unity";
    private const string ExeName = "Warband.exe";

    /// <summary>Every shader used by a runtime-created material. Keep the Warband entries in step
    /// with `Assets/Shaders/Warband/*.shader`; the build fails if any entry is missing.</summary>
    private static readonly string[] RuntimeShaders =
    {
        // ReplayPlayer explicitly replaces GameObject.CreatePrimitive's broken player material
        // with URP/Lit (UUM-136536); Burst/Tracer explicitly create URP/Unlit materials.
        // Both resolve by name at runtime, so neither may be stripped.
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Unlit",
        "Warband/Ring",
        "Warband/GroundFill",
        "Warband/Sigil",
        "Warband/Glow",
        "Warband/Particle",
        "Warband/Dissolve",
    };

    private static string BuildsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "warband-builds");
    private static string ClientDir => Path.Combine(BuildsRoot, "WindowsClient");
    private static string ManifestPath => Path.Combine(BuildsRoot, "release.json");

    [Serializable]
    private sealed class ReleaseArtifact
    {
        public string path;
        public string result;
        public long bytes;
        public int errors;
        public int warnings;
    }

    [Serializable]
    private sealed class ReleaseManifest
    {
        public int schemaVersion = 1;
        public string version;
        public string contentVersion;   // ADR 0008's fingerprint — which content this build fights with
        public string builtAtUtc;
        public string commit;
        public bool dirty;
        public string exe = ExeName;
        public ReleaseArtifact client;
    }

    [MenuItem("Warband/Build Windows Client")]
    public static void BuildWindowsClient()
    {
        DeletePath(ManifestPath);
        string version = StampVersion();

        try
        {
            EnsureRuntimeShadersAreIncluded();

            DeletePath(ClientDir);
            Directory.CreateDirectory(ClientDir);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var options = new BuildPlayerOptions
            {
                // Boot(0) → Game(1). GameBoot owns startup order from Boot; nothing else may be
                // scene 0 (four competing RuntimeInitializeOnLoadMethods is why two UIDocuments
                // once raced for input).
                scenes = new[] { BootScene, GameScene },
                locationPathName = Path.Combine(ClientDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            // BuildReport is owned by the pipeline — snapshot every value while it is alive.
            var artifact = new ReleaseArtifact
            {
                path = "WindowsClient",
                result = report.summary.result.ToString(),
                bytes = (long)report.summary.totalSize,
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
            };

            if (artifact.result != BuildResult.Succeeded.ToString() || artifact.errors > 0)
                throw new BuildFailedException(
                    $"Windows client v{version} reported {artifact.result} with {artifact.errors} errors.");

            var (commit, dirty) = ReadSourceRevision();
            var manifest = new ReleaseManifest
            {
                version = version,
                contentVersion = new Catalog().ContentVersion,
                builtAtUtc = DateTime.UtcNow.ToString("O"),
                commit = commit,
                dirty = dirty,
                client = artifact,
            };
            WriteManifestAtomically(manifest);

            Debug.Log($"[WarbandBuild] v{version} SUCCEEDED — {artifact.bytes / (1024 * 1024)}MB, " +
                      $"{artifact.warnings} warnings, content {manifest.contentVersion}.\n" +
                      $"  {ClientDir}\n  Publish from homeserv: make ship EXPECTED_VERSION={version}");
        }
        catch (Exception ex)
        {
            // No manifest means "nothing publishable" — ship-release.sh keys off exactly that, so a
            // failed build can never be shipped by mistake.
            DeletePath(ManifestPath);
            Debug.LogError($"[WarbandBuild] v{version} FAILED; no release manifest written.\n{ex}");
            throw;
        }
    }

    /// <summary>
    /// Report what a build WOULD do without spending twenty minutes on it. Exists because the
    /// shader-stripping trap is invisible until the build runs, and because a session driving this
    /// over MCP wants a cheap pre-check.
    /// </summary>
    [MenuItem("Warband/Build Preflight")]
    public static void Preflight()
    {
        var log = new List<string>();
        bool ok = true;
        void Check(string what, bool passed, string detail = "")
        {
            ok &= passed;
            log.Add($"{(passed ? "PASS" : "FAIL")}  {what}{(detail.Length > 0 ? " — " + detail : "")}");
        }

        Check("Boot scene exists", File.Exists(BootScene), BootScene);
        Check("Game scene exists", File.Exists(GameScene), GameScene);

        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        Check("active render pipeline is URP",
            pipeline != null && pipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"),
            pipeline != null ? pipeline.name : "none");

        foreach (string name in RuntimeShaders)
        {
            Shader shader = Shader.Find(name);
            Check($"shader '{name}' resolves in-editor", shader != null);
            if (shader != null)
                Check($"shader '{name}' is always-included", IsAlwaysIncluded(shader),
                    IsAlwaysIncluded(shader) ? "" : "would be STRIPPED from a player build");
        }

        // StreamingAssets ships verbatim, but only if it is actually there.
        Check("tuning.json present", File.Exists("Assets/StreamingAssets/tuning.json"));
        string replays = "Assets/StreamingAssets/replays";
        Check("replay fixtures present", Directory.Exists(replays) &&
            Directory.GetFiles(replays, "*.bytes").Length > 0,
            Directory.Exists(replays) ? Directory.GetFiles(replays, "*.bytes").Length + " fixtures" : "missing");

        Check("content version computes", !string.IsNullOrEmpty(new Catalog().ContentVersion),
            new Catalog().ContentVersion);
        Check("builds root is outside the Syncthing tree",
            !BuildsRoot.Replace('\\', '/').Contains("/warband/client"), BuildsRoot);

        string report = "[WarbandBuild preflight]\n" + string.Join("\n", log);
        if (ok) Debug.Log(report + "\n=> READY TO BUILD");
        else Debug.LogError(report + "\n=> NOT READY (failures above)");
    }

    /// <summary>
    /// Add any missing runtime-only shader to Always Included Shaders. Idempotent, and it logs
    /// additions so the first run leaves a record of what the build would otherwise have lost.
    /// </summary>
    private static void EnsureRuntimeShadersAreIncluded()
    {
        var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (graphics == null || graphics.Length == 0)
            throw new BuildFailedException("could not open ProjectSettings/GraphicsSettings.asset");

        var settings = new SerializedObject(graphics[0]);
        SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
        if (list == null)
            throw new BuildFailedException("GraphicsSettings has no m_AlwaysIncludedShaders array");

        var added = new List<string>();
        foreach (string name in RuntimeShaders)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
                throw new BuildFailedException(
                    $"shader '{name}' is missing from the project — it is resolved by Shader.Find at " +
                    "runtime, so a build would silently render nothing for it.");

            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
            if (present) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            added.Add(name);
        }

        if (added.Count == 0)
        {
            Debug.Log($"[WarbandBuild] all {RuntimeShaders.Length} runtime shaders already always-included.");
            return;
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.LogWarning($"[WarbandBuild] added {added.Count} shader(s) to Always Included Shaders — " +
                         $"a player build would have STRIPPED these: {string.Join(", ", added)}");
    }

    private static bool IsAlwaysIncluded(Shader shader)
    {
        var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (graphics == null || graphics.Length == 0) return false;
        SerializedProperty list = new SerializedObject(graphics[0]).FindProperty("m_AlwaysIncludedShaders");
        if (list == null) return false;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return true;
        return false;
    }

    /// <summary>Fresh version per build, same shape as Shoota's so the two read alike.</summary>
    private static string StampVersion()
    {
        string version = "0.1." + DateTime.Now.ToString("yyMMdd.HHmm");
        PlayerSettings.bundleVersion = version;
        return version;
    }

    private static (string Commit, bool Dirty) ReadSourceRevision()
    {
        // Windows has the checkout too (Syncthing), so ask git locally; a missing git is not a
        // build failure, just weaker provenance.
        try
        {
            string commit = Git("rev-parse --short HEAD");
            string status = Git("status --porcelain");
            return (string.IsNullOrEmpty(commit) ? "unknown" : commit, !string.IsNullOrEmpty(status));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WarbandBuild] could not read git provenance: {ex.Message}");
            return ("unknown", true);
        }
    }

    private static string Git(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git", args)
        {
            WorkingDirectory = Directory.GetParent(Application.dataPath)!.Parent!.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi);
        string output = p!.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit(10000);
        return output;
    }

    private static void WriteManifestAtomically(ReleaseManifest manifest)
    {
        Directory.CreateDirectory(BuildsRoot);
        string temp = ManifestPath + ".tmp";
        File.WriteAllText(temp, JsonUtility.ToJson(manifest, true));
        DeletePath(ManifestPath);
        File.Move(temp, ManifestPath);
    }

    private static void DeletePath(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
