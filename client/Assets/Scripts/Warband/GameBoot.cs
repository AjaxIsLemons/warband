using UnityEngine;

/// <summary>
/// The single entry point. Four systems used to spawn themselves independently from
/// <c>[RuntimeInitializeOnLoadMethod]</c> with no ordering guarantee between them, which is how
/// two full-screen UIDocuments ended up racing for input and why the shell had to disable the
/// skirmish controller defensively at runtime. Startup order is a decision, so it is made here,
/// once, in the open.
///
/// Adding a system: add a line to <see cref="Boot"/>. Do NOT give it its own
/// RuntimeInitializeOnLoadMethod — that is the pattern this class exists to replace.
/// </summary>
public static class GameBoot
{
    /// <summary>
    /// What the board is for in this session. The board is a heavy object — camera framing,
    /// lights, post-processing, pooled FX — so who drives it must be unambiguous.
    /// </summary>
    public enum BoardRole
    {
        /// <summary>Driven by the run shell: idle until a fight is deployed or watched.</summary>
        Driven,
        /// <summary>A fixture viewer: the board plays its replay file on start.</summary>
        Viewer,
    }

    /// <summary>
    /// Hook, don't spawn. `RuntimeInitializeOnLoadMethod` fires once after the FIRST scene loads —
    /// which is Boot, a scene that deliberately holds nothing. Spawning there would put every
    /// system into a scene that is about to be unloaded. So wait for Game.
    ///
    /// The active-scene check covers the case developers actually live in: pressing Play with the
    /// Game scene already open, skipping Boot entirely.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == BootLoader.GameScene)
            Boot();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                      UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == BootLoader.GameScene) Boot();
    }

    private static void Boot()
    {
        // The shell owns the game. It is spawned FIRST so that anything it needs to configure —
        // the board included — is configured before other systems observe it.
        Spawn<RunShell>("~RunShell");

        // The dev cockpit comes last: it reads state the others own, and it should never be the
        // reason a frame of gameplay behaves differently. The hover card that used to spawn here
        // was deleted 2026-07-29 — unit inspection is one pinned card, opened by click.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Spawn<DebugMenu>("~DebugMenu");
#endif
    }

    private static T Spawn<T>(string name) where T : MonoBehaviour
    {
        var existing = Object.FindFirstObjectByType<T>();
        if (existing != null) return existing;
        return new GameObject(name).AddComponent<T>();
    }
}
