using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Infinitepickaxe;
using InfinitePickaxe.Client.Net;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// MiningTabController의 보석 슬롯 해금 기능
    /// </summary>
    public partial class MiningTabController
    {
        [Header("Gem Slot Unlock Modal")]
        [SerializeField] private GameObject gemSlotUnlockModal;
        [SerializeField] private TextMeshProUGUI unlockSlotIndexText;
        [SerializeField] private TextMeshProUGUI unlockCostText;
        [SerializeField] private TextMeshProUGUI unlockCurrentCrystalText;
        [SerializeField] private Button unlockConfirmButton;
        [SerializeField] private Button unlockCancelButton;

        // 슬롯 해금 비용 (메타데이터와 동일)
        private static readonly uint[] GemSlotUnlockCosts = { 0, 100, 200, 400, 800, 1600 };

        // 현재 해금 시도 중인 슬롯 정보
        private uint pendingUnlockPickaxeSlotIndex;
        private uint pendingUnlockGemSlotIndex;

        /// <summary>
        /// GemSlotUnlockModal AutoBind
        /// </summary>
        private void AutoBindGemSlotUnlockModal()
        {
            if (gemSlotUnlockModal == null)
            {
                var modalObj = GameObject.Find("GemSlotUnlockModal");
                if (modalObj == null)
                {
                    // Resources에서 로드
                    var prefab = Resources.Load<GameObject>("UI/GemSlotUnlockModal");
                    if (prefab != null)
                    {
                        gemSlotUnlockModal = Instantiate(prefab, transform.root);
                        gemSlotUnlockModal.name = "GemSlotUnlockModal";
                        gemSlotUnlockModal.SetActive(false);
                    }
                }
                else
                {
                    gemSlotUnlockModal = modalObj;
                }
            }

            if (gemSlotUnlockModal == null) return;

            var root = gemSlotUnlockModal.transform;

            // ModalPanel 찾기
            var modalPanel = FindChildRecursive(root, "ModalPanel");
            if (modalPanel == null) return;

            // Text 컴포넌트 바인딩
            if (unlockSlotIndexText == null)
            {
                var slotIndexTf = FindChildRecursive(modalPanel, "SlotIndexText");
                if (slotIndexTf != null)
                {
                    unlockSlotIndexText = slotIndexTf.GetComponent<TextMeshProUGUI>();
                }
            }

            if (unlockCostText == null)
            {
                var costTf = FindChildRecursive(modalPanel, "CostText");
                if (costTf != null)
                {
                    unlockCostText = costTf.GetComponent<TextMeshProUGUI>();
                }
            }

            if (unlockCurrentCrystalText == null)
            {
                var currentTf = FindChildRecursive(modalPanel, "CurrentCrystalText");
                if (currentTf != null)
                {
                    unlockCurrentCrystalText = currentTf.GetComponent<TextMeshProUGUI>();
                }
            }

            // Button 바인딩
            var buttonPanel = FindChildRecursive(modalPanel, "ButtonPanel");
            if (buttonPanel != null)
            {
                if (unlockCancelButton == null)
                {
                    var cancelTf = FindChildRecursive(buttonPanel, "CancelButton");
                    if (cancelTf != null)
                    {
                        unlockCancelButton = cancelTf.GetComponent<Button>();
                    }
                }

                if (unlockConfirmButton == null)
                {
                    var confirmTf = FindChildRecursive(buttonPanel, "ConfirmButton");
                    if (confirmTf != null)
                    {
                        unlockConfirmButton = confirmTf.GetComponent<Button>();
                    }
                }
            }
        }

        /// <summary>
        /// GemSlotUnlockModal 버튼 이벤트 설정
        /// </summary>
        private void SetupGemSlotUnlockModalButtons()
        {
            if (gemSlotUnlockModal == null) return;

            // 배경 클릭으로 닫기
            var bgButton = gemSlotUnlockModal.GetComponent<Button>();
            if (bgButton != null)
            {
                bgButton.onClick.RemoveAllListeners();
                bgButton.onClick.AddListener(() => CloseGemSlotUnlockModal());
            }

            // ModalPanel 클릭 이벤트 차단
            var modalPanel = gemSlotUnlockModal.transform.Find("ModalPanel");
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = UnityEngine.UI.Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

            // 취소 버튼
            if (unlockCancelButton != null)
            {
                unlockCancelButton.onClick.RemoveAllListeners();
                unlockCancelButton.onClick.AddListener(() => CloseGemSlotUnlockModal());
            }

            // 확인 버튼
            if (unlockConfirmButton != null)
            {
                unlockConfirmButton.onClick.RemoveAllListeners();
                unlockConfirmButton.onClick.AddListener(() => OnConfirmGemSlotUnlock());
            }
        }

        /// <summary>
        /// 보석 슬롯 해금 모달 오픈
        /// </summary>
        /// <param name="pickaxeSlotIndex">곡괭이 슬롯 인덱스 (0-3)</param>
        /// <param name="gemSlotIndex">보석 슬롯 인덱스 (0-5)</param>
        /// <param name="currentCrystal">현재 보유 크리스탈</param>
        public void OpenGemSlotUnlockModal(uint pickaxeSlotIndex, uint gemSlotIndex, uint currentCrystal)
        {
            if (gemSlotUnlockModal == null) return;

            // 슬롯 해금 비용 조회
            if (gemSlotIndex >= GemSlotUnlockCosts.Length)
            {
                Debug.LogError($"Invalid gem slot index: {gemSlotIndex}");
                return;
            }

            uint cost = GemSlotUnlockCosts[gemSlotIndex];

            // 정보 저장
            pendingUnlockPickaxeSlotIndex = pickaxeSlotIndex;
            pendingUnlockGemSlotIndex = gemSlotIndex;

            // UI 업데이트
            if (unlockSlotIndexText != null)
            {
                unlockSlotIndexText.text = $"{gemSlotIndex}번 슬롯";
            }

            if (unlockCostText != null)
            {
                unlockCostText.text = $"필요 크리스탈: {cost}";
            }

            if (unlockCurrentCrystalText != null)
            {
                unlockCurrentCrystalText.text = $"보유: {currentCrystal}";
            }

            // 확인 버튼 활성화/비활성화
            if (unlockConfirmButton != null)
            {
                unlockConfirmButton.interactable = (currentCrystal >= cost);
            }

            gemSlotUnlockModal.SetActive(true);
        }

        /// <summary>
        /// 보석 슬롯 해금 모달 닫기
        /// </summary>
        private void CloseGemSlotUnlockModal()
        {
            if (gemSlotUnlockModal != null)
            {
                gemSlotUnlockModal.SetActive(false);
            }
        }

        /// <summary>
        /// 확인 버튼 클릭 시 서버 요청
        /// </summary>
        private void OnConfirmGemSlotUnlock()
        {
            // 서버로 GemSlotUnlockRequest 전송
            var request = new GemSlotUnlockRequest
            {
                PickaxeSlotIndex = pendingUnlockPickaxeSlotIndex,
                GemSlotIndex = pendingUnlockGemSlotIndex
            };

            var envelope = new Envelope
            {
                Type = MessageType.GemSlotUnlockRequest,
                GemSlotUnlockRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log($"Sent GemSlotUnlockRequest: pickaxe_slot={pendingUnlockPickaxeSlotIndex}, gem_slot={pendingUnlockGemSlotIndex}");

            // 모달 닫기
            CloseGemSlotUnlockModal();
        }

        /// <summary>
        /// 서버로부터 GemSlotUnlockResult 수신 시 처리
        /// </summary>
        /// <param name="result">해금 결과</param>
        public void OnGemSlotUnlockResult(GemSlotUnlockResult result)
        {
            if (!result.Success)
            {
                // 실패 처리
                Debug.LogWarning($"Gem slot unlock failed: {result.ErrorCode}");

                string errorMessage = result.ErrorCode switch
                {
                    "PICKAXE_SLOT_NOT_FOUND" => "곡괭이 슬롯을 찾을 수 없습니다.",
                    "PREVIOUS_SLOT_LOCKED" => "이전 슬롯을 먼저 해금해야 합니다.",
                    "ALREADY_UNLOCKED" => "이미 해금된 슬롯입니다.",
                    "INSUFFICIENT_CRYSTAL" => "크리스탈이 부족합니다.",
                    _ => $"해금 실패: {result.ErrorCode}"
                };

                // 에러 메시지 표시 (TODO: 에러 모달 구현)
                Debug.LogError(errorMessage);
                return;
            }

            // 성공 처리
            Debug.Log($"Gem slot unlocked: pickaxe_slot={result.PickaxeSlotIndex}, gem_slot={result.GemSlotIndex}, crystal_spent={result.CrystalSpent}");

            // 크리스탈 UI 동기화
            UpdateCrystalUI(result.RemainingCrystal);

            // PickaxeInfoModal 갱신 (해금된 슬롯 표시)
            RefreshPickaxeInfoGemSlots();
        }

        /// <summary>
        /// 크리스탈 UI 업데이트
        /// </summary>
        /// <remarks>
        /// MessageHandler의 lastCrystal이 자동으로 업데이트되고,
        /// TopbarController가 OnCurrencyUpdate 이벤트를 구독하여 자동으로 UI 갱신됨.
        /// 별도 처리 불필요.
        /// </remarks>
        private void UpdateCrystalUI(uint crystal)
        {
            // MessageHandler.Instance의 lastCrystal 업데이트는
            // GemSlotUnlockResult 처리 시 자동으로 이루어져야 함
            // TopbarController가 자동으로 UI 갱신
            Debug.Log($"Crystal updated: {crystal}");
        }

        /// <summary>
        /// PickaxeInfoModal의 보석 슬롯 섹션 갱신
        /// </summary>
        private void RefreshPickaxeInfoGemSlots()
        {
            // AllSlotsRequest 재요청하여 최신 데이터 가져오기
            var request = new AllSlotsRequest();
            var envelope = new Envelope
            {
                Type = MessageType.AllSlotsRequest,
                AllSlotsRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log("Sent AllSlotsRequest to refresh gem slots");
        }

        /// <summary>
        /// LockedOverlay 클릭 시 순차 해금 검증 및 모달 오픈
        /// </summary>
        /// <param name="pickaxeSlotIndex">곡괭이 슬롯 인덱스</param>
        /// <param name="gemSlotIndex">보석 슬롯 인덱스</param>
        /// <param name="unlockedSlots">해금된 슬롯 인덱스 배열</param>
        /// <param name="currentCrystal">현재 보유 크리스탈</param>
        public void OnLockedGemSlotClicked(uint pickaxeSlotIndex, uint gemSlotIndex, bool[] unlockedSlots, uint currentCrystal)
        {
            // 순차 해금 검증 (클라이언트 측)
            if (gemSlotIndex > 0)
            {
                for (uint i = 0; i < gemSlotIndex; i++)
                {
                    if (i >= unlockedSlots.Length || !unlockedSlots[i])
                    {
                        Debug.LogWarning($"Cannot unlock slot {gemSlotIndex}: slot {i} is locked");
                        // TODO: 에러 메시지 표시
                        return;
                    }
                }
            }

            // 검증 통과 시 모달 오픈
            OpenGemSlotUnlockModal(pickaxeSlotIndex, gemSlotIndex, currentCrystal);
        }
    }
}
