# 메타데이터 가이드 (스키마/정책 업데이트 반영)

## 공통 리셋 정책
- 리셋 기준: 매일 KST 00:00, 각 테이블의 날짜 컬럼(예: `mission_date`, `reset_date`, `offline_date`, `milestone_date`)으로 지연 초기화(lazy reset) 처리.
- 온라인 상태 자정 통과 → 즉시 리셋 후 날짜 업데이트, 오프라인 상태 → 다음 접속 시 리셋.
- 일일 초기화 대상: 미션 진행도/배정, 광고 시청 카운터, 오프라인 소모 시간, 미션 리롤 카운터.
- `current_offline_hours`만 사용(초 단위 저장), `max_offline_hours`는 제거. 보너스/기본값을 메타데이터로 제공해 서버에서 초 단위로 변환해 적용.

## 파일별 정의/권장 분리

### `daily_missions.json` (존재)
- 키: `reset_time_kst`, `total_slots`, `max_daily_assign`(일 최대 배정 수), `pools`(난이도별 미션 풀), `milestone_offline_bonus_hours`(완료 회차별 오프라인 시간 보너스, 시간 단위).
- 마일스톤 규칙: 일일 미션 3/5/7회 완료 시 보상 가능, 보상 청구 시 `current_offline_hours += bonus_hours * 3600`.
- 정합성: 미션 보상 크리스탈 값은 난이도/목표와 일관되게 유지, `max_daily_assign`는 슬롯 수와 조합해 하루에 배정 가능한 총량을 제한.

### `ads.json` (신규 권장)
- 목적: 광고 타입별 일일 한도/회차별 보상 정의.
- 예시 스키마:
  ```json
  {
    "reset_time_kst": "00:00",
    "ad_types": [
      {
        "id": "mission_reroll",
        "daily_limit": 3,
        "rewards_by_view": [10, 14, 18],
        "applies_to": ["mission_reroll"]
      }
    ]
  }
  ```
- 규칙: `rewards_by_view` 길이는 `daily_limit`와 동일, 1~3회차 보상(10/14/18 크리스탈) 고정. 타입별로 한도/보상 상이하면 항목을 추가한다.

### `offline_defaults.json` (신규 권장)
- 목적: 오프라인 모드 초기/기본 시간 정의(상한 없이 누적되는 `current_offline_hours` 초기값).
- 예시 스키마:
  ```json
  { "initial_offline_hours": 4 }
  ```
- 서버 적용 시 초 단위로 변환해 `user_offline_state.current_offline_hours` 초기화.

### `mission_reroll.json` (선택)
- 목적: 무료/광고 리롤 횟수 및 적용 규칙 명시.
- 권장 키: `free_rerolls_per_day`(2), `ad_rerolls_per_day`(3), `reset_time_kst`, `apply_to_slots`(true: 슬롯 전체 리롤), `progress_reset_on_reroll`(true).

### `mission_rewards.json` (선택, 미션 풀과 분리 필요 시)
- 목적: 미션 보상 정의를 미션 정의와 분리하고자 할 때 사용.
- 권장 키: `reward_id`, `crystal`, `items` 등.

## 스키마와의 매핑 포인트
- `user_mission_daily.completed_count`는 하루 완료 횟수만 누적, 메타데이터의 `max_daily_assign`와 일치하는지 검증.
- `user_ad_counters.reset_date`는 `ads.json.reset_time_kst` 기반으로 리셋.
- `user_mission_slots` 진행도/상태는 미션 메타데이터의 목표(`target`)와 타입(`type`)을 사용해 계산, 리롤 시 진행도 초기화.
- `user_milestones`는 `milestone_offline_bonus_hours`의 `completed` 값(3/5/7)과 매핑해 보상 지급/중복 방지.
- `user_offline_state.current_offline_hours`는 `offline_defaults` 초기값과 마일스톤 보너스를 초 단위로 반영.

## 운용 체크리스트
- 리셋 시각, 일일 한도, 보상 수치가 서버/클라/메타데이터 간 일치하는지 검증.
- `bonus_hours`, `initial_offline_hours` 등 시간 단위 필드는 초 단위로 변환해 사용.
- 새 메타데이터 파일을 추가할 경우 로더/배포 파이프라인에 등록하고, 스냅샷 검증을 위한 샘플 값을 제공한다.
