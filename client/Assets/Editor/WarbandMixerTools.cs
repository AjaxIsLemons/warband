using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Creates <c>Assets/Resources/Audio/GameMixer.mixer</c> from script — warband's bus tree and its
/// exposed params — because <c>UnityEditor.Audio.AudioMixerController</c> is internal and a
/// <c>.mixer</c> cannot be authored through any public API. Everything below is reflection over
/// that type. Ported from Shoota's <c>GameMixerTools</c>, which proved the approach.
///
/// Bus tree (`Design/audio.md` §5.0):
/// <code>
///   Master
///   ├── UI                 ← UiVol.  NEVER ducked: a click is direct feedback to the player.
///   └── Board              ← BoardVol
///       ├── Ducked         ← BoardDuck. The duck bus.
///       │   ├── Cast
///       │   ├── Impact
///       │   └── State
///       └── Decisive       ← sibling of Ducked, so death/crit ride OVER the duck untouched
/// </code>
///
/// Why `Ducked` exists as an intermediate bus: a mixer parameter can only be exposed ONCE, so
/// `BoardVol` and `BoardDuck` cannot both sit on `Board`. Splitting them lets the two compose in
/// the signal chain while leaving `Decisive` — a sibling, not a child — outside the duck entirely.
/// That is the whole "what stands out / what steps back" mechanism in §5.3.
///
/// Idempotent: re-running reuses the asset and skips groups and params that already exist. Logs
/// every group created and param exposed, so an MCP-driven run is auditable from the console.
/// </summary>
public static class WarbandMixerTools
{
    private const string Dir = "Assets/Resources/Audio";
    private const string Path = Dir + "/GameMixer.mixer";

    private static readonly string[] LeafGroups =
        { "UI", "Cast", "Impact", "State", "Decisive" };

