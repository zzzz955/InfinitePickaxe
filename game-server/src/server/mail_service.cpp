#include "mail_service.h"
#include <cstdlib>
#include <limits>

uint32_t MailService::resolve_list_limit(uint32_t requested) const {
    const auto& config = meta_.mail_config();
    uint32_t limit = requested;
    if (limit == 0) {
        limit = config.default_list_limit;
    }
    if (limit == 0) {
        limit = config.max_mail_count;
    }
    if (config.max_mail_count > 0 && limit > config.max_mail_count) {
        limit = config.max_mail_count;
    }
    return limit;
}

uint32_t MailService::resolve_claim_all_limit() const {
    const auto& config = meta_.mail_config();
    uint32_t limit = config.claim_all_limit;
    if (limit == 0) {
        limit = config.max_mail_count;
    }
    if (limit == 0) {
        limit = config.default_list_limit;
    }
    return limit;
}

bool MailService::try_parse_template_id(const std::string& text, uint32_t& out) const {
    if (text.empty()) {
        return false;
    }
    char* end = nullptr;
    unsigned long value = std::strtoul(text.c_str(), &end, 10);
    if (!end || *end != '\0' || value > std::numeric_limits<uint32_t>::max()) {
        return false;
    }
    out = static_cast<uint32_t>(value);
    return true;
}

infinitepickaxe::RewardType MailService::to_reward_type(const std::string& reward_type) const {
    if (reward_type == "gold") {
        return infinitepickaxe::GOLD;
    }
    if (reward_type == "crystal") {
        return infinitepickaxe::CRYSTAL;
    }
    if (reward_type == "item") {
        return infinitepickaxe::ITEM;
    }
    return infinitepickaxe::REWARD_TYPE_UNKNOWN;
}

infinitepickaxe::MailListResponse MailService::handle_mail_list(const std::string& user_id,
                                                                const infinitepickaxe::MailListRequest& req) {
    infinitepickaxe::MailListResponse res;

    auto counts = mail_repo_.get_mail_counts(user_id);
    res.set_unread_count(counts.unread_count);
    res.set_unclaimed_count(counts.unclaimed_count);

    uint32_t limit = resolve_list_limit(req.limit());
    if (limit == 0) {
        res.set_has_next(false);
        return res;
    }

    uint32_t fetch_limit = limit;
    if (limit < std::numeric_limits<uint32_t>::max()) {
        fetch_limit = limit + 1;
    }

    auto rows = mail_repo_.get_mail_summaries(
        user_id,
        fetch_limit,
        req.cursor_created_at_ms(),
        req.cursor_mail_id(),
        req.include_claimed(),
        req.include_expired());

    bool has_next = rows.size() > limit;
    if (has_next) {
        rows.resize(limit);
    }

    for (const auto& row : rows) {
        auto* summary = res.add_mails();
        summary->set_mail_id(row.mail_id);
        summary->set_mail_type(row.mail_type);

        std::string title = row.title;
        if (title.empty()) {
            uint32_t template_id = 0;
            if (try_parse_template_id(row.template_id, template_id)) {
                const auto* tmpl = meta_.mail_template(template_id);
                if (tmpl && !tmpl->title.empty()) {
                    title = tmpl->title;
                }
            }
        }

        summary->set_title(title);
        summary->set_created_at_ms(row.created_at_ms);
        summary->set_expires_at_ms(row.expires_at_ms);
        summary->set_is_read(row.is_read);
        summary->set_is_claimed(row.is_claimed);
        summary->set_has_reward(row.has_reward);
    }

    res.set_has_next(has_next);
    if (has_next && !rows.empty()) {
        const auto& last = rows.back();
        res.set_next_cursor_created_at_ms(last.created_at_ms);
        res.set_next_cursor_mail_id(last.mail_id);
    }

    return res;
}

