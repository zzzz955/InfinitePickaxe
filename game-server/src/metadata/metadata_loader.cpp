#include "metadata_loader.h"

#include <fstream>

#include <nlohmann/json.hpp>

#include <sstream>

bool MetadataLoader::load(const std::string &base_path)
{

    try
    {

        // 기존 데이터 클리어

        pickaxe_levels_.clear();

        minerals_.clear();

        minerals_info_.clear();

        infinite_mine_floor_index_by_floor_.clear();

        infinite_mine_floors_.clear();

        missions_.clear();

        weekly_missions_.clear();

        achievements_.clear();

        milestone_bonuses_.clear();

        weekly_milestone_rewards_.clear();

        ad_types_.clear();

        ad_types_by_id_.clear();

        daily_missions_config_ = DailyMissionConfig{};

        weekly_missions_config_ = WeeklyMissionConfig{};

        mission_reroll_ = MissionRerollMeta{};

        offline_defaults_ = OfflineDefaults{};

        new_user_defaults_ = NewUserDefaults{};

        mail_config_ = MailConfig{};

        weekly_ranking_config_ = WeeklyRankingConfig{};

        item_infos_.clear();

        item_info_by_id_.clear();

        shop_products_.clear();

        shop_products_by_id_.clear();

        reward_packages_.clear();
        reward_package_entries_.clear();
        reward_packages_by_id_.clear();
        reward_package_entries_by_package_.clear();
        reward_package_entries_by_key_.clear();

        gem_types_.clear();

        gem_grades_.clear();

        gem_definitions_.clear();

        gem_synthesis_rules_.clear();

        gem_conversion_costs_.clear();

        gem_discard_rewards_.clear();

        gem_slot_unlock_costs_.clear();

        pickaxe_slot_unlock_costs_.clear();

        gem_types_by_id_.clear();

        gem_grades_by_id_.clear();

        gem_definitions_by_id_.clear();

        achievements_by_id_.clear();

        mail_templates_by_id_.clear();

        gem_gacha_ = GemGachaMeta{};

        gem_inventory_config_ = GemInventoryConfig{};

        item_inventory_config_ = ItemInventoryConfig{};

        infinite_mine_config_ = InfiniteMineConfig{};

        // meta_bundle.json 읽기 (번들 기반 파싱)

        std::ifstream f(base_path + "/meta_bundle.json");

        if (!f.good())
        {

            return false;
        }

        nlohmann::json bundle;

        f >> bundle;

        // pickaxe_levels

        {

            nlohmann::json j = bundle["pickaxe_levels"];

            for (auto &e : j)
            {

                PickaxeLevel pl;

                pl.level = e["level"].get<uint32_t>();

                if (e["tier"].is_string())
                {

                    std::string t = e["tier"].get<std::string>();

                    if (!t.empty() && (t[0] == 'T' || t[0] == 't'))
                    {

                        pl.tier = static_cast<uint32_t>(std::stoul(t.substr(1)));
                    }
                    else
                    {

                        pl.tier = e.value("tier_num", 1);
                    }
                }
                else
                {

                    pl.tier = e.value("tier", 1);
                }

                pl.attack_power = e["attack_power"].get<uint64_t>();

                pl.attack_speed = e["attack_speed"].get<double>();

                pl.dps = e["dps"].get<uint64_t>();

                pl.cost = e["cost"].get<uint64_t>();

                pickaxe_levels_[pl.level] = pl;
            }
        }

        // minerals

        {

            nlohmann::json j = bundle["minerals"];

            if (j.is_array())
            {

                for (auto &e : j)
                {

                    MineralMeta mm;

                    mm.id = e.value("id", 0);

                    mm.name = e.value("name", "");

                    mm.hp = e.value<uint64_t>("hp", 0);

                    mm.reward = e.value<uint64_t>("reward", e.value<uint64_t>("gold", 0));

                    mm.respawn_time = e.value<uint32_t>("respawn_time", 5);

                    mm.recommended_min_dps = e.value<uint64_t>("recommended_min_DPS", 0);

                    mm.recommended_max_dps = e.value<uint64_t>("recommended_max_DPS", 0);

                    minerals_[mm.id] = mm;
                }
            }
        }

        // minerals_info

        if (bundle.contains("minerals_info"))
        {

            nlohmann::json j = bundle["minerals_info"];

            if (j.is_array())
            {

                for (auto &e : j)
                {

                    MineralInfoMeta mi;

                    mi.id = e.value("id", 0);

                    mi.name = e.value("name", "");

                    mi.sprite_key = e.value("sprite_key", "");

                    if (mi.id > 0)
                    {

                        minerals_info_[mi.id] = mi;
                    }
                }
            }
        }

        // infinite_mine

        if (bundle.contains("infinite_mine"))
        {

            nlohmann::json j = bundle["infinite_mine"];

            if (j.is_object())
            {

                infinite_mine_config_.reset_time_kst = j.value("reset_time_kst", "");

                infinite_mine_config_.time_limit_sec = j.value("time_limit_sec", infinite_mine_config_.time_limit_sec);

                infinite_mine_config_.max_floor = j.value("max_floor", infinite_mine_config_.max_floor);

                infinite_mine_config_.auto_reward_divisor = j.value("auto_reward_divisor", infinite_mine_config_.auto_reward_divisor);
            }

            if (j.contains("floors") && j["floors"].is_array())
            {

                for (auto &e : j["floors"])
                {

                    InfiniteMineFloorMeta floor;

                    floor.floor = e.value("floor", 0);

                    floor.mineral_info_id = e.value("mineral_info_id", 0);

                    floor.hp = e.value<uint64_t>("hp", 0);

                    floor.reward_gold = e.value<uint64_t>("reward_gold", 0);

                    floor.reward_crystal = e.value<uint64_t>("reward_crystal", 0);

                    floor.biome_id = e.value("biome_id", 0);

                    if (floor.floor > 0)
                    {

                        infinite_mine_floor_index_by_floor_[floor.floor] = infinite_mine_floors_.size();

                        infinite_mine_floors_.push_back(floor);
                    }
                }
            }
        }

        // daily_missions

        {

            nlohmann::json j = bundle["daily_missions"];

            if (j.is_object())
            {

                daily_missions_config_.total_slots = j.value("total_slots", daily_missions_config_.total_slots);

                daily_missions_config_.max_daily_assign = j.value("max_daily_assign", daily_missions_config_.max_daily_assign);
            }

            uint32_t idx = 0;

            auto parse_mission = [&](const nlohmann::json &e)
            {
                MissionMeta m;

                m.index = idx++;

                m.id = e.value("id", m.index);

                m.type = e.value("type", "");

                m.target = e.value("target", 0);

                m.reward_crystal = e.value("reward_crystal", 0);

                m.description = e.value("description", "");

                m.difficulty = e.value("difficulty", "");

                if (e.contains("mineral_id") && !e["mineral_id"].is_null())
                {

                    m.mineral_id = e.value("mineral_id", 0);
                }

                missions_.push_back(m);
            };

            if (j.is_array())
            {

                for (auto &e : j)
                {

                    parse_mission(e);
                }
            }
            else if (j.contains("missions") && j["missions"].is_array())
            {

                for (auto &e : j["missions"])
                {

                    parse_mission(e);
                }
            }
            else if (j.contains("pools"))
            {

                for (auto &pool : j["pools"].items())
                {

                    for (auto &e : pool.value())
                    {

                        parse_mission(e);
                    }
                }
            }

            if (j.contains("milestone_offline_bonus_hours"))
            {

                for (auto &e : j["milestone_offline_bonus_hours"])
                {

                    MilestoneBonus b;

                    b.completed = e.value("completed", 0);

                    b.bonus_hours = e.value("bonus_hours", 0);

                    b.reward_crystal = e.value("reward_crystal", 0);

                    if (b.completed > 0 && (b.bonus_hours > 0 || b.reward_crystal > 0))
                    {

                        milestone_bonuses_.push_back(b);
                    }
                }
            }
        }

        // weekly_missions

        if (bundle.contains("weekly_missions"))
        {

            nlohmann::json j = bundle["weekly_missions"];

            if (j.is_object())
            {

                weekly_missions_config_.reset_weekday_kst = j.value("reset_weekday_kst", "");

                weekly_missions_config_.reset_time_kst = j.value("reset_time_kst", "");
            }

            uint32_t idx = 0;

            auto parse_weekly_mission = [&](const nlohmann::json &e)
            {
                MissionMeta m;

                m.index = idx++;

                m.id = e.value("id", m.index);

                m.type = e.value("type", "");

                m.target = e.value("target", 0);

                m.reward_crystal = e.value("reward_crystal", 0);

                m.title = e.value("title", "");

                m.description = e.value("description", "");

                if (e.contains("mineral_id") && !e["mineral_id"].is_null())
                {

                    m.mineral_id = e.value("mineral_id", 0);
                }

                weekly_missions_.push_back(m);
            };

            if (j.is_array())
            {

                for (auto &e : j)
                {

                    parse_weekly_mission(e);
                }
            }
            else if (j.contains("missions") && j["missions"].is_array())
            {

                for (auto &e : j["missions"])
                {

                    parse_weekly_mission(e);
                }
            }

            if (j.contains("milestone_rewards"))
            {

                for (auto &e : j["milestone_rewards"])
                {

                    WeeklyMilestoneReward reward;

                    reward.completed = e.value("completed", 0);

                    reward.reward_crystal = e.value("reward_crystal", 0);

                    if (reward.completed > 0 && reward.reward_crystal > 0)
                    {

                        weekly_milestone_rewards_.push_back(reward);
                    }
                }
            }
        }

        // mail

        if (bundle.contains("mail"))
        {

            nlohmann::json j = bundle["mail"];

            if (j.is_object())
            {

                mail_config_.max_mail_count = j.value("max_mail_count", mail_config_.max_mail_count);

                mail_config_.expire_days = j.value("expire_days", mail_config_.expire_days);

                mail_config_.claim_all_limit = j.value("claim_all_limit", mail_config_.claim_all_limit);

                mail_config_.default_list_limit = j.value("default_list_limit", mail_config_.default_list_limit);

                if (j.contains("templates") && j["templates"].is_array())
                {

                    for (auto &e : j["templates"])
                    {

                        MailTemplateMeta tmpl;

                        tmpl.template_id = e.value("template_id", 0);

                        tmpl.mail_type = e.value("mail_type", "");

                        tmpl.title = e.value("title", "");

                        tmpl.body = e.value("body", "");

                        tmpl.sender = e.value("sender", "");

                        tmpl.default_expire_days = e.value("default_expire_days", mail_config_.expire_days);

                        if (tmpl.template_id > 0)
                        {

                            mail_templates_by_id_[tmpl.template_id] = mail_config_.templates.size();

                            mail_config_.templates.push_back(tmpl);
                        }
                    }
                }
            }
        }

        // item_info

        if (bundle.contains("item_info") && bundle["item_info"].is_array())
        {

            const auto &j = bundle["item_info"];

            item_infos_.reserve(j.size());

            for (auto &e : j)
            {

                ItemInfoMeta item;

                item.item_id = e.value("item_id", 0);

                item.item_type = e.value("item_type", "");

                item.sprite_key = e.value("sprite_key", "");

                item.rarity_id = e.value("rarity_id", 0);

                item.display_name = e.value("display_name", "");

                item.stackable = e.value("stackable", true);

                item.max_stack = e.value("max_stack", 0);

                item.use_action_type = e.value("use_action_type", "");

                if (e.contains("use_action_ref_id") && !e["use_action_ref_id"].is_null())
                {

                    item.use_action_ref_id = e.value("use_action_ref_id", 0);
                }

                if (item.item_id > 0)
                {

                    item_info_by_id_[item.item_id] = item_infos_.size();

                    item_infos_.push_back(item);
                }
            }
        }

        if (bundle.contains("shop_products") && bundle["shop_products"].is_array())
        {
            const auto& j = bundle["shop_products"];
            shop_products_.reserve(j.size());

            for (auto& e : j)
            {
                ShopProductMeta product;
                product.product_id = e.value("product_id", 0);
                product.tab_key = e.value("tab_key", "");
                product.item_id = e.value("item_id", 0);
                product.item_count = e.value("item_count", 1);
                product.price_currency = e.value("price_currency", "");
                product.sort_order = e.value("sort_order", 0);
                product.is_active = e.value("is_active", true);
                product.display_sprite_key = e.value("display_sprite_key", "");

                if (e.contains("price_amount") && !e["price_amount"].is_null())
                {
                    product.price_amount = e.value<uint64_t>("price_amount", 0);
                }

                if (product.product_id > 0)
                {
                    shop_products_by_id_[product.product_id] = shop_products_.size();
                    shop_products_.push_back(product);
                }
            }
        }

        // reward_packages

        if (bundle.contains("reward_packages") && bundle["reward_packages"].is_array())
        {
            const auto& j = bundle["reward_packages"];
            reward_packages_.reserve(j.size());

            for (auto& e : j)
            {
                RewardPackageMeta pkg;
                pkg.package_id = e.value("package_id", 0);
                pkg.mode = e.value("mode", "");
                pkg.roll_count = e.value("roll_count", 1);
                pkg.description = e.value("description", "");

                if (pkg.package_id > 0)
                {
                    reward_packages_by_id_[pkg.package_id] = reward_packages_.size();
                    reward_packages_.push_back(pkg);
                }
            }
        }

        // reward_package_entries

        if (bundle.contains("reward_package_entries") && bundle["reward_package_entries"].is_array())
        {
            const auto& j = bundle["reward_package_entries"];
            reward_package_entries_.reserve(j.size());

            for (auto& e : j)
            {
                RewardPackageEntry entry;
                entry.package_id = e.value("package_id", 0);
                entry.entry_id = e.value("entry_id", 0);
                entry.reward_type = e.value("reward_type", "");
                entry.reward_ref_id = e.value("reward_ref_id", 0);
                entry.amount = e.value<uint64_t>("amount", 0);
                entry.weight = e.value("weight", 0);
                entry.group_id = e.value("group_id", 0);

                if (entry.package_id > 0 && entry.entry_id > 0 && entry.amount > 0)
                {
                    reward_package_entries_by_package_[entry.package_id].push_back(entry);
                    uint64_t key = (static_cast<uint64_t>(entry.package_id) << 32) | entry.entry_id;
                    reward_package_entries_by_key_[key] = reward_package_entries_.size();
                    reward_package_entries_.push_back(entry);
                }
            }
        }

        // item_inventory

        if (bundle.contains("item_inventory"))
        {

            nlohmann::json j = bundle["item_inventory"];

            item_inventory_config_.base_capacity = j.value("base_capacity", item_inventory_config_.base_capacity);

            item_inventory_config_.max_capacity = j.value("max_capacity", item_inventory_config_.max_capacity);

            item_inventory_config_.expand_step = j.value("expand_step", item_inventory_config_.expand_step);

            item_inventory_config_.expand_cost = j.value("expand_cost", item_inventory_config_.expand_cost);
        }

        // weekly_ranking

        if (bundle.contains("weekly_ranking"))
        {

            nlohmann::json j = bundle["weekly_ranking"];

            if (j.is_object())
            {

                weekly_ranking_config_.reset_weekday_kst = j.value("reset_weekday_kst", "");

                weekly_ranking_config_.reset_time_kst = j.value("reset_time_kst", "");

                if (j.contains("rewards") && j["rewards"].is_array())
                {

                    for (auto &e : j["rewards"])
                    {

                        WeeklyRankingReward reward;

                        reward.rank_min = e.value("rank_min", 0);

                        reward.rank_max = e.value("rank_max", 0);

                        reward.reward_index = e.value("reward_index", 0);

                        reward.reward_type = e.value("reward_type", "");

                        reward.reward_key = e.value("reward_key", "");

                        reward.amount = e.value<uint64_t>("amount", 0);

                        reward.template_id = e.value("template_id", 0);

                        if (reward.rank_min > 0 && reward.rank_max >= reward.rank_min)
                        {

                            weekly_ranking_config_.rewards.push_back(reward);
                        }
                    }
                }
            }
        }

        // achievements

        if (bundle.contains("achievements") && bundle["achievements"].is_array())
        {

            const auto &j = bundle["achievements"];

            achievements_.reserve(j.size());

            for (auto &e : j)
            {

                AchievementMeta a;

                a.id = e.value("achievement_id", e.value("id", 0));

                a.chain_id = e.value("chain_id", 0);

                a.step_index = e.value("step_index", 0);

                a.type = e.value("type", "");

                a.target = e.value<uint64_t>("target", 0);

                a.title = e.value("title", "");

                a.description = e.value("description", "");

                a.reward_crystal = e.value("reward_crystal", 0);

                a.reward_gold = e.value<uint64_t>("reward_gold", 0);

                achievements_.push_back(a);

                if (a.id > 0)
                {

                    achievements_by_id_[a.id] = achievements_.size() - 1;
                }
            }
        }

        auto to_rate = [](const nlohmann::json &v, double fallback)
        {
            if (v.is_number_integer())
            {

                return static_cast<double>(v.get<int64_t>()) / 10000.0; // basis 10000
            }

            if (v.is_number())
            {

                return v.get<double>();
            }

            return fallback;
        };

        // upgrade_rules

        {

            if (bundle.contains("upgrade_rules"))
            {

                nlohmann::json j = bundle["upgrade_rules"];

                upgrade_rules_.min_rate = to_rate(j["min_rate"], 0.3);

                upgrade_rules_.bonus_rate = to_rate(j["bonus_rate"], 0.1);

                if (j.contains("base_rate_by_tier"))
                {

                    for (auto &item : j["base_rate_by_tier"].items())
                    {

                        uint32_t tier = static_cast<uint32_t>(std::stoul(item.key()));

                        double rate = to_rate(item.value(), 1.0);

                        upgrade_rules_.base_rate_by_tier[tier] = rate;
                    }
                }
            }
            else
            {

                upgrade_rules_.base_rate_by_tier[1] = 1.0;

                upgrade_rules_.base_rate_by_tier[2] = 0.95;

                upgrade_rules_.base_rate_by_tier[3] = 0.90;

                upgrade_rules_.base_rate_by_tier[4] = 0.85;
            }
        }

        // ads

        {

            if (bundle.contains("ads"))
            {

                nlohmann::json j = bundle["ads"];

                if (j.contains("ad_types") && j["ad_types"].is_array())
                {

                    for (auto &e : j["ad_types"])
                    {

                        AdTypeMeta ad;

                        ad.id = e.value("id", "");

                        ad.effect = e.value("effect", "");

                        ad.daily_limit = e.value("daily_limit", 0);

                        if (e.contains("rewards_by_view"))
                        {

                            for (auto &r : e["rewards_by_view"])
                            {

                                ad.rewards_by_view.push_back(r.get<uint32_t>());
                            }
                        }

                        if (e.contains("parameters"))
                        {

                            const auto &p = e["parameters"];

                            if (p.contains("cost_multiplier"))
                            {

                                ad.cost_multiplier = p.value("cost_multiplier", 100);
                            }

                            if (p.contains("apply_to_slots"))
                            {

                                if (p["apply_to_slots"].is_string())
                                {

                                    std::string v = p["apply_to_slots"].get<std::string>();

                                    ad.apply_to_all_slots = (v == "all");
                                }
                                else
                                {

                                    ad.apply_to_all_slots = p.value("apply_to_slots", true);
                                }
                            }

                            if (p.contains("progress_reset_on_reroll"))
                            {

                                ad.progress_reset_on_reroll = p.value("progress_reset_on_reroll", true);
                            }
                        }

                        if (!ad.id.empty())
                        {

                            ad_types_.push_back(ad);

                            ad_types_by_id_[ad.id] = ad;
                        }
                    }
                }
            }
        }

        // offline_defaults

        {

            if (bundle.contains("offline_defaults"))
            {

                nlohmann::json j = bundle["offline_defaults"];

                uint32_t hours = j.value("initial_offline_hours", 0);

                offline_defaults_.initial_offline_seconds = hours * 3600;
            }
            else
            {

                offline_defaults_.initial_offline_seconds = 0;
            }
        }

        // new_user_defaults

        {

            if (bundle.contains("new_user_defaults"))
            {

                nlohmann::json j = bundle["new_user_defaults"];

                new_user_defaults_.initial_gold = j.value("initial_gold", 0);

                new_user_defaults_.initial_crystal = j.value("initial_crystal", 0);

                new_user_defaults_.initial_pickaxe_level = j.value("initial_pickaxe_level", 0);

                new_user_defaults_.initial_critical_hit_percent = j.value("initial_critical_hit_percent", 500);

                new_user_defaults_.initial_critical_damage = j.value("initial_critical_damage", 15000);

                new_user_defaults_.initial_pity_bonus = j.value("initial_pity_bonus", 0);

                if (j.contains("initial_unlocked_pickaxe_slots") && j["initial_unlocked_pickaxe_slots"].is_array())
                {

                    new_user_defaults_.initial_unlocked_pickaxe_slots.clear();

                    for (auto &e : j["initial_unlocked_pickaxe_slots"])
                    {

                        new_user_defaults_.initial_unlocked_pickaxe_slots.push_back(e.get<uint32_t>());
                    }
                }

                if (j.contains("initial_unlocked_gem_slots") && j["initial_unlocked_gem_slots"].is_array())
                {

                    new_user_defaults_.initial_unlocked_gem_slots.clear();

                    for (auto &e : j["initial_unlocked_gem_slots"])
                    {

                        new_user_defaults_.initial_unlocked_gem_slots.push_back(e.get<uint32_t>());
                    }
                }
            }

            if (new_user_defaults_.initial_unlocked_pickaxe_slots.empty())
            {

                new_user_defaults_.initial_unlocked_pickaxe_slots.push_back(0);
            }

            if (new_user_defaults_.initial_unlocked_gem_slots.empty())
            {

                new_user_defaults_.initial_unlocked_gem_slots.push_back(0);
            }
        }

        // mission_reroll

        {

            if (bundle.contains("mission_reroll"))
            {

                nlohmann::json j = bundle["mission_reroll"];

                mission_reroll_.free_rerolls_per_day = j.value("free_rerolls_per_day", 0);

                mission_reroll_.ad_rerolls_per_day = j.value("ad_rerolls_per_day", 0);

                mission_reroll_.apply_to_all_slots = j.value("apply_to_slots", true);

                mission_reroll_.progress_reset_on_reroll = j.value("progress_reset_on_reroll", true);
            }
            else
            {

                mission_reroll_.free_rerolls_per_day = 2;

                mission_reroll_.ad_rerolls_per_day = 3;

                mission_reroll_.apply_to_all_slots = true;

                mission_reroll_.progress_reset_on_reroll = true;
            }
        }

        // 보석 시스템 메타데이터 파싱

        // gem_types

        {

            if (bundle.contains("gem_types"))
            {

                nlohmann::json j = bundle["gem_types"];

                for (auto &e : j)
                {

                    GemTypeMeta gt;

                    gt.id = e.value("id", 0);

                    gt.type = e.value("type", "");

                    gt.display_name = e.value("display_name", "");

                    gt.description = e.value("description", "");

                    gt.stat_key = e.value("stat_key", "");

                    gem_types_.push_back(gt);

                    gem_types_by_id_[gt.id] = gt;
                }
            }
        }

        // gem_grades

        {

            if (bundle.contains("gem_grades"))
            {

                nlohmann::json j = bundle["gem_grades"];

                for (auto &e : j)
                {

                    GemGradeMeta gg;

                    gg.id = e.value("id", 0);

                    gg.grade = e.value("grade", "");

                    gg.display_name = e.value("display_name", "");

                    gem_grades_.push_back(gg);

                    gem_grades_by_id_[gg.id] = gg;
                }
            }
        }

        // gem_definitions

        {

            if (bundle.contains("gem_definitions"))
            {

                nlohmann::json j = bundle["gem_definitions"];

                for (auto &e : j)
                {

                    GemDefinition gd;

                    gd.gem_id = e.value("gem_id", 0);

                    gd.grade_id = e.value("grade_id", 0);

                    gd.type_id = e.value("type_id", 0);

                    gd.name = e.value("name", "");

                    gd.icon = e.value("icon", "");

                    gd.stat_multiplier = e.value("stat_multiplier", 0);

                    gem_definitions_.push_back(gd);

                    gem_definitions_by_id_[gd.gem_id] = gd;
                }
            }
        }

        // gem_gacha

        {

            if (bundle.contains("gem_gacha"))
            {

                nlohmann::json j = bundle["gem_gacha"];

                gem_gacha_.single_pull_cost = j.value("single_pull_cost", 0);

                gem_gacha_.multi_pull_cost = j.value("multi_pull_cost", 0);

                gem_gacha_.multi_pull_count = j.value("multi_pull_count", 0);

                if (j.contains("grade_rates") && j["grade_rates"].is_array())
                {

                    for (auto &e : j["grade_rates"])
                    {

                        GemGradeRate rate;

                        rate.grade_id = e.value("grade_id", 0);

                        rate.rate_percent = e.value("rate_percent", 0);

                        gem_gacha_.grade_rates.push_back(rate);
                    }
                }
            }
        }

        // gem_synthesis_rules

        {

            if (bundle.contains("gem_synthesis_rules"))
            {

                nlohmann::json j = bundle["gem_synthesis_rules"];

                for (auto &e : j)
                {

                    GemSynthesisRule rule;

                    rule.from_grade = e.value("from_grade", "");

                    rule.to_grade = e.value("to_grade", "");

                    rule.success_rate_percent = e.value("success_rate_percent", 0);

                    gem_synthesis_rules_.push_back(rule);
                }
            }
        }

        // gem_conversion

        {

            if (bundle.contains("gem_conversion"))
            {

                nlohmann::json j = bundle["gem_conversion"];

                for (auto &e : j)
                {

                    GemConversionCost cost;

                    cost.grade_id = e.value("grade_id", 0);

                    cost.random_cost = e.value("random_cost", 0);

                    cost.fixed_cost = e.value("fixed_cost", 0);

                    gem_conversion_costs_.push_back(cost);
                }
            }
        }

        // gem_discard

        {

            if (bundle.contains("gem_discard"))
            {

                nlohmann::json j = bundle["gem_discard"];

                for (auto &e : j)
                {

                    GemDiscardReward reward;

                    reward.grade_id = e.value("grade_id", 0);

                    reward.crystal_reward = e.value("crystal_reward", 0);

                    gem_discard_rewards_.push_back(reward);
                }
            }
        }

        // gem_inventory

        {

            if (bundle.contains("gem_inventory"))
            {

                nlohmann::json j = bundle["gem_inventory"];

                gem_inventory_config_.base_capacity = j.value("base_capacity", 48);

                gem_inventory_config_.max_capacity = j.value("max_capacity", 128);

                gem_inventory_config_.expand_step = j.value("expand_step", 8);

                gem_inventory_config_.expand_cost = j.value("expand_cost", 200);
            }
        }

        // 곡괭이 슬롯 해금 비용

        {

            if (bundle.contains("pickaxe_slot_unlock_costs"))
            {

                nlohmann::json j = bundle["pickaxe_slot_unlock_costs"];

                for (auto &e : j)
                {

                    PickaxeSlotUnlockCost cost;

                    cost.slot_index = e.value("slot_index", 0);

                    cost.unlock_cost_crystal = e.value("unlock_cost_crystal", 0);

                    pickaxe_slot_unlock_costs_.push_back(cost);
                }
            }

            if (pickaxe_slot_unlock_costs_.empty())
            {

                pickaxe_slot_unlock_costs_.push_back({0, 0});

                pickaxe_slot_unlock_costs_.push_back({1, 400});

                pickaxe_slot_unlock_costs_.push_back({2, 2000});

                pickaxe_slot_unlock_costs_.push_back({3, 4000});
            }
        }

        // gem_slot_unlock_costs

        {

            if (bundle.contains("gem_slot_unlock_costs"))
            {

                nlohmann::json j = bundle["gem_slot_unlock_costs"];

                for (auto &e : j)
                {

                    GemSlotUnlockCost cost;

                    cost.slot_index = e.value("slot_index", 0);

                    cost.unlock_cost_crystal = e.value("unlock_cost_crystal", 0);

                    gem_slot_unlock_costs_.push_back(cost);
                }
            }
        }

        return true;
    }
    catch (...)
    {

        return false;
    }
}

