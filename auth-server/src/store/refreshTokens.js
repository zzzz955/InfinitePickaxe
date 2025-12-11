import crypto from 'crypto';
import { pool } from '../db/index.js';

const REFRESH_TTL_DAYS = 30;           // token-level sliding window
const FAMILY_MAX_DAYS = 90;            // hard cap
const MAX_REFRESH_COUNT = 200;

export async function rotateRefreshToken({ userId, deviceId, familyId = null }) {
  const client = await pool.connect();
  try {
    await client.query('BEGIN');

    let useFamilyId = familyId;
    if (!useFamilyId) {
      const famRes = await client.query(
        `INSERT INTO auth_schema.jwt_families (user_id, device_id, expires_at, max_refresh_count)
         VALUES ($1, $2, NOW() + INTERVAL '${FAMILY_MAX_DAYS} days', $3)
         RETURNING family_id, expires_at, refresh_count, max_refresh_count;`,
        [userId, deviceId, MAX_REFRESH_COUNT]
      );
      useFamilyId = famRes.rows[0].family_id;
    }

    const token = crypto.randomBytes(32).toString('hex');
    const tokenHash = crypto.createHash('sha256').update(token).digest('hex');
    const expiresAt = new Date(Date.now() + REFRESH_TTL_DAYS * 24 * 60 * 60 * 1000);

    await client.query(
      `INSERT INTO auth_schema.jwt_tokens (family_id, user_id, token_hash, jti, expires_at)
       VALUES ($1, $2, $3, gen_random_uuid()::text, $4);`,
      [useFamilyId, userId, tokenHash, expiresAt]
    );

    await client.query(
      `UPDATE auth_schema.jwt_families
       SET refresh_count = refresh_count + 1,
           last_refreshed_at = NOW(),
           device_id = $2,
           expires_at = LEAST(expires_at, NOW() + INTERVAL '${FAMILY_MAX_DAYS} days')
       WHERE family_id = $1;`,
      [useFamilyId, deviceId]
    );

    await client.query('COMMIT');
    return { token, expires_at: expiresAt, family_id: useFamilyId };
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    client.release();
  }
}

export async function verifyRefreshToken({ token, deviceId }) {
  const tokenHash = crypto.createHash('sha256').update(token).digest('hex');
  const now = new Date();
  const client = await pool.connect();
  try {
    const { rows } = await client.query(
      `SELECT t.token_id, t.family_id, t.user_id, t.expires_at, t.is_valid, f.device_id, u.google_id
       FROM auth_schema.jwt_tokens t
       JOIN auth_schema.jwt_families f ON f.family_id = t.family_id
       JOIN auth_schema.users u ON u.user_id = t.user_id
       WHERE t.token_hash = $1 AND t.is_valid = TRUE;`,
      [tokenHash]
    );
    if (!rows.length) {
      return { valid: false, error: 'INVALID_REFRESH' };
    }

    const row = rows[0];
    if (new Date(row.expires_at) < now) {
      return { valid: false, error: 'REFRESH_EXPIRED' };
    }
    if (row.device_id && deviceId && row.device_id !== deviceId) {
      return { valid: false, error: 'DEVICE_MISMATCH' };
    }

    // Invalidate the used token (single-use)
    await client.query(
      `UPDATE auth_schema.jwt_tokens SET is_valid = FALSE, is_used = TRUE, used_at = NOW()
       WHERE token_hash = $1;`,
      [tokenHash]
    );

    return {
      valid: true,
      family_id: row.family_id,
      user_id: row.user_id,
      google_id: row.google_id,
      expires_at: row.expires_at
    };
  } finally {
    client.release();
  }
}
