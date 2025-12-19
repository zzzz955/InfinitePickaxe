/**
 * CSV -> JSON 변환 및 meta_bundle.json 생성 스크립트
 * Usage: node metadata/build_meta_bundle.js
 *
 * - metadata/csv/*.csv 를 읽어 각 JSON 생성
 * - metadata/meta_bundle.json 으로 합본 생성 (SHA-256 로그 출력)
 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const rootDir = process.cwd();
const metaDir = path.join(rootDir, 'metadata');
const csvDir = path.join(metaDir, 'csv');
const bundlePath = path.join(metaDir, 'meta_bundle.json');

if (!fs.existsSync(csvDir)) {
  throw new Error(`CSV 디렉터리를 찾을 수 없습니다: ${csvDir}`);
}

function splitCsvLine(line) {
  const cells = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i += 1) {
    const char = line[i];
    if (char === '"') {
      if (inQuotes && line[i + 1] === '"') {
        current += '"';
        i += 1;
      } else {
        inQuotes = !inQuotes;
      }
    } else if (char === ',' && !inQuotes) {
      cells.push(current);
      current = '';
    } else {
      current += char;
    }
  }

  cells.push(current);
  return cells.map((cell) => cell.trim());
}

function readCsv(fileName) {
  const fullPath = path.join(csvDir, fileName);

  if (!fs.existsSync(fullPath)) {
    throw new Error(`CSV 파일이 없습니다: ${fileName}`);
  }

  const raw = fs.readFileSync(fullPath, 'utf-8').replace(/^\uFEFF/, '');
  const lines = raw.split(/\r?\n/).filter((line) => line.trim() !== '');

  if (lines.length === 0) {
    return [];
  }

  const headers = splitCsvLine(lines[0]);

  return lines.slice(1).map((line) => {
    const cells = splitCsvLine(line);
    const row = {};

    headers.forEach((header, idx) => {
      row[header] = cells[idx] !== undefined ? cells[idx] : '';
    });

    return row;
  });
}

function requireSingleRow(rows, fileName) {
  if (rows.length !== 1) {
    throw new Error(`${fileName} 은(는) 단일 행이어야 합니다. 현재 ${rows.length}행`);
  }

  return rows[0];
}

function requireField(value, fieldName, context) {
  if (value === undefined || value === '') {
    throw new Error(`${context} 필수값 누락: ${fieldName}`);
  }

  return value;
}

function toNumber(value, context) {
  const num = Number(value);

  if (Number.isNaN(num)) {
    throw new Error(`${context} 숫자 변환 실패: "${value}"`);
  }

  return num;
}

function toBoolean(value, context) {
  const normalized = String(value).toLowerCase();

  if (['true', '1', 'yes', 'y'].includes(normalized)) {
    return true;
  }

  if (['false', '0', 'no', 'n'].includes(normalized)) {
    return false;
  }

  throw new Error(`${context} 불리언 변환 실패: "${value}"`);
}

function toList(value, parser, context) {
  if (value === undefined || value === '') {
    return undefined;
  }

  return String(value)
    .split('|')
    .map((item, idx) => parser(item, `${context}[${idx + 1}]`));
}

function writeJson(fileName, data) {
  const fullPath = path.join(metaDir, fileName);
  const body = JSON.stringify(data, null, 2);

  fs.writeFileSync(fullPath, `${body}\n`, 'utf-8');
}

function buildAds() {
  const config = requireSingleRow(readCsv('ads_config.csv'), 'ads_config.csv');

  const adTypes = readCsv('ads.csv').map((row, idx) => {
    const context = `ads.csv row ${idx + 2}`;
    const parameters = {};

    if (row['parameters.cost_multiplier'] !== '') {
      parameters.cost_multiplier = toNumber(row['parameters.cost_multiplier'], `${context} parameters.cost_multiplier`);
    }

    if (row['parameters.apply_to_slots'] !== '') {
      parameters.apply_to_slots = row['parameters.apply_to_slots'];
    }

    if (row['parameters.progress_reset_on_reroll'] !== '') {
      parameters.progress_reset_on_reroll = toBoolean(row['parameters.progress_reset_on_reroll'], `${context} parameters.progress_reset_on_reroll`);
    }

    const rewards = toList(row.rewards_by_view, (v, c) => toNumber(v, c), `${context} rewards_by_view`);

    const ad = {
      id: requireField(row.id, 'id', context),
      daily_limit: toNumber(row.daily_limit, `${context} daily_limit`),
      effect: requireField(row.effect, 'effect', context),
    };

    if (Object.keys(parameters).length > 0) {
      ad.parameters = parameters;
    }

    if (rewards && rewards.length > 0) {
      ad.rewards_by_view = rewards;
    }

    return ad;
  });

  return {
    key: 'ads',
    file: 'ads.json',
    data: {
      reset_time_kst: requireField(config.reset_time_kst, 'reset_time_kst', 'ads_config.csv'),
      ad_types: adTypes,
    },
  };
}

function buildDailyMissions() {
  const config = requireSingleRow(readCsv('daily_missions_config.csv'), 'daily_missions_config.csv');
  const pools = {};

  readCsv('daily_mission_pools.csv').forEach((row, idx) => {
    const context = `daily_mission_pools.csv row ${idx + 2}`;
    const pool = requireField(row.pool, 'pool', context);
    const entry = {
      id: requireField(row.id, 'id', context),
      type: requireField(row.type, 'type', context),
      target: toNumber(row.target, `${context} target`),
      description: requireField(row.description, 'description', context),
      reward_crystal: toNumber(row.reward_crystal, `${context} reward_crystal`),
    };

    if (!pools[pool]) {
      pools[pool] = [];
    }

    pools[pool].push(entry);
  });

  const milestones = readCsv('daily_mission_milestones.csv').map((row, idx) => {
    const context = `daily_mission_milestones.csv row ${idx + 2}`;

    return {
      completed: toNumber(row.completed, `${context} completed`),
      bonus_hours: toNumber(row.bonus_hours, `${context} bonus_hours`),
    };
  });

  return {
    key: 'daily_missions',
    file: 'daily_missions.json',
    data: {
      reset_time_kst: requireField(config.reset_time_kst, 'reset_time_kst', 'daily_missions_config.csv'),
      total_slots: toNumber(config.total_slots, 'daily_missions_config.csv total_slots'),
      max_daily_assign: toNumber(config.max_daily_assign, 'daily_missions_config.csv max_daily_assign'),
      pools,
      milestone_offline_bonus_hours: milestones,
    },
  };
}

function buildMinerals() {
  const minerals = readCsv('minerals.csv').map((row, idx) => {
    const context = `minerals.csv row ${idx + 2}`;

    return {
      id: toNumber(row.id, `${context} id`),
      name: requireField(row.name, 'name', context),
      hp: toNumber(row.hp, `${context} hp`),
      gold: toNumber(row.gold, `${context} gold`),
      recommended_level: requireField(row.recommended_level, 'recommended_level', context),
      biome: requireField(row.biome, 'biome', context),
    };
  });

  return {
    key: 'minerals',
    file: 'minerals.json',
    data: minerals,
  };
}

function buildMissionReroll() {
  const row = requireSingleRow(readCsv('mission_reroll.csv'), 'mission_reroll.csv');

  return {
    key: 'mission_reroll',
    file: 'mission_reroll.json',
    data: {
      reset_time_kst: requireField(row.reset_time_kst, 'reset_time_kst', 'mission_reroll.csv'),
      free_rerolls_per_day: toNumber(row.free_rerolls_per_day, 'mission_reroll.csv free_rerolls_per_day'),
      ad_rerolls_per_day: toNumber(row.ad_rerolls_per_day, 'mission_reroll.csv ad_rerolls_per_day'),
      apply_to_slots: toBoolean(row.apply_to_slots, 'mission_reroll.csv apply_to_slots'),
      progress_reset_on_reroll: toBoolean(row.progress_reset_on_reroll, 'mission_reroll.csv progress_reset_on_reroll'),
    },
  };
}

function buildOfflineDefaults() {
  const row = requireSingleRow(readCsv('offline_defaults.csv'), 'offline_defaults.csv');

  return {
    key: 'offline_defaults',
    file: 'offline_defaults.json',
    data: {
      initial_offline_hours: toNumber(row.initial_offline_hours, 'offline_defaults.csv initial_offline_hours'),
    },
  };
}

function buildPickaxeLevels() {
  const levels = readCsv('pickaxe_levels.csv').map((row, idx) => {
    const context = `pickaxe_levels.csv row ${idx + 2}`;

    return {
      level: toNumber(row.level, `${context} level`),
      tier: requireField(row.tier, 'tier', context),
      appearance: requireField(row.appearance, 'appearance', context),
      attack_power: toNumber(row.attack_power, `${context} attack_power`),
      attack_speed: toNumber(row.attack_speed, `${context} attack_speed`),
      dps: toNumber(row.dps, `${context} dps`),
      cost: toNumber(row.cost, `${context} cost`),
      cumulative_cost: toNumber(row.cumulative_cost, `${context} cumulative_cost`),
    };
  });

  return {
    key: 'pickaxe_levels',
    file: 'pickaxe_levels.json',
    data: levels,
  };
}

function buildUpgradeRules() {
  const config = requireSingleRow(readCsv('upgrade_rules_config.csv'), 'upgrade_rules_config.csv');
  const baseRateByTier = {};

  readCsv('upgrade_base_rates.csv').forEach((row, idx) => {
    const context = `upgrade_base_rates.csv row ${idx + 2}`;
    const tier = requireField(row.tier, 'tier', context);

    baseRateByTier[String(tier)] = toNumber(row.base_rate, `${context} base_rate`);
  });

  return {
    key: 'upgrade_rules',
    file: 'upgrade_rules.json',
    data: {
      min_rate: toNumber(config.min_rate, 'upgrade_rules_config.csv min_rate'),
      bonus_rate: toNumber(config.bonus_rate, 'upgrade_rules_config.csv bonus_rate'),
      base_rate_by_tier: baseRateByTier,
    },
  };
}

const builders = [
  buildAds,
  buildDailyMissions,
  buildMinerals,
  buildMissionReroll,
  buildOfflineDefaults,
  buildPickaxeLevels,
  buildUpgradeRules,
];

const bundle = {};

builders.forEach((builder) => {
  const { key, file, data } = builder();

  bundle[key] = data;
  writeJson(file, data);
});

const bundleJson = JSON.stringify(bundle, null, 2);
fs.writeFileSync(bundlePath, `${bundleJson}\n`, 'utf-8');

const hash = crypto.createHash('sha256').update(bundleJson).digest('hex');
console.log(`meta_bundle.json 생성 완료: ${bundlePath}`);
console.log(`SHA-256: ${hash}`);
