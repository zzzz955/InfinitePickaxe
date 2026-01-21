using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Infinitepickaxe;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class RewardListModalController : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Panel Animation")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private bool autoAlignToCenterLine = true;
        [SerializeField] private bool playOpenAnimation = true;
        [SerializeField] private float openDuration = 0.5f;
        [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool clampToParentBounds = true;
        [SerializeField] private float rightPadding = 0f;

        [Header("Reward List")]
        [SerializeField] private RectTransform rewardContent;
        [SerializeField] private ItemChoiceOptionView rewardItemPrefab;

        private readonly List<ItemChoiceOptionView> rewardViews = new List<ItemChoiceOptionView>();

        private ItemMetaResolver itemMetaResolver;
        private GemMetaResolver gemMetaResolver;
        private CurrencyMetaResolver currencyMetaResolver;
        private RarityMetaResolver rarityMetaResolver;

        private readonly Color defaultFrameColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        private readonly Color defaultTextColor = Color.white;
        private Coroutine openRoutine;
        private Vector2 cachedPanelSize;
        private bool hasCachedPanelSize;

        private struct RewardViewData
        {
            public uint EntryId;
            public Sprite Icon;
            public ulong Amount;
            public Color FrameColor;
            public Color TextColor;
            public int SortOrder;
        }

        private void Awake()
        {
            BindButtons();
        }

        private void OnDisable()
        {
            if (openRoutine != null)
            {
                StopCoroutine(openRoutine);
                openRoutine = null;
            }
        }

        public void Show(UseItemResult result, string titleOverride = null)
        {
            if (result == null) return;
            EnsureMeta();

            var rewards = BuildRewardList(result);
            if (rewards.Count == 0) return;

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(titleOverride) ? "획득 보상" : titleOverride;
            }

            ApplyRewardViews(rewards);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            PreparePanelAnimation();
            PlayOpenAnimation();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void BindButtons()
        {
            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(Hide);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void PreparePanelAnimation()
        {
            if (panelRect == null) return;

            CachePanelSize();

            if (autoAlignToCenterLine)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;

                if (hasCachedPanelSize)
                {
                    panelRect.sizeDelta = cachedPanelSize;
                }
            }

            ClampPanelToParent();
        }

        private void CachePanelSize()
        {
            if (panelRect == null) return;

            Canvas.ForceUpdateCanvases();
            if (panelRect.parent is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            var size = panelRect.rect.size;
            if (size.x > 0f && size.y > 0f)
            {
                cachedPanelSize = size;
                hasCachedPanelSize = true;
            }
        }

        private void ClampPanelToParent()
        {
            if (!clampToParentBounds || panelRect == null) return;
            if (panelRect.parent is not RectTransform parentRect) return;

            float parentWidth = parentRect.rect.width;
            if (parentWidth <= 0f) return;

            float width = hasCachedPanelSize ? cachedPanelSize.x : panelRect.rect.width;
            if (width <= 0f) return;

            float half = parentWidth * 0.5f;
            float limit = half - rightPadding - width;

            var pos = panelRect.anchoredPosition;
            if (pos.x > limit)
            {
                pos.x = limit;
                panelRect.anchoredPosition = pos;
            }
        }

        private void PlayOpenAnimation()
        {
            if (panelRect == null) return;

            if (openRoutine != null)
            {
                StopCoroutine(openRoutine);
                openRoutine = null;
            }

            if (!playOpenAnimation || openDuration <= 0f)
            {
                SetPanelScale(1f);
                return;
            }

            openRoutine = StartCoroutine(OpenAnimationRoutine());
        }

        private IEnumerator OpenAnimationRoutine()
        {
            SetPanelScale(0f);

            float duration = Mathf.Max(0.01f, openDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = openCurve != null ? openCurve.Evaluate(t) : t;
                SetPanelScale(Mathf.Clamp01(eased));
                yield return null;
            }

            SetPanelScale(1f);
            openRoutine = null;
        }

        private void SetPanelScale(float scaleX)
        {
            if (panelRect == null) return;
            var scale = panelRect.localScale;
            scale.x = scaleX;
            panelRect.localScale = scale;
        }

        private List<RewardViewData> BuildRewardList(UseItemResult result)
        {
            var rewards = new List<RewardViewData>();
            var itemCounts = new Dictionary<uint, ulong>();
            var gemCounts = new Dictionary<uint, ulong>();

            ulong gold = 0;
            ulong crystal = 0;

            if (result.Rewards != null)
            {
                foreach (var reward in result.Rewards)
                {
                    if (reward == null) continue;
                    switch (reward.RewardType)
                    {
                        case RewardType.Gold:
                            gold += reward.Amount;
                            break;
                        case RewardType.Crystal:
                            crystal += reward.Amount;
                            break;
                        case RewardType.Item:
                            if (uint.TryParse(reward.RewardKey, out var itemId) && itemId > 0)
                            {
                                itemCounts[itemId] = itemCounts.TryGetValue(itemId, out var existing)
                                    ? existing + reward.Amount
                                    : reward.Amount;
                            }
                            break;
                    }
                }
            }

            if (result.Gems != null)
            {
                foreach (var gem in result.Gems)
                {
                    if (gem == null || gem.GemId == 0) continue;
                    gemCounts[gem.GemId] = gemCounts.TryGetValue(gem.GemId, out var existing)
                        ? existing + 1
                        : 1;
                }
            }

            uint entryId = 1;

            if (gold > 0)
            {
                rewards.Add(BuildCurrencyReward(entryId++, "GOLD", gold));
            }

            if (crystal > 0)
            {
                rewards.Add(BuildCurrencyReward(entryId++, "CRYSTAL", crystal));
            }

            foreach (var pair in itemCounts)
            {
                rewards.Add(BuildItemReward(entryId++, pair.Key, pair.Value));
            }

            foreach (var pair in gemCounts)
            {
                rewards.Add(BuildGemReward(entryId++, pair.Key, pair.Value));
            }

            rewards.Sort((a, b) =>
            {
                int order = b.SortOrder.CompareTo(a.SortOrder);
                if (order != 0) return order;
                return a.EntryId.CompareTo(b.EntryId);
            });

            return rewards;
        }

        private RewardViewData BuildCurrencyReward(uint entryId, string currencyType, ulong amount)
        {
            var icon = ResolveCurrencySprite(currencyType);
            uint rarityId = ResolveCurrencyRarityId(currencyType);
            ResolveRarityColors(rarityId, out var frameColor, out var textColor, out var sortOrder);

            return new RewardViewData
            {
                EntryId = entryId,
                Icon = icon,
                Amount = amount,
                FrameColor = frameColor,
                TextColor = textColor,
                SortOrder = sortOrder
            };
        }

        private RewardViewData BuildItemReward(uint entryId, uint itemId, ulong amount)
        {
            var icon = ResolveItemIcon(itemId);
            uint rarityId = 0;
            if (itemMetaResolver != null && itemMetaResolver.TryGetItem(itemId, out var meta))
            {
                rarityId = meta.RarityId;
            }
            ResolveRarityColors(rarityId, out var frameColor, out var textColor, out var sortOrder);

            return new RewardViewData
            {
                EntryId = entryId,
                Icon = icon,
                Amount = amount,
                FrameColor = frameColor,
                TextColor = textColor,
                SortOrder = sortOrder
            };
        }

        private RewardViewData BuildGemReward(uint entryId, uint gemId, ulong amount)
        {
            var icon = GemSpriteLoader.GetGemSprite(gemId);
            uint rarityId = ResolveGemRarityId(gemId);
            ResolveRarityColors(rarityId, out var frameColor, out var textColor, out var sortOrder);

            return new RewardViewData
            {
                EntryId = entryId,
                Icon = icon,
                Amount = amount,
                FrameColor = frameColor,
                TextColor = textColor,
                SortOrder = sortOrder
            };
        }

        private void ApplyRewardViews(List<RewardViewData> rewards)
        {
            ClearRewardViews();

            if (rewardContent == null || rewardItemPrefab == null) return;

            foreach (var reward in rewards)
            {
                var view = Instantiate(rewardItemPrefab, rewardContent, false);
                view.Bind(reward.EntryId, reward.Icon, reward.Amount, reward.FrameColor, reward.TextColor, false, null);
                rewardViews.Add(view);
            }
        }

        private void ClearRewardViews()
        {
            for (int i = 0; i < rewardViews.Count; i++)
            {
                if (rewardViews[i] != null)
                {
                    Destroy(rewardViews[i].gameObject);
                }
            }
            rewardViews.Clear();
        }

        private Sprite ResolveItemIcon(uint itemId)
        {
            if (itemMetaResolver == null) return null;
            if (itemMetaResolver.TryGetItem(itemId, out var meta))
            {
                return ItemSpriteLoader.GetItemSprite(meta.SpriteKey);
            }
            return null;
        }

        private Sprite ResolveCurrencySprite(string currencyType)
        {
            if (currencyMetaResolver == null) return null;
            if (currencyMetaResolver.TryGetCurrencyByType(currencyType, out var meta))
            {
                return ItemSpriteLoader.GetCurrencySprite(meta.SpriteKey);
            }
            return null;
        }

        private uint ResolveCurrencyRarityId(string currencyType)
        {
            if (currencyMetaResolver == null) return 0;
            if (currencyMetaResolver.TryGetCurrencyByType(currencyType, out var meta))
            {
                return meta.RarityId;
            }
            return 0;
        }

        private uint ResolveGemRarityId(uint gemId)
        {
            if (gemMetaResolver != null && gemMetaResolver.TryGetDefinition(gemId, out var def))
            {
                return def.GradeId;
            }
            return 0;
        }

        private void ResolveRarityColors(uint rarityId, out Color frameColor, out Color textColor, out int sortOrder)
        {
            frameColor = defaultFrameColor;
            textColor = defaultTextColor;
            sortOrder = 0;

            if (rarityId == 0 || rarityMetaResolver == null) return;
            if (rarityMetaResolver.TryGetRarity(rarityId, out var rarity))
            {
                frameColor = rarity.BgColor;
                textColor = rarity.TextColor;
                sortOrder = rarity.SortOrder;
            }
        }

        private void EnsureMeta()
        {
            if (itemMetaResolver == null)
            {
                itemMetaResolver = new ItemMetaResolver();
            }
            else if (MetaRepository.Loaded && !itemMetaResolver.HasData)
            {
                itemMetaResolver.Reload();
            }

            if (gemMetaResolver == null)
            {
                gemMetaResolver = new GemMetaResolver();
            }
            else if (MetaRepository.Loaded && gemMetaResolver.AllDefinitions.Count == 0)
            {
                gemMetaResolver.Reload();
            }

            if (currencyMetaResolver == null)
            {
                currencyMetaResolver = new CurrencyMetaResolver();
            }
            else if (MetaRepository.Loaded && !currencyMetaResolver.HasData)
            {
                currencyMetaResolver.Reload();
            }

            if (rarityMetaResolver == null)
            {
                rarityMetaResolver = new RarityMetaResolver();
            }
            else if (MetaRepository.Loaded && !rarityMetaResolver.HasData)
            {
                rarityMetaResolver.Reload();
            }
        }
    }
}
