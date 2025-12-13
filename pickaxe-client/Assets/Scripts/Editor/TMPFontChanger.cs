using UnityEngine;
using UnityEditor;
using TMPro;

namespace InfinitePickaxe.Client.Editor
{
    /// <summary>
    /// Hierarchy의 모든 TextMeshProUGUI 컴포넌트의 폰트를 일괄 변경하는 에디터 도구
    /// 사용법: Unity Editor 메뉴 → Tools → Change All TMP Fonts
    /// </summary>
    public class TMPFontChanger : EditorWindow
    {
        private const string DEFAULT_FONT_PATH = "Assets/Fonts/NeoDunggeunmoPro-Regular SDF.asset";

        private TMP_FontAsset targetFont;
        private string fontPath = DEFAULT_FONT_PATH;

        [MenuItem("Tools/Change All TMP Fonts")]
        public static void ShowWindow()
        {
            var window = GetWindow<TMPFontChanger>("TMP Font Changer");
            window.minSize = new Vector2(400, 250);
        }

        private void OnEnable()
        {
            // 기본 폰트 로드 시도
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        }

        private void OnGUI()
        {
            GUILayout.Label("TextMeshPro 폰트 일괄 변경", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "현재 씬의 Hierarchy에 있는 모든 TextMeshProUGUI 컴포넌트의 폰트를 " +
                "선택한 폰트로 일괄 변경합니다.\n\n" +
                "[주의] 실행 전 씬을 저장하는 것을 권장합니다.\n" +
                "[안전] Undo(Ctrl+Z)로 실행 취소 가능합니다.",
                MessageType.Info
            );

            GUILayout.Space(15);

            // 폰트 선택
            EditorGUILayout.LabelField("변경할 폰트:", EditorStyles.boldLabel);

            var newFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                "Target Font",
                targetFont,
                typeof(TMP_FontAsset),
                false
            );

            if (newFont != targetFont)
            {
                targetFont = newFont;
                if (targetFont != null)
                {
                    fontPath = AssetDatabase.GetAssetPath(targetFont);
                }
            }

            GUILayout.Space(10);

            // 폰트 경로 표시
            if (targetFont != null)
            {
                EditorGUILayout.HelpBox($"폰트 경로: {fontPath}", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("[주의] 폰트를 선택해주세요!", MessageType.Warning);
            }

            GUILayout.Space(15);

            // 실행 버튼
            GUI.enabled = targetFont != null;

            if (GUILayout.Button("모든 TextMeshProUGUI 폰트 변경", GUILayout.Height(50)))
            {
                ChangeAllFonts();
            }

            GUI.enabled = true;

            GUILayout.Space(10);

            // 빠른 설정 버튼
            if (GUILayout.Button("기본 폰트로 설정 (NeoDunggeunmoPro-Regular SDF)", GUILayout.Height(40)))
            {
                SetDefaultFont();
            }

            GUILayout.Space(10);

            // 선택된 오브젝트만 변경
            if (GUILayout.Button("선택된 GameObject만 변경", GUILayout.Height(40)))
            {
                ChangeSelectedObjects();
            }
        }

        private void SetDefaultFont()
        {
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DEFAULT_FONT_PATH);

