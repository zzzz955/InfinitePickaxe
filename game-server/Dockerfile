# syntax=docker/dockerfile:1.4
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    build-essential cmake git curl zip unzip tar pkg-config ninja-build ccache \
    libboost-all-dev libspdlog-dev libpqxx-dev libhiredis-dev libssl-dev \
    protobuf-compiler libprotobuf-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 소스 복사: game-server 코드, proto 스키마, 메타데이터
# 주의: 이 Dockerfile은 프로젝트 루트에서 실행되어야 합니다
# docker build -f game-server/Dockerfile .
COPY game-server/ .
COPY protocol/ ./protocol
COPY metadata/ ./metadata

ARG BUILD_JOBS=8
ENV CCACHE_DIR=/root/.cache/ccache \
    CMAKE_BUILD_PARALLEL_LEVEL=${BUILD_JOBS}

RUN --mount=type=cache,target=/root/.cache \
    --mount=type=cache,target=/root/.cache/ccache \
    cmake -G Ninja -B build -S . -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_C_COMPILER_LAUNCHER=ccache \
        -DCMAKE_CXX_COMPILER_LAUNCHER=ccache \
    && cmake --build build --config Release --parallel ${BUILD_JOBS}

EXPOSE 10001

CMD ["/app/build/game-server"]
