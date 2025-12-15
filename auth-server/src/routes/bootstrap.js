import { Router } from 'express';
import fs from 'fs';
import path from 'path';
import crypto from 'crypto';
import { fileURLToPath } from 'url';
import { PROTOCOL_VERSION, META_HASH, META_FILE, STORE_URL } from '../config.js';

const router = Router();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

console.log(`[bootstrap] PROTOCOL_VERSION=${PROTOCOL_VERSION}`);
console.log(`[bootstrap] META_FILE=${META_FILE}`);
if (META_HASH) console.log(`[bootstrap] META_HASH (env)=${META_HASH}`);
else console.log('[bootstrap] META_HASH env not set, will use computed hash from file');

let metaCache = null;

// Preload meta to log hash/size at startup (ignore failures)
getMetaInfo();

function getMetaInfo() {
  if (metaCache) return metaCache;

  try {
    const filePath = path.resolve(process.cwd(), META_FILE);
    const data = fs.readFileSync(filePath);
    const computedHash = crypto.createHash('sha256').update(data).digest('hex');
    let hash = computedHash;
    if (META_HASH && META_HASH !== computedHash) {
      console.warn(`[bootstrap] META_HASH env (${META_HASH}) != computed (${computedHash}), using computed hash.`);
    } else if (META_HASH) {
      hash = META_HASH;
    }
    console.log(`[bootstrap] meta loaded: path=${filePath}, size=${data.length}, hash=${computedHash} (used=${hash})`);
    metaCache = {
      hash,
      size_bytes: data.length,
      base64: data.toString('base64')
    };
    return metaCache;
  } catch (err) {
    console.error('[bootstrap] 메타 파일을 읽지 못했습니다:', err.message);
    return null;
  }
}

function buildMetaResponse() {
  const metaInfo = getMetaInfo();
  if (!metaInfo) return null;

  return {
    hash: metaInfo.hash,
    size_bytes: metaInfo.size_bytes,
    download_url: null,
    data: metaInfo.base64
  };
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

  const metaInfo = getMetaInfo();
  let meta = null;
  if (metaInfo && metaInfo.hash !== cachedMetaHash) {
    meta = buildMetaResponse();
  }

  const metaSent = meta ? meta.hash : null;
  const metaDataLen = meta && meta.data ? meta.data.length : 0;
  console.log(`[bootstrap] req protocol=${clientProtocol}, cached_meta_hash=${cachedMetaHash || '(empty)'} -> action=PROCEED, meta=${metaSent || 'null'}, dataLen=${metaDataLen}`);

  return res.json({
    action: 'PROCEED',
    message: null,
    store_url: null,
    retry_after_seconds: 0,
    meta
  });
});

export default router;
