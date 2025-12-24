using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.UI.Common;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 곡괭이 강화 탭 컨트롤러.
    /// 슬롯 상태를 받아와 UI를 갱신하고, 강화 요청/응답을 처리한다.
    /// </summary>
    public partial class UpgradeTabController : BaseTabController
    {
        [Header("Upgrade UI References")]
        [SerializeField] private TextMeshProUGUI pickaxeLevelText;
        [SerializeField] private TextMeshProUGUI currentDPSText;
        [SerializeField] private TextMeshProUGUI nextDPSText;
        [SerializeField] private TextMeshProUGUI upgradeCostText;
        [SerializeField] private TextMeshProUGUI upgradeChanceText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button adDiscountButton;

        [Header("Slot Selection")]
        [SerializeField] private Button pickaxeSlot1Button;
        [SerializeField] private Button pickaxeSlot2Button;
        [SerializeField] private Button pickaxeSlot3Button;
        [SerializeField] private Button pickaxeSlot4Button;
        [SerializeField] private Image currentPickaxeImage;
        [SerializeField] private Image nextPickaxeImage;

        [Header("Visual")]
        [SerializeField] private Color lockedSlotColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        [SerializeField] private Color unlockedSlotColor = Color.white;
        [SerializeField] private Color selectedSlotColor = new Color(0.8f, 0.8f, 0.9f, 1f);
        [SerializeField] private List<TierSprite> pickaxeTierSprites = new List<TierSprite>();

        [Header("Sprites / Atlases")]
        [SerializeField] private SpriteAtlas pickaxeSpriteAtlas;

        [Header("Upgrade Data (display)")]
        [SerializeField] private uint currentLevel;
        [SerializeField] private ulong currentAttack;
        [SerializeField] private ulong nextAttack;
        [SerializeField] private ulong upgradeCost;
        [SerializeField] private float upgradeChancePercent = 100f;
        [SerializeField] private uint baseRateBp;
        [SerializeField] private uint pityBonusBp;
        [SerializeField] private uint finalRateBp;
        [Header("Upgrade FX")]
        [SerializeField] private GameObject successFxImage;
        [SerializeField] private GameObject failFxImage;
        [SerializeField] private Color successFxColor = new Color(0.9f, 0.95f, 0.3f, 1f);
        [SerializeField] private Color failFxColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private float popScale = 1.12f;
        [SerializeField] private float popDuration = 0.2f;
        [SerializeField] private float flashDuration = 0.25f;
        [SerializeField] private float shakeDuration = 0.18f;
        [SerializeField] private float shakeMagnitude = 8f;
        [SerializeField] private float fxImageShowTime = 0.7f;
        [SerializeField] private float fxImageFadeTime = 0.3f;

        private readonly Dictionary<uint, PickaxeSlotInfo> slotInfos = new Dictionary<uint, PickaxeSlotInfo>();
        private MessageHandler messageHandler;
        private PickaxeStateCache pickaxeCache;
        // 서버 프로토 기준 슬롯 인덱스(0~3)
        private uint selectedSlotIndex = 0;
        private ulong currentGold;
        private bool hasGoldInfo;
        private bool hasSlotData;
        private bool upgradeInProgress;
        private readonly UpgradeMetaResolver metaResolver = new UpgradeMetaResolver();
        private readonly PickaxeTierResolver tierResolver = new PickaxeTierResolver();
        private Sprite runtimeWhiteSprite;
        private Vector3 upgradeButtonDefaultScale = Vector3.one;
        private Vector2 upgradeButtonDefaultPos = Vector2.zero;
        private RectTransform upgradeButtonRect;
        private Image upgradeButtonImage;
        private Color? upgradeButtonDefaultColor;
        private readonly List<(TextMeshProUGUI label, Color color)> labelDefaultColors = new List<(TextMeshProUGUI, Color)>();
        private bool subscribed;
        private bool cacheSubscribed;
        private bool hasLastResultRates;
        private uint lastResultSlot;
        private uint lastResultBaseRateBp;
        private uint lastResultFinalRateBp;
        private uint lastResultPityBonusBp;
        private Coroutine successFxCoroutine;
        private Coroutine failFxCoroutine;
        private Coroutine popFlashCoroutine;
        private Coroutine flashShakeCoroutine;
        private Coroutine blinkTextsCoroutine;

        protected override void Initialize()
        {
            base.Initialize();
            SpriteAtlasCache.RegisterPickaxeAtlas(pickaxeSpriteAtlas);
            AutoBindReferences();
            InitializeSubTabs();
            BindSlotButtons();
            BindActionButtons();
            CacheInitialVisualState();
            RefreshData();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeEvents();
            SubscribeCache();
            SyncSlotsFromCache();
            RequestAllSlotsIfNeeded();
            RefreshData();
        }

        protected override void OnDisable()
        {
            // 이벤트는 계속 구독 상태로 유지하여 탭이 비활성화되어도 데이터가 최신으로 갱신되도록 함
            base.OnDisable();
            ResetAllFxVisuals();
        }

        private void AutoBindReferences()
        {
            if (pickaxeSlot1Button == null)
                pickaxeSlot1Button = GameObject.Find("Slot1Button")?.GetComponent<Button>();
            if (pickaxeSlot2Button == null)
                pickaxeSlot2Button = GameObject.Find("Slot2Button")?.GetComponent<Button>();
            if (pickaxeSlot3Button == null)
                pickaxeSlot3Button = GameObject.Find("Slot3Button")?.GetComponent<Button>();
            if (pickaxeSlot4Button == null)
                pickaxeSlot4Button = GameObject.Find("Slot4Button")?.GetComponent<Button>();
            if (upgradeChanceText == null)
                upgradeChanceText = GameObject.Find("UpgradeChanceText")?.GetComponent<TextMeshProUGUI>();
        }

        private void BindSlotButtons()
        {
            SetupSlotButton(pickaxeSlot1Button, 0);
            SetupSlotButton(pickaxeSlot2Button, 1);
            SetupSlotButton(pickaxeSlot3Button, 2);
            SetupSlotButton(pickaxeSlot4Button, 3);
        }

        private void BindActionButtons()
        {
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }

            if (adDiscountButton != null)
            {
                adDiscountButton.onClick.RemoveListener(OnAdDiscountClicked);
                adDiscountButton.onClick.AddListener(OnAdDiscountClicked);
            }
        }

        private void CacheInitialVisualState()
        {
            if (upgradeButton != null)
            {
                upgradeButtonRect = upgradeButton.transform as RectTransform;
                if (upgradeButtonRect != null)
                {
                    upgradeButtonDefaultScale = upgradeButtonRect.localScale;
                    upgradeButtonDefaultPos = upgradeButtonRect.anchoredPosition;
                }
                upgradeButtonImage = upgradeButton.GetComponent<Image>();
                if (upgradeButtonImage != null)
                {
                    upgradeButtonDefaultColor = upgradeButtonImage.color;
                }
            }

            labelDefaultColors.Clear();
            CacheLabelDefault(pickaxeLevelText);
            CacheLabelDefault(currentDPSText);
            CacheLabelDefault(nextDPSText);
            CacheLabelDefault(upgradeChanceText);
        }

        private void CacheLabelDefault(TextMeshProUGUI label)
        {
            if (label == null) return;
            labelDefaultColors.Add((label, label.color));
        }

        private void SetupSlotButton(Button button, uint slotIndex)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnSlotButtonClicked(slotIndex));
        }

        private void SubscribeEvents()
        {
            if (subscribed) return;
            messageHandler = MessageHandler.Instance;
            if (messageHandler == null)
            {
                Debug.LogWarning("UpgradeTabController: MessageHandler is null, cannot subscribe to network events.");
                return;
            }

            messageHandler.OnHandshakeResult += HandleHandshake;
            messageHandler.OnUserDataSnapshot += HandleSnapshot;
            messageHandler.OnAllSlotsResponse += HandleAllSlotsResponse;
            messageHandler.OnUpgradeResult += HandleUpgradeResult;
            messageHandler.OnCurrencyUpdate += HandleCurrencyUpdate;
            messageHandler.OnMiningComplete += HandleMiningComplete;
            subscribed = true;
        }

        private void SubscribeCache()
        {
            if (cacheSubscribed) return;
            pickaxeCache = PickaxeStateCache.Instance;
            if (pickaxeCache != null)
            {
                pickaxeCache.OnChanged += HandleCacheChanged;
                cacheSubscribed = true;
            }
        }

        private void HandleCacheChanged()
        {
            SyncSlotsFromCache();
            RefreshData();
        }

        private void OnDestroy()
        {
            if (cacheSubscribed && pickaxeCache != null)
            {
                pickaxeCache.OnChanged -= HandleCacheChanged;
                cacheSubscribed = false;
            }

            if (!subscribed || messageHandler == null) return;

            messageHandler.OnHandshakeResult -= HandleHandshake;
            messageHandler.OnUserDataSnapshot -= HandleSnapshot;
            messageHandler.OnAllSlotsResponse -= HandleAllSlotsResponse;
            messageHandler.OnUpgradeResult -= HandleUpgradeResult;
            messageHandler.OnCurrencyUpdate -= HandleCurrencyUpdate;
            messageHandler.OnMiningComplete -= HandleMiningComplete;
            subscribed = false;
        }

        private void HandleHandshake(HandshakeResponse res)
        {
            if (res?.Success == true)
            {
                if (res.Snapshot != null)
                {
                    HandleSnapshot(res.Snapshot);
                }

                messageHandler?.RequestAllSlots();
            }
        }

        private void HandleSnapshot(UserDataSnapshot snapshot)
        {
            if (snapshot == null) return;

            if (snapshot.Gold.HasValue)
            {
                currentGold = snapshot.Gold.Value;
                hasGoldInfo = true;
            }

            pickaxeCache?.UpdateFromSnapshot(snapshot);
            SyncSlotsFromCache();
            hasSlotData = slotInfos.Count > 0;
            RefreshData();
        }

        private void HandleAllSlotsResponse(AllSlotsResponse response)
        {
            if (response == null) return;

            pickaxeCache?.UpdateFromAllSlots(response);
            SyncSlotsFromCache();
            hasSlotData = slotInfos.Count > 0;
            RefreshData();
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            if (update?.Gold.HasValue == true)
            {
                currentGold = update.Gold.Value;
                hasGoldInfo = true;
                RefreshData();
            }
        }

        private void HandleUpgradeResult(UpgradeResult result)
        {
            upgradeInProgress = false;
            ulong prevSlotDps = 0;
            if (result != null && slotInfos.TryGetValue(result.SlotIndex, out var prevSlot) && prevSlot != null)
            {
                prevSlotDps = prevSlot.Dps;
            }

            if (result == null)
            {
                hasLastResultRates = false;
                RefreshData();
                return;
            }

            lastResultSlot = result.SlotIndex;
            lastResultBaseRateBp = result.BaseRateBp;
            lastResultFinalRateBp = result.FinalRateBp;
            lastResultPityBonusBp = result.PityBonus;
            hasLastResultRates = true;

            if (result.Success)
            {
                currentGold = result.RemainingGold;
                hasGoldInfo = true;

                pickaxeCache?.UpdateFromUpgradeResult(result);
                SyncSlotsFromCache();

                // 성공 직후 현재 슬롯 상태/재화/UI 재계산
                RefreshData();

                // 서버 상태와 완전 동기화를 위해 최신 슬롯 상태 요청
                messageHandler?.RequestAllSlots();
            }
            else
            {
                Debug.Log($"UpgradeTabController: 강화 실패 - {result.ErrorCode}");
                // 실패 시에도 서버가 내려준 보너스/최종 확률을 즉시 반영
                pickaxeCache?.UpdateFromUpgradeResult(result);
                SyncSlotsFromCache();
            }

            PlayUpgradeFx(result, prevSlotDps);
            RefreshData();
        }

        private void HandleMiningComplete(MiningComplete complete)
        {
            currentGold = complete.TotalGold;
            hasGoldInfo = true;
            RefreshData();
        }

        private void OnSlotButtonClicked(uint slotIndex)
        {
            if (!IsSlotUnlocked(slotIndex))
            {
                Debug.LogWarning($"UpgradeTabController: 슬롯 {slotIndex} 은(는) 잠금 상태입니다.");
                return;
            }

            selectedSlotIndex = slotIndex;
            RefreshData();
        }

        /// <summary>
        /// 강화 버튼 클릭 -> 서버에 강화 요청 전송
        /// </summary>
        private void OnUpgradeClicked()
        {
            if (messageHandler == null)
            {
                Debug.LogWarning("UpgradeTabController: MessageHandler가 없습니다. 강화 요청을 보낼 수 없습니다.");
                return;
            }

            if (!IsSlotUnlocked(selectedSlotIndex))
            {
                Debug.LogWarning("UpgradeTabController: 잠금된 슬롯은 강화할 수 없습니다.");
                return;
            }

            upgradeInProgress = true;
            UpdateButtonState();
            messageHandler.RequestUpgrade(selectedSlotIndex);
        }

        /// <summary>
        /// 광고 할인 TODO 영역
        /// </summary>
        private void OnAdDiscountClicked()
        {
            Debug.Log("UpgradeTabController: 광고 할인 버튼 클릭됨 (TODO)");
        }

        public override void RefreshData()
        {
            if (!hasSlotData)
            {
                UpdateLevelPlaceholder();
                UpdateAttackPlaceholder();
                UpdateCostText();
                baseRateBp = 0;
                pityBonusBp = 0;
                finalRateBp = 0;
                UpdateChanceText();
                if (upgradeButton != null) upgradeButton.interactable = false;
                return;
            }

            var slot = GetSlot(selectedSlotIndex);
            var unlocked = IsSlotUnlocked(selectedSlotIndex);

            if (!unlocked)
            {
                currentLevel = 0;
                currentAttack = 0;
                nextAttack = 0;
                upgradeCost = 0;
                upgradeChancePercent = 0f;
                baseRateBp = 0;
                pityBonusBp = 0;
                finalRateBp = 0;
            }
            else if (slot != null)
            {
                currentLevel = slot.Level;
                currentAttack = slot.AttackPower;
                baseRateBp = 0;
                pityBonusBp = slot.PityBonus;
                finalRateBp = 0;

                if (currentAttack == 0 && metaResolver.TryGetLevel(slot.Level, selectedSlotIndex, out var curMeta))
                {
                    currentAttack = curMeta.AttackPower;
                }

                if (metaResolver.TryGetLevel(slot.Level + 1, selectedSlotIndex, out var nextMeta))
                {
                    upgradeCost = nextMeta.Cost;
                    nextAttack = nextMeta.AttackPower > 0 ? nextMeta.AttackPower : Math.Max(currentAttack, slot.AttackPower);
                    if (nextMeta.Tier > 0)
                    {
                        baseRateBp = metaResolver.GetBaseRateBpForTier(nextMeta.Tier);
                    }
                }
                else
                {
                    upgradeCost = 0;
                    upgradeChancePercent = 0f;
                    nextAttack = Math.Max(currentAttack, slot.AttackPower);
                }

                // 메타에 티어가 없으면 슬롯/레벨 기반으로 티어 계산
                if (baseRateBp == 0)
                {
                    var tier = tierResolver.ResolveTier(selectedSlotIndex, slot.Level, slot.Tier);
                    baseRateBp = metaResolver.GetBaseRateBpForTier(tier);
                }

                finalRateBp = metaResolver.ClampRateBp(baseRateBp + pityBonusBp);
                upgradeChancePercent = finalRateBp / 100f;
            }

            // 서버가 직전에 내려준 확률 정보가 있으면 우선 사용 (즉시 반영)
            if (hasLastResultRates && lastResultSlot == selectedSlotIndex)
            {
                if (lastResultBaseRateBp > 0) baseRateBp = lastResultBaseRateBp;
                if (lastResultPityBonusBp > 0 || lastResultFinalRateBp > 0)
                {
                    pityBonusBp = lastResultPityBonusBp;
                }

                if (lastResultFinalRateBp > 0)
                {
                    finalRateBp = metaResolver.ClampRateBp(lastResultFinalRateBp);
                }
                else
                {
                    finalRateBp = metaResolver.ClampRateBp(baseRateBp + pityBonusBp);
                }

                upgradeChancePercent = finalRateBp / 100f;
            }
            else if (finalRateBp == 0)
            {
                finalRateBp = metaResolver.ClampRateBp(baseRateBp + pityBonusBp);
                upgradeChancePercent = finalRateBp / 100f;
            }

            UpdateLevelText(unlocked);
            UpdateAttackText();
            UpdateCostText();
            UpdateChanceText();
            UpdateButtonState();
            UpdateSlotButtonsInteractivity();
            UpdatePickaxeImages(slot);
        }

        private void UpdateLevelText(bool unlocked)
        {
            if (pickaxeLevelText == null) return;
            pickaxeLevelText.text = unlocked
                ? $"레벨 {currentLevel} → {currentLevel + 1}"
                : "곡괭이 레벨: 잠금";
        }

        private void UpdateLevelPlaceholder()
        {
            if (pickaxeLevelText != null)
            {
                pickaxeLevelText.text = "레벨: 데이터 수신 중...";
            }
        }

        private void UpdateAttackText()
        {
            if (currentDPSText != null)
            {
                currentDPSText.text = $"현재 공격력 : {currentAttack:N0}";
            }

            if (nextDPSText != null)
            {
                var increase = currentAttack > 0 ? ((float)nextAttack - currentAttack) / currentAttack * 100f : 0f;
                nextDPSText.text = $"다음 공격력 : {nextAttack:N0} (+{increase:F1}%)";
            }
        }

        private void UpdateAttackPlaceholder()
        {
            if (currentDPSText != null)
            {
                currentDPSText.text = "현재 공격력 : -";
            }

            if (nextDPSText != null)
            {
                nextDPSText.text = "다음 공격력 : -";
            }
        }

        private void UpdateCostText()
        {
            if (upgradeCostText == null) return;

            if (upgradeCost > 0)
            {
                upgradeCostText.text = $"강화 비용: {upgradeCost:N0} 골드";
            }
            else
            {
                upgradeCostText.text = metaResolver.HasRows
                    ? "강화 비용: 정보 없음"
                    : "강화 비용: 메타 없음";
            }
        }

        private void UpdateChanceText()
        {
            if (upgradeChanceText == null) return;

            if (finalRateBp > 0)
            {
                float finalPercent = finalRateBp / 100f;
                float basePercent = baseRateBp / 100f;
                float bonusPercent = pityBonusBp / 100f;
                upgradeChanceText.text = $"강화 확률: {finalPercent:F2}% (기본 {basePercent:F2}% + 보너스 {bonusPercent:F2}%)";
                return;
            }

            if (upgradeChancePercent > 0f)
            {
                upgradeChanceText.text = $"강화 확률: {upgradeChancePercent:F2}%";
                return;
            }

            upgradeChanceText.text = metaResolver.HasRows
                ? "강화 확률: 정보 없음"
                : "강화 확률: 메타 없음";
        }

        private void PlayUpgradeFx(UpgradeResult result, ulong prevSlotDps)
        {
            if (upgradeButton == null || result == null) return;

            var target = upgradeButton.transform as RectTransform;
            if (target == null) return;

            StopButtonFxCoroutines();
            var color = result.Success ? successFxColor : failFxColor;
            if (result.Success)
            {
                StopFxImage(failFxImage, ref failFxCoroutine);
                popFlashCoroutine = StartCoroutine(PopAndFlash(target, color, popScale, popDuration, flashDuration));
                ShowFxImage(successFxImage, fxImageShowTime, fxImageFadeTime, ref successFxCoroutine);
                blinkTextsCoroutine = StartCoroutine(BlinkTexts(new[]
                {
                    pickaxeLevelText,
                    currentDPSText,
                    nextDPSText,
                    upgradeChanceText
                }, 3, 0.08f));
            }
            else
            {
                StopFxImage(successFxImage, ref successFxCoroutine);
                flashShakeCoroutine = StartCoroutine(FlashAndShake(target, color, flashDuration, shakeDuration, shakeMagnitude));
                ShowFxImage(failFxImage, fxImageShowTime, fxImageFadeTime, ref failFxCoroutine);
            }
        }

        private System.Collections.IEnumerator PopAndFlash(RectTransform target, Color color, float scale, float popTime, float flashTime)
        {
            var img = target.GetComponent<Image>();
            Color? originalColor = img != null ? img.color : (Color?)null;
            var originalScale = target.localScale;

            float elapsed = 0f;
            while (elapsed < popTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popTime);
                float s = Mathf.Lerp(1f, scale, t);
                target.localScale = originalScale * s;
                if (img != null) img.color = Color.Lerp(originalColor ?? color, color, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < popTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popTime);
                float s = Mathf.Lerp(scale, 1f, t);
                target.localScale = originalScale * s;
                if (img != null) img.color = Color.Lerp(color, originalColor ?? Color.white, t);
                yield return null;
            }

            // 짧은 플래시 유지
            if (flashTime > 0f && img != null)
            {
                img.color = color;
                yield return new WaitForSeconds(flashTime);
                if (img != null && originalColor.HasValue) img.color = originalColor.Value;
            }

            target.localScale = originalScale;

            popFlashCoroutine = null;
        }

        private System.Collections.IEnumerator FlashAndShake(RectTransform target, Color color, float flashTime, float shakeTime, float magnitude)
        {
            var img = target.GetComponent<Image>();
            Color? originalColor = img != null ? img.color : (Color?)null;
            var originalPos = target.anchoredPosition;

            float elapsed = 0f;
            while (elapsed < shakeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shakeTime);
                float strength = Mathf.Lerp(magnitude, 0f, t);
                var offset = new Vector2(
                    UnityEngine.Random.Range(-strength, strength),
                    UnityEngine.Random.Range(-strength, strength)
                );
                target.anchoredPosition = originalPos + offset;

                if (img != null)
                {
                    img.color = Color.Lerp(originalColor ?? color, color, 0.5f);
                }

                yield return null;
            }

            target.anchoredPosition = originalPos;
            if (img != null && originalColor.HasValue)
            {
                img.color = originalColor.Value;
            }

            flashShakeCoroutine = null;
        }

        private System.Collections.IEnumerator BlinkTexts(IEnumerable<TextMeshProUGUI> labels, int times, float interval)
        {
            if (labels == null) yield break;

            var cached = new List<(TextMeshProUGUI label, Color color)>();
            foreach (var label in labels)
            {
                if (label != null)
                {
                    cached.Add((label, label.color));
                }
            }

            if (cached.Count == 0) yield break;
            interval = Mathf.Max(0.02f, interval);
            times = Mathf.Max(1, times);

            for (int i = 0; i < times; i++)
            {
                foreach (var entry in cached)
                {
                    var c = entry.color;
                    c.a = Mathf.Clamp01(c.a * 0.6f);
                    entry.label.color = c;
                }
                yield return new WaitForSeconds(interval);

                foreach (var entry in cached)
                {
                    entry.label.color = entry.color;
                }
                yield return new WaitForSeconds(interval);
            }

            foreach (var entry in cached)
            {
                entry.label.color = entry.color;
            }

            blinkTextsCoroutine = null;
        }

        private void StopButtonFxCoroutines()
        {
            if (popFlashCoroutine != null)
            {
                StopCoroutine(popFlashCoroutine);
                popFlashCoroutine = null;
            }
            if (flashShakeCoroutine != null)
            {
                StopCoroutine(flashShakeCoroutine);
                flashShakeCoroutine = null;
            }
            if (blinkTextsCoroutine != null)
            {
                StopCoroutine(blinkTextsCoroutine);
                blinkTextsCoroutine = null;
            }

            RestoreButtonVisuals();
            RestoreLabelColors();
        }

        private void RestoreButtonVisuals()
        {
            if (upgradeButtonRect != null)
            {
                upgradeButtonRect.localScale = upgradeButtonDefaultScale;
                upgradeButtonRect.anchoredPosition = upgradeButtonDefaultPos;
            }

            if (upgradeButtonImage != null && upgradeButtonDefaultColor.HasValue)
            {
                upgradeButtonImage.color = upgradeButtonDefaultColor.Value;
            }
        }

        private void RestoreLabelColors()
        {
            if (labelDefaultColors.Count == 0) return;
            foreach (var entry in labelDefaultColors)
            {
                if (entry.label != null)
                {
                    entry.label.color = entry.color;
                }
            }
        }

        private void ShowFxImage(GameObject fxObject, float showTime, float fadeTime, ref Coroutine routine)
        {
            if (fxObject == null) return;
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            var cg = fxObject.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = fxObject.AddComponent<CanvasGroup>();
            }

            cg.alpha = 1f;
            fxObject.SetActive(true);
            fxObject.transform.SetAsLastSibling();
            routine = StartCoroutine(FadeOutFxImage(fxObject, cg, showTime, fadeTime));
        }

        private void StopFxImage(GameObject fxObject, ref Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (fxObject != null)
            {
                var cg = fxObject.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                }
                fxObject.SetActive(false);
            }
        }

        private void ResetAllFxVisuals()
        {
            StopButtonFxCoroutines();
            StopFxImage(successFxImage, ref successFxCoroutine);
            StopFxImage(failFxImage, ref failFxCoroutine);
        }

        private System.Collections.IEnumerator FadeOutFxImage(GameObject fxObject, CanvasGroup cg, float showTime, float fadeTime)
        {
            showTime = Mathf.Max(0.01f, showTime);
            fadeTime = Mathf.Max(0.05f, fadeTime);

            if (showTime > 0f)
            {
                yield return new WaitForSeconds(showTime);
            }

            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeTime);
                if (cg != null) cg.alpha = 1f - t;
                yield return null;
            }

            if (cg != null) cg.alpha = 0f;
        }

        private void UpdateButtonState()
        {
            if (upgradeButton == null) return;

            bool unlocked = IsSlotUnlocked(selectedSlotIndex);
            bool hasCost = upgradeCost > 0;
            bool hasGold = !hasGoldInfo || (hasCost && currentGold >= upgradeCost);
            upgradeButton.interactable = unlocked && !upgradeInProgress && hasCost && hasGold;
        }

        private void UpdateSlotButtonsInteractivity()
        {
            SetSlotButtonState(pickaxeSlot1Button, 0, IsSlotUnlocked(0));
            SetSlotButtonState(pickaxeSlot2Button, 1, IsSlotUnlocked(1));
            SetSlotButtonState(pickaxeSlot3Button, 2, IsSlotUnlocked(2));
            SetSlotButtonState(pickaxeSlot4Button, 3, IsSlotUnlocked(3));
        }

        private void SetSlotButtonState(Button button, uint slotIndex, bool unlocked)
        {
            if (button == null) return;

            button.interactable = unlocked;
            var graphic = button.targetGraphic as Image;
            if (graphic != null)
            {
                if (!unlocked)
                {
                    graphic.color = lockedSlotColor;
                }
                else
                {
                    graphic.color = slotIndex == selectedSlotIndex ? selectedSlotColor : unlockedSlotColor;
                }
            }
        }

        private void UpdatePickaxeImages(PickaxeSlotInfo slot)
        {
            uint currentTier = slot?.Tier ?? 1;
            currentTier = tierResolver.ResolveTier(selectedSlotIndex, currentLevel, currentTier);

            uint nextTier = currentTier;
            if (metaResolver.TryGetLevel(currentLevel + 1, selectedSlotIndex, out var nextMeta) && nextMeta.Tier > 0)
            {
                nextTier = Math.Max(nextTier, nextMeta.Tier);
            }

            var sprite = GetSpriteForTier(currentTier);
            var color = IsSlotUnlocked(selectedSlotIndex) ? unlockedSlotColor : lockedSlotColor;

            if (currentPickaxeImage != null)
            {
                currentPickaxeImage.sprite = sprite;
                currentPickaxeImage.color = color;
            }

            if (nextPickaxeImage != null)
            {
                var nextSprite = GetSpriteForTier(nextTier);
                nextPickaxeImage.sprite = nextSprite;
                nextPickaxeImage.color = color;
            }
        }

        private void SyncSlotsFromCache()
        {
            if (pickaxeCache == null) return;

            slotInfos.Clear();
            foreach (var kvp in pickaxeCache.Slots)
            {
                if (kvp.Value != null)
                {
                    slotInfos[kvp.Key] = kvp.Value;
                }
            }

            hasSlotData = slotInfos.Count > 0;
        }

        private void MergeSlotInfos(IEnumerable<PickaxeSlotInfo> slots)
        {
            if (slots == null) return;

            foreach (var slot in slots)
            {
                if (slot == null) continue;
                slotInfos[slot.SlotIndex] = slot;
            }
            hasSlotData = slotInfos.Count > 0;
        }

        private PickaxeSlotInfo GetSlot(uint slotIndex)
        {
            if (slotInfos.TryGetValue(slotIndex, out var slot))
            {
                return slot;
            }

            // 슬롯 0은 기본 해금이라고 가정
            if (slotIndex == 0)
            {
                var created = new PickaxeSlotInfo
                {
                    SlotIndex = 0,
                    Level = 0,
                    Tier = 1,
                    AttackPower = 0,
                    AttackSpeedX100 = 100,
                    CriticalDamage = 15000,
                    CriticalHitPercent = 500,
                    Dps = 0,
                    PityBonus = 0,
                    IsUnlocked = true
                };
                slotInfos[slotIndex] = created;
                return created;
            }

            return null;
        }

        private bool IsSlotUnlocked(uint slotIndex)
        {
            if (slotInfos.TryGetValue(slotIndex, out var slot))
            {
                return slot.IsUnlocked;
            }

            return slotIndex == 0;
        }

        private void RequestAllSlotsIfNeeded()
        {
            if (slotInfos.Count == 0 && (pickaxeCache == null || pickaxeCache.Slots.Count == 0) && messageHandler != null)
            {
                messageHandler.RequestAllSlots();
            }
        }

        private Sprite GetSpriteForTier(uint tier)
        {
            if (SpriteAtlasCache.TryGetPickaxeSprite(tier, out var sprite))
            {
                return sprite;
            }

            if (pickaxeTierSprites != null)
            {
                foreach (var entry in pickaxeTierSprites)
                {
                    if (entry.tier == tier && entry.sprite != null)
                    {
                        return entry.sprite;
                    }
                }
            }

            runtimeWhiteSprite = SpriteAtlasCache.GetFallbackSprite();
            return runtimeWhiteSprite;
        }

        [Serializable]
        private struct TierSprite
        {
            public uint tier;
            public Sprite sprite;
        }

        public void SetSelectedSlot(uint slotIndex)
        {
            if (slotIndex > 3) return;

            selectedSlotIndex = slotIndex;
            if (isActive)
            {
                RefreshData();
            }
        }

        #region Meta Resolver

        private sealed class UpgradeMetaResolver
        {
            private bool initialized;
            private bool warnedNoMeta;
            private readonly List<MetaRow> rows = new List<MetaRow>();
            private readonly Dictionary<uint, uint> baseRateByTierBp = new Dictionary<uint, uint>();
            private uint minRateBp;
            private uint bonusRateBp;

            public bool HasRows => rows.Count > 0;

            public bool TryGet(uint slotIndex, uint nextLevel, out MetaRow row)
            {
                EnsureInitialized();

                row = rows.Find(r =>
                    r.Level == nextLevel &&
                    (!r.SlotIndex.HasValue || r.SlotIndex.Value == slotIndex));

                return row != null;
            }

            public bool TryGetLevel(uint level, uint slotIndex, out MetaRow row)
            {
                EnsureInitialized();
                row = rows.Find(r =>
                    r.Level == level &&
                    (!r.SlotIndex.HasValue || r.SlotIndex.Value == slotIndex));
                return row != null;
            }

            private void EnsureInitialized()
            {
                if (initialized) return;
                initialized = true;

                if (!MetaRepository.Loaded || MetaRepository.Data == null)
                {
                    Debug.LogWarning("UpgradeMetaResolver: MetaRepository가 로드되지 않았습니다.");
                    return;
                }

                LoadUpgradeRules(MetaRepository.Data);
                TryAddFromKey(MetaRepository.Data, "pickaxe_upgrades");
                TryAddFromNested(MetaRepository.Data, "pickaxe", "upgrades");
                TryAddFromNested(MetaRepository.Data, "pickaxe", "levels");
                TryAddFromKey(MetaRepository.Data, "pickaxe_levels");

                if (rows.Count == 0 && !warnedNoMeta)
                {
                    warnedNoMeta = true;
                    Debug.LogWarning("UpgradeMetaResolver: 메타 데이터에서 pickaxe 업그레이드 정보를 찾지 못했습니다.");
                }
            }

            private void LoadUpgradeRules(IReadOnlyDictionary<string, object> data)
            {
                if (!data.TryGetValue("upgrade_rules", out var rulesObj) || rulesObj is not Dictionary<string, object> rulesDict)
                {
                    return;
                }

                if (TryGetUIntValue(rulesDict, new[] { "min_rate", "minRateBp" }, out var minRate))
                {
                    minRateBp = minRate;
                }

                if (TryGetUIntValue(rulesDict, new[] { "bonus_rate", "bonus_rate_bp", "bonusRateBp" }, out var bonus))
                {
                    bonusRateBp = bonus;
                }

                if (rulesDict.TryGetValue("base_rate_by_tier", out var baseObj) && baseObj is Dictionary<string, object> tierDict)
                {
                    foreach (var kv in tierDict)
                    {
                        if (TryConvertToUInt(kv.Value, out var rate) && uint.TryParse(kv.Key, out var tier))
                        {
                            baseRateByTierBp[tier] = rate;
                        }
                    }
                }
            }

            public uint GetBaseRateBpForTier(uint tier)
            {
                EnsureInitialized();
                if (baseRateByTierBp.TryGetValue(tier, out var rate))
                {
                    return rate;
                }

                return minRateBp;
            }

            public uint GetBonusRateBp()
            {
                EnsureInitialized();
                return bonusRateBp;
            }

            public uint ClampRateBp(uint rateBp)
            {
                return Math.Min(rateBp, 10000u);
            }

            private void TryAddFromKey(IReadOnlyDictionary<string, object> data, string key)
            {
                if (data.TryGetValue(key, out var obj))
                {
                    AddRows(obj);
                }
            }

            private void TryAddFromNested(IReadOnlyDictionary<string, object> data, string parent, string child)
            {
                if (data.TryGetValue(parent, out var pickaxeObj) && pickaxeObj is Dictionary<string, object> pickaxeDict)
                {
                    if (pickaxeDict.TryGetValue(child, out var childObj))
                    {
                        AddRows(childObj);
                    }
                }
            }

            private void AddRows(object obj)
            {
                if (obj is List<object> list)
                {
                    foreach (var entry in list)
                    {
                        if (entry is Dictionary<string, object> dict)
                        {
                            var row = MetaRow.FromDictionary(dict, baseRateByTierBp, minRateBp);
                            if (row != null)
                            {
                                rows.Add(row);
                            }
                        }
                    }
                }
            }

            private static bool TryGetUIntValue(Dictionary<string, object> dict, IEnumerable<string> keys, out uint value)
            {
                foreach (var key in keys)
                {
                    if (dict.TryGetValue(key, out var obj) && TryConvertToUInt(obj, out value))
                    {
                        return true;
                    }
                }

                value = 0;
                return false;
            }

            private static bool TryConvertToUInt(object obj, out uint value)
            {
                switch (obj)
                {
                    case int i when i >= 0:
                        value = (uint)i;
                        return true;
                    case long l when l >= 0:
                        value = (uint)Math.Min(l, uint.MaxValue);
                        return true;
                    case uint u:
                        value = u;
                        return true;
                    case ulong ul:
                        value = (uint)Math.Min(ul, uint.MaxValue);
                        return true;
                    case float f when f >= 0:
                        value = (uint)f;
                        return true;
                    case double d when d >= 0:
                        value = (uint)d;
                        return true;
                    case string s when uint.TryParse(s, out var parsed):
                        value = parsed;
                        return true;
                    default:
                        value = 0;
                        return false;
                }
            }

            public sealed class MetaRow
            {
                public uint Level { get; private set; }
                public uint? SlotIndex { get; private set; }
                public ulong Cost { get; private set; }
                public float SuccessRatePercent { get; private set; }
                public ulong AttackPower { get; private set; }
                public uint Tier { get; private set; }

                public static MetaRow FromDictionary(Dictionary<string, object> dict, Dictionary<uint, uint> baseRateByTier, uint minRateBp)
                {
                    if (!TryGetUInt(dict, new[] { "level", "Level" }, out uint level))
                    {
                        return null;
                    }

                    var row = new MetaRow
                    {
                        Level = level,
                        SlotIndex = TryGetUInt(dict, new[] { "slot", "slot_index", "slotIndex" }, out var slot) ? slot : (uint?)null,
                        Cost = TryGetULong(dict, new[] { "cost", "gold_cost", "goldCost" }, out var cost) ? cost : 0,
                        Tier = ParseTier(dict),
                        AttackPower = TryGetULong(dict, new[] { "attack_power", "attackPower", "dps", "next_attack_power", "nextAttackPower" }, out var atk)
                            ? atk
                            : 0
                    };

                    if (TryGetUInt(dict, new[] { "success_bp", "successRateBp", "success_rate_bp" }, out var rateBp))
                    {
                        row.SuccessRatePercent = rateBp / 100f;
                    }
                    else if (baseRateByTier != null && baseRateByTier.TryGetValue(row.Tier, out var tierRate))
                    {
                        row.SuccessRatePercent = tierRate / 100f;
                    }
                    else if (minRateBp > 0)
                    {
                        row.SuccessRatePercent = minRateBp / 100f;
                    }

                    return row;
                }

                private static uint ParseTier(Dictionary<string, object> dict)
                {
                    if (dict.TryGetValue("tier", out var obj))
                    {
                        switch (obj)
                        {
                            case string s when s.StartsWith("T", StringComparison.OrdinalIgnoreCase) && uint.TryParse(s.Substring(1), out var parsed):
                                return parsed;
                            case string s when uint.TryParse(s, out var parsed2):
                                return parsed2;
                            case uint u:
                                return u;
                            case int i when i >= 0:
                                return (uint)i;
                        }
                    }

                    return 1;
                }

                private static bool TryGetUInt(Dictionary<string, object> dict, IEnumerable<string> keys, out uint value)
                {
                    foreach (var key in keys)
                    {
                        if (dict.TryGetValue(key, out var obj) && TryConvertToUInt(obj, out value))
                        {
                            return true;
                        }
                    }

                    value = 0;
                    return false;
                }

                private static bool TryGetULong(Dictionary<string, object> dict, IEnumerable<string> keys, out ulong value)
                {
                    foreach (var key in keys)
                    {
                        if (dict.TryGetValue(key, out var obj) && TryConvertToULong(obj, out value))
                        {
                            return true;
                        }
                    }

                    value = 0;
                    return false;
                }

                private static bool TryConvertToUInt(object obj, out uint value)
                {
                    switch (obj)
                    {
                        case int i:
                            value = (uint)Math.Max(i, 0);
                            return true;
                        case long l:
                            value = (uint)Math.Max(l, 0);
                            return true;
                        case uint u:
                            value = u;
                            return true;
                        case ulong ul:
                            value = (uint)Math.Min(ul, uint.MaxValue);
                            return true;
                        case float f:
                            value = (uint)Math.Max(f, 0);
                            return true;
                        case double d:
                            value = (uint)Math.Max(d, 0);
                            return true;
                        case string s when uint.TryParse(s, out var parsed):
                            value = parsed;
                            return true;
                        default:
                            value = 0;
                            return false;
                    }
                }

                private static bool TryConvertToULong(object obj, out ulong value)
                {
                    switch (obj)
                    {
                        case int i:
                            value = (ulong)Math.Max(i, 0);
                            return true;
                        case long l:
                            value = (ulong)Math.Max(l, 0);
                            return true;
                        case uint u:
                            value = u;
                            return true;
                        case ulong ul:
                            value = ul;
                            return true;
                        case float f:
                            value = (ulong)Math.Max(f, 0);
                            return true;
                        case double d:
                            value = (ulong)Math.Max(d, 0);
                            return true;
                        case string s when ulong.TryParse(s, out var parsed):
                            value = parsed;
                            return true;
                        default:
                            value = 0;
                            return false;
                    }
                }
            }
        }

        #endregion
    }
}
