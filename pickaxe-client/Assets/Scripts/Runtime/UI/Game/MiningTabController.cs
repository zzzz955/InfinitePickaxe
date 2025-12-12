using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 채굴 탭 컨트롤러
    /// 현재 채굴 중인 광물 정보, HP 바, DPS 등을 표시
    /// </summary>
    public class MiningTabController : BaseTabController
    {
        [Header("Mining UI References")]
        [SerializeField] private TextMeshProUGUI mineInfoText;
        [SerializeField] private Slider mineHPSlider;
        [SerializeField] private TextMeshProUGUI mineHPText;
        [SerializeField] private TextMeshProUGUI dpsText;
        [SerializeField] private Button selectMineralButton;

        [Header("Mining Data")]
        [SerializeField] private string currentMineralName = "약한 돌";
        [SerializeField] private float currentHP = 25f;
        [SerializeField] private float maxHP = 25f;
        [SerializeField] private float currentDPS = 10f;

        protected override void Initialize()
        {
            base.Initialize();

            // 광물 선택 버튼 이벤트 등록
            if (selectMineralButton != null)
            {
                selectMineralButton.onClick.AddListener(OnSelectMineralClicked);
            }

            // 초기 UI 업데이트
            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();

            // 탭이 표시될 때마다 데이터 갱신
            RefreshData();
        }

        /// <summary>
        /// 채굴 데이터 UI 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateMineInfo();
            UpdateHPBar();
            UpdateDPS();
        }

        /// <summary>
        /// 광물 정보 텍스트 업데이트
        /// </summary>
        private void UpdateMineInfo()
        {
            if (mineInfoText != null)
            {
                mineInfoText.text = $"채굴 중: {currentMineralName}";
            }
        }

        /// <summary>
        /// HP 바 및 텍스트 업데이트
        /// </summary>
        private void UpdateHPBar()
        {
            if (mineHPSlider != null)
            {
                mineHPSlider.maxValue = maxHP;
                mineHPSlider.value = currentHP;
            }

            if (mineHPText != null)
            {
                var hpPercent = maxHP > 0 ? (currentHP / maxHP * 100f) : 0f;
                mineHPText.text = $"HP: {currentHP:F0}/{maxHP:F0} ({hpPercent:F1}%)";
            }
        }

        /// <summary>
        /// DPS 텍스트 업데이트
        /// </summary>
        private void UpdateDPS()
        {
            if (dpsText != null)
            {
                dpsText.text = $"DPS: {currentDPS:F1}";
            }
        }

        /// <summary>
        /// 광물 선택 버튼 클릭 이벤트
        /// </summary>
        private void OnSelectMineralClicked()
        {
            // TODO: 광물 선택 UI 표시
            Debug.Log("MiningTabController: 광물 선택 버튼 클릭됨");
        }

        /// <summary>
        /// 채굴 진행 상태 업데이트 (외부에서 호출)
        /// </summary>
        /// <param name="hp">현재 HP</param>
        /// <param name="maxHp">최대 HP</param>
        /// <param name="dps">현재 DPS</param>
        public void UpdateMiningProgress(float hp, float maxHp, float dps)
        {
            currentHP = hp;
            maxHP = maxHp;
            currentDPS = dps;

            RefreshData();
        }

        /// <summary>
        /// 현재 채굴 중인 광물 변경 (외부에서 호출)
        /// </summary>
        /// <param name="mineralName">광물 이름</param>
        /// <param name="hp">현재 HP</param>
        /// <param name="maxHp">최대 HP</param>
        public void SetCurrentMineral(string mineralName, float hp, float maxHp)
        {
            currentMineralName = mineralName;
            currentHP = hp;
            maxHP = maxHp;

            RefreshData();

#if UNITY_EDITOR || DEBUG_MINING
            Debug.Log($"MiningTabController: 광물 변경 - {mineralName} (HP: {hp}/{maxHp})");
#endif
        }

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("테스트: HP 50% 감소")]
        private void TestReduceHP()
        {
            currentHP = maxHP * 0.5f;
            RefreshData();
        }

        [ContextMenu("테스트: 채굴 완료")]
        private void TestMiningComplete()
        {
            currentHP = 0f;
            RefreshData();
        }

        [ContextMenu("테스트: 새 광물 시작")]
        private void TestNewMineral()
        {
            currentMineralName = "구리";
            maxHP = 1500f;
            currentHP = maxHP;
            currentDPS = 253f;
            RefreshData();
        }
#endif
        #endregion
    }
}
