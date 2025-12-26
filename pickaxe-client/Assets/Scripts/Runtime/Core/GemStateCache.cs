using System;
using System.Collections.Generic;
using System.Linq;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.Core
{
    /// <summary>
    /// 장착된 젬 정보 (PickaxeSlotInfo → GemSlotInfo에서 추출)
    /// </summary>
    public struct EquippedGemInfo
    {
        public uint PickaxeSlotIndex;
        public uint GemSlotIndex;
        public string GemInstanceId;
    }

    /// <summary>
    /// 서버에서 내려준 젬 인벤토리 상태를 전역으로 캐싱하고 브로드캐스트한다.
    /// Game 씬 어디서나 참조 가능하며, 실시간으로 업데이트된다.
    /// </summary>
    public sealed class GemStateCache
    {
        private static readonly Lazy<GemStateCache> Lazy = new Lazy<GemStateCache>(() => new GemStateCache());
        public static GemStateCache Instance => Lazy.Value;

        private readonly Dictionary<string, GemInfo> gemsByInstanceId = new Dictionary<string, GemInfo>();
        private readonly Dictionary<string, EquippedGemInfo> equippedGems = new Dictionary<string, EquippedGemInfo>();

        public event Action OnInventoryChanged;

        public uint InventoryCapacity { get; private set; }
        public bool HasData { get; private set; }

        private GemStateCache() { }

        public IReadOnlyCollection<GemInfo> AllGems => gemsByInstanceId.Values;

        public int Count => gemsByInstanceId.Count;

        public bool TryGetGem(string instanceId, out GemInfo gem)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                gem = null;
                return false;
            }

            return gemsByInstanceId.TryGetValue(instanceId, out gem);
        }

        /// <summary>
        /// 특정 젬이 장착되어 있는지 확인
        /// </summary>
        public bool IsEquipped(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return false;
            return equippedGems.ContainsKey(instanceId);
        }

        /// <summary>
        /// 특정 젬의 장착 정보 조회
        /// </summary>
        public bool TryGetEquippedInfo(string instanceId, out EquippedGemInfo info)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                info = default;
                return false;
            }

            return equippedGems.TryGetValue(instanceId, out info);
        }

        /// <summary>
        /// 특정 곡괭이 슬롯의 특정 젬 슬롯에 장착된 젬을 조회한다.
        /// </summary>
        public bool TryGetEquippedGem(uint pickaxeSlotIndex, uint gemSlotIndex, out GemInfo gem)
        {
            gem = null;

            var equipped = equippedGems.Values.FirstOrDefault(e =>
                e.PickaxeSlotIndex == pickaxeSlotIndex &&
                e.GemSlotIndex == gemSlotIndex);

            if (string.IsNullOrWhiteSpace(equipped.GemInstanceId))
                return false;

            return gemsByInstanceId.TryGetValue(equipped.GemInstanceId, out gem);
        }

        /// <summary>
        /// 특정 곡괭이 슬롯에 장착된 모든 젬을 조회한다.
        /// </summary>
        public IEnumerable<GemInfo> GetEquippedGemsForSlot(uint pickaxeSlotIndex)
        {
            var equippedInstanceIds = equippedGems.Values
                .Where(e => e.PickaxeSlotIndex == pickaxeSlotIndex)
                .Select(e => e.GemInstanceId)
                .ToList();

            foreach (var instanceId in equippedInstanceIds)
            {
                if (gemsByInstanceId.TryGetValue(instanceId, out var gem))
                {
                    yield return gem;
                }
            }
        }

        /// <summary>
        /// 인벤토리에 있는 젬만 조회한다 (장착되지 않은 젬).
        /// </summary>
        public IEnumerable<GemInfo> GetInventoryGems()
        {
            return gemsByInstanceId.Values.Where(g => !equippedGems.ContainsKey(g.GemInstanceId));
        }

        public void ResetAll()
        {
            gemsByInstanceId.Clear();
            equippedGems.Clear();
            InventoryCapacity = 0;
            HasData = false;
            RaiseChanged();
        }

        public void UpdateFromGemListResponse(GemListResponse response)
        {
            if (response == null) return;

            gemsByInstanceId.Clear();

            foreach (var gem in response.Gems)
            {
                if (gem == null || string.IsNullOrWhiteSpace(gem.GemInstanceId)) continue;
                gemsByInstanceId[gem.GemInstanceId] = gem;
            }

            InventoryCapacity = response.InventoryCapacity;
            HasData = true;

            RaiseChanged();
        }

        /// <summary>
        /// PickaxeStateCache로부터 장착 정보를 동기화한다.
        /// AllSlotsResponse 수신 시 호출된다.
        /// </summary>
        public void SyncEquippedGemsFromSlots()
        {
            equippedGems.Clear();

            var pickaxeCache = PickaxeStateCache.Instance;
            if (!pickaxeCache.Slots.Any()) return;

            foreach (var slotPair in pickaxeCache.Slots)
            {
                uint pickaxeSlotIndex = slotPair.Key;
                var pickaxeSlot = slotPair.Value;

                if (pickaxeSlot.GemSlots == null) continue;

                foreach (var gemSlot in pickaxeSlot.GemSlots)
                {
                    if (gemSlot == null || !gemSlot.IsUnlocked) continue;
                    if (gemSlot.EquippedGem == null) continue;
                    if (string.IsNullOrWhiteSpace(gemSlot.EquippedGem.GemInstanceId)) continue;

                    var info = new EquippedGemInfo
                    {
                        PickaxeSlotIndex = pickaxeSlotIndex,
                        GemSlotIndex = gemSlot.GemSlotIndex,
                        GemInstanceId = gemSlot.EquippedGem.GemInstanceId
                    };

                    equippedGems[gemSlot.EquippedGem.GemInstanceId] = info;
                }
            }

            RaiseChanged();
        }

        public void ApplyGachaResult(GemGachaResult result)
        {
            if (result == null || !result.Success) return;

            foreach (var gem in result.Gems)
            {
                if (gem == null || string.IsNullOrWhiteSpace(gem.GemInstanceId)) continue;
                gemsByInstanceId[gem.GemInstanceId] = gem;
            }

            if (result.InventoryCapacity > 0)
            {
                InventoryCapacity = result.InventoryCapacity;
            }

            RaiseChanged();
        }

        public void ApplySynthesisResult(GemSynthesisResult result)
        {
            if (result == null || !result.Success) return;

            // 합성에 사용된 젬 3개 제거 (서버에서 consumed_gem_instance_ids 제공하지 않음)
            // Request에 포함된 instance_ids로 제거해야 함 (MessageHandler에서 처리 필요)

            // 새로운 젬 추가
            if (result.ResultGem != null && !string.IsNullOrWhiteSpace(result.ResultGem.GemInstanceId))
            {
                gemsByInstanceId[result.ResultGem.GemInstanceId] = result.ResultGem;
            }

            RaiseChanged();
        }

        /// <summary>
        /// 합성에 사용된 젬들을 제거한다 (MessageHandler에서 request 정보와 함께 호출)
        /// </summary>
        public void RemoveGems(IEnumerable<string> instanceIds)
        {
            if (instanceIds == null) return;

            foreach (var instanceId in instanceIds)
            {
                gemsByInstanceId.Remove(instanceId);
                equippedGems.Remove(instanceId);
            }

            RaiseChanged();
        }

        public void ApplyConversionResult(GemConversionResult result, string requestInstanceId)
        {
            if (result == null || !result.Success) return;

            // 원본 젬 제거
            if (!string.IsNullOrWhiteSpace(requestInstanceId))
            {
                gemsByInstanceId.Remove(requestInstanceId);
                equippedGems.Remove(requestInstanceId);
            }

            // 변환된 젬 추가
            if (result.ConvertedGem != null && !string.IsNullOrWhiteSpace(result.ConvertedGem.GemInstanceId))
            {
                gemsByInstanceId[result.ConvertedGem.GemInstanceId] = result.ConvertedGem;
            }

            RaiseChanged();
        }

        public void ApplyDiscardResult(GemDiscardResult result, IEnumerable<string> requestInstanceIds)
        {
            if (result == null || !result.Success) return;

            // 분해된 젬들 제거
            if (requestInstanceIds != null)
            {
                foreach (var instanceId in requestInstanceIds)
                {
                    gemsByInstanceId.Remove(instanceId);
                    equippedGems.Remove(instanceId);
                }
            }

            RaiseChanged();
        }

        public void ApplyEquipResult(GemEquipResult result)
        {
            if (result == null || !result.Success) return;

            if (result.EquippedGem != null && !string.IsNullOrWhiteSpace(result.EquippedGem.GemInstanceId))
            {
                var info = new EquippedGemInfo
                {
                    PickaxeSlotIndex = result.PickaxeSlotIndex,
                    GemSlotIndex = result.GemSlotIndex,
                    GemInstanceId = result.EquippedGem.GemInstanceId
                };

                equippedGems[result.EquippedGem.GemInstanceId] = info;
            }

            RaiseChanged();
        }

        public void ApplyUnequipResult(GemUnequipResult result)
        {
            if (result == null || !result.Success) return;

            if (result.UnequippedGem != null && !string.IsNullOrWhiteSpace(result.UnequippedGem.GemInstanceId))
            {
                equippedGems.Remove(result.UnequippedGem.GemInstanceId);
            }

            RaiseChanged();
        }

        public void ApplySlotUnlockResult(GemSlotUnlockResult result)
        {
            if (result == null || !result.Success) return;

            // 슬롯 해금은 젬 인벤토리에 직접적인 변경이 없지만,
            // UI에서 슬롯 상태를 업데이트할 수 있도록 이벤트 발생
            RaiseChanged();
        }

        public void ApplyInventoryExpandResult(GemInventoryExpandResult result)
        {
            if (result == null || !result.Success) return;

            if (result.NewCapacity > 0)
            {
                InventoryCapacity = result.NewCapacity;
            }

            RaiseChanged();
        }

        private void RaiseChanged() => OnInventoryChanged?.Invoke();
    }
}
