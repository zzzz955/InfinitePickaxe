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
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resetTimerText;
        [SerializeField] private Button autoClaimAllButton;
        [SerializeField] private ScrollRect floorScrollRect;
        [SerializeField] private RectTransform floorContent;
        [SerializeField] private GameObject floorCardPrefab;
        [SerializeField] private RewardStoveModalController rewardStoveModal;
        [SerializeField] private InfiniteMineSimulationViewController simulationView;

        [Header("Floor List Virtualization")]
        [SerializeField] private float floorItemHeight = 0f;
        [SerializeField] private float floorItemSpacing = 0f;
        [SerializeField] private int floorItemPaddingTop = 0;
        [SerializeField] private int floorItemPaddingBottom = 0;
        [SerializeField] private int floorItemPoolExtra = 2;
        [SerializeField] private bool disableLayoutComponents = true;

        private InfiniteMineMetaResolver metaResolver;
        private InfiniteMineStateCache stateCache;
        private MessageHandler messageHandler;
        private readonly Queue<InfiniteMineStageCardView> floorViewPool = new Queue<InfiniteMineStageCardView>();
        private readonly Dictionary<int, InfiniteMineStageCardView> activeFloorViews = new Dictionary<int, InfiniteMineStageCardView>();
        private VerticalLayoutGroup cachedLayoutGroup;
        private ContentSizeFitter cachedContentSizeFitter;
        private bool layoutMetricsLoaded;
        private bool listInitialized;
        private int cachedMaxFloor;
        private float cachedContentHeight;
        private bool subscribed;
        private bool stateRequested;
        private bool pendingFocus;
        private uint pendingFocusFloor;
        private Coroutine focusRoutine;
        private Coroutine resetTimerRoutine;

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            Subscribe();
            RequestStateIfNeeded();
            ResetVirtualListState();
            RefreshAll();
            StartResetTimerTicker();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnbindScrollListener();
            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
                focusRoutine = null;
            }
            StopResetTimerTicker();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;
            if (!gameObject.activeInHierarchy) return;
            ResetVirtualListState();
            RefreshAll();
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
            ResetVirtualListState();
            RefreshAll();
            StartResetTimerTicker();
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
                messageHandler.OnInfiniteMineChallengeStartResult += HandleChallengeStartResult;
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
                messageHandler.OnInfiniteMineChallengeStartResult -= HandleChallengeStartResult;
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

        private void HandleChallengeStartResult(InfiniteMineChallengeStartResult result)
        {
            if (result == null || !result.Success) return;
            AutoBindSimulationView();
            if (simulationView == null) return;
            simulationView.Show();
            simulationView.ApplyStartResult(result);
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
            uint highest = GetHighestClearedFloor(maxFloor);

            if (titleText != null)
            {
                titleText.text = $"무한의 갱도 {highest:N0}/{maxFloor:N0}";
            }

            UpdateResetTimerText();
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
            EnsureVirtualList(maxFloor);
            UpdateVisibleItems();
        }

        private void ResetVirtualListState()
        {
            listInitialized = false;
            layoutMetricsLoaded = false;
            cachedMaxFloor = 0;
            cachedContentHeight = 0f;
            RecycleAllViews();
        }

        private void EnsureVirtualList(uint maxFloor)
        {
            EnsureFloorContent();
            EnsureLayoutMetrics();

            if (floorContent == null || floorCardPrefab == null)
            {
                return;
            }

            int maxCount = maxFloor > int.MaxValue ? int.MaxValue : (int)maxFloor;
            cachedMaxFloor = maxCount;
            UpdateContentHeight(maxCount);
            EnsurePoolForViewport(maxCount);
            listInitialized = true;
        }

        private void EnsureLayoutMetrics()
        {
            if (layoutMetricsLoaded) return;
            if (floorContent == null) return;

            cachedLayoutGroup = floorContent.GetComponent<VerticalLayoutGroup>();
            cachedContentSizeFitter = floorContent.GetComponent<ContentSizeFitter>();

            if (cachedLayoutGroup != null)
            {
                if (floorItemSpacing <= 0f)
                {
                    floorItemSpacing = cachedLayoutGroup.spacing;
                }

                if (floorItemPaddingTop == 0 && floorItemPaddingBottom == 0)
                {
                    floorItemPaddingTop = cachedLayoutGroup.padding.top;
                    floorItemPaddingBottom = cachedLayoutGroup.padding.bottom;
                }
            }

            if (floorItemHeight <= 0f && floorCardPrefab != null)
            {
                var rect = floorCardPrefab.GetComponent<RectTransform>();
                if (rect != null)
                {
                    var height = rect.rect.height;
                    if (height <= 0f)
                    {
                        height = rect.sizeDelta.y;
                    }
                    if (height > 0f)
                    {
                        floorItemHeight = height;
                    }
                }

                if (floorItemHeight <= 0f)
                {
                    var layout = floorCardPrefab.GetComponent<LayoutElement>();
                    if (layout != null)
                    {
                        if (layout.preferredHeight > 0f)
                        {
                            floorItemHeight = layout.preferredHeight;
                        }
                        else if (layout.minHeight > 0f)
                        {
                            floorItemHeight = layout.minHeight;
                        }
                    }
                }
            }

            if (floorItemHeight <= 0f)
            {
                floorItemHeight = 200f;
            }

            if (disableLayoutComponents)
            {
                if (cachedLayoutGroup != null) cachedLayoutGroup.enabled = false;
                if (cachedContentSizeFitter != null) cachedContentSizeFitter.enabled = false;
            }

            layoutMetricsLoaded = true;
        }

        private void UpdateContentHeight(int totalCount)
        {
            float height = 0f;
            if (totalCount > 0)
            {
                height = floorItemPaddingTop
                         + floorItemPaddingBottom
                         + totalCount * floorItemHeight
                         + (totalCount - 1) * floorItemSpacing;
            }

            cachedContentHeight = height;

            if (floorContent != null)
            {
                var size = floorContent.sizeDelta;
                size.y = height;
                floorContent.sizeDelta = size;
            }
        }

        private void EnsurePoolForViewport(int totalCount)
        {
            if (floorScrollRect == null || floorContent == null || floorCardPrefab == null) return;

            var viewport = floorScrollRect.viewport != null ? floorScrollRect.viewport : floorScrollRect.GetComponent<RectTransform>();
            float viewportHeight = viewport != null ? viewport.rect.height : 0f;
            float step = floorItemHeight + floorItemSpacing;

            int visibleCount = 1;
            if (viewportHeight > 0f && step > 0f)
            {
                visibleCount = Mathf.CeilToInt(viewportHeight / step) + 1;
            }

            int targetPoolSize = Mathf.Min(totalCount, visibleCount + Mathf.Max(0, floorItemPoolExtra));
            int currentCount = activeFloorViews.Count + floorViewPool.Count;

            while (currentCount < targetPoolSize)
            {
                var view = CreateFloorView();
                view.gameObject.SetActive(false);
                floorViewPool.Enqueue(view);
                currentCount++;
            }
        }

        private InfiniteMineStageCardView CreateFloorView()
        {
            var instance = Instantiate(floorCardPrefab, floorContent, false);
            instance.name = "InfiniteMineFloorItem";
            instance.SetActive(true);
            var view = instance.GetComponentInChildren<InfiniteMineStageCardView>(true);
            if (view == null)
            {
                view = instance.AddComponent<InfiniteMineStageCardView>();
            }

            var rect = view.GetComponent<RectTransform>();
            PrepareItemRect(rect);
            return view;
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

            if (floorContent != null)
            {
                var scale = floorContent.localScale;
                if (scale.y < 0f)
                {
                    scale.y = Mathf.Abs(scale.y);
                    floorContent.localScale = scale;
                }
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

            if (floorCardPrefab != null && floorCardPrefab.scene.IsValid() && floorCardPrefab.activeSelf)
            {
                floorCardPrefab.SetActive(false);
            }

            BindScrollListener();
        }

        private void BindScrollListener()
        {
            if (floorScrollRect == null) return;
            floorScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            floorScrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        private void UnbindScrollListener()
        {
            if (floorScrollRect == null) return;
            floorScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }

        private void OnScrollValueChanged(Vector2 _)
        {
            UpdateVisibleItems();
        }

        private void UpdateVisibleItems()
        {
            if (!listInitialized || cachedMaxFloor <= 0)
            {
                RecycleAllViews();
                return;
            }

            GetVisibleRange(out var startIndex, out var endIndex);
            if (endIndex < startIndex)
            {
                RecycleAllViews();
                return;
            }

            var activeKeys = new List<int>(activeFloorViews.Keys);
            for (int i = 0; i < activeKeys.Count; i++)
            {
                int index = activeKeys[i];
                if (index < startIndex || index > endIndex)
                {
                    RecycleView(index);
                }
            }

            uint maxFloor = (uint)cachedMaxFloor;
            uint highestCleared = GetHighestClearedFloor(maxFloor);
            uint currentChallengeFloor = highestCleared < maxFloor ? highestCleared + 1 : 0;
            uint divisor = metaResolver != null && metaResolver.AutoRewardDivisor > 0 ? metaResolver.AutoRewardDivisor : 10;

            for (int index = startIndex; index <= endIndex; index++)
            {
                if (!activeFloorViews.TryGetValue(index, out var view) || view == null)
                {
                    view = AcquireView();
                    activeFloorViews[index] = view;
                }

                PositionView(view, index);

                var data = BuildStageCardData(index, maxFloor, highestCleared, currentChallengeFloor, divisor);
                view.Apply(data, OnChallengeClicked, OnAutoClaimClicked);
            }
        }

        private void GetVisibleRange(out int startIndex, out int endIndex)
        {
            startIndex = 0;
            endIndex = cachedMaxFloor - 1;

            if (floorScrollRect == null || cachedMaxFloor <= 0)
            {
                return;
            }

            var viewport = floorScrollRect.viewport != null ? floorScrollRect.viewport : floorScrollRect.GetComponent<RectTransform>();
            if (viewport == null)
            {
                return;
            }

            float viewportHeight = viewport.rect.height;
            float step = floorItemHeight + floorItemSpacing;
            if (viewportHeight <= 0f || step <= 0f || cachedContentHeight <= viewportHeight)
            {
                return;
            }

            float maxScroll = Mathf.Max(0f, cachedContentHeight - viewportHeight);
            float scrollY = maxScroll * (1f - floorScrollRect.verticalNormalizedPosition);
            float startY = scrollY - floorItemPaddingTop;

            int rawStart = Mathf.FloorToInt(startY / step);
            int visibleCount = Mathf.CeilToInt(viewportHeight / step) + 1;
            int buffer = Mathf.Max(0, floorItemPoolExtra);

            startIndex = Mathf.Max(0, rawStart - buffer);
            endIndex = Mathf.Min(cachedMaxFloor - 1, startIndex + visibleCount + buffer * 2);
        }

        private InfiniteMineStageCardView AcquireView()
        {
            InfiniteMineStageCardView view = null;
            while (floorViewPool.Count > 0 && view == null)
            {
                view = floorViewPool.Dequeue();
            }

            if (view == null)
            {
                view = CreateFloorView();
            }

            view.gameObject.SetActive(true);
            return view;
        }

        private void RecycleView(int index)
        {
            if (!activeFloorViews.TryGetValue(index, out var view) || view == null) return;
            activeFloorViews.Remove(index);
            view.gameObject.SetActive(false);
            floorViewPool.Enqueue(view);
        }

        private void RecycleAllViews()
        {
            var keys = new List<int>(activeFloorViews.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                RecycleView(keys[i]);
            }
        }

        private void PositionView(InfiniteMineStageCardView view, int index)
        {
            if (view == null) return;
            var rect = view.GetComponent<RectTransform>();
            if (rect == null) return;

            PrepareItemRect(rect);

            float y = floorItemPaddingTop + index * (floorItemHeight + floorItemSpacing);
            rect.anchoredPosition = new Vector2(0f, -y);
        }

        private void PrepareItemRect(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            var size = rect.sizeDelta;
            size.x = 0f;
            size.y = floorItemHeight;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }

        private InfiniteMineStageCardData BuildStageCardData(int index, uint maxFloor, uint highestCleared, uint currentChallengeFloor, uint divisor)
        {
            uint floor = (uint)(index + 1);
            if (floor > maxFloor) floor = maxFloor;

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

            return new InfiniteMineStageCardData
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
            if (floor == 0) return;

            EnsureLayoutMetrics();
            EnsureVirtualList(GetMaxFloor());

            var viewport = floorScrollRect.viewport != null ? floorScrollRect.viewport : floorScrollRect.GetComponent<RectTransform>();
            if (viewport == null) return;

            float viewportHeight = viewport.rect.height;
            if (viewportHeight <= 0f) return;

            float contentHeight = cachedContentHeight;
            if (contentHeight <= viewportHeight + 0.01f) return;

            uint totalCount = cachedMaxFloor > 0 ? (uint)cachedMaxFloor : GetMaxFloor();
            uint clampedFloor = floor > totalCount ? totalCount : floor;
            float step = floorItemHeight + floorItemSpacing;

            float centerY = floorItemPaddingTop + (clampedFloor - 1) * step + floorItemHeight * 0.5f;
            float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
            float targetScroll = Mathf.Clamp(centerY - viewportHeight * 0.5f, 0f, maxScroll);
            float normalized = maxScroll > 0f ? 1f - (targetScroll / maxScroll) : 1f;

            floorScrollRect.verticalNormalizedPosition = normalized;
            UpdateVisibleItems();
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

        private uint GetHighestClearedFloor(uint maxFloor)
        {
            if (stateCache == null || !stateCache.HasState) return 0;

            uint highest = 0;
            if (stateCache.FloorStates != null && stateCache.FloorStates.Count > 0)
            {
                foreach (var kvp in stateCache.FloorStates)
                {
                    if (kvp.Key > highest) highest = kvp.Key;
                }
            }
            else
            {
                highest = stateCache.HighestClearedFloor;
            }

            if (maxFloor > 0 && highest > maxFloor) highest = maxFloor;
            return highest;
        }

        private uint GetTargetFocusFloor()
        {
            uint maxFloor = GetMaxFloor();
            uint highest = GetHighestClearedFloor(maxFloor);
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

        private void AutoBindSimulationView()
        {
            if (simulationView != null)
            {
                if (simulationView.gameObject.scene.IsValid()) return;
                var instance = Instantiate(simulationView.gameObject, GetOverlayRoot());
                instance.name = "InfiniteMineSimulationView";
                instance.SetActive(false);
                simulationView = instance.GetComponent<InfiniteMineSimulationViewController>();
                return;
            }

            var modalObj = GameObject.Find("InfiniteMineSimulationView");
            if (modalObj != null)
            {
                simulationView = modalObj.GetComponent<InfiniteMineSimulationViewController>();
                return;
            }

            var prefab = Resources.Load<GameObject>("UI/InfiniteMineSimulationView");
            if (prefab == null) return;
            var newInstance = Instantiate(prefab, GetOverlayRoot());
            newInstance.name = "InfiniteMineSimulationView";
            newInstance.SetActive(false);
            simulationView = newInstance.GetComponent<InfiniteMineSimulationViewController>();
        }

        private Transform GetOverlayRoot()
        {
            var overlayObj = GameObject.Find("InfiniteMineOverlayCanvas");
            if (overlayObj != null) return overlayObj.transform;
            return transform.root;
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

        private void UpdateResetTimerText()
        {
            if (resetTimerText == null) return;
            ulong resetMs = stateCache != null && stateCache.HasState ? stateCache.ResetTimestampMs : 0;
            resetTimerText.text = FormatResetTimer(resetMs);
        }

        private void StartResetTimerTicker()
        {
            if (!gameObject.activeInHierarchy) return;
            if (resetTimerRoutine != null)
            {
                StopCoroutine(resetTimerRoutine);
            }
            resetTimerRoutine = StartCoroutine(ResetTimerRoutine());
        }

        private void StopResetTimerTicker()
        {
            if (resetTimerRoutine == null) return;
            StopCoroutine(resetTimerRoutine);
            resetTimerRoutine = null;
        }

        private IEnumerator ResetTimerRoutine()
        {
            while (true)
            {
                UpdateResetTimerText();
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        private void BindButtons()
        {
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
            AutoBindSimulationView();
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
