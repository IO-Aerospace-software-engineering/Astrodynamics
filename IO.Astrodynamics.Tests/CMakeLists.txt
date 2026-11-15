cmake_minimum_required(VERSION 3.18)
set(This IO.Astrodynamics.Tests)

file(GLOB_RECURSE IO_SDK_TESTS_SRC "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp")

add_executable(${This} ${IO_SDK_TESTS_SRC})

# IO.Astrodynamics links CSPICE and SOFA PRIVATE, so this executable holds no copy of them: a test calls the proxies and the
# library's C++ API; a direct call of a CSPICE routine fails to link on Windows, and on Linux and macOS for a routine the
# library does not contain, instead of binding silently to a second CSPICE with its own kernel pool (#668, second round)
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

