cmake_minimum_required (VERSION 3.18)
set(This IO.SDK.Proxy)

file(GLOB_RECURSE IO_SDK_Proxy_SRC "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp")
file(GLOB_RECURSE IO_SDK_MODELS_PROXY_H "${CMAKE_CURRENT_SOURCE_DIR}/Models/*.h")
file(GLOB_RECURSE IO_SDK_PROXY_H "${CMAKE_CURRENT_SOURCE_DIR}/*.h")

add_library(${This} SHARED ${IO_SDK_Proxy_SRC} ${IO_SDK_MODELS_PROXY_H} ${IO_SDK_PROXY_H})

target_link_libraries(${This} PUBLIC "IO.SDK")

target_include_directories(${This} PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})