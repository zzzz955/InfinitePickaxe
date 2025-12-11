import dotenv from 'dotenv';

dotenv.config();

export const JWT_SECRET = process.env.JWT_SECRET || 'dev-secret-change-me';
export const JWT_EXPIRES_IN = process.env.JWT_EXPIRES_IN || '7d';
export const REFRESH_EXPIRES_DAYS = parseInt(process.env.REFRESH_EXPIRES_DAYS || '14', 10);
export const PORT = process.env.PORT || 10000;
export const FIREBASE_PROJECT_ID = process.env.FIREBASE_PROJECT_ID;

// DB config
export const DB_HOST = process.env.DB_HOST || 'localhost';
export const DB_PORT = Number(process.env.DB_PORT || 10002);
export const DB_USER = process.env.DB_USER || 'pickaxe';
export const DB_PASSWORD = process.env.DB_PASSWORD || 'pickaxe';
export const DB_NAME = process.env.DB_NAME || 'pickaxe_auth';
