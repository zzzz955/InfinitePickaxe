using System;
using System.Collections.Generic;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    public sealed class MailStateCache
    {
        private static readonly Lazy<MailStateCache> Lazy = new Lazy<MailStateCache>(() => new MailStateCache());
        public static MailStateCache Instance => Lazy.Value;

        public event Action OnMailListChanged;
        public event Action OnMailDetailChanged;
        public event Action OnMailCountsChanged;

        private readonly Dictionary<string, MailEntry> entriesById = new Dictionary<string, MailEntry>(StringComparer.Ordinal);
        private readonly List<string> currentMailIds = new List<string>();

        public IReadOnlyList<string> CurrentMailIds => currentMailIds;
        public IReadOnlyDictionary<string, MailEntry> Entries => entriesById;
        public uint UnreadCount { get; private set; }
        public uint UnclaimedCount { get; private set; }
        public bool HasList { get; private set; }
        public bool LastHasNext { get; private set; }
        public ulong LastNextCursorCreatedAtMs { get; private set; }
        public string LastNextCursorMailId { get; private set; } = string.Empty;

        private MailStateCache() { }

        public void ResetAll()
        {
            entriesById.Clear();
            currentMailIds.Clear();
            UnreadCount = 0;
            UnclaimedCount = 0;
            HasList = false;
            LastHasNext = false;
            LastNextCursorCreatedAtMs = 0;
            LastNextCursorMailId = string.Empty;
            OnMailListChanged?.Invoke();
            OnMailDetailChanged?.Invoke();
            OnMailCountsChanged?.Invoke();
        }

        public void UpdateFromListResponse(MailListResponse response)
        {
            if (response == null) return;

            currentMailIds.Clear();
            foreach (var summary in response.Mails)
            {
                if (summary == null || string.IsNullOrEmpty(summary.MailId)) continue;
                var entry = GetOrCreate(summary.MailId);
                entry.ApplySummary(summary);
                currentMailIds.Add(entry.MailId);
            }

            UnreadCount = response.UnreadCount;
            UnclaimedCount = response.UnclaimedCount;
            LastHasNext = response.HasNext;
            LastNextCursorCreatedAtMs = response.NextCursorCreatedAtMs;
            LastNextCursorMailId = response.NextCursorMailId ?? string.Empty;
            HasList = true;

            OnMailListChanged?.Invoke();
            OnMailCountsChanged?.Invoke();
        }

        public void UpdateFromDetailResponse(MailDetailResponse response)
        {
            if (response == null || response.Mail == null || !response.Success) return;

            var mail = response.Mail;
            if (string.IsNullOrEmpty(mail.MailId)) return;

            var entry = GetOrCreate(mail.MailId);
            bool wasRead = entry.IsRead;
            bool wasClaimed = entry.IsClaimed;

            entry.ApplyDetail(mail);

            bool countsChanged = false;
            if (!wasRead && entry.IsRead && UnreadCount > 0)
            {
                UnreadCount -= 1;
                countsChanged = true;
            }
            if (!wasClaimed && entry.IsClaimed && UnclaimedCount > 0)
            {
                UnclaimedCount -= 1;
                countsChanged = true;
            }

            OnMailDetailChanged?.Invoke();
            if (countsChanged)
            {
                OnMailCountsChanged?.Invoke();
            }
        }

        public void ApplyClaimResult(MailClaimResult result)
        {
            if (result == null || !result.Success) return;
            if (string.IsNullOrEmpty(result.MailId)) return;

            var entry = GetOrCreate(result.MailId);
            bool wasRead = entry.IsRead;
            bool wasClaimed = entry.IsClaimed;

            entry.IsRead = true;
            entry.IsClaimed = true;

            if (result.Rewards != null && result.Rewards.Count > 0)
            {
                entry.Rewards.Clear();
                foreach (var reward in result.Rewards)
                {
                    entry.Rewards.Add(new MailRewardEntry
                    {
                        RewardType = reward.RewardType,
                        RewardKey = reward.RewardKey ?? string.Empty,
                        Amount = reward.Amount
                    });
                }
                entry.HasReward = true;
            }

            bool countsChanged = false;
            if (!wasRead && UnreadCount > 0)
            {
                UnreadCount -= 1;
                countsChanged = true;
            }
            if (!wasClaimed && UnclaimedCount > 0)
            {
                UnclaimedCount -= 1;
                countsChanged = true;
            }

            OnMailListChanged?.Invoke();
            OnMailDetailChanged?.Invoke();
            if (countsChanged)
            {
                OnMailCountsChanged?.Invoke();
            }
        }

        public void ApplyClaimAllResult(MailClaimAllResult result)
        {
            if (result == null || !result.Success) return;

            if (result.ClaimedCount > 0 && UnclaimedCount > 0)
            {
                UnclaimedCount = UnclaimedCount > result.ClaimedCount
                    ? UnclaimedCount - result.ClaimedCount
                    : 0;
                OnMailCountsChanged?.Invoke();
            }
        }

        public bool TryGetEntry(string mailId, out MailEntry entry)
        {
            return entriesById.TryGetValue(mailId, out entry);
        }

        private MailEntry GetOrCreate(string mailId)
        {
            if (!entriesById.TryGetValue(mailId, out var entry))
            {
                entry = new MailEntry(mailId);
                entriesById[mailId] = entry;
            }
            return entry;
        }
    }

    public sealed class MailEntry
    {
        public string MailId { get; }
        public string MailType { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public string TemplateArgsJson { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public ulong CreatedAtMs { get; set; }
        public ulong ExpiresAtMs { get; set; }
        public bool IsRead { get; set; }
        public bool IsClaimed { get; set; }
        public bool HasReward { get; set; }
        public bool HasDetail { get; set; }
        public List<MailRewardEntry> Rewards { get; } = new List<MailRewardEntry>();

        public MailEntry(string mailId)
        {
            MailId = mailId ?? string.Empty;
        }

        public void ApplySummary(MailSummary summary)
        {
            if (summary == null) return;

            MailType = summary.MailType ?? MailType;
            if (!string.IsNullOrEmpty(summary.Title))
            {
                Title = summary.Title;
            }

            CreatedAtMs = summary.CreatedAtMs;
            ExpiresAtMs = summary.ExpiresAtMs;
            IsRead = summary.IsRead;
            IsClaimed = summary.IsClaimed;
            HasReward = summary.HasReward;
        }

        public void ApplyDetail(MailDetail detail)
        {
            if (detail == null) return;

            MailType = detail.MailType ?? MailType;
            TemplateId = detail.TemplateId ?? TemplateId;
            TemplateArgsJson = detail.TemplateArgsJson ?? TemplateArgsJson;
            if (!string.IsNullOrEmpty(detail.Title))
            {
                Title = detail.Title;
            }
            if (!string.IsNullOrEmpty(detail.Body))
            {
                Body = detail.Body;
            }
            if (!string.IsNullOrEmpty(detail.Sender))
            {
                Sender = detail.Sender;
            }

            CreatedAtMs = detail.CreatedAtMs;
            ExpiresAtMs = detail.ExpiresAtMs;
            IsRead = detail.IsRead;
            IsClaimed = detail.IsClaimed;

            if (detail.Rewards != null)
            {
                Rewards.Clear();
                foreach (var reward in detail.Rewards)
                {
                    Rewards.Add(new MailRewardEntry
                    {
                        RewardType = reward.RewardType,
                        RewardKey = reward.RewardKey ?? string.Empty,
                        Amount = reward.Amount
                    });
                }
                HasReward = detail.Rewards.Count > 0 || HasReward;
            }

            HasDetail = true;
        }
    }

    public struct MailRewardEntry
    {
        public RewardType RewardType;
        public string RewardKey;
        public ulong Amount;
    }
}
