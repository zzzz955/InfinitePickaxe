using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 미션 탭 컨트롤러
    /// 일일 미션 목록 및 보상 표시
    /// </summary>
    public class QuestTabController : BaseTabController
    {
        [Header("Quest UI References")]
        [SerializeField] private TextMeshProUGUI questCountText;
        [SerializeField] private Transform questListContainer;
        [SerializeField] private GameObject questItemPrefab;
        [SerializeField] private Button refreshQuestButton;
        [SerializeField] private TextMeshProUGUI refreshCountText;

        [Header("Milestone UI")]
        [SerializeField] private TextMeshProUGUI milestone3Text;
        [SerializeField] private TextMeshProUGUI milestone5Text;
        [SerializeField] private TextMeshProUGUI milestone7Text;

        [Header("Quest Data")]
        [SerializeField] private int completedCount = 0;
        [SerializeField] private int totalCount = 7;
        [SerializeField] private int freeRefreshCount = 2;
        [SerializeField] private int usedRefreshCount = 0;

        protected override void Initialize()
        {
            base.Initialize();

            // 미션 재설정 버튼 이벤트 등록
            if (refreshQuestButton != null)
            {
                refreshQuestButton.onClick.AddListener(OnRefreshQuestClicked);
            }

            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        /// <summary>
        /// 미션 UI 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateQuestCount();
            UpdateMilestones();
            UpdateRefreshButton();
            // TODO: 미션 리스트 갱신 (서버에서 데이터 받아오기)
        }

        private void UpdateQuestCount()
        {
            if (questCountText != null)
            {
                questCountText.text = $"일일 미션 ({completedCount}/{totalCount} 완료)";
            }
        }

        private void UpdateMilestones()
        {
            if (milestone3Text != null)
            {
                var status = completedCount >= 3 ? "✅" : "⬜";
                milestone3Text.text = $"{status} 3개 완료: 오프라인 +1h";
            }

            if (milestone5Text != null)
            {
                var status = completedCount >= 5 ? "✅" : "⬜";
                milestone5Text.text = $"{status} 5개 완료: 오프라인 +1h";
            }

            if (milestone7Text != null)
            {
                var status = completedCount >= 7 ? "✅" : "⬜";
                milestone7Text.text = $"{status} 7개 완료: 오프라인 +1h";
            }
        }

        private void UpdateRefreshButton()
        {
            if (refreshCountText != null)
            {
                var remaining = freeRefreshCount - usedRefreshCount;
                refreshCountText.text = $"미션 재설정 (무료 {remaining}/{freeRefreshCount})";
            }

            if (refreshQuestButton != null)
            {
                refreshQuestButton.interactable = (usedRefreshCount < freeRefreshCount);
            }
        }

        /// <summary>
        /// 미션 재설정 버튼 클릭 이벤트
        /// </summary>
        private void OnRefreshQuestClicked()
        {
            // TODO: 서버로 미션 재설정 요청
            Debug.Log("QuestTabController: 미션 재설정 버튼 클릭됨");
        }

        /// <summary>
        /// 미션 진행 상태 업데이트 (외부에서 호출)
        /// </summary>
        public void SetQuestProgress(int completed, int total)
        {
            completedCount = completed;
            totalCount = total;

            RefreshData();
        }

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("테스트: 3개 완료")]
        private void TestComplete3()
        {
            SetQuestProgress(3, 7);
        }

        [ContextMenu("테스트: 7개 완료")]
        private void TestComplete7()
        {
            SetQuestProgress(7, 7);
        }
#endif
        #endregion
    }
}
