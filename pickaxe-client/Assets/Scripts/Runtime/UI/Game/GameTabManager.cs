using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 게임 화면의 탭 전환을 관리하는 매니저
    /// FooterBar의 버튼 클릭에 따라 각 탭을 활성화/비활성화
    /// </summary>
    public class GameTabManager : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private GameObject miningTab;
        [SerializeField] private GameObject upgradeTab;
        [SerializeField] private GameObject questTab;
        [SerializeField] private GameObject shopTab;
        [SerializeField] private GameObject settingsTab;

        [Header("Footer Buttons")]
        [SerializeField] private Button mineButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button questButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button settingsButton;

        [Header("Settings")]
        [SerializeField] private GameTab defaultTab = GameTab.Mining;

        private GameTab currentTab;
        private bool isInitialized;

        private void Start()
        {
            InitializeButtons();
            ShowTab(defaultTab);
        }

        /// <summary>
        /// Footer 버튼들에 클릭 이벤트 리스너 등록
        /// </summary>
        private void InitializeButtons()
        {
            if (isInitialized)
            {
                return;
            }

            // 채굴 버튼
            if (mineButton != null)
            {
                mineButton.onClick.AddListener(() => ShowTab(GameTab.Mining));
            }
            else
            {
                Debug.LogWarning("GameTabManager: MineButton이 할당되지 않았습니다.");
            }

            // 강화 버튼
            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(() => ShowTab(GameTab.Upgrade));
            }
            else
            {
                Debug.LogWarning("GameTabManager: UpgradeButton이 할당되지 않았습니다.");
            }

            // 미션 버튼
            if (questButton != null)
            {
                questButton.onClick.AddListener(() => ShowTab(GameTab.Quest));
            }
            else
            {
                Debug.LogWarning("GameTabManager: QuestButton이 할당되지 않았습니다.");
            }

            // 상점 버튼
            if (shopButton != null)
            {
                shopButton.onClick.AddListener(() => ShowTab(GameTab.Shop));
            }
            else
            {
                Debug.LogWarning("GameTabManager: ShopButton이 할당되지 않았습니다.");
            }

            // 설정 버튼
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(() => ShowTab(GameTab.Settings));
            }
            else
            {
                Debug.LogWarning("GameTabManager: SettingsButton이 할당되지 않았습니다.");
            }

            isInitialized = true;

#if UNITY_EDITOR || DEBUG_TAB_MANAGER
            Debug.Log("GameTabManager: 버튼 초기화 완료");
#endif
        }

        /// <summary>
        /// 지정된 탭을 표시하고 나머지는 숨김
        /// </summary>
        /// <param name="tab">표시할 탭</param>
        public void ShowTab(GameTab tab)
        {
            // 이미 활성화된 탭이면 무시
            if (currentTab == tab)
            {
                return;
            }

            currentTab = tab;

            // 모든 탭 비활성화
            SetTabActive(miningTab, false);
            SetTabActive(upgradeTab, false);
            SetTabActive(questTab, false);
            SetTabActive(shopTab, false);
            SetTabActive(settingsTab, false);

            // 선택된 탭만 활성화
            switch (tab)
            {
                case GameTab.Mining:
                    SetTabActive(miningTab, true);
                    UpdateButtonStates(mineButton);
                    break;

                case GameTab.Upgrade:
                    SetTabActive(upgradeTab, true);
                    UpdateButtonStates(upgradeButton);
                    break;

                case GameTab.Quest:
                    SetTabActive(questTab, true);
                    UpdateButtonStates(questButton);
                    break;

                case GameTab.Shop:
                    SetTabActive(shopTab, true);
                    UpdateButtonStates(shopButton);
                    break;

                case GameTab.Settings:
                    SetTabActive(settingsTab, true);
                    UpdateButtonStates(settingsButton);
                    break;
            }

#if UNITY_EDITOR || DEBUG_TAB_MANAGER
            Debug.Log($"GameTabManager: {tab} 탭 활성화");
#endif
        }

        /// <summary>
        /// 탭 GameObject 활성화/비활성화
        /// </summary>
        private void SetTabActive(GameObject tab, bool active)
        {
            if (tab != null)
            {
                tab.SetActive(active);
            }
        }

        /// <summary>
        /// Footer 버튼들의 선택 상태 업데이트 (시각적 피드백)
        /// </summary>
        private void UpdateButtonStates(Button selectedButton)
        {
            // TODO: 선택된 버튼의 시각적 상태 변경 (색상, 크기 등)
            // 현재는 기본 Unity Button 상태로만 표시됨
            // 추후 ColorBlock이나 Animator를 통해 선택 상태 표시 가능
        }

        /// <summary>
        /// 현재 활성화된 탭 반환
        /// </summary>
        public GameTab GetCurrentTab()
        {
            return currentTab;
        }

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("채굴 탭 표시")]
        private void ShowMiningTab() => ShowTab(GameTab.Mining);

        [ContextMenu("강화 탭 표시")]
        private void ShowUpgradeTab() => ShowTab(GameTab.Upgrade);

        [ContextMenu("미션 탭 표시")]
        private void ShowQuestTab() => ShowTab(GameTab.Quest);

        [ContextMenu("상점 탭 표시")]
        private void ShowShopTab() => ShowTab(GameTab.Shop);

        [ContextMenu("설정 탭 표시")]
        private void ShowSettingsTab() => ShowTab(GameTab.Settings);
#endif
        #endregion
    }

    /// <summary>
    /// 게임 탭 타입 열거형
    /// </summary>
    public enum GameTab
    {
        /// <summary>채굴 탭 (기본)</summary>
        Mining = 0,

        /// <summary>강화 탭</summary>
        Upgrade = 1,

        /// <summary>미션 탭</summary>
        Quest = 2,

        /// <summary>상점 탭</summary>
        Shop = 3,

        /// <summary>설정 탭</summary>
        Settings = 4
    }
}