            if (targetFont != null)
            {
                fontPath = DEFAULT_FONT_PATH;
                Debug.Log($"기본 폰트로 설정됨: {DEFAULT_FONT_PATH}");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "폰트를 찾을 수 없음",
                    $"다음 경로에서 폰트를 찾을 수 없습니다:\n{DEFAULT_FONT_PATH}\n\n" +
                    "폰트 파일이 해당 경로에 있는지 확인해주세요.",
                    "확인"
                );
            }
        }

        private void ChangeAllFonts()
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("오류", "폰트를 선택해주세요!", "확인");
                return;
            }

            // 현재 씬의 모든 TextMeshProUGUI 컴포넌트 찾기
            var allTexts = FindObjectsOfType<TextMeshProUGUI>(true); // includeInactive = true

            if (allTexts.Length == 0)
            {
                EditorUtility.DisplayDialog("알림", "씬에 TextMeshProUGUI 컴포넌트가 없습니다.", "확인");
                return;
            }

            // 확인 다이얼로그
            if (!EditorUtility.DisplayDialog(
                "폰트 일괄 변경 확인",
                $"총 {allTexts.Length}개의 TextMeshProUGUI 컴포넌트의 폰트를 변경하시겠습니까?\n\n" +
                $"대상 폰트: {targetFont.name}",
                "변경",
                "취소"))
            {
                return;
            }

            // Undo 그룹 시작
            Undo.SetCurrentGroupName("Change All TMP Fonts");
            int undoGroup = Undo.GetCurrentGroup();

            int changedCount = 0;
            int skippedCount = 0;

            foreach (var tmp in allTexts)
            {
                if (tmp.font != targetFont)
                {
                    Undo.RecordObject(tmp, "Change TMP Font");
                    tmp.font = targetFont;
                    EditorUtility.SetDirty(tmp);
                    changedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            // Undo 그룹 종료
            Undo.CollapseUndoOperations(undoGroup);

            // 결과 로그
            Debug.Log($"TextMeshProUGUI 폰트 변경 완료!\n" +
                     $"- 변경됨: {changedCount}개\n" +
                     $"- 스킵됨: {skippedCount}개 (이미 동일한 폰트)\n" +
                     $"- 총 개수: {allTexts.Length}개\n" +
                     $"- 적용된 폰트: {targetFont.name}");

            EditorUtility.DisplayDialog(
                "완료",
                $"폰트 변경이 완료되었습니다!\n\n" +
                $"변경됨: {changedCount}개\n" +
                $"스킵됨: {skippedCount}개\n" +
                $"총 개수: {allTexts.Length}개",
                "확인"
            );
        }

        private void ChangeSelectedObjects()
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("오류", "폰트를 선택해주세요!", "확인");
                return;
            }

            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("알림", "GameObject를 선택해주세요!", "확인");
                return;
            }

            // 선택된 GameObject들에서 TextMeshProUGUI 컴포넌트 찾기
            var selectedTexts = new System.Collections.Generic.List<TextMeshProUGUI>();

            foreach (var go in Selection.gameObjects)
            {
                // 자기 자신과 모든 자식에서 TextMeshProUGUI 찾기
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                selectedTexts.AddRange(texts);
            }

            if (selectedTexts.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "선택된 GameObject에 TextMeshProUGUI 컴포넌트가 없습니다.", "확인");
                return;
            }

            // 확인 다이얼로그
            if (!EditorUtility.DisplayDialog(
                "폰트 변경 확인",
                $"선택된 GameObject에서 총 {selectedTexts.Count}개의 TextMeshProUGUI 컴포넌트의 폰트를 변경하시겠습니까?\n\n" +
                $"대상 폰트: {targetFont.name}",
                "변경",
                "취소"))
            {
                return;
            }

            // Undo 그룹 시작
            Undo.SetCurrentGroupName("Change Selected TMP Fonts");
            int undoGroup = Undo.GetCurrentGroup();

            int changedCount = 0;
            int skippedCount = 0;

            foreach (var tmp in selectedTexts)
            {
                if (tmp.font != targetFont)
                {
                    Undo.RecordObject(tmp, "Change TMP Font");
                    tmp.font = targetFont;
                    EditorUtility.SetDirty(tmp);
                    changedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            // Undo 그룹 종료
            Undo.CollapseUndoOperations(undoGroup);

            // 결과 로그
            Debug.Log($"선택된 TextMeshProUGUI 폰트 변경 완료!\n" +
                     $"- 변경됨: {changedCount}개\n" +
                     $"- 스킵됨: {skippedCount}개\n" +
                     $"- 총 개수: {selectedTexts.Count}개\n" +
                     $"- 적용된 폰트: {targetFont.name}");

            EditorUtility.DisplayDialog(
                "완료",
                $"폰트 변경이 완료되었습니다!\n\n" +
                $"변경됨: {changedCount}개\n" +
                $"스킵됨: {skippedCount}개\n" +
                $"총 개수: {selectedTexts.Count}개",
                "확인"
            );
        }

        /// <summary>
        /// 메뉴에서 직접 실행 가능한 단축 기능
        /// </summary>
        [MenuItem("Tools/Quick Change TMP Fonts (Default)")]
        public static void QuickChangeToDefaultFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DEFAULT_FONT_PATH);

            if (font == null)
            {
                EditorUtility.DisplayDialog(
                    "폰트를 찾을 수 없음",
                    $"다음 경로에서 폰트를 찾을 수 없습니다:\n{DEFAULT_FONT_PATH}",
                    "확인"
                );
                return;
            }

            var allTexts = FindObjectsOfType<TextMeshProUGUI>(true);

            if (allTexts.Length == 0)
            {
                EditorUtility.DisplayDialog("알림", "씬에 TextMeshProUGUI 컴포넌트가 없습니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "폰트 일괄 변경",
                $"총 {allTexts.Length}개의 TextMeshProUGUI를 NeoDunggeunmoPro-Regular SDF로 변경하시겠습니까?",
                "변경",
                "취소"))
            {
                return;
            }

            Undo.SetCurrentGroupName("Quick Change TMP Fonts");
            int undoGroup = Undo.GetCurrentGroup();

            int changedCount = 0;

            foreach (var tmp in allTexts)
            {
                if (tmp.font != font)
                {
                    Undo.RecordObject(tmp, "Change TMP Font");
                    tmp.font = font;
                    EditorUtility.SetDirty(tmp);
                    changedCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Quick 폰트 변경 완료! {changedCount}개 변경됨");

            EditorUtility.DisplayDialog("완료", $"{changedCount}개의 폰트가 변경되었습니다!", "확인");
        }
    }
}
