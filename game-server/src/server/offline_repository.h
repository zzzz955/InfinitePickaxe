#pragma once
#include "connection_pool.h"

class OfflineRepository {
public:
    explicit OfflineRepository(ConnectionPool& pool) : pool_(pool) {}
private:
    ConnectionPool& pool_;
};
