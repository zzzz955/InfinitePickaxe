using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// MiningTabController의 PickaxeInfoModal 스크롤뷰 관리 기능
    /// </summary>
    public partial class MiningTabController
    {
        [Header("Pickaxe Info Modal - Scroll View")]
        [SerializeField] private ScrollRect pickaxeInfoScrollRect;
        [SerializeField] private RectTransform pickaxeInfoScrollContent;

        /// <summary>
        /// PickaxeInfoModal 스크롤뷰 AutoBind
        /// AutoBindPickaxeInfoModalReferences()에서 호출
        /// </summary>
        private void AutoBindPickaxeInfoScrollView()
        {
            if (pickaxeInfoModal == null) return;

            var root = pickaxeInfoModal.transform;

            // ScrollView 바인딩
            if (pickaxeInfoScrollRect == null)
            {
                var scrollViewTf = FindChildRecursive(root, "ScrollView");
                if (scrollViewTf != null)
                {
                    pickaxeInfoScrollRect = scrollViewTf.GetComponent<ScrollRect>();
                }
            }

            // Content 바인딩
            if (pickaxeInfoScrollContent == null)
            {
                var contentTf = FindChildRecursive(root, "Content");
                if (contentTf != null)
                {
                    pickaxeInfoScrollContent = contentTf.GetComponent<RectTransform>();
                }
            }
        }

        /// <summary>
        /// PickaxeInfoModal 열릴 때 스크롤 위치 초기화 (상단으로)
        /// OpenPickaxeInfoModal()에서 호출
        /// </summary>
        private void ResetPickaxeInfoScrollPosition()
        {
            if (pickaxeInfoScrollRect != null)
            {
                // 1 = 상단, 0 = 하단
                pickaxeInfoScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// 스크롤뷰 강제 레이아웃 갱신
        /// 콘텐츠 변경 시 호출 (예: GemSection 업데이트 후)
        /// </summary>
        private void RefreshPickaxeInfoScrollLayout()
        {
            if (pickaxeInfoScrollContent != null)
            {
                // ContentSizeFitter와 LayoutGroup이 제대로 동작하도록 강제 갱신
                LayoutRebuilder.ForceRebuildLayoutImmediate(pickaxeInfoScrollContent);
            }
        }
    }
}
