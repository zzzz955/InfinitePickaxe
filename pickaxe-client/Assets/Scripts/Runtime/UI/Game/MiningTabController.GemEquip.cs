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
        [SerializeField] private Button gemEquipUnequipButton;  // 장착 해제 버튼
        [SerializeField] private GameObject currentGemPanel;     // 현재 장착된 보석 패널
        [SerializeField] private Image currentGemIcon;
        [SerializeField] private Image currentGemGradeBorder;

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

        [Header("Gem Reequip Confirm Modal")]
        [SerializeField] private GameObject gemReequipConfirmModal;
        [SerializeField] private TextMeshProUGUI reequipConfirmMessageText;
        // 기존 보석 (현재 장착 중)
        [SerializeField] private Image oldGemIcon;
        [SerializeField] private Image oldGemGradeBorder;
        [SerializeField] private TextMeshProUGUI oldGemNameText;
        [SerializeField] private TextMeshProUGUI oldGemTypeText;
        [SerializeField] private TextMeshProUGUI oldGemStatText;
        [SerializeField] private TextMeshProUGUI oldGemLocationText;
        // 새 보석 (장착하려는)
        [SerializeField] private Image newGemIcon;
        [SerializeField] private Image newGemGradeBorder;
        [SerializeField] private TextMeshProUGUI newGemNameText;
        [SerializeField] private TextMeshProUGUI newGemTypeText;
        [SerializeField] private TextMeshProUGUI newGemStatText;
        // 버튼
        [SerializeField] private Button reequipConfirmButton;
        [SerializeField] private Button reequipCancelButton;

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

            if (gemEquipUnequipButton == null)
            {
                gemEquipUnequipButton = gemEquipModal.transform.Find("ModalPanel/UnequipButton")?.GetComponent<Button>();
            }

            // 현재 장착 보석 패널
            if (currentGemPanel == null)
            {
                currentGemPanel = gemEquipModal.transform.Find("ModalPanel/CurrentGemPanel")?.gameObject;
            }

            if (currentGemPanel != null)
            {
                if (currentGemGradeBorder == null)
                {
                    currentGemGradeBorder = currentGemPanel.transform.Find("GradeBorder")?.GetComponent<Image>();
                }

                if (currentGemIcon == null)
                {
                    currentGemIcon = currentGemPanel.transform.Find("GradeBorder/GemIcon")?.GetComponent<Image>();
                }
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
            gemEquipUnequipButton?.onClick.AddListener(OnGemUnequipButtonClicked);

            var modalPanel = gemEquipModal != null ? gemEquipModal.transform.Find("ModalPanel") : null;
            if (modalPanel != null)
            {
                var panelButton = modalPanel.GetComponent<Button>();
                if (panelButton == null)
                {
                    panelButton = modalPanel.gameObject.AddComponent<Button>();
                    panelButton.transition = Selectable.Transition.None;
                }
                panelButton.onClick.RemoveAllListeners();
            }

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
        /// 재장착 확인 모달 AutoBind
        /// </summary>
        private void AutoBindGemReequipConfirmModal()
        {
            if (gemReequipConfirmModal == null)
            {
                var modalObj = GameObject.Find("GemReequipConfirmModal");
                if (modalObj == null)
                {
                    // Resources에서 로드
                    var prefab = Resources.Load<GameObject>("UI/GemReequipConfirmModal");
                    if (prefab != null)
                    {
                        gemReequipConfirmModal = Instantiate(prefab, transform.root);
                        gemReequipConfirmModal.name = "GemReequipConfirmModal";
                        gemReequipConfirmModal.SetActive(false);
                    }
                }
                else
                {
                    gemReequipConfirmModal = modalObj;
                }
            }

            if (gemReequipConfirmModal == null) return;

            var modalPanel = gemReequipConfirmModal.transform.Find("ModalPanel");
            if (modalPanel == null) return;

            if (reequipConfirmMessageText == null)
            {
                reequipConfirmMessageText = modalPanel.Find("MessageText")?.GetComponent<TextMeshProUGUI>();
            }

            // 기존 보석 (Old Gem)
            var oldGemPanel = modalPanel.Find("OldGemPanel");
            if (oldGemPanel != null)
            {
                if (oldGemIcon == null)
                {
                    oldGemIcon = oldGemPanel.Find("GemIcon")?.GetComponent<Image>();
                }

                if (oldGemGradeBorder == null)
                {
                    oldGemGradeBorder = oldGemPanel.Find("GradeBorder")?.GetComponent<Image>();
                }

                if (oldGemNameText == null)
                {
                    oldGemNameText = oldGemPanel.Find("GemNameText")?.GetComponent<TextMeshProUGUI>();
                }

                if (oldGemTypeText == null)
                {
                    oldGemTypeText = oldGemPanel.Find("GemTypeText")?.GetComponent<TextMeshProUGUI>();
                }

                if (oldGemStatText == null)
                {
                    oldGemStatText = oldGemPanel.Find("GemStatText")?.GetComponent<TextMeshProUGUI>();
                }

                if (oldGemLocationText == null)
                {
                    oldGemLocationText = oldGemPanel.Find("LocationText")?.GetComponent<TextMeshProUGUI>();
                }
            }

            // 새 보석 (New Gem)
            var newGemPanel = modalPanel.Find("NewGemPanel");
            if (newGemPanel != null)
            {
                if (newGemIcon == null)
                {
                    newGemIcon = newGemPanel.Find("GemIcon")?.GetComponent<Image>();
                }

                if (newGemGradeBorder == null)
                {
                    newGemGradeBorder = newGemPanel.Find("GradeBorder")?.GetComponent<Image>();
                }

                if (newGemNameText == null)
                {
                    newGemNameText = newGemPanel.Find("GemNameText")?.GetComponent<TextMeshProUGUI>();
                }

                if (newGemTypeText == null)
                {
                    newGemTypeText = newGemPanel.Find("GemTypeText")?.GetComponent<TextMeshProUGUI>();
                }

                if (newGemStatText == null)
                {
                    newGemStatText = newGemPanel.Find("GemStatText")?.GetComponent<TextMeshProUGUI>();
                }
            }

            var buttonPanel = modalPanel.Find("ButtonPanel");
            if (buttonPanel != null)
            {
                if (reequipCancelButton == null)
                {
                    reequipCancelButton = buttonPanel.Find("CancelButton")?.GetComponent<Button>();
                }

                if (reequipConfirmButton == null)
                {
                    reequipConfirmButton = buttonPanel.Find("ConfirmButton")?.GetComponent<Button>();
                }
            }
        }

        /// <summary>
        /// 재장착 확인 모달 버튼 이벤트 등록
        /// </summary>
        private void SetupGemReequipConfirmModalButtons()
        {
            if (gemReequipConfirmModal == null) return;

            // 배경 클릭으로 닫기
            var backgroundButton = gemReequipConfirmModal.GetComponent<Button>();
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseGemReequipConfirmModal);
            }

            // ModalPanel 클릭 이벤트 차단
            var modalPanel = gemReequipConfirmModal.transform.Find("ModalPanel");
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
            if (reequipCancelButton != null)
            {
                reequipCancelButton.onClick.RemoveAllListeners();
                reequipCancelButton.onClick.AddListener(CloseGemReequipConfirmModal);
            }

            // 확인 버튼
            if (reequipConfirmButton != null)
            {
                reequipConfirmButton.onClick.RemoveAllListeners();
                reequipConfirmButton.onClick.AddListener(OnConfirmGemReequip);
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
        /// 장착된 보석 클릭 시 호출 (PickaxeInfoModal에서)
        /// </summary>
        public void OnEquippedGemClicked(uint pickaxeSlotIndex, uint gemSlotIndex, GemInfo gem)
        {
            Debug.Log($"[MiningTabController] OnEquippedGemClicked: pickaxe={pickaxeSlotIndex}, gemSlot={gemSlotIndex}, gem={gem.GemInstanceId}");

            selectedGem = gem;
            selectedPickaxeSlotIndex = pickaxeSlotIndex;
            selectedGemSlotIndex = gemSlotIndex;

            // GemEquipModal을 열고 CurrentGemPanel 활성화
            RequestGemList();
        }

        /// <summary>
        /// 보석 해제 확인 모달 열기
        /// </summary>
        private void OpenGemUnequipConfirmModal()
        {
            if (selectedGem == null) return;

            // TODO: 전용 해제 확인 UI 구현 필요
            // 임시로 바로 해제 요청 전송 (추후 확인 모달 추가)
            Debug.Log($"[MiningTabController] 보석 해제 요청: {selectedGem.Name}");
            RequestGemUnequip();
        }

        /// <summary>
        /// 보석 해제 요청
        /// </summary>
        private void RequestGemUnequip()
        {
            if (selectedGem == null) return;

            var request = new GemUnequipRequest
            {
                PickaxeSlotIndex = selectedPickaxeSlotIndex,
                GemSlotIndex = selectedGemSlotIndex
            };

            var envelope = new Envelope
            {
                Type = MessageType.GemUnequipRequest,
                GemUnequipRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log($"[MiningTabController] GemUnequipRequest 전송: pickaxe={selectedPickaxeSlotIndex}, gemSlot={selectedGemSlotIndex}");

            // 서버 응답 후 UI는 OnGemUnequipResult에서 자동 갱신됨
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

            // 현재 장착된 보석 정보 표시
            UpdateCurrentGemDisplay();

            // 장착 해제 버튼 활성화/비활성화 (현재 슬롯에 보석이 장착되어 있을 때만 활성화)
            if (gemEquipUnequipButton != null)
            {
                bool hasEquippedGem = IsCurrentSlotOccupied();
                gemEquipUnequipButton.gameObject.SetActive(hasEquippedGem);
            }
        }

        /// <summary>
        /// 현재 장착된 보석 정보 표시 (GemEquipModal)
        /// </summary>
        private void UpdateCurrentGemDisplay()
        {
            var equippedGem = GetCurrentSlotEquippedGem();

            if (currentGemPanel != null)
            {
                currentGemPanel.SetActive(equippedGem != null);
            }

            if (equippedGem == null) return;

            // 등급 테두리 색상
            if (currentGemGradeBorder != null)
            {
                currentGemGradeBorder.color = GetGradeColor(equippedGem.Grade);
            }

            // 아이콘 설정
            if (currentGemIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(equippedGem);
                currentGemIcon.sprite = sprite;
                currentGemIcon.enabled = (sprite != null);
            }
        }

        /// <summary>
        /// 보석 타입 한글 이름 반환
        /// </summary>
        private string GetGemTypeDisplayName(GemType type)
        {
            return type switch
            {
                GemType.AttackSpeed => "공격속도",
                GemType.CritRate => "치명타 확률",
                GemType.CritDmg => "치명타 데미지",
                _ => type.ToString()
            };
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

            // 보석 장착/교체 버튼 텍스트 동적 변경
            UpdateEquipActionButtonText();

            gemActionListModal.SetActive(true);

            // 모달 위치 조정 (선택된 보석 위치 기준)
            if (selectedGemRectTransform != null)
            {
                PositionModalNearGem(gemActionListModal.GetComponent<RectTransform>(), selectedGemRectTransform);
            }
        }

        /// <summary>
        /// 장착 액션 버튼 텍스트 동적 변경 (장착/교체)
        /// </summary>
        private void UpdateEquipActionButtonText()
        {
            if (equipActionButton == null) return;

            var buttonText = equipActionButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText == null) return;

            // 현재 슬롯에 이미 보석이 장착되어 있으면 "교체", 아니면 "장착"
            bool isOccupied = IsCurrentSlotOccupied();
            buttonText.text = isOccupied ? "교체" : "장착";
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

            // 케이스 판단:
            // - 선택된 보석이 이미 다른 슬롯에 장착되어 있는가?
            // - 현재 슬롯에 이미 보석이 장착되어 있는가?

            bool isGemEquipped = IsGemAlreadyEquipped(selectedGem.GemInstanceId);
            bool isCurrentSlotOccupied = IsCurrentSlotOccupied();

            // 케이스 2 또는 케이스 4: 이미 장착된 보석을 선택한 경우
            if (isGemEquipped)
            {
                // 재장착 확인 모달 열기
                CloseGemActionListModal();
                OpenGemReequipConfirmModal();
                return;
            }

            // 케이스 1 또는 케이스 3: 장착되지 않은 보석을 선택한 경우
            // 서버에 장착 요청 전송
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

            // 아이콘 설정
            if (gemDiscardIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(selectedGem);
                gemDiscardIcon.sprite = sprite;
                gemDiscardIcon.enabled = (sprite != null);
            }

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

        /// <summary>
        /// 보석 해제 결과 처리
        /// </summary>
        public void OnGemUnequipResult(GemUnequipResult result)
        {
            if (!result.Success)
            {
                string errorMessage = result.ErrorCode switch
                {
                    "SLOT_NOT_FOUND" => "슬롯을 찾을 수 없습니다.",
                    "NO_GEM_EQUIPPED" => "장착된 보석이 없습니다.",
                    _ => $"해제 실패: {result.ErrorCode}"
                };
                Debug.LogError($"[MiningTabController] {errorMessage}");
                return;
            }

            Debug.Log($"[MiningTabController] 보석 해제 완료: {result.UnequippedGem.Name}");

            // GemEquipModal 닫기
            CloseGemEquipModal();

            // GemGridContent에서 해당 보석의 EquippedLabel 비활성화
            if (result.UnequippedGem != null && !string.IsNullOrWhiteSpace(result.UnequippedGem.GemInstanceId))
            {
                UpdateGemInventoryItemEquippedLabel(result.UnequippedGem.GemInstanceId, false);
            }

            // 곡괭이 정보 갱신 (PickaxeInfoModal이 열려있다면)
            RefreshPickaxeInfoGemSlots();

            // 선택 초기화
            selectedGem = null;
        }

        // ==================== 보석 장착 여부 체크 헬퍼 메서드 ====================

        /// <summary>
        /// 보석이 이미 다른 슬롯에 장착되어 있는지 확인
        /// </summary>
        private bool IsGemAlreadyEquipped(string gemInstanceId)
        {
            var pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache == null) return false;

            foreach (var slotKvp in pickaxeCache.Slots)
            {
                var slot = slotKvp.Value;
                if (slot == null || slot.GemSlots == null) continue;

                foreach (var gemSlot in slot.GemSlots)
                {
                    if (gemSlot.IsUnlocked && gemSlot.EquippedGem != null &&
                        gemSlot.EquippedGem.GemInstanceId == gemInstanceId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 보석이 장착된 위치 반환 (슬롯 인덱스, 보석 슬롯 인덱스)
        /// </summary>
        private (uint pickaxeSlotIndex, uint gemSlotIndex)? GetGemEquippedLocation(string gemInstanceId)
        {
            var pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache == null) return null;

            foreach (var slotKvp in pickaxeCache.Slots)
            {
                var slot = slotKvp.Value;
                if (slot == null || slot.GemSlots == null) continue;

                foreach (var gemSlot in slot.GemSlots)
                {
                    if (gemSlot.IsUnlocked && gemSlot.EquippedGem != null &&
                        gemSlot.EquippedGem.GemInstanceId == gemInstanceId)
                    {
                        return (slot.SlotIndex, gemSlot.GemSlotIndex);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 현재 선택된 슬롯에 이미 보석이 장착되어 있는지 확인
        /// </summary>
        private bool IsCurrentSlotOccupied()
        {
            var pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache == null) return false;

            if (!pickaxeCache.TryGetSlot(selectedPickaxeSlotIndex, out var slot))
                return false;

            if (slot == null || slot.GemSlots == null)
                return false;

            foreach (var gemSlot in slot.GemSlots)
            {
                if (gemSlot.GemSlotIndex == selectedGemSlotIndex &&
                    gemSlot.IsUnlocked &&
                    gemSlot.EquippedGem != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 현재 선택된 슬롯에 장착된 보석 정보 반환
        /// </summary>
        private GemInfo GetCurrentSlotEquippedGem()
        {
            var pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache == null) return null;

            if (!pickaxeCache.TryGetSlot(selectedPickaxeSlotIndex, out var slot))
                return null;

            if (slot == null || slot.GemSlots == null)
                return null;

            foreach (var gemSlot in slot.GemSlots)
            {
                if (gemSlot.GemSlotIndex == selectedGemSlotIndex &&
                    gemSlot.IsUnlocked &&
                    gemSlot.EquippedGem != null)
                {
                    return gemSlot.EquippedGem;
                }
            }

            return null;
        }

        // ==================== 버튼 클릭 핸들러 ====================

        /// <summary>
        /// 장착 해제 버튼 클릭 (GemEquipModal 내부)
        /// </summary>
        private void OnGemUnequipButtonClicked()
        {
            // 현재 슬롯에 장착된 보석 가져오기
            var equippedGem = GetCurrentSlotEquippedGem();
            if (equippedGem == null)
            {
                Debug.LogWarning("[MiningTabController] 장착된 보석이 없습니다.");
                return;
            }

            // selectedGem 설정 (해제 확인 모달에서 사용)
            selectedGem = equippedGem;

            // 해제 확인 모달 열기
            OpenGemUnequipConfirmModal();
        }

        // ==================== 재장착 확인 모달 ====================

        /// <summary>
        /// 재장착 확인 모달 열기
        /// </summary>
        private void OpenGemReequipConfirmModal()
        {
            if (gemReequipConfirmModal == null || selectedGem == null) return;

            var location = GetGemEquippedLocation(selectedGem.GemInstanceId);
            if (!location.HasValue)
            {
                Debug.LogWarning("[MiningTabController] 보석이 장착되어 있지 않습니다.");
                return;
            }

            // 메시지 설정
            if (reequipConfirmMessageText != null)
            {
                reequipConfirmMessageText.text = "이 보석을 다른 슬롯에서 해제하고 현재 슬롯에 장착하시겠습니까?";
            }

            // 기존 보석 정보 설정 (현재 장착 중인 보석 = selectedGem)
            SetupOldGemDisplay(selectedGem, location.Value);

            // 새 보석 정보 설정 (장착하려는 위치의 보석, 없을 수도 있음)
            var currentSlotGem = GetCurrentSlotEquippedGem();
            SetupNewGemDisplay(currentSlotGem);

            gemReequipConfirmModal.SetActive(true);
        }

        /// <summary>
        /// 기존 보석 정보 표시 (현재 장착 중)
        /// </summary>
        private void SetupOldGemDisplay(GemInfo gem, (uint pickaxeSlotIndex, uint gemSlotIndex) location)
        {
            if (gem == null) return;

            // 아이콘
            if (oldGemIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(gem);
                oldGemIcon.sprite = sprite;
                oldGemIcon.enabled = (sprite != null);
            }

            // 등급 테두리
            if (oldGemGradeBorder != null)
            {
                oldGemGradeBorder.color = GetGradeColor(gem.Grade);
            }

            // 이름
            if (oldGemNameText != null)
            {
                oldGemNameText.text = gem.Name;
            }

            // 타입
            if (oldGemTypeText != null)
            {
                oldGemTypeText.text = GetGemTypeDisplayName(gem.Type);
            }

            // 스탯
            if (oldGemStatText != null)
            {
                oldGemStatText.text = $"+{gem.StatMultiplier / 100f:F1}%";
            }

            // 현재 위치
            if (oldGemLocationText != null)
            {
                oldGemLocationText.text = $"곡괭이 슬롯 {location.pickaxeSlotIndex + 1} - 보석 슬롯 {location.gemSlotIndex + 1}";
            }
        }

        /// <summary>
        /// 새 보석 정보 표시 (장착하려는 위치에 이미 있는 보석, nullable)
        /// </summary>
        private void SetupNewGemDisplay(GemInfo gem)
        {
            if (gem == null)
            {
                // 빈 슬롯 표시
                if (newGemIcon != null)
                {
                    newGemIcon.enabled = false;
                }

                if (newGemGradeBorder != null)
                {
                    newGemGradeBorder.color = Color.gray;
                }

                if (newGemNameText != null)
                {
                    newGemNameText.text = "빈 슬롯";
                }

                if (newGemTypeText != null)
                {
                    newGemTypeText.text = "-";
                }

                if (newGemStatText != null)
                {
                    newGemStatText.text = "-";
                }

                return;
            }

            // 아이콘
            if (newGemIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(gem);
                newGemIcon.sprite = sprite;
                newGemIcon.enabled = (sprite != null);
            }

            // 등급 테두리
            if (newGemGradeBorder != null)
            {
                newGemGradeBorder.color = GetGradeColor(gem.Grade);
            }

            // 이름
            if (newGemNameText != null)
            {
                newGemNameText.text = gem.Name;
            }

            // 타입
            if (newGemTypeText != null)
            {
                newGemTypeText.text = GetGemTypeDisplayName(gem.Type);
            }

            // 스탯
            if (newGemStatText != null)
            {
                // 비교: 새 보석과 기존 보석의 스탯 차이 표시
                float oldStat = selectedGem != null ? selectedGem.StatMultiplier / 100f : 0f;
                float newStat = gem.StatMultiplier / 100f;
                float diff = newStat - oldStat;

                string statText = $"+{newStat:F1}%";
                if (diff > 0)
                {
                    statText += $" <color=#00FF00>(+{diff:F1}%)</color>";
                }
                else if (diff < 0)
                {
                    statText += $" <color=#FF0000>({diff:F1}%)</color>";
                }

                newGemStatText.text = statText;
            }
        }

        /// <summary>
        /// 재장착 확인 모달 닫기
        /// </summary>
        private void CloseGemReequipConfirmModal()
        {
            if (gemReequipConfirmModal != null)
            {
                gemReequipConfirmModal.SetActive(false);
            }
        }

        /// <summary>
        /// 재장착 확인 버튼 클릭
        /// </summary>
        private void OnConfirmGemReequip()
        {
            if (selectedGem == null) return;

            // 서버에 장착 요청 전송 (서버가 자동으로 이전 슬롯에서 해제함)
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

            CloseGemReequipConfirmModal();
            CloseGemActionListModal();
            CloseGemEquipModal();
        }

        /// <summary>
        /// GemGridContent에서 특정 보석의 EquippedLabel 활성화/비활성화
        /// </summary>
        private void UpdateGemInventoryItemEquippedLabel(string gemInstanceId, bool isEquipped)
        {
            if (string.IsNullOrWhiteSpace(gemInstanceId)) return;

            // gemInventoryItemPool에서 해당 보석 찾기
            foreach (var itemView in gemInventoryItemPool)
            {
                if (itemView == null) continue;

                // GemInventoryItemView의 내부 상태 확인 (public 프로퍼티나 메서드 필요)
                // 현재는 private이므로 리플렉션이나 public 메서드 추가 필요
                // 임시로 GemStateCache를 통해 갱신
            }

            // GemStateCache를 통해 전체 인벤토리 갱신
            // 이미 GemStateCache.Instance.ApplyUnequipResult()가 호출되어 상태가 업데이트되었으므로
            // UI만 갱신하면 됨
            if (gemEquipModal != null && gemEquipModal.activeSelf)
            {
                UpdateGemGrid();
            }
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
        private Image equippedLabel;  // 장착됨 라벨 이미지
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
            equippedLabel = transform.Find("EquippedLabel")?.GetComponent<Image>();

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

            // 아이콘 설정
            if (gemIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(gem);
                gemIcon.sprite = sprite;
                gemIcon.enabled = (sprite != null);
                gemIcon.gameObject.SetActive(sprite != null);
            }

            if (emptyState != null)
            {
                emptyState.SetActive(false);
            }

            // 장착 여부 확인하여 라벨 표시
            if (equippedLabel != null)
            {
                bool isEquipped = InfinitePickaxe.Client.Core.GemStateCache.Instance.IsEquipped(gem.GemInstanceId);
                equippedLabel.gameObject.SetActive(isEquipped);
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

            // 장착 라벨 숨김
            if (equippedLabel != null)
            {
                equippedLabel.gameObject.SetActive(false);
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
