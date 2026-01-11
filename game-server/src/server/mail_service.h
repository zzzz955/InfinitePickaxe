#pragma once
#include "game.pb.h"
#include "mail_repository.h"
#include "game_repository.h"
#include "metadata/metadata_loader.h"
#include <string>

class MailService {
public:
    MailService(MailRepository& mail_repo, GameRepository& game_repo, const MetadataLoader& meta)
        : mail_repo_(mail_repo), game_repo_(game_repo), meta_(meta) {}

    infinitepickaxe::MailListResponse handle_mail_list(const std::string& user_id,
                                                       const infinitepickaxe::MailListRequest& req);
    infinitepickaxe::MailDetailResponse handle_mail_detail(const std::string& user_id,
                                                           const std::string& mail_id,
                                                           bool mark_read);
    infinitepickaxe::MailClaimResult handle_mail_claim(const std::string& user_id,
                                                       const std::string& mail_id);
    infinitepickaxe::MailClaimAllResult handle_mail_claim_all(const std::string& user_id);

private:
    uint32_t resolve_list_limit(uint32_t requested) const;
    uint32_t resolve_claim_all_limit() const;
    bool try_parse_template_id(const std::string& text, uint32_t& out) const;
    infinitepickaxe::RewardType to_reward_type(const std::string& reward_type) const;

    MailRepository& mail_repo_;
    GameRepository& game_repo_;
    const MetadataLoader& meta_;
};
