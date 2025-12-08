import express from 'express';
import bodyParser from 'body-parser';
import authRouter from './routes/auth.js';
import { PORT } from './config.js';

const app = express();
app.use(bodyParser.json());

app.get('/health', (_req, res) => res.json({ ok: true }));
app.use('/auth', authRouter);

app.listen(PORT, () => {
  console.log(`Auth server listening on :${PORT}`);
});
