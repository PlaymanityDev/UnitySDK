// Optional helper for your initial/startup scene.
// You can either attach PlaymanityManager directly, or attach this script.
// This script simply ensures a PlaymanityManager exists and configures it.

using Playmanity.Unity;
using UnityEngine;

public sealed class PlaymanityStartupSceneBootstrap : MonoBehaviour
{
    public string gameId = "g_usmkbnwvdttysr8z96waypr7h4";
    public bool useMockInEditor = true;
    public bool forceMock = false;

    private void Awake()
    {
        PlaymanityManager manager = PlaymanityManager.Instance;

        if (manager == null)
        {
            GameObject go = new GameObject("PlaymanityManager");
            manager = go.AddComponent<PlaymanityManager>();
        }

        manager.gameId = gameId;
        manager.useMockInEditor = useMockInEditor;
        manager.forceMock = forceMock;
        manager.EnsureAdController();
    }
}
