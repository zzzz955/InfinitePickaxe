import { pool } from '../db/index.js';

export async function upsertUser({ googleId, deviceId }) {
  const query = `
    INSERT INTO auth_schema.users (google_id, device_id, last_login)
    VALUES ($1, $2, NOW())
    ON CONFLICT (google_id)
    DO UPDATE SET
      device_id = EXCLUDED.device_id,
      last_login = NOW(),
      updated_at = NOW()
    RETURNING user_id, google_id, device_id, is_banned, ban_reason, last_login;
  `;
  const { rows } = await pool.query(query, [googleId, deviceId]);
  return rows[0];
}

export async function findUserByGoogleId(googleId) {
  const query = `
    SELECT user_id, google_id, device_id, is_banned, ban_reason
    FROM auth_schema.users
    WHERE google_id = $1
    LIMIT 1;
  `;
  const { rows } = await pool.query(query, [googleId]);
  return rows[0] || null;
}
