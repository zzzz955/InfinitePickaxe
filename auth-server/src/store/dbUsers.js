import { pool } from '../db/index.js';

export async function upsertUser({ provider, externalId, email = null, nickname = null }) {
  const query = `
    INSERT INTO auth_schema.users (provider, external_id, email, nickname, last_login)
    VALUES ($1, $2, $3, $4, NOW())
    ON CONFLICT (external_id)
    DO UPDATE SET
      email = COALESCE(EXCLUDED.email, auth_schema.users.email),
      nickname = COALESCE(EXCLUDED.nickname, auth_schema.users.nickname),
      last_login = NOW(),
      updated_at = NOW()
    RETURNING user_id, provider, external_id, email, nickname, is_banned, ban_reason, last_login;
  `;
  const { rows } = await pool.query(query, [provider, externalId, email, nickname]);
  return rows[0];
}

export async function findUserByExternalId(externalId) {
  const query = `
    SELECT user_id, provider, external_id, email, nickname, is_banned, ban_reason
    FROM auth_schema.users
    WHERE external_id = $1
    LIMIT 1;
  `;
  const { rows } = await pool.query(query, [externalId]);
  return rows[0] || null;
}

export async function updateNickname({ userId, nickname }) {
  const query = `
    UPDATE auth_schema.users
    SET nickname = $2,
        updated_at = NOW()
    WHERE user_id = $1
    RETURNING user_id, nickname;
  `;
  const { rows } = await pool.query(query, [userId, nickname]);
  return rows[0];
}
