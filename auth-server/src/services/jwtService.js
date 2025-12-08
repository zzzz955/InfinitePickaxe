import jwt from 'jsonwebtoken';
import { JWT_SECRET, JWT_EXPIRES_IN } from '../config.js';

export function issueJwt(payload) {
  if (!JWT_SECRET || JWT_SECRET.length < 8) {
    throw new Error('JWT secret is missing or too short');
  }
  return jwt.sign(payload, JWT_SECRET, { expiresIn: JWT_EXPIRES_IN });
}

export function verifyJwt(token) {
  return jwt.verify(token, JWT_SECRET);
}
