using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public class ShopTabController : BaseTabController
    {
        [Header("Tab Switching UI")]
        [SerializeField] private Button gemsTabButton;
        [SerializeField] private Button iapTabButton;

        [Header("SubTab Content")]
        [SerializeField] private GameObject gemShopSubTab;
        [SerializeField] private GameObject iapShopSubTab;

        private enum SubTab { Gems, IAP }
        private SubTab currentSubTab = SubTab.Gems;

        protected override void Initialize()
        {
            base.Initialize();

            if (gemsTabButton != null)
            {
                gemsTabButton.onClick.RemoveAllListeners();
                gemsTabButton.onClick.AddListener(SwitchToGemTab);
            }

            if (iapTabButton != null)
            {
                iapTabButton.onClick.RemoveAllListeners();
                iapTabButton.onClick.AddListener(SwitchToIAPTab);
            }

            SwitchToGemTab();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        public override void RefreshData()
        {
            switch (currentSubTab)
            {
                case SubTab.Gems:
                    NotifyTabActivated(gemShopSubTab);
                    break;
                case SubTab.IAP:
                    NotifyTabActivated(iapShopSubTab);
                    break;
            }
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

            UpdateTabButtonColors();
            RefreshData();
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

            UpdateTabButtonColors();
            RefreshData();
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

        private void NotifyTabActivated(GameObject target)
        {
            if (target == null) return;

            var behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IShopTabContent content)
                {
                    content.OnTabSelected();
                }
            }
        }
    }
}
