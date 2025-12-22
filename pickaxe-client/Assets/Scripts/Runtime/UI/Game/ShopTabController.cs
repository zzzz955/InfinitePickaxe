using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 상점 탭 컨트롤러
    /// 크리스탈 구매, 슬롯 해금, 광고 시청 등
    /// </summary>
    public class ShopTabController : BaseTabController
    {
        [Header("Ad UI References")]
        [SerializeField] private Button watchAdButton1;
        [SerializeField] private Button watchAdButton2;
        [SerializeField] private Button watchAdButton3;
        [SerializeField] private TextMeshProUGUI adCountText;

        [Header("Slot Unlock UI")]
        [SerializeField] private Button unlockSlot2Button;
        [SerializeField] private Button unlockSlot3Button;
        [SerializeField] private Button unlockSlot4Button;
        [SerializeField] private TextMeshProUGUI slot2CostText;
        [SerializeField] private TextMeshProUGUI slot3CostText;
        [SerializeField] private TextMeshProUGUI slot4CostText;

        [Header("IAP UI References")]
        [SerializeField] private Button iapSmallButton;
        [SerializeField] private Button iapMediumButton;
        [SerializeField] private Button iapLargeButton;

        [Header("Shop Data")]
        [SerializeField] private int watchedAdCount = 0;
        [SerializeField] private int maxAdCount = 3;
        [SerializeField] private bool slot2Unlocked = false;
        [SerializeField] private bool slot3Unlocked = false;
        [SerializeField] private bool slot4Unlocked = false;

        private const string CrystalRewardAdType = "crystal_reward";

        private readonly int[] slotCosts = { 0, 400, 2000, 4000 }; // 서버 슬롯 인덱스 기준 (0~3)
        private readonly bool[] slotUnlockedStates = new bool[4];   // 서버 인덱스 기준
        private readonly bool[] unlockInProgress = new bool[4];     // 중복 요청 방지

        private uint currentCrystal;

        private MessageHandler messageHandler;
        private PickaxeStateCache pickaxeCache;
        private bool cacheSubscribed;
        private QuestStateCache questState;
        private bool adStateSubscribed;

        protected override void Initialize()
        {
            base.Initialize();

            messageHandler = MessageHandler.Instance;
            pickaxeCache = PickaxeStateCache.Instance;

            slotUnlockedStates[0] = true; // 슬롯 1은 기본 해금

            // 광고 버튼 이벤트 등록
            if (watchAdButton1 != null)
            {
                watchAdButton1.onClick.AddListener(() => OnWatchAdClicked(1));
            }
            if (watchAdButton2 != null)
            {
                watchAdButton2.onClick.AddListener(() => OnWatchAdClicked(2));
            }
            if (watchAdButton3 != null)
            {
                watchAdButton3.onClick.AddListener(() => OnWatchAdClicked(3));
            }

            // 슬롯 해금 버튼 이벤트 등록
            if (unlockSlot2Button != null)
            {
                unlockSlot2Button.onClick.AddListener(() => OnUnlockSlotClicked(2));
            }
            if (unlockSlot3Button != null)
            {
                unlockSlot3Button.onClick.AddListener(() => OnUnlockSlotClicked(3));
            }
            if (unlockSlot4Button != null)
            {
                unlockSlot4Button.onClick.AddListener(() => OnUnlockSlotClicked(4));
            }

            // IAP 버튼 이벤트 등록
            if (iapSmallButton != null)
            {
                iapSmallButton.onClick.AddListener(() => OnIAPClicked("small"));
            }
            if (iapMediumButton != null)
            {
                iapMediumButton.onClick.AddListener(() => OnIAPClicked("medium"));
            }
            if (iapLargeButton != null)
            {
                iapLargeButton.onClick.AddListener(() => OnIAPClicked("large"));
            }

            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            SubscribeMessageHandler();
            SubscribeCache();
            SubscribeAdState();
            SyncSlotsFromCache();
            RefreshData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnsubscribeMessageHandler();
            UnsubscribeCache();
            UnsubscribeAdState();
        }

        private void OnDestroy()
        {
            UnsubscribeMessageHandler();
            UnsubscribeCache();
            UnsubscribeAdState();
        }

        /// <summary>
        /// 상점 UI 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateAdCount();
            UpdateSlotButtons();
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
                string timer = FormatResetTimer(questState != null ? questState.AdCountersResetTimestampMs : 0);
                var textValue = $"?? ?? (?? {watchedAdCount}/{maxAdCount})";
                if (!string.IsNullOrEmpty(timer))
                {
                    textValue += $" | ?? {timer}";
                }
                adCountText.text = textValue;
            }

            if (watchAdButton1 != null)
            {
                watchAdButton1.interactable = watchedAdCount < 1 && maxAdCount >= 1;
            }
            if (watchAdButton2 != null)
            {
                watchAdButton2.interactable = watchedAdCount < 2 && maxAdCount >= 2;
            }
            if (watchAdButton3 != null)
            {
                watchAdButton3.interactable = watchedAdCount < 3 && maxAdCount >= 3;
            }
        }

        private void UpdateSlotButtons()
        {
            UpdateSlotButton(unlockSlot2Button, slot2CostText, 2);
            UpdateSlotButton(unlockSlot3Button, slot3CostText, 3);
            UpdateSlotButton(unlockSlot4Button, slot4CostText, 4);
        }

        /// <summary>
        /// 광고 시청 버튼 클릭 이벤트
        /// </summary>
        private void OnWatchAdClicked(int tier)
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.NotifyAdWatchComplete(CrystalRewardAdType);
            Debug.Log($"ShopTabController: ?? ?? ?? ??? (Tier {tier})");
        }

        /// <summary>
        /// 슬롯 해금 버튼 클릭 이벤트
        /// </summary>
        private void OnUnlockSlotClicked(int uiSlotNumber)
        {
            int serverSlotIndex = uiSlotNumber - 1; // 서버는 0-based
            if (!IsValidSlotIndex(serverSlotIndex))
            {
                Debug.LogWarning($"ShopTabController: 잘못된 슬롯 번호 {uiSlotNumber}");
                return;
            }

            if (!CanUnlock(serverSlotIndex))
            {
                RefreshData();
                return;
            }

            unlockInProgress[serverSlotIndex] = true;
            RefreshData();

            messageHandler?.RequestSlotUnlock((uint)serverSlotIndex);
            Debug.Log($"ShopTabController: 슬롯 {uiSlotNumber} 해금 요청 전송 (서버 인덱스 {serverSlotIndex})");
        }

        /// <summary>
        /// IAP 구매 버튼 클릭 이벤트
        /// </summary>
        private void OnIAPClicked(string packageType)
        {
            // MVP에서는 UI만 존재
            Debug.Log($"ShopTabController: IAP 버튼 클릭됨 ({packageType}) - MVP에서는 준비 중");
        }

        /// <summary>
        /// 슬롯 해금 상태 업데이트 (외부에서 호출)
        /// </summary>
        public void SetSlotUnlocked(int slotIndex, bool unlocked)
        {
            int serverSlotIndex = slotIndex > 0 ? slotIndex - 1 : slotIndex;
            if (IsValidSlotIndex(serverSlotIndex))
            {
                slotUnlockedStates[serverSlotIndex] = unlocked;
            }

            switch (slotIndex)
            {
                case 2:
                    slot2Unlocked = unlocked;
                    break;
                case 3:
                    slot3Unlocked = unlocked;
                    break;
                case 4:
                    slot4Unlocked = unlocked;
                    break;
            }

            RefreshData();
        }

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("테스트: 슬롯 2 해금")]
        private void TestUnlockSlot2()
        {
            SetSlotUnlocked(2, true);
        }
