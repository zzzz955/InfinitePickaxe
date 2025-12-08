FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    build-essential cmake git curl zip unzip tar pkg-config ninja-build \
    libboost-all-dev libspdlog-dev libpqxx-dev libhiredis-dev libssl-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY . .

# Configure & build
RUN cmake -B build -S . -DCMAKE_BUILD_TYPE=Release \
    && cmake --build build --config Release

EXPOSE 10001

CMD ["/app/build/game-server"]
