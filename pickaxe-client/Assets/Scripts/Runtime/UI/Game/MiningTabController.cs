using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

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

        [Header("Pickaxe Slot References")]
        [SerializeField] private Button pickaxeSlot1Button;
        [SerializeField] private Button pickaxeSlot2Button;
        [SerializeField] private Button pickaxeSlot3Button;
        [SerializeField] private Button pickaxeSlot4Button;

        [Header("Modal References")]
        [SerializeField] private GameObject pickaxeInfoModal;
        [SerializeField] private GameObject lockedSlotModal;
        [SerializeField] private GameObject mineralSelectModal;

        [Header("Mining Data")]
        [SerializeField] private string currentMineralName = "약한 돌";
        [SerializeField] private float currentHP = 25f;
        [SerializeField] private float maxHP = 25f;
        [SerializeField] private float currentDPS = 10f;
        [SerializeField] private bool slot2Unlocked = true;
        [SerializeField] private bool slot3Unlocked = false;
        [SerializeField] private bool slot4Unlocked = false;

        private GameTabManager tabManager;
        private MessageHandler messageHandler;

        protected override void Initialize()
        {
            base.Initialize();

            // GameTabManager 찾기
            tabManager = FindObjectOfType<GameTabManager>();
            if (tabManager == null)
            {
                Debug.LogWarning("MiningTabController: GameTabManager를 찾을 수 없습니다.");
            }

            // MessageHandler 찾기
            messageHandler = MessageHandler.Instance;
            if (messageHandler == null)
            {
                Debug.LogWarning("MiningTabController: MessageHandler를 찾을 수 없습니다.");
            }

            // 광물 선택 버튼 이벤트 등록
            if (selectMineralButton != null)
            {
                selectMineralButton.onClick.AddListener(OnSelectMineralClicked);
            }

            // 슬롯 버튼 이벤트 등록
            if (pickaxeSlot1Button != null)
            {
                pickaxeSlot1Button.onClick.AddListener(() => OnPickaxeSlotClicked(1));
            }
            if (pickaxeSlot2Button != null)
            {
                pickaxeSlot2Button.onClick.AddListener(() => OnPickaxeSlotClicked(2));
            }
            if (pickaxeSlot3Button != null)
            {
                pickaxeSlot3Button.onClick.AddListener(() => OnPickaxeSlotClicked(3));
            }
            if (pickaxeSlot4Button != null)
            {
                pickaxeSlot4Button.onClick.AddListener(() => OnPickaxeSlotClicked(4));
            }

            // 모달 닫기 버튼 이벤트 등록
            SetupModalCloseButtons();

            // 초기 UI 업데이트
            RefreshData();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // MessageHandler 이벤트 구독
            if (messageHandler != null)
            {
                messageHandler.OnUserDataSnapshot += HandleUserDataSnapshot;
                messageHandler.OnMiningUpdate += HandleMiningUpdate;
                messageHandler.OnMiningComplete += HandleMiningComplete;
                messageHandler.OnChangeMineralResponse += HandleChangeMineralResponse;
                messageHandler.OnAllSlotsResponse += HandleAllSlotsResponse;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // MessageHandler 이벤트 구독 해제
            if (messageHandler != null)
            {
                messageHandler.OnUserDataSnapshot -= HandleUserDataSnapshot;
                messageHandler.OnMiningUpdate -= HandleMiningUpdate;
                messageHandler.OnMiningComplete -= HandleMiningComplete;
                messageHandler.OnChangeMineralResponse -= HandleChangeMineralResponse;
                messageHandler.OnAllSlotsResponse -= HandleAllSlotsResponse;
            }
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
            OpenMineralSelectModal();
        }

        /// <summary>
        /// 곡괭이 슬롯 클릭 이벤트
        /// </summary>
        private void OnPickaxeSlotClicked(int slotIndex)
        {
            // 슬롯 해금 여부 확인
            bool isUnlocked = slotIndex switch
            {
                1 => true, // 슬롯 1은 항상 해금
                2 => slot2Unlocked,
                3 => slot3Unlocked,
                4 => slot4Unlocked,
                _ => false
            };

            if (isUnlocked)
            {
                // 해금된 슬롯: 곡괭이 정보 모달 열기
                OpenPickaxeInfoModal(slotIndex);
            }
            else
            {
                // 잠긴 슬롯: 잠금 안내 모달 열기
                OpenLockedSlotModal();
            }
        }

        /// <summary>
        /// 모달 닫기 버튼 이벤트 설정
        /// </summary>
        private void SetupModalCloseButtons()
        {
            // PickaxeInfoModal 닫기 버튼
            if (pickaxeInfoModal != null)
            {
                var closeButton = pickaxeInfoModal.transform.Find("ModalPanel/ButtonRow/CloseButton");
                if (closeButton != null)
                {
                    var button = closeButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(() => CloseModal(pickaxeInfoModal));
                    }
                }

                // 배경 클릭으로 닫기
                var bgButton = pickaxeInfoModal.GetComponent<Button>();
                if (bgButton != null)
                {
                    bgButton.onClick.AddListener(() => CloseModal(pickaxeInfoModal));
                }

                // 강화 버튼 (모달 닫고 업그레이드 탭으로 이동)
                var upgradeButton = pickaxeInfoModal.transform.Find("ModalPanel/ButtonRow/UpgradeButton");
                if (upgradeButton != null)
                {
                    var button = upgradeButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(OnUpgradeButtonClicked);
                    }
                }
            }

            // LockedSlotModal 닫기 버튼
            if (lockedSlotModal != null)
            {
                var closeButton = lockedSlotModal.transform.Find("ModalPanel/ButtonRow/CloseButton");
                if (closeButton != null)
                {
                    var button = closeButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(() => CloseModal(lockedSlotModal));
                    }
                }

                // 배경 클릭으로 닫기
                var bgButton = lockedSlotModal.GetComponent<Button>();
                if (bgButton != null)
                {
                    bgButton.onClick.AddListener(() => CloseModal(lockedSlotModal));
                }

                // 상점 버튼 (모달 닫고 상점 탭으로 이동)
                var shopButton = lockedSlotModal.transform.Find("ModalPanel/ButtonRow/ShopButton");
                if (shopButton != null)
                {
                    var button = shopButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(OnShopButtonClicked);
                    }
                }
            }

            // MineralSelectModal 닫기 버튼
            if (mineralSelectModal != null)
            {
                var closeButton = mineralSelectModal.transform.Find("ModalPanel/CloseButton");
                if (closeButton != null)
                {
                    var button = closeButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(() => CloseModal(mineralSelectModal));
                    }
                }

                // 배경 클릭으로 닫기
                var bgButton = mineralSelectModal.GetComponent<Button>();
                if (bgButton != null)
                {
                    bgButton.onClick.AddListener(() => CloseModal(mineralSelectModal));
                }
            }
        }

        /// <summary>
        /// 곡괭이 정보 모달 열기
        /// </summary>
        private void OpenPickaxeInfoModal(int slotIndex)
        {
            if (pickaxeInfoModal != null)
            {
                // TODO: 슬롯 인덱스에 맞는 곡괭이 정보 업데이트
                pickaxeInfoModal.SetActive(true);
                Debug.Log($"MiningTabController: 곡괭이 슬롯 {slotIndex} 정보 모달 열림");
            }
        }

        /// <summary>
        /// 잠긴 슬롯 모달 열기
        /// </summary>
        private void OpenLockedSlotModal()
        {
            if (lockedSlotModal != null)
            {
                lockedSlotModal.SetActive(true);
                Debug.Log("MiningTabController: 잠긴 슬롯 모달 열림");
            }
        }

        /// <summary>
        /// 광물 선택 모달 열기
        /// </summary>
        private void OpenMineralSelectModal()
        {
            if (mineralSelectModal != null)
            {
                mineralSelectModal.SetActive(true);
                Debug.Log("MiningTabController: 광물 선택 모달 열림");
            }
        }

        /// <summary>
        /// 모달 닫기
        /// </summary>
        private void CloseModal(GameObject modal)
        {
            if (modal != null)
            {
                modal.SetActive(false);
            }
        }

        /// <summary>
        /// 강화 버튼 클릭 (곡괭이 정보 모달에서)
        /// </summary>
        private void OnUpgradeButtonClicked()
        {
            CloseModal(pickaxeInfoModal);

            if (tabManager != null)
            {
                tabManager.ShowTab(GameTab.Upgrade);
                Debug.Log("MiningTabController: 강화 탭으로 이동");
            }
            else
            {
                Debug.LogError("MiningTabController: GameTabManager를 찾을 수 없어 탭 전환 실패");
            }
        }

        /// <summary>
        /// 상점 버튼 클릭 (잠긴 슬롯 모달에서)
        /// </summary>
        private void OnShopButtonClicked()
        {
            CloseModal(lockedSlotModal);

            if (tabManager != null)
            {
                tabManager.ShowTab(GameTab.Shop);
                Debug.Log("MiningTabController: 상점 탭으로 이동");
            }
            else
            {
                Debug.LogError("MiningTabController: GameTabManager를 찾을 수 없어 탭 전환 실패");
            }
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

        #region Server Message Handlers

        /// <summary>
        /// 유저 데이터 스냅샷 처리
        /// </summary>
        private void HandleUserDataSnapshot(UserDataSnapshot snapshot)
        {
            // 광물 정보 업데이트
            if (snapshot.CurrentMineralId.HasValue)
            {
                uint mineralId = snapshot.CurrentMineralId.Value;
                // TODO: 광물 ID로 이름 조회 (MineralMetadata 필요)
                currentMineralName = $"광물 #{mineralId}";
            }

            // HP 정보 업데이트
            if (snapshot.MineralHp != null && snapshot.MineralMaxHp != null)
            {
                currentHP = snapshot.MineralHp.Value;
                maxHP = snapshot.MineralMaxHp.Value;
            }

            // DPS 정보 업데이트
            if (snapshot.TotalDps > 0)
            {
                currentDPS = snapshot.TotalDps;
            }

            // 슬롯 해금 정보 업데이트
            for (int i = 0; i < snapshot.UnlockedSlots.Count && i < 4; i++)
            {
                bool isUnlocked = snapshot.UnlockedSlots[i];
                switch (i)
                {
                    case 0: /* 슬롯 1은 항상 해금 */ break;
                    case 1: slot2Unlocked = isUnlocked; break;
                    case 2: slot3Unlocked = isUnlocked; break;
                    case 3: slot4Unlocked = isUnlocked; break;
                }
            }

            RefreshData();
        }

        /// <summary>
        /// 채굴 진행 업데이트 처리 (서버 40ms 틱)
        /// </summary>
        private void HandleMiningUpdate(MiningUpdate update)
        {
            currentHP = update.CurrentHp;
            maxHP = update.MaxHp;

            // DPS는 AllSlotsResponse에서 받은 값 유지 (TotalDps 필드 제거됨)
            // currentDPS는 슬롯 변경 시에만 업데이트됨

            // 각 곡괭이 공격 처리 (애니메이션 트리거)
            if (update.Attacks != null && update.Attacks.Count > 0)
            {
                foreach (var attack in update.Attacks)
                {
                    TriggerPickaxeAttackAnimation(attack.SlotIndex, attack.Damage);
                }
            }

            UpdateHPBar();
            // UpdateDPS()는 호출하지 않음 (DPS는 슬롯 정보 변경 시에만 업데이트)
        }

        /// <summary>
        /// 채굴 완료 처리
        /// </summary>
        private void HandleMiningComplete(MiningComplete complete)
        {
            Debug.Log($"채굴 완료! 광물 #{complete.MineralId}, 획득 골드: {complete.GoldEarned}");

            // 다음 광물 자동 시작 (서버에서 MiningUpdate가 올 것임)
            // UI는 자동으로 업데이트됨
        }

        /// <summary>
        /// 곡괭이 공격 애니메이션 트리거
        /// </summary>
        /// <param name="slotIndex">슬롯 인덱스 (0~3)</param>
        /// <param name="damage">공격 데미지</param>
        private void TriggerPickaxeAttackAnimation(uint slotIndex, ulong damage)
        {
            // TODO: 슬롯별 곡괭이 공격 애니메이션 재생
            // 예시:
            // - 곡괭이 슬롯 버튼에 Animator 컴포넌트 추가
            // - Attack 트리거 파라미터 설정
            // - pickaxeSlotAnimators[slotIndex]?.SetTrigger("Attack");
            //
            // 또는:
            // - 파티클 이펙트 재생
            // - 데미지 텍스트 표시 (Floating Damage Number)
            // - 사운드 재생

#if UNITY_EDITOR || DEBUG_MINING
            Debug.Log($"곡괭이 공격: 슬롯 {slotIndex}, 데미지 {damage}");
#endif

            // 임시: 슬롯 버튼 찾아서 간단한 시각 효과
            // (실제 구현 시 아래 코드 대신 애니메이션 사용)
            Button slotButton = slotIndex switch
            {
                0 => pickaxeSlot1Button,
                1 => pickaxeSlot2Button,
                2 => pickaxeSlot3Button,
                3 => pickaxeSlot4Button,
                _ => null
            };

            if (slotButton != null)
            {
                // TODO: 실제 애니메이션 재생
                // 예: slotButton.GetComponent<Animator>()?.SetTrigger("Attack");
            }
        }

        /// <summary>
        /// 광물 변경 응답 처리
        /// </summary>
        private void HandleChangeMineralResponse(ChangeMineralResponse response)
        {
            if (response.Success)
            {
                uint mineralId = response.MineralId;
                // TODO: 광물 ID로 이름 조회
                currentMineralName = $"광물 #{mineralId}";
                currentHP = response.MineralHp;
                maxHP = response.MineralMaxHp;

                RefreshData();

                CloseModal(mineralSelectModal);
            }
            else
            {
                Debug.LogWarning($"광물 변경 실패: {response.ErrorCode}");
            }
        }

        /// <summary>
        /// 모든 슬롯 정보 응답 처리
        /// </summary>
        private void HandleAllSlotsResponse(AllSlotsResponse response)
        {
            // 총 DPS 업데이트
            currentDPS = response.TotalDps;

            // 슬롯별 해금 정보 업데이트
            foreach (var slot in response.Slots)
            {
                switch (slot.SlotIndex)
                {
                    case 1: /* 슬롯 1은 항상 해금 */ break;
                    case 2: slot2Unlocked = slot.IsUnlocked; break;
                    case 3: slot3Unlocked = slot.IsUnlocked; break;
                    case 4: slot4Unlocked = slot.IsUnlocked; break;
                }
            }

            RefreshData();
        }

        #endregion

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
