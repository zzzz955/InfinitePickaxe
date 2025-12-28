using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 가챠 결과 모달에서 개별 보석 아이템 표시 (간소화 버전)
    /// 아이콘과 등급별 배경색만 표시
    /// </summary>
    public class GemResultItemView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image gradeBorder;
        [SerializeField] private Image gemIcon;

        [Header("Grade Colors")]
        [SerializeField] private Color commonColor = Color.white; // 일반 (흰색)
        [SerializeField] private Color rareColor = new Color(0.3f, 0.9f, 0.3f); // 고급 (연두색)
        [SerializeField] private Color epicColor = new Color(0.3f, 0.6f, 1.0f); // 희귀 (파란색)
        [SerializeField] private Color heroColor = new Color(0.8f, 0.3f, 0.9f); // 영웅 (보라색)
        [SerializeField] private Color legendaryColor = new Color(1.0f, 1.0f, 0.2f); // 전설 (노란색)

        /// <summary>
        /// 보석 데이터 설정
        /// </summary>
        public void SetGem(GemInfo gem)
        {
            if (gem == null)
            {
                Debug.LogWarning("GemResultItemView: GemInfo is null");
                return;
            }

            // 등급별 배경 색상 설정
            if (gradeBorder != null)
            {
                gradeBorder.color = GetGradeColor(gem.Grade);
            }

            // 보석 아이콘 설정
            if (gemIcon != null)
            {
                var sprite = GemSpriteLoader.GetGemSprite(gem);
                gemIcon.sprite = sprite;
                gemIcon.enabled = (sprite != null);
            }
        }

        /// <summary>
        /// 등급별 배경 색상 반환
        /// </summary>
        private Color GetGradeColor(GemGrade grade)
        {
            return grade switch
            {
                GemGrade.Common => commonColor,
                GemGrade.Rare => rareColor,
                GemGrade.Epic => epicColor,
                GemGrade.Hero => heroColor,
                GemGrade.Legendary => legendaryColor,
                _ => Color.white
            };
        }
    }
}
