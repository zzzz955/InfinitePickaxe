using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InfinitePickaxe.Client.UI.Common;
using Object = UnityEngine.Object;

namespace InfinitePickaxe.Client.EditorTools
{
    /// <summary>
    /// BootStrap 씬에 로딩 오버레이 + 모달 UI를 자동 배치하는 빌더
    /// 메뉴: Tools/Bootstrap/Build UI
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        private const string CanvasName = "BootstrapCanvas";
        private const string LoadingOverlayName = "LoadingOverlay";
        private const string ModalLayerName = "ModalLayer";
        private const string ModalPanelName = "ModalPanel";

        [MenuItem("Tools/Bootstrap/Build UI")]
        public static void BuildUI()
        {
            var canvasGO = GameObject.Find(CanvasName) ?? CreateCanvas();
            EnsureEventSystem();

            var overlay = FindChild(canvasGO.transform, LoadingOverlayName) ?? CreateLoadingOverlay(canvasGO.transform);
            var modalLayer = FindChild(canvasGO.transform, ModalLayerName) ?? CreateModalLayer(canvasGO.transform);
            if (FindChild(modalLayer.transform, ModalPanelName) == null)
            {
                CreateModalPanel(modalLayer.transform);
            }

            Selection.activeObject = canvasGO;
            Debug.Log("Bootstrap UI built/updated.");
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child.gameObject;
                var result = FindChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static GameObject CreateCanvas()
        {
            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        private static GameObject CreateLoadingOverlay(Transform parent)
        {
            var go = new GameObject(LoadingOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            StretchFull(rt);

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);
            img.raycastTarget = true;

            // Spinner
            var spinner = new GameObject("Spinner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UIRotator));
            var srt = spinner.GetComponent<RectTransform>();
            srt.SetParent(rt, false);
            srt.sizeDelta = new Vector2(160, 160);
            srt.anchoredPosition = Vector2.zero;
            var simg = spinner.GetComponent<Image>();
            simg.sprite = GetSimpleWhiteSprite();
            simg.color = new Color(1f, 0.85f, 0.4f, 1f);
            simg.type = Image.Type.Filled;
            simg.fillMethod = Image.FillMethod.Radial360;

            // Status text
            var status = CreateText(rt, "StatusText", "부트스트랩 중...", 48, TextAlignmentOptions.Center);
            var srtxt = status.GetComponent<RectTransform>();
            srtxt.anchoredPosition = new Vector2(0, -150);

            Undo.RegisterCreatedObjectUndo(go, "Create LoadingOverlay");
            return go;
        }

        private static GameObject CreateModalLayer(Transform parent)
        {
            var go = new GameObject(ModalLayerName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            StretchFull(rt);
            Undo.RegisterCreatedObjectUndo(go, "Create ModalLayer");
            return go;
        }

        private static GameObject CreateModalPanel(Transform parent)
        {
            var panel = new GameObject(ModalPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = panel.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(900, 540);
            rt.anchoredPosition = Vector2.zero;
            var img = panel.GetComponent<Image>();
            img.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            img.raycastTarget = true;
            panel.SetActive(false);

            // Title
            var title = CreateText(rt, "Title", "알림", 56, TextAlignmentOptions.Center);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchoredPosition = new Vector2(0, 170);

            // Message
            var message = CreateText(rt, "Message", "메시지 내용", 38, TextAlignmentOptions.Center);
            var msgRT = message.GetComponent<RectTransform>();
            msgRT.sizeDelta = new Vector2(820, 220);
            msgRT.anchoredPosition = new Vector2(0, -10);

            // Buttons
            var buttonRow = new GameObject("ButtonRow", typeof(RectTransform));
            var brt = buttonRow.GetComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.sizeDelta = new Vector2(840, 120);
            brt.anchoredPosition = new Vector2(0, -190);

            CreateButton(brt, "PrimaryButton", "확인", new Vector2(-180, 0));
            CreateButton(brt, "SecondaryButton", "종료", new Vector2(180, 0));

            Undo.RegisterCreatedObjectUndo(panel, "Create ModalPanel");
            return panel;
        }

        private static GameObject CreateText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(800, 80);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(320, 100);
            rt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.6f, 0.85f, 1f);
            img.sprite = GetSimpleWhiteSprite();

            var txt = CreateText(rt, "Label", label, 40, TextAlignmentOptions.Center);
            var trt = txt.GetComponent<RectTransform>();
            trt.sizeDelta = rt.sizeDelta;
            trt.anchoredPosition = Vector2.zero;

            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Sprite GetSimpleWhiteSprite()
        {
            // 1x1 흰색 텍스처로 단순 스프라이트 생성
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
