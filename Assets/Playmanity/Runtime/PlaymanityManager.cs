// Playmanity Unity SDK
// Drop this file into Assets/Playmanity/Runtime/PlaymanityManager.cs
//
// Put this component on a GameObject in your initial/startup scene.
// It initializes Playmanity once, creates/owns the ad controller, and then
// marks itself DontDestroyOnLoad so later scenes can use PlaymanityManager.Instance.

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Playmanity.Unity
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class PlaymanityManager : MonoBehaviour
    {
        public static PlaymanityManager Instance { get; private set; }

        [Header("Initialization")]
        [Tooltip("Your Playmanity game ID.")]
        public string gameId = "g_usmkbnwvdttysr8z96waypr7h4";

        [Tooltip("Keep this enabled for Unity Editor Play Mode testing without the desktop client.")]
        public bool useMockInEditor = true;

        [Tooltip("Force mock mode even if PLAYMANITY_SESSION exists.")]
        public bool forceMock = false;

        [Tooltip("Run a lightweight ping/get-user check during startup.")]
        public bool validateOnStart = false;

        [Header("Ad Defaults")]
        [Range(0.01f, 1f)] public float viewportRatio = 1f;
        public float autoCloseAfterSeconds = 0f;
        public bool openClickUrl = true;
        public bool finishOnClick = true;

        [Header("Prefab UI")]
        public bool autoCreateAdUi = true;
        public PlaymanityAdView adUiPrefab;
        public string adUiResourcePath = "Playmanity/PlaymanityUI";

        public PlaymanityAdController AdController { get; private set; }

        public PlaymanityClient Client
        {
            get
            {
                EnsureAdController();
                return AdController.Client;
            }
        }

        public static bool HasInstance
        {
            get { return Instance != null; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAdController();
        }

    private async void Start()
    {
        gameId = ResolveRuntimeGameId(gameId);

        if (IsAdSmokeTestEnabled())
        {
            RunSmokeTestAsync();
        }

        if (!validateOnStart)
        {
            return;
        }

        try
        {
            PlaymanityResponse response = await Client.PingAsync();
            if (!response.success)
                Debug.LogWarning("Playmanity startup validation failed: " + response.error);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Playmanity startup validation exception: " + ex.Message);
        }
    }

    private bool IsAdSmokeTestEnabled()
    {
        string envValue = System.Environment.GetEnvironmentVariable(
            "PLAYMANITY_AD_SMOKE_TEST"
        );

        if (
            !string.IsNullOrWhiteSpace(envValue) &&
            (envValue == "1" || envValue.Equals("true", System.StringComparison.OrdinalIgnoreCase))
        ) {
            return true;
        }

        string[] commandLineArgs = System.Environment.GetCommandLineArgs();
        foreach (string arg in commandLineArgs)
        {
            if (string.Equals(arg, "--playmanity-ad-smoke-test", System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async void RunSmokeTestAsync()
    {
        const int smokeTestTimeoutMs = 15000;
        Debug.Log("Playmanity smoke test started for gameId=" + gameId);

        try
        {
            PlaymanityResponse response = await Client.RequestAdAsync(smokeTestTimeoutMs);
            if (!response.success)
            {
                Debug.LogWarning($"Playmanity smoke test requestAd failed: {response.error}");

                return;
            }

            Debug.Log("Playmanity smoke test passed. AdId=" + response.adId);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Playmanity smoke test exception: " + ex.Message);
        }
    }

    private string ResolveRuntimeGameId(string defaultGameId)
    {
        string environmentGameId = System.Environment.GetEnvironmentVariable(
            "PLAYMANITY_GAME_ID"
        );

        if (!string.IsNullOrWhiteSpace(environmentGameId))
        {
            return environmentGameId.Trim();
        }

        return defaultGameId;
    }

        private void OnValidate()
        {
            if (Application.isPlaying && Instance == this)
                ApplySettingsToAdController();
        }

        public void EnsureAdController()
        {
            if (AdController == null)
            {
                AdController = GetComponent<PlaymanityAdController>();
                if (AdController == null)
                    AdController = gameObject.AddComponent<PlaymanityAdController>();
            }

            ApplySettingsToAdController();
        }

        public void ApplySettingsToAdController()
        {
            if (AdController == null)
                return;

            AdController.Configure(gameId, useMockInEditor, forceMock);

            AdController.viewportRatio = viewportRatio;
            AdController.autoCloseAfterSeconds = autoCloseAfterSeconds;
            AdController.openClickUrl = openClickUrl;
            AdController.finishOnClick = finishOnClick;
            AdController.autoCreateUi = autoCreateAdUi;
            AdController.adViewPrefab = adUiPrefab;
            AdController.adViewResourcePath = adUiResourcePath;
        }

        public Task<PlaymanityResponse> RequestAdAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureAdController();
            return AdController.RequestAdAsync(cancellationToken);
        }

        public Task<PlaymanityAdResult> ShowLoadedAdAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureAdController();
            return AdController.ShowLoadedAdAsync(cancellationToken);
        }

        public Task<PlaymanityAdResult> RequestAndShowAdAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureAdController();
            return AdController.RequestAndShowAdAsync(cancellationToken);
        }

        public Task<PlaymanityAdResult> WaitForCurrentAdAsync()
        {
            EnsureAdController();
            return AdController.WaitForCurrentAdAsync();
        }

        public void CloseAd()
        {
            if (AdController != null)
                AdController.CloseAd();
        }
    }
}
