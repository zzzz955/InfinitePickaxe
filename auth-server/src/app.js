import express from 'express';
import bodyParser from 'body-parser';
import authRouter from './routes/auth.js';
import bootstrapRouter from './routes/bootstrap.js';
import { PORT } from './config.js';

const app = express();
app.use(bodyParser.json());

// 공통 실패 로깅 미들웨어
app.use((req, res, next) => {
  res.on('finish', () => {
    if (res.statusCode >= 400) {
      try {
        console.error(`[api][${req.method}] ${req.originalUrl} ${res.statusCode} body=${JSON.stringify(req.body)}`);
      } catch (err) {
        console.error(`[api][${req.method}] ${req.originalUrl} ${res.statusCode} (body stringify failed)`);
      }
    }
  });
  next();
});

app.get('/health', (_req, res) => res.json({ ok: true }));
app.use('/', bootstrapRouter);
app.use('/auth', authRouter);

app.listen(PORT, () => {
  console.log(`Auth server listening on :${PORT}`);
});
