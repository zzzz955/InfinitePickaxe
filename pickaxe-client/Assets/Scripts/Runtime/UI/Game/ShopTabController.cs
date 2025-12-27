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
    /// 보석 뽑기, IAP 상품 관리
    /// </summary>
    public class ShopTabController : BaseTabController
    {
        [Header("Tab Switching UI")]
        [SerializeField] private Button gemsTabButton;
        [SerializeField] private Button iapTabButton;

        [Header("SubTab Content")]
        [SerializeField] private GameObject gemShopSubTab;
        [SerializeField] private GameObject iapShopSubTab;

        [Header("Gem Shop UI")]
        [SerializeField] private TextMeshProUGUI gemCurrentCrystalText;
        [SerializeField] private Button gemSinglePullButton;
        [SerializeField] private Button gemMultiPullButton;

        [Header("Ad UI References (임시, Step 3에서 HUD로 이동)")]
        [SerializeField] private Button watchAdButton1;
        [SerializeField] private Button watchAdButton2;
        [SerializeField] private Button watchAdButton3;
        [SerializeField] private TextMeshProUGUI adCountText;

        [Header("Shop Data")]
        [SerializeField] private int watchedAdCount = 0;
        [SerializeField] private int maxAdCount = 3;

        private const string CrystalRewardAdType = "crystal_reward";
        private const int SinglePullCost = 50;
        private const int MultiPullCost = 500;
        private const int MultiPullCount = 11;

        private uint currentCrystal;
        private enum SubTab { Gems, IAP }
        private SubTab currentSubTab = SubTab.Gems;

        private MessageHandler messageHandler;
        private QuestStateCache questState;
        private bool adStateSubscribed;
        private float nextTimerRefreshTime;

        protected override void Initialize()
        {
            base.Initialize();

            messageHandler = MessageHandler.Instance;

            // 탭 전환 버튼 이벤트 등록
            if (gemsTabButton != null)
            {
                gemsTabButton.onClick.AddListener(() => SwitchToGemTab());
            }
            if (iapTabButton != null)
            {
                iapTabButton.onClick.AddListener(() => SwitchToIAPTab());
            }

            // 보석 뽑기 버튼 이벤트 등록
            if (gemSinglePullButton != null)
            {
                gemSinglePullButton.onClick.AddListener(() => OnGemPullClicked(false));
            }
            if (gemMultiPullButton != null)
            {
                gemMultiPullButton.onClick.AddListener(() => OnGemPullClicked(true));
            }

            // 광고 버튼 이벤트 등록 (임시)
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

            // 서브탭 AutoBind
            AutoBindSubTabs();

            // 기본 탭 활성화
            SwitchToGemTab();
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
            SubscribeAdState();
            RefreshData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnsubscribeMessageHandler();
            UnsubscribeAdState();
        }

        private void Update()
        {
            if (!isActive) return;
            if (Time.unscaledTime < nextTimerRefreshTime) return;

            nextTimerRefreshTime = Time.unscaledTime + 1f;
            UpdateAdCount();
        }

        private void OnDestroy()
        {
            UnsubscribeMessageHandler();
            UnsubscribeAdState();
        }

        /// <summary>
        /// 상점 UI 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateAdCount();
            UpdateGemShopUI();
        }

        #region Tab Switching

        private void AutoBindSubTabs()
        {
            if (gemShopSubTab == null)
            {
                var existingGemTab = GameObject.Find("GemShopSubTab");
                if (existingGemTab == null)
                {
                    var prefab = Resources.Load<GameObject>("UI/GemShopSubTab");
                    if (prefab != null)
                    {
                        gemShopSubTab = Instantiate(prefab, transform);
                        gemShopSubTab.name = "GemShopSubTab";

                        // UI 컴포넌트 바인딩
                        var currentCrystalTf = FindChildRecursive(gemShopSubTab.transform, "CurrentCrystalText");
                        if (currentCrystalTf != null)
                        {
                            gemCurrentCrystalText = currentCrystalTf.GetComponent<TextMeshProUGUI>();
                        }

                        var singleBtnTf = FindChildRecursive(gemShopSubTab.transform, "SinglePullButton");
                        if (singleBtnTf != null)
                        {
                            gemSinglePullButton = singleBtnTf.GetComponent<Button>();
                            if (gemSinglePullButton != null)
                            {
                                gemSinglePullButton.onClick.RemoveAllListeners();
                                gemSinglePullButton.onClick.AddListener(() => OnGemPullClicked(false));
                            }
                        }

                        var multiBtnTf = FindChildRecursive(gemShopSubTab.transform, "MultiPullButton");
                        if (multiBtnTf != null)
                        {
                            gemMultiPullButton = multiBtnTf.GetComponent<Button>();
                            if (gemMultiPullButton != null)
                            {
                                gemMultiPullButton.onClick.RemoveAllListeners();
                                gemMultiPullButton.onClick.AddListener(() => OnGemPullClicked(true));
                            }
                        }
                    }
                }
                else
                {
                    gemShopSubTab = existingGemTab;
                }
            }

            if (iapShopSubTab == null)
            {
                var existingIAPTab = GameObject.Find("IAPShopSubTab");
                if (existingIAPTab == null)
                {
                    var prefab = Resources.Load<GameObject>("UI/IAPShopSubTab");
                    if (prefab != null)
                    {
                        iapShopSubTab = Instantiate(prefab, transform);
                        iapShopSubTab.name = "IAPShopSubTab";
                    }
                }
                else
                {
                    iapShopSubTab = existingIAPTab;
                }
            }
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                var found = FindChildRecursive(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void SwitchToGemTab()
        {
            currentSubTab = SubTab.Gems;

            if (gemShopSubTab != null)
            {
                gemShopSubTab.SetActive(true);
            }
            if (iapShopSubTab != null)
            {
                iapShopSubTab.SetActive(false);
            }

            // 탭 버튼 색상 업데이트
            UpdateTabButtonColors();
            UpdateGemShopUI();
        }

        private void SwitchToIAPTab()
        {
            currentSubTab = SubTab.IAP;

            if (gemShopSubTab != null)
            {
                gemShopSubTab.SetActive(false);
            }
            if (iapShopSubTab != null)
            {
                iapShopSubTab.SetActive(true);
            }

            // 탭 버튼 색상 업데이트
            UpdateTabButtonColors();
        }

        private void UpdateTabButtonColors()
        {
            if (gemsTabButton != null)
            {
                var colors = gemsTabButton.colors;
                colors.normalColor = currentSubTab == SubTab.Gems
                    ? new Color(0.3f, 0.6f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f);
                gemsTabButton.colors = colors;
            }

            if (iapTabButton != null)
            {
                var colors = iapTabButton.colors;
                colors.normalColor = currentSubTab == SubTab.IAP
                    ? new Color(0.3f, 0.6f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f);
                iapTabButton.colors = colors;
            }
        }

        #endregion

        #region Gem Shop UI

        private void UpdateGemShopUI()
        {
            // 크리스탈 보유량 업데이트
            if (gemCurrentCrystalText != null)
            {
                gemCurrentCrystalText.text = $"보유: {currentCrystal} 💎";
            }

            // 단일 뽑기 버튼 활성화/비활성화
            if (gemSinglePullButton != null)
            {
                gemSinglePullButton.interactable = currentCrystal >= SinglePullCost;
            }

            // 멀티 뽑기 버튼 활성화/비활성화
            if (gemMultiPullButton != null)
            {
                gemMultiPullButton.interactable = currentCrystal >= MultiPullCost;
            }
        }

        private void OnGemPullClicked(bool isMulti)
        {
            int cost = isMulti ? MultiPullCost : SinglePullCost;
            int count = isMulti ? MultiPullCount : 1;

            if (currentCrystal < cost)
            {
                Debug.LogWarning($"ShopTabController: 크리스탈 부족 (필요: {cost}, 보유: {currentCrystal})");
                return;
            }

            // 서버로 보석 뽑기 요청 전송 (프로토콜 추가 필요)
            Debug.Log($"ShopTabController: 보석 뽑기 요청 (개수: {count}, 비용: {cost} 💎)");
            // TODO: messageHandler?.RequestGemPull(isMulti);
        }

        #endregion

        #region Ad UI (임시)

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
                var textValue = $"광고 보상 (오늘 {watchedAdCount}/{maxAdCount})";
                if (!string.IsNullOrEmpty(timer))
                {
                    textValue += $" | 리셋 {timer}";
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

        /// <summary>
        /// 광고 시청 버튼 클릭 이벤트
        /// </summary>
        private void OnWatchAdClicked(int tier)
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.NotifyAdWatchComplete(CrystalRewardAdType);
            Debug.Log($"ShopTabController: 광고 시청 완료 (Tier {tier})");
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

            ApplyLastKnownCurrency();
        }

        private void UnsubscribeMessageHandler()
        {
            if (messageHandler == null) return;

            messageHandler.OnHandshakeResult -= HandleHandshake;
            messageHandler.OnUserDataSnapshot -= HandleSnapshot;
            messageHandler.OnCurrencyUpdate -= HandleCurrencyUpdate;
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

        #endregion
    }
}
