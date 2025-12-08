# Infinite Pickaxe Auth Server (MVP)

Minimal auth server for MVP:
- POST `/auth/login`: verify Google token (mock by default), upsert user, issue JWT.
- POST `/auth/verify`: verify JWT and ban/user existence.
- GET `/health`: health check.

## Quick start
```bash
cd auth-server
npm install
JWT_SECRET=change-me GOOGLE_MOCK_MODE=true npm run dev
# Windows PowerShell: $env:JWT_SECRET='change-me'; $env:GOOGLE_MOCK_MODE='true'; npm run dev
```

## Docker (with root docker-compose)
루트에서 `.env`를 만든 뒤:
```bash
cp .env.example .env
docker compose up -d auth-server
```
Postgres/Redis도 함께 올라갑니다. 기본 포트:
- auth-server: 10000
- postgres: 호스트 10002 → 컨테이너 5432 (user/pass/db: pickaxe/pickaxe/pickaxe_auth)
- redis: 호스트 10003 → 컨테이너 6379

## Environment
- `JWT_SECRET` (required in prod): JWT signing key.
- `JWT_EXPIRES_IN` (optional): default `7d`.
- `PORT`: default `10000`.
- `GOOGLE_MOCK_MODE`: default `true` (accept any token); set `false` when real Google validation is added.
- `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`, `DB_NAME`: Postgres 연결 정보 (compose 기본값: postgres / 5432 / pickaxe / pickaxe / pickaxe_auth).

## Notes
- User store is in-memory (`src/store/memoryUsers.js`) for MVP/dev; replace with DB per TDD.
- Same-account single-session/IP enforcement and ban flows are to be added when server flags are finalized.
