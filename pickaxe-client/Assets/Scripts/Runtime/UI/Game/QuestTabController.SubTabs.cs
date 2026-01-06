using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public partial class QuestTabController
    {
        private const int SubTabDailyIndex = 0;
        private const int SubTabAchievementIndex = 1;

        [Header("Quest Sub Tabs")]
        [SerializeField] private RectTransform subTabBar;
        [SerializeField] private Button dailyTabButton;
        [SerializeField] private Button achievementTabButton;
        [SerializeField] private GameObject dailyTabRoot;
        [SerializeField] private GameObject achievementTabRoot;
        [SerializeField] private Color subTabSelectedColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        [SerializeField] private Color subTabUnselectedColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        private int currentSubTabIndex = SubTabDailyIndex;
        private bool subTabInitialized;

        private void InitializeSubTabs()
        {
            if (subTabInitialized) return;

            EnsureSubTabReferences();
            BindSubTabButtons();
            SetSubTab(currentSubTabIndex);

            subTabInitialized = true;
        }

        private void EnsureSubTabReferences()
        {
            var root = transform;

            if (subTabBar == null)
            {
                var barTf = FindChildRecursive(root, "QuestSubTabBar");
                if (barTf != null) subTabBar = barTf.GetComponent<RectTransform>();
            }

            if (dailyTabButton == null) dailyTabButton = FindButton("DailyTabButton");
            if (achievementTabButton == null) achievementTabButton = FindButton("AchievementTabButton");

            if (dailyTabRoot == null)
            {
                var dailyPanel = FindChildRecursive(root, "DailyQuestPanel");
                if (dailyPanel != null) dailyTabRoot = dailyPanel.gameObject;
            }

            if (achievementTabRoot == null)
            {
                var achievementPanel = FindChildRecursive(root, "AchievementPanel");
                if (achievementPanel != null) achievementTabRoot = achievementPanel.gameObject;
            }
        }

        private Button FindButton(string name)
        {
            var tf = FindChildRecursive(transform, name);
            return tf != null ? tf.GetComponent<Button>() : null;
        }

        private void BindSubTabButtons()
        {
            if (dailyTabButton != null)
            {
                dailyTabButton.onClick.RemoveAllListeners();
                dailyTabButton.onClick.AddListener(() => SetSubTab(SubTabDailyIndex));
            }

            if (achievementTabButton != null)
            {
                achievementTabButton.onClick.RemoveAllListeners();
                achievementTabButton.onClick.AddListener(() => SetSubTab(SubTabAchievementIndex));
            }
        }

        private void SetSubTab(int index)
        {
            currentSubTabIndex = NormalizeSubTabIndex(index);

            if (dailyTabRoot != null) dailyTabRoot.SetActive(currentSubTabIndex == SubTabDailyIndex);
            if (achievementTabRoot != null) achievementTabRoot.SetActive(currentSubTabIndex == SubTabAchievementIndex);

            UpdateSubTabButton(dailyTabButton, currentSubTabIndex == SubTabDailyIndex);
            UpdateSubTabButton(achievementTabButton, currentSubTabIndex == SubTabAchievementIndex);

            if (currentSubTabIndex == SubTabAchievementIndex)
            {
                RequestAchievementsIfNeeded();
                UpdateAchievementList();
            }
            else
            {
                UpdateQuestCount();
                UpdateMissionList();
                UpdateMilestones();
                UpdateRefreshButton();
            }
        }

        private int NormalizeSubTabIndex(int index)
        {
            index = Mathf.Clamp(index, SubTabDailyIndex, SubTabAchievementIndex);
            if (index == SubTabAchievementIndex && achievementTabRoot == null) return SubTabDailyIndex;
            return index;
        }

        private void UpdateSubTabButton(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected ? subTabSelectedColor : subTabUnselectedColor;
            }
            button.interactable = !selected;
        }

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.Equals(name))
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
