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

## Environment
- `JWT_SECRET` (required in prod): JWT signing key.
- `JWT_EXPIRES_IN` (optional): default `7d`.
- `PORT`: default `10000`.
- `GOOGLE_MOCK_MODE`: default `true` (accept any token); set `false` when real Google validation is added.

## Notes
- User store is in-memory (`src/store/memoryUsers.js`) for MVP/dev; replace with DB per TDD.
- Same-account single-session/IP enforcement and ban flows are to be added when server flags are finalized.
