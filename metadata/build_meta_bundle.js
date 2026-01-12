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

function toOptionalNumber(value, context) {
  if (value === undefined || value === null) {
    return null;
  }

  const trimmed = `${value}`.trim();
  if (trimmed === '' || trimmed.toLowerCase() === 'null') {
    return null;
  }

  return toNumber(trimmed, context);
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
      reward_crystal: toNumber(row.reward_crystal, `${context} reward_crystal`),
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

function buildWeeklyMissions() {
  const config = readKeyValueConfig('weekly_missions_config.csv');
  const missions = [];

  readCsv('weekly_missions.csv').forEach((row, idx) => {
    const context = `weekly_missions.csv row ${idx + 2}`;
    const entry = {
      id: toNumber(requireField(row.id, 'id', context), `${context} id`),
      type: requireField(row.type, 'type', context),
      target: toNumber(row.target, `${context} target`),
      title: requireField(row.title, 'title', context),
      description: requireField(row.description, 'description', context),
      reward_crystal: toNumber(row.reward_crystal, `${context} reward_crystal`),
    };
    missions.push(entry);
  });

  const milestones = readCsv('weekly_missions_milestones.csv').map((row, idx) => {
    const context = `weekly_missions_milestones.csv row ${idx + 2}`;

    return {
      completed: toNumber(row.completed, `${context} completed`),
      reward_crystal: toNumber(row.reward_crystal, `${context} reward_crystal`),
    };
  });

  return {
    key: 'weekly_missions',
    file: 'weekly_missions.json',
    data: {
      reset_weekday_kst: requireField(config.reset_weekday_kst, 'reset_weekday_kst', 'weekly_missions_config.csv'),
      reset_time_kst: requireField(config.reset_time_kst, 'reset_time_kst', 'weekly_missions_config.csv'),
      missions,
      milestone_rewards: milestones,
    },
  };
}

function buildAchievements() {
  const achievements = readCsv('achievements.csv').map((row, idx) => {
    const context = `achievements.csv row ${idx + 2}`;
    const entry = {
      achievement_id: toNumber(requireField(row.achievement_id, 'achievement_id', context), `${context} achievement_id`),
      chain_id: toNumber(requireField(row.chain_id, 'chain_id', context), `${context} chain_id`),
      step_index: toNumber(requireField(row.step_index, 'step_index', context), `${context} step_index`),
      type: requireField(row.type, 'type', context),
      target: toNumber(requireField(row.target, 'target', context), `${context} target`),
      title: requireField(row.title, 'title', context),
      description: requireField(row.description, 'description', context),
    };

    const rewardCrystal = toOptionalNumber(row.reward_crystal, `${context} reward_crystal`);
    if (rewardCrystal !== null) {
      entry.reward_crystal = rewardCrystal;
    }

    const rewardGold = toOptionalNumber(row.reward_gold, `${context} reward_gold`);
    if (rewardGold !== null) {
      entry.reward_gold = rewardGold;
    }

    return entry;
  });

  return {
    key: 'achievements',
    file: 'achievements.json',
    data: achievements,
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

function buildMineralsInfo() {
  const minerals = readCsv('minerals_info.csv').map((row, idx) => {
    const context = `minerals_info.csv row ${idx + 2}`;

    return {
      id: toNumber(requireField(row.id, 'id', context), `${context} id`),
      name: requireField(row.name, 'name', context),
      sprite_key: requireField(row.sprite_key, 'sprite_key', context),
    };
  });

  return {
    key: 'minerals_info',
    file: 'minerals_info.json',
    data: minerals,
  };
}

function buildInfiniteMine() {
  const config = readKeyValueConfig('infinite_mine_config.csv');
  const floors = readCsv('infinite_mine_floors.csv').map((row, idx) => {
    const context = `infinite_mine_floors.csv row ${idx + 2}`;

    let rewardGold = toOptionalNumber(row.reward_gold, `${context} reward_gold`);
    if (rewardGold === null) rewardGold = 0;
    let rewardCrystal = toOptionalNumber(row.reward_crystal, `${context} reward_crystal`);
    if (rewardCrystal === null) rewardCrystal = 0;

    return {
      floor: toNumber(row.floor, `${context} floor`),
      mineral_info_id: toNumber(row.mineral_info_id, `${context} mineral_info_id`),
      hp: toNumber(row.hp, `${context} hp`),
      reward_gold: rewardGold,
      reward_crystal: rewardCrystal,
      biome_id: toNumber(row.biome_id, `${context} biome_id`),
    };
  });

  return {
    key: 'infinite_mine',
    file: 'infinite_mine.json',
    data: {
      reset_time_kst: requireField(config.reset_time_kst, 'reset_time_kst', 'infinite_mine_config.csv'),
      time_limit_sec: toNumber(config.time_limit_sec, 'infinite_mine_config.csv time_limit_sec'),
      max_floor: toNumber(config.max_floor, 'infinite_mine_config.csv max_floor'),
      auto_reward_divisor: toNumber(config.auto_reward_divisor, 'infinite_mine_config.csv auto_reward_divisor'),
      floors,
    },
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

function buildMail() {
  const config = readKeyValueConfig('mail_config.csv');

  const templates = readCsv('mail_templates.csv').map((row, idx) => {
    const context = `mail_templates.csv row ${idx + 2}`;
    const entry = {
      template_id: toNumber(requireField(row.template_id, 'template_id', context), `${context} template_id`),
      mail_type: requireField(row.mail_type, 'mail_type', context),
      title: requireField(row.title, 'title', context),
      body: requireField(row.body, 'body', context),
      sender: row.sender !== undefined ? row.sender : ''
    };

    if (row.default_expire_days !== undefined && String(row.default_expire_days).trim() !== '') {
      entry.default_expire_days = toNumber(row.default_expire_days, `${context} default_expire_days`);
    }

    return entry;
  });

  return {
    key: 'mail',
    file: 'mail.json',
    data: {
      max_mail_count: toNumber(config.max_mail_count, 'mail_config.csv max_mail_count'),
      expire_days: toNumber(config.expire_days, 'mail_config.csv expire_days'),
      claim_all_limit: toNumber(config.claim_all_limit, 'mail_config.csv claim_all_limit'),
      default_list_limit: toNumber(config.default_list_limit, 'mail_config.csv default_list_limit'),
      templates
    }
  };
}

function buildRarityInfo() {
  const rarities = readCsv('rarity_info.csv').map((row, idx) => {
    const context = `rarity_info.csv row ${idx + 2}`;

    return {
      rarity_id: toNumber(requireField(row.rarity_id, 'rarity_id', context), `${context} rarity_id`),
      rarity_name: requireField(row.rarity_name, 'rarity_name', context),
      bg_color: requireField(row.bg_color, 'bg_color', context),
      text_color: requireField(row.text_color, 'text_color', context),
      sort_order: toNumber(requireField(row.sort_order, 'sort_order', context), `${context} sort_order`),
    };
  });

  return {
    key: 'rarity_info',
    file: 'rarity_info.json',
    data: rarities,
  };
}

function buildCurrencyInfo() {
  const currencies = readCsv('currency_info.csv').map((row, idx) => {
    const context = `currency_info.csv row ${idx + 2}`;

    return {
      currency_id: toNumber(requireField(row.currency_id, 'currency_id', context), `${context} currency_id`),
      currency_type: requireField(row.currency_type, 'currency_type', context),
      sprite_key: requireField(row.sprite_key, 'sprite_key', context),
      rarity_id: toNumber(requireField(row.rarity_id, 'rarity_id', context), `${context} rarity_id`),
      display_name: requireField(row.display_name, 'display_name', context),
    };
  });

  return {
    key: 'currency_info',
    file: 'currency_info.json',
    data: currencies,
  };
}

function buildItemInfo() {
  const items = readCsv('item_info.csv').map((row, idx) => {
    const context = `item_info.csv row ${idx + 2}`;
    const entry = {
      item_id: toNumber(requireField(row.item_id, 'item_id', context), `${context} item_id`),
      item_type: requireField(row.item_type, 'item_type', context),
      sprite_key: requireField(row.sprite_key, 'sprite_key', context),
      rarity_id: toNumber(requireField(row.rarity_id, 'rarity_id', context), `${context} rarity_id`),
      display_name: requireField(row.display_name, 'display_name', context),
      stackable: toBoolean(requireField(row.stackable, 'stackable', context), `${context} stackable`),
      max_stack: toNumber(requireField(row.max_stack, 'max_stack', context), `${context} max_stack`),
      use_action_type: requireField(row.use_action_type, 'use_action_type', context),
    };

    const useActionRefId = toOptionalNumber(row.use_action_ref_id, `${context} use_action_ref_id`);
    if (useActionRefId !== null) {
      entry.use_action_ref_id = useActionRefId;
    }

    if (row.description !== undefined && row.description !== '') {
      entry.description = row.description;
    }

    return entry;
  });

  return {
    key: 'item_info',
    file: 'item_info.json',
    data: items,
  };
}

function buildWeeklyRanking() {
  const config = readKeyValueConfig('weekly_ranking_config.csv');

  const rewards = readCsv('weekly_ranking_rewards.csv').map((row, idx) => {
    const context = `weekly_ranking_rewards.csv row ${idx + 2}`;
    const rewardKey = row.reward_key !== undefined ? String(row.reward_key).trim() : '';

    const entry = {
      rank_min: toNumber(requireField(row.rank_min, 'rank_min', context), `${context} rank_min`),
      rank_max: toNumber(requireField(row.rank_max, 'rank_max', context), `${context} rank_max`),
      reward_index: toNumber(requireField(row.reward_index, 'reward_index', context), `${context} reward_index`),
      reward_type: requireField(row.reward_type, 'reward_type', context),
      amount: toNumber(requireField(row.amount, 'amount', context), `${context} amount`),
      template_id: toNumber(requireField(row.template_id, 'template_id', context), `${context} template_id`)
    };

    if (rewardKey !== '') {
      entry.reward_key = rewardKey;
    }

    return entry;
  });

  return {
    key: 'weekly_ranking',
    file: 'weekly_ranking.json',
    data: {
      reset_weekday_kst: requireField(config.reset_weekday_kst, 'reset_weekday_kst', 'weekly_ranking_config.csv'),
      reset_time_kst: requireField(config.reset_time_kst, 'reset_time_kst', 'weekly_ranking_config.csv'),
      rewards
    }
  };
}

function buildNewUserDefaults() {
  const row = readKeyValueConfig('new_user_defaults.csv');
  const pickaxeSlots =
    toList(row.initial_unlocked_pickaxe_slots, (v, c) => toNumber(v, c), 'new_user_defaults.csv initial_unlocked_pickaxe_slots') || [0];
  const gemSlots =
    toList(row.initial_unlocked_gem_slots, (v, c) => toNumber(v, c), 'new_user_defaults.csv initial_unlocked_gem_slots') || [0];

  return {
    key: 'new_user_defaults',
    file: 'new_user_defaults.json',
    data: {
      initial_gold: toNumber(row.initial_gold, 'new_user_defaults.csv initial_gold'),
      initial_crystal: toNumber(row.initial_crystal, 'new_user_defaults.csv initial_crystal'),
      initial_pickaxe_level: toNumber(row.initial_pickaxe_level, 'new_user_defaults.csv initial_pickaxe_level'),
      initial_critical_hit_percent: toNumber(row.initial_critical_hit_percent, 'new_user_defaults.csv initial_critical_hit_percent'),
      initial_critical_damage: toNumber(row.initial_critical_damage, 'new_user_defaults.csv initial_critical_damage'),
      initial_pity_bonus: toNumber(row.initial_pity_bonus, 'new_user_defaults.csv initial_pity_bonus'),
      initial_unlocked_pickaxe_slots: pickaxeSlots,
      initial_unlocked_gem_slots: gemSlots,
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

function buildGemSelectGroups() {
  const groups = readCsv('gem_select_groups.csv').map((row, idx) => {
    const context = `gem_select_groups.csv row ${idx + 2}`;

    const entry = {
      group_id: toNumber(requireField(row.group_id, 'group_id', context), `${context} group_id`),
      group_name: requireField(row.group_name, 'group_name', context),
      max_select: toNumber(requireField(row.max_select, 'max_select', context), `${context} max_select`),
    };

    if (row.description !== undefined && row.description !== '') {
      entry.description = row.description;
    }

    return entry;
  });

  return {
    key: 'gem_select_groups',
    file: 'gem_select_groups.json',
    data: groups,
  };
}

function buildGemSelectOptions() {
  const options = readCsv('gem_select_options.csv').map((row, idx) => {
    const context = `gem_select_options.csv row ${idx + 2}`;

    return {
      group_id: toNumber(requireField(row.group_id, 'group_id', context), `${context} group_id`),
      choice_index: toNumber(requireField(row.choice_index, 'choice_index', context), `${context} choice_index`),
      gem_id: toNumber(requireField(row.gem_id, 'gem_id', context), `${context} gem_id`),
      amount: toNumber(requireField(row.amount, 'amount', context), `${context} amount`),
    };
  });

  return {
    key: 'gem_select_options',
    file: 'gem_select_options.json',
    data: options,
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

function buildPickaxeSlotUnlockCosts() {
  const costs = readCsv('pickaxe_slot_unlock_costs.csv').map((row, idx) => {
    const context = `pickaxe_slot_unlock_costs.csv row ${idx + 2}`;

    return {
      slot_index: toNumber(row.slot_index, `${context} slot_index`),
      unlock_cost_crystal: toNumber(row.unlock_cost_crystal, `${context} unlock_cost_crystal`),
    };
  });

  return {
    key: 'pickaxe_slot_unlock_costs',
    file: 'pickaxe_slot_unlock_costs.json',
    data: costs,
  };
}

const builders = [
  buildAds,
  buildDailyMissions,
  buildWeeklyMissions,
  buildAchievements,
  buildMinerals,
  buildMineralsInfo,
  buildInfiniteMine,
  buildMissionReroll,
  buildOfflineDefaults,
  buildMail,
  buildRarityInfo,
  buildCurrencyInfo,
  buildItemInfo,
  buildWeeklyRanking,
  buildNewUserDefaults,
  buildPickaxeLevels,
  buildPickaxeSlotUnlockCosts,
  buildUpgradeRules,
  buildGemTypes,
  buildGemGrades,
  buildGemDefinitions,
  buildGemGacha,
  buildGemSelectGroups,
  buildGemSelectOptions,
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
