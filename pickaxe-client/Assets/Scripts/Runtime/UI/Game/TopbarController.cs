using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 탭과 무관하게 항상 표시되는 상단바 재화 표시 담당
    /// 서버 이벤트(UserDataSnapshot, CurrencyUpdate, MiningComplete)만 반영하고
    /// 클라이언트에서 임의로 증감하지 않는다.
    /// </summary>
    public class TopbarController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI crystalText;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private TextMeshProUGUI adCountText;
        [SerializeField] private int watchedAdCount = 0;
        [SerializeField] private int maxAdCount = 3;
        [SerializeField] private GameObject adWatchResultModal;
        [SerializeField] private TextMeshProUGUI adWatchResultText;
        [SerializeField] private TextMeshProUGUI adWatchResultCrystalText;
        [SerializeField] private Button adWatchResultCloseButton;
        [SerializeField] private Color adWatchFailColor = new Color(1f, 0.6f, 0.6f);
        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject menuDropdown;
        [SerializeField] private Button menuBackgroundButton;
        [SerializeField] private Button menuSettingsButton;
        [SerializeField] private Button menuLogoutButton;
        [SerializeField] private Button menuExitButton;
        [SerializeField] private RectTransform menuPanel;
        [SerializeField] private Vector2 menuAnchorOffset = new Vector2(0f, -12f);
        [SerializeField] private Vector2 menuClampPadding = new Vector2(8f, 8f);
        [SerializeField] private RectTransform menuDropdownRoot;
        [SerializeField] private GameObject settingsModal;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button settingsBackgroundButton;
        [SerializeField] private GameObject logoutConfirmModal;
        [SerializeField] private Button logoutConfirmButton;
        [SerializeField] private Button logoutCancelButton;
        [SerializeField] private Button logoutBackgroundButton;

        private ulong? currentGold;
        private uint? currentCrystal;
        private UserResourceCache resourceCache;
        private MessageHandler messageHandler;
        private QuestStateCache questState;
        private bool adStateSubscribed;
        private const string CrystalRewardAdType = "crystal_reward";
        private Color adWatchResultDefaultColor;
        private bool adWatchResultColorCached;

        private void OnEnable()
        {
            resourceCache = UserResourceCache.Instance;
            if (resourceCache != null)
            {
                resourceCache.OnChanged += HandleResourceChanged;
                ApplyResourceCache();
            }

            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveAllListeners();
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (adWatchResultCloseButton != null)
            {
                adWatchResultCloseButton.onClick.RemoveAllListeners();
                adWatchResultCloseButton.onClick.AddListener(CloseAdWatchResultModal);
            }

            if (!adWatchResultColorCached && adWatchResultText != null)
            {
                adWatchResultDefaultColor = adWatchResultText.color;
                adWatchResultColorCached = true;
            }

            SetupMenuButtons();
            SetupSettingsModalButtons();
            SetupLogoutModalButtons();

            messageHandler = MessageHandler.Instance;
            if (messageHandler != null)
            {
                messageHandler.OnAdWatchResult -= HandleAdWatchResult;
                messageHandler.OnAdWatchResult += HandleAdWatchResult;
            }

            SubscribeAdState();
            UpdateAdCount();
        }

        private void OnDisable()
        {
            if (resourceCache != null)
            {
                resourceCache.OnChanged -= HandleResourceChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnAdWatchResult -= HandleAdWatchResult;
            }

            CloseMenu();
            CloseSettingsModal();
            CloseLogoutModal();

            UnsubscribeAdState();
        }

        private void HandleResourceChanged()
        {
            ApplyResourceCache();
        }

        private void ApplyResourceCache()
        {
            if (resourceCache == null)
            {
                return;
            }

            currentGold = resourceCache.Gold;
            currentCrystal = resourceCache.Crystal;
            Apply();
        }

        private void Apply()
        {
            if (goldText != null && currentGold.HasValue)
            {
                goldText.text = currentGold.Value.ToString("N0");
            }
            if (crystalText != null && currentCrystal.HasValue)
            {
                crystalText.text = currentCrystal.Value.ToString("N0");
            }
        }

        private void SubscribeAdState()
        {
            if (adStateSubscribed) return;
            questState = QuestStateCache.Instance;
            if (questState != null)
            {
                questState.OnAdCountersChanged += HandleAdCountersChanged;
                adStateSubscribed = true;
            }
        }

        private void UnsubscribeAdState()
        {
            if (!adStateSubscribed || questState == null) return;
            questState.OnAdCountersChanged -= HandleAdCountersChanged;
            adStateSubscribed = false;
        }

        private void HandleAdCountersChanged()
        {
            UpdateAdCount();
        }

        private void UpdateAdCount()
        {
            int watched = watchedAdCount;
            int limit = maxAdCount;

            if (questState != null && questState.TryGetAdCounter(CrystalRewardAdType, out var counter))
            {
                watched = (int)counter.AdCount;
                if (counter.DailyLimit > 0)
                {
                    limit = (int)counter.DailyLimit;
                }
            }

            watchedAdCount = watched;
            maxAdCount = limit;

            if (adCountText != null)
            {
                adCountText.text = $"({watchedAdCount}/{maxAdCount})";
            }

            if (watchAdButton != null)
            {
                watchAdButton.interactable = watchedAdCount < maxAdCount;
            }
        }

        private void OnWatchAdClicked()
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.NotifyAdWatchComplete(CrystalRewardAdType);
            Debug.Log("TopbarController: ad watch requested");
        }

        private void HandleAdWatchResult(AdWatchResult result)
        {
            if (result == null) return;
            if (!string.Equals(result.AdType, CrystalRewardAdType, StringComparison.OrdinalIgnoreCase)) return;

            if (adWatchResultText != null)
            {
                adWatchResultText.text = result.Success ? "광고 시청 성공" : "광고 시청 실패";
                if (!adWatchResultColorCached)
                {
                    adWatchResultDefaultColor = adWatchResultText.color;
                    adWatchResultColorCached = true;
                }
                adWatchResultText.color = result.Success ? adWatchResultDefaultColor : adWatchFailColor;
            }

            if (adWatchResultCrystalText != null)
            {
                uint crystalEarned = result.Success ? result.CrystalEarned : 0;
                adWatchResultCrystalText.text = $"{crystalEarned}";
            }

            if (adWatchResultModal != null)
            {
                adWatchResultModal.SetActive(true);
                adWatchResultModal.transform.SetAsLastSibling();
            }

            UpdateAdCount();
        }

        private void CloseAdWatchResultModal()
        {
            if (adWatchResultModal != null)
            {
                adWatchResultModal.SetActive(false);
            }
        }

        private void SetupMenuButtons()
        {
            CacheMenuPanel();

            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(ToggleMenu);
            }

            if (menuBackgroundButton != null)
            {
                menuBackgroundButton.onClick.RemoveAllListeners();
                menuBackgroundButton.onClick.AddListener(CloseMenu);
            }

            if (menuSettingsButton != null)
            {
                menuSettingsButton.onClick.RemoveAllListeners();
                menuSettingsButton.onClick.AddListener(OpenSettingsModal);
            }

            if (menuLogoutButton != null)
            {
                menuLogoutButton.onClick.RemoveAllListeners();
                menuLogoutButton.onClick.AddListener(OpenLogoutModal);
            }

            if (menuExitButton != null)
            {
                menuExitButton.onClick.RemoveAllListeners();
                menuExitButton.onClick.AddListener(RequestGameExit);
            }

            CloseMenu();
        }

        private void ToggleMenu()
        {
            if (menuDropdown == null)
            {
                return;
            }

            bool nextActive = !menuDropdown.activeSelf;
            menuDropdown.SetActive(nextActive);
            if (nextActive)
            {
                menuDropdown.transform.SetAsLastSibling();
                PositionMenuDropdown();
            }
        }

        private void CloseMenu()
        {
            if (menuDropdown != null)
            {
                menuDropdown.SetActive(false);
            }
        }

        private void CacheMenuPanel()
        {
            if (menuPanel != null || menuDropdown == null)
            {
                return;
            }

            var panelTransform = menuDropdown.transform.Find("MenuPanel");
            if (panelTransform == null)
            {
                panelTransform = menuDropdown.transform.Find("ModalPanel");
            }

            menuPanel = panelTransform as RectTransform;
        }

        private RectTransform ResolveMenuRoot()
        {
            if (menuDropdownRoot != null)
            {
                return menuDropdownRoot;
            }

            if (menuDropdown != null && menuDropdown.transform.parent is RectTransform parentRect)
            {
                return parentRect;
            }

            var canvas = menuButton != null ? menuButton.GetComponentInParent<Canvas>() : null;
            if (canvas == null && menuDropdown != null)
            {
                canvas = menuDropdown.GetComponentInParent<Canvas>();
            }

            return canvas != null ? canvas.transform as RectTransform : null;
        }

        private void PositionMenuDropdown()
        {
            CacheMenuPanel();
            if (menuPanel == null || menuButton == null)
            {
                return;
            }

            var rootRect = ResolveMenuRoot();
            if (rootRect == null)
            {
                return;
            }

            if (menuDropdownRoot != null && menuDropdown != null && menuDropdown.transform.parent != menuDropdownRoot)
            {
                menuDropdown.transform.SetParent(menuDropdownRoot, false);
            }

            var menuButtonRect = menuButton.GetComponent<RectTransform>();
            if (menuButtonRect == null)
            {
                return;
            }

            var canvas = rootRect.GetComponent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanel);

            var corners = new Vector3[4];
            menuButtonRect.GetWorldCorners(corners);
            var bottomLeft = corners[0];
            var bottomRight = corners[3];
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect,
                    RectTransformUtility.WorldToScreenPoint(camera, bottomLeft),
                    camera,
                    out var localBottomLeft))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootRect,
                    RectTransformUtility.WorldToScreenPoint(camera, bottomRight),
                    camera,
                    out var localBottomRight))
            {
                return;
            }

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(menuPanel);
            var panelSize = bounds.size;
            var canvasBounds = rootRect.rect;

            float minX = canvasBounds.xMin + menuClampPadding.x;
            float maxX = canvasBounds.xMax - menuClampPadding.x;
            float minY = canvasBounds.yMin + menuClampPadding.y;
            float maxY = canvasBounds.yMax - menuClampPadding.y;

            var centerPoint = (localBottomLeft + localBottomRight) * 0.5f;
            float halfWidth = panelSize.x * 0.5f;
            bool overflowLeft = centerPoint.x - halfWidth < minX;
            bool overflowRight = centerPoint.x + halfWidth > maxX;

            Vector2 basePoint = centerPoint;
            Vector2 pivot = new Vector2(0.5f, 1f);

            if (overflowRight && !overflowLeft)
            {
                basePoint = localBottomRight;
                pivot = new Vector2(1f, 1f);
            }
            else if (overflowLeft && !overflowRight)
            {
                basePoint = localBottomLeft;
                pivot = new Vector2(0f, 1f);
            }

            menuPanel.anchorMin = new Vector2(0.5f, 0.5f);
            menuPanel.anchorMax = new Vector2(0.5f, 0.5f);
            menuPanel.pivot = pivot;

            Vector2 anchored = basePoint + menuAnchorOffset;

            float left = anchored.x - (panelSize.x * menuPanel.pivot.x);
            float right = left + panelSize.x;
            float top = anchored.y + (panelSize.y * (1f - menuPanel.pivot.y));
            float bottom = top - panelSize.y;

            if (left < minX)
            {
                anchored.x += minX - left;
            }
            if (right > maxX)
            {
                anchored.x -= right - maxX;
            }
            if (bottom < minY)
            {
                anchored.y += minY - bottom;
            }
            if (top > maxY)
            {
                anchored.y -= top - maxY;
            }

            menuPanel.anchoredPosition = anchored;
        }

        private void OpenSettingsModal()
        {
            CloseMenu();

            if (settingsModal == null)
            {
                return;
            }

            settingsModal.SetActive(true);
            settingsModal.transform.SetAsLastSibling();

            var controller = settingsModal.GetComponentInChildren<SettingsTabController>();
            controller?.RefreshData();
        }

        private void CloseSettingsModal()
        {
            if (settingsModal != null)
            {
                settingsModal.SetActive(false);
            }
        }

        private void OpenLogoutModal()
        {
            CloseMenu();

            if (logoutConfirmModal == null)
            {
                ConfirmLogout();
                return;
            }

            logoutConfirmModal.SetActive(true);
            logoutConfirmModal.transform.SetAsLastSibling();
        }

        private void CloseLogoutModal()
        {
            if (logoutConfirmModal != null)
            {
                logoutConfirmModal.SetActive(false);
            }
        }

        private void ConfirmLogout()
        {
            CloseLogoutModal();

            var gameController = FindObjectOfType<GameSceneController>();
            if (gameController != null)
            {
                gameController.LogoutToTitle();
                return;
            }
        }

        private void RequestGameExit()
        {
            CloseMenu();

            var gameController = FindObjectOfType<GameSceneController>();
            gameController?.RequestExitFromGame();
        }

        private void SetupSettingsModalButtons()
        {
            if (settingsModal == null)
            {
                return;
            }

            if (settingsBackgroundButton != null)
            {
                settingsBackgroundButton.onClick.RemoveAllListeners();
                settingsBackgroundButton.onClick.AddListener(CloseSettingsModal);
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.RemoveAllListeners();
                settingsCloseButton.onClick.AddListener(CloseSettingsModal);
            }

            CloseSettingsModal();
        }

        private void SetupLogoutModalButtons()
        {
            if (logoutConfirmModal == null)
            {
                return;
            }

            if (logoutBackgroundButton != null)
            {
                logoutBackgroundButton.onClick.RemoveAllListeners();
                logoutBackgroundButton.onClick.AddListener(CloseLogoutModal);
            }

            if (logoutCancelButton != null)
            {
                logoutCancelButton.onClick.RemoveAllListeners();
                logoutCancelButton.onClick.AddListener(CloseLogoutModal);
            }

            if (logoutConfirmButton != null)
            {
                logoutConfirmButton.onClick.RemoveAllListeners();
                logoutConfirmButton.onClick.AddListener(ConfirmLogout);
            }

            CloseLogoutModal();
        }
    }
}
