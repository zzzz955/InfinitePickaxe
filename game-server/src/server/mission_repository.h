#pragma once
#include "connection_pool.h"

class MissionRepository {
public:
    explicit MissionRepository(ConnectionPool& pool) : pool_(pool) {}
private:
    ConnectionPool& pool_;
};