#endif
        #endregion

        #region Message & Cache

        private void SubscribeMessageHandler()
        {
            if (messageHandler == null)
            {
                messageHandler = MessageHandler.Instance;
            }
            if (messageHandler == null) return;

            messageHandler.OnHandshakeResult += HandleHandshake;
            messageHandler.OnUserDataSnapshot += HandleSnapshot;
            messageHandler.OnCurrencyUpdate += HandleCurrencyUpdate;
            messageHandler.OnAllSlotsResponse += HandleAllSlotsResponse;
            messageHandler.OnSlotUnlockResult += HandleSlotUnlockResult;

            ApplyLastKnownCurrency();
        }

        private void UnsubscribeMessageHandler()
        {
            if (messageHandler == null) return;

            messageHandler.OnHandshakeResult -= HandleHandshake;
            messageHandler.OnUserDataSnapshot -= HandleSnapshot;
            messageHandler.OnCurrencyUpdate -= HandleCurrencyUpdate;
            messageHandler.OnAllSlotsResponse -= HandleAllSlotsResponse;
            messageHandler.OnSlotUnlockResult -= HandleSlotUnlockResult;
        }

        private void SubscribeCache()
        {
            if (cacheSubscribed) return;
            pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache != null)
            {
                pickaxeCache.OnChanged += HandlePickaxeCacheChanged;
                cacheSubscribed = true;
            }
        }

        private void UnsubscribeCache()
        {
            if (!cacheSubscribed) return;
            if (pickaxeCache != null)
            {
                pickaxeCache.OnChanged -= HandlePickaxeCacheChanged;
            }
            cacheSubscribed = false;
        }

        private void SubscribeAdState()
        {
            if (adStateSubscribed) return;
            questState = QuestStateCache.Instance;
            if (questState != null)
            {
                questState.OnAdCountersChanged += HandleAdCountersChanged;
                adStateSubscribed = true;
                UpdateAdCount();
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

        private void HandleHandshake(HandshakeResponse res)
        {
            if (res?.Snapshot != null)
            {
                HandleSnapshot(res.Snapshot);
            }
        }

        private void HandleSnapshot(UserDataSnapshot snapshot)
        {
            if (snapshot == null) return;

            if (snapshot.Crystal.HasValue)
            {
                currentCrystal = snapshot.Crystal.Value;
            }

            SyncSlotsFrom(snapshot.PickaxeSlots);
            RefreshData();
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            if (update == null) return;
            if (update.Crystal.HasValue)
            {
                currentCrystal = update.Crystal.Value;
                RefreshData();
            }
        }

        private void HandleAllSlotsResponse(AllSlotsResponse response)
        {
            if (response == null) return;
            SyncSlotsFrom(response.Slots);
            RefreshData();
        }

        private void HandleSlotUnlockResult(SlotUnlockResult result)
        {
            if (result == null) return;

            int slotIndex = (int)result.SlotIndex;
            if (IsValidSlotIndex(slotIndex))
            {
                unlockInProgress[slotIndex] = false;
            }

            if (result.Success)
            {
                MarkSlotUnlocked(slotIndex);
                SyncSlotsFrom(new[] { result.NewSlot });
            }

            RefreshData();
        }

        private void HandlePickaxeCacheChanged()
        {
            SyncSlotsFromCache();
            RefreshData();
        }

        private void ApplyLastKnownCurrency()
        {
            if (messageHandler != null && messageHandler.TryGetLastCurrency(out var gold, out var crystal))
            {
                if (crystal.HasValue)
                {
                    currentCrystal = crystal.Value;
                }
            }
        }

        private string FormatResetTimer(ulong resetTimestampMs)
        {
            if (resetTimestampMs == 0) return string.Empty;

            long remainingMs = (long)resetTimestampMs - ServerTimeCache.Instance.NowMs;
            if (remainingMs < 0) remainingMs = 0;

            var span = TimeSpan.FromMilliseconds(remainingMs);
            int hours = (int)Math.Floor(span.TotalHours);
            return $"{hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        #endregion

        #region Slot Helpers

        private void UpdateSlotButton(Button button, TextMeshProUGUI costText, int uiSlotNumber)
        {
            if (button == null && costText == null) return;

            int serverSlotIndex = uiSlotNumber - 1;
            bool unlocked = IsSlotUnlocked(serverSlotIndex);
            bool pending = IsUnlockPending(serverSlotIndex);
            int cost = GetCost(serverSlotIndex);
            bool canAfford = currentCrystal >= cost;
            bool interactable = !unlocked && !pending && canAfford;

            if (button != null)
            {
                button.interactable = interactable;
            }

            if (costText != null)
            {
                if (unlocked)
                {
                    costText.text = "해금 완료";
                }
                else if (pending)
                {
                    costText.text = "해금 중...";
                }
                else
                {
                    costText.text = $"슬롯 {uiSlotNumber}: {cost:N0} 💎";
                }
            }

            // 인스펙터에서 확인용 Legacy 필드 업데이트
            slot2Unlocked = IsSlotUnlocked(1);
            slot3Unlocked = IsSlotUnlocked(2);
            slot4Unlocked = IsSlotUnlocked(3);
        }

        private bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < slotUnlockedStates.Length;
        }

        private bool IsSlotUnlocked(int slotIndex)
        {
            return IsValidSlotIndex(slotIndex) && slotUnlockedStates[slotIndex];
        }

        private bool IsUnlockPending(int slotIndex)
        {
            return IsValidSlotIndex(slotIndex) && unlockInProgress[slotIndex];
        }

        private int GetCost(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < slotCosts.Length)
            {
                return slotCosts[slotIndex];
            }
            return 0;
        }

        private bool CanUnlock(int slotIndex)
        {
            return IsValidSlotIndex(slotIndex)
                   && !IsSlotUnlocked(slotIndex)
                   && !IsUnlockPending(slotIndex)
                   && currentCrystal >= GetCost(slotIndex);
        }

        private void MarkSlotUnlocked(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex)) return;
            slotUnlockedStates[slotIndex] = true;
        }

        private void SyncSlotsFrom(IEnumerable<PickaxeSlotInfo> slots)
        {
            if (slots == null) return;

            foreach (var slot in slots)
            {
                if (slot == null) continue;
                if (!IsValidSlotIndex((int)slot.SlotIndex)) continue;
                slotUnlockedStates[slot.SlotIndex] = slot.IsUnlocked;
                if (slot.IsUnlocked)
                {
                    unlockInProgress[slot.SlotIndex] = false;
                }
            }
        }

        private void SyncSlotsFromCache()
        {
            slotUnlockedStates[0] = true;
            if (pickaxeCache == null || pickaxeCache.Slots == null) return;

            foreach (var kvp in pickaxeCache.Slots)
            {
                var slotIndex = (int)kvp.Key;
                if (!IsValidSlotIndex(slotIndex)) continue;
                var info = kvp.Value;
                if (info == null) continue;
                slotUnlockedStates[slotIndex] = info.IsUnlocked;
            }
        }

        #endregion
    }
}
