using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Metadata;
using InfinitePickaxe.Client.Net;
using InfinitePickaxe.Client.UI.Common;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class InfiniteMineSimulationViewController : MonoBehaviour
    {
        [Header("Overlay")]
        [SerializeField] private GameObject uiRootPanel;

        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI floorTitleText;
        [SerializeField] private TextMeshProUGUI mineralNameText;
        [SerializeField] private TextMeshProUGUI remainingTimeText;
        [SerializeField] private Button exitButton;
        [SerializeField] private string floorTitleFormat = "\uBB34\uD55C\uC758 \uAC31\uB3C4 {0}\uCE35";

        [Header("Exit Modal")]
        [SerializeField] private GameObject exitModal;
        [SerializeField] private Button exitConfirmButton;
        [SerializeField] private Button exitCancelButton;

        [Header("Result Modal")]
        [SerializeField] private InfiniteMineResultModalController resultModal;

        [Header("Pickaxe Slots")]
        [SerializeField] private Button pickaxeSlot1Button;
        [SerializeField] private Button pickaxeSlot2Button;
        [SerializeField] private Button pickaxeSlot3Button;
        [SerializeField] private Button pickaxeSlot4Button;
        [SerializeField] private TextMeshProUGUI slot1LevelText;
        [SerializeField] private TextMeshProUGUI slot2LevelText;
        [SerializeField] private TextMeshProUGUI slot3LevelText;
        [SerializeField] private TextMeshProUGUI slot4LevelText;
        [SerializeField] private Image pickaxeSlot1Image;
        [SerializeField] private Image pickaxeSlot2Image;
        [SerializeField] private Image pickaxeSlot3Image;
        [SerializeField] private Image pickaxeSlot4Image;
        [SerializeField] private SpriteAtlas pickaxeSpriteAtlas;

        [Header("Mineral Area")]
        [SerializeField] private Image mineralImage;
        [SerializeField] private RectTransform damageSpriteRoot;
        [SerializeField] private GameObject damageSpriteLabelPrefab;

        [Header("Damage Sprite (Floating)")]
        [SerializeField] private float damageSpriteLifetime = 0.9f;
        [SerializeField] private float damageSpriteRiseSpeed = 80f;
        [SerializeField] private Vector2 damageSpriteRandomOffset = new Vector2(60f, 0f);
        [SerializeField] private Vector2 damageDigitVerticalJitterRange = new Vector2(-6f, 6f);
        [SerializeField] private float damageDigitSpacing = -6f;
        [SerializeField] private float damageDigitMinWidth = 0f;
        [SerializeField] private float damageDigitScale = 1f;
        [SerializeField] private float damageSpriteStackSpacing = 0f;
        [SerializeField] private float criticalScale = 1.2f;

        [Header("HP Slider")]
        [SerializeField] private TextMeshProUGUI mineHPText;
        [SerializeField] private Slider mineHPSlider;
        [SerializeField] private Image mineHPSliderFill;
        [SerializeField] private Image mineHPSliderBackground;
        [SerializeField] private float hpSliderDefaultWidth = 800f;
        [SerializeField] private float hpSliderDefaultHeight = 50f;

        [Header("HP Bar Animation")]
        [SerializeField] private float fillLerpSpeed = 6f;
        [SerializeField] private float colorLerpSpeed = 4f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulsePeriodSeconds = 0f;
        [SerializeField] private float pulseAmplitude = 0.08f;
        [SerializeField] private Color lowColor = Color.red;
        [SerializeField] private Color midColor = Color.yellow;
        [SerializeField] private Color highColor = Color.green;

        [Header("Pickaxe Swing Animation")]
        [SerializeField] private float restAngle = 0f;
        [SerializeField] private float swingDownDegrees = 135f;
        [SerializeField] private float swingDuration = 1.0f;
        [SerializeField, Range(0.1f, 0.9f)] private float swingDownPortion = 0.35f;

        private const string DamageSpriteLabelResourcePath = "UI/DamageSpriteLabel";
        private const string MineralSpriteResourcePrefix = "Sprites/Mineral/";

        private static Sprite runtimeDefaultSprite;

        private MessageHandler messageHandler;
        private PickaxeStateCache pickaxeCache;
        private InfiniteMineMetaResolver infiniteMineMeta;
        private MineralInfoMetaResolver mineralInfoMeta;
        private readonly PickaxeTierResolver tierResolver = new PickaxeTierResolver();
        private readonly Dictionary<uint, PickaxeSlotInfo> slotInfos = new Dictionary<uint, PickaxeSlotInfo>();
        private readonly Dictionary<string, Sprite> mineralSpriteCache = new Dictionary<string, Sprite>();
        private bool slot2Unlocked;
        private bool slot3Unlocked;
        private bool slot4Unlocked;
        private bool subscribed;
        private bool uiPanelWasActive;

        private uint currentFloor;
        private string currentMineralName = string.Empty;
        private string currentMineralSpriteKey = string.Empty;
        private ulong currentHp;
        private ulong maxHp;
        private bool hasActiveChallenge;

        private float targetFillNormalized = 1f;
        private float displayedFillNormalized = 1f;
        private float safeMaxForDisplay = 1f;
        private Color currentFillColor = Color.green;
        private bool hpLayoutFixed;

        private ulong remainingMsAtSync;
        private ulong lastServerTimestampMs;
        private float lastLocalSyncTime;

        private readonly List<DamageSpriteEntry> activeDamageSprites = new List<DamageSpriteEntry>();
        private readonly Queue<DamageSpriteLabel> damageSpritePool = new Queue<DamageSpriteLabel>();
        private readonly List<float> damageDigitWidths = new List<float>(16);
        private readonly Sprite[] damageNormalSprites = new Sprite[10];
        private readonly Sprite[] damageCriticalSprites = new Sprite[10];
        private bool damageFontLoaded;
        private readonly PickaxeSwingState[] swingStates = new PickaxeSwingState[4];

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
            RegisterPickaxeAtlas();
        }

        private void OnEnable()
        {
            EnsureReferences();
            BindButtons();
            RegisterPickaxeAtlas();
            Subscribe();
            AutoBindResultModal();
            HideUiPanel();
            FixHpSliderLayout();
            SyncSlotsFromCache();
            UpdateSlotLevels();
            UpdatePickaxeSlotSprites();
            UpdateTopBar();
            UpdateRemainingTimeText();
            if (exitModal != null)
            {
                exitModal.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreUiPanel();
            if (resultModal != null)
            {
                resultModal.Hide();
            }
            if (exitModal != null)
            {
                exitModal.SetActive(false);
            }
        }

        private void Update()
        {
            if (hasActiveChallenge)
            {
                AnimateHPBar();
            }

            UpdatePickaxeSwings();
            UpdateDamageSpriteAnimations();
            UpdateRemainingTimeText();
        }

        public void Show()
        {
            EnsureReferences();
            BindButtons();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void ApplyStartResult(InfiniteMineChallengeStartResult result)
        {
            if (result == null || !result.Success) return;

            currentFloor = result.Floor;
            currentHp = result.CurrentHp;
            maxHp = result.MaxHp;
            hasActiveChallenge = true;
            SyncRemainingTimer(result.RemainingMs, 0);
            UpdateMineralMeta(currentFloor);
            UpdateTopBar();
            UpdateHPBar();
            UpdateMineralSprite();
            ClearDamageSprites();
            if (resultModal != null)
            {
                resultModal.Hide();
            }
        }

        private void Subscribe()
        {
            if (subscribed) return;

            messageHandler ??= MessageHandler.Instance;
            pickaxeCache = PickaxeStateCache.Instance;

            if (messageHandler != null)
            {
                messageHandler.OnInfiniteMineChallengeStartResult += HandleChallengeStartResult;
                messageHandler.OnInfiniteMineChallengeUpdate += HandleChallengeUpdate;
                messageHandler.OnInfiniteMineChallengeResult += HandleChallengeResult;
            }

            if (pickaxeCache != null)
            {
                pickaxeCache.OnChanged += HandlePickaxeCacheChanged;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;

            if (messageHandler != null)
            {
                messageHandler.OnInfiniteMineChallengeStartResult -= HandleChallengeStartResult;
                messageHandler.OnInfiniteMineChallengeUpdate -= HandleChallengeUpdate;
                messageHandler.OnInfiniteMineChallengeResult -= HandleChallengeResult;
            }

            if (pickaxeCache != null)
            {
                pickaxeCache.OnChanged -= HandlePickaxeCacheChanged;
            }

            subscribed = false;
        }

        private void HandleChallengeStartResult(InfiniteMineChallengeStartResult result)
        {
            ApplyStartResult(result);
        }

        private void HandleChallengeUpdate(InfiniteMineChallengeUpdate update)
        {
            if (update == null) return;

            currentFloor = update.Floor;
            currentHp = update.CurrentHp;
            maxHp = update.MaxHp;
            hasActiveChallenge = true;
            SyncRemainingTimer(update.RemainingMs, update.ServerTimestamp);

            UpdateMineralMeta(currentFloor);
            UpdateTopBar();
            UpdateHPBar();
            UpdateMineralSprite();

            if (update.Attacks != null && update.Attacks.Count > 0)
            {
                foreach (var attack in update.Attacks)
                {
                    TriggerPickaxeAttackAnimation(attack.SlotIndex, attack.Damage, attack.IsCritical);
                }
            }
        }

        private void HandleChallengeResult(InfiniteMineChallengeResult result)
        {
            if (result == null) return;
            hasActiveChallenge = false;
            remainingMsAtSync = 0;
            lastServerTimestampMs = 0;
            UpdateRemainingTimeText();
            AutoBindResultModal();
            if (resultModal != null)
            {
                resultModal.SetSimulationView(this);
                resultModal.Show(result);
            }
        }

        private void HandlePickaxeCacheChanged()
        {
            SyncSlotsFromCache();
            UpdateSlotLevels();
            UpdatePickaxeSlotSprites();
        }

        private void UpdateTopBar()
        {
            if (floorTitleText != null)
            {
                var format = string.IsNullOrEmpty(floorTitleFormat)
                    ? "\uBB34\uD55C\uC758 \uAC31\uB3C4 {0}\uCE35"
                    : floorTitleFormat;
                floorTitleText.text = string.Format(format, currentFloor);
            }

            if (mineralNameText != null)
            {
                mineralNameText.text = currentMineralName ?? string.Empty;
            }

            UpdateRemainingTimeText();
        }

        private void UpdateRemainingTimeText()
        {
            if (remainingTimeText == null) return;
            if (!hasActiveChallenge)
            {
                remainingTimeText.text = "0.0s";
                return;
            }

            var remainingMs = GetRemainingMsNow();
            float seconds = Mathf.Max(0f, remainingMs / 1000f);
            remainingTimeText.text = string.Format("{0:0.0}s", seconds);
        }

        private void SyncRemainingTimer(ulong remainingMs, ulong serverTimestamp)
        {
            remainingMsAtSync = remainingMs;
            lastServerTimestampMs = serverTimestamp;
            lastLocalSyncTime = Time.unscaledTime;
        }

        private float GetRemainingMsNow()
        {
            if (remainingMsAtSync == 0) return 0f;

            long elapsedMs;
            if (lastServerTimestampMs > 0 && ServerTimeCache.Instance.HasServerTime)
            {
                elapsedMs = ServerTimeCache.Instance.NowMs - (long)lastServerTimestampMs;
            }
            else
            {
                elapsedMs = (long)((Time.unscaledTime - lastLocalSyncTime) * 1000f);
            }

            long remaining = (long)remainingMsAtSync - elapsedMs;
            if (remaining < 0) remaining = 0;
            return remaining;
        }

        private void UpdateHPBar()
        {
            FixHpSliderLayout();

            if (mineHPSlider != null)
            {
                var rt = mineHPSlider.GetComponent<RectTransform>();
                if (rt != null && (rt.sizeDelta.x <= 0 || rt.sizeDelta.y <= 0))
                {
                    rt.sizeDelta = new Vector2(hpSliderDefaultWidth, hpSliderDefaultHeight);
                }
            }

            double max = maxHp > 0 ? maxHp : (currentHp > 0 ? currentHp : 1);
            safeMaxForDisplay = (float)Math.Min(max, float.MaxValue);
            double ratio = max > 0 ? currentHp / max : 0;
            targetFillNormalized = Mathf.Clamp01((float)ratio);

            if (mineHPText != null)
            {
                double hpPercent = maxHp > 0 ? (currentHp / (double)maxHp * 100d) : 0d;
                string currentHpText = currentHp.ToString("N0");
                string maxHpText = maxHp.ToString("N0");
                mineHPText.text = $"HP: {currentHpText}/{maxHpText} ({hpPercent:F1}%)";

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

        private void AnimateHPBar()
        {
            displayedFillNormalized = Mathf.Lerp(displayedFillNormalized, targetFillNormalized, Time.deltaTime * fillLerpSpeed);
            displayedFillNormalized = Mathf.Clamp01(displayedFillNormalized);

            if (mineHPSliderFill != null)
            {
                mineHPSliderFill.fillAmount = displayedFillNormalized;
            }

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
            if (normalized < 0.5f)
            {
                float t = normalized / 0.5f;
                return Color.Lerp(lowColor, midColor, t);
            }

            float t2 = (normalized - 0.5f) / 0.5f;
            return Color.Lerp(midColor, highColor, t2);
        }

        private void FixHpSliderLayout()
        {
            if (hpLayoutFixed) return;

            if (mineHPSliderFill != null)
            {
                mineHPSliderFill.type = Image.Type.Filled;
                mineHPSliderFill.fillMethod = Image.FillMethod.Horizontal;
                mineHPSliderFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                mineHPSliderFill.fillCenter = true;
                if (mineHPSliderFill.sprite == null)
                {
                    mineHPSliderFill.sprite = GetRuntimeDefaultSprite();
                }
                var rt = mineHPSliderFill.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }

            if (mineHPSlider != null)
            {
                mineHPSlider.minValue = 0f;
                mineHPSlider.maxValue = 1f;
                mineHPSlider.value = 1f;

                var fillRect = mineHPSlider.fillRect;
                if (fillRect != null)
                {
                    fillRect.anchorMin = new Vector2(0f, 0f);
                    fillRect.anchorMax = new Vector2(1f, 1f);
                    fillRect.pivot = new Vector2(0.5f, 0.5f);
                    fillRect.anchoredPosition = Vector2.zero;
                    fillRect.sizeDelta = Vector2.zero;
                }
            }

            hpLayoutFixed = true;
        }

        private static Sprite GetRuntimeDefaultSprite()
        {
            if (runtimeDefaultSprite != null) return runtimeDefaultSprite;
            var tex = Texture2D.whiteTexture;
            runtimeDefaultSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            runtimeDefaultSprite.name = "RuntimeWhiteSprite";
            return runtimeDefaultSprite;
        }

        private void UpdateMineralMeta(uint floor)
        {
            EnsureMeta();
            currentMineralName = string.Empty;
            currentMineralSpriteKey = string.Empty;

            if (infiniteMineMeta != null && infiniteMineMeta.TryGetFloor(floor, out var floorMeta))
            {
                if (mineralInfoMeta != null && mineralInfoMeta.TryGetMineral(floorMeta.MineralInfoId, out var mineralMeta))
                {
                    currentMineralName = mineralMeta.Name ?? string.Empty;
                    currentMineralSpriteKey = mineralMeta.SpriteKey ?? string.Empty;
                }
            }
        }

        private void UpdateMineralSprite()
        {
            if (mineralImage == null) return;

            if (string.IsNullOrEmpty(currentMineralSpriteKey))
            {
                mineralImage.sprite = null;
                mineralImage.enabled = false;
                return;
            }

            mineralImage.sprite = LoadMineralSprite(currentMineralSpriteKey);
            mineralImage.enabled = mineralImage.sprite != null;
            mineralImage.type = Image.Type.Simple;
            mineralImage.preserveAspect = true;
            var scale = mineralImage.rectTransform.localScale;
            mineralImage.rectTransform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
        }

        private Sprite LoadMineralSprite(string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey)) return null;
            if (mineralSpriteCache.TryGetValue(spriteKey, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>($"{MineralSpriteResourcePrefix}{spriteKey}");
            if (sprite != null)
            {
                mineralSpriteCache[spriteKey] = sprite;
            }
            return sprite;
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

            tier = tierResolver.ResolveTier(slotIndex, level, tier);

            if (!SpriteAtlasCache.TryGetPickaxeSprite(tier, out var sprite))
            {
                sprite = SpriteAtlasCache.GetFallbackSprite();
            }

            targetImage.sprite = sprite;
            targetImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        private void TriggerPickaxeAttackAnimation(uint slotIndex, ulong damage, bool isCritical)
        {
            ShowDamageSprite(damage, isCritical);
            StartPickaxeSwing(slotIndex);
        }

        private void UpdateDamageSpriteAnimations()
        {
            if (activeDamageSprites.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = activeDamageSprites.Count - 1; i >= 0; i--)
            {
                var entry = activeDamageSprites[i];
                if (entry.Label == null || entry.Label.Root == null)
                {
                    activeDamageSprites.RemoveAt(i);
                    continue;
                }

                entry.Elapsed += dt;
                float t = entry.Elapsed / entry.Lifetime;

                var rect = entry.Label.Root;
                var pos = rect.anchoredPosition;
                pos.y += entry.RiseSpeed * dt;
                rect.anchoredPosition = pos;

                if (entry.Label.Group != null)
                {
                    entry.Label.Group.alpha = Mathf.Lerp(entry.StartAlpha, 0f, t);
                }

                if (entry.Elapsed >= entry.Lifetime)
                {
                    RecycleDamageSprite(entry);
                    activeDamageSprites.RemoveAt(i);
                }
                else
                {
                    activeDamageSprites[i] = entry;
                }
            }
        }

        private void ShowDamageSprite(ulong damage, bool isCritical)
        {
            var root = damageSpriteRoot;
            if (root == null)
            {
                root = mineHPSliderBackground != null
                    ? mineHPSliderBackground.rectTransform.parent as RectTransform
                    : mineHPText?.transform.parent as RectTransform;
            }

            if (root == null) return;

            var label = GetDamageSpriteLabelInstance(root);
            if (label == null || label.Root == null) return;

            string value = damage.ToString();
            float baseHeight = ApplyDamageDigits(label, value, isCritical);

            if (label.Group != null)
            {
                label.Group.alpha = 1f;
            }

            float scale = isCritical ? criticalScale : 1f;
            float stackHeight = (baseHeight + damageSpriteStackSpacing) * scale;
            ShiftActiveDamageSpritesUp(stackHeight);
            label.Root.localScale = Vector3.one * scale;

            float rangeX = Mathf.Abs(damageSpriteRandomOffset.x);
            var offset = new Vector2(
                UnityEngine.Random.Range(-rangeX, rangeX),
                0f
            );
            label.Root.anchoredPosition = offset;

            var entry = new DamageSpriteEntry
            {
                Label = label,
                Elapsed = 0f,
                Lifetime = Mathf.Max(0.1f, damageSpriteLifetime),
                RiseSpeed = damageSpriteRiseSpeed,
                StartAlpha = label.Group != null ? label.Group.alpha : 1f
            };

            activeDamageSprites.Add(entry);
        }

        private float ApplyDamageDigits(DamageSpriteLabel label, string value, bool isCritical)
        {
            if (label == null || label.Root == null || string.IsNullOrEmpty(value)) return 0f;

            EnsureDamageFontLoaded();

            int count = value.Length;
            EnsureDamageDigitCount(label, count);

            damageDigitWidths.Clear();
            float totalWidth = 0f;
            float maxHeight = 0f;
            float jitterMin = Mathf.Min(damageDigitVerticalJitterRange.x, damageDigitVerticalJitterRange.y);
            float jitterMax = Mathf.Max(damageDigitVerticalJitterRange.x, damageDigitVerticalJitterRange.y);
            float jitterSpan = Mathf.Abs(jitterMin) + Mathf.Abs(jitterMax);

            for (int i = 0; i < count; i++)
            {
                int digit = value[i] - '0';
                var sprite = GetDamageDigitSprite(digit, isCritical);
                var img = label.Digits[i];
                if (img == null)
                {
                    damageDigitWidths.Add(0f);
                    continue;
                }

                img.sprite = sprite;
                img.enabled = sprite != null;
                img.color = Color.white;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.SetNativeSize();

                var rect = img.rectTransform;
                var size = rect.sizeDelta;
                size.x = Mathf.Max(size.x, damageDigitMinWidth);
                rect.sizeDelta = size;
                rect.localScale = Vector3.one * damageDigitScale;

                float width = size.x * damageDigitScale;
                float height = size.y * damageDigitScale;
                damageDigitWidths.Add(width);

                totalWidth += width;
                if (i < count - 1) totalWidth += damageDigitSpacing;
                if (height > maxHeight) maxHeight = height;
            }

            float cursor = -totalWidth * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var img = label.Digits[i];
                if (img == null) continue;

                float width = damageDigitWidths[i];
                float jitter = UnityEngine.Random.Range(jitterMin, jitterMax);

                var rect = img.rectTransform;
                rect.anchoredPosition = new Vector2(cursor + width * 0.5f, jitter);
                cursor += width + damageDigitSpacing;
            }

            float totalHeight = maxHeight + jitterSpan;
            label.Root.sizeDelta = new Vector2(totalWidth, totalHeight);
            return totalHeight;
        }

        private void ShiftActiveDamageSpritesUp(float offset)
        {
            if (offset <= 0f) return;

            for (int i = 0; i < activeDamageSprites.Count; i++)
            {
                var entry = activeDamageSprites[i];
                if (entry.Label == null || entry.Label.Root == null) continue;

                var pos = entry.Label.Root.anchoredPosition;
                pos.y += offset;
                entry.Label.Root.anchoredPosition = pos;
            }
        }

        private void EnsureDamageDigitCount(DamageSpriteLabel label, int count)
        {
            if (label == null) return;

            var root = label.DigitRoot != null ? label.DigitRoot : label.Root;
            if (root == null) return;

            while (label.Digits.Count < count)
            {
                Image img = null;
                if (label.DigitTemplate != null)
                {
                    img = Instantiate(label.DigitTemplate, root);
                    img.gameObject.SetActive(true);
                }
                else
                {
                    var go = new GameObject($"Digit_{label.Digits.Count}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(root, false);
                    img = go.GetComponent<Image>();
                }

                img.gameObject.name = $"Digit_{label.Digits.Count}";
                img.raycastTarget = false;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;

                var rect = img.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                label.Digits.Add(img);
            }

            for (int i = 0; i < label.Digits.Count; i++)
            {
                var img = label.Digits[i];
                if (img != null)
                {
                    img.gameObject.SetActive(i < count);
                }
            }
        }

        private DamageSpriteLabel GetDamageSpriteLabelInstance(RectTransform parent)
        {
            DamageSpriteLabel label = null;
            while (damageSpritePool.Count > 0 && label == null)
            {
                label = damageSpritePool.Dequeue();
            }

            if (label == null)
            {
                label = CreateDamageSpriteLabel(parent);
            }
            else if (label.Root != null)
            {
                label.Root.SetParent(parent, false);
            }

            if (label != null && label.Root != null)
            {
                label.Root.gameObject.SetActive(true);
            }

            return label;
        }

        private DamageSpriteLabel CreateDamageSpriteLabel(RectTransform parent)
        {
            GameObject go = null;
            if (damageSpriteLabelPrefab == null)
            {
                damageSpriteLabelPrefab = Resources.Load<GameObject>(DamageSpriteLabelResourcePath);
            }

            if (damageSpriteLabelPrefab != null)
            {
                go = Instantiate(damageSpriteLabelPrefab, parent);
            }
            else
            {
                go = new GameObject("DamageSpriteLabel", typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(parent, false);
            }

            var root = go.GetComponent<RectTransform>();
            var group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var label = new DamageSpriteLabel
            {
                Root = root,
                Group = group
            };

            var templateTf = go.transform.Find("DigitTemplate");
            if (templateTf != null)
            {
                label.DigitTemplate = templateTf.GetComponent<Image>();
                label.DigitRoot = templateTf.parent as RectTransform ?? root;
                templateTf.gameObject.SetActive(false);
            }
            else
            {
                label.DigitRoot = root;
            }

            return label;
        }

        private void EnsureDamageFontLoaded()
        {
            if (damageFontLoaded) return;

            for (int i = 0; i < 10; i++)
            {
                damageNormalSprites[i] = Resources.Load<Sprite>($"Sprites/UI/DamageFonts/damage_font_normal_{i}");
                damageCriticalSprites[i] = Resources.Load<Sprite>($"Sprites/UI/DamageFonts/damage_font_critical_{i}");
            }

            damageFontLoaded = true;
        }

        private Sprite GetDamageDigitSprite(int digit, bool isCritical)
        {
            if (digit < 0 || digit > 9) return null;
            return isCritical ? damageCriticalSprites[digit] : damageNormalSprites[digit];
        }

        private void RecycleDamageSprite(DamageSpriteEntry entry)
        {
            if (entry.Label == null || entry.Label.Root == null) return;
            entry.Label.Root.gameObject.SetActive(false);
            damageSpritePool.Enqueue(entry.Label);
        }

        private void ClearDamageSprites()
        {
            for (int i = activeDamageSprites.Count - 1; i >= 0; i--)
            {
                var entry = activeDamageSprites[i];
                RecycleDamageSprite(entry);
            }
            activeDamageSprites.Clear();
        }

        private void StartPickaxeSwing(uint slotIndex)
        {
            if (slotIndex >= swingStates.Length) return;

            float duration = Mathf.Max(0.03f, swingDuration);
            if (slotInfos.TryGetValue(slotIndex, out var info) && info.AttackSpeed > 0)
            {
                float speedMul = info.AttackSpeed / 10000f;
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
                    float td = t / downPortion;
                    float easeOut = 1f - Mathf.Pow(1f - td, 2f);
                    angle = Mathf.Lerp(restAngle, restAngle + swingDownDegrees, easeOut);
                }
                else
                {
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

        private void BindButtons()
        {
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OpenExitModal);
            }

            if (exitConfirmButton != null)
            {
                exitConfirmButton.onClick.RemoveAllListeners();
                exitConfirmButton.onClick.AddListener(ExitSimulation);
            }

            if (exitCancelButton != null)
            {
                exitCancelButton.onClick.RemoveAllListeners();
                exitCancelButton.onClick.AddListener(CloseExitModal);
            }
        }

        private void OpenExitModal()
        {
            if (exitModal == null)
            {
                ExitSimulation();
                return;
            }
            exitModal.SetActive(true);
        }

        private void CloseExitModal()
        {
            if (exitModal != null)
            {
                exitModal.SetActive(false);
            }
        }

        private void ExitSimulation()
        {
            CloseExitModal();
            messageHandler ??= MessageHandler.Instance;
            messageHandler?.RequestInfiniteMineExit();
            Hide();
        }

        private void HideUiPanel()
        {
            if (uiRootPanel == null) return;
            uiPanelWasActive = uiRootPanel.activeSelf;
            if (uiPanelWasActive)
            {
                uiRootPanel.SetActive(false);
            }
        }

        private void RestoreUiPanel()
        {
            if (uiRootPanel != null && uiPanelWasActive)
            {
                uiRootPanel.SetActive(true);
            }
        }

        private void RegisterPickaxeAtlas()
        {
            if (pickaxeSpriteAtlas != null)
            {
                SpriteAtlasCache.RegisterPickaxeAtlas(pickaxeSpriteAtlas);
            }
        }

        private void AutoBindResultModal()
        {
            if (resultModal != null)
            {
                if (resultModal.gameObject.scene.IsValid()) return;
                var instance = Instantiate(resultModal.gameObject, GetOverlayRoot());
                instance.name = "InfiniteMineResultModal";
                instance.SetActive(false);
                resultModal = instance.GetComponent<InfiniteMineResultModalController>();
                return;
            }

            var modalObj = GameObject.Find("InfiniteMineResultModal");
            if (modalObj != null)
            {
                resultModal = modalObj.GetComponent<InfiniteMineResultModalController>();
                return;
            }

            var prefab = Resources.Load<GameObject>("UI/InfiniteMineResultModal");
            if (prefab == null) return;
            var newInstance = Instantiate(prefab, GetOverlayRoot());
            newInstance.name = "InfiniteMineResultModal";
            newInstance.SetActive(false);
            resultModal = newInstance.GetComponent<InfiniteMineResultModalController>();
        }

        private Transform GetOverlayRoot()
        {
            var overlayObj = GameObject.Find("InfiniteMineOverlayCanvas");
            if (overlayObj != null) return overlayObj.transform;
            return transform.root;
        }

        private void EnsureMeta()
        {
            if (infiniteMineMeta == null)
            {
                infiniteMineMeta = new InfiniteMineMetaResolver();
            }
            else if (MetaRepository.Loaded && infiniteMineMeta.Floors.Count == 0)
            {
                infiniteMineMeta.Reload();
            }

            if (mineralInfoMeta == null)
            {
                mineralInfoMeta = new MineralInfoMetaResolver();
            }
            else if (MetaRepository.Loaded && mineralInfoMeta.Minerals.Count == 0)
            {
                mineralInfoMeta.Reload();
            }
        }

        private void EnsureReferences()
        {
            if (uiRootPanel == null)
            {
                var uiCanvas = GameObject.Find("UI Canvas");
                if (uiCanvas != null)
                {
                    var panel = uiCanvas.transform.Find("Panel");
                    if (panel != null) uiRootPanel = panel.gameObject;
                }
            }

            if (floorTitleText == null)
            {
                floorTitleText = FindText("TopBar/FloorText", "FloorText");
                if (floorTitleText == null)
                {
                    floorTitleText = FindText("TopBar/FloorTitleText", "FloorTitleText");
                }
            }

            if (mineralNameText == null)
            {
                mineralNameText = FindText("TopBar/MineralNameText", "MineralNameText");
            }

            if (remainingTimeText == null)
            {
                remainingTimeText = FindText("TopBar/RemainingTimeText", "RemainingTimeText");
            }

            if (exitButton == null)
            {
                exitButton = FindButton("TopBar/ExitButton", "ExitButton");
                if (exitButton == null)
                {
                    exitButton = FindButton("TopBar/CloseButton", "CloseButton");
                }
            }

            if (exitModal == null)
            {
                var modalTf = FindChildRecursive(transform, "ExitModal") ?? FindChildRecursive(transform, "ExitPanel");
                if (modalTf != null) exitModal = modalTf.gameObject;
            }

            if (exitConfirmButton == null && exitModal != null)
            {
                var tf = exitModal.transform.Find("ModalPanel/ExitButton")
                         ?? exitModal.transform.Find("ModalPanel/ConfirmButton")
                         ?? exitModal.transform.Find("ExitButton")
                         ?? exitModal.transform.Find("ConfirmButton");
                if (tf != null) exitConfirmButton = tf.GetComponent<Button>();
            }

            if (exitCancelButton == null && exitModal != null)
            {
                var tf = exitModal.transform.Find("ModalPanel/CancelButton")
                         ?? exitModal.transform.Find("CancelButton")
                         ?? exitModal.transform.Find("CloseButton");
                if (tf != null) exitCancelButton = tf.GetComponent<Button>();
            }

            if (pickaxeSlot1Button == null)
                pickaxeSlot1Button = FindButton("SlotsRow/PickaxeSlot1", "PickaxeSlot1");
            if (pickaxeSlot2Button == null)
                pickaxeSlot2Button = FindButton("SlotsRow/PickaxeSlot2", "PickaxeSlot2");
            if (pickaxeSlot3Button == null)
                pickaxeSlot3Button = FindButton("SlotsRow/PickaxeSlot3", "PickaxeSlot3");
            if (pickaxeSlot4Button == null)
                pickaxeSlot4Button = FindButton("SlotsRow/PickaxeSlot4", "PickaxeSlot4");

            if (slot1LevelText == null)
                slot1LevelText = FindLevelText("PickaxeSlot1");
            if (slot2LevelText == null)
                slot2LevelText = FindLevelText("PickaxeSlot2");
            if (slot3LevelText == null)
                slot3LevelText = FindLevelText("PickaxeSlot3");
            if (slot4LevelText == null)
                slot4LevelText = FindLevelText("PickaxeSlot4");

            if (pickaxeSlot1Image == null)
                pickaxeSlot1Image = GetButtonImage(pickaxeSlot1Button);
            if (pickaxeSlot2Image == null)
                pickaxeSlot2Image = GetButtonImage(pickaxeSlot2Button);
            if (pickaxeSlot3Image == null)
                pickaxeSlot3Image = GetButtonImage(pickaxeSlot3Button);
            if (pickaxeSlot4Image == null)
                pickaxeSlot4Image = GetButtonImage(pickaxeSlot4Button);

            if (mineralImage == null)
            {
                mineralImage = FindImage("MineralArea/MineralImage", "MineralImage");
                if (mineralImage == null)
                {
                    mineralImage = FindImage("MineralArea", "MineralArea");
                }
            }

            if (damageSpriteRoot == null)
            {
                var damageTf = FindChildRecursive(transform, "DamageArea");
                if (damageTf != null) damageSpriteRoot = damageTf as RectTransform;
            }

            if (mineHPText == null)
            {
                mineHPText = FindText("HPSliderContainer/MineHPText", "MineHPText");
            }

            if (mineHPSlider == null)
            {
                var sliderTf = transform.Find("HPSliderContainer/MineHPSlider") ?? transform.Find("MineHPSlider");
                if (sliderTf != null) mineHPSlider = sliderTf.GetComponent<Slider>();
            }

            if (mineHPSliderFill == null && mineHPSlider != null)
            {
                mineHPSliderFill = mineHPSlider.fillRect != null ? mineHPSlider.fillRect.GetComponent<Image>() : null;
            }

            if (mineHPSliderBackground == null && mineHPSlider != null)
            {
                mineHPSliderBackground = mineHPSlider.GetComponent<Image>();
            }
        }

        private TextMeshProUGUI FindText(string path, string fallbackName)
        {
            var target = transform.Find(path);
            if (target != null)
            {
                var text = target.GetComponent<TextMeshProUGUI>();
                if (text != null) return text;
            }

            if (string.IsNullOrEmpty(fallbackName)) return null;
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == fallbackName)
                {
                    return texts[i];
                }
            }

            return null;
        }

        private Button FindButton(string path, string fallbackName)
        {
            var target = transform.Find(path);
            if (target != null)
            {
                var button = target.GetComponent<Button>();
                if (button != null) return button;
            }

            if (string.IsNullOrEmpty(fallbackName)) return null;
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == fallbackName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private Image FindImage(string path, string fallbackName)
        {
            var target = transform.Find(path);
            if (target != null)
            {
                var image = target.GetComponent<Image>();
                if (image != null) return image;
            }

            if (string.IsNullOrEmpty(fallbackName)) return null;
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].name == fallbackName)
                {
                    return images[i];
                }
            }

            return null;
        }

        private Image GetButtonImage(Button button)
        {
            if (button == null) return null;
            if (button.targetGraphic is Image target) return target;
            return button.GetComponent<Image>();
        }

        private TextMeshProUGUI FindLevelText(string slotName)
        {
            var slot = FindChildRecursive(transform, slotName);
            if (slot == null) return null;
            var levelTf = slot.Find("LevelText");
            if (levelTf != null)
            {
                return levelTf.GetComponent<TextMeshProUGUI>();
            }
            return null;
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
                if (found != null)
                    return found;
            }
            return null;
        }

        private sealed class DamageSpriteLabel
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform DigitRoot;
            public Image DigitTemplate;
            public readonly List<Image> Digits = new List<Image>();
        }

        private struct DamageSpriteEntry
        {
            public DamageSpriteLabel Label;
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
    }
}
