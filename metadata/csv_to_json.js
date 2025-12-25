const fs = require('fs');
const path = require('path');

// UTF-8 BOM을 제거하는 헬퍼 함수
function removeBOM(content) {
  if (content.charCodeAt(0) === 0xFEFF) {
    return content.slice(1);
  }
  return content;
}

// CSV를 파싱하는 헬퍼 함수
function parseCSV(csvContent) {
  // BOM 제거
  csvContent = removeBOM(csvContent);

  const lines = csvContent.split('\n').filter(line => line.trim());
  if (lines.length === 0) return [];

  const headers = parseCSVLine(lines[0]);
  const rows = [];

  for (let i = 1; i < lines.length; i++) {
    const values = parseCSVLine(lines[i]);
    const row = {};
    headers.forEach((header, index) => {
      row[header] = values[index] || '';
    });
    rows.push(row);
  }

  return rows;
}

// CSV 라인을 파싱 (쉼표와 따옴표 처리)
function parseCSVLine(line) {
  const result = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const char = line[i];

    if (char === '"') {
      if (inQuotes && line[i + 1] === '"') {
        current += '"';
        i++;
      } else {
        inQuotes = !inQuotes;
      }
    } else if (char === ',' && !inQuotes) {
      result.push(current);
      current = '';
    } else {
      current += char;
    }
  }
  result.push(current);

  return result.map(v => v.trim());
}

// key-value CSV를 객체로 변환
function keyValueCSVToObject(csvContent) {
  const rows = parseCSV(csvContent);
  const obj = {};
  rows.forEach(row => {
    const key = row.key;
    const value = row.value;
    // 숫자로 변환 시도
    obj[key] = isNaN(value) ? value : (value.includes('.') ? parseFloat(value) : parseInt(value));
  });
  return obj;
}

// JSON 파일 저장 헬퍼 (개행 추가)
function writeJSON(filename, data) {
  fs.writeFileSync(filename, JSON.stringify(data, null, 2) + '\n', 'utf8');
}

// 1. minerals.csv -> minerals.json
function convertMinerals() {
  const csvContent = fs.readFileSync('csv/minerals.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const minerals = rows.map(row => ({
    id: parseInt(row.id),
    name: row.name,
    hp: parseInt(row.hp),
    gold: parseInt(row.gold),
    recommended_min_DPS: parseInt(row.recommended_min_DPS),
    recommended_max_DPS: parseInt(row.recommended_max_DPS),
    biome: row.biome
  }));

  writeJSON('minerals.json', minerals);
  console.log('생성 완료: minerals.json');
}

// 2. pickaxe_levels.csv -> pickaxe_levels.json
function convertPickaxeLevels() {
  const csvContent = fs.readFileSync('csv/pickaxe_levels.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const levels = rows.map(row => ({
    level: parseInt(row.level),
    tier: row.tier,
    appearance: row.appearance,
    attack_power: parseInt(row.attack_power),
    attack_speed: parseInt(row.attack_speed),
    dps: parseInt(row.dps),
    cost: parseInt(row.cost),
    cumulative_cost: parseInt(row.cumulative_cost)
  }));

  writeJSON('pickaxe_levels.json', levels);
  console.log('생성 완료: pickaxe_levels.json');
}

// 3. daily_missions.csv -> daily_missions.json
function convertDailyMissions() {
  // 설정 파일
  const configContent = fs.readFileSync('csv/daily_missions_config.csv', 'utf8');
  const config = keyValueCSVToObject(configContent);

  // 미션 풀
  const missionsContent = fs.readFileSync('csv/daily_missions.csv', 'utf8');
  const missionRows = parseCSV(missionsContent);

  const missions = missionRows.map(row => {
    const mineralId =
      row.mineral_id === undefined || row.mineral_id === null || row.mineral_id === '' || row.mineral_id.toLowerCase?.() === 'null'
        ? null
        : parseInt(row.mineral_id, 10);

    return {
      id: parseInt(row.id, 10),
      difficulty: row.difficulty || row.pool,
      type: row.type,
      target: parseInt(row.target, 10),
      mineral_id: mineralId,
      description: row.description,
      reward_crystal: parseInt(row.reward_crystal, 10)
    };
  });

  // 마일스톤
  const milestonesContent = fs.readFileSync('csv/daily_missions_milestones.csv', 'utf8');
  const milestoneRows = parseCSV(milestonesContent);
  const milestones = milestoneRows.map(row => ({
    completed: parseInt(row.completed),
    bonus_hours: parseInt(row.bonus_hours)
  }));

  const result = {
    reset_time_kst: config.reset_time_kst,
    total_slots: config.total_slots,
    max_daily_assign: config.max_daily_assign,
    missions,
    milestone_offline_bonus_hours: milestones
  };

  writeJSON('daily_missions.json', result);
  console.log('생성 완료: daily_missions.json');
}

