cmake_minimum_required(VERSION 3.20)
project(infinite_pickaxe_game_server LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_executable(game-server
    src/main.cpp
    src/server/tcp_server.cpp
    src/server/http_auth_client.cpp
)

# Force module mode to avoid missing Boost config packages on distro builds
set(Boost_NO_BOOST_CMAKE ON)
find_package(Boost REQUIRED COMPONENTS system)
find_package(spdlog REQUIRED)

include(FetchContent)

# httplib: use package if available, otherwise fetch header-only
find_package(httplib QUIET)
if(NOT httplib_FOUND)
    FetchContent_Declare(
        httplib
        GIT_REPOSITORY https://github.com/yhirose/cpp-httplib.git
        GIT_TAG v0.15.3
    )
    FetchContent_MakeAvailable(httplib)
endif()

# libpqxx: fetch if no package
find_package(pqxx QUIET)
if(NOT pqxx_FOUND)
    set(PQXX_ENABLE_SHARED OFF CACHE BOOL "" FORCE)
    set(PQXX_BUILD_TEST OFF CACHE BOOL "" FORCE)
    FetchContent_Declare(
        libpqxx
        GIT_REPOSITORY https://github.com/jtv/libpqxx.git
        GIT_TAG 7.9.2
    )
    FetchContent_MakeAvailable(libpqxx)
    add_library(pqxx::pqxx ALIAS pqxx)
endif()

# redis-plus-plus: use package if available, otherwise fetch
find_package(redis++ QUIET)
if(NOT redis++_FOUND)
    set(REDIS_PLUS_PLUS_BUILD_TEST OFF CACHE BOOL "" FORCE)
    set(REDIS_PLUS_PLUS_CXX_STANDARD 17 CACHE STRING "" FORCE)
    FetchContent_Declare(
        redisplusplus
        GIT_REPOSITORY https://github.com/sewenew/redis-plus-plus.git
        GIT_TAG 1.3.9
    )
    FetchContent_MakeAvailable(redisplusplus)
endif()

target_include_directories(game-server PRIVATE src)
target_link_libraries(game-server PRIVATE
    Boost::boost
    Boost::system
    spdlog::spdlog
    httplib::httplib
    pqxx::pqxx
    redis++::redis++
)
