cmake_minimum_required(VERSION 3.18)
set(This IO.SDK)
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
if (UNIX)
    target_include_directories(${This} PUBLIC ${CMAKE_SOURCE_DIR}/external-lib/includeLinux)
    target_link_libraries(${This} ${CMAKE_SOURCE_DIR}/external-lib/cspice.a)
else ()
    target_include_directories(${This} PUBLIC ${CMAKE_SOURCE_DIR}/external-lib/includeWindows)
    target_link_libraries(${This} ${CMAKE_SOURCE_DIR}/external-lib/cspice.lib)
endif ()


#INSTALL
if (UNIX)
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/external-lib/includeLinux/ DESTINATION include/IO FILES_MATCHING PATTERN "*.h")
elseif ()
    install(DIRECTORY ${CMAKE_SOURCE_DIR}/external-lib/includeWindows/ DESTINATION include/IO FILES_MATCHING PATTERN "*.h")
endif ()

install(FILES ${IO_SDK_H} DESTINATION include/IO)
install(TARGETS ${This} LIBRARY DESTINATION lib)
