using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InfinitePickaxe.Client.Core;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 상점 컨트롤러
    /// 보석 뽑기, IAP 상품 관리
    /// </summary>
    public class ShopTabController : BaseTabController
    {
        [Header("Tab Switching UI")]
        [SerializeField] private Button gemsTabButton;
        [SerializeField] private Button iapTabButton;

        [Header("SubTab Content")]
        [SerializeField] private GameObject gemShopSubTab;
        [SerializeField] private GameObject iapShopSubTab;

        [Header("Gem Shop UI")]
        [SerializeField] private Button gemSinglePullButton;
        [SerializeField] private Button gemMultiPullButton;

        [Header("Modals")]
        [SerializeField] private GameObject toastModal;
        [SerializeField] private TextMeshProUGUI toastMessageText;
        [SerializeField] private Button toastConfirmButton;
        [SerializeField] private GemGachaResultModalController gemGachaResultModal;

        private const int SinglePullCost = 50;
        private const int MultiPullCost = 500;
        private const int MultiPullCount = 11;

        private uint currentCrystal;
        private UserResourceCache resourceCache;
        private enum SubTab { Gems, IAP }
        private SubTab currentSubTab = SubTab.Gems;

        private MessageHandler messageHandler;

        protected override void Initialize()
        {
            base.Initialize();

            messageHandler = MessageHandler.Instance;

            // 하위탭 변경 버튼
            if (gemsTabButton != null)
            {
                gemsTabButton.onClick.AddListener(() => SwitchToGemTab());
            }
            if (iapTabButton != null)
            {
                iapTabButton.onClick.AddListener(() => SwitchToIAPTab());
            }

            // 보석 뽑기 버튼 이벤트 리스너 등록
            if (gemSinglePullButton != null)
            {
                gemSinglePullButton.onClick.AddListener(() => OnGemPullClicked(false));
            }
            if (gemMultiPullButton != null)
            {
                gemMultiPullButton.onClick.AddListener(() => OnGemPullClicked(true));
            }

            // 광고 버튼 이벤트 리스너 등록
            // 서브탭 AutoBind
            AutoBindSubTabs();

            // 토스트모달 AutoBind
            AutoBindToastModal();

            SwitchToGemTab();
            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            SubscribeMessageHandler();
            SubscribeResourceCache();
            RefreshData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UnsubscribeMessageHandler();
            UnsubscribeResourceCache();
        }

        private void OnDestroy()
        {
            UnsubscribeMessageHandler();
            UnsubscribeResourceCache();
        }

        /// <summary>
        /// 상점 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
        }

        #region Tab Switching

        private void AutoBindSubTabs()
        {
            if (gemShopSubTab == null)
            {
                var existingGemTab = GameObject.Find("GemShopSubTab");
                if (existingGemTab == null)
                {
                    var prefab = Resources.Load<GameObject>("UI/GemShopSubTab");
                    if (prefab != null)
                    {
                        gemShopSubTab = Instantiate(prefab, transform);
                        gemShopSubTab.name = "GemShopSubTab";

                        // UI 컴포넌트 바인딩
                        var singleBtnTf = FindChildRecursive(gemShopSubTab.transform, "SinglePullButton");
                        if (singleBtnTf != null)
                        {
                            gemSinglePullButton = singleBtnTf.GetComponent<Button>();
                            if (gemSinglePullButton != null)
                            {
                                gemSinglePullButton.onClick.RemoveAllListeners();
                                gemSinglePullButton.onClick.AddListener(() => OnGemPullClicked(false));
                            }
                        }

                        var multiBtnTf = FindChildRecursive(gemShopSubTab.transform, "MultiPullButton");
                        if (multiBtnTf != null)
                        {
                            gemMultiPullButton = multiBtnTf.GetComponent<Button>();
                            if (gemMultiPullButton != null)
                            {
                                gemMultiPullButton.onClick.RemoveAllListeners();
                                gemMultiPullButton.onClick.AddListener(() => OnGemPullClicked(true));
                            }
                        }
                    }
                }
                else
                {
                    gemShopSubTab = existingGemTab;
                }
            }

            if (iapShopSubTab == null)
            {
                var existingIAPTab = GameObject.Find("IAPShopSubTab");
                if (existingIAPTab == null)
                {
                    var prefab = Resources.Load<GameObject>("UI/IAPShopSubTab");
                    if (prefab != null)
                    {
                        iapShopSubTab = Instantiate(prefab, transform);
                        iapShopSubTab.name = "IAPShopSubTab";
                    }
                }
                else
                {
                    iapShopSubTab = existingIAPTab;
                }
            }
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                var found = FindChildRecursive(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void SwitchToGemTab()
        {
            currentSubTab = SubTab.Gems;

            if (gemShopSubTab != null)
            {
                gemShopSubTab.SetActive(true);
            }
            if (iapShopSubTab != null)
            {
                iapShopSubTab.SetActive(false);
            }

            // 버튼 상태 업데이트
            UpdateTabButtonColors();
        }

        private void SwitchToIAPTab()
        {
            currentSubTab = SubTab.IAP;

            if (gemShopSubTab != null)
            {
                gemShopSubTab.SetActive(false);
            }
            if (iapShopSubTab != null)
            {
                iapShopSubTab.SetActive(true);
            }

            // 버튼 상태 업데이트
            UpdateTabButtonColors();
        }

        private void UpdateTabButtonColors()
        {
            if (gemsTabButton != null)
            {
                var colors = gemsTabButton.colors;
                colors.normalColor = currentSubTab == SubTab.Gems
                    ? new Color(0.3f, 0.6f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f);
                gemsTabButton.colors = colors;
            }

            if (iapTabButton != null)
            {
                var colors = iapTabButton.colors;
                colors.normalColor = currentSubTab == SubTab.IAP
                    ? new Color(0.3f, 0.6f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f);
                iapTabButton.colors = colors;
            }
        }

        #endregion

        #region Toast Modal

        private void AutoBindToastModal()
        {
            if (toastModal == null)
            {
                var existingModal = GameObject.Find("ToastModal");
                if (existingModal == null)
                {
                    var prefab = Resources.Load<GameObject>("UI/Modal");
                    if (prefab != null)
                    {
                        toastModal = Instantiate(prefab, transform.root);
                        toastModal.name = "ToastModal";
                        toastModal.SetActive(false);

                        // UI 컴포넌트 바인딩
                        var modalPanel = toastModal.transform.Find("ModalPanel");
                        if (modalPanel != null)
                        {
                            var messageTf = FindChildRecursive(modalPanel, "MessageText");
                            if (messageTf != null)
                            {
                                toastMessageText = messageTf.GetComponent<TextMeshProUGUI>();
                            }

                            var confirmBtnTf = FindChildRecursive(modalPanel, "ConfirmButton");
                            if (confirmBtnTf != null)
                            {
                                toastConfirmButton = confirmBtnTf.GetComponent<Button>();
                            }
                        }
                    }
                }
                else
                {
                    toastModal = existingModal;
                }
            }

            if (toastConfirmButton != null)
            {
                toastConfirmButton.onClick.RemoveAllListeners();
                toastConfirmButton.onClick.AddListener(() => HideToastModal());
            }
        }

        private void ShowToastMessage(string message)
        {
            if (toastModal == null)
            {
                Debug.LogWarning("ShopTabController: Toast modal not found");
                return;
            }

            if (toastMessageText != null)
            {
                toastMessageText.text = message;
            }

            toastModal.SetActive(true);
            toastModal.transform.SetAsLastSibling();
        }

        private void HideToastModal()
        {
            if (toastModal != null)
            {
                toastModal.SetActive(false);
            }
        }

        #endregion

        #region Gem Shop UI

        private void OnGemPullClicked(bool isMulti)
        {
            int cost = isMulti ? MultiPullCost : SinglePullCost;
            int count = isMulti ? MultiPullCount : 1;

            // ?�리?�탈 부�?체크
            if (currentCrystal < cost)
            {
                ShowToastMessage($"크리스탈이 부족합니다.\n필요: {cost} / 보유: {currentCrystal}");
                return;
            }

            // ?�버�?보석 뽑기 ?�청 ?�송
            var request = new GemGachaRequest
            {
                PullCount = (uint)count
            };

            var envelope = new Envelope
            {
                Type = MessageType.GemGachaRequest,
                GemGachaRequest = request
            };

            NetworkManager.Instance.SendMessage(envelope);
            Debug.Log($"ShopTabController: 보석 뽑기 ?�청 ?�송 (개수: {count}, 비용: {cost})");
        }

        private void OnGemGachaResult(GemGachaResult result)
        {
            if (result == null)
            {
                Debug.LogError("ShopTabController: GemGachaResult is null");
                return;
            }

            if (!result.Success)
            {
                Debug.LogWarning($"ShopTabController: 보석 뽑기 실패 - {result.ErrorCode}");

                string errorMessage = result.ErrorCode switch
                {
                    "INSUFFICIENT_CRYSTAL" => "크리스탈이 부족합니다.",
                    "INVENTORY_FULL" => "보석 인벤토리가 가득 찼습니다.",
                    _ => $"보석 뽑기 실패: {result.ErrorCode}"
                };

                ShowToastMessage(errorMessage);
                return;
            }

            currentCrystal = result.RemainingCrystal;

            // 보석 ?�득 결과 모달 ?�시
            ShowGemGachaResultModal(result);
        }

        /// <summary>
        /// 보석 가�?결과 모달 ?�시
        /// </summary>
        private void ShowGemGachaResultModal(GemGachaResult result)
        {
            if (gemGachaResultModal == null)
            {
                Debug.LogWarning("ShopTabController: gemGachaResultModal is null");
                return;
            }

            int pullCount = result.Gems.Count >= 10 ? MultiPullCount : 1;

            gemGachaResultModal.SetResult(
                result,
                pullCount,
                currentCrystal,
                OnPullAgainFromModal
            );
        }

        /// <summary>
        /// 모달에서 "N번 다시 뽑기" 콜백
        /// </summary>
        private void OnPullAgainFromModal(int pullCount)
        {
            bool isMulti = (pullCount == MultiPullCount);
            OnGemPullClicked(isMulti);
        }

        #endregion


        #region Message & Cache

        private void SubscribeMessageHandler()
        {
            if (messageHandler == null)
            {
                messageHandler = MessageHandler.Instance;
            }
            if (messageHandler == null) return;

            messageHandler.OnHandshakeResult += HandleHandshake;
            messageHandler.OnUserDataSnapshot += HandleSnapshot;
            messageHandler.OnCurrencyUpdate += HandleCurrencyUpdate;
            messageHandler.OnGemGachaResult += OnGemGachaResult;

            ApplyLastKnownCurrency();
        }

        private void UnsubscribeMessageHandler()
        {
            if (messageHandler == null) return;

            messageHandler.OnHandshakeResult -= HandleHandshake;
            messageHandler.OnUserDataSnapshot -= HandleSnapshot;
            messageHandler.OnCurrencyUpdate -= HandleCurrencyUpdate;
            messageHandler.OnGemGachaResult -= OnGemGachaResult;
        }

        private void HandleHandshake(HandshakeResponse res)
        {
            if (res?.Snapshot != null)
            {
                HandleSnapshot(res.Snapshot);
            }
        }

        private void HandleSnapshot(UserDataSnapshot snapshot)
        {
            ApplyResourceCache();
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            ApplyResourceCache();
        }

        private void ApplyLastKnownCurrency()
        {
            ApplyResourceCache();
        }

        private void SubscribeResourceCache()
        {
            if (resourceCache != null) return;
            resourceCache = UserResourceCache.Instance;
            if (resourceCache != null)
            {
                resourceCache.OnChanged += HandleResourceCacheChanged;
                ApplyResourceCache();
            }
        }

        private void UnsubscribeResourceCache()
        {
            if (resourceCache == null) return;
            resourceCache.OnChanged -= HandleResourceCacheChanged;
            resourceCache = null;
        }

        private void HandleResourceCacheChanged()
        {
            ApplyResourceCache();
        }

        private void ApplyResourceCache()
        {
            if (resourceCache == null) return;

            if (resourceCache.Crystal.HasValue)
            {
                currentCrystal = resourceCache.Crystal.Value;
                RefreshData();

                if (gemGachaResultModal != null && gemGachaResultModal.gameObject.activeSelf)
                {
                    gemGachaResultModal.UpdateCrystal(currentCrystal);
                }
            }
        }

        #endregion
    }
}