// 4. ads.csv -> ads.json
function convertAds() {
  // 설정
  const configContent = fs.readFileSync('csv/ads_config.csv', 'utf8');
  const config = keyValueCSVToObject(configContent);

  // ad_types
  const adsContent = fs.readFileSync('csv/ads.csv', 'utf8');
  const adRows = parseCSV(adsContent);

  const adTypes = adRows.map(row => {
    const ad = {
      id: row.id,
      daily_limit: parseInt(row.daily_limit),
      effect: row.effect,
      parameters: JSON.parse(row.parameters || '{}')
    };
    if (row.rewards_by_view) {
      ad.rewards_by_view = JSON.parse(row.rewards_by_view);
    }
    return ad;
  });

  const result = {
    reset_time_kst: config.reset_time_kst,
    ad_types: adTypes
  };

  writeJSON('ads.json', result);
  console.log('생성 완료: ads.json');
}

// 5. mission_reroll.csv -> mission_reroll.json
function convertMissionReroll() {
  const csvContent = fs.readFileSync('csv/mission_reroll.csv', 'utf8');
  const obj = keyValueCSVToObject(csvContent);

  // boolean 변환
  obj.apply_to_slots = obj.apply_to_slots === 'true';
  obj.progress_reset_on_reroll = obj.progress_reset_on_reroll === 'true';

  writeJSON('mission_reroll.json', obj);
  console.log('생성 완료: mission_reroll.json');
}

// 6. offline_defaults.csv -> offline_defaults.json
function convertOfflineDefaults() {
  const csvContent = fs.readFileSync('csv/offline_defaults.csv', 'utf8');
  const obj = keyValueCSVToObject(csvContent);

  writeJSON('offline_defaults.json', obj);
  console.log('생성 완료: offline_defaults.json');
}

// 7. upgrade_rules.csv -> upgrade_rules.json
function convertUpgradeRules() {
  // 기본 설정
  const configContent = fs.readFileSync('csv/upgrade_rules_config.csv', 'utf8');
  const config = keyValueCSVToObject(configContent);

  // 티어별 확률
  const ratesContent = fs.readFileSync('csv/upgrade_rules_tier_rates.csv', 'utf8');
  const rateRows = parseCSV(ratesContent);

  const baseRateByTier = {};
  rateRows.forEach(row => {
    baseRateByTier[row.tier] = parseInt(row.base_rate);
  });

  const result = {
    min_rate: config.min_rate,
    bonus_rate: config.bonus_rate,
    base_rate_by_tier: baseRateByTier
  };

  writeJSON('upgrade_rules.json', result);
  console.log('생성 완료: upgrade_rules.json');
}

// 8. gem_types.csv -> gem_types.json
function convertGemTypes() {
  const csvContent = fs.readFileSync('csv/gem_types.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const types = rows.map(row => ({
    id: parseInt(row.id),
    type: row.type,
    display_name: row.display_name,
    description: row.description,
    stat_key: row.stat_key
  }));

  writeJSON('gem_types.json', types);
  console.log('생성 완료: gem_types.json');
}

// 9. gem_grades.csv -> gem_grades.json
function convertGemGrades() {
  const csvContent = fs.readFileSync('csv/gem_grades.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const grades = rows.map(row => ({
    id: parseInt(row.id),
    grade: row.grade,
    display_name: row.display_name
  }));

  writeJSON('gem_grades.json', grades);
  console.log('생성 완료: gem_grades.json');
}

