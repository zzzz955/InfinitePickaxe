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
    }
}
