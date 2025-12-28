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
        [SerializeField] private Color commonColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color rareColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color epicColor = new Color(0.6f, 0.2f, 1f);
        [SerializeField] private Color heroColor = new Color(1f, 0.7f, 0f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.3f, 0f);

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
