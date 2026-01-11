#include "mail_repository.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <unordered_map>

MailCounts MailRepository::get_mail_counts(const std::string& user_id) {
    MailCounts counts;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto row = tx.exec_params1(
            "SELECT "
            "  COALESCE(SUM(CASE WHEN read_at IS NULL "
            "    AND deleted_at IS NULL "
            "    AND (expires_at IS NULL OR expires_at > NOW()) "
            "    THEN 1 ELSE 0 END), 0) AS unread_count, "
            "  COALESCE(SUM(CASE WHEN claimed_at IS NULL "
            "    AND deleted_at IS NULL "
            "    AND (expires_at IS NULL OR expires_at > NOW()) "
            "    THEN 1 ELSE 0 END), 0) AS unclaimed_count "
            "FROM game_schema.user_mail "
            "WHERE user_id = $1::uuid",
            user_id);
        counts.unread_count = static_cast<uint32_t>(row[0].as<int64_t>());
        counts.unclaimed_count = static_cast<uint32_t>(row[1].as<int64_t>());
        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_mail_counts failed: user={} error={}", user_id, ex.what());
    }
    return counts;
}

std::vector<MailSummaryRow> MailRepository::get_mail_summaries(const std::string& user_id,
                                                               uint32_t limit,
                                                               uint64_t cursor_created_at_ms,
                                                               const std::string& cursor_mail_id,
                                                               bool include_claimed,
                                                               bool include_expired) {
    std::vector<MailSummaryRow> rows;
    if (limit == 0) {
        return rows;
    }

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        std::string query =
            "SELECT m.mail_id::text, m.mail_type, m.template_id, "
            "  COALESCE(m.title, '') AS title, "
            "  COALESCE((EXTRACT(EPOCH FROM m.created_at) * 1000)::BIGINT, 0) AS created_at_ms, "
            "  COALESCE((EXTRACT(EPOCH FROM m.expires_at) * 1000)::BIGINT, 0) AS expires_at_ms, "
            "  (m.read_at IS NOT NULL) AS is_read, "
            "  (m.claimed_at IS NOT NULL) AS is_claimed, "
            "  EXISTS (SELECT 1 FROM game_schema.user_mail_rewards r WHERE r.mail_id = m.mail_id) AS has_reward "
            "FROM game_schema.user_mail m "
            "WHERE m.user_id = $1::uuid "
            "  AND m.deleted_at IS NULL";

        if (!include_claimed) {
            query += " AND m.claimed_at IS NULL";
        }
        if (!include_expired) {
            query += " AND (m.expires_at IS NULL OR m.expires_at > NOW())";
        }

        const bool use_cursor = (cursor_created_at_ms > 0 && !cursor_mail_id.empty());
        if (use_cursor) {
            query += " AND (m.created_at < to_timestamp($2 / 1000.0) "
                     "  OR (m.created_at = to_timestamp($2 / 1000.0) AND m.mail_id < $3::uuid))";
        }

        if (use_cursor) {
            query += " ORDER BY m.created_at DESC, m.mail_id DESC LIMIT $4";
            auto result = tx.exec_params(
                query,
                user_id,
                static_cast<int64_t>(cursor_created_at_ms),
                cursor_mail_id,
                static_cast<int32_t>(limit));

            for (const auto& row : result) {
                MailSummaryRow entry;
                entry.mail_id = row[0].as<std::string>();
                entry.mail_type = row[1].as<std::string>();
                entry.template_id = row[2].as<std::string>();
                entry.title = row[3].as<std::string>();
                entry.created_at_ms = row[4].as<uint64_t>();
                entry.expires_at_ms = row[5].as<uint64_t>();
                entry.is_read = row[6].as<bool>();
                entry.is_claimed = row[7].as<bool>();
                entry.has_reward = row[8].as<bool>();
                rows.push_back(std::move(entry));
            }
        } else {
            query += " ORDER BY m.created_at DESC, m.mail_id DESC LIMIT $2";
            auto result = tx.exec_params(
                query,
                user_id,
                static_cast<int32_t>(limit));

            for (const auto& row : result) {
                MailSummaryRow entry;
                entry.mail_id = row[0].as<std::string>();
                entry.mail_type = row[1].as<std::string>();
                entry.template_id = row[2].as<std::string>();
                entry.title = row[3].as<std::string>();
                entry.created_at_ms = row[4].as<uint64_t>();
                entry.expires_at_ms = row[5].as<uint64_t>();
                entry.is_read = row[6].as<bool>();
                entry.is_claimed = row[7].as<bool>();
                entry.has_reward = row[8].as<bool>();
                rows.push_back(std::move(entry));
            }
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_mail_summaries failed: user={} error={}", user_id, ex.what());
    }
    return rows;
}

