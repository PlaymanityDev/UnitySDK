// Example startup script.
// Put this on the Playmanity setup scene to continue into the next build scene
// after PlaymanityManager has initialized.

using Playmanity.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-9990)]
public sealed class PlaymanityLoadNextSceneAfterSetup : MonoBehaviour
{
    [Tooltip("Load the scene immediately after the active scene in Build Settings.")]
    public bool loadNextSceneInBuildOrder = true;

    [Tooltip("Fallback scene name used when there is no next Build Settings scene.")]
    public string fallbackSceneName = "AdAfterFiveSecondsExample";

    private void Start()
    {
        PlaymanityManager manager = PlaymanityManager.Instance;
        if (manager == null)
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            manager = FindAnyObjectByType<PlaymanityManager>();
#else
            manager = FindObjectOfType<PlaymanityManager>();
#endif

        if (manager != null)
            manager.EnsureAdController();

        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
        if (loadNextSceneInBuildOrder &&
            currentBuildIndex >= 0 &&
            currentBuildIndex + 1 < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(currentBuildIndex + 1);
            return;
        }

        if (!string.IsNullOrEmpty(fallbackSceneName))
            SceneManager.LoadScene(fallbackSceneName);
    }
}
