using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Infinitepickaxe;
using InfinitePickaxe.Client.Net;
using InfinitePickaxe.Client.Core;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// MiningTabController의 보석 장착 기능 (Partial Class)
    /// </summary>
    public partial class MiningTabController
    {
        [Header("Gem Equip Modal")]
        [SerializeField] private GameObject gemEquipModal;
        [SerializeField] private Transform gemGridContent;
        [SerializeField] private Button gemEquipExpandButton;
        [SerializeField] private Button gemEquipCloseButton;

        [Header("Gem Inventory Item Template")]
        [SerializeField] private GameObject gemInventoryItemTemplate;

        [Header("Gem Action List Modal")]
        [SerializeField] private GameObject gemActionListModal;
        [SerializeField] private Button equipActionButton;
        [SerializeField] private Button synthesisActionButton;
        [SerializeField] private Button conversionActionButton;
        [SerializeField] private Button discardActionButton;
        [SerializeField] private Button cancelActionButton;

        [Header("Gem Discard Modal")]
        [SerializeField] private GameObject gemDiscardModal;
        [SerializeField] private Image gemDiscardIcon;
        [SerializeField] private TextMeshProUGUI gemDiscardNameText;
        [SerializeField] private TextMeshProUGUI gemDiscardRewardText;
        [SerializeField] private Button gemDiscardConfirmButton;
        [SerializeField] private Button gemDiscardCancelButton;

        [Header("Gem Inventory Expand Confirm Modal")]
        [SerializeField] private GameObject gemInventoryExpandConfirmModal;
        [SerializeField] private TextMeshProUGUI expandConfirmCapacityText;
        [SerializeField] private TextMeshProUGUI expandConfirmCostText;
        [SerializeField] private TextMeshProUGUI expandConfirmCurrentCrystalText;
        [SerializeField] private Button expandConfirmButton;
        [SerializeField] private Button expandCancelButton;

        [Header("Gem Inventory Expand Result Modal")]
        [SerializeField] private GameObject gemInventoryExpandResultModal;
        [SerializeField] private TextMeshProUGUI expandResultTitleText;
        [SerializeField] private TextMeshProUGUI expandResultMessageText;
        [SerializeField] private Button expandResultCloseButton;

        // 보석 인벤토리 데이터 (서버로부터 수신)
        private List<GemInfo> gemInventory = new List<GemInfo>();
        private uint gemInventoryCapacity = 48;
        private uint maxGemCapacity = 128;

        // 선택된 보석 정보
        private GemInfo selectedGem = null;
        private uint selectedPickaxeSlotIndex = 0;
        private uint selectedGemSlotIndex = 0;
        private RectTransform selectedGemRectTransform = null;

        // 보석 인벤토리 아이템 풀
        private List<GemInventoryItemView> gemInventoryItemPool = new List<GemInventoryItemView>();

        /// <summary>
        /// 보석 장착 모달 AutoBind 및 초기화
        /// </summary>
        private void AutoBindGemEquipModal()
        {
            if (gemEquipModal == null)
            {
                gemEquipModal = transform.Find("GemEquipModal")?.gameObject;
                if (gemEquipModal == null)
                {
                    Debug.LogWarning("[MiningTabController] GemEquipModal을 찾을 수 없습니다!");
                    return;
                }
            }

            if (gemGridContent == null)
            {
                gemGridContent = gemEquipModal.transform.Find("ModalPanel/GemGridScrollView/GemGridContent");
            }

            if (gemEquipExpandButton == null)
            {
                gemEquipExpandButton = gemEquipModal.transform.Find("ModalPanel/ExpandButton")?.GetComponent<Button>();
            }

            if (gemEquipCloseButton == null)
            {
                gemEquipCloseButton = gemEquipModal.transform.Find("ModalPanel/CloseButton")?.GetComponent<Button>();
            }
        }

        /// <summary>
        /// 보석 액션 리스트 모달 AutoBind
        /// </summary>
        private void AutoBindGemActionListModal()
        {
            if (gemActionListModal == null)
            {
                gemActionListModal = transform.Find("GemActionListModal")?.gameObject;
                if (gemActionListModal == null)
                {
                    Debug.LogWarning("[MiningTabController] GemActionListModal을 찾을 수 없습니다!");
                    return;
                }
            }

            if (equipActionButton == null)
            {
                equipActionButton = gemActionListModal.transform.Find("ModalPanel/ActionButtons/EquipButton")?.GetComponent<Button>();
            }

            if (synthesisActionButton == null)
            {
                synthesisActionButton = gemActionListModal.transform.Find("ModalPanel/ActionButtons/SynthesisButton")?.GetComponent<Button>();
            }

            if (conversionActionButton == null)
            {
                conversionActionButton = gemActionListModal.transform.Find("ModalPanel/ActionButtons/ConversionButton")?.GetComponent<Button>();
            }

            if (discardActionButton == null)
            {
                discardActionButton = gemActionListModal.transform.Find("ModalPanel/ActionButtons/DiscardButton")?.GetComponent<Button>();
            }

            if (cancelActionButton == null)
            {
                cancelActionButton = gemActionListModal.transform.Find("ModalPanel/ActionButtons/CancelButton")?.GetComponent<Button>();
            }
        }

        /// <summary>
        /// 보석 분해 모달 AutoBind
        /// </summary>
        private void AutoBindGemDiscardModal()
        {
            if (gemDiscardModal == null)
            {
                gemDiscardModal = transform.Find("GemDiscardModal")?.gameObject;
                if (gemDiscardModal == null)
                {
                    Debug.LogWarning("[MiningTabController] GemDiscardModal을 찾을 수 없습니다!");
                    return;
                }
            }

            if (gemDiscardIcon == null)
            {
                gemDiscardIcon = gemDiscardModal.transform.Find("ModalPanel/GemInfoPanel/GemIcon")?.GetComponent<Image>();
            }

            if (gemDiscardNameText == null)
            {
                gemDiscardNameText = gemDiscardModal.transform.Find("ModalPanel/GemInfoPanel/GemNameText")?.GetComponent<TextMeshProUGUI>();
            }

            if (gemDiscardRewardText == null)
            {
                gemDiscardRewardText = gemDiscardModal.transform.Find("ModalPanel/RewardText")?.GetComponent<TextMeshProUGUI>();
            }

            if (gemDiscardConfirmButton == null)
            {
                gemDiscardConfirmButton = gemDiscardModal.transform.Find("ModalPanel/ButtonPanel/ConfirmButton")?.GetComponent<Button>();
            }

            if (gemDiscardCancelButton == null)
            {
                gemDiscardCancelButton = gemDiscardModal.transform.Find("ModalPanel/ButtonPanel/CancelButton")?.GetComponent<Button>();
            }
        }

        /// <summary>
        /// 보석 장착 모달 버튼 이벤트 등록
        /// </summary>
        private void SetupGemEquipModalButtons()
        {
            gemEquipExpandButton?.onClick.AddListener(OnExpandInventoryClicked);
            gemEquipCloseButton?.onClick.AddListener(CloseGemEquipModal);

            // 배경 클릭으로 닫기
            var backgroundButton = gemEquipModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGemEquipModal);
            }
        }

        /// <summary>
        /// 보석 액션 리스트 모달 버튼 이벤트 등록
        /// </summary>
        private void SetupGemActionListModalButtons()
        {
            equipActionButton?.onClick.AddListener(OnEquipActionClicked);
            synthesisActionButton?.onClick.AddListener(OnSynthesisActionClicked);
            conversionActionButton?.onClick.AddListener(OnConversionActionClicked);
            discardActionButton?.onClick.AddListener(OnDiscardActionClicked);
            cancelActionButton?.onClick.AddListener(CloseGemActionListModal);

            // 배경 클릭으로 닫기
            var backgroundButton = gemActionListModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGemActionListModal);
            }
        }

        /// <summary>
        /// 보석 분해 모달 버튼 이벤트 등록
        /// </summary>
        private void SetupGemDiscardModalButtons()
        {
            gemDiscardConfirmButton?.onClick.AddListener(OnConfirmGemDiscard);
            gemDiscardCancelButton?.onClick.AddListener(CloseGemDiscardModal);

            // 배경 클릭으로 닫기
            var backgroundButton = gemDiscardModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGemDiscardModal);
            }
        }

        /// <summary>
        /// 보석 인벤토리 확장 확인 모달 AutoBind
        /// </summary>
        private void AutoBindGemInventoryExpandConfirmModal()
        {
            if (gemInventoryExpandConfirmModal == null)
            {
                var modalObj = GameObject.Find("GemInventoryExpandConfirmModal");
                if (modalObj == null)
                {
                    // Resources에서 로드
                    var prefab = Resources.Load<GameObject>("UI/GemInventoryExpandConfirmModal");
                    if (prefab != null)
                    {
                        gemInventoryExpandConfirmModal = Instantiate(prefab, transform.root);
                        gemInventoryExpandConfirmModal.name = "GemInventoryExpandConfirmModal";
                        gemInventoryExpandConfirmModal.SetActive(false);
                    }
                }
                else
                {
                    gemInventoryExpandConfirmModal = modalObj;
                }
            }

            if (gemInventoryExpandConfirmModal == null) return;

            var modalPanel = gemInventoryExpandConfirmModal.transform.Find("ModalPanel");
            if (modalPanel == null) return;

            if (expandConfirmCapacityText == null)
            {
                expandConfirmCapacityText = modalPanel.Find("CapacityText")?.GetComponent<TextMeshProUGUI>();
            }

            if (expandConfirmCostText == null)
            {
                expandConfirmCostText = modalPanel.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            }

            if (expandConfirmCurrentCrystalText == null)
            {
                expandConfirmCurrentCrystalText = modalPanel.Find("CurrentCrystalText")?.GetComponent<TextMeshProUGUI>();
            }

            var buttonPanel = modalPanel.Find("ButtonPanel");
            if (buttonPanel != null)
            {
                if (expandCancelButton == null)
                {
                    expandCancelButton = buttonPanel.Find("CancelButton")?.GetComponent<Button>();
                }

                if (expandConfirmButton == null)
                {
                    expandConfirmButton = buttonPanel.Find("ConfirmButton")?.GetComponent<Button>();
                }
            }
        }

        /// <summary>
        /// 보석 인벤토리 확장 결과 모달 AutoBind
        /// </summary>
        private void AutoBindGemInventoryExpandResultModal()
        {
            if (gemInventoryExpandResultModal == null)
            {
                var modalObj = GameObject.Find("GemInventoryExpandResultModal");
                if (modalObj == null)
                {
                    // Resources에서 로드
                    var prefab = Resources.Load<GameObject>("UI/GemInventoryExpandResultModal");
                    if (prefab != null)
                    {
                        gemInventoryExpandResultModal = Instantiate(prefab, transform.root);
                        gemInventoryExpandResultModal.name = "GemInventoryExpandResultModal";
                        gemInventoryExpandResultModal.SetActive(false);
                    }
                }
                else
                {
                    gemInventoryExpandResultModal = modalObj;
                }
            }

            if (gemInventoryExpandResultModal == null) return;

            var modalPanel = gemInventoryExpandResultModal.transform.Find("ModalPanel");
            if (modalPanel == null) return;

            if (expandResultTitleText == null)
            {
                expandResultTitleText = modalPanel.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            }

            if (expandResultMessageText == null)
            {
                expandResultMessageText = modalPanel.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
            }

            if (expandResultCloseButton == null)
            {
                expandResultCloseButton = modalPanel.Find("CloseButton")?.GetComponent<Button>();
            }
        }

        /// <summary>
        /// 보석 인벤토리 확장 확인 모달 버튼 이벤트 등록
        /// </summary>
        private void SetupGemInventoryExpandConfirmModalButtons()
        {
            if (gemInventoryExpandConfirmModal == null) return;

            // 배경 클릭으로 닫기
            var backgroundButton = gemInventoryExpandConfirmModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGemInventoryExpandConfirmModal);
            }

            // ModalPanel 클릭 이벤트 차단
            var modalPanel = gemInventoryExpandConfirmModal.transform.Find("ModalPanel");
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
            if (expandCancelButton != null)
            {
                expandCancelButton.onClick.RemoveAllListeners();
                expandCancelButton.onClick.AddListener(CloseGemInventoryExpandConfirmModal);
            }

            // 확인 버튼
            if (expandConfirmButton != null)
            {
                expandConfirmButton.onClick.RemoveAllListeners();
                expandConfirmButton.onClick.AddListener(OnConfirmGemInventoryExpand);
            }
        }

        /// <summary>
        /// 보석 인벤토리 확장 결과 모달 버튼 이벤트 등록
        /// </summary>
        private void SetupGemInventoryExpandResultModalButtons()
        {
            if (gemInventoryExpandResultModal == null) return;

            // 배경 클릭으로 닫기
            var backgroundButton = gemInventoryExpandResultModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGemInventoryExpandResultModal);
            }

            // ModalPanel 클릭 이벤트 차단
            var modalPanel = gemInventoryExpandResultModal.transform.Find("ModalPanel");
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

            // 닫기 버튼
            if (expandResultCloseButton != null)
            {
                expandResultCloseButton.onClick.RemoveAllListeners();
                expandResultCloseButton.onClick.AddListener(CloseGemInventoryExpandResultModal);
            }
        }

        /// <summary>
        /// 해금된 보석 슬롯 클릭 시 호출 (PickaxeInfoModal에서)
        /// </summary>
        public void OnUnlockedGemSlotClicked(uint pickaxeSlotIndex, uint gemSlotIndex)
        {
            selectedPickaxeSlotIndex = pickaxeSlotIndex;
            selectedGemSlotIndex = gemSlotIndex;

            // 보석 목록 요청
            RequestGemList();
        }

        /// <summary>
        /// 보석 목록 요청
        /// </summary>
        private void RequestGemList()
        {
            var request = new GemListRequest();
            var envelope = new Envelope
            {
                Type = MessageType.GemListRequest,
                GemListRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 보석 목록 응답 처리
        /// </summary>
        public void OnGemListResponse(GemListResponse response)
        {
            gemInventory = response.Gems.ToList();
            gemInventoryCapacity = response.InventoryCapacity;

            Debug.Log($"[MiningTabController] 보석 목록 수신: {response.TotalGems}개, 용량: {response.InventoryCapacity}");

            // 모달 열기
            OpenGemEquipModal();
        }

        /// <summary>
        /// 보석 장착 모달 열기
        /// </summary>
        private void OpenGemEquipModal()
        {
            if (gemEquipModal == null) return;

            gemEquipModal.SetActive(true);

            // 보석 Grid 갱신
            UpdateGemGrid();
        }

        /// <summary>
        /// 보석 장착 모달 닫기
        /// </summary>
        private void CloseGemEquipModal()
        {
            if (gemEquipModal == null) return;

            gemEquipModal.SetActive(false);

            // 선택 초기화
            selectedGem = null;
            selectedGemRectTransform = null;
        }

        /// <summary>
        /// 보석 Grid 갱신
        /// </summary>
        private void UpdateGemGrid()
        {
            if (gemGridContent == null || gemInventoryItemTemplate == null) return;

            // 기존 아이템 비활성화
            foreach (var item in gemInventoryItemPool)
            {
                item.gameObject.SetActive(false);
            }

            // 보석 정렬 (gem_id 내림차순)
            var sortedGems = gemInventory.OrderByDescending(g => g.GemId).ToList();

            int slotIndex = 0;

            // 보석 표시
            foreach (var gem in sortedGems)
            {
                var slotItem = GetOrCreateGemInventoryItem(slotIndex);
                slotItem.gameObject.SetActive(true);
                slotItem.SetGem(gem, OnGemSlotClicked);
                slotIndex++;
            }

            // 빈 슬롯 표시 (현재 용량까지)
            for (int i = sortedGems.Count; i < gemInventoryCapacity; i++)
            {
                var slotItem = GetOrCreateGemInventoryItem(i);
                slotItem.gameObject.SetActive(true);
                slotItem.SetEmpty();
                slotIndex++;
            }

            // 확장 버튼 표시/숨김 (max_capacity 미달 시 표시)
            if (gemEquipExpandButton != null)
            {
                gemEquipExpandButton.gameObject.SetActive(gemInventoryCapacity < maxGemCapacity);
            }
        }

        /// <summary>
        /// GemInventoryItem 가져오기 또는 생성
        /// </summary>
        private GemInventoryItemView GetOrCreateGemInventoryItem(int index)
        {
            while (gemInventoryItemPool.Count <= index)
            {
                var newItem = Instantiate(gemInventoryItemTemplate, gemGridContent);
                var view = newItem.AddComponent<GemInventoryItemView>();
                gemInventoryItemPool.Add(view);
            }

            return gemInventoryItemPool[index];
        }

        /// <summary>
        /// 보석 슬롯 클릭 이벤트
        /// </summary>
        private void OnGemSlotClicked(GemInfo gem, RectTransform itemRect)
        {
            selectedGem = gem;
            selectedGemRectTransform = itemRect;
            OpenGemActionListModal();
        }

        /// <summary>
        /// 인벤토리 확장 버튼 클릭
        /// </summary>
        private void OnExpandInventoryClicked()
        {
            // 확인 모달 열기
            OpenGemInventoryExpandConfirmModal();
        }

        /// <summary>
        /// 보석 인벤토리 확장 확인 모달 열기
        /// </summary>
        private void OpenGemInventoryExpandConfirmModal()
        {
            if (gemInventoryExpandConfirmModal == null) return;

            // 현재 보유 크리스탈 (MessageHandler에서 가져오기)
            uint currentCrystal = MessageHandler.Instance != null ? (MessageHandler.Instance.LastCrystal ?? 0) : 0;

            // 확장 비용 (메타데이터 또는 하드코딩)
            uint expandCost = 200;

            // 확장 크기
            uint expandSize = 8;

            // UI 업데이트
            if (expandConfirmCapacityText != null)
            {
                expandConfirmCapacityText.text = $"현재 용량: {gemInventoryCapacity} / {maxGemCapacity}";
            }

            if (expandConfirmCostText != null)
            {
                expandConfirmCostText.text = $"필요 크리스탈: {expandCost}";
            }

            if (expandConfirmCurrentCrystalText != null)
            {
                expandConfirmCurrentCrystalText.text = $"보유: {currentCrystal}";
            }

            // 확인 버튼 활성화/비활성화
            if (expandConfirmButton != null)
            {
                expandConfirmButton.interactable = (currentCrystal >= expandCost && gemInventoryCapacity < maxGemCapacity);
            }

            gemInventoryExpandConfirmModal.SetActive(true);
        }

        /// <summary>
        /// 보석 인벤토리 확장 확인 모달 닫기
        /// </summary>
        private void CloseGemInventoryExpandConfirmModal()
        {
            if (gemInventoryExpandConfirmModal != null)
            {
                gemInventoryExpandConfirmModal.SetActive(false);
            }
        }

        /// <summary>
        /// 확인 버튼 클릭 시 서버 요청
        /// </summary>
        private void OnConfirmGemInventoryExpand()
        {
            var request = new GemInventoryExpandRequest();
            var envelope = new Envelope
            {
                Type = MessageType.GemInventoryExpandRequest,
                GemInventoryExpandRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log("[MiningTabController] GemInventoryExpandRequest 전송");

            // 확인 모달 닫기
            CloseGemInventoryExpandConfirmModal();
        }

        /// <summary>
        /// 인벤토리 확장 결과 처리
        /// </summary>
        public void OnGemInventoryExpandResult(GemInventoryExpandResult result)
        {
            if (!result.Success)
            {
                // 실패 모달 표시
                OpenGemInventoryExpandResultModal(false, result.ErrorCode, 0);
                return;
            }

            // 성공 처리
            uint oldCapacity = gemInventoryCapacity;
            gemInventoryCapacity = result.NewCapacity;
            UpdateCrystalUI(result.RemainingCrystal);

            Debug.Log($"[MiningTabController] 인벤토리 확장 완료: {oldCapacity} → {result.NewCapacity}");

            // Grid 갱신
            UpdateGemGrid();

            // 성공 모달 표시
            OpenGemInventoryExpandResultModal(true, "", result.NewCapacity);
        }

        /// <summary>
        /// 보석 인벤토리 확장 결과 모달 열기
        /// </summary>
        /// <param name="success">성공 여부</param>
        /// <param name="errorCode">에러 코드 (실패 시)</param>
        /// <param name="newCapacity">새 용량 (성공 시)</param>
        private void OpenGemInventoryExpandResultModal(bool success, string errorCode, uint newCapacity)
        {
            if (gemInventoryExpandResultModal == null) return;

            if (success)
            {
                // 성공 메시지
                if (expandResultTitleText != null)
                {
                    expandResultTitleText.text = "확장 성공";
                    expandResultTitleText.color = new Color(0.5f, 1f, 0.5f, 1f); // 녹색
                }

                if (expandResultMessageText != null)
                {
                    uint oldCapacity = gemInventoryCapacity - 8; // 확장 크기 8
                    expandResultMessageText.text = $"보석 가방이 {oldCapacity}에서 {newCapacity}로 확장되었습니다!";
                    expandResultMessageText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                }
            }
            else
            {
                // 실패 메시지
                if (expandResultTitleText != null)
                {
                    expandResultTitleText.text = "확장 실패";
                    expandResultTitleText.color = new Color(1f, 0.5f, 0.5f, 1f); // 빨간색
                }

                if (expandResultMessageText != null)
                {
                    string message = errorCode switch
                    {
                        "MAX_CAPACITY" => $"최대 용량에 도달했습니다. ({maxGemCapacity}/{maxGemCapacity})",
                        "INSUFFICIENT_CRYSTAL" => "크리스탈이 부족합니다. (필요: 200)",
                        _ => $"확장 실패: {errorCode}"
                    };
                    expandResultMessageText.text = message;
                    expandResultMessageText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                }
            }

            gemInventoryExpandResultModal.SetActive(true);
        }

        /// <summary>
        /// 보석 인벤토리 확장 결과 모달 닫기
        /// </summary>
        private void CloseGemInventoryExpandResultModal()
        {
            if (gemInventoryExpandResultModal != null)
            {
                gemInventoryExpandResultModal.SetActive(false);
            }
        }

        /// <summary>
        /// 보석 액션 리스트 모달 열기 (클릭한 보석 위치에 표시)
        /// </summary>
        private void OpenGemActionListModal()
        {
            if (gemActionListModal == null || selectedGem == null) return;

            gemActionListModal.SetActive(true);

            // 모달 위치 조정 (선택된 보석 위치 기준)
            if (selectedGemRectTransform != null)
            {
                PositionModalNearGem(gemActionListModal.GetComponent<RectTransform>(), selectedGemRectTransform);
            }
        }

        /// <summary>
        /// 모달을 보석 위치 근처에 배치 (캔버스 경계 체크)
        /// </summary>
        private void PositionModalNearGem(RectTransform modalRect, RectTransform gemRect)
        {
            if (modalRect == null || gemRect == null) return;

            // 캔버스 찾기
            Canvas canvas = modalRect.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            // 보석 아이템의 월드 위치를 캔버스 로컬 위치로 변환
            Vector2 gemWorldPos = gemRect.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                gemWorldPos,
                canvas.worldCamera,
                out Vector2 gemLocalPos
            );

            // 모달 크기
            Vector2 modalSize = modalRect.sizeDelta;

            // 초기 위치: 보석 우측에 배치
            Vector2 targetPos = gemLocalPos + new Vector2(gemRect.rect.width / 2 + 20, 0);

            // 캔버스 경계 체크
            Rect canvasBounds = canvasRect.rect;

            // 우측 경계 체크
            if (targetPos.x + modalSize.x / 2 > canvasBounds.xMax)
            {
                // 보석 좌측에 배치
                targetPos.x = gemLocalPos.x - gemRect.rect.width / 2 - modalSize.x - 20;
            }

            // 좌측 경계 체크
            if (targetPos.x - modalSize.x / 2 < canvasBounds.xMin)
            {
                targetPos.x = canvasBounds.xMin + modalSize.x / 2 + 10;
            }

            // 상단 경계 체크
            if (targetPos.y + modalSize.y / 2 > canvasBounds.yMax)
            {
                targetPos.y = canvasBounds.yMax - modalSize.y / 2 - 10;
            }

            // 하단 경계 체크
            if (targetPos.y - modalSize.y / 2 < canvasBounds.yMin)
            {
                targetPos.y = canvasBounds.yMin + modalSize.y / 2 + 10;
            }

            modalRect.anchoredPosition = targetPos;
        }

        /// <summary>
        /// 보석 액션 리스트 모달 닫기
        /// </summary>
        private void CloseGemActionListModal()
        {
            if (gemActionListModal == null) return;

            gemActionListModal.SetActive(false);
        }

        /// <summary>
        /// 장착 액션 클릭
        /// </summary>
        private void OnEquipActionClicked()
        {
            if (selectedGem == null) return;

            var request = new GemEquipRequest
            {
                PickaxeSlotIndex = selectedPickaxeSlotIndex,
                GemSlotIndex = selectedGemSlotIndex,
                GemInstanceId = selectedGem.GemInstanceId
            };

            var envelope = new Envelope
            {
                Type = MessageType.GemEquipRequest,
                GemEquipRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);

            CloseGemActionListModal();
            CloseGemEquipModal();
        }

        /// <summary>
        /// 합성 액션 클릭
        /// </summary>
        private void OnSynthesisActionClicked()
        {
            // TODO: 강화 탭 → 보석 사이드 탭으로 이동
            Debug.Log("[MiningTabController] 합성 탭으로 이동 (구현 예정)");
            CloseGemActionListModal();
            CloseGemEquipModal();
        }

        /// <summary>
        /// 전환 액션 클릭
        /// </summary>
        private void OnConversionActionClicked()
        {
            // TODO: 강화 탭 → 보석 사이드 탭으로 이동
            Debug.Log("[MiningTabController] 전환 탭으로 이동 (구현 예정)");
            CloseGemActionListModal();
            CloseGemEquipModal();
        }

        /// <summary>
        /// 분해 액션 클릭
        /// </summary>
        private void OnDiscardActionClicked()
        {
            if (selectedGem == null) return;

            OpenGemDiscardModal();
            CloseGemActionListModal();
        }

        /// <summary>
        /// 보석 분해 모달 열기
        /// </summary>
        private void OpenGemDiscardModal()
        {
            if (gemDiscardModal == null || selectedGem == null) return;

            // 보석 정보 표시
            gemDiscardNameText.text = selectedGem.Name;

            // 분해 보상 계산 (메타데이터 기반)
            uint reward = GetDiscardReward(selectedGem.Grade);
            gemDiscardRewardText.text = $"분해 보상: {reward} 크리스탈";

            // 아이콘 설정 (TODO: 실제 스프라이트 로드)
            // gemDiscardIcon.sprite = ...

            gemDiscardModal.SetActive(true);
        }

        /// <summary>
        /// 보석 분해 모달 닫기
        /// </summary>
        private void CloseGemDiscardModal()
        {
            if (gemDiscardModal == null) return;

            gemDiscardModal.SetActive(false);
        }

        /// <summary>
        /// 보석 분해 확인
        /// </summary>
        private void OnConfirmGemDiscard()
        {
            if (selectedGem == null) return;

            var request = new GemDiscardRequest();
            request.GemInstanceIds.Add(selectedGem.GemInstanceId);

            var envelope = new Envelope
            {
                Type = MessageType.GemDiscardRequest,
                GemDiscardRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);

            CloseGemDiscardModal();
            CloseGemEquipModal();
        }

        /// <summary>
        /// 보석 장착 결과 처리
        /// </summary>
        public void OnGemEquipResult(GemEquipResult result)
        {
            if (!result.Success)
            {
                string errorMessage = result.ErrorCode switch
                {
                    "SLOT_NOT_UNLOCKED" => "슬롯이 해금되지 않았습니다.",
                    "GEM_NOT_FOUND" => "보석을 찾을 수 없습니다.",
                    _ => $"장착 실패: {result.ErrorCode}"
                };
                Debug.LogError(errorMessage);
                return;
            }

            Debug.Log($"[MiningTabController] 보석 장착 완료: {result.EquippedGem.Name}");

            // UI 갱신
            RefreshPickaxeInfoGemSlots();
        }

        /// <summary>
        /// 보석 분해 결과 처리
        /// </summary>
        public void OnGemDiscardResult(GemDiscardResult result)
        {
            if (!result.Success)
            {
                Debug.LogError($"분해 실패: {result.ErrorCode}");
                return;
            }

            Debug.Log($"[MiningTabController] 보석 분해 완료: {result.CrystalEarned} 크리스탈 획득");

            UpdateCrystalUI(result.TotalCrystal);

            // 인벤토리에서 제거
            gemInventory.RemoveAll(g => g.GemInstanceId == selectedGem.GemInstanceId);
            selectedGem = null;
        }

        /// <summary>
        /// 분해 보상 계산 (메타데이터 기반)
        /// </summary>
        private uint GetDiscardReward(GemGrade grade)
        {
            // gem_discard.json 데이터
            return grade switch
            {
                GemGrade.Common => 5,
                GemGrade.Rare => 15,
                GemGrade.Epic => 50,
                GemGrade.Hero => 150,
                GemGrade.Legendary => 500,
                _ => 0
            };
        }
    }

    /// <summary>
    /// 보석 인벤토리 아이템 뷰
    /// </summary>
    public class GemInventoryItemView : MonoBehaviour
    {
        private Image gradeBorder;
        private Image gemIcon;
        private GameObject emptyState;
        private Button button;

        private GemInfo gemData;
        private System.Action<GemInfo, RectTransform> onClickCallback;

        private void Awake()
        {
            AutoBind();
        }

        private void AutoBind()
        {
            gradeBorder = transform.Find("GradeBorder")?.GetComponent<Image>();
            gemIcon = transform.Find("GemIcon")?.GetComponent<Image>();
            emptyState = transform.Find("EmptyState")?.gameObject;

            button = GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            button.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// 보석 데이터 설정
        /// </summary>
        public void SetGem(GemInfo gem, System.Action<GemInfo, RectTransform> onClick)
        {
            gemData = gem;
            onClickCallback = onClick;

            // 등급별 테두리 색상 설정
            if (gradeBorder != null)
            {
                gradeBorder.color = GetGradeColor(gem.Grade);
            }

            // 아이콘 설정 (TODO: 실제 스프라이트 로드)
            if (gemIcon != null)
            {
                gemIcon.gameObject.SetActive(true);
                // gemIcon.sprite = Resources.Load<Sprite>($"Gems/{gem.Icon}");
            }

            if (emptyState != null)
            {
                emptyState.SetActive(false);
            }
        }

        /// <summary>
        /// 빈 슬롯 설정
        /// </summary>
        public void SetEmpty()
        {
            gemData = null;
            onClickCallback = null;

            if (gradeBorder != null)
            {
                gradeBorder.color = Color.gray;
            }

            if (gemIcon != null)
            {
                gemIcon.gameObject.SetActive(false);
            }

            if (emptyState != null)
            {
                emptyState.SetActive(true);
            }
        }

        private void OnClicked()
        {
            if (gemData != null)
            {
                onClickCallback?.Invoke(gemData, GetComponent<RectTransform>());
            }
        }

        /// <summary>
        /// 등급별 색상 반환
        /// </summary>
        private Color GetGradeColor(GemGrade grade)
        {
            return grade switch
            {
                GemGrade.Common => Color.white,          // 흰색
                GemGrade.Rare => Color.green,            // 녹색
                GemGrade.Epic => Color.blue,             // 파란색
                GemGrade.Hero => new Color(0.6f, 0.4f, 0.7f),  // 보라색
                GemGrade.Legendary => Color.yellow,      // 노란색
                _ => Color.gray
            };
        }
    }
}
