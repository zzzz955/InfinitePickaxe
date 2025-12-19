# Metadata CSV Files

이 디렉토리는 게임 메타데이터의 CSV 버전을 포함합니다.

## 파일 구조

### 광물 데이터
- `minerals.csv`: 광물 정보 (이름, HP, 골드 보상, 추천 레벨, 바이옴)

### 곡괭이 레벨
- `pickaxe_levels.csv`: 곡괭이 레벨별 스탯 (외형, 공격력, DPS, 강화 비용)

### 일일 미션
- `daily_missions_config.csv`: 일일 미션 시스템 설정
- `daily_missions.csv`: 미션 풀 (Easy/Medium/Hard 난이도별)
- `daily_missions_milestones.csv`: 미션 완료 마일스톤 보너스

### 광고 시스템
- `ads_config.csv`: 광고 시스템 설정
- `ads.csv`: 광고 타입 정의 (강화 할인, 미션 리롤, 크리스탈 보상)

### 기타 설정
- `mission_reroll.csv`: 미션 리롤 규칙
- `offline_defaults.csv`: 오프라인 보상 기본값
- `upgrade_rules_config.csv`: 강화 규칙 설정
- `upgrade_rules_tier_rates.csv`: 티어별 기본 강화 확률

## 사용 방법

### CSV → JSON 변환
```bash
cd metadata
node csv_to_json.js
```

### JSON → CSV 변환
```bash
cd metadata
node json_to_csv.js
```

## 인코딩

모든 CSV 파일은 **UTF-8 (BOM 없음)** 인코딩으로 저장됩니다.
- Excel에서 열 때: "데이터 가져오기" 사용, UTF-8 선택
- Google Sheets: 자동으로 UTF-8 인식

## 주의사항

1. CSV 파일 편집 시 인코딩을 UTF-8로 유지해야 합니다.
2. 쉼표가 포함된 값은 자동으로 따옴표로 감싸집니다.
3. 복잡한 중첩 구조(JSON)는 JSON 문자열로 저장됩니다.

## 파일 변경 워크플로우

1. CSV 파일 편집 (Google Sheets, Excel 등)
2. `csv_to_json.js` 실행하여 JSON 생성
3. JSON 파일 커밋

또는

1. JSON 파일 편집
2. `json_to_csv.js` 실행하여 CSV 생성
3. CSV 파일로 데이터 검증
