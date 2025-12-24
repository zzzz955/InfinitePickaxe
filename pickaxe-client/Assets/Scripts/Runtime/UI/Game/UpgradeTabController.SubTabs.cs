using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public partial class UpgradeTabController
    {
        private const int SubTabPickaxeIndex = 0;
        private const int SubTabGemIndex = 1;

        [Header("Upgrade Sub Tabs")]
        [SerializeField] private RectTransform subTabBar;
        [SerializeField] private Button pickaxeTabButton;
        [SerializeField] private Button gemTabButton;
        [SerializeField] private GameObject pickaxePanel;
        [SerializeField] private GameObject gemPanel;
        [SerializeField] private Color subTabSelectedColor = new Color(0.22f, 0.22f, 0.22f, 0.95f);
        [SerializeField] private Color subTabUnselectedColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        private int currentSubTabIndex = SubTabPickaxeIndex;
        private bool subTabInitialized;

        private void InitializeSubTabs()
        {
            if (subTabInitialized) return;

            EnsureSubTabReferences();
            BindSubTabButtons();
            SetSubTab(currentSubTabIndex);

            subTabInitialized = true;
        }

        private void EnsureSubTabReferences()
        {
            var root = transform;

            if (subTabBar == null)
            {
                var barTf = FindChildRecursive(root, "UpgradeSubTabBar");
                if (barTf != null) subTabBar = barTf.GetComponent<RectTransform>();
            }

            if (pickaxeTabButton == null)
            {
                var btnTf = FindChildRecursive(root, "PickaxeTabButton");
                if (btnTf != null) pickaxeTabButton = btnTf.GetComponent<Button>();
            }

            if (gemTabButton == null)
            {
                var btnTf = FindChildRecursive(root, "GemTabButton");
                if (btnTf != null) gemTabButton = btnTf.GetComponent<Button>();
            }

            if (pickaxePanel == null)
            {
                var panelTf = FindChildRecursive(root, "PickaxePanel");
                if (panelTf != null) pickaxePanel = panelTf.gameObject;
            }

            if (gemPanel == null)
            {
                var panelTf = FindChildRecursive(root, "GemPanel");
                if (panelTf != null) gemPanel = panelTf.gameObject;
            }

            if (subTabBar == null)
            {
                Debug.LogWarning("UpgradeTabController: UpgradeSubTabBar를 찾을 수 없습니다. 인스펙터 연결 또는 하이어라키 추가가 필요합니다.");
            }

            if (pickaxePanel == null)
            {
                Debug.LogWarning("UpgradeTabController: PickaxePanel을 찾을 수 없습니다. 기존 강화 UI를 PickaxePanel 하위로 이동하세요.");
            }

            if (gemPanel == null)
            {
                Debug.LogWarning("UpgradeTabController: GemPanel을 찾을 수 없습니다. 보석 UI 패널을 하이어라키에 추가하세요.");
            }
        }

        private void BindSubTabButtons()
        {
            if (pickaxeTabButton != null)
            {
                pickaxeTabButton.onClick.RemoveAllListeners();
                pickaxeTabButton.onClick.AddListener(() => SetSubTab(SubTabPickaxeIndex));
            }

            if (gemTabButton != null)
            {
                gemTabButton.onClick.RemoveAllListeners();
                gemTabButton.onClick.AddListener(() => SetSubTab(SubTabGemIndex));
            }
        }

        private void SetSubTab(int index)
        {
            currentSubTabIndex = NormalizeSubTabIndex(index);

            if (pickaxePanel != null) pickaxePanel.SetActive(currentSubTabIndex == SubTabPickaxeIndex);
            if (gemPanel != null) gemPanel.SetActive(currentSubTabIndex == SubTabGemIndex);

            UpdateSubTabButton(pickaxeTabButton, currentSubTabIndex == SubTabPickaxeIndex);
            UpdateSubTabButton(gemTabButton, currentSubTabIndex == SubTabGemIndex);
        }

        private int NormalizeSubTabIndex(int index)
        {
            index = Mathf.Clamp(index, SubTabPickaxeIndex, SubTabGemIndex);
            if (index == SubTabGemIndex && gemPanel == null) return SubTabPickaxeIndex;
            return index;
        }

        private void UpdateSubTabButton(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected ? subTabSelectedColor : subTabUnselectedColor;
            }

            button.interactable = !selected;
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