// 10. gem_definitions.csv -> gem_definitions.json
function convertGemDefinitions() {
  const csvContent = fs.readFileSync('csv/gem_definitions.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const definitions = rows.map(row => ({
    gem_id: parseInt(row.gem_id),
    grade_id: parseInt(row.grade_id),
    type_id: parseInt(row.type_id),
    name: row.name,
    icon: row.icon,
    stat_multiplier: parseInt(row.stat_multiplier)
  }));

  writeJSON('gem_definitions.json', definitions);
  console.log('생성 완료: gem_definitions.json');
}

// 11. gem_gacha (costs + rates) -> gem_gacha.json
function convertGemGacha() {
  // 비용 설정
  const costsContent = fs.readFileSync('csv/gem_gacha_costs.csv', 'utf8');
  const costs = keyValueCSVToObject(costsContent);

  // 확률 설정
  const ratesContent = fs.readFileSync('csv/gem_gacha_rates.csv', 'utf8');
  const rateRows = parseCSV(ratesContent);

  const rates = rateRows.map(row => ({
    grade_id: parseInt(row.grade_id),
    rate_percent: parseInt(row.rate_percent)
  }));

  const result = {
    single_pull_cost: costs.single_pull_cost,
    multi_pull_cost: costs.multi_pull_cost,
    multi_pull_count: costs.multi_pull_count,
    grade_rates: rates
  };

  writeJSON('gem_gacha.json', result);
  console.log('생성 완료: gem_gacha.json');
}

// 12. gem_conversion_costs.csv -> gem_conversion.json
function convertGemConversion() {
  const csvContent = fs.readFileSync('csv/gem_conversion_costs.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const conversion = rows.map(row => ({
    grade_id: parseInt(row.grade_id),
    random_cost: parseInt(row.random_cost),
    fixed_cost: parseInt(row.fixed_cost)
  }));

  writeJSON('gem_conversion.json', conversion);
  console.log('생성 완료: gem_conversion.json');
}

// 13. gem_discard_rewards.csv -> gem_discard.json
function convertGemDiscard() {
  const csvContent = fs.readFileSync('csv/gem_discard_rewards.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const discard = rows.map(row => ({
    grade_id: parseInt(row.grade_id),
    crystal_reward: parseInt(row.crystal_reward)
  }));

  writeJSON('gem_discard.json', discard);
  console.log('생성 완료: gem_discard.json');
}

// 14. gem_inventory_config.csv -> gem_inventory.json
function convertGemInventory() {
  const csvContent = fs.readFileSync('csv/gem_inventory_config.csv', 'utf8');
  const config = keyValueCSVToObject(csvContent);

  writeJSON('gem_inventory.json', config);
  console.log('생성 완료: gem_inventory.json');
}

// 15. gem_synthesis_rules.csv -> gem_synthesis_rules.json
function convertGemSynthesisRules() {
  const csvContent = fs.readFileSync('csv/gem_synthesis_rules.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const rules = rows.map(row => ({
    from_grade: row.from_grade,
    to_grade: row.to_grade,
    success_rate_percent: parseInt(row.success_rate_percent)
  }));

  writeJSON('gem_synthesis_rules.json', rules);
  console.log('생성 완료: gem_synthesis_rules.json');
}

// 16. gem_slot_unlock_costs.csv -> gem_slot_unlock_costs.json
function convertGemSlotUnlockCosts() {
  const csvContent = fs.readFileSync('csv/gem_slot_unlock_costs.csv', 'utf8');
  const rows = parseCSV(csvContent);

  const costs = rows.map(row => ({
    slot_index: parseInt(row.slot_index),
    unlock_cost_crystal: parseInt(row.unlock_cost_crystal)
  }));

  writeJSON('gem_slot_unlock_costs.json', costs);
  console.log('생성 완료: gem_slot_unlock_costs.json');
}

// 메인 실행
try {
  console.log('CSV -> JSON 변환 시작...\n');

  convertMinerals();
  convertPickaxeLevels();
  convertDailyMissions();
  convertAds();
  convertMissionReroll();
  convertOfflineDefaults();
  convertUpgradeRules();
  convertGemTypes();
  convertGemGrades();
  convertGemDefinitions();
  convertGemGacha();
  convertGemConversion();
  convertGemDiscard();
  convertGemInventory();
  convertGemSynthesisRules();
  convertGemSlotUnlockCosts();

  console.log('\n변환 완료!');
} catch (error) {
  console.error('변환 중 오류 발생:', error.message);
  process.exit(1);
}
