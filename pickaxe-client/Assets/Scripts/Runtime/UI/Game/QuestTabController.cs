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
    public class QuestTabController : BaseTabController
    {
        private const string MissionRerollAdType = "mission_reroll";

        [Header("Quest UI References")]
        [SerializeField] private TextMeshProUGUI questCountText;
        [SerializeField] private Transform questListContainer;
        [SerializeField] private GameObject questItemPrefab;
        [SerializeField] private Button refreshQuestButton;
        [SerializeField] private Button adRefreshButton;
        [SerializeField] private TextMeshProUGUI refreshCountText;
        [SerializeField] private TextMeshProUGUI allCompleteMessage;

        [Header("Milestone UI")]
        [SerializeField] private TextMeshProUGUI milestoneTitleText;
        [SerializeField] private TextMeshProUGUI milestone3Text;
        [SerializeField] private TextMeshProUGUI milestone5Text;
        [SerializeField] private TextMeshProUGUI milestone7Text;
        [SerializeField] private Button milestone3Button;
        [SerializeField] private Button milestone5Button;
        [SerializeField] private Button milestone7Button;
        [SerializeField] private Color milestoneLockedColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color milestoneClaimableColor = new Color(0.2f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color milestoneClaimedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private float milestoneButtonDisabledAlpha = 0.5f;

        [Header("Quest Data (Fallback)")]
        [SerializeField] private int completedCount = 0;
        [SerializeField] private int totalCount = 7;
        [SerializeField] private int freeRefreshCount = 2;
        [SerializeField] private int usedRefreshCount = 0;

        private MessageHandler messageHandler;
        private QuestStateCache questState;
        private DailyMissionMetaResolver missionMetaResolver;
        private MineralMetaResolver mineralMetaResolver;
        private TextMeshProUGUI adRefreshButtonText;
        private readonly List<GameObject> missionItemInstances = new List<GameObject>();
        private bool cacheSubscribed;
        private float nextTimerRefreshTime;

        protected override void Initialize()
        {
            base.Initialize();

            EnsureReferences();
            messageHandler = MessageHandler.Instance;
            questState = QuestStateCache.Instance;
            missionMetaResolver = new DailyMissionMetaResolver();
            mineralMetaResolver = new MineralMetaResolver();

            if (refreshQuestButton != null)
            {
                refreshQuestButton.onClick.AddListener(OnRefreshQuestClicked);
            }

            if (adRefreshButton != null)
            {
                adRefreshButton.onClick.AddListener(OnAdRefreshQuestClicked);
            }

            if (milestone3Button != null)
            {
                milestone3Button.onClick.AddListener(() => OnMilestoneClaimClicked(3));
            }

            if (milestone5Button != null)
            {
                milestone5Button.onClick.AddListener(() => OnMilestoneClaimClicked(5));
            }

            if (milestone7Button != null)
            {
                milestone7Button.onClick.AddListener(() => OnMilestoneClaimClicked(7));
            }

            RefreshData();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureReferences();
            SubscribeState();
            RefreshData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnsubscribeState();
        }

        private void Update()
        {
            if (!isActive) return;
            if (Time.unscaledTime < nextTimerRefreshTime) return;

            nextTimerRefreshTime = Time.unscaledTime + 1f;
            UpdateQuestCount();
            UpdateMilestones();
            UpdateRefreshButton();
        }

        public override void RefreshData()
        {
            EnsureMeta();
            UpdateQuestCount();
            UpdateMissionList();
            UpdateMilestones();
            UpdateRefreshButton();
        }

        private void EnsureMeta()
        {
            if (missionMetaResolver == null)
            {
                missionMetaResolver = new DailyMissionMetaResolver();
            }
            else if (MetaRepository.Loaded && !missionMetaResolver.HasData)
            {
                missionMetaResolver.Reload();
            }

            if (mineralMetaResolver == null)
            {
                mineralMetaResolver = new MineralMetaResolver();
            }
            else if (MetaRepository.Loaded && (mineralMetaResolver.All == null || mineralMetaResolver.All.Count == 0))
            {
                mineralMetaResolver.Reload();
            }
        }

        private void EnsureReferences()
        {
            if (questCountText == null)
            {
                questCountText = transform.Find("TitleArea/QuestCountText")?.GetComponent<TextMeshProUGUI>();
            }

            if (questListContainer == null)
            {
                questListContainer = transform.Find("QuestListContainer");
            }

            if (refreshQuestButton == null)
            {
                refreshQuestButton = transform.Find("RefreshArea/ButtonRow/RefreshButton")?.GetComponent<Button>();
            }

            if (adRefreshButton == null)
            {
                adRefreshButton = transform.Find("RefreshArea/ButtonRow/AdRefreshButton")?.GetComponent<Button>();
            }

            if (refreshCountText == null)
            {
                refreshCountText = transform.Find("RefreshArea/RefreshCountText")?.GetComponent<TextMeshProUGUI>();
            }

            if (allCompleteMessage == null && questListContainer != null)
            {
                allCompleteMessage = questListContainer.Find("AllCompleteMessage")?.GetComponent<TextMeshProUGUI>();
            }

            if (milestoneTitleText == null)
            {
                milestoneTitleText = transform.Find("MilestonePanel/MilestoneTitleText")?.GetComponent<TextMeshProUGUI>();
            }

            if (milestone3Text == null)
            {
                milestone3Text = transform.Find("MilestonePanel/Milestone3Row/Milestone3Text")?.GetComponent<TextMeshProUGUI>();
            }

            if (milestone5Text == null)
            {
                milestone5Text = transform.Find("MilestonePanel/Milestone5Row/Milestone5Text")?.GetComponent<TextMeshProUGUI>();
            }

            if (milestone7Text == null)
            {
                milestone7Text = transform.Find("MilestonePanel/Milestone7Row/Milestone7Text")?.GetComponent<TextMeshProUGUI>();
            }

            if (milestone3Button == null)
            {
                milestone3Button = transform.Find("MilestonePanel/Milestone3Row/Milestone3Button")?.GetComponent<Button>();
            }

            if (milestone5Button == null)
            {
                milestone5Button = transform.Find("MilestonePanel/Milestone5Row/Milestone5Button")?.GetComponent<Button>();
            }

            if (milestone7Button == null)
            {
                milestone7Button = transform.Find("MilestonePanel/Milestone7Row/Milestone7Button")?.GetComponent<Button>();
            }

            if (adRefreshButtonText == null && adRefreshButton != null)
            {
                adRefreshButtonText = adRefreshButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        private void SubscribeState()
        {
            if (cacheSubscribed) return;

            questState = QuestStateCache.Instance;
            if (questState != null)
            {
                questState.OnMissionsChanged += HandleMissionsChanged;
                questState.OnMilestoneChanged += HandleMilestonesChanged;
                cacheSubscribed = true;
            }
        }

        private void UnsubscribeState()
        {
            if (!cacheSubscribed || questState == null) return;

            questState.OnMissionsChanged -= HandleMissionsChanged;
            questState.OnMilestoneChanged -= HandleMilestonesChanged;
            cacheSubscribed = false;
        }

        private void HandleMissionsChanged()
        {
            UpdateQuestCount();
            UpdateMissionList();
            UpdateRefreshButton();
        }

        private void HandleMilestonesChanged()
        {
            UpdateMilestones();
        }

        private void UpdateQuestCount()
        {
            if (questCountText == null) return;

            int completed = GetCompletedCount();
            int total = GetTotalMissionCount();
            string timer = FormatResetTimer(questState != null ? questState.ResetTimestampMs : 0);

            var text = $"일일 미션 ({completed}/{total} 완료)";
            if (!string.IsNullOrEmpty(timer))
            {
                text += $" | 리셋 {timer}";
            }

            questCountText.text = text;
        }

        private void UpdateMissionList()
        {
            if (questListContainer == null) return;

            ClearMissionItems();

            var missions = questState != null ? questState.Missions : null;
            bool hasMissions = missions != null && missions.Count > 0;

            if (allCompleteMessage != null)
            {
                int completed = GetCompletedCount();
                int total = GetTotalMissionCount();
                bool showComplete = questState != null
                                    && questState.HasDailyMissions
                                    && total > 0
                                    && completed >= total;
                allCompleteMessage.gameObject.SetActive(showComplete);
            }

            if (!hasMissions)
            {
                return;
            }

            foreach (var mission in missions)
            {
                if (mission == null) continue;
                var display = BuildMissionDisplay(mission);
                CreateMissionItem(display);
            }
        }

        private void ClearMissionItems()
        {
            for (int i = 0; i < missionItemInstances.Count; i++)
            {
                if (missionItemInstances[i] != null)
                {
                    Destroy(missionItemInstances[i]);
                }
            }
            missionItemInstances.Clear();
        }

        private void CreateMissionItem(MissionDisplayData display)
        {
            if (questItemPrefab != null)
            {
                var instance = Instantiate(questItemPrefab, questListContainer);
                var view = instance.GetComponentInChildren<QuestMissionItemView>(true);
                if (view != null)
                {
                    uint slotNo = display.SlotNo;
                    view.Apply(
                        display.TypeLabel,
                        display.DifficultyLabel,
                        display.Description,
                        display.Reward,
                        display.Status,
                        display.StatusState,
                        display.CanClaim,
                        () => OnMissionClaimClicked(slotNo));
                }
                else
                {
                    var text = instance.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        text.text = display.ToFallbackText();
                    }
                }
                missionItemInstances.Add(instance);
                return;
            }

            var fallback = new GameObject($"MissionItem_{display.SlotNo}");
            fallback.transform.SetParent(questListContainer, false);
            var rect = fallback.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(660, 120);

            var textMesh = fallback.AddComponent<TextMeshProUGUI>();
            textMesh.fontSize = 32;
            textMesh.alignment = TextAlignmentOptions.Left;
            textMesh.enableWordWrapping = true;
            textMesh.text = display.ToFallbackText();
            if (questCountText != null)
            {
                textMesh.font = questCountText.font;
            }

            missionItemInstances.Add(fallback);
        }

        private void UpdateMilestones()
        {
            string milestoneTimer = FormatResetTimer(questState != null ? questState.MilestoneResetTimestampMs : 0);
            if (milestoneTitleText != null)
            {
                var title = "마일스톤 보상";
                if (!string.IsNullOrEmpty(milestoneTimer))
                {
                    title += $" | 리셋 {milestoneTimer}";
                }
                milestoneTitleText.text = title;
            }

            UpdateMilestoneRow(3, milestone3Text, milestone3Button);
            UpdateMilestoneRow(5, milestone5Text, milestone5Button);
            UpdateMilestoneRow(7, milestone7Text, milestone7Button);
        }

        private void UpdateMilestoneRow(uint milestoneCount, TextMeshProUGUI text, Button button)
        {
            bool hasState = questState != null && questState.HasMilestoneState;
            bool claimed = hasState && questState.IsMilestoneClaimed(milestoneCount);
            bool canClaim = hasState
                            && questState.MilestoneCompletedCount >= milestoneCount
                            && !claimed;

            string stateLabel;
            Color stateColor;
            if (claimed)
            {
                stateLabel = "수령 완료";
                stateColor = milestoneClaimedColor;
            }
            else if (canClaim)
            {
                stateLabel = "획득 가능";
                stateColor = milestoneClaimableColor;
            }
            else
            {
                stateLabel = "획득 불가";
                stateColor = milestoneLockedColor;
            }

            if (text != null)
            {
                text.text = $"일일 미션 {milestoneCount}개 완료 : {stateLabel}";
                text.color = stateColor;
            }

            if (button != null)
            {
                button.interactable = canClaim;
                ApplyMilestoneButtonVisual(button, canClaim);
            }
        }

        private void UpdateRefreshButton()
        {
            int freeRemaining;
            int freeLimit;
            int adRemaining;
            int adLimit;

            GetRerollCounts(out freeRemaining, out freeLimit, out adRemaining, out adLimit);

            if (refreshCountText != null)
            {
                refreshCountText.text = $"미션 재설정 (무료 {freeRemaining}/{freeLimit})";
            }

            if (refreshQuestButton != null)
            {
                refreshQuestButton.interactable = freeRemaining > 0;
            }

            if (adRefreshButton != null)
            {
                adRefreshButton.interactable = adRemaining > 0;
            }

            if (adRefreshButtonText != null)
            {
                adRefreshButtonText.text = adLimit > 0
                    ? $"광고로 재설정 ({adRemaining}/{adLimit})"
                    : "광고로 재설정";
            }
        }

        private void GetRerollCounts(out int freeRemaining, out int freeLimit, out int adRemaining, out int adLimit)
        {
            freeRemaining = 0;
            freeLimit = freeRefreshCount;
            adRemaining = 0;
            adLimit = 0;

            if (questState != null && questState.HasDailyMissions && questState.RerollsTotalLimit > 0)
            {
                freeLimit = (int)questState.RerollsFree;
                int totalLimit = (int)questState.RerollsTotalLimit;
                int usedTotal = (int)questState.RerollCount;
                int adUsed = 0;

                if (questState.TryGetAdCounter(MissionRerollAdType, out var counter))
                {
                    adUsed = (int)counter.AdCount;
                    if (counter.DailyLimit > 0)
                    {
                        adLimit = (int)counter.DailyLimit;
                    }
                }

                if (adLimit == 0)
                {
                    adLimit = Math.Max(0, totalLimit - freeLimit);
                }

                int freeUsed = Math.Max(0, usedTotal - adUsed);
                freeRemaining = Math.Max(0, freeLimit - freeUsed);
                adRemaining = Math.Max(0, adLimit - adUsed);
                return;
            }

            freeRemaining = Math.Max(0, freeRefreshCount - usedRefreshCount);
        }

        private void ApplyMilestoneButtonVisual(Button button, bool canClaim)
        {
            if (button == null) return;
            var graphic = button.targetGraphic;
            if (graphic == null) return;

            var color = graphic.color;
            color.a = canClaim ? 1f : milestoneButtonDisabledAlpha;
            graphic.color = color;
        }

        private void OnRefreshQuestClicked()
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestMissionReroll();
        }

        private void OnAdRefreshQuestClicked()
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.NotifyAdWatchComplete("mission_reroll");
            messageHandler?.RequestMissionReroll();
        }

        private void OnMilestoneClaimClicked(uint milestoneCount)
        {
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestMilestoneClaim(milestoneCount);
        }

        private void OnMissionClaimClicked(uint slotNo)
        {
            if (slotNo == 0) return;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestMissionComplete(slotNo);
        }

        public void SetQuestProgress(int completed, int total)
        {
            completedCount = completed;
            totalCount = total;
            RefreshData();
        }

        private int GetCompletedCount()
        {
            if (questState != null && questState.HasDailyMissions)
            {
                return (int)questState.CompletedCount;
            }
            return completedCount;
        }

        private int GetTotalMissionCount()
        {
            if (missionMetaResolver != null && missionMetaResolver.MaxDailyAssign > 0)
            {
                return (int)missionMetaResolver.MaxDailyAssign;
            }

            if (questState != null && questState.Missions != null && questState.Missions.Count > 0)
            {
                return questState.Missions.Count;
            }

            return totalCount;
        }

        private uint GetMilestoneBonusHours(uint milestoneCount)
        {
            if (missionMetaResolver == null) return 0;
            foreach (var milestone in missionMetaResolver.Milestones)
            {
                if (milestone.Completed == milestoneCount)
                {
                    return milestone.BonusHours;
                }
            }
            return 0;
        }

        private MissionDisplayData BuildMissionDisplay(MissionEntry entry)
        {
            DailyMissionMeta meta = null;
            if (missionMetaResolver != null)
            {
                missionMetaResolver.TryGetMission(entry.MissionId, out meta);
            }

            string type = !string.IsNullOrEmpty(entry.MissionType) ? entry.MissionType : meta?.Type ?? string.Empty;
            string difficulty = !string.IsNullOrEmpty(entry.Difficulty) ? entry.Difficulty : meta?.Difficulty ?? string.Empty;
            uint target = entry.TargetValue > 0 ? entry.TargetValue : meta?.Target ?? 0;
            uint reward = entry.RewardCrystal > 0 ? entry.RewardCrystal : meta?.RewardCrystal ?? 0;
            uint? mineralId = entry.MineralId.HasValue ? entry.MineralId : meta?.MineralId;

            string description = !string.IsNullOrEmpty(entry.Description) ? entry.Description : meta?.Description;
            if (string.IsNullOrEmpty(description))
            {
                description = BuildDescriptionFromType(type, target, mineralId);
            }

            string typeLabel = GetMissionTypeLabel(type, mineralId);
            string difficultyLabel = GetDifficultyLabel(difficulty);
            string title = !string.IsNullOrEmpty(difficultyLabel) ? $"{typeLabel} [{difficultyLabel}]" : typeLabel;
            string progress = FormatProgress(type, entry.CurrentValue, target);
            string descriptionWithProgress = CombineDescriptionProgress(description, progress);
            string rewardText = reward > 0 ? reward.ToString("N0") : "0";
            var statusState = GetStatusState(entry.Status);
            string status = GetStatusLabel(entry.Status);
            bool canClaim = statusState == QuestMissionItemView.MissionStatusState.Completed && entry.SlotNo > 0;

            return new MissionDisplayData
            {
                SlotNo = entry.SlotNo,
                Title = title,
                TypeLabel = typeLabel,
                DifficultyLabel = difficultyLabel,
                Description = descriptionWithProgress,
                Reward = rewardText,
                Status = status,
                StatusState = statusState,
                CanClaim = canClaim
            };
        }

        private string BuildTitle(string type, string difficulty, uint? mineralId)
        {
            string typeLabel = GetMissionTypeLabel(type, mineralId);
            string difficultyLabel = GetDifficultyLabel(difficulty);
            if (!string.IsNullOrEmpty(difficultyLabel))
            {
                return $"{typeLabel} [{difficultyLabel}]";
            }
            return typeLabel;
        }

        private string FormatProgress(string type, uint currentValue, uint targetValue)
        {
            if (!string.IsNullOrEmpty(type) && type.Equals("play_time", StringComparison.OrdinalIgnoreCase))
            {
                int currentMinutes = SecondsToRoundedMinutes(currentValue);
                if (targetValue > 0)
                {
                    int targetMinutes = SecondsToRoundedMinutes(targetValue);
                    return $"{currentMinutes}/{targetMinutes}";
                }
                return $"{currentMinutes}";
            }

            return targetValue > 0 ? $"{currentValue}/{targetValue}" : $"{currentValue}";
        }

        private int SecondsToRoundedMinutes(uint seconds)
        {
            if (seconds == 0) return 0;
            return (int)Math.Ceiling(seconds / 60.0);
        }

        private string CombineDescriptionProgress(string description, string progress)
        {
            if (string.IsNullOrEmpty(progress)) return description ?? string.Empty;
            if (string.IsNullOrEmpty(description)) return progress;
            return $"{description}({progress})";
        }

        private string BuildDescriptionFromType(string type, uint target, uint? mineralId)
        {
            string mineralName = GetMineralName(mineralId);
            switch (type)
            {
                case "mine_any":
                    return $"아무 광물 {target}개 채굴";
                case "play_time":
                    return $"플레이 시간 {FormatDurationSeconds(target)}";
                case "upgrade_success":
                    return $"강화 {target}회 성공";
                case "upgrade_try":
                    return $"강화 {target}회 시도";
                case "gold":
                    return $"골드 {target:N0} 획득";
                case "mine_mineral":
                    if (string.IsNullOrEmpty(mineralName))
                    {
                        mineralName = "광물";
                    }
                    return $"{mineralName} {target}개 채굴";
                default:
                    return string.Empty;
            }
        }

        private string GetMissionTypeLabel(string type, uint? mineralId)
        {
            string mineralName = GetMineralName(mineralId);
            switch (type)
            {
                case "mine_any":
                    return "채굴";
                case "play_time":
                    return "플레이";
                case "upgrade_success":
                    return "강화 성공";
                case "upgrade_try":
                    return "강화 시도";
                case "gold":
                    return "골드";
                case "mine_mineral":
                    return string.IsNullOrEmpty(mineralName) ? "광물 채굴" : $"{mineralName} 채굴";
                default:
                    return "미션";
            }
        }

        private string GetDifficultyLabel(string difficulty)
        {
            if (string.IsNullOrEmpty(difficulty)) return string.Empty;
            switch (difficulty.Trim().ToLowerInvariant())
            {
                case "easy":
                    return "쉬움";
                case "medium":
                    return "보통";
                case "hard":
                    return "어려움";
                default:
                    return difficulty;
            }
        }

        private string GetStatusLabel(string status)
        {
            if (string.IsNullOrEmpty(status)) return "-";
            switch (GetStatusState(status))
            {
                case QuestMissionItemView.MissionStatusState.Active:
                    return "진행 중";
                case QuestMissionItemView.MissionStatusState.Completed:
                    return "완료";
                case QuestMissionItemView.MissionStatusState.Claimed:
                    return "수령 완료";
                default:
                    return status;
            }
        }

        private QuestMissionItemView.MissionStatusState GetStatusState(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return QuestMissionItemView.MissionStatusState.Unknown;
            switch (status.Trim().ToLowerInvariant())
            {
                case "active":
                    return QuestMissionItemView.MissionStatusState.Active;
                case "completed":
                    return QuestMissionItemView.MissionStatusState.Completed;
                case "claimed":
                    return QuestMissionItemView.MissionStatusState.Claimed;
                default:
                    return QuestMissionItemView.MissionStatusState.Unknown;
            }
        }

        private string GetMineralName(uint? mineralId)
        {
            if (!mineralId.HasValue) return string.Empty;
            if (mineralMetaResolver != null)
            {
                return mineralMetaResolver.GetNameOrDefault(mineralId.Value, $"광물 {mineralId.Value}");
            }
            return $"광물 {mineralId.Value}";
        }

        private string FormatDurationSeconds(uint seconds)
        {
            if (seconds == 0) return "0초";

            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}시간 {span.Minutes}분";
            }

            if (span.TotalMinutes >= 1)
            {
                return $"{span.Minutes}분 {span.Seconds}초";
            }

            return $"{span.Seconds}초";
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

        private struct MissionDisplayData
        {
            public uint SlotNo;
            public string Title;
            public string TypeLabel;
            public string DifficultyLabel;
            public string Description;
            public string Reward;
            public string Status;
            public QuestMissionItemView.MissionStatusState StatusState;
            public bool CanClaim;

            public string ToFallbackText()
            {
                return $"{Title}\n{Description}\n보상 {Reward} | {Status}";
            }
        }
    }
}
