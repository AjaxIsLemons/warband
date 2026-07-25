using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-shot, idempotent builder for the two states the death presentation needs on the shared
/// board Animator (fx-runtime "Death presentation" / P5):
///
///  · <b>Hit</b> — the flinch. Exit-time transition straight back to Idle, so it is a blip the
///    controller recovers from on its own; the renderer only ever crossfades INTO it.
///  · <b>Death</b> — the slump. NO outgoing transition at all: the clip is authored non-looping, so
///    the state parks on its last frame and holds the pose for as long as the corpse lingers.
///
/// Both carry the same <c>ActionSpeed</c> speed parameter the attack states use, because both are
/// fitted at runtime — the flinch to 1×, the slump to FxTune.deathLingerSeconds (DeathSequence).
///
/// Run it once from <b>Warband ▸ Build BoardUnit Controller</b>; the .controller is the committed
/// artifact. Re-running is safe: an existing state of either name is left exactly as it is.
/// </summary>
public static class BuildBoardUnitController
{
    private const string ControllerPath = "Assets/Resources/Board/KayKit/BoardUnit.controller";
    private const string ClipsPath = "Assets/Resources/Board/KayKit/Animations/Rig_Medium_General.fbx";
    private const string SpeedParameter = "ActionSpeed";

    [MenuItem("Warband/Build BoardUnit Controller")]
    public static void Build()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[BoardUnit] no controller at {ControllerPath}");
            return;
        }

        var machine = controller.layers[0].stateMachine;
        var idle = Find(machine, "Idle");
        if (idle == null)
        {
            Debug.LogError("[BoardUnit] no Idle state to return a flinch to — controller unexpected");
            return;
        }

        int added = 0;
        added += AddState(controller, machine, "Hit", "Hit_A", idle) ? 1 : 0;
        added += AddState(controller, machine, "Death", "Death_A", null) ? 1 : 0;

        if (added == 0)
        {
            Debug.Log("[BoardUnit] Hit + Death already present — nothing to do");
            return;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BoardUnit] added {added} state(s) to {Path.GetFileName(ControllerPath)} — commit the .controller");
    }

    /// <summary>Add one state driving <paramref name="clipName"/>, laid out under the existing ones.
    /// <paramref name="exitTo"/> null = no outgoing transition (Death holds its last frame).
    /// Returns false when the state already exists, which is what makes the menu item idempotent.</summary>
    private static bool AddState(AnimatorController controller, AnimatorStateMachine machine,
                                 string stateName, string clipName, AnimatorState exitTo)
    {
        if (Find(machine, stateName) != null) return false;

        var clip = LoadClip(clipName);
        if (clip == null)
        {
            Debug.LogError($"[BoardUnit] clip '{clipName}' not found in {ClipsPath} — skipping {stateName}");
            return false;
        }
        if (!HasParameter(controller, SpeedParameter))
        {
            Debug.LogError($"[BoardUnit] controller has no '{SpeedParameter}' float — skipping {stateName}");
            return false;
        }

        // Stack the new states below the row the existing ones sit on, so the graph stays readable
        // for whoever opens it in the Animator window.
        var state = machine.AddState(stateName, machine.entryPosition + new Vector3(320f, 220f + 70f * machine.states.Length, 0f));
        state.motion = clip;
        state.speedParameterActive = true;
        state.speedParameter = SpeedParameter;
        state.writeDefaultValues = true;

        if (exitTo != null)
        {
            var t = state.AddTransition(exitTo);
            t.hasExitTime = true;
            t.exitTime = 0.85f;      // the recovery half of the flinch blends into Idle
            t.duration = 0.12f;
            t.hasFixedDuration = true;
        }
        return true;
    }

    private static AnimatorState Find(AnimatorStateMachine machine, string name)
    {
        foreach (var c in machine.states) if (c.state != null && c.state.name == name) return c.state;
        return null;
    }

    private static bool HasParameter(AnimatorController controller, string name)
    {
        foreach (var p in controller.parameters) if (p.name == name) return true;
        return false;
    }

    private static AnimationClip LoadClip(string clipName)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ClipsPath))
            if (asset is AnimationClip clip && clip.name == clipName) return clip;
        return null;
    }
}
