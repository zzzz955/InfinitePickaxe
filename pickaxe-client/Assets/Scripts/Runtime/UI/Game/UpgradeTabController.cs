using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 강화 탭 컨트롤러
    /// 곡괭이 강화 UI 및 로직 처리
    /// </summary>
    public class UpgradeTabController : BaseTabController
    {
        [Header("Upgrade UI References")]
        [SerializeField] private TextMeshProUGUI pickaxeLevelText;
        [SerializeField] private TextMeshProUGUI currentDPSText;
        [SerializeField] private TextMeshProUGUI nextDPSText;
        [SerializeField] private TextMeshProUGUI upgradeCostText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button adDiscountButton;

        [Header("Upgrade Data")]
        [SerializeField] private int currentLevel = 0;
        [SerializeField] private float currentDPS = 10f;
        [SerializeField] private float nextDPS = 17f;
        [SerializeField] private int upgradeCost = 5;

        protected override void Initialize()
        {
            base.Initialize();

            // 강화 버튼 이벤트 등록
            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }

            // 광고 할인 버튼 이벤트 등록
            if (adDiscountButton != null)
            {
                adDiscountButton.onClick.AddListener(OnAdDiscountClicked);
            }

            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        /// <summary>
        /// 강화 UI 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateLevelText();
            UpdateDPSText();
            UpdateCostText();
            UpdateButtonState();
        }

        private void UpdateLevelText()
        {
            if (pickaxeLevelText != null)
            {
                pickaxeLevelText.text = $"곡괭이 레벨: {currentLevel}";
            }
        }

        private void UpdateDPSText()
        {
            if (currentDPSText != null)
            {
                currentDPSText.text = $"현재 DPS: {currentDPS:F0}";
            }

            if (nextDPSText != null)
            {
                var increase = currentDPS > 0 ? ((nextDPS - currentDPS) / currentDPS * 100f) : 0f;
                nextDPSText.text = $"다음 DPS: {nextDPS:F0} (+{increase:F0}%)";
            }
        }

        private void UpdateCostText()
        {
            if (upgradeCostText != null)
            {
                upgradeCostText.text = $"강화 비용: {upgradeCost:N0} 골드";
            }
        }

        private void UpdateButtonState()
        {
            if (upgradeButton != null)
            {
                // TODO: 골드 부족 시 버튼 비활성화
                // upgradeButton.interactable = (playerGold >= upgradeCost);
            }
        }

        /// <summary>
        /// 강화 버튼 클릭 이벤트
        /// </summary>
        private void OnUpgradeClicked()
        {
            // TODO: 서버로 강화 요청 전송
            Debug.Log("UpgradeTabController: 강화 버튼 클릭됨");
        }

        /// <summary>
        /// 광고 할인 버튼 클릭 이벤트
        /// </summary>
        private void OnAdDiscountClicked()
        {
            // TODO: 광고 시청 후 비용 -25%
            Debug.Log("UpgradeTabController: 광고 할인 버튼 클릭됨");
        }

        /// <summary>
        /// 강화 데이터 설정 (외부에서 호출)
        /// </summary>
        public void SetUpgradeData(int level, float dps, float nextDps, int cost)
        {
            currentLevel = level;
            currentDPS = dps;
            nextDPS = nextDps;
            upgradeCost = cost;

            RefreshData();
        }

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("테스트: 레벨 5")]
        private void TestLevel5()
        {
            SetUpgradeData(5, 110f, 168f, 100);
        }
#endif
        #endregion
    }
}
