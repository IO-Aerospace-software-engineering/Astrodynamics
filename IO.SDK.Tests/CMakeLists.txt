cmake_minimum_required (VERSION 3.18)
set(This IO.SDK.Tests)

file(GLOB_RECURSE IO_SDK_TESTS_SRC "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp")

add_executable(${This} ${IO_SDK_TESTS_SRC} )

target_link_libraries(${This} PUBLIC "IO.SDK" gtest_main)

add_test(NAME ${This} COMMAND ${This})

target_include_directories(${This} PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

if (MSVC)
add_custom_command(TARGET ${This}
POST_BUILD
COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:IO.SDK> "${CMAKE_CURRENT_BINARY_DIR}/Debug"
COMMENT "Copy dll file to ${CMAKE_CURRENT_BINARY_DIR}/Debug directory" VERBATIM
)
elseif(UNIX)
add_custom_command(TARGET ${This}
POST_BUILD
COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:IO.SDK> "${CMAKE_CURRENT_BINARY_DIR}/"
COMMENT "Copy dll file to ${CMAKE_CURRENT_BINARY_DIR} directory" VERBATIM
)
endif ()


#Copy DLL to tests project

