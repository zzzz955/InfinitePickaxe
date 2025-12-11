import { Router } from 'express';
import { verifyGoogleToken } from '../services/googleAuth.js';
import { issueJwt, verifyJwt } from '../services/jwtService.js';
import { upsertUser, findUserByGoogleId } from '../store/dbUsers.js';
import { rotateRefreshToken, verifyRefreshToken } from '../store/refreshTokens.js';

const router = Router();

// POST /auth/login
router.post('/login', async (req, res) => {
  try {
    const { google_token: googleToken, device_id: deviceId } = req.body || {};
    if (!googleToken || !deviceId) {
      return res.status(400).json({ success: false, error: 'MISSING_PARAMS' });
    }

    const googleId = await verifyGoogleToken(googleToken);
    const user = await upsertUser({ googleId, deviceId });
    if (user.is_banned) {
      return res.status(403).json({ success: false, error: 'BANNED' });
    }

    const jwt = issueJwt({ user_id: user.user_id, google_id: googleId, device_id: deviceId });
    const refresh = await rotateRefreshToken({ userId: user.user_id, deviceId });

    return res.json({
      success: true,
      jwt,
      refresh_token: refresh.token,
      refresh_expires_at: refresh.expires_at,
      user_id: user.user_id,
      is_new_user: false,
      server_time: Date.now()
    });
  } catch (err) {
    console.error('login_error', err);
    return res.status(500).json({ success: false, error: 'LOGIN_FAILED' });
  }
});

// POST /auth/verify
router.post('/verify', async (req, res) => {
  try {
    const { jwt, refresh_token: refreshToken, device_id: deviceId } = req.body || {};
    if (!jwt && !refreshToken) {
      return res.status(400).json({ valid: false, error: 'MISSING_TOKEN' });
    }

    if (jwt) {
      try {
        const payload = verifyJwt(jwt);
        const user = await findUserByGoogleId(payload.google_id);
        if (!user) return res.json({ valid: false, error: 'USER_NOT_FOUND' });
        if (user.is_banned) return res.json({ valid: false, error: 'USER_BANNED' });
        return res.json({
          valid: true,
          user_id: user.user_id,
          google_id: user.google_id,
          device_id: user.device_id,
          is_banned: user.is_banned,
          ban_reason: user.ban_reason,
          expires_at: payload.exp
        });
      } catch (err) {
        // fall through to refresh if provided
      }
    }

    if (!refreshToken) {
      return res.json({ valid: false, error: 'TOKEN_EXPIRED' });
    }

    const refreshResult = await verifyRefreshToken({ token: refreshToken, deviceId });
    if (!refreshResult.valid) {
      return res.json({ valid: false, error: refreshResult.error });
    }

    const user = await findUserByGoogleId(refreshResult.google_id);
    if (!user) return res.json({ valid: false, error: 'USER_NOT_FOUND' });
    if (user.is_banned) return res.json({ valid: false, error: 'USER_BANNED' });

    const newJwt = issueJwt({ user_id: user.user_id, google_id: user.google_id, device_id: deviceId || user.device_id });
    const rotated = await rotateRefreshToken({ userId: user.user_id, deviceId: deviceId || user.device_id, familyId: refreshResult.family_id });

    return res.json({
      valid: true,
      jwt: newJwt,
      refresh_token: rotated.token,
      refresh_expires_at: rotated.expires_at,
      user_id: user.user_id,
      google_id: user.google_id,
      device_id: user.device_id,
      expires_at: refreshResult.expires_at
    });
  } catch (err) {
    return res.json({ valid: false, error: 'VERIFY_FAILED' });
  }
});

export default router;
