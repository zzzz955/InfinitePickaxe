/**
 * 메타데이터 디렉토리의 JSON을 하나로 묶어 meta_bundle.json 생성
 * Usage: node build_meta_bundle.js
 * Output: metadata/meta_bundle.json + SHA256 해시 출력
 */
import fs from 'fs';
import path from 'path';
import crypto from 'crypto';

const root = process.cwd();
const metaDir = path.join(root, 'metadata');
const outFile = path.join(metaDir, 'meta_bundle.json');

const files = [
  'ads.json',
  'daily_missions.json',
  'minerals.json',
  'mission_reroll.json',
  'offline_defaults.json',
  'pickaxe_levels.json',
  'upgrade_rules.json'
];

function readJson(file) {
  const p = path.join(metaDir, file);
  const data = fs.readFileSync(p, 'utf-8');
  return JSON.parse(data);
}

const bundle = {};
for (const f of files) {
  const key = f.replace('.json', '');
  bundle[key] = readJson(f);
}

const normalized = JSON.stringify(bundle, null, 2);
fs.writeFileSync(outFile, normalized, 'utf-8');

const hash = crypto.createHash('sha256').update(normalized).digest('hex');
console.log(`meta_bundle.json written. SHA256=${hash}`);
