# Protocol (protobuf, MVP)

공통 `.proto` 스키마를 관리합니다. C++/C# 양쪽에서 같은 파일을 코드 생성해 사용합니다.

- 전송: TCP length-prefix(4바이트) + `Envelope` protobuf 메시지
- 메시지: MVP 기준 (Handshake, Heartbeat, Mining*, Upgrade, Mission, SlotUnlock, OfflineReward, Error)

## 인증 서버 부트스트랩
- stateless HTTP/JSON API는 `auth_api.md`에 명세
