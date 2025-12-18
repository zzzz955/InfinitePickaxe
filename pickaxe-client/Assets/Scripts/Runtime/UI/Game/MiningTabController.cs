using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;
using System.Collections.Generic;
using InfinitePickaxe.Client.Core;

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
        [SerializeField] private Image mineHPSliderFill;
        [SerializeField] private Image mineHPSliderBackground;
        [SerializeField] private TextMeshProUGUI mineHPText;
        [SerializeField] private TextMeshProUGUI dpsText;
        [SerializeField] private Button selectMineralButton;

        [Header("Mineral Select Items")]
        [SerializeField] private Button mineralItemNullButton;
        [SerializeField] private Button mineralItem1Button;
        [SerializeField] private Button mineralItem2Button;
        [SerializeField] private Button mineralItem3Button;
        [SerializeField] private Button mineralItem4Button;
        [SerializeField] private Button mineralItem5Button;
        [SerializeField] private Button mineralItem6Button;
        [SerializeField] private Button mineralItem7Button;

        [Header("Pickaxe Slot References")]
        [SerializeField] private Button pickaxeSlot1Button;
        [SerializeField] private Button pickaxeSlot2Button;
        [SerializeField] private Button pickaxeSlot3Button;
        [SerializeField] private Button pickaxeSlot4Button;
        [SerializeField] private TextMeshProUGUI slot1LevelText;
        [SerializeField] private TextMeshProUGUI slot2LevelText;
        [SerializeField] private TextMeshProUGUI slot3LevelText;
        [SerializeField] private TextMeshProUGUI slot4LevelText;

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

        // 채굴 중단 요청 시 서버와 합의한 sentinel ID (0은 중단, 실제 광물 ID는 1부터 시작)
        private const uint StopMineralId = 0;

        // 런타임 기본 스프라이트 캐시 (빈 스프라이트일 때 fillAmount가 먹지 않는 문제 회피)
        private static Sprite runtimeDefaultSprite;

        // 메타 기반 광물 이름 테이블 (인덱스=ID). 필요 시 인스펙터에서 교체/확장 가능.
        [SerializeField] private string[] mineralNames = new[]
        {
            "채굴 중단",   // 0
            "약한 돌",     // 1
            "돌",         // 2
            "석탄",       // 3
            "구리",       // 4
            "철광석",     // 5
            "금광석",     // 6
            "에메랄드"    // 7
        };

        private bool hasServerState = false;

        private GameTabManager tabManager;
        private MessageHandler messageHandler;
        private bool isRespawning = false;
        private bool hpLayoutFixed = false;
        private bool cacheSubscribed = false;
        [SerializeField] private float hpSliderDefaultWidth = 800f;
        [SerializeField] private float hpSliderDefaultHeight = 50f;
        private readonly Dictionary<uint, PickaxeSlotInfo> slotInfos = new Dictionary<uint, PickaxeSlotInfo>();
        private PickaxeStateCache pickaxeCache;

        [Header("HP Bar Animation")]
        [SerializeField] private float fillLerpSpeed = 6f;
        [SerializeField] private float colorLerpSpeed = 4f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulsePeriodSeconds = 0f; // 0이면 pulseSpeed 사용, 양수면 주기(초) 기반
        [SerializeField] private float pulseAmplitude = 0.08f;
        [SerializeField] private Color lowColor = Color.red;
        [SerializeField] private Color midColor = Color.yellow;
        [SerializeField] private Color highColor = Color.green;

        private float targetFillNormalized = 1f;
        private float displayedFillNormalized = 1f;
        private float safeMaxForDisplay = 1f;
        private Color currentFillColor = Color.green;

        protected override void Initialize()
        {
            base.Initialize();

            // 초기값을 채굴 중단 상태로 설정해 fallback 노출을 피함
            currentMineralName = GetMineralName(StopMineralId);
            currentHP = 0;
            maxHP = 0;

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

            // MineralSelectModal 아이템 버튼 이벤트 등록 (null=채굴 중단, 0~6=광물 선택)
            BindMineralSelectButtons();

            // 슬롯 버튼 이벤트 등록
            if (pickaxeSlot1Button != null)
            {
                pickaxeSlot1Button.onClick.AddListener(() => OnPickaxeSlotClicked(0));
            }
            if (pickaxeSlot2Button != null)
            {
                pickaxeSlot2Button.onClick.AddListener(() => OnPickaxeSlotClicked(1));
            }
            if (pickaxeSlot3Button != null)
            {
                pickaxeSlot3Button.onClick.AddListener(() => OnPickaxeSlotClicked(2));
            }
            if (pickaxeSlot4Button != null)
            {
                pickaxeSlot4Button.onClick.AddListener(() => OnPickaxeSlotClicked(3));
            }

            AutoBindLevelTexts();

            // 모달 닫기 버튼 이벤트 등록
            SetupModalCloseButtons();

            // 초기 UI 업데이트
            RefreshData();
        }

        private void OnDestroy()
        {
            // 중복 해제 방지: OnDisable에서도 수행하지만 안전하게 한 번 더 수행
            if (messageHandler != null)
            {
                messageHandler.OnUserDataSnapshot -= HandleUserDataSnapshot;
                messageHandler.OnMiningUpdate -= HandleMiningUpdate;
                messageHandler.OnMiningComplete -= HandleMiningComplete;
                messageHandler.OnChangeMineralResponse -= HandleChangeMineralResponse;
                messageHandler.OnAllSlotsResponse -= HandleAllSlotsResponse;
                messageHandler.OnUpgradeResult -= HandleUpgradeResult;
            }
            UnsubscribeCache();
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
                messageHandler.OnUpgradeResult += HandleUpgradeResult;
            }

            SubscribeCache();
            SyncSlotsFromCache();
            RefreshData();
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
                messageHandler.OnUpgradeResult -= HandleUpgradeResult;
            }

            UnsubscribeCache();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();

            // 탭이 표시될 때마다 데이터 갱신
            RefreshData();
            AutoBindLevelTexts();
        }

        private void SubscribeCache()
        {
            if (cacheSubscribed) return;
            pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache != null)
            {
                pickaxeCache.OnChanged += HandlePickaxeCacheChanged;
                cacheSubscribed = true;
            }
        }

        private void UnsubscribeCache()
        {
            if (!cacheSubscribed || pickaxeCache == null) return;
            pickaxeCache.OnChanged -= HandlePickaxeCacheChanged;
            cacheSubscribed = false;
        }

        private void HandlePickaxeCacheChanged()
        {
            SyncSlotsFromCache();
            RefreshData();
        }

        private void SyncSlotsFromCache()
        {
            if (pickaxeCache == null) return;

            slot2Unlocked = false;
            slot3Unlocked = false;
            slot4Unlocked = false;
            slotInfos.Clear();
            foreach (var kvp in pickaxeCache.Slots)
            {
                if (kvp.Value == null) continue;
                slotInfos[kvp.Key] = kvp.Value;
                switch (kvp.Key)
                {
                    case 1:
                        slot2Unlocked = kvp.Value.IsUnlocked;
                        break;
                    case 2:
                        slot3Unlocked = kvp.Value.IsUnlocked;
                        break;
                    case 3:
                        slot4Unlocked = kvp.Value.IsUnlocked;
                        break;
                }
            }

            currentDPS = pickaxeCache.TotalDps;
        }

        private void AutoBindLevelTexts()
        {
            if (slot1LevelText == null)
                slot1LevelText = FindLevelText("Slot1");
            if (slot2LevelText == null)
                slot2LevelText = FindLevelText("Slot2");
            if (slot3LevelText == null)
                slot3LevelText = FindLevelText("Slot3");
            if (slot4LevelText == null)
                slot4LevelText = FindLevelText("Slot4");
        }

        private TextMeshProUGUI FindLevelText(string slotName)
        {
            var go = GameObject.Find($"{slotName}/LevelText");
            if (go != null)
            {
                return go.GetComponent<TextMeshProUGUI>();
            }

            var slot = GameObject.Find(slotName);
            if (slot != null)
            {
                var t = slot.transform.Find("LevelText");
                if (t != null) return t.GetComponent<TextMeshProUGUI>();
            }

            return null;
        }

        /// <summary>
        /// 채굴 데이터 UI 갱신
        /// </summary>
        public override void RefreshData()
        {
            // targetFillNormalized는 Animate에서 부드럽게 따라감
            // 서버 상태를 받은 뒤에만 렌더링되도록 기본 상태는 채굴 중단을 표시
            UpdateMineInfo();
            UpdateHPBar();
            UpdateDPS();
            UpdateSlotLevels();
        }

        /// <summary>
        /// 광물 정보 텍스트 업데이트
        /// </summary>
        private void UpdateMineInfo()
        {
            if (mineInfoText != null)
            {
                var status = isRespawning ? "(리스폰 중)" : "";
                mineInfoText.text = $"채굴 중: {currentMineralName} {status}";
            }
        }

        /// <summary>
        /// HP 바 및 텍스트 업데이트
        /// </summary>
        private void UpdateHPBar()
        {
            // Fill/Background RectTransform이 0x0이면 슬라이더가 안 보이므로 한 번 강제 리레이아웃
            FixHpSliderLayout();

            safeMaxForDisplay = Mathf.Max(1f, maxHP);
            if (mineHPSlider != null)
            {
                mineHPSlider.minValue = 0f;
                mineHPSlider.maxValue = safeMaxForDisplay;
                mineHPSlider.wholeNumbers = false;
                mineHPSlider.SetValueWithoutNotify(Mathf.Clamp(currentHP, 0f, safeMaxForDisplay));
                // 슬라이더 오브젝트 자체 사이즈가 0이라면 디폴트 크기로 보정
                var rt = mineHPSlider.GetComponent<RectTransform>();
                if (rt != null && (rt.sizeDelta.x <= 0 || rt.sizeDelta.y <= 0))
                {
                    rt.sizeDelta = new Vector2(hpSliderDefaultWidth, hpSliderDefaultHeight);
                }
            }

            targetFillNormalized = safeMaxForDisplay > 0 ? Mathf.Clamp01(currentHP / safeMaxForDisplay) : 0f;
            // displayedFillNormalized는 Animate에서 따라감 (Update에서 실행)

            if (mineHPText != null)
            {
                var hpPercent = maxHP > 0 ? (currentHP / maxHP * 100f) : 0f;
                mineHPText.text = $"HP: {currentHP:F0}/{maxHP:F0} ({hpPercent:F1}%)";

                // 색상 변경: 70%+ 연두, 30~70% 노랑, 0~30% 빨강
                Color target = Color.green;
                if (hpPercent < 30f)
                    target = Color.red;
                else if (hpPercent < 70f)
                    target = Color.yellow;
                mineHPText.color = target;

                if (mineHPSliderFill != null)
                {
                    mineHPSliderFill.color = target;
                }
                if (mineHPSliderBackground != null)
                {
                    mineHPSliderBackground.color = Color.black;
                }
            }
        }

        private void Update()
        {
            AnimateHPBar();
        }

        private void AnimateHPBar()
        {
            // 부드러운 채움 값 보간
            displayedFillNormalized = Mathf.Lerp(displayedFillNormalized, targetFillNormalized, Time.deltaTime * fillLerpSpeed);
            displayedFillNormalized = Mathf.Clamp01(displayedFillNormalized);

            if (mineHPSliderFill != null)
            {
                mineHPSliderFill.fillAmount = displayedFillNormalized;
            }

            // 부드러운 색상 보간 + 펄스
            Color targetColor = EvaluateHPGradient(displayedFillNormalized);
            currentFillColor = Color.Lerp(currentFillColor, targetColor, Time.deltaTime * colorLerpSpeed);

            float omega = pulseSpeed;
            if (pulsePeriodSeconds > 0.0001f)
            {
                float freq = 1f / pulsePeriodSeconds;
                omega = freq * 2f * Mathf.PI;
            }
            float pulse = 1f + Mathf.Sin(Time.time * omega) * pulseAmplitude;
            float clampedPulse = Mathf.Clamp(pulse, 0.25f, 2f);
            Color pulsed = currentFillColor * clampedPulse;
            pulsed.a = currentFillColor.a;

            if (mineHPSliderFill != null)
            {
                mineHPSliderFill.color = pulsed;
            }

            if (mineHPSliderBackground != null)
            {
                // 배경은 펄스 없이 어둡게 고정
                var bgBase = new Color(currentFillColor.r * 0.3f, currentFillColor.g * 0.3f, currentFillColor.b * 0.3f, mineHPSliderBackground.color.a);
                mineHPSliderBackground.color = bgBase;
            }

            if (mineHPText != null)
            {
                mineHPText.color = pulsed;
            }
        }

        private Color EvaluateHPGradient(float normalized)
        {
            // 0~0.5: red -> yellow, 0.5~1: yellow -> green
            if (normalized < 0.5f)
            {
                float t = normalized / 0.5f;
                return Color.Lerp(lowColor, midColor, t);
            }
            else
            {
                float t = (normalized - 0.5f) / 0.5f;
                return Color.Lerp(midColor, highColor, t);
            }
        }

        private void FixHpSliderLayout()
        {
            if (hpLayoutFixed) return;

            // Fill Image도 함께 수정 (위와 같은 오브젝트일 수 있음)
            if (mineHPSliderFill != null)
            {
                // fillAmount 기반으로만 표현되도록 타입 강제
                mineHPSliderFill.type = Image.Type.Filled;
                mineHPSliderFill.fillMethod = Image.FillMethod.Horizontal;
                mineHPSliderFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                mineHPSliderFill.fillCenter = true;
                // Sprite가 비어 있으면 런타임 생성 스프라이트로 설정 (빈 스프라이트는 fillAmount가 적용되지 않음)
                if (mineHPSliderFill.sprite == null)
                {
                    mineHPSliderFill.sprite = GetRuntimeDefaultSprite();
                }
                var rt = mineHPSliderFill.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero; // 앵커 스트레치 모드에서는 sizeDelta를 0으로
            }
            // Slider가 fillRect를 조정하지 않도록 분리 (값 컨테이너 역할만 유지)
            if (mineHPSlider != null)
            {
                mineHPSlider.fillRect = null;

                // 상위 LayoutGroup/ContentSizeFitter가 sizeDelta를 0으로 덮어쓰는 경우를 막기 위해
                // LayoutElement로 선호 크기를 강제한다.
                var layout = mineHPSlider.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = mineHPSlider.gameObject.AddComponent<LayoutElement>();
                }
                layout.preferredWidth = Mathf.Max(1f, hpSliderDefaultWidth);
                layout.preferredHeight = Mathf.Max(1f, hpSliderDefaultHeight);
                layout.minWidth = 0f;
                layout.minHeight = Mathf.Max(1f, hpSliderDefaultHeight);
                layout.flexibleWidth = 0f;
                layout.flexibleHeight = 0f;
            }

            // Background 수정
            if (mineHPSliderBackground != null)
            {
                if (mineHPSliderBackground.sprite == null)
                {
                    mineHPSliderBackground.sprite = GetRuntimeDefaultSprite();
                }
                var rt = mineHPSliderBackground.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero; // 앵커 스트레치 모드에서는 sizeDelta를 0으로
            }

            // Slider 자체 크기 확인
            if (mineHPSlider != null)
            {
                var sliderRt = mineHPSlider.GetComponent<RectTransform>();
                if (sliderRt != null)
                {
                    Debug.Log($"Slider RectTransform: anchorMin={sliderRt.anchorMin}, anchorMax={sliderRt.anchorMax}, sizeDelta={sliderRt.sizeDelta}");
                }
            }

            hpLayoutFixed = true;
        }

        private static Sprite GetRuntimeDefaultSprite()
        {
            if (runtimeDefaultSprite != null) return runtimeDefaultSprite;

            // Texture2D.whiteTexture는 16x16이므로 그대로 사용
            var tex = Texture2D.whiteTexture;
            runtimeDefaultSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            runtimeDefaultSprite.name = "RuntimeWhiteSprite";
            return runtimeDefaultSprite;
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
                0 => true, // 슬롯 0은 항상 해금
                1 => slot2Unlocked,
                2 => slot3Unlocked,
                3 => slot4Unlocked,
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
        /// 광물 선택 모달 내 버튼 바인딩
        /// </summary>
        private void BindMineralSelectButtons()
        {
            if (mineralItemNullButton != null)
            {
                mineralItemNullButton.onClick.AddListener(() => SelectMineralNullable(null));
            }
            if (mineralItem1Button != null)
            {
                mineralItem1Button.onClick.AddListener(() => SelectMineralNullable(1));
            }
            if (mineralItem2Button != null)
            {
                mineralItem2Button.onClick.AddListener(() => SelectMineralNullable(2));
            }
            if (mineralItem3Button != null)
            {
                mineralItem3Button.onClick.AddListener(() => SelectMineralNullable(3));
            }
            if (mineralItem4Button != null)
            {
                mineralItem4Button.onClick.AddListener(() => SelectMineralNullable(4));
            }
            if (mineralItem5Button != null)
            {
                mineralItem5Button.onClick.AddListener(() => SelectMineralNullable(5));
            }
            if (mineralItem6Button != null)
            {
                mineralItem6Button.onClick.AddListener(() => SelectMineralNullable(6));
            }
            if (mineralItem7Button != null)
            {
                mineralItem7Button.onClick.AddListener(() => SelectMineralNullable(7));
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
        /// 광물 선택/중단 요청 (null이면 채굴 중단)
        /// </summary>
        /// <param name="mineralId">실제 광물 ID(1부터), null은 채굴 중단</param>
        private void SelectMineralNullable(int? mineralId)
        {
            // 서버 권한: 메시지 전송만, 클라이언트는 결과 수신 후 렌더링
            try
            {
                var handler = MessageHandler.Instance;
                if (handler == null)
                {
                    Debug.LogError("MessageHandler 인스턴스를 찾을 수 없습니다.");
                    return;
                }

                var envelope = new Envelope
                {
                    Type = MessageType.ChangeMineralRequest
                };

                if (mineralId.HasValue)
                {
                envelope.ChangeMineralRequest = new ChangeMineralRequest
                {
                    MineralId = (uint)mineralId.Value
                };
            }
            else
            {
                // 프로토에 null 표현이 없어 StopMineralId(0)를 채굴 중단 의미로 사용 (서버와 합의된 규약)
                envelope.ChangeMineralRequest = new ChangeMineralRequest
                {
                    MineralId = StopMineralId
                };
            }

                NetworkManager.Instance.SendMessage(envelope);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"광물 선택 요청 실패: {ex.Message}");
            }
            finally
            {
                CloseModal(mineralSelectModal);
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
            hasServerState = true;

            pickaxeCache?.UpdateFromSnapshot(snapshot);
            SyncSlotsFromCache();
            currentDPS = pickaxeCache?.TotalDps ?? currentDPS;

            // 광물 정보 업데이트
            if (snapshot.CurrentMineralId.HasValue)
            {
                uint mineralId = snapshot.CurrentMineralId.Value;
                currentMineralName = GetMineralName(mineralId);
            }

            // HP 정보 업데이트
            if (snapshot.MineralHp != null && snapshot.MineralMaxHp != null)
            {
                currentHP = snapshot.MineralHp.Value;
                maxHP = snapshot.MineralMaxHp.Value;
            }

            // 채굴 중단 상태 감지 (ID 0 또는 HP 0/0)
            if ((snapshot.CurrentMineralId.HasValue && snapshot.CurrentMineralId.Value == StopMineralId) ||
                (maxHP == 0))
            {
                currentMineralName = GetMineralName(StopMineralId);
                currentHP = 0;
                maxHP = 0;
            }

            // DPS 정보 업데이트
            currentDPS = snapshot.TotalDps;

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
            currentMineralName = GetMineralName(update.MineralId);

            // 서버가 채굴 중단 상태라면 HP 0/0으로 유지
            if (update.MaxHp == 0)
            {
                currentMineralName = GetMineralName(StopMineralId);
                currentHP = 0;
                maxHP = 0;
                isRespawning = true;
            }
            else
            {
                isRespawning = false;
            }

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

            UpdateMineInfo();
            UpdateHPBar();
            // UpdateDPS()는 호출하지 않음 (DPS는 슬롯 정보 변경 시에만 업데이트)
        }

        /// <summary>
        /// 채굴 완료 처리
        /// </summary>
        private void HandleMiningComplete(MiningComplete complete)
        {
            Debug.Log($"채굴 완료! 광물 #{complete.MineralId}, 획득 골드: {complete.GoldEarned}");

            // 다음 광물 자동 시작 (서버에서 MiningUpdate가 올 것임) 전까지 리스폰 상태 표시
            isRespawning = true;
            currentHP = 0;
            UpdateMineInfo();
            UpdateHPBar();

            // 재화 갱신: 서버가 total_gold를 내려주므로 상단 재화도 반영
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
                // 광물 ID 1~N: 정상 채굴, StopMineralId(0): 채굴 중단
                uint mineralId = response.MineralId;
                bool isStop = mineralId == StopMineralId || (response.MineralHp == 0 && response.MineralMaxHp == 0);

                if (isStop)
                {
                    currentMineralName = GetMineralName(StopMineralId);
                    currentHP = 0;
                    maxHP = 0;
                    isRespawning = false;
                }
                else
                {
                    currentMineralName = GetMineralName(mineralId);
                    currentHP = response.MineralHp;
                    maxHP = response.MineralMaxHp;
                    isRespawning = false;
                }

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
            if (response == null) return;

            pickaxeCache?.UpdateFromAllSlots(response);
            SyncSlotsFromCache();

            currentDPS = response.TotalDps;

            foreach (var slot in response.Slots)
            {
                switch (slot.SlotIndex)
                {
                    case 0: /* ?? 0? ?? ?? */ break;
                    case 1: slot2Unlocked = slot.IsUnlocked; break;
                    case 2: slot3Unlocked = slot.IsUnlocked; break;
                    case 3: slot4Unlocked = slot.IsUnlocked; break;
                }
            }

            RefreshData();
        }

        private void HandleUpgradeResult(UpgradeResult result)
        {
            if (result == null) return;
            if (result.Success)
            {
                pickaxeCache?.UpdateFromUpgradeResult(result);
                SyncSlotsFromCache();
                RefreshData();
            }
        }

        private void UpdateSlotLevels()
        {
            if (slot1LevelText != null)
            {
                slot1LevelText.text = slotInfos.TryGetValue(0, out var s0) ? $"Lv {s0.Level}" : "Lv 0";
            }
            if (slot2LevelText != null)
            {
                slot2LevelText.text = slotInfos.TryGetValue(1, out var s1) ? $"Lv {s1.Level}" : "Lv 0";
            }
            if (slot3LevelText != null)
            {
                slot3LevelText.text = slotInfos.TryGetValue(2, out var s2) ? $"Lv {s2.Level}" : "Lv 0";
            }
            if (slot4LevelText != null)
            {
                slot4LevelText.text = slotInfos.TryGetValue(3, out var s3) ? $"Lv {s3.Level}" : "Lv 0";
            }
        }

        private string GetMineralName(uint mineralId)
        {
            if (mineralId == StopMineralId)
            {
                return mineralNames != null && mineralNames.Length > 0 ? mineralNames[0] : "채굴 중단";
            }

            if (mineralNames != null && mineralId < mineralNames.Length && !string.IsNullOrEmpty(mineralNames[mineralId]))
            {
                return mineralNames[mineralId];
            }

            return $"광물 #{mineralId}";
        }

        #endregion
    }
}
