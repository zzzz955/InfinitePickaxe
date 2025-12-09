#pragma once
#include "connection_pool.h"

class MiningRepository {
public:
    explicit MiningRepository(ConnectionPool& pool) : pool_(pool) {}
private:
    ConnectionPool& pool_;
};
