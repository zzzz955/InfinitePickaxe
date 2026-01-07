using UnityEngine;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public partial class QuestTabController
    {
        [Header("Reward Stove UI")]
        [SerializeField] private RewardStoveModalController rewardStoveModal;

        private bool rewardStoveSubscribed;

        private void EnsureRewardStoveReferences()
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
            instance.SetActive(false);
            rewardStoveModal = instance.GetComponent<RewardStoveModalController>();
        }

        private void SubscribeRewardStoveMessageHandler()
        {
            if (rewardStoveSubscribed) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            messageHandler.OnMissionCompleteResult += HandleMissionCompleteResult;
            messageHandler.OnMilestoneClaimResult += HandleMilestoneClaimResult;
            messageHandler.OnWeeklyMissionClaimResult += HandleWeeklyMissionClaimResult;
            messageHandler.OnAchievementClaimResult += HandleAchievementClaimResult;
            rewardStoveSubscribed = true;
        }

        private void UnsubscribeRewardStoveMessageHandler()
        {
            if (!rewardStoveSubscribed || messageHandler == null) return;

            messageHandler.OnMissionCompleteResult -= HandleMissionCompleteResult;
            messageHandler.OnMilestoneClaimResult -= HandleMilestoneClaimResult;
            messageHandler.OnWeeklyMissionClaimResult -= HandleWeeklyMissionClaimResult;
            messageHandler.OnAchievementClaimResult -= HandleAchievementClaimResult;
            rewardStoveSubscribed = false;
        }

        private void HandleMissionCompleteResult(MissionCompleteResult result)
        {
            if (result == null || !result.Success) return;
            ShowRewardStove(result.RewardCrystal, 0);
        }

        private void HandleMilestoneClaimResult(MilestoneClaimResult result)
        {
            if (result == null || !result.Success) return;
            ShowRewardStove(result.RewardCrystal, 0);
        }

        private void HandleWeeklyMissionClaimResult(WeeklyMissionClaimResult result)
        {
            if (result == null || !result.Success) return;
            ShowRewardStove(result.RewardCrystal, result.RewardGold);
        }

        private void HandleAchievementClaimResult(AchievementClaimResult result)
        {
            if (result == null || !result.Success) return;
            ShowRewardStove(result.RewardCrystal, result.RewardGold);
        }

        private void ShowRewardStove(uint rewardCrystal, ulong rewardGold)
        {
            if (rewardCrystal == 0 && rewardGold == 0) return;
            EnsureRewardStoveReferences();
            if (rewardStoveModal == null) return;
            rewardStoveModal.Show(rewardCrystal, rewardGold);
        }
    }
}
