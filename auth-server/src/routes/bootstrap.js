import { Router } from 'express';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { PROTOCOL_VERSION, META_HASH, META_FILE, STORE_URL } from '../config.js';

const router = Router();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function buildMetaResponse() {
  if (!META_HASH) return null;

  try {
    const filePath = path.resolve(process.cwd(), META_FILE);
    const data = fs.readFileSync(filePath);
    const base64 = data.toString('base64');
    return {
      hash: META_HASH,
      size_bytes: data.length,
      download_url: null,
      data: base64
    };
  } catch (err) {
    console.error('[bootstrap] 메타 파일을 읽지 못했습니다:', err.message);
    return {
      hash: META_HASH,
      size_bytes: 0,
      download_url: null,
      data: null
    };
  }
}

router.post('/bootstrap', (req, res) => {
  const { protocol_version: clientProtocol, cached_meta_hash: cachedMetaHash } = req.body || {};

  if (!clientProtocol) {
    return res.status(400).json({ action: 'UPDATE_REQUIRED', message: 'protocol_version 누락', store_url: STORE_URL || null });
  }

  if (clientProtocol !== PROTOCOL_VERSION) {
    return res.json({
      action: 'UPDATE_REQUIRED',
      message: '클라이언트 버전이 오래되었습니다. 업데이트가 필요합니다.',
      store_url: STORE_URL || null,
      retry_after_seconds: 0,
      meta: null
    });
  }

  let meta = null;
  if (META_HASH && META_HASH !== cachedMetaHash) {
    meta = buildMetaResponse();
  }

  return res.json({
    action: 'PROCEED',
    message: null,
    store_url: null,
    retry_after_seconds: 0,
    meta
  });
});

export default router;
