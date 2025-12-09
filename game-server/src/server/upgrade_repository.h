#pragma once
#include "connection_pool.h"

class UpgradeRepository {
public:
    explicit UpgradeRepository(ConnectionPool& pool) : pool_(pool) {}
private:
    ConnectionPool& pool_;
};
