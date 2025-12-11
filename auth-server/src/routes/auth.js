import { Router } from 'express';
import { verifyGoogleToken } from '../services/googleAuth.js';
import { issueJwt, verifyJwt } from '../services/jwtService.js';
import { upsertUser, findUserByExternalId, updateNickname } from '../store/dbUsers.js';
import { rotateRefreshToken, verifyRefreshToken } from '../store/refreshTokens.js';
import { logSession } from '../store/sessionHistory.js';

const router = Router();

// Helpers
function buildExternalId(provider, providerUserId) {
  return `${provider}-${providerUserId}`;
}

function clientInfo(req) {
  return {
    clientIp: req.ip,
    userAgent: req.get('user-agent') || null
  };
}

// POST /auth/login
router.post('/login', async (req, res) => {
  const { provider, token, device_id: deviceId, email: clientEmail } = req.body || {};
  if (!provider || !token || !deviceId) {
    const msg = '필수 파라미터(provider, token, device_id)가 없습니다.';
    console.error('[auth/login] ', msg, req.body);
    return res.status(400).json({ success: false, error: 'MISSING_PARAMS', message: msg });
  }

  try {
    const { providerUserId, email: verifiedEmail } = await verifyProviderToken({ provider, token });
    const externalId = buildExternalId(provider, providerUserId);
    const email = verifiedEmail || clientEmail || null;

    const user = await upsertUser({
      provider,
      externalId,
      email,
      nickname: null
    });
    if (user.is_banned) {
      await logSession({ userId: user.user_id, provider, externalId, deviceId, ...clientInfo(req), result: 'BANNED', reason: user.ban_reason });
      return res.status(403).json({ success: false, error: 'BANNED' });
    }

    const jwt = issueJwt({ user_id: user.user_id, external_id: user.external_id, provider, device_id: deviceId });
    const refresh = await rotateRefreshToken({ userId: user.user_id, deviceId });

    await logSession({ userId: user.user_id, provider, externalId, deviceId, ...clientInfo(req), result: 'SUCCESS', reason: null });

    return res.json({
      success: true,
      jwt,
      refresh_token: refresh.token,
      refresh_expires_at: refresh.expires_at,
      user_id: user.user_id,
      nickname: user.nickname,
      email: user.email,
      provider,
      external_id: user.external_id
    });
  } catch (err) {
    console.error('[auth/login] 로그인 실패:', err);
    return res.status(500).json({ success: false, error: 'LOGIN_FAILED', message: '로그인 처리 중 오류가 발생했습니다.' });
  }
});

// POST /auth/verify
router.post('/verify', async (req, res) => {
  try {
    const { jwt, refresh_token: refreshToken, device_id: deviceId } = req.body || {};
    if (!jwt && !refreshToken) {
      const msg = 'JWT 또는 refresh_token이 없습니다.';
      console.error('[auth/verify] ', msg, req.body);
      return res.status(400).json({ valid: false, error: 'MISSING_TOKEN', message: msg });
    }

    if (jwt) {
      try {
        const payload = verifyJwt(jwt);
        const externalId = payload.external_id || (payload.provider && payload.google_id ? buildExternalId(payload.provider, payload.google_id) : null);
        if (!externalId) {
          return res.json({ valid: false, error: 'INVALID_JWT' });
        }
        const user = await findUserByExternalId(externalId);
        if (!user) return res.json({ valid: false, error: 'USER_NOT_FOUND' });
        if (user.is_banned) return res.json({ valid: false, error: 'USER_BANNED' });
        return res.json({
          valid: true,
          user_id: user.user_id,
          external_id: user.external_id,
          provider: user.provider,
          device_id: deviceId,
          email: user.email,
          nickname: user.nickname,
          is_banned: user.is_banned,
          ban_reason: user.ban_reason,
          expires_at: payload.exp
        });
      } catch (err) {
        // fall through to refresh if provided
      }
    }

    if (!refreshToken) {
      return res.json({ valid: false, error: 'TOKEN_EXPIRED', message: '토큰이 만료되었습니다.' });
    }

    const refreshResult = await verifyRefreshToken({ token: refreshToken, deviceId });
    if (!refreshResult.valid) {
      console.error('[auth/verify] 리프레시 검증 실패:', refreshResult.error);
      return res.json({ valid: false, error: refreshResult.error, message: '리프레시 토큰 검증 실패' });
    }

    const user = await findUserByExternalId(refreshResult.external_id);
    if (!user) return res.json({ valid: false, error: 'USER_NOT_FOUND' });
    if (user.is_banned) return res.json({ valid: false, error: 'USER_BANNED' });

    const newJwt = issueJwt({ user_id: user.user_id, external_id: user.external_id, provider: user.provider, device_id: deviceId || null });
    const rotated = await rotateRefreshToken({ userId: user.user_id, deviceId: deviceId || null, familyId: refreshResult.family_id });

    return res.json({
      valid: true,
      jwt: newJwt,
      refresh_token: rotated.token,
      refresh_expires_at: rotated.expires_at,
      user_id: user.user_id,
      external_id: user.external_id,
      provider: user.provider,
      email: user.email,
      nickname: user.nickname,
      device_id: deviceId || null,
      expires_at: refreshResult.expires_at
    });
  } catch (err) {
    console.error('[auth/verify] 알 수 없는 오류:', err);
    return res.json({ valid: false, error: 'VERIFY_FAILED', message: '검증 처리 중 오류' });
  }
});

// POST /auth/nickname
router.post('/nickname', async (req, res) => {
  try {
    const { jwt, nickname } = req.body || {};
    if (!jwt || !nickname) {
      const msg = 'JWT 또는 nickname이 없습니다.';
      console.error('[auth/nickname] ', msg, req.body);
      return res.status(400).json({ success: false, error: 'MISSING_PARAMS', message: msg });
    }

    const payload = verifyJwt(jwt);
    const userId = payload.user_id;
    const normalized = (nickname || '').trim();
    if (!isValidNickname(normalized)) {
      return res.json({ success: false, error: 'NICKNAME_INVALID', message: '닉네임 형식이 올바르지 않습니다.' });
    }

    const updated = await updateNickname({ userId, nickname: normalized });
    return res.json({ success: true, nickname: updated.nickname });
  } catch (err) {
    console.error('[auth/nickname] JWT 검증 실패:', err);
    return res.status(400).json({ success: false, error: 'INVALID_JWT', message: 'JWT 검증 실패' });
  }
});

export default router;

function isValidNickname(nick) {
  if (!nick) return false;
  const hangul = /^[\p{Script=Hangul}0-9]{2,8}$/u; // 한글/숫자
  const ascii = /^[A-Za-z0-9]{4,16}$/;            // 영문/숫자
  return hangul.test(nick) || ascii.test(nick);
}

async function verifyProviderToken({ provider, token }) {
  if (provider === 'google') {
    return verifyGoogleToken(token);
  }
  if (provider === 'admin') {
    // Dev/admin shortcut: trust provided token as user id (no external validation)
    const providerUserId = token.trim();
    if (!providerUserId) {
      throw new Error('INVALID_TOKEN');
    }
    return { providerUserId, email: null };
  }
  throw new Error('UNSUPPORTED_PROVIDER');
}
