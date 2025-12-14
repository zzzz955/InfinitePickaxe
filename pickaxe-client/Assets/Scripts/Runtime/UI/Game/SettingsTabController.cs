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

        [Header("Notification UI")]
        [SerializeField] private Toggle offlineNotificationToggle;
        [SerializeField] private Toggle missionNotificationToggle;

        [Header("Account UI")]
        [SerializeField] private TextMeshProUGUI accountInfoText;
        [SerializeField] private Button logoutButton;

        [Header("Info UI")]
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button supportButton;

        [Header("Settings Data")]
        [SerializeField] private float bgmVolume = 0.8f;
        [SerializeField] private float sfxVolume = 1.0f;
        [SerializeField] private bool offlineNotification = true;
        [SerializeField] private bool missionNotification = true;

        protected override void Initialize()
        {
            base.Initialize();

            // 사운드 슬라이더 이벤트 등록
            if (bgmSlider != null)
            {
                bgmSlider.value = bgmVolume;
                bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = sfxVolume;
                sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }

            // 알림 토글 이벤트 등록
            if (offlineNotificationToggle != null)
            {
                offlineNotificationToggle.isOn = offlineNotification;
                offlineNotificationToggle.onValueChanged.AddListener(OnOfflineNotificationChanged);
            }

            if (missionNotificationToggle != null)
            {
                missionNotificationToggle.isOn = missionNotification;
                missionNotificationToggle.onValueChanged.AddListener(OnMissionNotificationChanged);
            }

            // 버튼 이벤트 등록
            if (logoutButton != null)
            {
                logoutButton.onClick.AddListener(OnLogoutClicked);
            }

            if (termsButton != null)
            {
                termsButton.onClick.AddListener(OnTermsClicked);
            }

            if (privacyButton != null)
            {
                privacyButton.onClick.AddListener(OnPrivacyClicked);
            }

            if (supportButton != null)
            {
                supportButton.onClick.AddListener(OnSupportClicked);
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
        private void OnOfflineNotificationChanged(bool isOn)
        {
            offlineNotification = isOn;

            // TODO: 알림 설정 저장
#if UNITY_EDITOR || DEBUG_SETTINGS
            Debug.Log($"SettingsTabController: 오프라인 알림 - {(isOn ? "ON" : "OFF")}");
#endif
        }

        /// <summary>
        /// 미션 알림 토글 변경 이벤트
        /// </summary>
        private void OnMissionNotificationChanged(bool isOn)
        {
            missionNotification = isOn;

            // TODO: 알림 설정 저장
#if UNITY_EDITOR || DEBUG_SETTINGS
            Debug.Log($"SettingsTabController: 미션 알림 - {(isOn ? "ON" : "OFF")}");
#endif
        }

        /// <summary>
        /// 로그아웃 버튼 클릭 이벤트
        /// </summary>
        private void OnLogoutClicked()
        {
            // TODO: 로그아웃 처리 및 Title 씬으로 이동
            Debug.Log("SettingsTabController: 로그아웃 버튼 클릭됨");
        }

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

        #region Unity Editor Helper
#if UNITY_EDITOR
        [ContextMenu("테스트: 모든 알림 끄기")]
        private void TestDisableAllNotifications()
        {
            if (offlineNotificationToggle != null)
            {
                offlineNotificationToggle.isOn = false;
            }
            if (missionNotificationToggle != null)
            {
                missionNotificationToggle.isOn = false;
            }
        }
#endif
        #endregion
    }
}
