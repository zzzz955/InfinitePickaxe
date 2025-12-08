# syntax=docker/dockerfile:1.4
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    build-essential cmake git curl zip unzip tar pkg-config ninja-build ccache \
    libboost-all-dev libspdlog-dev libpqxx-dev libhiredis-dev libssl-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Cache source to layer; build with cache mounts
COPY . .

ARG BUILD_JOBS=0
ENV CCACHE_DIR=/root/.cache/ccache \
    CMAKE_BUILD_PARALLEL_LEVEL=${BUILD_JOBS}

RUN --mount=type=cache,target=/root/.cache \
    --mount=type=cache,target=/root/.cache/ccache \
    cmake -G Ninja -B build -S . -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_C_COMPILER_LAUNCHER=ccache \
        -DCMAKE_CXX_COMPILER_LAUNCHER=ccache \
    && cmake --build build --config Release --parallel

EXPOSE 10001

CMD ["/app/build/game-server"]
