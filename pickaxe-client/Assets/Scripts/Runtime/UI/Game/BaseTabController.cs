using UnityEngine;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 모든 게임 탭의 기본 클래스
    /// 탭 활성화/비활성화 시 호출되는 공통 메서드 제공
    /// </summary>
    public abstract class BaseTabController : MonoBehaviour
    {
        [Header("Base Settings")]
        [SerializeField] protected bool initializeOnAwake = true;

        protected bool isInitialized;
        protected bool isActive;

        protected virtual void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        protected virtual void OnEnable()
        {
            OnTabShown();
        }

        protected virtual void OnDisable()
        {
            OnTabHidden();
        }

        /// <summary>
        /// 탭 초기화 (최초 1회만 실행)
        /// </summary>
        protected virtual void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;

#if UNITY_EDITOR || DEBUG_TAB
            // Debug.Log($"{GetType().Name}: 초기화 완료");
#endif
        }

        /// <summary>
        /// 탭이 표시될 때 호출
        /// </summary>
        protected virtual void OnTabShown()
        {
            isActive = true;

#if UNITY_EDITOR || DEBUG_TAB
            // Debug.Log($"{GetType().Name}: 탭 표시됨");
#endif
        }

        /// <summary>
        /// 탭이 숨겨질 때 호출
        /// </summary>
        protected virtual void OnTabHidden()
        {
            isActive = false;

#if UNITY_EDITOR || DEBUG_TAB
            // Debug.Log($"{GetType().Name}: 탭 숨김");
#endif
        }

        /// <summary>
        /// 탭 데이터 갱신 (외부에서 호출 가능)
        /// </summary>
        public virtual void RefreshData()
        {
            // 하위 클래스에서 구현
        }
    }
}
