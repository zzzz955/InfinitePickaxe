#include "ad_service.h"
#include "time_utils.h"
#include <unordered_set>

namespace {
uint32_t reward_for_ad_view(const std::vector<uint32_t>& rewards_by_view, uint32_t count) {
    if (count == 0) return 0;
    if (count > rewards_by_view.size()) return 0;
    return rewards_by_view[count - 1];
}
} // namespace

std::vector<AdCounter> AdService::get_ad_counters(const std::string& user_id) {
    std::vector<AdCounter> counters;
    const auto& metas = meta_.ad_types();
    if (metas.empty()) {
        return repo_.get_all_ad_counters(user_id);
    }
    counters.reserve(metas.size());

    std::unordered_set<std::string> seen;
    for (const auto& meta : metas) {
        if (meta.id.empty()) {
            continue;
        }
        if (!seen.insert(meta.id).second) {
            continue;
        }
        counters.push_back(repo_.get_or_create_ad_counter(user_id, meta.id));
    }
    return counters;
}

AdCounter AdService::get_or_create_ad_counter(const std::string& user_id, const std::string& ad_type) {
    return repo_.get_or_create_ad_counter(user_id, ad_type);
}

infinitepickaxe::AdCountersState AdService::get_ad_counters_state(const std::string& user_id) {
    infinitepickaxe::AdCountersState state;
    state.set_reset_timestamp_ms(kst_next_midnight_ms());

    auto counters = get_ad_counters(user_id);
    for (const auto& counter : counters) {
        auto* ad_counter = state.add_ad_counters();
        ad_counter->set_ad_type(counter.ad_type);
        ad_counter->set_ad_count(counter.ad_count);
        uint32_t limit = 0;
        if (const auto* meta = meta_.ad_meta(counter.ad_type)) {
            limit = meta->daily_limit;
        }
        ad_counter->set_daily_limit(limit);
    }
    return state;
}

infinitepickaxe::AdWatchResult AdService::handle_ad_watch(const std::string& user_id,
                                                          const std::string& ad_type) {
    infinitepickaxe::AdWatchResult result;
    result.set_success(false);
    result.set_ad_type(ad_type);
    result.set_error_code("");

    const auto* ad_meta = meta_.ad_meta(ad_type);
    if (!ad_meta) {
        result.set_error_code("INVALID_AD_TYPE");
    } else {
        auto counter = repo_.get_or_create_ad_counter(user_id, ad_type);
        if (ad_meta->daily_limit > 0 && counter.ad_count >= ad_meta->daily_limit) {
            result.set_error_code("DAILY_LIMIT_REACHED");
        } else {
            uint32_t next_count = counter.ad_count + 1;
            if (!repo_.increment_ad_counter(user_id, ad_type)) {
                result.set_error_code("DB_ERROR");
            } else {
                uint32_t crystal_reward = 0;
                if (ad_meta->effect == "crystal_reward") {
                    crystal_reward = reward_for_ad_view(ad_meta->rewards_by_view, next_count);
                    if (crystal_reward > 0) {
                        auto total_opt = game_repo_.add_crystal(user_id, crystal_reward);
                        if (!total_opt.has_value()) {
                            result.set_error_code("DB_ERROR");
                        } else {
                            result.set_total_crystal(total_opt.value());
                        }
                    }
                }
                result.set_crystal_earned(crystal_reward);
                if (result.error_code().empty()) {
                    result.set_success(true);
                }
            }
        }
    }

    auto state = get_ad_counters_state(user_id);
    for (const auto& counter_updated : state.ad_counters()) {
        *result.add_ad_counters() = counter_updated;
    }

    if (!result.success() && result.error_code().empty()) {
        result.set_error_code("UNKNOWN_ERROR");
    }

    return result;
}
