const fs = require('fs');
const path = require('path');

// UTF-8 BOM과 함께 CSV 작성하는 헬퍼 함수 (Windows Excel 호환)
function writeCSV(filepath, rows) {
  const csvContent = rows.join('\n');
  // UTF-8 BOM 추가 (Excel이 UTF-8을 인식하도록)
  const bom = '\uFEFF';
  fs.writeFileSync(filepath, bom + csvContent, { encoding: 'utf8' });
  console.log(`생성 완료: ${filepath}`);
}

// 객체 배열을 CSV 행으로 변환
function arrayToCSVRows(array, headers) {
  const rows = [headers.join(',')];
  array.forEach(item => {
    const values = headers.map(header => {
      const value = item[header];
      if (value === null || value === undefined) return '';
      // 따옴표나 쉼표가 있으면 따옴표로 감싸기
      const strValue = String(value);
      if (strValue.includes(',') || strValue.includes('"') || strValue.includes('\n')) {
        return `"${strValue.replace(/"/g, '""')}"`;
      }
      return strValue;
    });
    rows.push(values.join(','));
  });
  return rows;
}

// 1. minerals.json -> minerals.csv
function convertMinerals() {
  const data = JSON.parse(fs.readFileSync('minerals.json', 'utf8'));
  const headers = ['id', 'name', 'hp', 'gold', 'recommended_level', 'biome'];
  const rows = arrayToCSVRows(data, headers);
  writeCSV('csv/minerals.csv', rows);
}

// 2. pickaxe_levels.json -> pickaxe_levels.csv
function convertPickaxeLevels() {
  const data = JSON.parse(fs.readFileSync('pickaxe_levels.json', 'utf8'));
  const headers = ['level', 'tier', 'appearance', 'attack_power', 'attack_speed', 'dps', 'cost', 'cumulative_cost'];
  const rows = arrayToCSVRows(data, headers);
  writeCSV('csv/pickaxe_levels.csv', rows);
}

// 3. daily_missions.json -> daily_missions.csv (pools를 평면화)
function convertDailyMissions() {
  const data = JSON.parse(fs.readFileSync('daily_missions.json', 'utf8'));

  // 기본 설정 CSV
  const configRows = [
    'key,value',
    `reset_time_kst,${data.reset_time_kst}`,
    `total_slots,${data.total_slots}`,
    `max_daily_assign,${data.max_daily_assign}`
  ];
  writeCSV('csv/daily_missions_config.csv', configRows);

  // 미션 풀 CSV (Easy, Medium, Hard 통합)
  const missions = [];
  Object.entries(data.pools).forEach(([difficulty, pool]) => {
    pool.forEach(mission => {
      missions.push({
        difficulty,
        id: mission.id,
        type: mission.type,
        target: mission.target,
        description: mission.description,
        reward_crystal: mission.reward_crystal
      });
    });
  });
  const missionHeaders = ['difficulty', 'id', 'type', 'target', 'description', 'reward_crystal'];
  const missionRows = arrayToCSVRows(missions, missionHeaders);
  writeCSV('csv/daily_missions.csv', missionRows);

  // 마일스톤 보너스 CSV
  const milestones = data.milestone_offline_bonus_hours.map(m => ({
    completed: m.completed,
    bonus_hours: m.bonus_hours
  }));
  const milestoneHeaders = ['completed', 'bonus_hours'];
  const milestoneRows = arrayToCSVRows(milestones, milestoneHeaders);
  writeCSV('csv/daily_missions_milestones.csv', milestoneRows);
}

// 4. ads.json -> ads.csv
function convertAds() {
  const data = JSON.parse(fs.readFileSync('ads.json', 'utf8'));

  // 기본 설정
  const configRows = [
    'key,value',
    `reset_time_kst,${data.reset_time_kst}`
  ];
  writeCSV('csv/ads_config.csv', configRows);

  // ad_types를 평면화 (parameters는 JSON 문자열로)
  const adTypes = data.ad_types.map(ad => ({
    id: ad.id,
    daily_limit: ad.daily_limit,
    effect: ad.effect,
    parameters: JSON.stringify(ad.parameters || {}),
    rewards_by_view: ad.rewards_by_view ? JSON.stringify(ad.rewards_by_view) : ''
  }));
  const headers = ['id', 'daily_limit', 'effect', 'parameters', 'rewards_by_view'];
  const rows = arrayToCSVRows(adTypes, headers);
  writeCSV('csv/ads.csv', rows);
}

// 5. mission_reroll.json -> mission_reroll.csv
function convertMissionReroll() {
  const data = JSON.parse(fs.readFileSync('mission_reroll.json', 'utf8'));
  const rows = [
    'key,value',
    `reset_time_kst,${data.reset_time_kst}`,
    `free_rerolls_per_day,${data.free_rerolls_per_day}`,
    `ad_rerolls_per_day,${data.ad_rerolls_per_day}`,
    `apply_to_slots,${data.apply_to_slots}`,
    `progress_reset_on_reroll,${data.progress_reset_on_reroll}`
  ];
  writeCSV('csv/mission_reroll.csv', rows);
}

// 6. offline_defaults.json -> offline_defaults.csv
function convertOfflineDefaults() {
  const data = JSON.parse(fs.readFileSync('offline_defaults.json', 'utf8'));
  const rows = [
    'key,value',
    `initial_offline_hours,${data.initial_offline_hours}`
  ];
  writeCSV('csv/offline_defaults.csv', rows);
}

// 7. upgrade_rules.json -> upgrade_rules.csv
function convertUpgradeRules() {
  const data = JSON.parse(fs.readFileSync('upgrade_rules.json', 'utf8'));

  // 기본 규칙
  const configRows = [
    'key,value',
    `min_rate,${data.min_rate}`,
    `bonus_rate,${data.bonus_rate}`
  ];
  writeCSV('csv/upgrade_rules_config.csv', configRows);

  // 티어별 기본 확률
  const tierRates = Object.entries(data.base_rate_by_tier).map(([tier, rate]) => ({
    tier,
    base_rate: rate
  }));
  const rateHeaders = ['tier', 'base_rate'];
  const rateRows = arrayToCSVRows(tierRates, rateHeaders);
  writeCSV('csv/upgrade_rules_tier_rates.csv', rateRows);
}

// 메인 실행
try {
  console.log('JSON -> CSV 변환 시작...\n');

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
