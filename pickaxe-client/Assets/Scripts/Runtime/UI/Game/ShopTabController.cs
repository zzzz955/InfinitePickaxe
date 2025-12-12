using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 상점 탭 컨트롤러
    /// 크리스탈 구매, 슬롯 해금, 광고 시청 등
    /// </summary>
    public class ShopTabController : BaseTabController
    {
        [Header("Ad UI References")]
        [SerializeField] private Button watchAdButton1;
        [SerializeField] private Button watchAdButton2;
        [SerializeField] private Button watchAdButton3;
        [SerializeField] private TextMeshProUGUI adCountText;

        [Header("Slot Unlock UI")]
        [SerializeField] private Button unlockSlot2Button;
        [SerializeField] private Button unlockSlot3Button;
        [SerializeField] private Button unlockSlot4Button;
        [SerializeField] private TextMeshProUGUI slot2CostText;
        [SerializeField] private TextMeshProUGUI slot3CostText;
        [SerializeField] private TextMeshProUGUI slot4CostText;

        [Header("IAP UI References")]
        [SerializeField] private Button iapSmallButton;
        [SerializeField] private Button iapMediumButton;
        [SerializeField] private Button iapLargeButton;

        [Header("Shop Data")]
        [SerializeField] private int watchedAdCount = 0;
        [SerializeField] private int maxAdCount = 3;
        [SerializeField] private bool slot2Unlocked = true;
        [SerializeField] private bool slot3Unlocked = false;
        [SerializeField] private bool slot4Unlocked = false;

        protected override void Initialize()
        {
            base.Initialize();

            // 광고 버튼 이벤트 등록
            if (watchAdButton1 != null)
            {
                watchAdButton1.onClick.AddListener(() => OnWatchAdClicked(1));
            }
            if (watchAdButton2 != null)
            {
                watchAdButton2.onClick.AddListener(() => OnWatchAdClicked(2));
            }
            if (watchAdButton3 != null)
            {
                watchAdButton3.onClick.AddListener(() => OnWatchAdClicked(3));
            }

            // 슬롯 해금 버튼 이벤트 등록
            if (unlockSlot2Button != null)
            {
                unlockSlot2Button.onClick.AddListener(() => OnUnlockSlotClicked(2));
            }
            if (unlockSlot3Button != null)
            {
                unlockSlot3Button.onClick.AddListener(() => OnUnlockSlotClicked(3));
            }
            if (unlockSlot4Button != null)
            {
                unlockSlot4Button.onClick.AddListener(() => OnUnlockSlotClicked(4));
            }

            // IAP 버튼 이벤트 등록
            if (iapSmallButton != null)
            {
                iapSmallButton.onClick.AddListener(() => OnIAPClicked("small"));
            }
            if (iapMediumButton != null)
            {
                iapMediumButton.onClick.AddListener(() => OnIAPClicked("medium"));
            }
            if (iapLargeButton != null)
            {
                iapLargeButton.onClick.AddListener(() => OnIAPClicked("large"));
            }

            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        /// <summary>
        /// 상점 UI 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateAdCount();
            UpdateSlotButtons();
        }

        private void UpdateAdCount()
        {
            if (adCountText != null)
            {
                adCountText.text = $"📺 광고 시청 (오늘 {watchedAdCount}/{maxAdCount})";
            }

            // 광고 버튼 활성화 상태 업데이트
            if (watchAdButton1 != null)
            {
                watchAdButton1.interactable = (watchedAdCount < 1);
            }
            if (watchAdButton2 != null)
            {
                watchAdButton2.interactable = (watchedAdCount < 2);
            }
            if (watchAdButton3 != null)
            {
                watchAdButton3.interactable = (watchedAdCount < 3);
            }
        }

        private void UpdateSlotButtons()
        {
            // 슬롯 2
            if (unlockSlot2Button != null)
            {
                unlockSlot2Button.interactable = !slot2Unlocked;
            }
            if (slot2CostText != null)
            {
                slot2CostText.text = slot2Unlocked ? "해금 완료" : "슬롯 2: 400 💎";
            }

            // 슬롯 3
            if (unlockSlot3Button != null)
            {
                unlockSlot3Button.interactable = !slot3Unlocked;
            }
            if (slot3CostText != null)
            {
                slot3CostText.text = slot3Unlocked ? "해금 완료" : "슬롯 3: 2,000 💎";
            }

            // 슬롯 4
            if (unlockSlot4Button != null)
            {
                unlockSlot4Button.interactable = !slot4Unlocked;
            }
            if (slot4CostText != null)
            {
                slot4CostText.text = slot4Unlocked ? "해금 완료" : "슬롯 4: 4,000 💎";
            }
        }

        /// <summary>
        /// 광고 시청 버튼 클릭 이벤트
        /// </summary>
        private void OnWatchAdClicked(int tier)
        {
            // TODO: 광고 SDK 호출
            Debug.Log($"ShopTabController: 광고 시청 버튼 클릭됨 (Tier {tier})");
        }

        /// <summary>
        /// 슬롯 해금 버튼 클릭 이벤트
        /// </summary>
        private void OnUnlockSlotClicked(int slotIndex)
        {
            // TODO: 서버로 슬롯 해금 요청
            Debug.Log($"ShopTabController: 슬롯 {slotIndex} 해금 버튼 클릭됨");
        }

        /// <summary>
        /// IAP 구매 버튼 클릭 이벤트
        /// </summary>
        private void OnIAPClicked(string packageType)
        {
            // MVP에서는 UI만 존재
            Debug.Log($"ShopTabController: IAP 버튼 클릭됨 ({packageType}) - MVP에서는 준비 중");
        }

        /// <summary>
        /// 슬롯 해금 상태 업데이트 (외부에서 호출)
        /// </summary>
        public void SetSlotUnlocked(int slotIndex, bool unlocked)
        {
            switch (slotIndex)
            {
                case 2:
                    slot2Unlocked = unlocked;
                    break;
                case 3:
                    slot3Unlocked = unlocked;
                    break;
                case 4:
                    slot4Unlocked = unlocked;
                    break;
            }

            RefreshData();
        }

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("테스트: 슬롯 2 해금")]
        private void TestUnlockSlot2()
        {
            SetSlotUnlocked(2, true);
        }
#endif
        #endregion
    }
}
