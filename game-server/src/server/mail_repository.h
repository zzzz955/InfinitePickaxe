#pragma once
#include "connection_pool.h"
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

struct MailSummaryRow {
    std::string mail_id;
    std::string mail_type;
    std::string template_id;
    std::string title;
    uint64_t created_at_ms{0};
    uint64_t expires_at_ms{0};
    bool is_read{false};
    bool is_claimed{false};
    bool has_reward{false};
};

struct MailDetailRow {
    std::string mail_id;
    std::string mail_type;
    std::string template_id;
    std::string template_args_json;
    std::string title;
    std::string body;
    std::string sender;
    uint64_t created_at_ms{0};
    uint64_t expires_at_ms{0};
    bool is_read{false};
    bool is_claimed{false};
};

struct MailRewardRow {
    std::string reward_type;
    std::string reward_key;
    uint64_t amount{0};
};

struct MailCounts {
    uint32_t unread_count{0};
    uint32_t unclaimed_count{0};
};

struct MailClaimResult {
    bool success{false};
    bool not_found{false};
    bool expired{false};
    bool already_claimed{false};
    bool unsupported_reward{false};
    bool db_error{false};
    bool totals_updated{false};
    uint64_t total_gold{0};
    uint32_t total_crystal{0};
    std::vector<MailRewardRow> rewards;
};

struct MailClaimAllResult {
    bool success{false};
    bool nothing_to_claim{false};
    bool unsupported_reward{false};
    bool db_error{false};
    bool totals_updated{false};
    uint32_t claimed_count{0};
    uint64_t total_gold{0};
    uint32_t total_crystal{0};
    std::vector<MailRewardRow> rewards;
};

class MailRepository {
public:
    explicit MailRepository(ConnectionPool& pool) : pool_(pool) {}

    MailCounts get_mail_counts(const std::string& user_id);
    std::vector<MailSummaryRow> get_mail_summaries(const std::string& user_id,
                                                   uint32_t limit,
                                                   uint64_t cursor_created_at_ms,
                                                   const std::string& cursor_mail_id,
                                                   bool include_claimed,
                                                   bool include_expired);
    std::optional<MailDetailRow> get_mail_detail(const std::string& user_id, const std::string& mail_id);
    std::vector<MailRewardRow> get_mail_rewards(const std::string& mail_id);
    bool mark_mail_read(const std::string& user_id, const std::string& mail_id);
    MailClaimResult claim_mail(const std::string& user_id, const std::string& mail_id);
    MailClaimAllResult claim_all(const std::string& user_id, uint32_t limit);

private:
    ConnectionPool& pool_;
};
