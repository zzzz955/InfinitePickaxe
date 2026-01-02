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
            }
        }

        private void CloseMenu()
        {
            if (menuDropdown != null)
            {
                menuDropdown.SetActive(false);
            }
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
