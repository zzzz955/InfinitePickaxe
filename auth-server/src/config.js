import dotenv from 'dotenv';

dotenv.config();

export const JWT_SECRET = process.env.JWT_SECRET || 'dev-secret-change-me';
export const JWT_EXPIRES_IN = process.env.JWT_EXPIRES_IN || '7d';
export const PORT = process.env.PORT || 10000;

export const GOOGLE_MOCK_MODE = process.env.GOOGLE_MOCK_MODE !== 'false';