infinitepickaxe::MailDetailResponse MailService::handle_mail_detail(const std::string& user_id,
                                                                    const std::string& mail_id,
                                                                    bool mark_read) {
    infinitepickaxe::MailDetailResponse res;

    auto detail_opt = mail_repo_.get_mail_detail(user_id, mail_id);
    if (!detail_opt.has_value()) {
        res.set_success(false);
        res.set_error_code("MAIL_NOT_FOUND");
        return res;
    }

    auto detail = detail_opt.value();
    if (mark_read && !detail.is_read) {
        if (!mail_repo_.mark_mail_read(user_id, mail_id)) {
            res.set_success(false);
            res.set_error_code("DB_ERROR");
            return res;
        }
        detail.is_read = true;
    }

    std::string title = detail.title;
    std::string body = detail.body;
    std::string sender = detail.sender;

    uint32_t template_id = 0;
    if (try_parse_template_id(detail.template_id, template_id)) {
        const auto* tmpl = meta_.mail_template(template_id);
        if (tmpl) {
            if (title.empty()) {
                title = tmpl->title;
            }
            if (body.empty()) {
                body = tmpl->body;
            }
            if (sender.empty()) {
                sender = tmpl->sender;
            }
        }
    }

    auto* mail = res.mutable_mail();
    mail->set_mail_id(detail.mail_id);
    mail->set_mail_type(detail.mail_type);
    mail->set_template_id(detail.template_id);
    mail->set_template_args_json(detail.template_args_json);
    mail->set_title(title);
    mail->set_body(body);
    mail->set_sender(sender);
    mail->set_created_at_ms(detail.created_at_ms);
    mail->set_expires_at_ms(detail.expires_at_ms);
    mail->set_is_read(detail.is_read);
    mail->set_is_claimed(detail.is_claimed);

    auto rewards = mail_repo_.get_mail_rewards(mail_id);
    for (const auto& reward : rewards) {
        auto* entry = mail->add_rewards();
        entry->set_reward_type(to_reward_type(reward.reward_type));
        entry->set_reward_key(reward.reward_key);
        entry->set_amount(reward.amount);
    }

    res.set_success(true);
    res.set_error_code("");
    return res;
}

infinitepickaxe::MailClaimResult MailService::handle_mail_claim(const std::string& user_id,
                                                                const std::string& mail_id) {
    infinitepickaxe::MailClaimResult res;
    res.set_mail_id(mail_id);

    auto claim = mail_repo_.claim_mail(user_id, mail_id);
    if (!claim.success) {
        res.set_success(false);
        if (claim.not_found) {
            res.set_error_code("MAIL_NOT_FOUND");
        } else if (claim.expired) {
            res.set_error_code("MAIL_EXPIRED");
        } else if (claim.already_claimed) {
            res.set_error_code("ALREADY_CLAIMED");
        } else if (claim.unsupported_reward) {
            res.set_error_code("ITEM_NOT_SUPPORTED");
        } else {
            res.set_error_code("DB_ERROR");
        }
        return res;
    }

    for (const auto& reward : claim.rewards) {
        auto* entry = res.add_rewards();
        entry->set_reward_type(to_reward_type(reward.reward_type));
        entry->set_reward_key(reward.reward_key);
        entry->set_amount(reward.amount);
    }

    if (claim.totals_updated) {
        res.set_total_gold(claim.total_gold);
        res.set_total_crystal(claim.total_crystal);
    } else {
        auto data = game_repo_.get_user_game_data(user_id);
        res.set_total_gold(data.gold);
        res.set_total_crystal(data.crystal);
    }

    res.set_success(true);
    res.set_error_code("");
    return res;
}

infinitepickaxe::MailClaimAllResult MailService::handle_mail_claim_all(const std::string& user_id) {
    infinitepickaxe::MailClaimAllResult res;

    uint32_t limit = resolve_claim_all_limit();
    if (limit == 0) {
        res.set_success(false);
        res.set_error_code("CONFIG_ERROR");
        return res;
    }

    auto claim = mail_repo_.claim_all(user_id, limit);
    if (!claim.success) {
        res.set_success(false);
        if (claim.nothing_to_claim) {
            res.set_error_code("NOTHING_TO_CLAIM");
        } else if (claim.unsupported_reward) {
            res.set_error_code("ITEM_NOT_SUPPORTED");
        } else {
            res.set_error_code("DB_ERROR");
        }
        return res;
    }

    res.set_claimed_count(claim.claimed_count);
    for (const auto& reward : claim.rewards) {
        auto* entry = res.add_rewards();
        entry->set_reward_type(to_reward_type(reward.reward_type));
        entry->set_reward_key(reward.reward_key);
        entry->set_amount(reward.amount);
    }

    if (claim.totals_updated) {
        res.set_total_gold(claim.total_gold);
        res.set_total_crystal(claim.total_crystal);
    } else {
        auto data = game_repo_.get_user_game_data(user_id);
        res.set_total_gold(data.gold);
        res.set_total_crystal(data.crystal);
    }

    res.set_success(true);
    res.set_error_code("");
    return res;
}
