using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using TMPro;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.UI.Common;
using InfinitePickaxe.Client.Metadata;

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

        [Header("Pickaxe Slot References")]
        [SerializeField] private Button pickaxeSlot1Button;
        [SerializeField] private Button pickaxeSlot2Button;
        [SerializeField] private Button pickaxeSlot3Button;
        [SerializeField] private Button pickaxeSlot4Button;
        [SerializeField] private TextMeshProUGUI slot1LevelText;
        [SerializeField] private TextMeshProUGUI slot2LevelText;
        [SerializeField] private TextMeshProUGUI slot3LevelText;
        [SerializeField] private TextMeshProUGUI slot4LevelText;

        [Header("Sprites / Atlases")]
        [SerializeField] private Image currentMineralImage;
        [SerializeField] private Image pickaxeSlot1Image;
        [SerializeField] private Image pickaxeSlot2Image;
        [SerializeField] private Image pickaxeSlot3Image;
        [SerializeField] private Image pickaxeSlot4Image;
        [SerializeField] private SpriteAtlas mineralSpriteAtlas;
        [SerializeField] private SpriteAtlas pickaxeSpriteAtlas;

        [Header("Modal References")]
        [SerializeField] private GameObject pickaxeInfoModal;
        [SerializeField] private GameObject lockedSlotModal;
        [SerializeField] private GameObject mineralSelectModal;
        [Header("Pickaxe Info Modal UI")]
        [SerializeField] private Image pickaxeInfoImage;
        [SerializeField] private TextMeshProUGUI pickaxeInfoLevelText;
        [SerializeField] private TextMeshProUGUI pickaxeInfoAttackPowerText;
        [SerializeField] private TextMeshProUGUI pickaxeInfoAttackSpeedText;
        [SerializeField] private TextMeshProUGUI pickaxeInfoDpsText;
        [SerializeField] private TextMeshProUGUI pickaxeInfoCriticalChanceText;
        [SerializeField] private TextMeshProUGUI pickaxeInfoCriticalDamageText;
        [Header("Mineral Select (Dynamic)")]
        [SerializeField] private Transform mineralListContent;
        [SerializeField] private GameObject mineralItemTemplate;
        private bool warnedMissingMineralTemplate = false;
        private bool mineralListBuilt = false;
        private int lastBuiltMineralCount = 0;
        private bool mineralRecommendDirty = false;
        private int lastPickaxeInfoSlotIndex = -1;

        [Header("Mining Data")]
        [SerializeField] private string currentMineralName = "약한 돌";
        [SerializeField] private float currentHP = 25f;
        [SerializeField] private float maxHP = 25f;
        [SerializeField] private float currentDPS = 10f;
        [SerializeField] private bool slot2Unlocked = true;
        [SerializeField] private bool slot3Unlocked = false;
        [SerializeField] private bool slot4Unlocked = false;
        private uint currentMineralId = StopMineralId;

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
        private bool messageSubscribed = false;
        private enum MiningViewState
        {
            Loading,
            NoSelection,
            Active
        }
        private const string LoadingMineralMessage = "광물 정보를 불러오는 중...";
        private const string NoSelectionMessage = "선택된 광물이 없습니다";
        private const string UnknownHpMessage = "HP: -/-";
        private const string PickaxeInfoMissingMessage = "데이터를 불러올 수 없다";
        [SerializeField] private float initialOverlayDelaySeconds = 0.3f;
        private MiningViewState viewState = MiningViewState.Loading;
        private Coroutine initialOverlayRoutine;
        private bool overlayOwned;

        private GameTabManager tabManager;
        private MessageHandler messageHandler;
        private bool isRespawning = false;
        private bool isPreparingMineral = false;
        private bool hpLayoutFixed = false;
        private bool cacheSubscribed = false;
        [SerializeField] private float hpSliderDefaultWidth = 800f;
        [SerializeField] private float hpSliderDefaultHeight = 50f;
        private readonly Dictionary<uint, PickaxeSlotInfo> slotInfos = new Dictionary<uint, PickaxeSlotInfo>();
        private PickaxeStateCache pickaxeCache;
        private readonly PickaxeTierResolver tierResolver = new PickaxeTierResolver();
        private readonly MineralMetaResolver mineralMetaResolver = new MineralMetaResolver();
        private readonly List<Button> dynamicMineralButtons = new List<Button>();

        [Header("HP Bar Animation")]
        [SerializeField] private float fillLerpSpeed = 6f;
        [SerializeField] private float colorLerpSpeed = 4f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulsePeriodSeconds = 0f; // 0이면 pulseSpeed 사용, 양수면 주기(초) 기반
        [SerializeField] private float pulseAmplitude = 0.08f;
        [SerializeField] private Color lowColor = Color.red;
        [SerializeField] private Color midColor = Color.yellow;
        [SerializeField] private Color highColor = Color.green;

        [Header("Pickaxe Swing Animation")]
        [SerializeField] private float restAngle = 0f; // 기본 이미지를 그대로 두기 위해 0도
        [SerializeField] private float swingDownDegrees = 135f; // 양수 회전으로 내려치기
        [SerializeField] private float swingDuration = 1.0f; // 공격속도 1.0초 기준
        [SerializeField, Range(0.1f, 0.9f)] private float swingDownPortion = 0.35f;

        [Header("Damage Text (Floating)")]
        [SerializeField] private RectTransform damageTextRoot;
        [SerializeField] private TextMeshProUGUI damageTextPrefab;
        [SerializeField] private float damageTextLifetime = 0.9f;
        [SerializeField] private float damageTextRiseSpeed = 80f;
        [SerializeField] private Vector2 damageTextRandomOffset = new Vector2(60f, 10f);
        [SerializeField] private Color normalDamageColor = new Color(1f, 0.95f, 0.85f, 1f);
        [SerializeField] private Color criticalDamageColor = new Color(1f, 0.35f, 0.2f, 1f);
        [SerializeField] private float criticalScale = 1.2f;

        private float targetFillNormalized = 1f;
        private float displayedFillNormalized = 1f;
        private float safeMaxForDisplay = 1f;
        private Color currentFillColor = Color.green;
        private readonly List<DamageTextEntry> activeDamageTexts = new List<DamageTextEntry>();
        private readonly Queue<TextMeshProUGUI> damageTextPool = new Queue<TextMeshProUGUI>();
        private readonly PickaxeSwingState[] swingStates = new PickaxeSwingState[4];
        private readonly bool[] swingDirections = new bool[4];

        protected override void Initialize()
        {
            base.Initialize();

            // 아틀라스 등록 (어디에서든 공용으로 사용)
            SpriteAtlasCache.RegisterMineralAtlas(mineralSpriteAtlas);
            SpriteAtlasCache.RegisterPickaxeAtlas(pickaxeSpriteAtlas);

            // 초기값을 채굴 중단 상태로 설정해 fallback 노출을 피함
            currentMineralName = GetMineralName(StopMineralId);
            currentMineralId = StopMineralId;
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
            AutoBindSlotImages();
            AutoBindPickaxeInfoModalReferences();

            // 모달 닫기 버튼 이벤트 등록
            SetupModalCloseButtons();

            // 초기 UI 업데이트
            RefreshData();
        }

        private void OnDestroy()
        {
            // 중복 해제 방지: OnDisable에서도 수행하지만 안전하게 한 번 더 수행
            UnsubscribeMessageHandler();
            UnsubscribeCache();
            EndInitialLoadingOverlay();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeMessageHandler();

            SubscribeCache();
            SyncSlotsFromCache();
            ApplyLastSnapshotIfAvailable();
            SyncInitialLoadingState();
            RefreshData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnsubscribeCache();
            EndInitialLoadingOverlay();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();

            // 탭이 표시될 때마다 데이터 갱신
            RefreshData();
            AutoBindLevelTexts();
        }

        private void SubscribeMessageHandler()
        {
            if (messageSubscribed) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler == null) return;

            messageHandler.OnUserDataSnapshot += HandleUserDataSnapshot;
            messageHandler.OnMiningUpdate += HandleMiningUpdate;
            messageHandler.OnMiningComplete += HandleMiningComplete;
            messageHandler.OnChangeMineralResponse += HandleChangeMineralResponse;
            messageHandler.OnAllSlotsResponse += HandleAllSlotsResponse;
            messageHandler.OnUpgradeResult += HandleUpgradeResult;
            messageSubscribed = true;
        }

        private void UnsubscribeMessageHandler()
        {
            if (!messageSubscribed || messageHandler == null) return;

            messageHandler.OnUserDataSnapshot -= HandleUserDataSnapshot;
            messageHandler.OnMiningUpdate -= HandleMiningUpdate;
            messageHandler.OnMiningComplete -= HandleMiningComplete;
            messageHandler.OnChangeMineralResponse -= HandleChangeMineralResponse;
            messageHandler.OnAllSlotsResponse -= HandleAllSlotsResponse;
            messageHandler.OnUpgradeResult -= HandleUpgradeResult;
            messageSubscribed = false;
        }

        private void ApplyLastSnapshotIfAvailable()
        {
            if (hasServerState) return;
            messageHandler ??= MessageHandler.Instance;
            if (messageHandler != null && messageHandler.TryGetLastSnapshot(out var snapshot))
            {
                HandleUserDataSnapshot(snapshot);
            }
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
            if (isActive)
            {
                RefreshData();
                UpdatePickaxeInfoModalIfOpen();
            }
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

        private void AutoBindSlotImages()
        {
            if (pickaxeSlot1Image == null)
                pickaxeSlot1Image = GetButtonImage(pickaxeSlot1Button);
            if (pickaxeSlot2Image == null)
                pickaxeSlot2Image = GetButtonImage(pickaxeSlot2Button);
            if (pickaxeSlot3Image == null)
                pickaxeSlot3Image = GetButtonImage(pickaxeSlot3Button);
            if (pickaxeSlot4Image == null)
                pickaxeSlot4Image = GetButtonImage(pickaxeSlot4Button);
        }

        private void AutoBindPickaxeInfoModalReferences()
        {
            if (pickaxeInfoModal == null) return;

            var root = pickaxeInfoModal.transform;
            if (pickaxeInfoImage == null)
            {
                var imageTf = FindChildRecursive(root, "PickaxeImage");
                if (imageTf != null) pickaxeInfoImage = imageTf.GetComponent<Image>();
            }

            if (pickaxeInfoLevelText == null)
            {
                var levelTf = FindChildRecursive(root, "PickaxeLevelText");
                if (levelTf != null) pickaxeInfoLevelText = levelTf.GetComponent<TextMeshProUGUI>();
            }

            if (pickaxeInfoAttackPowerText == null)
            {
                var attackTf = FindChildRecursive(root, "AttackPowerText");
                if (attackTf != null) pickaxeInfoAttackPowerText = attackTf.GetComponent<TextMeshProUGUI>();
            }

            if (pickaxeInfoAttackSpeedText == null)
            {
                var speedTf = FindChildRecursive(root, "AttackSpeedText");
                if (speedTf != null) pickaxeInfoAttackSpeedText = speedTf.GetComponent<TextMeshProUGUI>();
            }

            if (pickaxeInfoDpsText == null)
            {
                var dpsTf = FindChildRecursive(root, "DPSText");
                if (dpsTf != null) pickaxeInfoDpsText = dpsTf.GetComponent<TextMeshProUGUI>();
            }

            if (pickaxeInfoCriticalChanceText == null)
            {
                var chanceTf = FindChildRecursive(root, "CriticalChanceText");
                if (chanceTf != null) pickaxeInfoCriticalChanceText = chanceTf.GetComponent<TextMeshProUGUI>();
            }

            if (pickaxeInfoCriticalDamageText == null)
            {
                var damageTf = FindChildRecursive(root, "CriticalDamageText");
                if (damageTf != null) pickaxeInfoCriticalDamageText = damageTf.GetComponent<TextMeshProUGUI>();
            }

            EnsurePickaxeInfoModalCriticalTexts();
        }

        private void EnsurePickaxeInfoModalCriticalTexts()
        {
            if (pickaxeInfoModal == null) return;

            var template = pickaxeInfoAttackSpeedText != null ? pickaxeInfoAttackSpeedText : pickaxeInfoAttackPowerText;
            if (template == null) return;

            if (pickaxeInfoCriticalChanceText == null)
            {
                int insertIndex = template.transform.GetSiblingIndex() + 1;
                pickaxeInfoCriticalChanceText = ClonePickaxeInfoText("CriticalChanceText", template, insertIndex);
            }

            if (pickaxeInfoCriticalDamageText == null)
            {
                int baseIndex = pickaxeInfoCriticalChanceText != null
                    ? pickaxeInfoCriticalChanceText.transform.GetSiblingIndex()
                    : template.transform.GetSiblingIndex();
                pickaxeInfoCriticalDamageText = ClonePickaxeInfoText("CriticalDamageText", template, baseIndex + 1);
            }
        }

        private TextMeshProUGUI ClonePickaxeInfoText(string name, TextMeshProUGUI template, int siblingIndex)
        {
            if (template == null) return null;
            var parent = template.transform.parent;
            if (parent == null) return null;

            var clone = Instantiate(template.gameObject, parent, false);
            clone.name = name;
            clone.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));

            var text = clone.GetComponent<TextMeshProUGUI>();
            if (text != null) text.text = string.Empty;
            return text;
        }

        private Image GetButtonImage(Button button)
        {
            if (button == null) return null;
            if (button.targetGraphic is Image target) return target;
            return button.GetComponent<Image>();
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
            viewState = ResolveViewState();
            // targetFillNormalized는 Animate에서 부드럽게 따라감
            // 서버 상태를 받은 뒤에만 렌더링되도록 기본 상태는 채굴 중단을 표시
            UpdateMineInfo();
            UpdateHPBar();
            UpdateMineralSprite();
            UpdateDPS();
            UpdateSlotLevels();
            UpdatePickaxeSlotSprites();
            UpdateMineralSelectIcons();
        }

        /// <summary>
        /// 광물 정보 텍스트 업데이트
        /// </summary>
        private void UpdateMineInfo()
        {
            if (mineInfoText == null) return;

            if (viewState == MiningViewState.Loading)
            {
                mineInfoText.text = LoadingMineralMessage;
                return;
            }

            if (viewState == MiningViewState.NoSelection)
            {
                mineInfoText.text = NoSelectionMessage;
                return;
            }

            if (isPreparingMineral)
            {
                mineInfoText.text = $"대기 중: {currentMineralName}";
                return;
            }

            string status = isRespawning ? " (리스폰 중)" : string.Empty;
            mineInfoText.text = $"채굴 중: {currentMineralName}{status}";
        }

        /// <summary>
        /// HP 바 및 텍스트 업데이트
        /// </summary>
        private void UpdateHPBar()
        {
            // Fill/Background RectTransform이 0x0이면 슬라이더가 안 보이므로 한 번 강제 리레이아웃
            FixHpSliderLayout();

            if (viewState != MiningViewState.Active)
            {
                safeMaxForDisplay = 1f;
                if (mineHPSlider != null)
                {
                    mineHPSlider.minValue = 0f;
                    mineHPSlider.maxValue = safeMaxForDisplay;
                    mineHPSlider.wholeNumbers = false;
                    mineHPSlider.SetValueWithoutNotify(0f);
                    // 슬라이더 오브젝트 자체 사이즈가 0이라면 디폴트 크기로 보정
                    var rt = mineHPSlider.GetComponent<RectTransform>();
                    if (rt != null && (rt.sizeDelta.x <= 0 || rt.sizeDelta.y <= 0))
                    {
                        rt.sizeDelta = new Vector2(hpSliderDefaultWidth, hpSliderDefaultHeight);
                    }
                }

                targetFillNormalized = 0f;
                displayedFillNormalized = 0f;

                if (mineHPSliderFill != null)
                {
                    mineHPSliderFill.fillAmount = 0f;
                }

                if (mineHPText != null)
                {
                    mineHPText.text = UnknownHpMessage;
                    mineHPText.color = Color.gray;
                }

                if (mineHPSliderBackground != null)
                {
                    mineHPSliderBackground.color = Color.black;
                }

                return;
            }

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
                string currentHpText = currentHP.ToString("N0");
                string maxHpText = maxHP.ToString("N0");
                mineHPText.text = $"HP: {currentHpText}/{maxHpText} ({hpPercent:F1}%)";

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
            UpdatePickaxeSwings();
            UpdateDamageTextAnimations();
        }

        private void AnimateHPBar()
        {
            if (viewState != MiningViewState.Active)
            {
                return;
            }

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

        private void UpdateMineralSprite()
        {
            if (currentMineralImage == null) return;

            if (viewState != MiningViewState.Active)
            {
                currentMineralImage.sprite = null;
                currentMineralImage.enabled = false;
                return;
            }

            var sprite = SpriteAtlasCache.GetMineralSprite(currentMineralId);
            currentMineralImage.sprite = sprite;
            currentMineralImage.enabled = sprite != null;
            // UI에서 이미지가 뒤집히거나 타일링되지 않도록 기본 설정 보정
            currentMineralImage.type = Image.Type.Simple;
            currentMineralImage.preserveAspect = true;
            var scale = currentMineralImage.rectTransform.localScale;
            currentMineralImage.rectTransform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
        }

        private void UpdatePickaxeSlotSprites()
        {
            UpdatePickaxeSlotSprite(pickaxeSlot1Image, 0);
            UpdatePickaxeSlotSprite(pickaxeSlot2Image, 1);
            UpdatePickaxeSlotSprite(pickaxeSlot3Image, 2);
            UpdatePickaxeSlotSprite(pickaxeSlot4Image, 3);
        }

        private void UpdatePickaxeSlotSprite(Image targetImage, uint slotIndex)
        {
            if (targetImage == null) return;

            uint tier = 1;
            uint level = 0;
            bool unlocked = slotIndex switch
            {
                0 => true,
                1 => slot2Unlocked,
                2 => slot3Unlocked,
                3 => slot4Unlocked,
                _ => false
            };

            if (slotInfos.TryGetValue(slotIndex, out var slotInfo))
            {
                tier = slotInfo.Tier;
                level = slotInfo.Level;
                unlocked = slotInfo.IsUnlocked;
            }

            // 메타에 정의된 레벨별 티어가 더 높다면 그것을 사용
            tier = tierResolver.ResolveTier(slotIndex, level, tier);

            if (!SpriteAtlasCache.TryGetPickaxeSprite(tier, out var sprite))
            {
                sprite = SpriteAtlasCache.GetFallbackSprite();
            }

            targetImage.sprite = sprite;
            targetImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        private void UpdateMineralSelectIcons()
        {
            SetMineralIcon(mineralItemNullButton, 0);
            foreach (var btn in dynamicMineralButtons)
            {
                if (btn == null) continue;
                if (btn.TryGetComponent<MineralButtonTag>(out var tag))
                {
                    SetMineralIcon(btn, tag.MineralId);
                    if (mineralMetaResolver.TryGetMineral(tag.MineralId, out var meta))
                    {
                        UpdateMineralButtonLabel(btn.transform, meta, currentDPS);
                        UpdateRecommendedBadge(btn.transform, meta, currentDPS);
                    }
                }
            }
        }

        private void ClearMineralListContent()
        {
            if (mineralListContent == null) return;
            FindNullButtonInContent();

            var toDestroy = new List<Transform>();
            for (int i = 0; i < mineralListContent.childCount; i++)
            {
                var child = mineralListContent.GetChild(i);
                if (mineralItemNullButton != null && child == mineralItemNullButton.transform) continue;
                if (mineralItemTemplate != null && child == mineralItemTemplate.transform) continue;
                toDestroy.Add(child);
            }

            foreach (var t in toDestroy)
            {
                Destroy(t.gameObject);
            }
        }

        private void SetMineralIcon(Button button, uint mineralId)
        {
            if (button == null) return;
            if (!SpriteAtlasCache.TryGetMineralSprite(mineralId, out var sprite)) return;

            // 우선순위: 같은 아이템 루트(버튼의 부모 포함) 하위의 "Icon" 이름을 가진 Image
            Image img = null;
            Transform itemRoot = button.transform.parent != null ? button.transform.parent : button.transform;

            var icon = button.transform.Find("Icon");
            if (icon == null && itemRoot != null)
            {
                icon = itemRoot.Find("Icon");
            }
            if (icon != null)
            {
                img = icon.GetComponent<Image>();
            }

            if (img == null && itemRoot != null)
            {
                var images = itemRoot.GetComponentsInChildren<Image>(true);
                foreach (var childImg in images)
                {
                    if (childImg.gameObject == button.gameObject) continue; // 버튼 자기 자신은 제외
                    if (childImg.name.Contains("Icon")) { img = childImg; break; }
                }
            }

            // 버튼의 타겟 그래픽이나 자기 Image에는 스프라이트를 넣지 않는다.
            if (img == null) return;

            img.sprite = sprite;
            img.enabled = true;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            var s = img.rectTransform.localScale;
            img.rectTransform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1f);
        }

        private void UpdateMineralButtonIcon(Transform root, uint mineralId)
        {
            if (root == null) return;
            if (!SpriteAtlasCache.TryGetMineralSprite(mineralId, out var sprite)) return;

            var icon = FindChildRecursive(root, "Icon");
            if (icon == null) return;

            var img = icon.GetComponent<Image>();
            if (img == null) return;

            img.sprite = sprite;
            img.enabled = true;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            var s = img.rectTransform.localScale;
            img.rectTransform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1f);
        }

        private void UpdateDamageTextAnimations()
        {
            if (activeDamageTexts.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = activeDamageTexts.Count - 1; i >= 0; i--)
            {
                var entry = activeDamageTexts[i];
                entry.Elapsed += dt;
                float t = entry.Elapsed / entry.Lifetime;

                if (entry.Rect != null)
                {
                    var pos = entry.Rect.anchoredPosition;
                    pos.y += entry.RiseSpeed * dt;
                    entry.Rect.anchoredPosition = pos;
                }

                if (entry.Text != null)
                {
                    var c = entry.Text.color;
                    c.a = Mathf.Lerp(entry.StartAlpha, 0f, t);
                    entry.Text.color = c;
                }

                if (entry.Elapsed >= entry.Lifetime)
                {
                    RecycleDamageText(entry);
                    activeDamageTexts.RemoveAt(i);
                }
                else
                {
                    activeDamageTexts[i] = entry;
                }
            }
        }

        private void UpdatePickaxeSwings()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < swingStates.Length; i++)
            {
                var state = swingStates[i];
                if (!state.Active)
                {
                    ApplyPickaxeAngle((uint)i, restAngle);
                    continue;
                }

                state.Elapsed += dt;
                float t = Mathf.Clamp01(state.Elapsed / state.Duration);
                float downPortion = Mathf.Clamp(swingDownPortion, 0.1f, 0.9f);

                float angle;
                if (t <= downPortion)
                {
                    // 빠르게 내려치는 구간 (ease-out)
                    float td = t / downPortion;
                    float easeOut = 1f - Mathf.Pow(1f - td, 2f);
                    angle = Mathf.Lerp(restAngle, restAngle + swingDownDegrees, easeOut);
                }
                else
                {
                    // 천천히 원위치로 복귀 (ease-in-out)
                    float tu = (t - downPortion) / (1f - downPortion);
                    float easeInOut = tu < 0.5f
                        ? 2f * tu * tu
                        : 1f - Mathf.Pow(-2f * tu + 2f, 2f) / 2f;
                    angle = Mathf.Lerp(restAngle + swingDownDegrees, restAngle, easeInOut);
                }

                ApplyPickaxeAngle((uint)i, angle);

                if (t >= 1f)
                {
                    state.Active = false;
                }

                swingStates[i] = state;
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
                var roundedDps = Mathf.FloorToInt(currentDPS);
                dpsText.text = $"DPS: {roundedDps:N0}";
            }
        }

        private MiningViewState ResolveViewState()
        {
            if (!hasServerState)
            {
                return MiningViewState.Loading;
            }

            if (IsNoSelectionState())
            {
                return MiningViewState.NoSelection;
            }

            return MiningViewState.Active;
        }

        private bool IsNoSelectionState()
        {
            if (isRespawning || isPreparingMineral)
            {
                return false;
            }

            if (currentMineralId == StopMineralId)
            {
                return true;
            }

            if (maxHP <= 0f && currentHP <= 0f)
            {
                return true;
            }

            return false;
        }

        private void SyncInitialLoadingState()
        {
            if (!hasServerState)
            {
                BeginInitialLoadingOverlay();
                return;
            }

            EndInitialLoadingOverlay();
        }

        private void BeginInitialLoadingOverlay()
        {
            if (overlayOwned || initialOverlayRoutine != null) return;

            initialOverlayRoutine = StartCoroutine(ShowInitialOverlayAfterDelay());
        }

        private IEnumerator ShowInitialOverlayAfterDelay()
        {
            if (initialOverlayDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(initialOverlayDelaySeconds);
            }

            initialOverlayRoutine = null;
            if (hasServerState || overlayOwned) yield break;

            var manager = LoadingOverlayManager.Instance;
            if (manager == null) yield break;

            manager.Show(LoadingMineralMessage);
            overlayOwned = true;
        }

        private void EndInitialLoadingOverlay()
        {
            if (initialOverlayRoutine != null)
            {
                StopCoroutine(initialOverlayRoutine);
                initialOverlayRoutine = null;
            }

            if (!overlayOwned)
            {
                return;
            }

            var manager = LoadingOverlayManager.Instance;
            if (manager != null)
            {
                manager.Hide();
            }

            overlayOwned = false;
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
        }

        private void UpdatePickaxeInfoModalIfOpen()
        {
            if (pickaxeInfoModal == null || !pickaxeInfoModal.activeSelf) return;
            if (lastPickaxeInfoSlotIndex < 0) return;

            UpdatePickaxeInfoModal(lastPickaxeInfoSlotIndex);
        }

        private void UpdatePickaxeInfoModal(int slotIndex)
        {
            AutoBindPickaxeInfoModalReferences();

            if (!TryGetPickaxeSlotInfo(slotIndex, out var slotInfo))
            {
                ApplyPickaxeInfoMissingState();
                return;
            }

            UpdatePickaxeInfoModalData(slotIndex, slotInfo);
        }

        private bool TryGetPickaxeSlotInfo(int slotIndex, out PickaxeSlotInfo slotInfo)
        {
            slotInfo = null;
            if (slotIndex < 0 || slotIndex > 3) return false;

            var key = (uint)slotIndex;
            if (slotInfos.TryGetValue(key, out slotInfo) && slotInfo != null)
            {
                return true;
            }

            if (pickaxeCache != null && pickaxeCache.TryGetSlot(key, out slotInfo) && slotInfo != null)
            {
                slotInfos[key] = slotInfo;
                return true;
            }

            return false;
        }

        private void ApplyPickaxeInfoMissingState()
        {
            SetPickaxeInfoText(pickaxeInfoLevelText, PickaxeInfoMissingMessage);
            SetPickaxeInfoText(pickaxeInfoAttackPowerText, PickaxeInfoMissingMessage);
            SetPickaxeInfoText(pickaxeInfoAttackSpeedText, PickaxeInfoMissingMessage);
            SetPickaxeInfoText(pickaxeInfoDpsText, PickaxeInfoMissingMessage);
            SetPickaxeInfoText(pickaxeInfoCriticalChanceText, PickaxeInfoMissingMessage);
            SetPickaxeInfoText(pickaxeInfoCriticalDamageText, PickaxeInfoMissingMessage);

            if (pickaxeInfoImage != null)
            {
                pickaxeInfoImage.sprite = null;
                pickaxeInfoImage.enabled = false;
            }
        }

        private void SetPickaxeInfoText(TextMeshProUGUI target, string text)
        {
            if (target != null)
            {
                target.text = text;
            }
        }

        private void UpdatePickaxeInfoModalData(int slotIndex, PickaxeSlotInfo slotInfo)
        {
            if (pickaxeInfoLevelText != null)
            {
                pickaxeInfoLevelText.text = $"Lv {slotInfo.Level}";
            }

            if (pickaxeInfoAttackPowerText != null)
            {
                pickaxeInfoAttackPowerText.text = $"공격력: {slotInfo.AttackPower:N0}";
            }

            if (pickaxeInfoAttackSpeedText != null)
            {
                float attackSpeed = slotInfo.AttackSpeedX100 / 100f;
                pickaxeInfoAttackSpeedText.text = $"공격속도: {attackSpeed:0.0#}";
            }

            if (pickaxeInfoDpsText != null)
            {
                pickaxeInfoDpsText.text = $"DPS: {slotInfo.Dps:N0}";
            }

            if (pickaxeInfoCriticalChanceText != null)
            {
                float critChance = slotInfo.CriticalHitPercent / 100f;
                pickaxeInfoCriticalChanceText.text = $"크리티컬 확률: {critChance:0.##}%";
            }

            if (pickaxeInfoCriticalDamageText != null)
            {
                float critDamage = slotInfo.CriticalDamage / 100f;
                pickaxeInfoCriticalDamageText.text = $"크리티컬 데미지: {critDamage:0.##}%";
            }

            if (pickaxeInfoImage != null)
            {
                uint tier = slotInfo.Tier;
                tier = tierResolver.ResolveTier((uint)slotIndex, slotInfo.Level, tier);

                if (!SpriteAtlasCache.TryGetPickaxeSprite(tier, out var sprite))
                {
                    sprite = SpriteAtlasCache.GetFallbackSprite();
                }

                pickaxeInfoImage.sprite = sprite;
                pickaxeInfoImage.enabled = sprite != null;
                pickaxeInfoImage.type = Image.Type.Simple;
                pickaxeInfoImage.preserveAspect = true;
                var scale = pickaxeInfoImage.rectTransform.localScale;
                pickaxeInfoImage.rectTransform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
            }
        }

        /// <summary>
        /// 곡괭이 정보 모달 열기
        /// </summary>
        private void OpenPickaxeInfoModal(int slotIndex)
        {
            if (pickaxeInfoModal != null)
            {
                lastPickaxeInfoSlotIndex = slotIndex;
                UpdatePickaxeInfoModal(slotIndex);
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

            BuildMineralSelectList();
            mineralRecommendDirty = true; // 모달을 열 때 최신 DPS로 라벨 갱신 플래그
            UpdateMineralSelectIcons();
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
                if (lastPickaxeInfoSlotIndex >= 0)
                {
                    var upgradeTab = FindObjectOfType<UpgradeTabController>();
                    if (upgradeTab != null)
                    {
                        upgradeTab.SetSelectedSlot((uint)lastPickaxeInfoSlotIndex);
                    }
                }
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
            mineralRecommendDirty = true;

            RefreshData();
        }

        /// <summary>
        /// 현재 채굴 중인 광물 변경 (외부에서 호출)
        /// </summary>
        /// <param name="mineralName">광물 이름</param>
        /// <param name="hp">현재 HP</param>
        /// <param name="maxHp">최대 HP</param>
        /// <param name="mineralId">광물 ID (기본=채굴 중단)</param>
        public void SetCurrentMineral(string mineralName, float hp, float maxHp, uint mineralId = StopMineralId)
        {
            currentMineralName = mineralName;
            currentMineralId = mineralId;
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
            isPreparingMineral = false;

            pickaxeCache?.UpdateFromSnapshot(snapshot);
            SyncSlotsFromCache();
            currentDPS = pickaxeCache?.TotalDps ?? currentDPS;

            // 광물 정보 업데이트
            if (snapshot.CurrentMineralId.HasValue)
            {
                uint mineralId = snapshot.CurrentMineralId.Value;
                currentMineralId = mineralId;
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
                currentMineralId = StopMineralId;
                currentHP = 0;
                maxHP = 0;
            }

            // DPS 정보 업데이트
            currentDPS = snapshot.TotalDps;
            mineralRecommendDirty = true;

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
            viewState = ResolveViewState();
            if (isActive)
            {
                RefreshData();
            }
            SyncInitialLoadingState();
        }

        /// <summary>
        /// 채굴 진행 업데이트 처리 (서버 40ms 틱)
        /// </summary>
        private void HandleMiningUpdate(MiningUpdate update)
        {
            hasServerState = true;
            currentHP = update.CurrentHp;
            maxHP = update.MaxHp;
            currentMineralId = update.MineralId;
            currentMineralName = GetMineralName(update.MineralId);
            isPreparingMineral = false;

            // 서버가 채굴 중단 상태라면 HP 0/0으로 유지
            if (update.MaxHp == 0)
            {
                currentMineralName = GetMineralName(StopMineralId);
                currentMineralId = StopMineralId;
                currentHP = 0;
                maxHP = 0;
                isRespawning = true;
            }
            else
            {
                isRespawning = false;
            }

            viewState = ResolveViewState();
            SyncInitialLoadingState();

            // DPS는 AllSlotsResponse에서 받은 값 유지 (TotalDps 필드 제거됨)
            // currentDPS는 슬롯 변경 시에만 업데이트됨

            if (!isActive) return;

            // 각 곡괭이 공격 처리 (애니메이션 트리거)
            if (update.Attacks != null && update.Attacks.Count > 0)
            {
                foreach (var attack in update.Attacks)
                {
                    TriggerPickaxeAttackAnimation(attack.SlotIndex, attack.Damage, attack.IsCritical);
                }
            }

            UpdateMineInfo();
            UpdateHPBar();
            UpdateMineralSprite();
            // UpdateDPS()는 호출하지 않음 (DPS는 슬롯 정보 변경 시에만 업데이트)
        }

        /// <summary>
        /// 채굴 완료 처리
        /// </summary>
        private void HandleMiningComplete(MiningComplete complete)
        {
            // Debug.Log($"채굴 완료! 광물 #{complete.MineralId}, 획득 골드: {complete.GoldEarned}");

            // 다음 광물 자동 시작 (서버에서 MiningUpdate가 올 것임) 전까지 리스폰 상태 표시
            isPreparingMineral = false;
            isRespawning = true;
            currentHP = 0;
            if (isActive)
            {
                UpdateMineInfo();
                UpdateHPBar();
                UpdateMineralSprite();
            }

            // 재화 갱신: 서버가 total_gold를 내려주므로 상단 재화도 반영
        }

        /// <summary>
        /// 곡괭이 공격 애니메이션 트리거
        /// </summary>
        /// <param name="slotIndex">슬롯 인덱스 (0~3)</param>
        /// <param name="damage">공격 데미지</param>
        private void TriggerPickaxeAttackAnimation(uint slotIndex, ulong damage, bool isCritical)
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

            ShowDamageText(damage, isCritical);
            StartPickaxeSwing(slotIndex);

#if UNITY_EDITOR || DEBUG_MINING
            Debug.Log($"곡괭이 공격: 슬롯 {slotIndex}, 데미지 {damage}, 크리티컬={isCritical}");
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
                    currentMineralId = StopMineralId;
                    currentHP = 0;
                    maxHP = 0;
                    isRespawning = false;
                    isPreparingMineral = false;
                }
                else
                {
                    currentMineralName = GetMineralName(mineralId);
                    currentMineralId = mineralId;
                    currentHP = response.MineralHp;
                    maxHP = response.MineralMaxHp;
                    isRespawning = false;
                    isPreparingMineral = true; // 서버의 첫 MiningUpdate가 도착하기 전까지 준비 상태
                }

                if (isActive)
                {
                    RefreshData();
                }

                CloseModal(mineralSelectModal);
            }
            else
            {
                Debug.LogWarning($"광물 변경 실패: {response.ErrorCode}");
                isPreparingMineral = false;
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

            if (isActive)
            {
                RefreshData();
            }
        }

        private void HandleUpgradeResult(UpgradeResult result)
        {
            if (result == null) return;
            if (result.Success)
            {
                pickaxeCache?.UpdateFromUpgradeResult(result);
                SyncSlotsFromCache();
                if (isActive)
                {
                    RefreshData();
                }
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

            if (mineralMetaResolver.TryGetMineral(mineralId, out var meta))
            {
                return meta.Name;
            }

            if (mineralNames != null && mineralId < mineralNames.Length && !string.IsNullOrEmpty(mineralNames[mineralId]))
            {
                return mineralNames[mineralId];
            }

            return $"광물 #{mineralId}";
        }

        private void BuildMineralSelectList()
        {
            if (mineralListBuilt && lastBuiltMineralCount > 0)
            {
                return; // 이미 한 번 생성 완료
            }

            if (!MetaRepository.Loaded)
            {
                mineralMetaResolver.Reload();
            }

            if (!EnsureMineralSelectReferences())
            {
                if (!warnedMissingMineralTemplate)
                {
                    warnedMissingMineralTemplate = true;
                    Debug.LogWarning("MiningTabController: mineralListContent 또는 mineralItemTemplate가 설정되지 않아 광물 목록을 생성하지 않습니다. Content와 템플릿 프리팹을 인스펙터에 할당하세요.");
                }
                return;
            }

            if (!MetaRepository.Loaded || mineralMetaResolver.All == null || mineralMetaResolver.All.Count == 0)
            {
                return;
            }

            var metas = mineralMetaResolver.All
                .GroupBy(m => m.Id)
                .Select(g => g.First())
                .OrderBy(m => m.Id);

            int metaCount = metas.Count();
            FindNullButtonInContent();
            int created = 0;
            foreach (var meta in metas)
            {
                var go = Instantiate(mineralItemTemplate, mineralListContent, false);
                go.name = $"MineralItem_{meta.Id}";
                go.SetActive(true);

                var btn = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    var id = (int)meta.Id;
                    btn.onClick.AddListener(() => SelectMineralNullable(id));
                    if (!btn.gameObject.TryGetComponent<MineralButtonTag>(out var tag))
                    {
                        tag = btn.gameObject.AddComponent<MineralButtonTag>();
                    }
                    tag.MineralId = meta.Id;
                }

                UpdateMineralButtonLabel(go.transform, meta, currentDPS);
                UpdateMineralButtonStats(go.transform, meta);
                UpdateMineralButtonIcon(go.transform, meta.Id);
                UpdateRecommendedBadge(go.transform, meta, currentDPS);
                if (btn != null) dynamicMineralButtons.Add(btn);
                created++;
            }

            if (created > 0)
            {
                mineralListBuilt = true;
                lastBuiltMineralCount = created;
                Debug.Log($"MiningTabController: 메타 광물 목록 로드됨. count={created}");
            }
            else
            {
                Debug.LogWarning($"MiningTabController: 메타 광물 목록이 비어있습니다. metaCount={metaCount}");
            }

            // 템플릿은 리스트 생성용으로만 사용하므로 숨겨둔다.
            if (mineralItemTemplate.activeSelf)
            {
                mineralItemTemplate.SetActive(false);
            }
        }

        private void UpdateMineralButtonLabel(Transform root, MineralMeta meta, float dps)
        {
            if (root == null) return;
            var nameTf = FindChildRecursive(root, "Name") ?? FindChildRecursive(root, "NameText");
            if (nameTf == null) return;
            var text = nameTf.GetComponent<TextMeshProUGUI>();
            if (text == null) return;

            text.text = meta.Name;
        }

        private void UpdateRecommendedBadge(Transform root, MineralMeta meta, float dps)
        {
            if (root == null) return;
            var badgeTf = FindChildRecursive(root, "RecommendImage");
            if (badgeTf == null) return;

            bool recommended = meta.RecommendedMinDps <= dps && dps <= meta.RecommendedMaxDps;
            badgeTf.gameObject.SetActive(recommended);
        }

        private void UpdateMineralButtonStats(Transform root, MineralMeta meta)
        {
            if (root == null || meta == null) return;

            string stats = $"HP: {meta.Hp:N0}  골드: {meta.Gold:N0}";

            // StatsText 우선
            var statsTf = FindChildRecursive(root, "StatsText");
            if (statsTf != null)
            {
                var t = statsTf.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = stats;
            }

            // HPText / GoldText 개별 적용
            var hpTf = FindChildRecursive(root, "HPText");
            if (hpTf == null) hpTf = FindChildRecursive(root, "HpText");
            if (hpTf != null)
            {
                var t = hpTf.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = $"HP: {meta.Hp:N0}";
            }

            var goldTf = FindChildRecursive(root, "GoldText");
            if (goldTf != null)
            {
                var t = goldTf.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = $"골드: {meta.Gold:N0}";
            }
        }

        private bool EnsureMineralSelectReferences()
        {
            return mineralListContent != null && mineralItemTemplate != null;
        }

        private void ShowDamageText(ulong damage, bool isCritical)
        {
            var root = damageTextRoot;
            if (root == null)
            {
                root = mineHPSliderBackground != null
                    ? mineHPSliderBackground.rectTransform.parent as RectTransform
                    : mineHPText?.transform.parent as RectTransform;
            }

            if (root == null) return;

            var label = GetDamageTextInstance(root);
            if (label == null) return;

            label.text = damage.ToString("N0");
            var color = isCritical ? criticalDamageColor : normalDamageColor;
            label.color = color;
            var scale = isCritical ? criticalScale : 1f;
            label.rectTransform.localScale = Vector3.one * scale;

            var offset = new Vector2(
                UnityEngine.Random.Range(-damageTextRandomOffset.x, damageTextRandomOffset.x),
                UnityEngine.Random.Range(0f, damageTextRandomOffset.y)
            );
            label.rectTransform.anchoredPosition = offset;

            var entry = new DamageTextEntry
            {
                Text = label,
                Rect = label.rectTransform,
                Elapsed = 0f,
                Lifetime = Mathf.Max(0.1f, damageTextLifetime),
                RiseSpeed = damageTextRiseSpeed,
                StartAlpha = color.a
            };

            activeDamageTexts.Add(entry);
            label.gameObject.SetActive(true);
        }

        private TextMeshProUGUI GetDamageTextInstance(RectTransform parent)
        {
            TextMeshProUGUI label = null;
            while (damageTextPool.Count > 0 && label == null)
            {
                label = damageTextPool.Dequeue();
            }

            if (label == null)
            {
                if (damageTextPrefab != null)
                {
                    label = Instantiate(damageTextPrefab, parent);
                }
                else
                {
                    var go = new GameObject("DamageText");
                    go.transform.SetParent(parent, false);
                    label = go.AddComponent<TextMeshProUGUI>();
                    label.fontSize = 48f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.outlineWidth = 0.15f;
                }
            }
            else
            {
                label.transform.SetParent(parent, false);
            }

            return label;
        }

        private void RecycleDamageText(DamageTextEntry entry)
        {
            if (entry.Text == null) return;
            entry.Text.gameObject.SetActive(false);
            damageTextPool.Enqueue(entry.Text);
        }

        private void StartPickaxeSwing(uint slotIndex)
        {
            if (slotIndex >= swingStates.Length) return;

            float duration = Mathf.Max(0.03f, swingDuration);
            if (slotInfos.TryGetValue(slotIndex, out var info) && info.AttackSpeedX100 > 0)
            {
                float speedMul = info.AttackSpeedX100 / 100f;
                duration = Mathf.Max(0.03f, swingDuration / speedMul);
            }

            swingStates[slotIndex] = new PickaxeSwingState
            {
                Active = true,
                Elapsed = 0f,
                Duration = duration
            };

            ApplyPickaxeAngle(slotIndex, restAngle);
        }

        private void ApplyPickaxeAngle(uint slotIndex, float angle)
        {
            var img = GetSlotImage(slotIndex);
            if (img == null) return;

            var rt = img.rectTransform;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Image GetSlotImage(uint slotIndex)
        {
            return slotIndex switch
            {
                0 => pickaxeSlot1Image,
                1 => pickaxeSlot2Image,
                2 => pickaxeSlot3Image,
                3 => pickaxeSlot4Image,
                _ => null
            };
        }

        private Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name.Equals(name))
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private struct DamageTextEntry
        {
            public TextMeshProUGUI Text;
            public RectTransform Rect;
            public float Elapsed;
            public float Lifetime;
            public float RiseSpeed;
            public float StartAlpha;
        }

        private struct PickaxeSwingState
        {
            public bool Active;
            public float Elapsed;
            public float Duration;
        }

        private void EnsureNullButtonForDynamicList()
        {
            // 선택 해제 버튼은 씬/프리팹에 배치된 것을 사용 (자동 생성하지 않음)
        }

        private void FindNullButtonInContent()
        {
            if (mineralItemNullButton != null) return;
            if (mineralListContent == null) return;

            // 0번 id 태그가 있으면 사용
            foreach (Transform child in mineralListContent)
            {
                var btn = child.GetComponent<Button>() ?? child.GetComponentInChildren<Button>();
                if (btn != null && btn.TryGetComponent<MineralButtonTag>(out var tag) && tag.MineralId == 0)
                {
                    mineralItemNullButton = btn;
                    return;
                }
            }

            // 이름으로 추정
            foreach (Transform child in mineralListContent)
            {
                if (child.name.Contains("0") || child.name.ToLower().Contains("stop") || child.name.ToLower().Contains("none"))
                {
                    var btn = child.GetComponent<Button>() ?? child.GetComponentInChildren<Button>();
                    if (btn != null)
                    {
                        mineralItemNullButton = btn;
                        return;
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 동적 생성된 광물 버튼에 ID를 보관하는 태그 컴포넌트.
    /// </summary>
    public sealed class MineralButtonTag : MonoBehaviour
    {
        public uint MineralId;
    }
}
