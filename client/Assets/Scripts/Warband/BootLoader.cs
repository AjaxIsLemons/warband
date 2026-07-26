using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Boot scene's only job: be the explicit entry point, then hand off to Game.
///
/// It exists so that "what runs first" is a scene at build index 0 rather than an implicit race
/// between whichever objects happen to be in whichever scene was open when someone hit Play. It
/// deliberately holds nothing else — no camera, no systems — so it stays instant and can never
/// become a second place where game state lives.
/// </summary>
public sealed class BootLoader : MonoBehaviour
{
    public const string GameScene = "Game";

    private void Start() => SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
}
