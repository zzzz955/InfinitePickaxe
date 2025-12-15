using UnityEngine;

namespace InfinitePickaxe.Client.UI.Common
{
    /// <summary>
    /// 단순 회전 효과(예: 로딩 스피너)
    /// </summary>
    public class UIRotator : MonoBehaviour
    {
        [SerializeField] private float speedDegPerSec = 180f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -speedDegPerSec * Time.deltaTime);
        }
    }
}
