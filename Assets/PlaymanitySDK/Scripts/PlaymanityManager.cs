using PlaymanitySDK;
using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using static PlaymanitySDK.PlaymanityClient;

public class PlaymanityManager : MonoBehaviour
{
    private static PlaymanityManager _instance;
    public static PlaymanityManager Instance
    {
        get { return _instance; }
    }

    private bool displaysAd = false;

    [SerializeField] private GameObject adPanel;
    [SerializeField] private Image adImage;
    [SerializeField] private TMP_Text adTitle, adSubtitle;
    [SerializeField] private Slider adSlider;

    private Coroutine currentAdCoroutine;
    private Advertisement advertisement;

    public delegate void AdEventHandler(bool success, string message = "");
    public delegate void AdProgressHandler(float progress);

    public event AdEventHandler OnAdStarted;
    public event AdEventHandler OnAdCompleted;
    public event AdEventHandler OnAdFailed;
    public event AdProgressHandler OnAdProgress;

    void Start()
    {
        //Vector2 size = GetAdImageSize();
        //Debug.Log($"adImage size: Width = {size.x}, Height = {size.y}");
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public IEnumerator ShowStaticAd(float time)
    {
        if (displaysAd)
        {
            Debug.LogWarning("An ad is already being displayed.");
            yield break;
        }

        displaysAd = true;
        Task<Advertisement> adTask = GetAdvertisementAsync();
        yield return new WaitUntil(() => adTask.IsCompleted);

        Advertisement ad = adTask.Result;

        if (ad == null)
        {
            string errorMsg = "No advertisement retrieved.";
            Debug.LogWarning(errorMsg);
            displaysAd = false;
            OnAdFailed?.Invoke(false, errorMsg);
            yield break;
        }

        advertisement = ad;

        Debug.Log($"Showing ad: {ad.Title} for {time} seconds");

        adTitle.text = ad.Title;
        adSubtitle.text = ad.Description;
        adSlider.value = 1f;

        yield return StartCoroutine(LoadImage(ad.Media));

        if (adImage.sprite == null)
        {
            string errorMsg = "Failed to load ad image.";
            Debug.LogWarning(errorMsg);
            displaysAd = false;
            advertisement = null;
            OnAdFailed?.Invoke(false, errorMsg);
            yield break;
        }

        adPanel.SetActive(true);
        OnAdStarted?.Invoke(true);

        float elapsedTime = 0f;
        while (elapsedTime < time)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / time;
            adSlider.value = 1f - progress;
            OnAdProgress?.Invoke(progress);
            yield return null;
        }

        adPanel.SetActive(false);
        adImage.sprite = null;
        displaysAd = false;
        advertisement = null;
        Debug.Log("Ad display time ended.");
        OnAdCompleted?.Invoke(true);
    }

    private IEnumerator LoadImage(string mediaUrl)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(mediaUrl))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load ad image: {uwr.error}");
                OnAdFailed?.Invoke(false, $"Failed to load ad image: {uwr.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            adImage.sprite = sprite;
        }
    }

    public void StopCurrentAd()
    {
        if (currentAdCoroutine != null)
        {
            StopCoroutine(currentAdCoroutine);
            adPanel.SetActive(false);
            adImage.sprite = null;
            displaysAd = false;
            Debug.Log("Current ad stopped manually.");
            OnAdCompleted?.Invoke(false, "Ad stopped manually");
        }
    }

    [Obsolete("Use InvokeAd with event handlers instead.")]
    public void InvokeAd(float time)
    {
        currentAdCoroutine = StartCoroutine(ShowStaticAd(time));
    }

    public void InvokeAd(float time, AdEventHandler onCompleted = null, AdEventHandler onFailed = null, AdProgressHandler onProgress = null)
    {
        if (onCompleted != null)
            OnAdCompleted += onCompleted;

        if (onFailed != null)
            OnAdFailed += onFailed;

        if (onProgress != null)
            OnAdProgress += onProgress;

        currentAdCoroutine = StartCoroutine(ShowStaticAd(time));
    }

    public void AdClick()
    {
        if (advertisement != null)
        {
            Application.OpenURL(advertisement.Url);
        }
    }

    public Vector2 GetAdImageSize()
    {
        if (adImage != null)
        {
            RectTransform rectTransform = adImage.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                return rectTransform.rect.size;
            }
            else
            {
                Debug.LogError("adImage has no RectTransform component.");
                return Vector2.zero;
            }
        }
        else
        {
            Debug.LogError("adImage is not assigned in the inspector.");
            return Vector2.zero;
        }
    }

    public IEnumerator GetAdImageSizeAfterLayout(Action<Vector2> callback)
    {
        yield return new WaitForEndOfFrame();
        Vector2 size = GetAdImageSize();
        callback?.Invoke(size);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            StopCurrentAd();
            _instance = null;
        }
    }
}