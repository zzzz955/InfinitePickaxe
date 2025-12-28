import crypto from 'crypto';
import { pool } from '../db/index.js';

const REFRESH_TTL_DAYS = 30;           // token-level sliding window
const FAMILY_MAX_DAYS = 90;            // hard cap from initial family creation

export async function rotateRefreshToken({ userId, deviceId, familyId = null }) {
  const client = await pool.connect();
  try {
    await client.query('BEGIN');

    const now = Date.now();
    const ttlMillis = REFRESH_TTL_DAYS * 24 * 60 * 60 * 1000;
    const familyCapMillis = FAMILY_MAX_DAYS * 24 * 60 * 60 * 1000;

    // 1) 우선 기존 family 재사용 (우선순위: device 일치 > 최근 갱신)
    let familyRow = null;
    if (familyId) {
      const { rows } = await client.query(
        `SELECT family_id, expires_at
         FROM auth_schema.jwt_families
         WHERE family_id = $1 AND user_id = $2
           AND is_active = TRUE
           AND is_revoked = FALSE
           AND expires_at > NOW()`,
        [familyId, userId]
      );
      familyRow = rows[0] || null;
    }

    if (!familyRow) {
      const { rows } = await client.query(
        `SELECT family_id, expires_at
         FROM auth_schema.jwt_families
         WHERE user_id = $1
           AND is_active = TRUE
           AND is_revoked = FALSE
           AND expires_at > NOW()
         ORDER BY (CASE WHEN device_id IS NOT NULL AND device_id = $2 THEN 1 ELSE 0 END) DESC,
                  COALESCE(last_refreshed_at, created_at, NOW()) DESC
         LIMIT 1`,
        [userId, deviceId || null]
      );
      familyRow = rows[0] || null;
    }

    // 2) 없으면 새 family 생성
    if (!familyRow) {
      if (familyId) {
        const error = new Error('FAMILY_EXPIRED');
        error.code = 'FAMILY_EXPIRED';
        throw error;
      }

      await client.query(
        `UPDATE auth_schema.jwt_families
         SET is_active = FALSE,
             is_revoked = TRUE,
             revoked_reason = 'EXPIRED',
             revoked_at = NOW()
         WHERE user_id = $1
           AND is_active = TRUE
           AND expires_at <= NOW();`,
        [userId]
      );

      const famRes = await client.query(
        `INSERT INTO auth_schema.jwt_families (user_id, device_id, expires_at)
         VALUES ($1, $2, NOW() + INTERVAL '${FAMILY_MAX_DAYS} days')
         RETURNING family_id, expires_at;`,
        [userId, deviceId || null]
      );
      familyRow = famRes.rows[0];
    }

    const useFamilyId = familyRow.family_id;
    const familyExpires = new Date(familyRow.expires_at).getTime();
    const tokenExpires = new Date(Math.min(now + ttlMillis, familyExpires));

    // 3) 새 토큰 발급 전에 기존 유효/미사용 토큰 무효화 (user 단위, 필요 시 device 단위로 좁힐 수 있음)
    await client.query(
      `UPDATE auth_schema.jwt_tokens
       SET is_valid = FALSE
       WHERE user_id = $1
         AND is_valid = TRUE
         AND is_used = FALSE;`,
      [userId]
    );

    const token = crypto.randomBytes(32).toString('hex');
    const tokenHash = crypto.createHash('sha256').update(token).digest('hex');

    await client.query(
      `INSERT INTO auth_schema.jwt_tokens (family_id, user_id, token_hash, jti, expires_at)
       VALUES ($1, $2, $3, gen_random_uuid()::text, $4);`,
      [useFamilyId, userId, tokenHash, tokenExpires]
    );

    await client.query(
      `UPDATE auth_schema.jwt_families
       SET last_refreshed_at = NOW(),
           device_id = $2,
           expires_at = LEAST(expires_at, NOW() + INTERVAL '${FAMILY_MAX_DAYS} days')
       WHERE family_id = $1
         AND is_active = TRUE
         AND is_revoked = FALSE;`,
      [useFamilyId, deviceId]
    );

    await client.query('COMMIT');
    return { token, expires_at: tokenExpires, family_id: useFamilyId };
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
      `SELECT t.token_id, t.family_id, t.user_id, t.expires_at, t.is_valid,
              f.device_id, f.is_active, f.is_revoked, f.expires_at AS family_expires_at,
              u.external_id, u.provider
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
    if (!row.is_active || row.is_revoked) {
      return { valid: false, error: 'FAMILY_REVOKED' };
    }
    if (new Date(row.family_expires_at) < now) {
      return { valid: false, error: 'FAMILY_EXPIRED' };
    }
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
      external_id: row.external_id,
      provider: row.provider,
      expires_at: row.expires_at
    };
  } finally {
    client.release();
  }
}