const PickaxeLevel *MetadataLoader::pickaxe_level(uint32_t level) const
{

    auto it = pickaxe_levels_.find(level);

    if (it == pickaxe_levels_.end())
        return nullptr;

    return &it->second;
}

const MineralMeta *MetadataLoader::mineral(uint32_t id) const
{

    auto it = minerals_.find(id);

    if (it == minerals_.end())
        return nullptr;

    return &it->second;
}

const MineralInfoMeta *MetadataLoader::mineral_info(uint32_t id) const
{

    auto it = minerals_info_.find(id);

    if (it == minerals_info_.end())
        return nullptr;

    return &it->second;
}

const InfiniteMineFloorMeta *MetadataLoader::infinite_mine_floor(uint32_t floor) const
{

    auto it = infinite_mine_floor_index_by_floor_.find(floor);

    if (it == infinite_mine_floor_index_by_floor_.end())
        return nullptr;

    return &infinite_mine_floors_[it->second];
}

const AdTypeMeta *MetadataLoader::ad_meta(const std::string &id) const
{

    auto it = ad_types_by_id_.find(id);

    if (it == ad_types_by_id_.end())
        return nullptr;

    return &it->second;
}

const MailTemplateMeta *MetadataLoader::mail_template(uint32_t template_id) const
{

    auto it = mail_templates_by_id_.find(template_id);

    if (it == mail_templates_by_id_.end())
        return nullptr;

    return &mail_config_.templates[it->second];
}