std::optional<MailDetailRow> MailRepository::get_mail_detail(const std::string& user_id, const std::string& mail_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto result = tx.exec_params(
            "SELECT m.mail_id::text, m.mail_type, m.template_id, "
            "  COALESCE(m.template_args::text, '') AS template_args_json, "
            "  COALESCE(m.title, '') AS title, "
            "  COALESCE(m.body, '') AS body, "
            "  COALESCE(m.sender, '') AS sender, "
            "  COALESCE((EXTRACT(EPOCH FROM m.created_at) * 1000)::BIGINT, 0) AS created_at_ms, "
            "  COALESCE((EXTRACT(EPOCH FROM m.expires_at) * 1000)::BIGINT, 0) AS expires_at_ms, "
            "  (m.read_at IS NOT NULL) AS is_read, "
            "  (m.claimed_at IS NOT NULL) AS is_claimed "
            "FROM game_schema.user_mail m "
            "WHERE m.user_id = $1::uuid "
            "  AND m.mail_id = $2::uuid "
            "  AND m.deleted_at IS NULL",
            user_id, mail_id);

        if (result.empty()) {
            return std::nullopt;
        }

        const auto& row = result[0];
        MailDetailRow detail;
        detail.mail_id = row[0].as<std::string>();
        detail.mail_type = row[1].as<std::string>();
        detail.template_id = row[2].as<std::string>();
        detail.template_args_json = row[3].as<std::string>();
        detail.title = row[4].as<std::string>();
        detail.body = row[5].as<std::string>();
        detail.sender = row[6].as<std::string>();
        detail.created_at_ms = row[7].as<uint64_t>();
        detail.expires_at_ms = row[8].as<uint64_t>();
        detail.is_read = row[9].as<bool>();
        detail.is_claimed = row[10].as<bool>();

        tx.commit();
        return detail;
    } catch (const std::exception& ex) {
        spdlog::error("get_mail_detail failed: user={} mail_id={} error={}", user_id, mail_id, ex.what());
        return std::nullopt;
    }
}

std::vector<MailRewardRow> MailRepository::get_mail_rewards(const std::string& mail_id) {
    std::vector<MailRewardRow> rewards;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto result = tx.exec_params(
            "SELECT reward_type, COALESCE(reward_key, '') AS reward_key, amount "
            "FROM game_schema.user_mail_rewards "
            "WHERE mail_id = $1::uuid "
            "ORDER BY reward_index",
            mail_id);

        for (const auto& row : result) {
            MailRewardRow reward;
            reward.reward_type = row[0].as<std::string>();
            reward.reward_key = row[1].as<std::string>();
            reward.amount = static_cast<uint64_t>(row[2].as<int64_t>());
            rewards.push_back(std::move(reward));
        }

        tx.commit();
    } catch (const std::exception& ex) {
        spdlog::error("get_mail_rewards failed: mail_id={} error={}", mail_id, ex.what());
    }
    return rewards;
}

bool MailRepository::mark_mail_read(const std::string& user_id, const std::string& mail_id) {
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);
        auto result = tx.exec_params(
            "UPDATE game_schema.user_mail "
            "SET read_at = NOW(), updated_at = NOW() "
            "WHERE user_id = $1::uuid "
            "  AND mail_id = $2::uuid "
            "  AND deleted_at IS NULL "
            "  AND read_at IS NULL",
            user_id, mail_id);
        tx.commit();
        return result.affected_rows() > 0;
    } catch (const std::exception& ex) {
        spdlog::error("mark_mail_read failed: user={} mail_id={} error={}", user_id, mail_id, ex.what());
        return false;
    }
}

MailClaimResult MailRepository::claim_mail(const std::string& user_id, const std::string& mail_id) {
    MailClaimResult result;
    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto mail_row = tx.exec_params(
            "SELECT (claimed_at IS NOT NULL) AS is_claimed, "
            "       (expires_at IS NOT NULL AND expires_at <= NOW()) AS is_expired "
            "FROM game_schema.user_mail "
            "WHERE user_id = $1::uuid "
            "  AND mail_id = $2::uuid "
            "  AND deleted_at IS NULL "
            "FOR UPDATE",
            user_id, mail_id);

        if (mail_row.empty()) {
            result.not_found = true;
            return result;
        }

        const bool is_claimed = mail_row[0][0].as<bool>();
        const bool is_expired = mail_row[0][1].as<bool>();

        if (is_claimed) {
            result.already_claimed = true;
            return result;
        }
        if (is_expired) {
            result.expired = true;
            return result;
        }

        auto rewards = tx.exec_params(
            "SELECT reward_type, COALESCE(reward_key, '') AS reward_key, amount "
            "FROM game_schema.user_mail_rewards "
            "WHERE mail_id = $1::uuid "
            "ORDER BY reward_index",
            mail_id);

        uint64_t total_gold = 0;
        uint32_t total_crystal = 0;

        for (const auto& row : rewards) {
            MailRewardRow reward;
            reward.reward_type = row[0].as<std::string>();
            reward.reward_key = row[1].as<std::string>();
            reward.amount = static_cast<uint64_t>(row[2].as<int64_t>());

            if (reward.reward_type == "gold") {
                total_gold += reward.amount;
            } else if (reward.reward_type == "crystal") {
                total_crystal += static_cast<uint32_t>(reward.amount);
            } else {
                result.unsupported_reward = true;
                return result;
            }

            result.rewards.push_back(std::move(reward));
        }

        if (total_gold > 0 || total_crystal > 0) {
            auto totals = tx.exec_params1(
                "UPDATE game_schema.user_game_data "
                "SET gold = gold + $2, crystal = crystal + $3 "
                "WHERE user_id = $1::uuid "
                "RETURNING gold, crystal",
                user_id,
                static_cast<int64_t>(total_gold),
                static_cast<int64_t>(total_crystal));
            result.total_gold = totals[0].as<uint64_t>();
            result.total_crystal = totals[1].as<uint32_t>();
            result.totals_updated = true;
        }

        tx.exec_params(
            "UPDATE game_schema.user_mail "
            "SET claimed_at = NOW(), read_at = COALESCE(read_at, NOW()), updated_at = NOW() "
            "WHERE user_id = $1::uuid AND mail_id = $2::uuid",
            user_id, mail_id);

        tx.commit();
        result.success = true;
    } catch (const std::exception& ex) {
        spdlog::error("claim_mail failed: user={} mail_id={} error={}", user_id, mail_id, ex.what());
        result.db_error = true;
    }
    return result;
}

