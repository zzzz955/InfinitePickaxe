using TMPro;
using UnityEngine;
using InfinitePickaxe.Client.Core;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 탭과 무관하게 항상 표시되는 상단바 재화 표시 담당
    /// 서버 이벤트(UserDataSnapshot, CurrencyUpdate, MiningComplete)만 반영하고
    /// 클라이언트에서 임의로 증감하지 않는다.
    /// </summary>
    public class TopbarController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI crystalText;

        private ulong? currentGold;
        private uint? currentCrystal;
        private UserResourceCache resourceCache;

        private void OnEnable()
        {
            resourceCache = UserResourceCache.Instance;
            if (resourceCache != null)
            {
                resourceCache.OnChanged += HandleResourceChanged;
                ApplyResourceCache();
            }
        }

        private void OnDisable()
        {
            if (resourceCache != null)
            {
                resourceCache.OnChanged -= HandleResourceChanged;
            }
        }

        private void HandleResourceChanged()
        {
            ApplyResourceCache();
        }

        private void ApplyResourceCache()
        {
            if (resourceCache == null)
            {
                return;
            }

            currentGold = resourceCache.Gold;
            currentCrystal = resourceCache.Crystal;
            Apply();
        }

        private void Apply()
        {
            if (goldText != null && currentGold.HasValue)
            {
                goldText.text = currentGold.Value.ToString("N0");
            }
            if (crystalText != null && currentCrystal.HasValue)
            {
                crystalText.text = currentCrystal.Value.ToString("N0");
            }
        }
    }
}
