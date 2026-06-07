// Playmanity Unity SDK
// Drop this file into Assets/Playmanity/Runtime/PlaymanityAdController.cs
//
// This component adds higher-level ad controls:
// - request an ad
// - show the ad in a prefab-backed Unity UI overlay
// - wait until the ad finishes
// - report impression after visible exposure time
// - request click intent, report click, and open the click URL
//
// It can be used in Unity Editor Play Mode with mock responses.

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace Playmanity.Unity
{
    public enum PlaymanityAdFinishReason
    {
        Unknown,
        Closed,
        Clicked,
        AutoClosed,
        Failed,
        Cancelled
    }

    [Serializable]
    public sealed class PlaymanityAdResult
    {
        public bool success;
        public bool clicked;
        public bool impressionReported;
        public PlaymanityAdFinishReason reason;
        public PlaymanityResponse ad;
        public string error;
    }

    public sealed class PlaymanityAdController : MonoBehaviour
    {
        private const int ImpressionExposureMs = 2000;

        [Header("Playmanity")]
        [Tooltip("Your Playmanity game ID.")]
        public string gameId = "g_usmkbnwvdttysr8z96waypr7h4";

        [Tooltip("Use local mock responses in Unity Editor when PLAYMANITY_SESSION is missing.")]
        public bool useMockInEditor = true;

        [Tooltip("Force mock mode even if PLAYMANITY_SESSION exists.")]
        public bool forceMock = false;

        [Header("Ad Behaviour")]
        [Range(0.01f, 1f)]
        [Tooltip("Viewport ratio sent with reportAdImpression. Fullscreen overlay should usually be 1.")]
        public float viewportRatio = 1f;

        [Tooltip("Automatically finish and close the ad after this many seconds. Set 0 to disable.")]
        public float autoCloseAfterSeconds = 0f;

        [Tooltip("Open the ad click URL after reportAdClick succeeds.")]
        public bool openClickUrl = true;

        [Tooltip("Finish the ad immediately after a successful ad click.")]
        public bool finishOnClick = true;

        [Header("UI")]
        [Tooltip("Instantiate the prefab-backed ad UI automatically when no view is assigned.")]
        public bool autoCreateUi = true;

        [Tooltip("Prefab-backed ad view. This is preferred over the legacy manually wired fields below.")]
        public PlaymanityAdView adView;

        [Tooltip("Optional prefab override. If empty, the SDK loads Resources/Playmanity/PlaymanityUI.")]
        public PlaymanityAdView adViewPrefab;

        [Tooltip("Resources path used when the ad view is auto-created.")]
        public string adViewResourcePath = "Playmanity/PlaymanityUI";

        [Header("Legacy UI Bindings")]
        public Canvas adCanvas;
        public CanvasGroup adCanvasGroup;
        public RawImage mediaImage;
        public Text titleText;
        public Text descriptionText;
        public Text statusText;
        public Button clickButton;
        public Button closeButton;

        private PlaymanityClient _client;
        private PlaymanityResponse _loadedAd;
        private PlaymanityResponse _currentAd;
        private TaskCompletionSource<PlaymanityAdResult> _finishSource;
        private Coroutine _impressionCoroutine;
        private Coroutine _autoCloseCoroutine;
        private Coroutine _cancellationCoroutine;
        private Texture2D _mockTexture;
        private bool _impressionReported;
        private bool _clicked;
        private bool _clickInFlight;
        private string _status = "Idle";
        private PlaymanityAdView _boundAdView;
        private bool _legacyButtonsBound;

        public PlaymanityClient Client
        {
            get
            {
                if (_client == null)
                {
                    PlaymanityClientOptions options = new PlaymanityClientOptions
                    {
                        useMockInEditor = useMockInEditor,
                        forceMock = forceMock
                    };

                    _client = new PlaymanityClient(gameId, null, PlaymanityClient.DefaultPipeName, options);
                }

                return _client;
            }
        }

        public PlaymanityResponse LoadedAd
        {
            get { return _loadedAd; }
        }

        public bool IsShowingAd
        {
            get { return _finishSource != null; }
        }

        /// <summary>
        /// Updates controller settings from the persistent manager/initializer.
        /// If the low-level client was already created and settings changed,
        /// it is recreated so mock/live mode and game ID stay correct.
        /// </summary>
        public void Configure(
            string gameId,
            bool useMockInEditor,
            bool forceMock)
        {
            bool changed =
                this.gameId != gameId ||
                this.useMockInEditor != useMockInEditor ||
                this.forceMock != forceMock;

            this.gameId = gameId;
            this.useMockInEditor = useMockInEditor;
            this.forceMock = forceMock;

            if (changed && _client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        private bool HasLegacyUi
        {
            get
            {
                return adCanvas != null ||
                       adCanvasGroup != null ||
                       mediaImage != null ||
                       titleText != null ||
                       descriptionText != null ||
                       statusText != null ||
                       clickButton != null ||
                       closeButton != null;
            }
        }

        private void EnsureAdView()
        {
            if (adView != null)
            {
                adView.AutoAssignReferences();
                return;
            }

            adView = GetComponentInChildren<PlaymanityAdView>(true);
            if (adView != null)
            {
                adView.AutoAssignReferences();
                return;
            }

#if UNITY_2023_1_OR_NEWER
            adView = FindAnyObjectByType<PlaymanityAdView>(FindObjectsInactive.Include);
#else
            adView = FindObjectOfType<PlaymanityAdView>(true);
#endif
            if (adView != null)
            {
                adView.AutoAssignReferences();
                return;
            }

            if (!autoCreateUi || HasLegacyUi)
                return;

            PlaymanityAdView prefab = adViewPrefab;
            if (prefab == null && !string.IsNullOrEmpty(adViewResourcePath))
                prefab = Resources.Load<PlaymanityAdView>(adViewResourcePath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "Playmanity ad UI prefab was not found. Assign PlaymanityAdController.adView or place PlaymanityUI at Resources/Playmanity/PlaymanityUI.");
                return;
            }

            EnsureEventSystem();
            adView = Instantiate(prefab, transform, false);
            adView.name = prefab.name;
            adView.AutoAssignReferences();
        }

        private void BindUiEvents()
        {
            if (adView != null)
            {
                UnbindLegacyButtons();

                if (_boundAdView != adView)
                {
                    if (_boundAdView != null)
                        _boundAdView.Unbind();

                    adView.Bind(OnClickAdButton, CloseAd);
                    _boundAdView = adView;
                }

                return;
            }

            if (_legacyButtonsBound)
                return;

            if (clickButton != null)
                clickButton.onClick.AddListener(OnClickAdButton);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseAd);

            _legacyButtonsBound = clickButton != null || closeButton != null;
        }

        private void UnbindUiEvents()
        {
            if (_boundAdView != null)
            {
                _boundAdView.Unbind();
                _boundAdView = null;
            }

            UnbindLegacyButtons();
        }

        private void UnbindLegacyButtons()
        {
            if (!_legacyButtonsBound)
                return;

            if (clickButton != null)
                clickButton.onClick.RemoveListener(OnClickAdButton);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(CloseAd);

            _legacyButtonsBound = false;
        }

        private void Awake()
        {
            if (autoCreateUi)
                EnsureAdView();

            BindUiEvents();
            HideAdUi();
        }

        private void OnDestroy()
        {
            UnbindUiEvents();

            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }

            if (_mockTexture != null)
                Destroy(_mockTexture);
        }

        public async Task<PlaymanityResponse> RequestAdAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _status = "Requesting ad...";
            PlaymanityResponse response = await Client.RequestAdAsync(
                PlaymanityClient.DefaultTimeoutMs,
                cancellationToken);

            if (response.success)
            {
                _loadedAd = response;
                _status = "Ad loaded: " + response.title;
                SetUiStatus(_status, false);
            }
            else
            {
                _status = "Request failed: " + FormatResponseError(response);
                SetUiStatus(_status, true);
                Debug.LogWarning("Playmanity requestAd failed: " + FormatResponseError(response));
            }

            return response;
        }

        public async Task<PlaymanityAdResult> RequestAndShowAdAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            PlaymanityResponse response = await RequestAdAsync(cancellationToken);
            if (!response.success)
            {
                return new PlaymanityAdResult
                {
                    success = false,
                    reason = PlaymanityAdFinishReason.Failed,
                    ad = response,
                    error = response.error
                };
            }

            return await ShowAdAndWaitAsync(response, cancellationToken);
        }

        public Task<PlaymanityAdResult> ShowLoadedAdAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_loadedAd == null)
            {
                return Task.FromResult(new PlaymanityAdResult
                {
                    success = false,
                    reason = PlaymanityAdFinishReason.Failed,
                    error = "No loaded ad. Call RequestAdAsync first."
                });
            }

            return ShowAdAndWaitAsync(_loadedAd, cancellationToken);
        }

        public async Task<PlaymanityAdResult> ShowAdAndWaitAsync(
            PlaymanityResponse ad,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (ad == null || !ad.success)
            {
                return new PlaymanityAdResult
                {
                    success = false,
                    reason = PlaymanityAdFinishReason.Failed,
                    ad = ad,
                    error = ad == null ? "Ad is null." : ad.error
                };
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new PlaymanityAdResult
                {
                    success = false,
                    reason = PlaymanityAdFinishReason.Cancelled,
                    ad = ad,
                    error = "Cancelled."
                };
            }

            if (_finishSource != null)
                FinishAd(PlaymanityAdFinishReason.Cancelled, "Another ad started.");

            EnsureAdView();
            BindUiEvents();

            _currentAd = ad;
            _impressionReported = false;
            _clicked = false;
            _clickInFlight = false;
            _finishSource = new TaskCompletionSource<PlaymanityAdResult>();
            TaskCompletionSource<PlaymanityAdResult> finishSource = _finishSource;

            if (cancellationToken.CanBeCanceled)
            {
                if (_cancellationCoroutine != null)
                    StopCoroutine(_cancellationCoroutine);
                _cancellationCoroutine = StartCoroutine(FinishWhenCanceled(cancellationToken, finishSource));
            }

            ApplyAdToUi(ad);
            ShowAdUi();

            _status = "Showing ad...";
            await LoadMediaAsync(ad);

            if (_finishSource == finishSource && _currentAd == ad)
            {
                if (_impressionCoroutine != null)
                    StopCoroutine(_impressionCoroutine);
                _impressionCoroutine = StartCoroutine(ReportImpressionAfterExposure(ad));

                if (autoCloseAfterSeconds > 0f)
                {
                    if (_autoCloseCoroutine != null)
                        StopCoroutine(_autoCloseCoroutine);
                    _autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay(autoCloseAfterSeconds));
                }
            }

            return await finishSource.Task;
        }

        public Task<PlaymanityAdResult> WaitForCurrentAdAsync()
        {
            if (_finishSource == null)
            {
                return Task.FromResult(new PlaymanityAdResult
                {
                    success = true,
                    reason = PlaymanityAdFinishReason.Unknown,
                    error = "No ad is currently showing."
                });
            }

            return _finishSource.Task;
        }

        public void CloseAd()
        {
            FinishAd(PlaymanityAdFinishReason.Closed, null);
        }

        private IEnumerator ReportImpressionAfterExposure(PlaymanityResponse ad)
        {
            float visibleMs = 0f;
            string messageId = PlaymanityClient.NewMessageId();

            while (_finishSource != null && _currentAd == ad && visibleMs < ImpressionExposureMs)
            {
                if (IsAdUiVisible())
                    visibleMs += Time.unscaledDeltaTime * 1000f;

                SetExposureProgress(visibleMs / ImpressionExposureMs);
                yield return null;
            }

            if (_finishSource == null || _currentAd != ad || _impressionReported)
                yield break;

            SetExposureProgress(1f);
            ReportImpressionAsync(ad, messageId, Mathf.Max(ImpressionExposureMs, Mathf.RoundToInt(visibleMs)));
        }

        private async void ReportImpressionAsync(PlaymanityResponse ad, string messageId, int exposureMs)
        {
            try
            {
                PlaymanityResponse response = await Client.ReportAdImpressionAsync(
                    ad.adId,
                    exposureMs,
                    viewportRatio,
                    messageId,
                    PlaymanityClient.NowMs());

                if (!IsCurrentAd(ad))
                    return;

                if (response.success)
                {
                    _impressionReported = true;
                    _status = "Impression reported.";
                    SetUiStatus("Sponsored by Playmanity", false);
                }
                else
                {
                    _status = "Impression failed: " + FormatResponseError(response);
                    SetUiStatus(_status, true);
                    Debug.LogWarning("Playmanity impression failed: " + FormatResponseError(response));
                }
            }
            catch (Exception ex)
            {
                if (!IsCurrentAd(ad))
                    return;

                _status = "Impression exception: " + ex.Message;
                SetUiStatus(_status, true);
                Debug.LogException(ex);
            }
        }

        private IEnumerator AutoCloseAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);

            if (_finishSource != null)
                FinishAd(PlaymanityAdFinishReason.AutoClosed, null);
        }

        private IEnumerator FinishWhenCanceled(
            CancellationToken cancellationToken,
            TaskCompletionSource<PlaymanityAdResult> finishSource)
        {
            while (_finishSource == finishSource && !cancellationToken.IsCancellationRequested)
                yield return null;

            if (_finishSource == finishSource && cancellationToken.IsCancellationRequested)
            {
                _cancellationCoroutine = null;
                FinishAd(PlaymanityAdFinishReason.Cancelled, "Cancelled.");
            }
        }

        private async void OnClickAdButton()
        {
            if (_currentAd == null || _clickInFlight)
                return;

            PlaymanityResponse ad = _currentAd;
            _clickInFlight = true;
            _status = "Reporting click...";
            SetClickInFlight(true);
            SetUiStatus(_status, false);

            try
            {
                PlaymanityResponse intent = await Client.RequestClickIntentAsync(ad.adId);
                if (!IsCurrentAd(ad))
                    return;

                if (!intent.success)
                {
                    _status = "Click intent failed: " + FormatResponseError(intent);
                    SetUiStatus(_status, true);
                    Debug.LogWarning("Playmanity click intent failed: " + FormatResponseError(intent));
                    return;
                }

                PlaymanityResponse click = await Client.ReportAdClickAsync(ad.adId, intent.nonce);
                if (!IsCurrentAd(ad))
                    return;

                if (!click.success)
                {
                    _status = "Click failed: " + FormatResponseError(click);
                    SetUiStatus(_status, true);
                    Debug.LogWarning("Playmanity click failed: " + FormatResponseError(click));
                    return;
                }

                _clicked = true;
                _status = "Click reported.";
                SetUiStatus(_status, false);

                if (openClickUrl && !string.IsNullOrEmpty(ad.clickUrl))
                    Application.OpenURL(ad.clickUrl);

                if (finishOnClick)
                    FinishAd(PlaymanityAdFinishReason.Clicked, null);
            }
            catch (Exception ex)
            {
                if (!IsCurrentAd(ad))
                    return;

                _status = "Click exception: " + ex.Message;
                SetUiStatus(_status, true);
                Debug.LogException(ex);
            }
            finally
            {
                _clickInFlight = false;

                if (IsCurrentAd(ad))
                    SetClickInFlight(false);
            }
        }

        private void FinishAd(PlaymanityAdFinishReason reason, string error)
        {
            if (_finishSource == null)
                return;

            TaskCompletionSource<PlaymanityAdResult> source = _finishSource;

            if (_impressionCoroutine != null)
            {
                StopCoroutine(_impressionCoroutine);
                _impressionCoroutine = null;
            }

            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }

            if (_cancellationCoroutine != null)
            {
                StopCoroutine(_cancellationCoroutine);
                _cancellationCoroutine = null;
            }

            PlaymanityAdResult result = new PlaymanityAdResult
            {
                success = error == null,
                clicked = _clicked,
                impressionReported = _impressionReported,
                reason = reason,
                ad = _currentAd,
                error = error
            };

            HideAdUi();
            _currentAd = null;
            _finishSource = null;

            _status = "Ad finished: " + reason;
            source.TrySetResult(result);
        }

        private void ApplyAdToUi(PlaymanityResponse ad)
        {
            if (adView != null)
            {
                adView.SetAd(ad, GetOrCreateMockTexture());
                return;
            }

            if (titleText != null)
                titleText.text = string.IsNullOrEmpty(ad.title) ? "Sponsored" : ad.title;

            if (descriptionText != null)
                descriptionText.text = string.IsNullOrEmpty(ad.description) ? "" : ad.description;

            if (statusText != null)
                statusText.text = "Loading media...";

            if (mediaImage != null)
                mediaImage.texture = GetOrCreateMockTexture();
        }

        private async Task LoadMediaAsync(PlaymanityResponse ad)
        {
            if (adView == null && mediaImage == null)
                return;

            if (!IsCurrentAd(ad))
                return;

            SetUiStatus("Loading media...", false);

            if (ad == null || string.IsNullOrEmpty(ad.mediaUrl) || ad.mediaUrl.StartsWith("mock://", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsCurrentAd(ad))
                    return;

                SetMediaTexture(GetOrCreateMockTexture());
                SetUiStatus("Sponsored by Playmanity", false);
                return;
            }

            UnityWebRequest request = null;
            try
            {
                request = UnityWebRequestTexture.GetTexture(ad.mediaUrl);
                UnityWebRequestAsyncOperation op = request.SendWebRequest();

                while (!op.isDone)
                    await Task.Yield();

                if (!IsCurrentAd(ad))
                    return;

#if UNITY_2020_2_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif

                if (failed)
                {
                    Debug.LogWarning("Failed to load Playmanity ad media: " + request.error);
                    SetMediaTexture(GetOrCreateMockTexture());
                    SetUiStatus("Media unavailable", true);
                }
                else
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    SetMediaTexture(texture);
                    SetUiStatus("Sponsored by Playmanity", false);
                }
            }
            catch (Exception ex)
            {
                if (!IsCurrentAd(ad))
                    return;

                SetMediaTexture(GetOrCreateMockTexture());
                SetUiStatus("Media unavailable", true);
                Debug.LogWarning("Failed to load Playmanity ad media: " + ex.Message);
            }
            finally
            {
                if (request != null)
                    request.Dispose();
            }
        }

        private Texture2D GetOrCreateMockTexture()
        {
            if (_mockTexture != null)
                return _mockTexture;

            int width = 640;
            int height = 360;
            _mockTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color top = new Color(0.372549f, 0.384314f, 0.945098f, 1f);
            Color bottom = new Color(0.05f, 0.06f, 0.12f, 1f);
            Color accent = new Color(0.62f, 0.64f, 1f, 1f);

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                Color row = Color.Lerp(bottom, top, t);

                for (int x = 0; x < width; x++)
                {
                    bool stripe = ((x + y) / 48) % 2 == 0;
                    Color pixel = stripe ? row : Color.Lerp(row, accent, 0.12f);
                    _mockTexture.SetPixel(x, y, pixel);
                }
            }

            _mockTexture.Apply();
            _mockTexture.name = "Playmanity Mock Ad";
            return _mockTexture;
        }

        private void ShowAdUi()
        {
            if (adView != null)
            {
                adView.Show();
                return;
            }

            if (adCanvas != null)
                adCanvas.gameObject.SetActive(true);

            if (adCanvasGroup != null)
            {
                adCanvasGroup.alpha = 1f;
                adCanvasGroup.interactable = true;
                adCanvasGroup.blocksRaycasts = true;
            }
        }

        private void HideAdUi()
        {
            if (adView != null)
            {
                adView.Hide();
                return;
            }

            if (adCanvasGroup != null)
            {
                adCanvasGroup.alpha = 0f;
                adCanvasGroup.interactable = false;
                adCanvasGroup.blocksRaycasts = false;
            }

            if (adCanvas != null)
                adCanvas.gameObject.SetActive(false);
        }

        private void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
