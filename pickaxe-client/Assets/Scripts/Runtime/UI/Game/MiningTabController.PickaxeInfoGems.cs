using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Infinitepickaxe;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// MiningTabController의 PickaxeInfoModal 젬 정보 표시 기능
    /// </summary>
    public partial class MiningTabController
    {
        [Header("Pickaxe Info Modal - Gem Section")]
        [SerializeField] private GameObject pickaxeInfoGemSection;
        [SerializeField] private TextMeshProUGUI gemSectionTitleText;
        [SerializeField] private Transform gemSlotsContainer;
        [SerializeField] private GameObject gemSlotItemTemplate;

        private readonly List<GemSlotItemView> gemSlotItemViews = new List<GemSlotItemView>();
        private bool gemSlotItemsInitialized;

        /// <summary>
        /// PickaxeInfoModal 젬 섹션 초기화
        /// AutoBindPickaxeInfoModalReferences()에서 호출
        /// </summary>
        private void AutoBindPickaxeInfoGemSection()
        {
            if (pickaxeInfoModal == null) return;

            var root = pickaxeInfoModal.transform;

            if (pickaxeInfoGemSection == null)
            {
                var sectionTf = FindChildRecursive(root, "GemSection");
                if (sectionTf != null) pickaxeInfoGemSection = sectionTf.gameObject;
            }

            if (gemSectionTitleText == null)
            {
                var titleTf = FindChildRecursive(root, "GemSectionTitle");
                if (titleTf != null) gemSectionTitleText = titleTf.GetComponent<TextMeshProUGUI>();
            }

            if (gemSlotsContainer == null)
            {
                var containerTf = FindChildRecursive(root, "GemSlotsContainer");
                if (containerTf != null) gemSlotsContainer = containerTf;
            }

            if (gemSlotItemTemplate == null)
            {
                var templateTf = FindChildRecursive(root, "GemSlotItemTemplate");
                if (templateTf != null) gemSlotItemTemplate = templateTf.gameObject;
            }
        }

        /// <summary>
        /// PickaxeInfoModal의 젬 정보 업데이트
        /// UpdatePickaxeInfoModalData()에서 호출
        /// </summary>
        private void UpdatePickaxeInfoGemSlots(int slotIndex, PickaxeSlotInfo slotInfo)
        {
            if (pickaxeInfoGemSection == null) return;

            if (slotInfo == null || slotInfo.GemSlots == null || slotInfo.GemSlots.Count == 0)
            {
                pickaxeInfoGemSection.SetActive(false);
                return;
            }

            pickaxeInfoGemSection.SetActive(true);

            // 타이틀 업데이트
            if (gemSectionTitleText != null)
            {
                int equippedCount = CountEquippedGems(slotInfo.GemSlots);
                int totalCount = slotInfo.GemSlots.Count;
                gemSectionTitleText.text = $"보석 슬롯 ({equippedCount}/{totalCount})";
            }

            // 젬 슬롯 아이템 생성/업데이트
            EnsureGemSlotItems(slotInfo.GemSlots.Count);
            UpdateGemSlotItems((uint)slotIndex, slotInfo.GemSlots);
        }

        /// <summary>
        /// 장착된 젬 개수 계산
        /// </summary>
        private int CountEquippedGems(IReadOnlyList<GemSlotInfo> gemSlots)
        {
            if (gemSlots == null) return 0;

            int count = 0;
            foreach (var slot in gemSlots)
            {
                if (slot != null && slot.EquippedGem != null && !string.IsNullOrWhiteSpace(slot.EquippedGem.GemInstanceId))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 젬 슬롯 아이템 뷰 생성 (필요한 개수만큼)
        /// </summary>
        private void EnsureGemSlotItems(int requiredCount)
        {
            if (gemSlotsContainer == null || gemSlotItemTemplate == null) return;

            // 템플릿 비활성화
            if (!gemSlotItemsInitialized && gemSlotItemTemplate.activeSelf)
            {
                gemSlotItemTemplate.SetActive(false);
            }

            // 부족한 만큼 생성
            while (gemSlotItemViews.Count < requiredCount)
            {
                var go = Instantiate(gemSlotItemTemplate, gemSlotsContainer);
                go.name = $"GemSlotItem_{gemSlotItemViews.Count}";

                var view = go.GetComponent<GemSlotItemView>();
                if (view == null)
                {
                    view = go.AddComponent<GemSlotItemView>();
                }

                go.SetActive(true);
                gemSlotItemViews.Add(view);

                Debug.Log($"[MiningTabController] {go.name} 생성됨");
            }

            // 초과한 만큼 비활성화
            for (int i = requiredCount; i < gemSlotItemViews.Count; i++)
            {
                if (gemSlotItemViews[i] != null && gemSlotItemViews[i].gameObject != null)
                {
                    gemSlotItemViews[i].gameObject.SetActive(false);
                }
            }

            gemSlotItemsInitialized = true;

            // Layout 강제 갱신 (VerticalLayoutGroup이 제대로 적용되도록)
            if (gemSlotsContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(gemSlotsContainer.GetComponent<RectTransform>());
                Debug.Log($"[MiningTabController] GemSlotsContainer Layout 강제 갱신");
            }
        }

        /// <summary>
        /// 젬 슬롯 아이템 데이터 업데이트
        /// </summary>
        private void UpdateGemSlotItems(uint pickaxeSlotIndex, IReadOnlyList<GemSlotInfo> gemSlots)
        {
            if (gemSlots == null) return;

            for (int i = 0; i < gemSlots.Count && i < gemSlotItemViews.Count; i++)
            {
                var view = gemSlotItemViews[i];
                var slot = gemSlots[i];

                if (view != null && view.gameObject != null)
                {
                    view.gameObject.SetActive(true);

                    // Setup() 호출하여 버튼 이벤트 등록
                    view.Setup(pickaxeSlotIndex, (uint)i, this);

                    // 슬롯 데이터 업데이트
                    view.UpdateSlot(slot);
                }
            }
        }
    }

    /// <summary>
    /// PickaxeInfoModal의 젬 슬롯 아이템 뷰
    /// </summary>
    public sealed class GemSlotItemView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image gemIcon;
        [SerializeField] private TextMeshProUGUI gemNameText;
        [SerializeField] private TextMeshProUGUI gemStatsText;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private GameObject emptyOverlay;

        // 슬롯 인덱스 및 컨트롤러 참조
        private uint pickaxeSlotIndex;
        private uint gemSlotIndex;
        private MiningTabController controller;

        // Visual Settings는 필요 없음 - EmptyOverlay와 LockedOverlay가 직접 관리

        private void Awake()
        {
            AutoBindReferences();
        }

        private void AutoBindReferences()
        {
            if (gemIcon == null)
            {
                var iconTf = FindChildRecursive(transform, "GemIcon");
                if (iconTf != null)
                {
                    gemIcon = iconTf.GetComponent<Image>();
                    Debug.Log($"[GemSlotItemView] AutoBind: GemIcon 찾음");
                }
                else
                {
                    Debug.LogWarning($"[GemSlotItemView] AutoBind: GemIcon을 찾을 수 없음");
                }
            }

            if (gemNameText == null)
            {
                var nameTf = FindChildRecursive(transform, "GemNameText");
                if (nameTf != null)
                {
                    gemNameText = nameTf.GetComponent<TextMeshProUGUI>();
                    Debug.Log($"[GemSlotItemView] AutoBind: GemNameText 찾음");
                }
                else
                {
                    Debug.LogWarning($"[GemSlotItemView] AutoBind: GemNameText를 찾을 수 없음");
                }
            }

            if (gemStatsText == null)
            {
                var statsTf = FindChildRecursive(transform, "GemStatsText");
                if (statsTf != null)
                {
                    gemStatsText = statsTf.GetComponent<TextMeshProUGUI>();
                    Debug.Log($"[GemSlotItemView] AutoBind: GemStatsText 찾음");
                }
                else
                {
                    Debug.LogWarning($"[GemSlotItemView] AutoBind: GemStatsText를 찾을 수 없음");
                }
            }

            if (lockedOverlay == null)
            {
                var lockedTf = FindChildRecursive(transform, "LockedOverlay");
                if (lockedTf != null)
                {
                    lockedOverlay = lockedTf.gameObject;
                    Debug.Log($"[GemSlotItemView] AutoBind: LockedOverlay 찾음");
                }
                else
                {
                    Debug.LogWarning($"[GemSlotItemView] AutoBind: LockedOverlay를 찾을 수 없음");
                }
            }

            if (emptyOverlay == null)
            {
                var emptyTf = FindChildRecursive(transform, "EmptyOverlay");
                if (emptyTf != null)
                {
                    emptyOverlay = emptyTf.gameObject;
                    Debug.Log($"[GemSlotItemView] AutoBind: EmptyOverlay 찾음");
                }
                else
                {
                    Debug.LogWarning($"[GemSlotItemView] AutoBind: EmptyOverlay를 찾을 수 없음");
                }
            }
        }

        public void UpdateSlot(GemSlotInfo slot)
        {
            // Overlay가 null이면 AutoBind 재실행 (Awake보다 먼저 호출될 수 있음)
            if (emptyOverlay == null || lockedOverlay == null)
            {
                Debug.LogWarning($"[GemSlotItemView] Overlay가 null → AutoBind 재실행");
                AutoBindReferences();
            }

            Debug.Log($"[GemSlotItemView] UpdateSlot 호출: slot={slot}, name={gameObject.name}");

            if (slot == null)
            {
                Debug.Log($"[GemSlotItemView] slot이 null → SetEmpty() 호출");
                SetEmpty();
                return;
            }

            if (!slot.IsUnlocked)
            {
                Debug.Log($"[GemSlotItemView] slot.IsUnlocked=false → SetLocked() 호출");
                SetLocked();
                return;
            }

            if (slot.EquippedGem != null && !string.IsNullOrWhiteSpace(slot.EquippedGem.GemInstanceId))
            {
                Debug.Log($"[GemSlotItemView] 젬 장착됨: {slot.EquippedGem.GemInstanceId} → SetEquipped() 호출");
                SetEquipped(slot.EquippedGem);
            }
            else
            {
                Debug.Log($"[GemSlotItemView] 빈 슬롯 (IsUnlocked=true, EquippedGem=null) → SetEmpty() 호출");
                SetEmpty();
            }
        }

        private void SetEquipped(GemInfo gem)
        {
            // Overlay 비활성화
            if (lockedOverlay != null) lockedOverlay.SetActive(false);
            if (emptyOverlay != null) emptyOverlay.SetActive(false);

            // 젬 아이콘 (TODO: SpriteAtlasCache 연동)
            if (gemIcon != null)
            {
                gemIcon.sprite = null; // TODO: GetGemSprite(gem.GemId)
                gemIcon.color = Color.white;
                gemIcon.enabled = true; // 스프라이트가 있으면 표시, 없으면 숨김 (TODO 연동 후)
            }

            // 젬 이름 (TODO: GemMetaResolver 연동)
            if (gemNameText != null)
            {
                gemNameText.enabled = true; // 텍스트 활성화
                gemNameText.text = GetGemDisplayName(gem);
                gemNameText.color = GetGemGradeColor((uint)gem.Grade);
            }

            // 젬 스탯
            if (gemStatsText != null)
            {
                gemStatsText.enabled = true; // 텍스트 활성화
                gemStatsText.text = GetGemStatsText(gem);
            }
        }

        private void SetEmpty()
        {
            Debug.Log($"[GemSlotItemView] SetEmpty() 시작: name={gameObject.name}");

            // Overlay 활성화
            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(false);
                Debug.Log($"[GemSlotItemView] LockedOverlay 비활성화");
            }
            else
            {
                Debug.LogWarning($"[GemSlotItemView] lockedOverlay가 null입니다!");
            }

            if (emptyOverlay != null)
            {
                emptyOverlay.SetActive(true);
                Debug.Log($"[GemSlotItemView] EmptyOverlay 활성화");
            }
            else
            {
                Debug.LogWarning($"[GemSlotItemView] emptyOverlay가 null입니다!");
            }

            // EmptyOverlay가 활성화되면 기본 UI 요소들을 숨김
            // EmptyOverlay 내부에 EmptyText가 "빈 슬롯"을 표시
            if (gemIcon != null)
            {
                gemIcon.enabled = false;
            }

            if (gemNameText != null)
            {
                gemNameText.enabled = false;
            }

            if (gemStatsText != null)
            {
                gemStatsText.enabled = false;
            }

            Debug.Log($"[GemSlotItemView] SetEmpty() 완료");
        }

        private void SetLocked()
        {
            // Overlay 활성화
            if (lockedOverlay != null) lockedOverlay.SetActive(true);
            if (emptyOverlay != null) emptyOverlay.SetActive(false);

            // LockedOverlay가 활성화되면 기본 UI 요소들을 숨김
            // LockedOverlay 내부에 LockIcon과 텍스트가 "잠김", "슬롯 해금 필요"를 표시
            if (gemIcon != null)
            {
                gemIcon.enabled = false;
            }

            if (gemNameText != null)
            {
                gemNameText.enabled = false;
            }

            if (gemStatsText != null)
            {
                gemStatsText.enabled = false;
            }
        }

        /// <summary>
        /// 슬롯 인덱스 및 컨트롤러 설정
        /// </summary>
        public void Setup(uint pickaxeIndex, uint gemIndex, MiningTabController ctrl)
        {
            pickaxeSlotIndex = pickaxeIndex;
            gemSlotIndex = gemIndex;
            controller = ctrl;

            // AutoBind가 아직 실행되지 않았으면 먼저 실행
            if (lockedOverlay == null)
            {
                Debug.Log($"[GemSlotItemView] Setup: lockedOverlay가 null → AutoBind 실행");
                AutoBindReferences();
            }

            Debug.Log($"[GemSlotItemView] Setup 호출: pickaxe={pickaxeIndex}, gem={gemIndex}, lockedOverlay={(lockedOverlay != null ? "있음" : "null")}");

            // LockedOverlay에 Button 컴포넌트 추가하여 클릭 이벤트 등록
            if (lockedOverlay != null)
            {
                var button = lockedOverlay.GetComponent<Button>();
                if (button == null)
                {
                    button = lockedOverlay.AddComponent<Button>();
                    Debug.Log($"[GemSlotItemView] LockedOverlay에 Button 추가");
                }
                else
                {
                    Debug.Log($"[GemSlotItemView] LockedOverlay에 Button 이미 존재");
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnLockedOverlayClicked);

                Debug.Log($"[GemSlotItemView] LockedOverlay 클릭 이벤트 등록 완료");
            }
            else
            {
                Debug.LogWarning($"[GemSlotItemView] lockedOverlay가 null입니다! 클릭 이벤트를 등록할 수 없습니다.");
            }
        }

        /// <summary>
        /// LockedOverlay 클릭 시 호출
        /// </summary>
        private void OnLockedOverlayClicked()
        {
            Debug.Log($"[GemSlotItemView] OnLockedOverlayClicked 호출됨! pickaxe={pickaxeSlotIndex}, gem={gemSlotIndex}");

            if (controller == null)
            {
                Debug.LogWarning("[GemSlotItemView] Controller 참조가 null입니다");
                return;
            }

            // 현재 슬롯들의 해금 상태 및 보유 크리스탈 정보 수집
            var cache = PickaxeStateCache.Instance;
            if (cache == null)
            {
                Debug.LogWarning("[GemSlotItemView] PickaxeStateCache가 null입니다");
                return;
            }

            // 해당 곡괭이 슬롯의 젬 슬롯 해금 정보 가져오기
            bool[] unlockedSlots = new bool[6];
            if (cache.TryGetSlot(pickaxeSlotIndex, out var pickaxeSlot) && pickaxeSlot.GemSlots != null)
            {
                foreach (var gemSlot in pickaxeSlot.GemSlots)
                {
                    if (gemSlot != null && gemSlot.GemSlotIndex < 6)
                    {
                        unlockedSlots[gemSlot.GemSlotIndex] = gemSlot.IsUnlocked;
                    }
                }
            }

            // 현재 보유 크리스탈 정보 가져오기
            var messageHandler = MessageHandler.Instance;
            uint currentCrystal = messageHandler?.LastCrystal ?? 0;

            controller.OnLockedGemSlotClicked(pickaxeSlotIndex, gemSlotIndex, unlockedSlots, currentCrystal);
        }

        /// <summary>
        /// 젬 표시 이름 가져오기 (TODO: GemMetaResolver 연동)
        /// </summary>
        private string GetGemDisplayName(GemInfo gem)
        {
            if (gem == null) return "알 수 없음";

            // TODO: GemMetaResolver.Instance.TryGetDefinition(gem.GemId, out var meta)
            // return meta?.Name ?? $"보석 #{gem.GemId}";

            return $"보석 #{gem.GemId}";
        }

        /// <summary>
        /// 젬 등급 색상 가져오기 (TODO: GemMetaResolver 연동)
        /// </summary>
        private Color GetGemGradeColor(uint gradeId)
        {
            // TODO: GemMetaResolver 연동
            // 임시: 등급별 색상
            return gradeId switch
            {
                1 => new Color(0.7f, 0.7f, 0.7f), // 일반 (회색)
                2 => new Color(0.3f, 0.9f, 0.3f), // 고급 (초록)
                3 => new Color(0.3f, 0.6f, 1.0f), // 희귀 (파랑)
                4 => new Color(0.8f, 0.3f, 0.9f), // 영웅 (보라)
                5 => new Color(1.0f, 0.6f, 0.2f), // 전설 (주황)
                _ => Color.white
            };
        }

        /// <summary>
        /// 젬 스탯 텍스트 생성
        /// </summary>
        private string GetGemStatsText(GemInfo gem)
        {
            if (gem == null) return string.Empty;

            // TODO: GemMetaResolver로 타입명 가져오기
            string typeName = GetGemTypeName(gem.Type);
            float statValue = gem.StatMultiplier / 100f;

            return $"{typeName} +{statValue:0.#}%";
        }

        /// <summary>
        /// 젬 타입명 가져오기 (TODO: GemMetaResolver 연동)
        /// </summary>
        private string GetGemTypeName(Infinitepickaxe.GemType gemType)
        {
            // TODO: GemMetaResolver.Instance.TryGetType((uint)gemType, out var meta)
            // return meta?.DisplayName ?? "알 수 없음";

            return gemType switch
            {
                Infinitepickaxe.GemType.AttackSpeed => "공격속도",
                Infinitepickaxe.GemType.CritRate => "크리티컬 확률",
                Infinitepickaxe.GemType.CritDmg => "크리티컬 데미지",
                _ => "알 수 없음"
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
    }
}
