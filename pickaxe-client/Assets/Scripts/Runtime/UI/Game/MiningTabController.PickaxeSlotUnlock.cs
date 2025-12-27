using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Infinitepickaxe;
using InfinitePickaxe.Client.Net;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// MiningTabController의 곡괭이 슬롯 해금 기능
    /// </summary>
    public partial class MiningTabController
    {
        [Header("Pickaxe Slot Unlock Modal")]
        [SerializeField] private GameObject pickaxeSlotUnlockModal;
        [SerializeField] private TextMeshProUGUI pickaxeUnlockSlotIndexText;
        [SerializeField] private TextMeshProUGUI pickaxeUnlockCostText;
        [SerializeField] private TextMeshProUGUI pickaxeUnlockCurrentCrystalText;
        [SerializeField] private Button pickaxeUnlockConfirmButton;
        [SerializeField] private Button pickaxeUnlockCancelButton;

        // 슬롯 해금 비용 (메타데이터와 동일)
        private static readonly uint[] PickaxeSlotUnlockCosts = { 0, 400, 2000, 4000 };

        // 현재 해금 시도 중인 슬롯 정보
        private uint pendingUnlockSlotIndex;

        /// <summary>
        /// PickaxeSlotUnlockModal AutoBind
        /// </summary>
        private void AutoBindPickaxeSlotUnlockModal()
        {
            if (pickaxeSlotUnlockModal == null)
            {
                var modalObj = GameObject.Find("PickaxeSlotUnlockModal");
                if (modalObj == null)
                {
                    // Resources에서 로드
                    var prefab = Resources.Load<GameObject>("UI/PickaxeSlotUnlockModal");
                    if (prefab != null)
                    {
                        pickaxeSlotUnlockModal = Instantiate(prefab, transform.root);
                        pickaxeSlotUnlockModal.name = "PickaxeSlotUnlockModal";
                        pickaxeSlotUnlockModal.SetActive(false);
                    }
                }
                else
                {
                    pickaxeSlotUnlockModal = modalObj;
                }
            }

            if (pickaxeSlotUnlockModal == null) return;

            var root = pickaxeSlotUnlockModal.transform;

            // ModalPanel 찾기
            var modalPanel = FindChildRecursive(root, "ModalPanel");
            if (modalPanel == null) return;

            // Text 컴포넌트 바인딩
            if (pickaxeUnlockSlotIndexText == null)
            {
                var slotIndexTf = FindChildRecursive(modalPanel, "SlotIndexText");
                if (slotIndexTf != null)
                {
                    pickaxeUnlockSlotIndexText = slotIndexTf.GetComponent<TextMeshProUGUI>();
                }
            }

            if (pickaxeUnlockCostText == null)
            {
                var costTf = FindChildRecursive(modalPanel, "CostText");
                if (costTf != null)
                {
                    pickaxeUnlockCostText = costTf.GetComponent<TextMeshProUGUI>();
                }
            }

            if (pickaxeUnlockCurrentCrystalText == null)
            {
                var currentTf = FindChildRecursive(modalPanel, "CurrentCrystalText");
                if (currentTf != null)
                {
                    pickaxeUnlockCurrentCrystalText = currentTf.GetComponent<TextMeshProUGUI>();
                }
            }

            // Button 바인딩
            var buttonPanel = FindChildRecursive(modalPanel, "ButtonPanel");
            if (buttonPanel != null)
            {
                if (pickaxeUnlockCancelButton == null)
                {
                    var cancelTf = FindChildRecursive(buttonPanel, "CancelButton");
                    if (cancelTf != null)
                    {
                        pickaxeUnlockCancelButton = cancelTf.GetComponent<Button>();
                    }
                }

                if (pickaxeUnlockConfirmButton == null)
                {
                    var confirmTf = FindChildRecursive(buttonPanel, "ConfirmButton");
                    if (confirmTf != null)
                    {
                        pickaxeUnlockConfirmButton = confirmTf.GetComponent<Button>();
                    }
                }
            }
        }

        /// <summary>
        /// PickaxeSlotUnlockModal 버튼 이벤트 설정
        /// </summary>
        private void SetupPickaxeSlotUnlockModalButtons()
        {
            if (pickaxeSlotUnlockModal == null) return;

            // 배경 클릭으로 닫기
            var bgButton = pickaxeSlotUnlockModal.GetComponent<Button>();
            if (bgButton != null)
            {
                bgButton.onClick.RemoveAllListeners();
                bgButton.onClick.AddListener(() => ClosePickaxeSlotUnlockModal());
            }

            // ModalPanel 클릭 이벤트 차단
            var modalPanel = pickaxeSlotUnlockModal.transform.Find("ModalPanel");
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
            if (pickaxeUnlockCancelButton != null)
            {
                pickaxeUnlockCancelButton.onClick.RemoveAllListeners();
                pickaxeUnlockCancelButton.onClick.AddListener(() => ClosePickaxeSlotUnlockModal());
            }

            // 확인 버튼
            if (pickaxeUnlockConfirmButton != null)
            {
                pickaxeUnlockConfirmButton.onClick.RemoveAllListeners();
                pickaxeUnlockConfirmButton.onClick.AddListener(() => OnConfirmPickaxeSlotUnlock());
            }
        }

        /// <summary>
        /// 곡괭이 슬롯 해금 모달 오픈
        /// </summary>
        /// <param name="slotIndex">곡괭이 슬롯 인덱스 (0-3)</param>
        /// <param name="currentCrystal">현재 보유 크리스탈</param>
        public void OpenPickaxeSlotUnlockModal(uint slotIndex, uint currentCrystal)
        {
            if (pickaxeSlotUnlockModal == null) return;

            // 슬롯 해금 비용 조회
            if (slotIndex >= PickaxeSlotUnlockCosts.Length)
            {
                Debug.LogError($"Invalid pickaxe slot index: {slotIndex}");
                return;
            }

            uint cost = PickaxeSlotUnlockCosts[slotIndex];

            // 정보 저장
            pendingUnlockSlotIndex = slotIndex;

            // UI 업데이트
            if (pickaxeUnlockSlotIndexText != null)
            {
                pickaxeUnlockSlotIndexText.text = $"슬롯 {slotIndex + 1}";
            }

            if (pickaxeUnlockCostText != null)
            {
                pickaxeUnlockCostText.text = $"필요 크리스탈: {cost}";
            }

            if (pickaxeUnlockCurrentCrystalText != null)
            {
                pickaxeUnlockCurrentCrystalText.text = $"보유: {currentCrystal}";
            }

            // 확인 버튼 활성화/비활성화
            if (pickaxeUnlockConfirmButton != null)
            {
                pickaxeUnlockConfirmButton.interactable = (currentCrystal >= cost);
            }

            pickaxeSlotUnlockModal.SetActive(true);
        }

        /// <summary>
        /// 곡괭이 슬롯 해금 모달 닫기
        /// </summary>
        private void ClosePickaxeSlotUnlockModal()
        {
            if (pickaxeSlotUnlockModal != null)
            {
                pickaxeSlotUnlockModal.SetActive(false);
            }
        }

        /// <summary>
        /// 확인 버튼 클릭 시 서버 요청
        /// </summary>
        private void OnConfirmPickaxeSlotUnlock()
        {
            // 서버로 SlotUnlock 전송
            var request = new SlotUnlock
            {
                SlotIndex = pendingUnlockSlotIndex
            };

            var envelope = new Envelope
            {
                Type = MessageType.SlotUnlock,
                SlotUnlock = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log($"[MiningTabController] SlotUnlockRequest 전송: slot={pendingUnlockSlotIndex}");

            // 모달 닫기
            ClosePickaxeSlotUnlockModal();
        }

        /// <summary>
        /// 서버로부터 SlotUnlockResult 수신 시 처리
        /// </summary>
        /// <param name="result">해금 결과</param>
        public void OnSlotUnlockResult(SlotUnlockResult result)
        {
            if (!result.Success)
            {
                // 실패 처리
                Debug.LogWarning($"Pickaxe slot unlock failed: {result.ErrorCode}");

                string errorMessage = result.ErrorCode switch
                {
                    "SLOT_ALREADY_UNLOCKED" => "이미 해금된 슬롯입니다.",
                    "INVALID_SLOT_INDEX" => "잘못된 슬롯 인덱스입니다.",
                    "INSUFFICIENT_CRYSTAL" => "크리스탈이 부족합니다.",
                    _ => $"해금 실패: {result.ErrorCode}"
                };

                // 에러 메시지 표시 (TODO: 에러 모달 구현)
                Debug.LogError(errorMessage);
                return;
            }

            // 성공 처리
            Debug.Log($"[MiningTabController] 곡괭이 슬롯 해금 완료: slot={result.SlotIndex}, crystal_spent={result.CrystalSpent}");

            // 크리스탈 UI 동기화 (MessageHandler가 자동 처리)
            Debug.Log($"Crystal updated: {result.RemainingCrystal}");

            // PickaxeSlot 정보 갱신
            RefreshPickaxeSlots();
        }

        /// <summary>
        /// 곡괭이 슬롯 정보 갱신
        /// </summary>
        private void RefreshPickaxeSlots()
        {
            // AllSlotsRequest 재요청하여 최신 데이터 가져오기
            var request = new AllSlotsRequest();
            var envelope = new Envelope
            {
                Type = MessageType.AllSlotsRequest,
                AllSlotsRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log("[MiningTabController] AllSlotsRequest 전송 (슬롯 해금 후)");
        }

        /// <summary>
        /// 잠긴 슬롯 클릭 시 해금 모달 오픈
        /// </summary>
        /// <param name="slotIndex">곡괭이 슬롯 인덱스 (0-3)</param>
        /// <param name="currentCrystal">현재 보유 크리스탈</param>
        public void OnLockedPickaxeSlotClicked(uint slotIndex, uint currentCrystal)
        {
            // 슬롯 해금 모달 오픈
            OpenPickaxeSlotUnlockModal(slotIndex, currentCrystal);
        }
    }
}
