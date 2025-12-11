import { pool } from '../db/index.js';

export async function logSession({ userId, provider, externalId, deviceId, clientIp, userAgent, result, reason }) {
  const query = `
    INSERT INTO auth_schema.session_history
    (user_id, provider, external_id, device_id, client_ip, user_agent, result, reason, login_at, created_at)
    VALUES ($1, $2, $3, $4, $5, $6, $7, $8, NOW(), NOW());
  `;
  await pool.query(query, [userId, provider, externalId, deviceId, clientIp, userAgent, result, reason]);
}
