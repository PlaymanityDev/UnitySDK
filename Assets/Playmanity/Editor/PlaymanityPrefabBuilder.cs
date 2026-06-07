using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Playmanity.Unity.Editor
{
    public static class PlaymanityPrefabBuilder
    {
        private const string OldPrefabPath = "Assets/Playmanity/PlaymanityUI.prefab";
        private const string PrefabPath = "Assets/Playmanity/Resources/Playmanity/PlaymanityUI.prefab";
        private const int UiLayer = 5;
        private static readonly Color BrandColor = new Color(0.372549f, 0.384314f, 0.945098f, 1f);
        private static readonly Color BrandHoverColor = new Color(0.48f, 0.50f, 1f, 1f);
        private static readonly Color BrandPressedColor = new Color(0.29f, 0.30f, 0.74f, 1f);
        private static readonly Color BrandSoftTextColor = new Color(0.78f, 0.79f, 1f, 1f);

        [MenuItem("Playmanity/Rebuild Playmanity UI Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Playmanity/Resources/Playmanity");
            PreserveExistingPrefabGuid();

            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            }

            GameObject root = new GameObject(
                "PlaymanityUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(PlaymanityAdView));

            try
            {
                SetLayerRecursively(root, UiLayer);

                RectTransform rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);
                rootRect.localScale = Vector3.one;

                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;

                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                RectTransform dim = CreateRect("Dim", root.transform);
                Stretch(dim);
                AddImage(dim.gameObject, new Color(0.015f, 0.016f, 0.026f, 0.88f), null);

                RectTransform surface = CreateRect("AdSurface", root.transform);
                Stretch(surface);
                Image surfaceImage = AddImage(surface.gameObject, new Color(0.058f, 0.067f, 0.093f, 0.98f), uiSprite);
                surfaceImage.raycastTarget = true;

                RectTransform topRule = CreateRect("TopRule", surface);
                AnchorTop(topRule, new Vector2(1536f, 3f), new Vector2(0f, -32f));
                AddImage(topRule.gameObject, BrandColor, null).raycastTarget = false;

                RectTransform mediaFrame = CreateRect("MediaFrame", surface);
                AnchorTopAndBottom(mediaFrame, 1536f, 48f, 220f);
                AddImage(mediaFrame.gameObject, new Color(0.018f, 0.021f, 0.032f, 1f), uiSprite).raycastTarget = false;

                RectTransform mediaRect = CreateRect("MediaImage", mediaFrame);
                Center(mediaRect, new Vector2(1516f, 853f));
                RawImage mediaImage = mediaRect.gameObject.AddComponent<RawImage>();
                mediaImage.color = Color.white;
                mediaImage.raycastTarget = false;
                AspectRatioFitter mediaAspectFitter = mediaRect.gameObject.AddComponent<AspectRatioFitter>();
                mediaAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                mediaAspectFitter.aspectRatio = 16f / 9f;

                RectTransform sponsorPill = CreateRect("SponsorPill", mediaFrame);
                AnchorTopLeft(sponsorPill, new Vector2(300f, 44f), new Vector2(28f, -28f));
                AddImage(sponsorPill.gameObject, new Color(0.038f, 0.044f, 0.062f, 0.88f), uiSprite).raycastTarget = false;

                TextMeshProUGUI sponsorText = AddText(
                    "SponsorText",
                    sponsorPill,
                    "Sponsored by Playmanity",
                    20f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center,
                    BrandSoftTextColor,
                    font);
                Stretch(sponsorText.rectTransform, new Vector2(14f, 0f), new Vector2(-14f, 0f));

                RectTransform exposureTrack = CreateRect("ExposureTrack", mediaFrame);
                AnchorBottom(exposureTrack, new Vector2(1480f, 6f), new Vector2(0f, 22f));
                AddImage(exposureTrack.gameObject, new Color(1f, 1f, 1f, 0.14f), null).raycastTarget = false;

                RectTransform exposureFillRect = CreateRect("ExposureFill", exposureTrack);
                Stretch(exposureFillRect);
                exposureFillRect.anchorMax = new Vector2(0f, 1f);
                Image exposureFill = AddImage(exposureFillRect.gameObject, BrandColor, null);
                exposureFill.raycastTarget = false;

                TextMeshProUGUI titleText = AddText(
                    "TitleText",
                    surface,
                    "Sponsored",
                    42f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Left,
                    new Color(0.965f, 0.972f, 0.992f, 1f),
                    font);
                AnchorBottomLeft(titleText.rectTransform, new Vector2(1060f, 54f), new Vector2(192f, 104f));
                titleText.enableAutoSizing = true;
                titleText.fontSizeMin = 24f;
                titleText.fontSizeMax = 42f;
                titleText.overflowMode = TextOverflowModes.Ellipsis;

                TextMeshProUGUI descriptionText = AddText(
                    "DescriptionText",
                    surface,
                    "",
                    24f,
                    FontStyles.Normal,
                    TextAlignmentOptions.TopLeft,
                    new Color(0.74f, 0.78f, 0.86f, 1f),
                    font);
                AnchorBottomLeft(descriptionText.rectTransform, new Vector2(1060f, 52f), new Vector2(192f, 50f));
                descriptionText.textWrappingMode = TextWrappingModes.Normal;
                descriptionText.overflowMode = TextOverflowModes.Ellipsis;

                TextMeshProUGUI statusText = AddText(
                    "StatusText",
                    surface,
                    "Loading media...",
                    20f,
                    FontStyles.Normal,
                    TextAlignmentOptions.Left,
                    new Color(0.72f, 0.75f, 0.82f, 1f),
                    font);
                AnchorBottomLeft(statusText.rectTransform, new Vector2(650f, 32f), new Vector2(192f, 18f));

                Button closeButton = CreateButton(
                    "CloseButton",
                    "Close",
                    surface,
                    new Vector2(156f, 58f),
                    new Vector2(-396f, 44f),
                    new Color(0.17f, 0.19f, 0.25f, 1f),
                    new Color(0.23f, 0.26f, 0.34f, 1f),
                    new Color(0.12f, 0.14f, 0.19f, 1f),
                    new Color(0.92f, 0.94f, 0.98f, 1f),
                    uiSprite,
                    font,
                    out TextMeshProUGUI closeLabel);

                Button visitButton = CreateButton(
                    "VisitButton",
                    "Visit",
                    surface,
                    new Vector2(180f, 58f),
                    new Vector2(-192f, 44f),
                    BrandColor,
                    BrandHoverColor,
                    BrandPressedColor,
                    new Color(0.965f, 0.972f, 0.992f, 1f),
                    uiSprite,
                    font,
                    out TextMeshProUGUI visitLabel);

                PlaymanityAdView view = root.GetComponent<PlaymanityAdView>();
                view.canvas = canvas;
                view.canvasGroup = group;
                view.mediaImage = mediaImage;
                view.mediaAspectFitter = mediaAspectFitter;
                view.sponsorText = sponsorText;
                view.titleText = titleText;
                view.descriptionText = descriptionText;
                view.statusText = statusText;
                view.exposureFill = exposureFill;
                view.clickButton = visitButton;
                view.clickButtonLabel = visitLabel;
                view.closeButton = closeButton;
                view.closeButtonLabel = closeLabel;
                view.statusColor = new Color(0.72f, 0.75f, 0.82f, 1f);
                view.errorStatusColor = new Color(1f, 0.62f, 0.50f, 1f);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("Rebuilt Playmanity UI prefab at " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PreserveExistingPrefabGuid()
        {
            if (!File.Exists(OldPrefabPath) || File.Exists(PrefabPath))
                return;

            string error = AssetDatabase.MoveAsset(OldPrefabPath, PrefabPath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning("Could not move existing PlaymanityUI prefab: " + error);
        }

        private static Button CreateButton(
            string name,
            string label,
            Transform parent,
            Vector2 size,
            Vector2 bottomRightOffset,
            Color normal,
            Color highlighted,
            Color pressed,
            Color labelColor,
            Sprite sprite,
            TMP_FontAsset font,
            out TextMeshProUGUI labelText)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = bottomRightOffset;
            rect.sizeDelta = size;

            Image image = AddImage(rect.gameObject, normal, sprite);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = highlighted,
                pressedColor = pressed,
                selectedColor = highlighted,
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.48f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            labelText = AddText(
                name + "Label",
                rect,
                label,
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                labelColor,
                font);
            Stretch(labelText.rectTransform);

            return button;
        }

        private static TextMeshProUGUI AddText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color,
            TMP_FontAsset font)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = Vector4.zero;
            return label;
        }

        private static Image AddImage(GameObject go, Color color, Sprite sprite)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite != null)
                image.type = Image.Type.Sliced;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            SetLayerRecursively(go, UiLayer);
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void AnchorTop(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void AnchorTopAndBottom(RectTransform rect, float width, float topInset, float bottomInset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, (bottomInset - topInset) * 0.5f);
            rect.sizeDelta = new Vector2(width, -(topInset + bottomInset));
            rect.localScale = Vector3.one;
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void AnchorBottom(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void AnchorBottomLeft(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
