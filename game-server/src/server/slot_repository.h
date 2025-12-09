#pragma once
#include "connection_pool.h"

class SlotRepository {
public:
    explicit SlotRepository(ConnectionPool& pool) : pool_(pool) {}
private:
    ConnectionPool& pool_;
};
