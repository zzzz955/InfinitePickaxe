# Auth Server HTTP API (stateless, JSON)

인증 서버에서 사용하는 HTTP API를 JSON으로 명세합니다. 실제 값/코드는 포함하지 않고 필드/타입/의미만 기술합니다.

## 공통 정책
- 모든 엔드포인트: `Content-Type: application/json`
- 오류 시 `success`/`valid` 플래그와 `error` 코드 문자열을 반환
- 해시: SHA256 hex 사용 (메타데이터)
- 압축: 현재 없음 (raw JSON, 필요 시 향후 필드 추가)
- `protocol_version`: 서버/클라 모두 버전을 갖고 호환성 판단(비호환 시 차단)

---

## POST /bootstrap
- 용도: 앱/프로토콜/메타 버전 확인, 필요 시 메타 스냅샷 전달

Request
```json
{
  "app_version": "0.3.1",          // 필수
  "protocol_version": "1",         // 필수 (게임 서버도 같은 버전 정책을 가짐)
  "build_number": "42",            // 필수 (스토어 빌드 코드)
  "device_id": "abcdef...",        // 필수
  "locale": "ko-KR",               // 선택
  "cached_meta_hash": "ab12..."    // 선택, SHA256 hex (캐시 보유 시)
}
```

Response
```json
{
  "action": "PROCEED",           // PROCEED | UPDATE_REQUIRED | MAINTENANCE | BLOCKED
  "message": "점검 중입니다.",
  "store_url": "https://play.google.com/store/apps/details?id=...", // UPDATE_REQUIRED
  "retry_after_seconds": 300,    // MAINTENANCE 시 재시도 권고
  "meta": {
    "hash": "ab12...",           // SHA256 hex (콘텐츠 기반 식별자, version 제거)
    "size_bytes": 12345,
    "download_url": "https://signed-url/meta.json", // 직다운 URL, 없으면 null
    "data": "base64string..."    // 메타 페이로드(raw JSON base64). download_url 또는 data 중 하나만 사용
  }
}
```

Notes
- 메타 해시가 동일하면 `meta`는 null/생략 가능.
- 압축 없음: raw JSON을 base64 인코딩해서 `data`로 넣거나, URL을 내려줌.
- `protocol_version` 불일치 시 `action=UPDATE_REQUIRED` 또는 `BLOCKED`로 처리.

---

## POST /auth/login
- 용도: 소셜 토큰 검증 후 JWT/Refresh 발급, 유저 생성/업데이트

Request
```json
{
  "provider": "google",     // 필수: google | admin(dev)
  "token": "id_token_or_admin_userid",
  "device_id": "abcdef...", // 필수
  "email": "user@example.com" // 선택, 소셜에서 안 주는 경우 보조용
}
```

Response
```json
{
  "success": true,
  "jwt": "...",
  "refresh_token": "...",
  "refresh_expires_at": "2025-01-01T00:00:00Z",
  "user_id": "uuid",
  "external_id": "google-123",
  "provider": "google",
  "nickname": "닉넴",        // 없으면 null
  "email": "user@example.com"
}
```
- 실패 시: `success=false`, `error` 코드(`MISSING_PARAMS`, `BANNED`, `LOGIN_FAILED` 등)

---

## POST /auth/verify
- 용도: JWT 유효성 검증 또는 Refresh 토큰 교체

Request
```json
{
  "jwt": "...",              // 선택: 없으면 refresh로만 검증
  "refresh_token": "...",    // 선택: jwt가 만료된 경우 필요
  "device_id": "abcdef..."   // 선택: 디바이스 바인딩 시 사용
}
```

Response
```json
{
  "valid": true,
  "jwt": "...",              // 새 JWT (refresh 사용 시)
  "refresh_token": "...",    // 새 Refresh (refresh 사용 시)
  "refresh_expires_at": "...",
  "user_id": "uuid",
  "external_id": "google-123",
  "provider": "google",
  "email": "user@example.com",
  "nickname": "닉넴",
  "device_id": "abcdef...",
  "expires_at": 1700000000    // JWT exp (unix seconds)
}
```
- 실패 시: `valid=false`, `error`(`MISSING_TOKEN`, `INVALID_JWT`, `USER_NOT_FOUND`, `USER_BANNED`, `VERIFY_FAILED`, `TOKEN_EXPIRED` 등)

---

## POST /auth/nickname
- 용도: JWT 검증 후 닉네임 설정/변경

Request
```json
{
  "jwt": "...",           // 필수
  "nickname": "닉네임"     // 필수, 검증 규칙: 한글/숫자 2-8자 또는 영문/숫자 4-16자
}
```

Response
```json
{ "success": true, "nickname": "닉네임" }
```
- 실패 시: `success=false`, `error`(`MISSING_PARAMS`, `INVALID_JWT`, `NICKNAME_INVALID`)

---

## GET /health
- 용도: 헬스 체크
- Response: 200 OK, `{ "status": "ok" }`