const ItemInfoMeta *MetadataLoader::item_info(uint32_t item_id) const
{

    auto it = item_info_by_id_.find(item_id);

    if (it == item_info_by_id_.end())
        return nullptr;

    return &item_infos_[it->second];
}

const ShopProductMeta *MetadataLoader::shop_product(uint32_t product_id) const
{

    auto it = shop_products_by_id_.find(product_id);

    if (it == shop_products_by_id_.end())
        return nullptr;

    return &shop_products_[it->second];
}

const RewardPackageMeta *MetadataLoader::reward_package(uint32_t package_id) const
{

    auto it = reward_packages_by_id_.find(package_id);

    if (it == reward_packages_by_id_.end())
        return nullptr;

    return &reward_packages_[it->second];
}

const std::vector<RewardPackageEntry> *MetadataLoader::reward_package_entries(uint32_t package_id) const
{

    auto it = reward_package_entries_by_package_.find(package_id);

    if (it == reward_package_entries_by_package_.end())
        return nullptr;

    return &it->second;
}

const RewardPackageEntry *MetadataLoader::reward_package_entry(uint32_t package_id, uint32_t entry_id) const
{

    uint64_t key = (static_cast<uint64_t>(package_id) << 32) | entry_id;
    auto it = reward_package_entries_by_key_.find(key);

    if (it == reward_package_entries_by_key_.end())
        return nullptr;

    return &reward_package_entries_[it->second];
}

const GemTypeMeta *MetadataLoader::gem_type(uint32_t id) const
{

    auto it = gem_types_by_id_.find(id);

    if (it == gem_types_by_id_.end())
        return nullptr;

    return &it->second;
}

const GemGradeMeta *MetadataLoader::gem_grade(uint32_t id) const
{

    auto it = gem_grades_by_id_.find(id);

    if (it == gem_grades_by_id_.end())
        return nullptr;

    return &it->second;
}

const GemDefinition *MetadataLoader::gem_definition(uint32_t gem_id) const
{

    auto it = gem_definitions_by_id_.find(gem_id);

    if (it == gem_definitions_by_id_.end())
        return nullptr;

    return &it->second;
}
