using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class InfiniteMineModalController : MonoBehaviour
    {
        [Header("Modal UI")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resetTimerText;
        [SerializeField] private Button autoClaimAllButton;
        [SerializeField] private ScrollRect floorScrollRect;
        [SerializeField] private RectTransform floorContent;
        [SerializeField] private GameObject floorCardPrefab;
        [SerializeField] private RewardStoveModalController rewardStoveModal;

        private InfiniteMineMetaResolver metaResolver;
        private InfiniteMineStateCache stateCache;
        private MessageHandler messageHandler;
        private readonly List<InfiniteMineStageCardView> floorViews = new List<InfiniteMineStageCardView>();
        private bool subscribed;
        private bool stateRequested;
        private bool pendingFocus;
        private uint pendingFocusFloor;
        private Coroutine focusRoutine;

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            Subscribe();
            RequestStateIfNeeded();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Show()
        {
            EnsureReferences();
            pendingFocus = true;
            pendingFocusFloor = GetTargetFocusFloor();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            transform.SetAsLastSibling();
            RefreshAll();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Subscribe()
        {
            if (subscribed) return;
            stateCache = InfiniteMineStateCache.Instance;
            messageHandler ??= MessageHandler.Instance;

            if (stateCache != null)
            {
                stateCache.OnStateChanged += HandleStateChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnInfiniteMineAutoClaimResult += HandleAutoClaimResult;
                messageHandler.OnInfiniteMineAutoClaimAllResult += HandleAutoClaimAllResult;
                messageHandler.OnInfiniteMineChallengeResult += HandleChallengeResult;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (stateCache != null)
            {
                stateCache.OnStateChanged -= HandleStateChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnInfiniteMineAutoClaimResult -= HandleAutoClaimResult;
                messageHandler.OnInfiniteMineAutoClaimAllResult -= HandleAutoClaimAllResult;
                messageHandler.OnInfiniteMineChallengeResult -= HandleChallengeResult;
            }

            subscribed = false;
        }

        private void HandleStateChanged()
        {
            if (pendingFocus)
            {
                pendingFocusFloor = GetTargetFocusFloor();
            }
            RefreshAll();
        }

        private void HandleAutoClaimResult(InfiniteMineAutoClaimResult result)
        {
            if (result == null || !result.Success) return;
            ShowReward(result.RewardCrystal, result.RewardGold);
        }

        private void HandleAutoClaimAllResult(InfiniteMineAutoClaimAllResult result)
        {
            if (result == null || !result.Success) return;
            ShowReward(result.TotalRewardCrystal, result.TotalRewardGold);
        }

        private void HandleChallengeResult(InfiniteMineChallengeResult result)
        {
            if (result == null || !result.Success) return;
            ShowReward(result.RewardCrystal, result.RewardGold);
        }

        private void RequestStateIfNeeded()
        {
            if (stateRequested) return;
            if (stateCache != null && stateCache.HasState) return;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineState();
            stateRequested = true;
        }

        private void RefreshAll()
        {
            EnsureMeta();
            EnsureFloorContent();
            UpdateHeader();
            UpdateAutoClaimAllButton();
            UpdateFloorCards();
            TryFocusPendingFloor();
        }

        private void EnsureMeta()
        {
            if (metaResolver == null)
            {
                metaResolver = new InfiniteMineMetaResolver();
            }
            else if (MetaRepository.Loaded && metaResolver.Floors.Count == 0)
            {
                metaResolver.Reload();
            }

            // 무한의 갱도 메타만 사용
        }

        private void UpdateHeader()
        {
            uint maxFloor = GetMaxFloor();
            uint highest = stateCache != null && stateCache.HasState ? stateCache.HighestClearedFloor : 0;

            if (titleText != null)
            {
                titleText.text = $"INFINITE MINE {highest:N0}/{maxFloor:N0}";
            }

            if (resetTimerText != null)
            {
                ulong resetMs = stateCache != null && stateCache.HasState ? stateCache.ResetTimestampMs : 0;
                resetTimerText.text = FormatResetTimer(resetMs);
            }
        }

        private void UpdateAutoClaimAllButton()
        {
            if (autoClaimAllButton == null) return;
            bool hasClaimable = HasAnyAutoClaimable();
            autoClaimAllButton.interactable = hasClaimable;
        }

        private bool HasAnyAutoClaimable()
        {
            if (stateCache == null || !stateCache.HasState) return false;
            foreach (var kvp in stateCache.FloorStates)
            {
                if (kvp.Value != null && kvp.Value.AutoClaimable)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateFloorCards()
        {
            uint maxFloor = GetMaxFloor();
            EnsureFloorCards(maxFloor);

            uint highestCleared = stateCache != null && stateCache.HasState ? stateCache.HighestClearedFloor : 0;
            uint currentChallengeFloor = highestCleared < maxFloor ? highestCleared + 1 : 0;
            uint divisor = metaResolver != null && metaResolver.AutoRewardDivisor > 0 ? metaResolver.AutoRewardDivisor : 10;

            for (uint floor = 1; floor <= maxFloor; floor++)
            {
                var view = floorViews[(int)floor - 1];
                if (view == null) continue;

                bool isCleared = floor <= highestCleared && highestCleared > 0;
                bool isCurrent = currentChallengeFloor > 0 && floor == currentChallengeFloor;
                bool isLocked = !isCleared && !isCurrent;

                bool autoClaimable = stateCache != null && stateCache.IsAutoClaimable(floor);
                bool autoClaimedToday = stateCache != null && stateCache.IsAutoClaimedToday(floor);

                InfiniteMineFloorMeta floorMeta = null;
                if (metaResolver != null)
                {
                    metaResolver.TryGetFloor(floor, out floorMeta);
                }

                ulong baseGold = floorMeta != null ? floorMeta.RewardGold : 0;
                uint baseCrystal = floorMeta != null ? (uint)Math.Min(floorMeta.RewardCrystal, uint.MaxValue) : 0;

                bool useAutoReward = isCleared;
                ulong rewardGold = useAutoReward ? baseGold / divisor : baseGold;
                uint rewardCrystal = useAutoReward ? (uint)(baseCrystal / divisor) : baseCrystal;

                var data = new InfiniteMineStageCardData
                {
                    Floor = floor,
                    IsCleared = isCleared,
                    IsCurrent = isCurrent,
                    IsLocked = isLocked,
                    CanChallenge = isCurrent,
                    CanAutoClaim = isCleared && autoClaimable,
                    AutoClaimedToday = autoClaimedToday,
                    RewardGold = rewardGold,
                    RewardCrystal = rewardCrystal
                };

                view.Apply(data, OnChallengeClicked, OnAutoClaimClicked);
            }
        }

        private void EnsureFloorCards(uint maxFloor)
        {
            if (floorContent == null) return;
            if (floorCardPrefab == null) return;

            int maxCount = maxFloor > int.MaxValue ? int.MaxValue : (int)maxFloor;

            while (floorViews.Count < maxCount)
            {
                var instance = Instantiate(floorCardPrefab, floorContent, false);
                instance.name = $"InfiniteMineFloor_{floorViews.Count + 1}";
                instance.SetActive(true);
                var view = instance.GetComponentInChildren<InfiniteMineStageCardView>(true);
                if (view == null)
                {
                    view = instance.AddComponent<InfiniteMineStageCardView>();
                }
                floorViews.Add(view);
            }

            for (int i = 0; i < floorViews.Count; i++)
            {
                var view = floorViews[i];
                if (view != null)
                {
                    view.gameObject.SetActive(i < maxCount);
                }
            }

            if (floorCardPrefab.scene.IsValid() && floorCardPrefab.activeSelf)
            {
                floorCardPrefab.SetActive(false);
            }
        }

        private void EnsureFloorContent()
        {
            if (floorScrollRect == null)
            {
                var tf = transform.Find("FloorScrollRect");
                if (tf != null) floorScrollRect = tf.GetComponent<ScrollRect>();
            }

            if (floorContent == null && floorScrollRect != null)
            {
                floorContent = floorScrollRect.content;
            }

            if (floorContent == null)
            {
                var tf = transform.Find("FloorContent");
                if (tf != null) floorContent = tf.GetComponent<RectTransform>();
            }

            if (floorCardPrefab == null && floorContent != null)
            {
                var tf = floorContent.Find("FloorCardPrefab");
                if (tf == null)
                {
                    tf = floorContent.Find("StageCardPrefab");
                }
                if (tf != null) floorCardPrefab = tf.gameObject;
            }
        }

        private void TryFocusPendingFloor()
        {
            if (!pendingFocus) return;
            if (stateCache != null && !stateCache.HasState) return;
            pendingFocus = false;

            uint floor = pendingFocusFloor;
            if (floor == 0) return;

            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
            }
            focusRoutine = StartCoroutine(FocusFloorRoutine(floor));
        }

        private IEnumerator FocusFloorRoutine(uint floor)
        {
            yield return null;
            FocusFloor(floor);
            focusRoutine = null;
        }

        private void FocusFloor(uint floor)
        {
            if (floorScrollRect == null || floorContent == null) return;
            if (floor == 0 || floor > (uint)floorViews.Count) return;

            var target = floorViews[(int)floor - 1];
            if (target == null) return;
            var targetRect = target.GetComponent<RectTransform>();
            if (targetRect == null) return;

            var viewport = floorScrollRect.viewport != null ? floorScrollRect.viewport : floorScrollRect.GetComponent<RectTransform>();
            if (viewport == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(floorContent);
            Canvas.ForceUpdateCanvases();

            var contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(floorContent);
            var targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(floorContent, targetRect);
            float contentHeight = contentBounds.size.y;
            float viewportHeight = viewport.rect.height;
            if (contentHeight <= viewportHeight + 0.01f) return;

            float normalized = (targetBounds.center.y - contentBounds.min.y) / contentHeight;
            float viewportNormalizedHeight = viewportHeight / contentHeight;
            float desired = Mathf.Clamp01(normalized - viewportNormalizedHeight * 0.5f);

            var pos = floorScrollRect.normalizedPosition;
            pos.y = 1f - desired;
            floorScrollRect.normalizedPosition = pos;
        }

        private uint GetMaxFloor()
        {
            uint max = stateCache != null && stateCache.HasState ? stateCache.MaxFloor : 0;
            if (max == 0 && metaResolver != null && metaResolver.MaxFloor > 0)
            {
                max = metaResolver.MaxFloor;
            }
            return max == 0 ? 100u : max;
        }

        private uint GetTargetFocusFloor()
        {
            uint maxFloor = GetMaxFloor();
            uint highest = stateCache != null && stateCache.HasState ? stateCache.HighestClearedFloor : 0;
            uint target = highest < maxFloor ? highest + 1 : maxFloor;
            if (maxFloor == 0) return 1;
            if (target < 1) return 1;
            if (target > maxFloor) return maxFloor;
            return target;
        }

        private void OnChallengeClicked(uint floor)
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineChallengeStart(floor);
        }

        private void OnAutoClaimClicked(uint floor)
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineAutoClaim(floor);
        }

        private void OnAutoClaimAllClicked()
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineAutoClaimAll();
        }

        private void ShowReward(uint rewardCrystal, ulong rewardGold)
        {
            if (rewardCrystal == 0 && rewardGold == 0) return;
            AutoBindRewardStoveModal();
            rewardStoveModal?.Show(rewardCrystal, rewardGold);
        }

        private void AutoBindRewardStoveModal()
        {
            if (rewardStoveModal != null) return;
            var modalObj = GameObject.Find("RewardStoveModal");
            if (modalObj != null)
            {
                rewardStoveModal = modalObj.GetComponent<RewardStoveModalController>();
                return;
            }

            var prefab = Resources.Load<GameObject>("UI/RewardStoveModal");
            if (prefab == null) return;
            var instance = Instantiate(prefab, transform.root);
            instance.name = "RewardStoveModal";
            rewardStoveModal = instance.GetComponent<RewardStoveModalController>();
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

        private void BindButtons()
        {
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(Hide);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            if (autoClaimAllButton != null)
            {
                autoClaimAllButton.onClick.RemoveAllListeners();
                autoClaimAllButton.onClick.AddListener(OnAutoClaimAllClicked);
            }
        }

        private void EnsureReferences()
        {
            if (backgroundButton == null)
            {
                var tf = transform.Find("Background");
                if (tf != null) backgroundButton = tf.GetComponent<Button>();
                if (backgroundButton == null) backgroundButton = GetComponent<Button>();
            }

            if (closeButton == null)
            {
                var tf = transform.Find("ModalPanel/CloseButton");
                if (tf != null) closeButton = tf.GetComponent<Button>();
            }

            if (titleText == null)
            {
                titleText = FindText("ModalPanel/TitleText", "TitleText");
            }

            if (resetTimerText == null)
            {
                resetTimerText = FindText("ModalPanel/ResetTimerText", "ResetTimerText");
            }

            if (autoClaimAllButton == null)
            {
                var tf = transform.Find("ModalPanel/AutoClaimAllButton");
                if (tf != null) autoClaimAllButton = tf.GetComponent<Button>();
            }

            EnsureFloorContent();
            AutoBindRewardStoveModal();
        }

        private TextMeshProUGUI FindText(string path, string fallbackName)
        {
            var target = transform.Find(path);
            if (target != null)
            {
                var text = target.GetComponent<TextMeshProUGUI>();
                if (text != null) return text;
            }

            if (string.IsNullOrEmpty(fallbackName)) return null;
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == fallbackName)
                {
                    return texts[i];
                }
            }

            return null;
        }
    }
}
