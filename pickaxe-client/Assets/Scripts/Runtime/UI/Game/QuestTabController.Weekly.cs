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
    public partial class QuestTabController
    {
        [Header("Weekly Quest UI References")]
        [SerializeField] private TextMeshProUGUI weeklyTitleText;
        [SerializeField] private Transform weeklyMissionListContainer;
        [SerializeField] private GameObject weeklyMissionCardPrefab;
        [SerializeField] private Slider weeklyMilestoneSlider;
        [SerializeField] private RectTransform weeklyMilestoneFillArea;
        [SerializeField] private Transform weeklyMilestoneRewardContainer;
        [SerializeField] private WeeklyRewardModalController weeklyRewardModal;
        [SerializeField] private List<WeeklyMilestoneRewardView> weeklyMilestoneRewardViews = new List<WeeklyMilestoneRewardView>();

        private WeeklyMissionMetaResolver weeklyMissionMetaResolver;
        private readonly List<GameObject> weeklyMissionCardInstances = new List<GameObject>();
        private bool weeklyRequested;
        private bool weeklyMessageSubscribed;

        private void EnsureWeeklyMeta()
        {
            if (weeklyMissionMetaResolver == null)
            {
                weeklyMissionMetaResolver = new WeeklyMissionMetaResolver();
            }
            else if (MetaRepository.Loaded && !weeklyMissionMetaResolver.HasData)
            {
                weeklyMissionMetaResolver.Reload();
            }
        }

        private void EnsureWeeklyReferences()
        {
            var root = weeklyTabRoot != null ? weeklyTabRoot.transform : transform;

            if (weeklyTitleText == null)
            {
                var titleTf = FindChildRecursive(root, "WeeklyTitleText");
                if (titleTf != null) weeklyTitleText = titleTf.GetComponent<TextMeshProUGUI>();
            }

            if (weeklyMissionListContainer == null)
            {
                var container = FindChildRecursive(root, "WeeklyMissionListContainer");
                if (container != null) weeklyMissionListContainer = container;
            }

            if (weeklyMissionCardPrefab == null)
            {
                var prefabTf = FindChildRecursive(root, "WeeklyMissionCardPrefab");
                if (prefabTf != null) weeklyMissionCardPrefab = prefabTf.gameObject;
            }

            if (weeklyMilestoneSlider == null)
            {
                var sliderTf = FindChildRecursive(root, "WeeklyMilestoneSlider");
                if (sliderTf != null) weeklyMilestoneSlider = sliderTf.GetComponent<Slider>();
            }

            if (weeklyMilestoneFillArea == null && weeklyMilestoneSlider != null)
            {
                if (weeklyMilestoneSlider.fillRect != null && weeklyMilestoneSlider.fillRect.parent is RectTransform fillParent)
                {
                    weeklyMilestoneFillArea = fillParent;
                }
                else
                {
                    var fillAreaTf = FindChildRecursive(weeklyMilestoneSlider.transform, "Fill Area");
                    if (fillAreaTf == null)
                    {
                        fillAreaTf = FindChildRecursive(weeklyMilestoneSlider.transform, "FillArea");
                    }
                    if (fillAreaTf != null)
                    {
                        weeklyMilestoneFillArea = fillAreaTf.GetComponent<RectTransform>();
                    }
                }
            }

            if (weeklyMilestoneRewardContainer == null)
            {
                var rewardTf = FindChildRecursive(root, "WeeklyMilestoneRewardContainer");
                if (rewardTf == null)
                {
                    rewardTf = FindChildRecursive(root, "WeeklyMilestoneRewards");
                }
                if (rewardTf != null) weeklyMilestoneRewardContainer = rewardTf;
            }

            if ((weeklyMilestoneRewardViews == null || weeklyMilestoneRewardViews.Count == 0)
                && weeklyMilestoneRewardContainer != null)
            {
                weeklyMilestoneRewardViews = new List<WeeklyMilestoneRewardView>(
                    weeklyMilestoneRewardContainer.GetComponentsInChildren<WeeklyMilestoneRewardView>(true));
            }

            AutoBindWeeklyRewardModal();
        }

        private void AutoBindWeeklyRewardModal()
        {
            if (weeklyRewardModal != null) return;

            var modalObj = GameObject.Find("WeeklyRewardModal");
            if (modalObj != null)
            {
                weeklyRewardModal = modalObj.GetComponent<WeeklyRewardModalController>();
                return;
            }

            var prefab = Resources.Load<GameObject>("UI/WeeklyRewardModal");
            if (prefab == null) return;

            var instance = Instantiate(prefab, transform.root);
            instance.name = "WeeklyRewardModal";
            instance.SetActive(false);
            weeklyRewardModal = instance.GetComponent<WeeklyRewardModalController>();
        }

        private void SubscribeWeeklyMessageHandler()
        {
            if (weeklyMessageSubscribed) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            messageHandler.OnWeeklyMilestoneClaimResult += HandleWeeklyMilestoneClaimResult;
            weeklyMessageSubscribed = true;
        }

        private void UnsubscribeWeeklyMessageHandler()
        {
            if (!weeklyMessageSubscribed || messageHandler == null) return;
            messageHandler.OnWeeklyMilestoneClaimResult -= HandleWeeklyMilestoneClaimResult;
            weeklyMessageSubscribed = false;
        }

        private void HandleWeeklyMissionsChanged()
        {
            UpdateWeeklyTitle();
            UpdateWeeklyMissionList();
            UpdateWeeklyMilestones();
        }

        private void HandleWeeklyMilestonesChanged()
        {
            UpdateWeeklyMilestones();
        }

        private void RequestWeeklyMissionsIfNeeded()
        {
            if (questState == null)
            {
                questState = QuestStateCache.Instance;
            }

            if (questState != null && questState.HasWeeklyMissions)
            {
                weeklyRequested = true;
                return;
            }

            if (weeklyRequested) return;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestWeeklyMissions();
            weeklyRequested = true;
        }

        private void UpdateWeeklyTitle()
        {
            if (weeklyTitleText == null) return;

            uint claimed = questState != null ? questState.WeeklyClaimedCount : 0;
            uint maxCount = GetWeeklyMilestoneMaxCount();
            string timer = FormatResetTimer(questState != null ? questState.WeeklyResetTimestampMs : 0);

            var text = $"주간 미션 (보상 {claimed:N0}/{maxCount:N0})";
            if (!string.IsNullOrEmpty(timer))
            {
                text += $" | 리셋 {timer}";
            }
            weeklyTitleText.text = text;
        }

        private uint GetWeeklyMilestoneMaxCount()
        {
            if (weeklyMissionMetaResolver != null && weeklyMissionMetaResolver.Milestones.Count > 0)
            {
                return weeklyMissionMetaResolver.Milestones[^1].Completed;
            }
            return 30;
        }

        private void UpdateWeeklyMissionList()
        {
            if (weeklyTabRoot != null && !weeklyTabRoot.activeSelf && currentSubTabIndex != SubTabWeeklyIndex)
            {
                return;
            }

            EnsureWeeklyReferences();
            EnsureWeeklyMeta();

            if (weeklyMissionListContainer == null) return;

            ClearWeeklyMissionCards();

            var missions = questState != null ? questState.WeeklyMissions : null;
            if (missions == null || missions.Count == 0) return;

            var ordered = new List<WeeklyMissionEntry>(missions);
            ordered.Sort((a, b) =>
            {
                bool aClaimable = IsWeeklyMissionClaimable(a);
                bool bClaimable = IsWeeklyMissionClaimable(b);
                int claimCompare = bClaimable.CompareTo(aClaimable);
                if (claimCompare != 0) return claimCompare;
                return a.MissionId.CompareTo(b.MissionId);
            });

            foreach (var mission in ordered)
            {
                if (mission == null) continue;
                if (IsWeeklyMissionClaimed(mission.Status)) continue;

                var display = BuildWeeklyMissionDisplay(mission);
                CreateWeeklyMissionCard(display);
            }
        }

        private bool IsWeeklyMissionClaimed(string status)
        {
            return !string.IsNullOrEmpty(status)
                   && status.Trim().Equals("claimed", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsWeeklyMissionClaimable(WeeklyMissionEntry entry)
        {
            if (entry == null) return false;
            return GetStatusState(entry.Status) == QuestMissionItemView.MissionStatusState.Completed;
        }

        private void ClearWeeklyMissionCards()
        {
            for (int i = 0; i < weeklyMissionCardInstances.Count; i++)
            {
                if (weeklyMissionCardInstances[i] != null)
                {
                    Destroy(weeklyMissionCardInstances[i]);
                }
            }
            weeklyMissionCardInstances.Clear();
        }

        private WeeklyMissionDisplayData BuildWeeklyMissionDisplay(WeeklyMissionEntry entry)
        {
            WeeklyMissionMeta meta = null;
            if (weeklyMissionMetaResolver != null)
            {
                weeklyMissionMetaResolver.TryGetMission(entry.MissionId, out meta);
            }

            string type = !string.IsNullOrEmpty(entry.MissionType) ? entry.MissionType : meta?.Type ?? string.Empty;
            uint target = entry.TargetValue > 0 ? entry.TargetValue : meta?.Target ?? 0;
            uint rewardCrystal = entry.RewardCrystal > 0 ? entry.RewardCrystal : meta?.RewardCrystal ?? 0;

            string title = !string.IsNullOrEmpty(entry.Title) ? entry.Title : meta?.Title ?? string.Empty;
            string description = !string.IsNullOrEmpty(entry.Description) ? entry.Description : meta?.Description;
            if (string.IsNullOrEmpty(description))
            {
                description = BuildDescriptionFromType(type, target, null);
            }

            string progress = FormatWeeklyProgress(type, entry.CurrentValue, target);
            if (string.IsNullOrEmpty(description))
            {
                description = progress;
            }
            var statusState = GetStatusState(entry.Status);
            bool canClaim = statusState == QuestMissionItemView.MissionStatusState.Completed;

            return new WeeklyMissionDisplayData
            {
                MissionId = entry.MissionId,
                Title = title,
                Description = description,
                Progress = progress,
                RewardCrystal = rewardCrystal,
                Status = GetStatusLabel(entry.Status),
                StatusState = statusState,
                CanClaim = canClaim
            };
        }

        private string FormatWeeklyProgress(string type, uint currentValue, uint targetValue)
        {
            if (!string.IsNullOrEmpty(type) && type.Equals("play_time", StringComparison.OrdinalIgnoreCase))
            {
                int currentMinutes = SecondsToRoundedMinutes(currentValue);
                if (targetValue > 0)
                {
                    int targetMinutes = SecondsToRoundedMinutes(targetValue);
                    return $"{currentMinutes:N0}/{targetMinutes:N0}";
                }
                return $"{currentMinutes:N0}";
            }

            return targetValue > 0
                ? $"{currentValue:N0}/{targetValue:N0}"
                : $"{currentValue:N0}";
        }

        private void CreateWeeklyMissionCard(WeeklyMissionDisplayData display)
        {
            if (weeklyMissionListContainer == null) return;

            GameObject instance;
            if (weeklyMissionCardPrefab != null)
            {
                instance = Instantiate(weeklyMissionCardPrefab, weeklyMissionListContainer);
            }
            else
            {
                instance = new GameObject($"WeeklyMission_{display.MissionId}");
                instance.transform.SetParent(weeklyMissionListContainer, false);
            }

            var view = instance.GetComponentInChildren<WeeklyMissionCardView>(true);
            if (view != null)
            {
                uint missionId = display.MissionId;
                view.Apply(
                    display.Title,
                    display.Description,
                    display.Progress,
                    display.RewardCrystal,
                    display.Status,
                    display.StatusState,
                    display.CanClaim,
                    () => OnWeeklyMissionClaimClicked(missionId));
            }

            weeklyMissionCardInstances.Add(instance);
        }

        private void OnWeeklyMissionClaimClicked(uint missionId)
        {
            if (missionId == 0) return;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestWeeklyMissionClaim(missionId);
        }

        private void UpdateWeeklyMilestones()
        {
            if (weeklyTabRoot != null && !weeklyTabRoot.activeSelf && currentSubTabIndex != SubTabWeeklyIndex)
            {
                return;
            }

            EnsureWeeklyReferences();
            EnsureWeeklyMeta();
            UpdateWeeklyTitle();
            UpdateWeeklyMilestoneSlider();
            UpdateWeeklyMilestoneRewards();
            UpdateWeeklyMilestoneRewardPositions();
        }

        private void UpdateWeeklyMilestoneSlider()
        {
            if (weeklyMilestoneSlider == null) return;

            float maxValue = 30f;
            weeklyMilestoneSlider.minValue = 0f;
            weeklyMilestoneSlider.maxValue = maxValue;

            float current = questState != null ? questState.WeeklyClaimedCount : 0f;
            weeklyMilestoneSlider.value = Mathf.Clamp(current, 0f, maxValue);
        }

        private void UpdateWeeklyMilestoneRewards()
        {
            if (weeklyMilestoneRewardViews == null || weeklyMilestoneRewardViews.Count == 0) return;

            bool hasState = questState != null && questState.HasWeeklyMilestoneState;
            uint claimedCount = questState != null ? questState.WeeklyClaimedCount : 0;

            for (int i = 0; i < weeklyMilestoneRewardViews.Count; i++)
            {
                var view = weeklyMilestoneRewardViews[i];
                if (view == null) continue;

                uint milestoneCount = view.MilestoneCount;
                uint rewardCrystal = GetWeeklyMilestoneRewardCrystal(milestoneCount);
                bool claimed = hasState && questState.IsWeeklyMilestoneClaimed(milestoneCount);
                bool canClaim = hasState && claimedCount >= milestoneCount && !claimed;

                view.Apply(rewardCrystal, canClaim, claimed, OnWeeklyMilestoneClaimClicked);
            }
        }

        private void UpdateWeeklyMilestoneRewardPositions()
        {
            if (weeklyMilestoneRewardViews == null || weeklyMilestoneRewardViews.Count == 0) return;
            if (weeklyMilestoneRewardContainer == null) return;

            if (weeklyMilestoneFillArea == null)
            {
                EnsureWeeklyReferences();
            }
            if (weeklyMilestoneFillArea == null) return;

            float minValue = weeklyMilestoneSlider != null ? weeklyMilestoneSlider.minValue : 0f;
            float maxValue = weeklyMilestoneSlider != null ? weeklyMilestoneSlider.maxValue : 0f;
            if (Mathf.Abs(maxValue - minValue) <= Mathf.Epsilon) return;

            var rect = weeklyMilestoneFillArea.rect;

            for (int i = 0; i < weeklyMilestoneRewardViews.Count; i++)
            {
                var view = weeklyMilestoneRewardViews[i];
                if (view == null) continue;

                var rewardRect = view.GetComponent<RectTransform>();
                if (rewardRect == null) continue;

                float t = Mathf.InverseLerp(minValue, maxValue, view.MilestoneCount);
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                Vector3 worldPos = weeklyMilestoneFillArea.TransformPoint(new Vector3(x, rect.center.y, 0f));
                Vector3 localPos = weeklyMilestoneRewardContainer.InverseTransformPoint(worldPos);

                var rewardLocal = rewardRect.localPosition;
                float centerOffset = (0.5f - rewardRect.pivot.x) * rewardRect.rect.width * rewardRect.localScale.x;
                rewardLocal.x = localPos.x - centerOffset;
                rewardRect.localPosition = rewardLocal;
            }
        }

        private uint GetWeeklyMilestoneRewardCrystal(uint milestoneCount)
        {
            if (weeklyMissionMetaResolver == null) return 0;
            foreach (var milestone in weeklyMissionMetaResolver.Milestones)
            {
                if (milestone.Completed == milestoneCount)
                {
                    return milestone.RewardCrystal;
                }
            }
            return 0;
        }

        private void OnWeeklyMilestoneClaimClicked(uint milestoneCount)
        {
            if (milestoneCount == 0) return;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestWeeklyMilestoneClaim(milestoneCount);
        }

        private void HandleWeeklyMilestoneClaimResult(WeeklyMilestoneClaimResult result)
        {
            if (result == null || !result.Success) return;
            ShowRewardStove(result.RewardCrystal, result.RewardGold);
        }

        private void ShowWeeklyRewardModal(uint rewardCrystal)
        {
            AutoBindWeeklyRewardModal();
            if (weeklyRewardModal == null) return;
            weeklyRewardModal.Show(rewardCrystal);
        }

        private struct WeeklyMissionDisplayData
        {
            public uint MissionId;
            public string Title;
            public string Description;
            public string Progress;
            public uint RewardCrystal;
            public string Status;
            public QuestMissionItemView.MissionStatusState StatusState;
            public bool CanClaim;
        }
    }
}
