using System;
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
    public sealed class MailboxModalController : MonoBehaviour
    {
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button claimAllButton;
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TextMeshProUGUI pageInfoText;
        [SerializeField] private ScrollRect mailScrollRect;
        [SerializeField] private RectTransform mailContent;
        [SerializeField] private GameObject mailCardPrefab;
        [SerializeField] private GameObject mailRewardItemPrefab;
        [SerializeField] private TextMeshProUGUI emptyMessageText;
        [SerializeField] private int fallbackPageSize = 50;

        private MessageHandler messageHandler;
        private MailStateCache mailCache;
        private MailMetaResolver metaResolver;

        private readonly List<GameObject> mailCardInstances = new List<GameObject>();
        private readonly HashSet<string> pendingDetailRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> pendingClaimRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PageCache> pages = new List<PageCache>();

        private int currentPageIndex;
        private int pendingPageIndex = -1;
        private bool listRequestInFlight;
        private bool claimAllInFlight;
        private bool subscribed;

        private sealed class PageCache
        {
            public readonly List<string> MailIds = new List<string>();
            public bool HasNext;
            public ulong NextCursorCreatedAtMs;
            public string NextCursorMailId = string.Empty;
        }

        public void Show()
        {
            EnsureReferences();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RequestFirstPage();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshList();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            mailCache = MailStateCache.Instance;
            messageHandler ??= MessageHandler.Instance;

            if (mailCache != null)
            {
                mailCache.OnMailListChanged += HandleMailListChanged;
                mailCache.OnMailDetailChanged += HandleMailDetailChanged;
                mailCache.OnMailCountsChanged += HandleMailCountsChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnMailListResponse += HandleMailListResponse;
                messageHandler.OnMailClaimResult += HandleMailClaimResult;
                messageHandler.OnMailClaimAllResult += HandleMailClaimAllResult;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;

            if (mailCache != null)
            {
                mailCache.OnMailListChanged -= HandleMailListChanged;
                mailCache.OnMailDetailChanged -= HandleMailDetailChanged;
                mailCache.OnMailCountsChanged -= HandleMailCountsChanged;
            }

            if (messageHandler != null)
            {
                messageHandler.OnMailListResponse -= HandleMailListResponse;
                messageHandler.OnMailClaimResult -= HandleMailClaimResult;
                messageHandler.OnMailClaimAllResult -= HandleMailClaimAllResult;
            }

            subscribed = false;
        }

        private void HandleMailListResponse(MailListResponse response)
        {
            listRequestInFlight = false;
            ApplyListToPage();
            RefreshList();
        }

        private void HandleMailListChanged()
        {
            RefreshList();
        }

        private void HandleMailDetailChanged()
        {
            RefreshList();
        }

        private void HandleMailCountsChanged()
        {
            RefreshList();
        }

        private void HandleMailClaimResult(MailClaimResult result)
        {
            if (result == null) return;
            if (!string.IsNullOrEmpty(result.MailId))
            {
                pendingClaimRequests.Remove(result.MailId);
            }
            RefreshList();
        }

        private void HandleMailClaimAllResult(MailClaimAllResult result)
        {
            claimAllInFlight = false;
            UpdateClaimAllButton();
            if (result != null && result.Success)
            {
                RequestFirstPage();
                return;
            }
            RefreshList();
        }

        private void RequestFirstPage()
        {
            pages.Clear();
            pendingDetailRequests.Clear();
            pendingClaimRequests.Clear();
            currentPageIndex = 0;
            pendingPageIndex = 0;
            RequestMailList(0, string.Empty, 0);
        }

        private void RequestMailList(ulong cursorCreatedAtMs, string cursorMailId, int pageIndex)
        {
            if (listRequestInFlight) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            int limit = ResolvePageSize();
            listRequestInFlight = true;
            pendingPageIndex = pageIndex;
            messageHandler.RequestMailList((uint)limit, cursorCreatedAtMs, cursorMailId, true, false);
        }

        private void RequestMailDetail(string mailId)
        {
            if (string.IsNullOrEmpty(mailId)) return;
            if (pendingDetailRequests.Contains(mailId)) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            pendingDetailRequests.Add(mailId);
            messageHandler.RequestMailDetail(mailId, false);
        }

        private void RequestMailClaim(string mailId)
        {
            if (string.IsNullOrEmpty(mailId)) return;
            if (pendingClaimRequests.Contains(mailId)) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            pendingClaimRequests.Add(mailId);
            messageHandler.RequestMailClaim(mailId);
            RefreshList();
        }

        private void RequestMailClaimAll()
        {
            if (claimAllInFlight) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            claimAllInFlight = true;
            UpdateClaimAllButton();
            messageHandler.RequestMailClaimAll();
        }

        private void ApplyListToPage()
        {
            if (mailCache == null || !mailCache.HasList) return;

            var page = new PageCache();
            page.MailIds.AddRange(mailCache.CurrentMailIds);
            page.HasNext = mailCache.LastHasNext;
            page.NextCursorCreatedAtMs = mailCache.LastNextCursorCreatedAtMs;
            page.NextCursorMailId = mailCache.LastNextCursorMailId ?? string.Empty;

            int targetIndex = pendingPageIndex >= 0 ? pendingPageIndex : currentPageIndex;
            if (targetIndex < 0) targetIndex = 0;

            if (targetIndex < pages.Count)
            {
                pages[targetIndex] = page;
            }
            else if (targetIndex == pages.Count)
            {
                pages.Add(page);
            }
            else
            {
                pages.Add(page);
                targetIndex = pages.Count - 1;
            }

            currentPageIndex = Mathf.Clamp(targetIndex, 0, pages.Count - 1);
            pendingPageIndex = -1;
        }

        private void RefreshList()
        {
            EnsureReferences();
            ClearMailCards();

            var page = GetCurrentPage();
            var entries = BuildSortedEntries(page);

            if (entries.Count == 0)
            {
                SetEmptyState(true);
            }
            else
            {
                SetEmptyState(false);
                for (int i = 0; i < entries.Count; i++)
                {
                    CreateMailCard(entries[i]);
                }
            }

            UpdatePageInfo(page);
            UpdatePageButtons(page);
            UpdateClaimAllButton();
        }

        private List<MailEntry> BuildSortedEntries(PageCache page)
        {
            var result = new List<MailEntry>();
            if (mailCache == null || page == null || page.MailIds.Count == 0) return result;

            var arrivalOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            int count = page.MailIds.Count;
            for (int i = 0; i < count; i++)
            {
                arrivalOrder[page.MailIds[i]] = i;
            }

            for (int i = 0; i < page.MailIds.Count; i++)
            {
                if (mailCache.TryGetEntry(page.MailIds[i], out var entry))
                {
                    result.Add(entry);
                }
            }

            result.Sort((a, b) =>
            {
                bool aClaimable = IsClaimable(a);
                bool bClaimable = IsClaimable(b);
                if (aClaimable != bClaimable)
                {
                    return aClaimable ? -1 : 1;
                }

                int aOrder = arrivalOrder.TryGetValue(a.MailId, out var av) ? av : int.MaxValue;
                int bOrder = arrivalOrder.TryGetValue(b.MailId, out var bv) ? bv : int.MaxValue;
                if (aOrder != bOrder)
                {
                    return aOrder.CompareTo(bOrder);
                }

                return a.CreatedAtMs.CompareTo(b.CreatedAtMs);
            });

            return result;
        }

        private void CreateMailCard(MailEntry entry)
        {
            if (mailCardPrefab == null)
            {
                mailCardPrefab = Resources.Load<GameObject>("UI/MailCardPrefab");
            }
            if (mailRewardItemPrefab == null)
            {
                mailRewardItemPrefab = Resources.Load<GameObject>("UI/MailRewardItem");
            }
            if (mailCardPrefab == null || mailContent == null || entry == null) return;

            var instance = Instantiate(mailCardPrefab, mailContent, false);
            var view = instance.GetComponentInChildren<MailCardView>(true);
            if (view != null)
            {
                var data = BuildCardData(entry);
                view.Apply(data, mailRewardItemPrefab, ResolveRewardRarity, RequestMailClaim);
            }

            mailCardInstances.Add(instance);

            if (!entry.HasDetail)
            {
                RequestMailDetail(entry.MailId);
            }
        }

        private MailCardView.MailCardViewData BuildCardData(MailEntry entry)
        {
            bool hasReward = entry.HasReward || entry.Rewards.Count > 0;
            bool isExpired = IsExpired(entry);
            bool claimable = hasReward && !entry.IsClaimed && !isExpired;
            bool isPending = pendingClaimRequests.Contains(entry.MailId);

            var title = string.IsNullOrEmpty(entry.Title)
                ? "\uC81C\uBAA9 \uC5C6\uC74C"
                : entry.Title;

            return new MailCardView.MailCardViewData
            {
                MailId = entry.MailId,
                Title = title,
                Body = entry.Body ?? string.Empty,
                Sender = entry.Sender ?? string.Empty,
                TimeLabel = FormatTimestamp(entry.CreatedAtMs),
                HasReward = hasReward,
                IsClaimed = entry.IsClaimed,
                IsExpired = isExpired,
                IsClaimable = claimable,
                IsPending = isPending,
                Rewards = entry.Rewards
            };
        }

        private MailRewardItemView.MailRewardRarity ResolveRewardRarity(MailRewardEntry reward)
        {
            if (reward.RewardType == RewardType.Item)
            {
                return MailRewardItemView.MailRewardRarity.Rare;
            }
            return MailRewardItemView.MailRewardRarity.Common;
        }

        private bool IsClaimable(MailEntry entry)
        {
            if (entry == null) return false;
            if (!entry.HasReward && entry.Rewards.Count == 0) return false;
            if (entry.IsClaimed) return false;
            return !IsExpired(entry);
        }

        private bool IsExpired(MailEntry entry)
        {
            if (entry == null) return false;
            if (entry.ExpiresAtMs == 0) return false;
            ulong now = ServerTimeCache.Instance != null
                ? (ulong)ServerTimeCache.Instance.NowMs
                : (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now >= entry.ExpiresAtMs;
        }

        private string FormatTimestamp(ulong timestampMs)
        {
            if (timestampMs == 0) return string.Empty;
            try
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds((long)timestampMs).ToLocalTime();
                return time.ToString("yyyy.MM.dd HH:mm");
            }
            catch
            {
                return string.Empty;
            }
        }

        private PageCache GetCurrentPage()
        {
            if (pages.Count == 0) return null;
            if (currentPageIndex < 0 || currentPageIndex >= pages.Count) return null;
            return pages[currentPageIndex];
        }

        private void UpdatePageInfo(PageCache page)
        {
            if (pageInfoText == null) return;
            if (page == null)
            {
                pageInfoText.text = "0 / 0";
                return;
            }

            string totalLabel = page.HasNext ? "?" : (currentPageIndex + 1).ToString();
            pageInfoText.text = $"{currentPageIndex + 1} / {totalLabel}";
        }

        private void UpdatePageButtons(PageCache page)
        {
            if (prevPageButton != null)
            {
                prevPageButton.interactable = currentPageIndex > 0;
            }

            if (nextPageButton != null)
            {
                bool hasNext = page != null && page.HasNext && !string.IsNullOrEmpty(page.NextCursorMailId);
                nextPageButton.interactable = hasNext;
            }
        }

        private void UpdateClaimAllButton()
        {
            if (claimAllButton == null) return;
            bool canClaim = mailCache != null && mailCache.UnclaimedCount > 0;
            claimAllButton.interactable = canClaim && !claimAllInFlight;
        }

        private void ClearMailCards()
        {
            for (int i = 0; i < mailCardInstances.Count; i++)
            {
                if (mailCardInstances[i] != null)
                {
                    Destroy(mailCardInstances[i]);
                }
            }
            mailCardInstances.Clear();
        }

        private void SetEmptyState(bool isEmpty)
        {
            if (emptyMessageText != null)
            {
                emptyMessageText.gameObject.SetActive(isEmpty);
            }
        }

        private int ResolvePageSize()
        {
            EnsureMeta();
            uint limit = metaResolver != null && metaResolver.DefaultListLimit > 0
                ? metaResolver.DefaultListLimit
                : (uint)Mathf.Max(1, fallbackPageSize);

            if (metaResolver != null && metaResolver.MaxMailCount > 0 && limit > metaResolver.MaxMailCount)
            {
                limit = metaResolver.MaxMailCount;
            }

            if (limit > 50)
            {
                limit = 50;
            }

            return (int)limit;
        }

        private void EnsureMeta()
        {
            if (metaResolver == null)
            {
                metaResolver = new MailMetaResolver();
            }
            else if (MetaRepository.Loaded && !metaResolver.HasData)
            {
                metaResolver.Reload();
            }
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

            if (claimAllButton != null)
            {
                claimAllButton.onClick.RemoveAllListeners();
                claimAllButton.onClick.AddListener(RequestMailClaimAll);
            }

            if (prevPageButton != null)
            {
                prevPageButton.onClick.RemoveAllListeners();
                prevPageButton.onClick.AddListener(OnPrevPageClicked);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveAllListeners();
                nextPageButton.onClick.AddListener(OnNextPageClicked);
            }
        }

        private void OnPrevPageClicked()
        {
            if (currentPageIndex <= 0) return;
            currentPageIndex -= 1;
            RefreshList();
        }

        private void OnNextPageClicked()
        {
            var page = GetCurrentPage();
            if (page == null || !page.HasNext) return;

            int nextIndex = currentPageIndex + 1;
            if (nextIndex < pages.Count)
            {
                currentPageIndex = nextIndex;
                RefreshList();
                return;
            }

            RequestMailList(page.NextCursorCreatedAtMs, page.NextCursorMailId, nextIndex);
        }

        private void EnsureReferences()
        {
            if (backgroundButton == null)
            {
                backgroundButton = GetComponent<Button>();
            }

            if (closeButton == null)
            {
                var tf = transform.Find("ModalPanel/CloseButton");
                if (tf != null) closeButton = tf.GetComponent<Button>();
            }

            if (claimAllButton == null)
            {
                var tf = transform.Find("ModalPanel/ClaimAllButton");
                if (tf != null) claimAllButton = tf.GetComponent<Button>();
            }

            if (prevPageButton == null)
            {
                var tf = transform.Find("ModalPanel/PrevButton");
                if (tf != null) prevPageButton = tf.GetComponent<Button>();
            }

            if (nextPageButton == null)
            {
                var tf = transform.Find("ModalPanel/NextButton");
                if (tf != null) nextPageButton = tf.GetComponent<Button>();
            }

            if (pageInfoText == null)
            {
                var tf = transform.Find("ModalPanel/PageInfoText");
                if (tf != null) pageInfoText = tf.GetComponent<TextMeshProUGUI>();
            }

            if (mailScrollRect == null)
            {
                var tf = transform.Find("ModalPanel/MailScrollView");
                if (tf != null) mailScrollRect = tf.GetComponent<ScrollRect>();
            }

            if (mailContent == null && mailScrollRect != null)
            {
                mailContent = mailScrollRect.content;
            }

            if (mailContent == null)
            {
                var tf = transform.Find("ModalPanel/MailScrollView/Content");
                if (tf != null) mailContent = tf as RectTransform;
            }

            if (emptyMessageText == null)
            {
                var tf = transform.Find("ModalPanel/MailScrollView/Content/EmptyMessageText");
                if (tf != null) emptyMessageText = tf.GetComponent<TextMeshProUGUI>();
            }

            BindButtons();
        }
    }
}
