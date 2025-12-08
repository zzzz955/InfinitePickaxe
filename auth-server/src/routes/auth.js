import { Router } from 'express';
import { verifyGoogleToken } from '../services/googleAuth.js';
import { issueJwt, verifyJwt } from '../services/jwtService.js';
import { upsertUser, findUserByGoogleId } from '../store/memoryUsers.js';

const router = Router();

// POST /auth/login
router.post('/login', async (req, res) => {
  try {
    const { google_token: googleToken, device_id: deviceId } = req.body || {};
    if (!googleToken || !deviceId) {
      return res.status(400).json({ success: false, error: 'MISSING_PARAMS' });
    }

    const googleId = await verifyGoogleToken(googleToken);
    const user = upsertUser({ googleId, deviceId });
    if (user.isBanned) {
      return res.status(403).json({ success: false, error: 'BANNED' });
    }

    const jwt = issueJwt({ user_id: user.userId, google_id: googleId, device_id: deviceId });
    return res.json({
      success: true,
      jwt,
      user_id: user.userId,
      is_new_user: false,
      server_time: Date.now()
    });
  } catch (err) {
    console.error('login_error', err);
    return res.status(500).json({ success: false, error: 'LOGIN_FAILED' });
  }
});

// POST /auth/verify
router.post('/verify', (req, res) => {
  try {
    const { jwt } = req.body || {};
    if (!jwt) {
      return res.status(400).json({ valid: false, error: 'MISSING_JWT' });
    }
    const payload = verifyJwt(jwt);
    const user = findUserByGoogleId(payload.google_id);
    if (!user) return res.json({ valid: false, error: 'USER_NOT_FOUND' });
    if (user.isBanned) return res.json({ valid: false, error: 'USER_BANNED' });
    return res.json({ valid: true, user_id: user.userId, google_id: user.googleId, expires_at: payload.exp });
  } catch (err) {
    return res.json({ valid: false, error: err.name === 'TokenExpiredError' ? 'TOKEN_EXPIRED' : 'INVALID_JWT' });
  }
});

export default router;
