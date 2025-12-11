import { OAuth2Client } from 'google-auth-library';
import { FIREBASE_PROJECT_ID } from '../config.js';

const client = new OAuth2Client();

export async function verifyGoogleToken(idToken) {
  if (!FIREBASE_PROJECT_ID) {
    throw new Error('FIREBASE_PROJECT_ID is not configured');
  }

  const ticket = await client.verifyIdToken({
    idToken,
    audience: FIREBASE_PROJECT_ID
  });

  const payload = ticket.getPayload();
  if (!payload) {
    throw new Error('Invalid token payload');
  }

  const providerUserId = payload.sub;
  const email = payload.email || null;
  const emailVerified = payload.email_verified || false;

  return {
    provider: 'google',
    providerUserId,
    email: emailVerified ? email : null
  };
}
