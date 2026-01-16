using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Infinitepickaxe;
using InfinitePickaxe.Client.Metadata;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 보석 정보 툴팁 모달 (싱글톤)
    /// Resources/UI/GemTooltipModal.prefab 로드
    /// </summary>
    public sealed class GemTooltipModal : MonoBehaviour
    {
        private static GemTooltipModal instance;
        public static GemTooltipModal Instance
        {
            get
            {
                if (instance == null)
                {
                    CreateInstance();
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private Image gradeBorder;
        [SerializeField] private Image gemIcon;
        [SerializeField] private TextMeshProUGUI gemNameText;
        [SerializeField] private TextMeshProUGUI gemGradeText;
        [SerializeField] private TextMeshProUGUI gemTypeText;
        [SerializeField] private TextMeshProUGUI gemStatText;
        [SerializeField] private TextMeshProUGUI gemDescriptionText;

        [Header("Position Settings")]
        [SerializeField] private Vector2 offset = new Vector2(20f, 0f);
        [SerializeField] private float edgePadding = 10f;

        private RectTransform modalPanelRectTransform;
        private RectTransform canvasRectTransform;
        private Canvas parentCanvas;
        private bool isListeningForOutsideClick;
        private GemMetaResolver metaResolver;

        private void Update()
        {
            if (!isListeningForOutsideClick) return;

            // 마우스 클릭 또는 터치 감지 (모바일 + PC)
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                // 클릭 위치가 툴팁 영역 외부인지 확인
                if (IsPointerOutsideTooltip())
                {
                    Hide();
                }
            }
        }

        /// <summary>
        /// 현재 포인터가 툴팁 영역 외부에 있는지 확인
        /// </summary>
        private bool IsPointerOutsideTooltip()
        {
            if (modalPanel == null || !modalPanel.activeSelf) return true;

            // EventSystem을 통해 현재 클릭된 UI 요소 확인
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // 클릭된 UI 요소 중 ModalPanel 또는 그 자식이 있는지 확인
            foreach (var result in results)
            {
                if (result.gameObject == modalPanel || result.gameObject.transform.IsChildOf(modalPanel.transform))
                {
                    return false; // 툴팁 영역 내부 클릭
                }
            }

            return true; // 툴팁 영역 외부 클릭
        }

        private static void CreateInstance()
        {
            // Resources에서 프리팹 로드
            var prefab = Resources.Load<GameObject>("UI/GemTooltipModal");
            if (prefab == null)
            {
                Debug.LogError("[GemTooltipModal] Resources/UI/GemTooltipModal.prefab를 찾을 수 없습니다!");
                return;
            }

            // Canvas 찾기 (최상위 Canvas)
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[GemTooltipModal] Canvas를 찾을 수 없습니다!");
                return;
            }

            // 인스턴스 생성
            var go = Instantiate(prefab, canvas.transform);
            go.name = "GemTooltipModal";
            instance = go.GetComponent<GemTooltipModal>();

            if (instance == null)
            {
                instance = go.AddComponent<GemTooltipModal>();
            }

            instance.Initialize();
            instance.Hide();

            DontDestroyOnLoad(go);
        }

        private void Initialize()
        {
            parentCanvas = GetComponentInParent<Canvas>();
            canvasRectTransform = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : null;
            metaResolver = new GemMetaResolver();

            AutoBindReferences();

            if (modalPanel != null)
            {
                modalPanelRectTransform = modalPanel.GetComponent<RectTransform>();
            }
        }

        private void AutoBindReferences()
        {
            if (modalPanel == null)
            {
                modalPanel = transform.Find("ModalPanel")?.gameObject;
            }

            if (modalPanel == null) return;

            var panel = modalPanel.transform;

            if (gradeBorder == null)
            {
                gradeBorder = panel.Find("GradeBorder")?.GetComponent<Image>();
            }

            if (gemIcon == null)
            {
                gemIcon = panel.Find("GradeBorder/GemIcon")?.GetComponent<Image>();
            }

            if (gemNameText == null)
            {
                gemNameText = panel.Find("GemNameText")?.GetComponent<TextMeshProUGUI>();
            }

            if (gemGradeText == null)
            {
                gemGradeText = panel.Find("GemGradeText")?.GetComponent<TextMeshProUGUI>();
            }

            if (gemTypeText == null)
            {
                gemTypeText = panel.Find("GemTypeText")?.GetComponent<TextMeshProUGUI>();
            }

            if (gemStatText == null)
            {
                gemStatText = panel.Find("StatText")?.GetComponent<TextMeshProUGUI>();
            }

            if (gemDescriptionText == null)
            {
                gemDescriptionText = panel.Find("Description")?.GetComponent<TextMeshProUGUI>();
            }

            if (modalPanelRectTransform == null && modalPanel != null)
            {
                modalPanelRectTransform = modalPanel.GetComponent<RectTransform>();
            }
        }

        /// <summary>
        /// 툴팁 표시
        /// </summary>
        public void Show(GemInfo gemInfo, Vector3 worldPosition)
        {
            if (gemInfo == null || modalPanel == null) return;

            // 보석 정보 업데이트
            UpdateGemInfo(gemInfo);

            // 위치 조정
            PositionTooltip(worldPosition);

            // 모달 활성화
            modalPanel.SetActive(true);

            // 외부 클릭 감지 시작
            isListeningForOutsideClick = true;
        }

        /// <summary>
        /// 툴팁 숨김
        /// </summary>
        public void Hide()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }

            // 외부 클릭 감지 중지
            isListeningForOutsideClick = false;
        }

        /// <summary>
        /// 보석 정보 업데이트
        /// </summary>
        private void UpdateGemInfo(GemInfo gemInfo)
        {
            // 등급 테두리 색상
            if (gradeBorder != null)
            {
                gradeBorder.color = GetGradeColor(gemInfo.Grade);
            }

            // 보석 아이콘
            if (gemIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(gemInfo);
                gemIcon.sprite = sprite;
                gemIcon.enabled = (sprite != null);
            }

            // 보석 이름
            if (gemNameText != null)
            {
                gemNameText.text = GetGemDisplayName(gemInfo);
                gemNameText.color = GetGradeColor(gemInfo.Grade);
            }

            // 보석 등급
            if (gemGradeText != null)
            {
                gemGradeText.text = GetGradeLabel(gemInfo.Grade);
                gemGradeText.color = GetGradeColor(gemInfo.Grade);
            }

            // 보석 타입
            if (gemTypeText != null)
            {
                gemTypeText.text = GetGemTypeName(gemInfo.Type);
            }

            // 보석 스탯
            if (gemStatText != null)
            {
                float statValue = gemInfo.StatMultiplier / 100f;
                gemStatText.text = $"+{statValue:0.#}%";
            }

            // 보석 설명
            if (gemDescriptionText != null)
            {
                gemDescriptionText.text = GetGemDescription(gemInfo);
            }
        }

        /// <summary>
        /// 툴팁 위치 조정 (화면 경계 체크)
        /// </summary>
        private void PositionTooltip(Vector3 worldPosition)
        {
            if (modalPanelRectTransform == null || parentCanvas == null || canvasRectTransform == null) return;

            // 월드 위치를 캔버스 로컬 위치로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, worldPosition),
                parentCanvas.worldCamera,
                out Vector2 localPosition
            );

            // 초기 위치: 우측에 배치
            Vector2 targetPosition = localPosition + offset;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(modalPanelRectTransform);

            // 화면 경계 체크
            Rect canvasRect = canvasRectTransform.rect;
            Vector2 modalSize = modalPanelRectTransform.rect.size;
            Vector2 modalPivot = modalPanelRectTransform.pivot;

            // 우측 경계 초과 시 좌측에 배치
            if (targetPosition.x + modalSize.x * (1f - modalPivot.x) > canvasRect.xMax - edgePadding)
            {
                float rightEdge = localPosition.x - offset.x;
                targetPosition.x = rightEdge - modalSize.x * (1f - modalPivot.x);
            }

            // 좌측 경계 초과 시 우측으로 강제
            if (targetPosition.x - modalSize.x * modalPivot.x < canvasRect.xMin + edgePadding)
            {
                targetPosition.x = canvasRect.xMin + edgePadding + modalSize.x * modalPivot.x;
            }

            // 상단 경계 초과 시 아래로 이동
            if (targetPosition.y + modalSize.y * (1f - modalPivot.y) > canvasRect.yMax - edgePadding)
            {
                targetPosition.y = canvasRect.yMax - edgePadding - modalSize.y * (1f - modalPivot.y);
            }

            // 하단 경계 초과 시 위로 이동
            if (targetPosition.y - modalSize.y * modalPivot.y < canvasRect.yMin + edgePadding)
            {
                targetPosition.y = canvasRect.yMin + edgePadding + modalSize.y * modalPivot.y;
            }

            modalPanelRectTransform.anchoredPosition = targetPosition;
        }

        /// <summary>
        /// 보석 표시 이름 가져오기
        /// </summary>
        private string GetGemDisplayName(GemInfo gem)
        {
            if (gem == null) return string.Empty;

            if (!string.IsNullOrEmpty(gem.Name))
            {
                return gem.Name;
            }

            if (metaResolver != null && metaResolver.TryGetDefinition(gem.GemId, out var def) && !string.IsNullOrEmpty(def.Name))
            {
                return def.Name;
            }

            return gem.GemId.ToString();
        }

        /// <summary>
        /// 등급 라벨 가져오기
        /// </summary>
        private string GetGradeLabel(GemGrade grade)
        {
            if (metaResolver != null && metaResolver.TryGetGrade((uint)grade, out var meta) && !string.IsNullOrEmpty(meta.DisplayName))
            {
                return meta.DisplayName;
            }

            return grade.ToString();
        }

        /// <summary>
        /// 등급 색상 가져오기
        /// </summary>
        private Color GetGradeColor(GemGrade grade)
        {
            return grade switch
            {
                GemGrade.Common => Color.white,                     // 흰색
                GemGrade.Rare => new Color(0.3f, 0.9f, 0.3f),      // 연두색
                GemGrade.Epic => new Color(0.3f, 0.6f, 1.0f),      // 파란색
                GemGrade.Hero => new Color(0.8f, 0.3f, 0.9f),      // 보라색
                GemGrade.Legendary => new Color(1.0f, 1.0f, 0.2f), // 노란색
                _ => Color.white
            };
        }

        /// <summary>
        /// 보석 타입명 가져오기
        /// </summary>
        private string GetGemTypeName(GemType gemType)
        {
            if (metaResolver != null && metaResolver.TryGetType((uint)gemType, out var meta) && !string.IsNullOrEmpty(meta.DisplayName))
            {
                return meta.DisplayName;
            }

            return gemType.ToString();
        }

        /// <summary>
        /// 보석 설명 가져오기
        /// </summary>
        private string GetGemDescription(GemInfo gem)
        {
            if (gem == null) return string.Empty;

            string typeName = GetGemTypeName(gem.Type);
            float statValue = gem.StatMultiplier / 100f;

            return gem.Type switch
            {
                GemType.AttackSpeed => $"곡괭이의 공격 속도를 {statValue:0.#}% 증가시킵니다.",
                GemType.CritRate => $"크리티컬 확률을 {statValue:0.#}% 증가시킵니다.",
                GemType.CritDmg => $"크리티컬 데미지를 {statValue:0.#}% 증가시킵니다.",
                _ => "알 수 없는 효과입니다."
            };
        }
    }
}
