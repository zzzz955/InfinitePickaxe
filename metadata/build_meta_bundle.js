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

function readKeyValueConfig(fileName) {
  const rows = readCsv(fileName);
  const config = {};
  rows.forEach((row, idx) => {
    const context = `${fileName} row ${idx + 2}`;
    const key = requireField(row.key, 'key', context);
    config[key] = row.value !== undefined ? row.value : '';
  });
  return config;
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

function parseJsonSafe(value, context) {
  try {
    return JSON.parse(value);
  } catch (err) {
    throw new Error(`${context} JSON 파싱 실패: ${err.message}`);
  }
}

function writeJson(fileName, data) {
  const fullPath = path.join(metaDir, fileName);
  const body = JSON.stringify(data, null, 2);

  fs.writeFileSync(fullPath, `${body}\n`, 'utf-8');
}

function buildAds() {
  const config = readKeyValueConfig('ads_config.csv');

  const adTypes = readCsv('ads.csv').map((row, idx) => {
    const context = `ads.csv row ${idx + 2}`;
    let parameters = {};
    if (row.parameters) {
      if (row.parameters.trim() !== '') {
        parameters = parseJsonSafe(row.parameters, `${context} parameters`);
      }
    } else {
      if (row['parameters.cost_multiplier'] !== undefined && row['parameters.cost_multiplier'] !== '') {
        parameters.cost_multiplier = toNumber(row['parameters.cost_multiplier'], `${context} parameters.cost_multiplier`);
      }
      if (row['parameters.apply_to_slots'] !== undefined && row['parameters.apply_to_slots'] !== '') {
        parameters.apply_to_slots = row['parameters.apply_to_slots'];
      }
      if (row['parameters.progress_reset_on_reroll'] !== undefined && row['parameters.progress_reset_on_reroll'] !== '') {
        parameters.progress_reset_on_reroll = toBoolean(row['parameters.progress_reset_on_reroll'], `${context} parameters.progress_reset_on_reroll`);
      }
    }

    let rewards;
    if (row.rewards_by_view !== undefined && row.rewards_by_view !== '') {
      const raw = String(row.rewards_by_view).trim();
      if (raw.startsWith('[')) {
        rewards = parseJsonSafe(raw, `${context} rewards_by_view`);
      } else {
        rewards = toList(raw, (v, c) => toNumber(v, c), `${context} rewards_by_view`);
      }
    }

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
  const config = readKeyValueConfig('daily_missions_config.csv');
  const missions = [];

  readCsv('daily_missions.csv').forEach((row, idx) => {
    const context = `daily_missions.csv row ${idx + 2}`;
    const difficulty = requireField(row.difficulty || row.pool, 'difficulty', context);
    const mineralRaw = row.mineral_id;
    const mineralId =
      mineralRaw === undefined || mineralRaw === null || `${mineralRaw}`.trim() === '' || `${mineralRaw}`.toLowerCase() === 'null'
        ? null
        : toNumber(mineralRaw, `${context} mineral_id`);
    const entry = {
      id: toNumber(requireField(row.id, 'id', context), `${context} id`),
      difficulty,
      type: requireField(row.type, 'type', context),
      target: toNumber(row.target, `${context} target`),
      mineral_id: mineralId,
      description: requireField(row.description, 'description', context),
      reward_crystal: toNumber(row.reward_crystal, `${context} reward_crystal`),
    };
    missions.push(entry);
  });

  const milestones = readCsv('daily_missions_milestones.csv').map((row, idx) => {
    const context = `daily_missions_milestones.csv row ${idx + 2}`;

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
      missions,
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
      recommended_min_DPS: toNumber(row.recommended_min_DPS, `${context} recommended_min_DPS`),
      recommended_max_DPS: toNumber(row.recommended_max_DPS, `${context} recommended_max_DPS`),
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
  const row = readKeyValueConfig('mission_reroll.csv');

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
  const row = readKeyValueConfig('offline_defaults.csv');

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
  const config = readKeyValueConfig('upgrade_rules_config.csv');
  const baseRateByTier = {};

  readCsv('upgrade_rules_tier_rates.csv').forEach((row, idx) => {
    const context = `upgrade_rules_tier_rates.csv row ${idx + 2}`;
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

function buildGemTypes() {
  const types = readCsv('gem_types.csv').map((row, idx) => {
    const context = `gem_types.csv row ${idx + 2}`;

    return {
      id: toNumber(row.id, `${context} id`),
      type: requireField(row.type, 'type', context),
      display_name: requireField(row.display_name, 'display_name', context),
      description: requireField(row.description, 'description', context),
      stat_key: requireField(row.stat_key, 'stat_key', context),
    };
  });

  return {
    key: 'gem_types',
    file: 'gem_types.json',
    data: types,
  };
}

function buildGemGrades() {
  const grades = readCsv('gem_grades.csv').map((row, idx) => {
    const context = `gem_grades.csv row ${idx + 2}`;

    return {
      id: toNumber(row.id, `${context} id`),
      grade: requireField(row.grade, 'grade', context),
      display_name: requireField(row.display_name, 'display_name', context),
    };
  });

  return {
    key: 'gem_grades',
    file: 'gem_grades.json',
    data: grades,
  };
}

function buildGemDefinitions() {
  const definitions = readCsv('gem_definitions.csv').map((row, idx) => {
    const context = `gem_definitions.csv row ${idx + 2}`;

    return {
      gem_id: toNumber(row.gem_id, `${context} gem_id`),
      grade_id: toNumber(row.grade_id, `${context} grade_id`),
      type_id: toNumber(row.type_id, `${context} type_id`),
      name: requireField(row.name, 'name', context),
      icon: requireField(row.icon, 'icon', context),
      stat_multiplier: toNumber(row.stat_multiplier, `${context} stat_multiplier`),
    };
  });

  return {
    key: 'gem_definitions',
    file: 'gem_definitions.json',
    data: definitions,
  };
}

function buildGemGacha() {
  const config = readKeyValueConfig('gem_gacha_costs.csv');

  const rates = readCsv('gem_gacha_rates.csv').map((row, idx) => {
    const context = `gem_gacha_rates.csv row ${idx + 2}`;

    return {
      grade_id: toNumber(row.grade_id, `${context} grade_id`),
      rate_percent: toNumber(row.rate_percent, `${context} rate_percent`),
    };
  });

  return {
    key: 'gem_gacha',
    file: 'gem_gacha.json',
    data: {
      single_pull_cost: toNumber(config.single_pull_cost, 'gem_gacha_costs.csv single_pull_cost'),
      multi_pull_cost: toNumber(config.multi_pull_cost, 'gem_gacha_costs.csv multi_pull_cost'),
      multi_pull_count: toNumber(config.multi_pull_count, 'gem_gacha_costs.csv multi_pull_count'),
      grade_rates: rates,
    },
  };
}

function buildGemConversion() {
  const conversion = readCsv('gem_conversion_costs.csv').map((row, idx) => {
    const context = `gem_conversion_costs.csv row ${idx + 2}`;

    return {
      grade_id: toNumber(row.grade_id, `${context} grade_id`),
      random_cost: toNumber(row.random_cost, `${context} random_cost`),
      fixed_cost: toNumber(row.fixed_cost, `${context} fixed_cost`),
    };
  });

  return {
    key: 'gem_conversion',
    file: 'gem_conversion.json',
    data: conversion,
  };
}

function buildGemDiscard() {
  const discard = readCsv('gem_discard_rewards.csv').map((row, idx) => {
    const context = `gem_discard_rewards.csv row ${idx + 2}`;

    return {
      grade_id: toNumber(row.grade_id, `${context} grade_id`),
      crystal_reward: toNumber(row.crystal_reward, `${context} crystal_reward`),
    };
  });

  return {
    key: 'gem_discard',
    file: 'gem_discard.json',
    data: discard,
  };
}

function buildGemInventory() {
  const config = readKeyValueConfig('gem_inventory_config.csv');

  return {
    key: 'gem_inventory',
    file: 'gem_inventory.json',
    data: {
      base_capacity: toNumber(config.base_capacity, 'gem_inventory_config.csv base_capacity'),
      max_capacity: toNumber(config.max_capacity, 'gem_inventory_config.csv max_capacity'),
      expand_step: toNumber(config.expand_step, 'gem_inventory_config.csv expand_step'),
      expand_cost: toNumber(config.expand_cost, 'gem_inventory_config.csv expand_cost'),
    },
  };
}

function buildGemSynthesisRules() {
  const rules = readCsv('gem_synthesis_rules.csv').map((row, idx) => {
    const context = `gem_synthesis_rules.csv row ${idx + 2}`;

    return {
      from_grade: requireField(row.from_grade, 'from_grade', context),
      to_grade: requireField(row.to_grade, 'to_grade', context),
      success_rate_percent: toNumber(row.success_rate_percent, `${context} success_rate_percent`),
    };
  });

  return {
    key: 'gem_synthesis_rules',
    file: 'gem_synthesis_rules.json',
    data: rules,
  };
}

function buildGemSlotUnlockCosts() {
  const costs = readCsv('gem_slot_unlock_costs.csv').map((row, idx) => {
    const context = `gem_slot_unlock_costs.csv row ${idx + 2}`;

    return {
      slot_index: toNumber(row.slot_index, `${context} slot_index`),
      unlock_cost_crystal: toNumber(row.unlock_cost_crystal, `${context} unlock_cost_crystal`),
    };
  });

  return {
    key: 'gem_slot_unlock_costs',
    file: 'gem_slot_unlock_costs.json',
    data: costs,
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
  buildGemTypes,
  buildGemGrades,
  buildGemDefinitions,
  buildGemGacha,
  buildGemConversion,
  buildGemDiscard,
  buildGemInventory,
  buildGemSynthesisRules,
  buildGemSlotUnlockCosts,
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
