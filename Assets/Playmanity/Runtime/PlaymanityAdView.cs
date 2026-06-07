using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Playmanity.Unity
{
    [DisallowMultipleComponent]
    public sealed class PlaymanityAdView : MonoBehaviour
    {
        [Header("Root")]
        public Canvas canvas;
        public CanvasGroup canvasGroup;

        [Header("Content")]
        public RawImage mediaImage;
        public AspectRatioFitter mediaAspectFitter;
        public TMP_Text sponsorText;
        public TMP_Text titleText;
        public TMP_Text descriptionText;
        public TMP_Text statusText;
        public Image exposureFill;

        [Header("Actions")]
        public Button clickButton;
        public TMP_Text clickButtonLabel;
        public Button closeButton;
        public TMP_Text closeButtonLabel;

        [Header("State Colors")]
        public Color statusColor = new Color(0.72f, 0.75f, 0.82f, 1f);
        public Color errorStatusColor = new Color(1f, 0.62f, 0.50f, 1f);

        private UnityAction _clickAction;
        private UnityAction _closeAction;

        public bool IsVisible
        {
            get { return gameObject.activeInHierarchy; }
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void Bind(UnityAction clickAction, UnityAction closeAction)
        {
            Unbind();

            _clickAction = clickAction;
            _closeAction = closeAction;

            if (clickButton != null && _clickAction != null)
                clickButton.onClick.AddListener(_clickAction);

            if (closeButton != null && _closeAction != null)
                closeButton.onClick.AddListener(_closeAction);
        }

        public void Unbind()
        {
            if (clickButton != null && _clickAction != null)
                clickButton.onClick.RemoveListener(_clickAction);

            if (closeButton != null && _closeAction != null)
                closeButton.onClick.RemoveListener(_closeAction);

            _clickAction = null;
            _closeAction = null;
        }

        public void Show()
        {
            gameObject.SetActive(true);

            if (canvas != null)
                canvas.enabled = true;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (canvas != null)
                canvas.enabled = false;

            gameObject.SetActive(false);
        }

        public void SetAd(PlaymanityResponse ad, Texture placeholderTexture)
        {
            if (sponsorText != null)
                sponsorText.text = "Sponsored by Playmanity";

            if (titleText != null)
                titleText.text = string.IsNullOrEmpty(ad.title) ? "Sponsored" : ad.title;

            if (descriptionText != null)
                descriptionText.text = string.IsNullOrEmpty(ad.description) ? "" : ad.description;

            SetMediaTexture(placeholderTexture);
            SetExposureProgress(0f);
            SetClickInFlight(false);
            SetStatus("Loading media...", false);

            if (closeButtonLabel != null)
                closeButtonLabel.text = "Close";
        }

        public void SetMediaTexture(Texture texture)
        {
            if (mediaImage == null || texture == null)
                return;

            mediaImage.texture = texture;

            if (mediaAspectFitter != null && texture.width > 0 && texture.height > 0)
            {
                mediaAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                mediaAspectFitter.aspectRatio = (float)texture.width / texture.height;
            }
        }

        public void SetStatus(string message, bool isError)
        {
            if (statusText == null)
                return;

            statusText.text = message ?? "";
            statusText.color = isError ? errorStatusColor : statusColor;
        }

        public void SetClickInFlight(bool inFlight)
        {
            SetClickInteractable(!inFlight);

            if (clickButtonLabel != null)
                clickButtonLabel.text = inFlight ? "Opening..." : "Visit";
        }

        public void SetClickInteractable(bool interactable)
        {
            if (clickButton != null)
                clickButton.interactable = interactable;
        }

        public void SetCloseInteractable(bool interactable)
        {
            if (closeButton != null)
                closeButton.interactable = interactable;
        }

        public void SetExposureProgress(float normalized)
        {
            if (exposureFill == null)
                return;

            float progress = Mathf.Clamp01(normalized);
            RectTransform fillRect = exposureFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(progress, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            exposureFill.fillAmount = 1f;
        }

        public void AutoAssignReferences()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (mediaImage == null)
                mediaImage = FindComponentByName<RawImage>("MediaImage");

            if (mediaAspectFitter == null && mediaImage != null)
                mediaAspectFitter = mediaImage.GetComponent<AspectRatioFitter>();

            if (mediaAspectFitter == null)
                mediaAspectFitter = FindComponentByName<AspectRatioFitter>("MediaImage");

            if (sponsorText == null)
                sponsorText = FindComponentByName<TMP_Text>("SponsorText");

            if (titleText == null)
                titleText = FindComponentByName<TMP_Text>("TitleText");

            if (descriptionText == null)
                descriptionText = FindComponentByName<TMP_Text>("DescriptionText");

            if (statusText == null)
                statusText = FindComponentByName<TMP_Text>("StatusText");

            if (exposureFill == null)
                exposureFill = FindComponentByName<Image>("ExposureFill");

            if (clickButton == null)
                clickButton = FindComponentByName<Button>("VisitButton");

            if (clickButtonLabel == null)
                clickButtonLabel = FindComponentByName<TMP_Text>("VisitButtonLabel");

            if (closeButton == null)
                closeButton = FindComponentByName<Button>("CloseButton");

            if (closeButtonLabel == null)
                closeButtonLabel = FindComponentByName<TMP_Text>("CloseButtonLabel");
        }

        private T FindComponentByName<T>(string objectName) where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].name == objectName)
                    return components[i];
            }

            return null;
        }
    }
}
