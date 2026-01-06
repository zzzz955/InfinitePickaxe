using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Net;

namespace InfinitePickaxe.Client.UI.Game
{
    public partial class QuestTabController
    {
        [Header("Achievement UI References")]
        [SerializeField] private Transform achievementListContainer;
        [SerializeField] private GameObject achievementCardPrefab;

        private AchievementStateCache achievementState;
        private AchievementMetaResolver achievementMetaResolver;
        private readonly List<GameObject> achievementCardInstances = new List<GameObject>();
        private bool achievementSubscribed;
        private bool achievementRequested;

        private void EnsureAchievementMeta()
        {
            if (achievementMetaResolver == null)
            {
                achievementMetaResolver = new AchievementMetaResolver();
            }
            else if (MetaRepository.Loaded && !achievementMetaResolver.HasData)
            {
                achievementMetaResolver.Reload();
            }
        }

        private void EnsureAchievementReferences()
        {
            var root = achievementTabRoot != null ? achievementTabRoot.transform : transform;

            if (achievementListContainer == null)
            {
                var container = FindChildRecursive(root, "AchievementListContainer");
                if (container != null) achievementListContainer = container;
            }

            if (achievementCardPrefab == null)
            {
                var prefabTf = FindChildRecursive(root, "AchievementCardPrefab");
                if (prefabTf != null) achievementCardPrefab = prefabTf.gameObject;
            }
        }

        private void SubscribeAchievementState()
        {
            if (achievementSubscribed) return;

            achievementState = AchievementStateCache.Instance;
            if (achievementState != null)
            {
                achievementState.OnProgressChanged += HandleAchievementProgressChanged;
                achievementState.OnChainsChanged += HandleAchievementChainsChanged;
                achievementSubscribed = true;
            }
        }

        private void UnsubscribeAchievementState()
        {
            if (!achievementSubscribed || achievementState == null) return;

            achievementState.OnProgressChanged -= HandleAchievementProgressChanged;
            achievementState.OnChainsChanged -= HandleAchievementChainsChanged;
            achievementSubscribed = false;
        }

        private void HandleAchievementProgressChanged()
        {
            UpdateAchievementList();
        }

        private void HandleAchievementChainsChanged()
        {
            UpdateAchievementList();
        }

        private void RequestAchievementsIfNeeded()
        {
            if (achievementState == null)
            {
                achievementState = AchievementStateCache.Instance;
            }

            if (achievementState != null && achievementState.HasState)
            {
                achievementRequested = true;
                return;
            }

            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestAchievements();
            achievementRequested = true;
        }

        private void UpdateAchievementList()
        {
            if (achievementTabRoot != null && !achievementTabRoot.activeSelf && currentSubTabIndex != SubTabAchievementIndex)
            {
                return;
            }

            EnsureAchievementReferences();
            EnsureAchievementMeta();

            if (achievementListContainer == null) return;

            ClearAchievementCards();

            if (achievementMetaResolver == null || !achievementMetaResolver.HasData)
            {
                return;
            }

            var cardDataList = new List<AchievementCardData>();
            foreach (var pair in achievementMetaResolver.Chains)
            {
                if (pair.Key == 0 || pair.Value == null || pair.Value.Count == 0) continue;
                if (TryBuildAchievementCardData(pair.Key, pair.Value, out var data))
                {
                    cardDataList.Add(data);
                }
            }

            cardDataList.Sort((a, b) =>
            {
                int claimCompare = b.CanClaim.CompareTo(a.CanClaim);
                if (claimCompare != 0) return claimCompare;
                return a.ChainId.CompareTo(b.ChainId);
            });

            for (int i = 0; i < cardDataList.Count; i++)
            {
                CreateAchievementCard(cardDataList[i]);
            }
        }

        private bool TryBuildAchievementCardData(uint chainId, List<AchievementMeta> steps, out AchievementCardData data)
        {
            data = default;

            AchievementMeta currentStep = null;
            uint lastClaimedStep = achievementState != null ? achievementState.GetLastClaimedStep(chainId) : 0;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].StepIndex > lastClaimedStep)
                {
                    currentStep = steps[i];
                    break;
                }
            }

            bool chainCompleted = false;
            if (currentStep == null)
            {
                currentStep = steps[steps.Count - 1];
                chainCompleted = true;
            }

            ulong currentValue = achievementState != null ? achievementState.GetProgressOrDefault(currentStep.Type) : 0;
            ulong targetValue = currentStep.Target;
            bool canClaim = !chainCompleted && targetValue > 0 && currentValue >= targetValue;

            ulong displayValue = currentValue;
            if (targetValue > 0 && displayValue > targetValue)
            {
                displayValue = targetValue;
            }

            string progressLabel;
            if (currentStep.Type.Equals("play_time", StringComparison.OrdinalIgnoreCase))
            {
                string currentLabel = FormatPlayTime(displayValue);
                if (targetValue > 0)
                {
                    string targetLabel = FormatPlayTime(targetValue);
                    progressLabel = $"{currentLabel}/{targetLabel}";
                }
                else
                {
                    progressLabel = currentLabel;
                }
            }
            else
            {
                progressLabel = targetValue > 0
                    ? $"{displayValue:N0}/{targetValue:N0}"
                    : displayValue.ToString("N0");
            }

            data = new AchievementCardData
            {
                AchievementId = currentStep.Id,
                ChainId = chainId,
                CanClaim = canClaim,
                Title = currentStep.Title,
                Description = currentStep.Description,
                ProgressText = progressLabel,
                TargetText = string.Empty,
                RewardCrystal = currentStep.RewardCrystal,
                RewardCrystalText = currentStep.RewardCrystal.ToString("N0"),
                RewardGold = currentStep.RewardGold,
                RewardGoldText = currentStep.RewardGold.ToString("N0")
            };

            return true;
        }

        private void ClearAchievementCards()
        {
            for (int i = 0; i < achievementCardInstances.Count; i++)
            {
                if (achievementCardInstances[i] != null)
                {
                    Destroy(achievementCardInstances[i]);
                }
            }
            achievementCardInstances.Clear();
        }

        private void CreateAchievementCard(AchievementCardData data)
        {
            if (achievementListContainer == null) return;

            GameObject instance;
            if (achievementCardPrefab != null)
            {
                instance = Instantiate(achievementCardPrefab, achievementListContainer);
            }
            else
            {
                instance = new GameObject($"AchievementCard_{data.ChainId}");
                instance.transform.SetParent(achievementListContainer, false);
            }

            var view = instance.GetComponentInChildren<AchievementCardView>(true);
            if (view != null)
            {
                uint achievementId = data.AchievementId;
                view.Apply(data, () => OnAchievementClaimClicked(achievementId));
            }

            achievementCardInstances.Add(instance);
        }

        private void OnAchievementClaimClicked(uint achievementId)
        {
            if (achievementId == 0) return;
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestAchievementClaim(achievementId);
        }

        private string FormatPlayTime(ulong seconds)
        {
            ulong totalMinutes = seconds / 60;
            if (totalMinutes >= 60)
            {
                ulong hours = totalMinutes / 60;
                ulong minutes = totalMinutes % 60;
                return $"{hours:N0}시간 {minutes:N0}분";
            }
            return $"{totalMinutes:N0}분";
        }
    }
}
