// Minimal in-memory store for MVP dev. Replace with DB in production.
const users = new Map();

export function upsertUser({ googleId, deviceId }) {
  const now = new Date().toISOString();
  const existing = users.get(googleId);
  if (existing) {
    existing.deviceId = deviceId;
    existing.lastLogin = now;
    users.set(googleId, existing);
    return existing;
  }
  const user = {
    userId: `user-${users.size + 1}`,
    googleId,
    deviceId,
    isBanned: false,
    lastLogin: now
  };
  users.set(googleId, user);
  return user;
}

export function findUserByGoogleId(googleId) {
  return users.get(googleId) || null;
}
