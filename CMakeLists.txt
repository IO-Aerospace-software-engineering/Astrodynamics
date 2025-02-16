﻿# CMakeList.txt : Top-level CMake project file, do global configuration
# and include sub-projects here.
#
cmake_minimum_required (VERSION 3.18)
set(This IO)

project (${This} C CXX)

set(CMAKE_C_STANDARD 99)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

SET(CMAKE_CXX_OUTPUT_EXTENSION_REPLACE 1)

if(UNIX)
    # SET(CMAKE_CXX_FLAGS_DEBUG  "-O0 -g --coverage") Coverage
    SET(CMAKE_CXX_FLAGS_DEBUG  "-O0 -g")
    add_compile_options(-Wall -Wextra -Wpedantic)
endif ()

if(MSVC)
endif ()

enable_testing()

#Install Google Tests 
include(FetchContent)
FetchContent_Declare(
  googletest
  URL https://github.com/google/googletest/archive/e2239ee6043f73722e7aa812a459f54a28552929.zip
)
# For Windows: Prevent overriding the parent project's compiler/linker settings
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(googletest)

# Include sub-projects.
add_subdirectory("IO.Astrodynamics")
add_subdirectory("IO.Astrodynamics.Tests")
