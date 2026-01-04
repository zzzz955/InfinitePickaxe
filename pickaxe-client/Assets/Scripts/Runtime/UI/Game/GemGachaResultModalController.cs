using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Infinitepickaxe;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 보석 가챠 결과 모달 컨트롤러
    /// 단일/멀티 뽑기 결과를 동일 프리팹으로 처리
    /// </summary>
    public class GemGachaResultModalController : MonoBehaviour
    {
        [Header("Modal UI")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform gemItemContainer;
        [SerializeField] private ScrollRect gemScrollRect;

        [Header("Buttons")]
        [SerializeField] private Button pullAgainButton;
        [SerializeField] private TextMeshProUGUI pullAgainButtonText;
        [SerializeField] private Button closeButton;

        [Header("Prefabs")]
        [SerializeField] private GameObject gemResultItemPrefab;

        private GemGachaResult currentResult;
        private int lastPullCount; // 단일/멀티 뽑기 횟수
        private uint currentCrystal;
        private Action<int> onPullAgainCallback;

        [SerializeField] private int fallbackSinglePullCost = 30;
        [SerializeField] private int fallbackMultiPullCost = 300;
        [SerializeField] private int fallbackMultiPullCount = 11;
        private readonly GemMetaResolver metaResolver = new GemMetaResolver();

        private void Awake()
        {
            if (pullAgainButton != null)
            {
                pullAgainButton.onClick.AddListener(OnPullAgainClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        /// <summary>
        /// 가챠 결과 설정 및 모달 표시
        /// </summary>
        /// <param name="result">가챠 결과</param>
        /// <param name="pullCount">뽑기 횟수 (단일/멀티)</param>
        /// <param name="currentCrystalAmount">현재 보유 크리스탈</param>
        /// <param name="onPullAgain">한번 더 뽑기 콜백</param>
        public void SetResult(GemGachaResult result, int pullCount, uint currentCrystalAmount, Action<int> onPullAgain)
        {
            currentResult = result;
            lastPullCount = pullCount > 0 ? pullCount : 1;
            currentCrystal = currentCrystalAmount;
            onPullAgainCallback = onPullAgain;

            // 타이틀 설정
            if (titleText != null)
            {
                string pullType = $"{lastPullCount}회 뽑기";
                titleText.text = $"{pullType} 결과";
            }

            // 기존 아이템 제거
            ClearGemItems();

            // 보석 아이템 생성
            if (result != null && result.Gems != null && result.Gems.Count > 0)
            {
                foreach (var gem in result.Gems)
                {
                    CreateGemItem(gem);
                }
            }

            // 버튼 상태 업데이트
            UpdatePullAgainButton();

            // 스크롤 위치 초기화
            if (gemScrollRect != null)
            {
                gemScrollRect.normalizedPosition = new Vector2(0, 1);
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 보석 아이템 UI 생성
        /// </summary>
        private void CreateGemItem(GemInfo gem)
        {
            if (gemResultItemPrefab == null || gemItemContainer == null)
            {
                Debug.LogWarning("GemGachaResultModalController: gemResultItemPrefab or gemItemContainer is null");
                return;
            }

            var itemObj = Instantiate(gemResultItemPrefab, gemItemContainer);
            var itemView = itemObj.GetComponent<GemResultItemView>();

            if (itemView != null)
            {
                itemView.SetGem(gem);
            }
        }

        /// <summary>
        /// 기존 보석 아이템 모두 제거
        /// </summary>
        private void ClearGemItems()
        {
            if (gemItemContainer == null) return;

            foreach (Transform child in gemItemContainer)
            {
                Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// "한번 더 뽑기" 버튼 상태 업데이트
        /// </summary>
        private void UpdatePullAgainButton()
        {
            if (pullAgainButton == null || pullAgainButtonText == null) return;

            int cost = IsMultiPull(lastPullCount) ? ResolveMultiPullCost() : ResolveSinglePullCost();
            bool canAfford = currentCrystal >= cost;

            pullAgainButton.interactable = canAfford;

            if (canAfford)
            {
                string pullType = $"{lastPullCount}회";
                pullAgainButtonText.text = $"한번 더 뽑기 ({pullType}, 크리스탈 {cost})";
            }
            else
            {
                pullAgainButtonText.text = "크리스탈 부족";
            }
        }

        private int ResolveSinglePullCost()
        {
            int cost = (int)metaResolver.SinglePullCost;
            return cost > 0 ? cost : fallbackSinglePullCost;
        }

        private int ResolveMultiPullCost()
        {
            int cost = (int)metaResolver.MultiPullCost;
            return cost > 0 ? cost : fallbackMultiPullCost;
        }

        private int ResolveMultiPullCount()
        {
            int count = (int)metaResolver.MultiPullCount;
            return count > 0 ? count : fallbackMultiPullCount;
        }

        private bool IsMultiPull(int pullCount)
        {
            int multiCount = ResolveMultiPullCount();
            if (multiCount > 0)
            {
                return pullCount == multiCount;
            }
            return pullCount != 1;
        }

        /// <summary>
        /// "한번 더 뽑기" 버튼 클릭
        /// </summary>
        private void OnPullAgainClicked()
        {
            int cost = IsMultiPull(lastPullCount) ? ResolveMultiPullCost() : ResolveSinglePullCost();

            // 재화 체크 (다시 확인)
            if (currentCrystal < cost)
            {
                Debug.LogWarning("GemGachaResultModalController: 크리스탈 부족");
                return;
            }

            // 콜백 호출 (ShopTabController에서 처리)
            onPullAgainCallback?.Invoke(lastPullCount);

            // 모달 닫기 (재구매 후 새 결과로 다시 열림)
            gameObject.SetActive(false);
        }

        /// <summary>
        /// "닫기" 버튼 클릭
        /// </summary>
        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 크리스탈 업데이트 (외부에서 호출, 실시간 동기화)
        /// </summary>
        public void UpdateCrystal(uint newCrystal)
        {
            currentCrystal = newCrystal;
            UpdatePullAgainButton();
        }
    }
}
