cmake_minimum_required(VERSION 3.18)
set(This IO.Astrodynamics.Tests)

file(GLOB_RECURSE IO_SDK_TESTS_SRC "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp")

add_executable(${This} ${IO_SDK_TESTS_SRC})

target_link_libraries(${This} PUBLIC "IO.Astrodynamics" gtest_main)

add_test(NAME ${This} COMMAND ${This})

target_include_directories(${This} PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

if (UNIX)
    add_custom_command(TARGET ${This}
            POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:IO.Astrodynamics> "${CMAKE_CURRENT_BINARY_DIR}/"
            COMMENT "Copy dll file to ${CMAKE_CURRENT_BINARY_DIR} directory" VERBATIM)
    file(COPY "Data/Linux/" DESTINATION ".")
else ()
    add_custom_command(TARGET ${This}
            POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:IO.Astrodynamics> "${CMAKE_CURRENT_BINARY_DIR}/${CMAKE_CFG_INTDIR}"
            COMMENT "Copy dll file to ${CMAKE_CURRENT_BINARY_DIR}/${CMAKE_CFG_INTDIR} directory" VERBATIM)
    file(COPY "Data/Windows/" DESTINATION "Release")
    file(COPY "Data/Windows/" DESTINATION "Debug")
endif ()


#Copy DLL to tests project

