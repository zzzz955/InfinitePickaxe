# Database DDL (MVP)

- DBMS: PostgreSQL (UTF-8, UTC)
- 스키마 분리: `auth_schema`, `game_schema`
- FK는 스키마 내부에만 사용, 스키마 간 FK는 두지 않음
- UUID PK: `gen_random_uuid()` (pgcrypto 필요)
- 일일 리셋: Lazy Evaluation (접속 시 날짜 비교)

구조
```
database/
  auth_schema/
    schema.sql   # 인증 스키마 DDL
    schema.md    # 테이블/컬럼/제약 요약
  game_schema/
    schema.sql   # 게임 스키마 DDL
    schema.md    # 테이블/컬럼/제약 요약
```

적용 순서:
1) `CREATE EXTENSION IF NOT EXISTS pgcrypto;`
2) `\i database/auth_schema/schema.sql`
3) `\i database/game_schema/schema.sql`