    /// <summary>
    /// Self-heal on domain reload: if the mixer asset is absent, build it. Deferred through
    /// <c>delayCall</c> because the AssetDatabase is not reliably ready during
    /// <c>InitializeOnLoadMethod</c> itself.
    ///
    /// This exists because the project is driven by agents over the MCP relay, where
    /// <c>Unity_RunCommand</c> compiles into a library and therefore rejects top-level statements —
    /// so "just call the menu item" is not reliably available from outside the Editor. Making the
    /// asset self-create removes the Editor round-trip entirely: sync the script, Unity compiles,
    /// the mixer exists. Idempotent by the same existence check the menu item uses, so it costs one
    /// `LoadAssetAtPath` per reload and nothing else.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void EnsureMixerOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(Path) != null) return;
            Debug.Log("[GameMixer] No mixer asset found — creating it on load.");
            CreateGameMixer();
        };
    }

    [MenuItem("Warband/Audio/Create Game Mixer")]
    public static void CreateGameMixer()
    {
        var log = new StringBuilder("[GameMixer] ");

        Type ctrlType = FindType("UnityEditor.Audio.AudioMixerController");
        Type groupType = FindType("UnityEditor.Audio.AudioMixerGroupController");
        if (ctrlType == null || groupType == null)
        {
            Debug.LogError("[GameMixer] Could not resolve AudioMixerController / "
                + "AudioMixerGroupController via reflection. Aborting.");
            return;
        }

        EnsureFolder(Dir);

        object controller = AssetDatabase.LoadAssetAtPath(Path, ctrlType);
        if (controller == null)
        {
            MethodInfo createAt = ctrlType.GetMethod("CreateMixerControllerAtPath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (createAt == null)
            {
                Debug.LogError("[GameMixer] No CreateMixerControllerAtPath(string). Static members:\n"
                    + DumpMembers(ctrlType, true));
                return;
            }
            controller = createAt.Invoke(null, new object[] { Path });
            if (controller == null)
            {
                Debug.LogError("[GameMixer] CreateMixerControllerAtPath returned null.");
                return;
            }
            log.Append("created asset; ");
        }
        else log.Append("reusing existing asset; ");

        object master = ctrlType.GetProperty("masterGroup")?.GetValue(controller);
        if (master == null)
        {
            Debug.LogError("[GameMixer] No masterGroup on the controller. Instance members:\n"
                + DumpMembers(ctrlType, false));
            return;
        }

        object ui = EnsureGroup(ctrlType, groupType, controller, master, "UI", log);
        object board = EnsureGroup(ctrlType, groupType, controller, master, "Board", log);
        object ducked = EnsureGroup(ctrlType, groupType, controller, board, "Ducked", log);
        EnsureGroup(ctrlType, groupType, controller, ducked, "Cast", log);
        EnsureGroup(ctrlType, groupType, controller, ducked, "Impact", log);
        EnsureGroup(ctrlType, groupType, controller, ducked, "State", log);
        EnsureGroup(ctrlType, groupType, controller, board, "Decisive", log);

        Expose(ctrlType, groupType, controller, master, "MasterVol", log);
        Expose(ctrlType, groupType, controller, ui, "UiVol", log);
        Expose(ctrlType, groupType, controller, board, "BoardVol", log);
        Expose(ctrlType, groupType, controller, ducked, "BoardDuck", log);

        EditorUtility.SetDirty((UnityEngine.Object)controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        // Verify it round-trips as a RUNTIME AudioMixer resolving the exact group names SfxPlayer
        // looks up — creating the asset is not the same as the game being able to route to it.
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
        var missing = new List<string>();
        if (mixer != null)
            foreach (string n in LeafGroups)
                if (mixer.FindMatchingGroups(n).Length == 0)
                    missing.Add(n);

        string verdict = mixer == null
            ? "FAILED — asset does not load as a runtime AudioMixer"
            : missing.Count == 0
                ? $"OK — all {LeafGroups.Length} buses resolve via FindMatchingGroups"
                : "INCOMPLETE — unresolved: " + string.Join(", ", missing);
        Debug.Log(log.ToString().TrimEnd(';', ' ') + $".\n[GameMixer] {Path} → {verdict}");
    }

    // NOTE: there is deliberately no "Refresh SFX" menu item here. It would want to call
    // SfxPlayer.ClearCache(), and editor scripts compile into Assembly-CSharp-EDITOR, which cannot
    // see `internal` types in Assembly-CSharp — SfxPlayer is internal like the rest of the client's
    // presentation layer (ReplayPlayer is public only because it is a scene MonoBehaviour). Making
    // the type public to serve a dev convenience is the wrong trade; re-baked clips are picked up
    // by the domain reload that any script edit or asset import triggers anyway.

    // --- groups --------------------------------------------------------------------------------

    private static object EnsureGroup(Type ctrlType, Type groupType, object controller,
        object parent, string name, StringBuilder log)
    {
        if (parent == null) return null;
        object existing = FindChild(groupType, parent, name);
        if (existing != null) return existing;

        MethodInfo createNew = ctrlType.GetMethod("CreateNewGroup",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(bool) }, null);
        MethodInfo addChild = ctrlType.GetMethod("AddChildToParent",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { groupType, groupType }, null);
        if (createNew == null || addChild == null)
        {
            Debug.LogError("[GameMixer] Missing CreateNewGroup/AddChildToParent. Instance members:\n"
                + DumpMembers(ctrlType, false));
            return null;
        }

        object group = createNew.Invoke(controller, new object[] { name, false });
        ((UnityEngine.Object)group).name = name;
        addChild.Invoke(controller, new[] { group, parent });

        try
        {
            ctrlType.GetMethod("AddGroupToCurrentView",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { groupType }, null)
                ?.Invoke(controller, new[] { group });
        }
        catch { /* mixer-window view bookkeeping only; never fatal */ }

        // The group AND its default Attenuation effect must persist as sub-assets or the .mixer
        // serializes broken. Guard for Unity versions where CreateNewGroup already persisted them
        // (AddObjectToAsset on an already-persistent object throws).
        AddSubAsset((UnityEngine.Object)group, (UnityEngine.Object)controller);
        if (groupType.GetProperty("effects")?.GetValue(group) is Array effects)
            foreach (object eff in effects)
                if (eff is UnityEngine.Object o)
                    AddSubAsset(o, (UnityEngine.Object)controller);

        log.Append("group " + name + "; ");
        return group;
    }

    private static void AddSubAsset(UnityEngine.Object obj, UnityEngine.Object owner)
    {
        if (obj != null && !AssetDatabase.Contains(obj))
            AssetDatabase.AddObjectToAsset(obj, owner);
    }

    private static object FindChild(Type groupType, object group, string name)
    {
        if (((UnityEngine.Object)group).name == name) return group;
        if (groupType.GetProperty("children")?.GetValue(group) is Array children)
            foreach (object child in children)
            {
                object found = FindChild(groupType, child, name);
                if (found != null) return found;
            }
        return null;
    }

    // --- exposed params ------------------------------------------------------------------------

    private static void Expose(Type ctrlType, Type groupType, object controller,
        object group, string paramName, StringBuilder log)
    {
        if (group == null) return;
        try
        {
            PropertyInfo exposedProp = ctrlType.GetProperty("exposedParameters");
            Array current = (Array)exposedProp.GetValue(controller);
            Type expType = exposedProp.PropertyType.GetElementType();
            FieldInfo fName = expType.GetField("name");
            FieldInfo fGuid = expType.GetField("guid");

            foreach (object e in current)
                if ((string)fName.GetValue(e) == paramName)
                {
                    log.Append("param " + paramName + " (exists); ");
                    return;
                }

            MethodInfo guidForVol = groupType.GetMethod("GetGUIDForVolume",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (guidForVol == null)
            {
                Debug.LogError("[GameMixer] No GetGUIDForVolume on group. GUID/Volume members:\n"
                    + DumpMembers(groupType, false, "GUID", "Volume"));
                return;
            }

            object entry = Activator.CreateInstance(expType);
            fName.SetValue(entry, paramName);
            fGuid.SetValue(entry, guidForVol.Invoke(group, null));

            Array next = Array.CreateInstance(expType, current.Length + 1);
            Array.Copy(current, next, current.Length);
            next.SetValue(entry, current.Length);
            exposedProp.SetValue(controller, next);
            log.Append("param " + paramName + "; ");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameMixer] Exposing '{paramName}' failed: {ex.Message}\n{ex}");
        }
    }

    // --- reflection utils ----------------------------------------------------------------------

    private static Type FindType(string fullName)
    {
        Type t = Type.GetType(fullName + ", UnityEditor")
            ?? Type.GetType(fullName + ", UnityEditor.CoreModule");
        if (t != null) return t;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = a.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    private static void EnsureFolder(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(dir);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static string DumpMembers(Type t, bool statics, params string[] filters)
    {
        BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic
            | (statics ? BindingFlags.Static : BindingFlags.Instance);
        IEnumerable<string> names = t.GetMethods(f).Select(m => m.Name)
            .Concat(t.GetProperties(f).Select(p => p.Name)).Distinct();
        if (filters != null && filters.Length > 0)
            names = names.Where(n => filters.Any(x =>
                n.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0));
        return string.Join(", ", names.OrderBy(n => n));
    }
}
