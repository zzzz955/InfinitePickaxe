using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 보석 아이콘에 붙여서 0.5초 이상 꾹 누르면 툴팁을 표시하는 컴포넌트
    /// ScrollView 내부에서도 작동하도록 드래그 감지 기능 포함
    /// </summary>
    public sealed class GemTooltipTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Settings")]
        [SerializeField] private float longPressThreshold = 0.5f;
        [SerializeField] private bool enableDebugLog = false;

        private GemInfo gemInfo;
        private bool isPressed;
        private float pressTime;
        private bool tooltipShown;
        private bool isDragging;
        private ScrollRect parentScrollRect;

        private void Awake()
        {
            // 부모 ScrollRect 찾기 (있다면)
            parentScrollRect = GetComponentInParent<ScrollRect>();

            if (enableDebugLog)
            {
                Debug.Log($"[GemTooltipTrigger] Awake: parentScrollRect = {(parentScrollRect != null ? parentScrollRect.name : "null")}");
            }
        }

        /// <summary>
        /// 보석 정보 설정
        /// </summary>
        public void SetGemInfo(GemInfo info)
        {
            gemInfo = info;
        }

        /// <summary>
        /// 보석 ID로 설정 (간편 버전)
        /// </summary>
        public void SetGemId(uint gemId)
        {
            // GemInfo 생성 (최소 정보만)
            gemInfo = new GemInfo
            {
                GemId = gemId
            };
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (gemInfo == null) return;

            isPressed = true;
            pressTime = 0f;
            tooltipShown = false;
            isDragging = false;

            if (enableDebugLog)
            {
                Debug.Log("[GemTooltipTrigger] OnPointerDown");
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (enableDebugLog)
            {
                Debug.Log("[GemTooltipTrigger] OnInitializePotentialDrag");
            }

            // ScrollRect로 이벤트 전파
            if (parentScrollRect != null)
            {
                parentScrollRect.OnInitializePotentialDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            pressTime = 0f;
            isDragging = false;

            // 툴팁이 표시된 경우 이벤트 소비 (하위 버튼 클릭 방지)
            if (tooltipShown)
            {
                eventData.Use();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPressed = false;
            pressTime = 0f;
            // 포인터가 벗어나도 툴팁은 유지 (다른 영역 클릭 시 닫힘)
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 툴팁이 표시된 경우 클릭 이벤트 소비 (하위 버튼 클릭 방지)
            if (tooltipShown)
            {
                eventData.Use();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (enableDebugLog)
            {
                Debug.Log("[GemTooltipTrigger] OnBeginDrag");
            }

            // 드래그 시작 시 툴팁 표시 취소
            isPressed = false;
            pressTime = 0f;
            isDragging = true;

            // 부모 ScrollRect가 있다면 드래그 이벤트 전파
            if (parentScrollRect != null)
            {
                parentScrollRect.OnBeginDrag(eventData);

                if (enableDebugLog)
                {
                    Debug.Log($"[GemTooltipTrigger] Forwarded to ScrollRect: {parentScrollRect.name}");
                }
            }
            else if (enableDebugLog)
            {
                Debug.LogWarning("[GemTooltipTrigger] parentScrollRect is null!");
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // 부모 ScrollRect가 있다면 드래그 이벤트 전파
            if (parentScrollRect != null)
            {
                parentScrollRect.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;

            // 부모 ScrollRect가 있다면 드래그 이벤트 전파
            if (parentScrollRect != null)
            {
                parentScrollRect.OnEndDrag(eventData);
            }
        }

        private void Update()
        {
            if (!isPressed || tooltipShown || isDragging) return;

            pressTime += Time.deltaTime;

            if (pressTime >= longPressThreshold)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[GemTooltipTrigger] Showing tooltip after {pressTime:F2}s");
                }

                ShowTooltip();
                tooltipShown = true;
            }
        }

        private void ShowTooltip()
        {
            if (gemInfo == null) return;

            // 화면 위치 계산
            Vector3 worldPosition = transform.position;

            GemTooltipModal.Instance?.Show(gemInfo, worldPosition);
        }

        private void HideTooltip()
        {
            GemTooltipModal.Instance?.Hide();
            tooltipShown = false;
        }

        private void OnDisable()
        {
            if (tooltipShown)
            {
                HideTooltip();
            }
        }
    }
}
