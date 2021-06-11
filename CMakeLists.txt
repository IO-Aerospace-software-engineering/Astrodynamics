# CMakeList.txt : Top-level CMake project file, do global configuration
# and include sub-projects here.
#
cmake_minimum_required (VERSION 3.18)
set(This IOSDK)

project (${This} C CXX)

set(CMAKE_C_STANDARD 99)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

enable_testing()

add_subdirectory(googletest)

# Include sub-projects.
add_subdirectory("IO.SDK")

add_subdirectory("IO.SDK.Tests")
