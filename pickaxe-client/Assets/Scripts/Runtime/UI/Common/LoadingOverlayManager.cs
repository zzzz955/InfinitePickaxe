using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Common
{
    public sealed class LoadingOverlayManager : MonoBehaviour
    {
        private static LoadingOverlayManager instance;
        private static Sprite runtimeSprite;
        public static LoadingOverlayManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("LoadingOverlayManager");
                    instance = go.AddComponent<LoadingOverlayManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Overlay Bindings (Optional)")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Overlay Auto Bind")]
        [SerializeField] private string overlayName = "LoadingOverlay";
        [SerializeField] private string statusTextName = "StatusText";
        [SerializeField] private bool autoFindOnSceneLoaded = true;
        [SerializeField] private bool createIfMissing = true;

        [Header("Overlay Create Defaults")]
        [SerializeField] private int overlaySortingOrder = 999;
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.35f);

        private int showCount;
        public bool IsVisible => showCount > 0;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (autoFindOnSceneLoaded)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }

            TryBindExisting();
            if (overlayRoot == null && createIfMissing)
            {
                CreateOverlay();
            }
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                instance = null;
            }
        }

        public void Show(string message = null)
        {
            showCount = Math.Max(0, showCount + 1);
            if (!string.IsNullOrEmpty(message))
            {
                SetMessage(message);
            }
            EnsureOverlayVisible();
            ApplyVisibility();
        }

        public void Hide()
        {
            if (showCount > 0)
            {
                showCount--;
            }
            ApplyVisibility();
        }

        public void Clear()
        {
            showCount = 0;
            ApplyVisibility();
        }

        public void SetMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void Bind(GameObject root, TextMeshProUGUI text = null)
        {
            overlayRoot = root;
            statusText = text != null
                ? text
                : root != null ? root.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            ApplyVisibility();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBindExisting();
            if (overlayRoot == null && createIfMissing)
            {
                CreateOverlay();
            }
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (overlayRoot == null) return;

            var shouldShow = showCount > 0;
            overlayRoot.SetActive(shouldShow);

            var canvasGroup = overlayRoot.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = shouldShow ? 1f : 0f;
                canvasGroup.interactable = shouldShow;
                canvasGroup.blocksRaycasts = shouldShow;
            }

            var image = overlayRoot.GetComponent<Image>();
            if (image != null)
            {
                if (shouldShow)
                {
                    EnsureOverlayAlpha(image);
                    EnsureOverlaySprite(image);
                }
                image.raycastTarget = shouldShow;
            }

            var raycaster = overlayRoot.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = shouldShow;
            }
        }

        private void TryBindExisting()
        {
            if (overlayRoot == null && !string.IsNullOrEmpty(overlayName))
            {
                overlayRoot = GameObject.Find(overlayName);
            }

            if (statusText == null && !string.IsNullOrEmpty(statusTextName))
            {
                var statusObj = GameObject.Find(statusTextName);
                if (statusObj != null)
                {
                    statusText = statusObj.GetComponent<TextMeshProUGUI>();
                }
            }

            if (statusText == null && overlayRoot != null)
            {
                statusText = overlayRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (overlayRoot != null)
            {
                EnsureOverlaySetup();
            }
        }

        private void EnsureOverlayVisible()
        {
            if (overlayRoot == null)
            {
                TryBindExisting();
                if (overlayRoot == null && createIfMissing)
                {
                    CreateOverlay();
                }
            }

            if (overlayRoot == null)
            {
                return;
            }

            EnsureOverlaySetup();
            var canvasGroup = overlayRoot.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            var image = overlayRoot.GetComponent<Image>();
            if (image != null)
            {
                EnsureOverlayAlpha(image);
                EnsureOverlaySprite(image);
            }
        }

        private void EnsureOverlaySetup()
        {
            var canvas = overlayRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = overlaySortingOrder;
            }

            var rect = overlayRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            var image = overlayRoot.GetComponent<Image>();
            if (image != null)
            {
                EnsureOverlayAlpha(image);
                EnsureOverlaySprite(image);
            }
        }

        private void EnsureOverlayAlpha(Image image)
        {
            if (image == null) return;
            if (image.color.a >= overlayColor.a) return;
            var color = image.color;
            color.a = overlayColor.a;
            image.color = color;
        }

        private void EnsureOverlaySprite(Image image)
        {
            if (image == null) return;
            if (image.sprite != null) return;
            runtimeSprite ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            image.sprite = runtimeSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
        }

        private void CreateOverlay()
        {
            var root = new GameObject(overlayName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            root.transform.SetParent(transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = overlaySortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var image = root.GetComponent<Image>();
            image.color = overlayColor;
            image.raycastTarget = true;

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayRoot = root;
        }
    }
}