#else
            if (FindObjectOfType<EventSystem>() != null)
                return;
#endif

            GameObject eventSystemGo = new GameObject("Playmanity EventSystem");
            eventSystemGo.transform.SetParent(transform, false);
            eventSystemGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private bool IsAdUiVisible()
        {
            if (adView != null)
                return adView.IsVisible;

            if (adCanvas != null)
                return adCanvas.gameObject.activeInHierarchy;

            return true;
        }

        private bool IsCurrentAd(PlaymanityResponse ad)
        {
            return _finishSource != null && _currentAd == ad;
        }

        private void SetUiStatus(string message, bool isError)
        {
            if (adView != null)
            {
                adView.SetStatus(message, isError);
                return;
            }

            if (statusText != null)
                statusText.text = message ?? "";
        }

        private void SetMediaTexture(Texture texture)
        {
            if (adView != null)
            {
                adView.SetMediaTexture(texture);
                return;
            }

            if (mediaImage != null && texture != null)
                mediaImage.texture = texture;
        }

        private void SetClickInFlight(bool inFlight)
        {
            if (adView != null)
            {
                adView.SetClickInFlight(inFlight);
                return;
            }

            if (clickButton != null)
                clickButton.interactable = !inFlight;
        }

        private void SetExposureProgress(float normalized)
        {
            if (adView != null)
                adView.SetExposureProgress(normalized);
        }

        private string FormatResponseError(PlaymanityResponse response)
        {
            if (response == null)
                return "Unknown error";

            string message = string.IsNullOrEmpty(response.error)
                ? "Unknown error"
                : response.error;

            if (response.retryAfterMs > 0 &&
                message.IndexOf("retry", StringComparison.OrdinalIgnoreCase) < 0)
            {
                message += " Retry in " + Mathf.CeilToInt(response.retryAfterMs / 1000f) + "s.";
            }

            return message;
        }
    }
}
