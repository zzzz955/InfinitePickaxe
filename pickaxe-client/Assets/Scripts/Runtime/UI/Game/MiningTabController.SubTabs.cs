using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace InfinitePickaxe.Client.UI.Game
{
    public partial class MiningTabController
    {
        private const int SubTabNormalIndex = 0;
        private const int SubTabChallengeIndex = 1;
        private const int SubTabCompetitionIndex = 2;

        [Header("Mining Sub Tabs")]
        [SerializeField] private RectTransform subTabBar;
        [SerializeField] private Button normalTabButton;
        [FormerlySerializedAs("dailyBossTabButton")]
        [SerializeField] private Button challengeTabButton;
        [SerializeField] private Button competitionTabButton;
        [SerializeField] private GameObject normalTabPanel;
        [FormerlySerializedAs("dailyBossTabPanel")]
        [SerializeField] private GameObject challengeTabPanel;
        [SerializeField] private GameObject competitionTabPanel;
        [SerializeField] private Color subTabSelectedColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        [SerializeField] private Color subTabUnselectedColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        private int currentSubTabIndex = SubTabNormalIndex;
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

            if (normalTabPanel == null)
            {
                var center = FindChildRecursive(root, "CenterPanel");
                if (center != null) normalTabPanel = center.gameObject;
            }

            if (subTabBar == null)
            {
                var barTf = FindChildRecursive(root, "MiningSubTabBar");
                if (barTf != null) subTabBar = barTf.GetComponent<RectTransform>();
            }

            if (subTabBar == null)
            {
                Debug.LogWarning("MiningTabController: MiningSubTabBar를 찾을 수 없습니다. 인스펙터 연결 또는 하이어라키 추가가 필요합니다.");
            }

            if (normalTabButton == null) normalTabButton = FindButton("NormalTabButton");
            if (challengeTabButton == null)
            {
                challengeTabButton = FindButton("ChallengeTabButton") ?? FindButton("DailyBossTabButton");
            }
            if (competitionTabButton == null) competitionTabButton = FindButton("CompetitionTabButton");

            if (challengeTabPanel == null)
            {
                var challenge = FindChildRecursive(root, "ChallengePanel");
                if (challenge == null)
                {
                    challenge = FindChildRecursive(root, "DailyBossPanel");
                }
                if (challenge != null) challengeTabPanel = challenge.gameObject;
            }

            if (competitionTabPanel == null)
            {
                var competition = FindChildRecursive(root, "CompetitionPanel");
                if (competition != null) competitionTabPanel = competition.gameObject;
            }

            if (challengeTabPanel == null)
            {
                Debug.LogWarning("MiningTabController: ChallengePanel not found. Assign it in the inspector or add to the hierarchy.");
            }

            if (competitionTabPanel == null)
            {
                Debug.LogWarning("MiningTabController: CompetitionPanel을 찾을 수 없습니다. 스텁 패널을 하이어라키에 추가하세요.");
            }

            // 하이어라키 순서는 인스펙터 설정을 우선으로 둔다.
        }

        private Button FindButton(string name)
        {
            var tf = FindChildRecursive(transform, name);
            return tf != null ? tf.GetComponent<Button>() : null;
        }

        private void BindSubTabButtons()
        {
            if (normalTabButton != null)
            {
                normalTabButton.onClick.RemoveAllListeners();
                normalTabButton.onClick.AddListener(() => SetSubTab(SubTabNormalIndex));
            }

            if (challengeTabButton != null)
            {
                challengeTabButton.onClick.RemoveAllListeners();
                challengeTabButton.onClick.AddListener(() => SetSubTab(SubTabChallengeIndex));
            }

            if (competitionTabButton != null)
            {
                competitionTabButton.onClick.RemoveAllListeners();
                competitionTabButton.onClick.AddListener(() => SetSubTab(SubTabCompetitionIndex));
            }
        }

        private void SetSubTab(int index)
        {
            currentSubTabIndex = NormalizeSubTabIndex(index);

            if (normalTabPanel != null) normalTabPanel.SetActive(currentSubTabIndex == SubTabNormalIndex);
            if (challengeTabPanel != null) challengeTabPanel.SetActive(currentSubTabIndex == SubTabChallengeIndex);
            if (competitionTabPanel != null) competitionTabPanel.SetActive(currentSubTabIndex == SubTabCompetitionIndex);

            UpdateSubTabButton(normalTabButton, currentSubTabIndex == SubTabNormalIndex);
            UpdateSubTabButton(challengeTabButton, currentSubTabIndex == SubTabChallengeIndex);
            UpdateSubTabButton(competitionTabButton, currentSubTabIndex == SubTabCompetitionIndex);
        }

        private int NormalizeSubTabIndex(int index)
        {
            index = Mathf.Clamp(index, SubTabNormalIndex, SubTabCompetitionIndex);
            if (index == SubTabChallengeIndex && challengeTabPanel == null) return SubTabNormalIndex;
            if (index == SubTabCompetitionIndex && competitionTabPanel == null) return SubTabNormalIndex;
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
    }
}
