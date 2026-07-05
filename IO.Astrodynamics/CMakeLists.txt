cmake_minimum_required(VERSION 3.18)
set(This IO.Astrodynamics)
message(STATUS "Tests Projet source: ${PROJECT_SOURCE_DIR}")
message(STATUS "Tests CMake source dir: ${CMAKE_SOURCE_DIR}")
message(STATUS "Tests CMake current source dir: ${CMAKE_CURRENT_SOURCE_DIR}")
message(STATUS "Tests CMake current bin dir: ${CMAKE_CURRENT_BINARY_DIR}")

file(GLOB_RECURSE IO_SDK_SRC "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp")
file(GLOB_RECURSE IO_SDK_H "${CMAKE_CURRENT_SOURCE_DIR}/*.h")

add_library(${This} SHARED ${IO_SDK_SRC})

# REFERENCE SDK INCLUDES
MACRO(HEADER_DIRECTORIES return_list)
    FILE(GLOB_RECURSE new_list ${CMAKE_CURRENT_SOURCE_DIR}/*.h)
    SET(dir_list "")
    FOREACH (file_path ${new_list})
        GET_FILENAME_COMPONENT(dir_path ${file_path} PATH)
        SET(dir_list ${dir_list} ${dir_path})
    ENDFOREACH ()
    LIST(REMOVE_DUPLICATES dir_list)
    SET(${return_list} ${dir_list})
ENDMACRO()
HEADER_DIRECTORIES(MyList)
target_include_directories(${This} PUBLIC ${MyList})

#ADD SPECIFICS LIBS AND HEADERS
if (APPLE)
    # macOS: pick the CSPICE archive by target arch (native runners set
    # CMAKE_SYSTEM_PROCESSOR to arm64/x86_64); -DOSX_ARCH overrides for cross-compile.
    if (NOT OSX_ARCH)
        set(OSX_ARCH ${CMAKE_SYSTEM_PROCESSOR})
    endif ()
    if (OSX_ARCH STREQUAL "arm64")
        set(_cspice ${CMAKE_SOURCE_DIR}/external-lib/cspice_osx_arm64.a)
        set(_sofa   ${CMAKE_SOURCE_DIR}/external-lib/libsofa_c_osx_arm64.a)
    else ()
        set(_cspice ${CMAKE_SOURCE_DIR}/external-lib/cspice_osx_x64.a)
        set(_sofa   ${CMAKE_SOURCE_DIR}/external-lib/libsofa_c_osx_x64.a)
    endif ()
    # Mac CSPICE headers match includeLinux (which also carries sofa.h/sofam.h) — reuse it.
    target_include_directories(${This} PUBLIC ${CMAKE_SOURCE_DIR}/external-lib/includeLinux)
    target_link_libraries(${This} ${_cspice})
    target_link_libraries(${This} ${_sofa})
elseif (UNIX)
    target_include_directories(${This} PUBLIC ${CMAKE_SOURCE_DIR}/external-lib/includeLinux)
    target_link_libraries(${This} ${CMAKE_SOURCE_DIR}/external-lib/cspice.a)
    target_link_libraries(${This} ${CMAKE_SOURCE_DIR}/external-lib/libsofa_c.a)
else ()
    target_include_directories(${This} PUBLIC ${CMAKE_SOURCE_DIR}/external-lib/includeWindows)
    target_link_libraries(${This} ${CMAKE_SOURCE_DIR}/external-lib/cspice.lib)
    target_link_libraries(${This} ${CMAKE_SOURCE_DIR}/external-lib/sofa.lib)
endif ()


#INSTALL
if (UNIX)
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/external-lib/includeLinux/ DESTINATION include/IO FILES_MATCHING PATTERN "*.h")
elseif ()
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/external-lib/includeWindows/ DESTINATION include/IO FILES_MATCHING PATTERN "*.h")
endif ()

install(FILES ${IO_SDK_H} DESTINATION include/IO)
install(TARGETS ${This} LIBRARY DESTINATION lib)
