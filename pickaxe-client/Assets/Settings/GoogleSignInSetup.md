# Google Sign-In (play-services-auth) Unity 설정 가이드

목표: 웹 리디렉션 없이 네이티브/One Tap 계정 선택을 사용하고, 획득한 ID 토큰을 서버에 전달해 JWT를 받는 흐름을 구축한다.  
GPGS는 사용하지 않고 `play-services-auth`(Google Sign-In)만 사용한다.

## 1) 의존성
- EDM4U(External Dependency Manager) : `manifest.json`에 추가됨(`com.google.external-dependency-manager`).
- Google Sign-In for Unity 플러그인: 공식 GitHub 릴리스(.unitypackage) 수동 임포트 필요.
  - 다운로드: https://github.com/googlesamples/google-signin-unity/releases
  - 에디터에서 Assets > Import Package > Custom Package… 로 임포트.

## 2) Android 세팅
- Google Cloud Console에서 OAuth 클라이언트(Web) 생성 → Client ID 확보 (One Tap도 Web Client ID 사용).
- Android 패키지명/서명(SHA-1/256) 등록.
- `Assets/Plugins/Android` 하위에 `google-services` 필요 없음 (Firebase 미사용).
- EDM4U 메뉴에서 Android Resolver 실행해 `com.google.android.gms:play-services-auth`를 자동 주입.

## 3) 코드 구성(예시 흐름)
- `GoogleSignInConfiguration`에 Web Client ID 설정 + `requestIdToken = true`, 필요 시 `requestServerAuthCode = true`.
- One Tap 사용 시 팝업 허용(`HidePopups = false`), `ForceTokenRefresh = true`.
- 로그인 성공 → `IdToken`(또는 `ServerAuthCode`)를 서버로 전송 → 서버에서 검증 후 JWT 발급 → SecureStorage에 JWT/RefreshToken 저장.
- 로그아웃/계정 전환: GSI `SignOut()` 호출 후 다시 `SignIn()`로 계정 선택 유도.

## 4) 유의사항
- 빌드 파이프라인(AGP/Gradle) 변동 시 EDM4U로 종속성 다시 해석.
- 에뮬레이터/실기기에서 One Tap UI 호출 여부 확인.
- iOS 지원 시 GoogleSignIn iOS 네이티브 의존성도 추가로 필요(별도 가이드 작성 예정).
