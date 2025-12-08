// Mockable Google token verifier. In MVP dev, always accept and extract a stub google_id.
import crypto from 'crypto';
import { GOOGLE_MOCK_MODE } from '../config.js';

export async function verifyGoogleToken(googleToken) {
  if (GOOGLE_MOCK_MODE) {
    // Deterministic fake google_id from token
    const hash = crypto.createHash('sha256').update(googleToken || '').digest('hex');
    return `mock-${hash.slice(0, 16)}`;
  }
  // TODO: Integrate real Google Play Games token validation.
  throw new Error('Real Google validation not implemented in MVP');
}
