using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 설정 탭 컨트롤러
    /// 사운드, 알림, 계정 설정 등
    /// </summary>
    public class SettingsTabController : BaseTabController
    {
        [Header("Sound UI")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI bgmVolumeText;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;

        [Header("Account UI")]
        [SerializeField] private TextMeshProUGUI accountInfoText;

        [Header("Info UI")]
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button supportButton;

        [Header("Settings Data")]
        [SerializeField] private float bgmVolume = 0.8f;
        [SerializeField] private float sfxVolume = 1.0f;

        protected override void Initialize()
        {
            base.Initialize();

            // 사운드 슬라이더 이벤트 등록
            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(bgmVolume);
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.interactable = false;
            }

            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(sfxVolume);
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.interactable = false;
            }

            // 알림 토글 이벤트 등록
            // 버튼 이벤트 등록
            if (termsButton != null)
            {
                termsButton.onClick.RemoveAllListeners();
                termsButton.interactable = false;
            }

            if (privacyButton != null)
            {
                privacyButton.onClick.RemoveAllListeners();
                privacyButton.interactable = false;
            }

            if (supportButton != null)
            {
                supportButton.onClick.RemoveAllListeners();
                supportButton.interactable = false;
            }

            RefreshData();
        }

        protected override void OnTabShown()
        {
            base.OnTabShown();
            RefreshData();
        }

        /// <summary>
        /// 설정 UI 데이터 갱신
        /// </summary>
        public override void RefreshData()
        {
            UpdateVolumeText();
            UpdateAccountInfo();
            UpdateVersionInfo();
        }

        private void UpdateVolumeText()
        {
            if (bgmVolumeText != null)
            {
                bgmVolumeText.text = $"{(bgmVolume * 100):F0}%";
            }

            if (sfxVolumeText != null)
            {
                sfxVolumeText.text = $"{(sfxVolume * 100):F0}%";
            }
        }

        private void UpdateAccountInfo()
        {
            if (accountInfoText != null)
            {
                // TODO: 실제 계정 정보 표시
                accountInfoText.text = "Google Play 연동";
            }
        }

        private void UpdateVersionInfo()
        {
            if (versionText != null)
            {
                versionText.text = $"버전: {Application.version} (MVP)";
            }
        }

        /// <summary>
        /// BGM 볼륨 변경 이벤트
        /// </summary>
        private void OnBGMVolumeChanged(float value)
        {
            bgmVolume = value;
            UpdateVolumeText();

            // TODO: 실제 오디오 소스 볼륨 변경
#if UNITY_EDITOR || DEBUG_SETTINGS
            Debug.Log($"SettingsTabController: BGM 볼륨 변경 - {value * 100:F0}%");
#endif
        }

        /// <summary>
        /// 효과음 볼륨 변경 이벤트
        /// </summary>
        private void OnSFXVolumeChanged(float value)
        {
            sfxVolume = value;
            UpdateVolumeText();

            // TODO: 실제 오디오 소스 볼륨 변경
#if UNITY_EDITOR || DEBUG_SETTINGS
            Debug.Log($"SettingsTabController: 효과음 볼륨 변경 - {value * 100:F0}%");
#endif
        }

        /// <summary>
        /// 오프라인 알림 토글 변경 이벤트
        /// </summary>

        /// <summary>
        /// 미션 알림 토글 변경 이벤트
        /// </summary>

        /// <summary>
        /// 로그아웃 버튼 클릭 이벤트
        /// </summary>
        /// <summary>
        /// 이용약관 버튼 클릭 이벤트
        /// </summary>
        private void OnTermsClicked()
        {
            // TODO: 이용약관 URL 열기
            Debug.Log("SettingsTabController: 이용약관 버튼 클릭됨");
        }

        /// <summary>
        /// 개인정보처리방침 버튼 클릭 이벤트
        /// </summary>
        private void OnPrivacyClicked()
        {
            // TODO: 개인정보처리방침 URL 열기
            Debug.Log("SettingsTabController: 개인정보처리방침 버튼 클릭됨");
        }

        /// <summary>
        /// 고객지원 버튼 클릭 이벤트
        /// </summary>
        private void OnSupportClicked()
        {
            // TODO: 고객지원 URL 열기
            Debug.Log("SettingsTabController: 고객지원 버튼 클릭됨");
        }

    }
}