MailClaimAllResult MailRepository::claim_all(const std::string& user_id, uint32_t limit) {
    MailClaimAllResult result;
    if (limit == 0) {
        result.nothing_to_claim = true;
        return result;
    }

    try {
        auto conn = pool_.acquire();
        pqxx::work tx(*conn);

        auto mail_rows = tx.exec_params(
            "SELECT mail_id::text "
            "FROM game_schema.user_mail "
            "WHERE user_id = $1::uuid "
            "  AND deleted_at IS NULL "
            "  AND claimed_at IS NULL "
            "  AND (expires_at IS NULL OR expires_at > NOW()) "
            "ORDER BY created_at ASC, mail_id ASC "
            "LIMIT $2 "
            "FOR UPDATE",
            user_id,
            static_cast<int32_t>(limit));

        if (mail_rows.empty()) {
            result.nothing_to_claim = true;
            return result;
        }

        std::vector<std::string> mail_ids;
        mail_ids.reserve(mail_rows.size());
        for (const auto& row : mail_rows) {
            mail_ids.push_back(row[0].as<std::string>());
        }

        auto rewards = tx.exec_params(
            "SELECT reward_type, COALESCE(reward_key, '') AS reward_key, amount "
            "FROM game_schema.user_mail_rewards "
            "WHERE mail_id = ANY($1::uuid[]) "
            "ORDER BY mail_id, reward_index",
            mail_ids);

        uint64_t total_gold = 0;
        uint32_t total_crystal = 0;
        std::unordered_map<std::string, size_t> reward_index;

        for (const auto& row : rewards) {
            std::string reward_type = row[0].as<std::string>();
            std::string reward_key = row[1].as<std::string>();
            uint64_t amount = static_cast<uint64_t>(row[2].as<int64_t>());

            if (reward_type == "gold") {
                total_gold += amount;
            } else if (reward_type == "crystal") {
                total_crystal += static_cast<uint32_t>(amount);
            } else {
                result.unsupported_reward = true;
                return result;
            }

            std::string map_key = reward_type;
            map_key.push_back(':');
            map_key += reward_key;

            auto it = reward_index.find(map_key);
            if (it == reward_index.end()) {
                MailRewardRow entry;
                entry.reward_type = reward_type;
                entry.reward_key = reward_key;
                entry.amount = amount;
                result.rewards.push_back(std::move(entry));
                reward_index.emplace(std::move(map_key), result.rewards.size() - 1);
            } else {
                result.rewards[it->second].amount += amount;
            }
        }

        if (total_gold > 0 || total_crystal > 0) {
            auto totals = tx.exec_params1(
                "UPDATE game_schema.user_game_data "
                "SET gold = gold + $2, crystal = crystal + $3 "
                "WHERE user_id = $1::uuid "
                "RETURNING gold, crystal",
                user_id,
                static_cast<int64_t>(total_gold),
                static_cast<int64_t>(total_crystal));
            result.total_gold = totals[0].as<uint64_t>();
            result.total_crystal = totals[1].as<uint32_t>();
            result.totals_updated = true;
        }

        tx.exec_params(
            "UPDATE game_schema.user_mail "
            "SET claimed_at = NOW(), read_at = COALESCE(read_at, NOW()), updated_at = NOW() "
            "WHERE user_id = $1::uuid AND mail_id = ANY($2::uuid[])",
            user_id, mail_ids);

        tx.commit();
        result.success = true;
        result.claimed_count = static_cast<uint32_t>(mail_ids.size());
    } catch (const std::exception& ex) {
        spdlog::error("claim_all failed: user={} error={}", user_id, ex.what());
        result.db_error = true;
    }
    return result;
}
