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

  fs.writeFileSync('minerals.json', JSON.stringify(minerals, null, 2), 'utf8');
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

  fs.writeFileSync('pickaxe_levels.json', JSON.stringify(levels, null, 2), 'utf8');
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

  const pools = { Easy: [], Medium: [], Hard: [] };
  missionRows.forEach(row => {
    const mission = {
      id: row.id,
      type: row.type,
      target: parseInt(row.target),
      description: row.description,
      reward_crystal: parseInt(row.reward_crystal)
    };
    pools[row.difficulty].push(mission);
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
    pools,
    milestone_offline_bonus_hours: milestones
  };

  fs.writeFileSync('daily_missions.json', JSON.stringify(result, null, 2), 'utf8');
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

  fs.writeFileSync('ads.json', JSON.stringify(result, null, 2), 'utf8');
  console.log('생성 완료: ads.json');
}

// 5. mission_reroll.csv -> mission_reroll.json
function convertMissionReroll() {
  const csvContent = fs.readFileSync('csv/mission_reroll.csv', 'utf8');
  const obj = keyValueCSVToObject(csvContent);

  // boolean 변환
  obj.apply_to_slots = obj.apply_to_slots === 'true';
  obj.progress_reset_on_reroll = obj.progress_reset_on_reroll === 'true';

  fs.writeFileSync('mission_reroll.json', JSON.stringify(obj, null, 2), 'utf8');
  console.log('생성 완료: mission_reroll.json');
}

// 6. offline_defaults.csv -> offline_defaults.json
function convertOfflineDefaults() {
  const csvContent = fs.readFileSync('csv/offline_defaults.csv', 'utf8');
  const obj = keyValueCSVToObject(csvContent);

  fs.writeFileSync('offline_defaults.json', JSON.stringify(obj, null, 2), 'utf8');
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

  fs.writeFileSync('upgrade_rules.json', JSON.stringify(result, null, 2), 'utf8');
  console.log('생성 완료: upgrade_rules.json');
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

  console.log('\n변환 완료!');
} catch (error) {
  console.error('변환 중 오류 발생:', error.message);
  process.exit(1);
}
