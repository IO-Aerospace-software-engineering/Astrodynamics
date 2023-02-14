# SDK
Welcome!

Astrodynamics toolkit can be seen as an extension and a C++ wrapper of cspice toolkit(N 67) developped by the JPL.

This project has been initiated to make life easier for people who don't know cspice or need an object-oriented approach.

The goal of this project is to :
- Allow object oriented development and provide high level objects
- Abstract kernels and frames files management
- Provide a body integrator
- Simulate spacecraft and impulsive maneuvers
- Evaluate constraints like occultations, body in instrument field of view, ...
    
## Project status

[![IO SDK Integration](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml)

[![IO SDK Deployment](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml)

[![IO SDK Code coverage](https://img.shields.io/badge/Code%20coverage-Passing-Green.svg)](https://htmlpreview.github.io/?https://github.com/IO-Aerospace-software-engineering/SDK/blob/develop/coverage/index.html)

# Quick start
## Download SDK
Download the latest Linux or Windows release :
[Releases](https://github.com/IO-Aerospace-software-engineering/SDK/releases)

At this stage we assume that you have mastered your development environment but if you need some advises for yours developments we suggest you this approach :
- [Cross plateform development](https://code.visualstudio.com/docs/cpp/cmake-linux)
- [Linux development](https://code.visualstudio.com/docs/cpp/config-linux)
- [Windows development](https://code.visualstudio.com/docs/cpp/config-mingw)

In this quick start you have 2 options to install the SDK, one from binaries another from cmake.

## Option 1 - Install from binaries
### On Linux

1. Create your C/C++ project folder, in this example we assume your output path will be called "build" but you can use the name of your choice. 

2. Extract **Includes** folder from archive IO-Toolkit-Linux-vx.x.xx-x to folder /usr/local/include/IO/.
3. Copy **libIO.SDK.so** to /usr/local/lib/

4. Extract **Data** folder from archive IO-Toolkit-Linux-vx.x.xx-x to your build folder.
5. You should have :
    ```
    YourProject
        | build
           | Data
    ```


### On Windows

1. Create your C/C++ project folder, in this example we assume your output path will be called "build" but you can use the name of your choice.

2. From the dll package you just downloaded
   - Copy **Includes** folder at the root of the project
   - Copy **IO.SDK.dll** and **IO.SDK.lib** in the root folder(used to link libraries) and in the build folder(used at runtime).
   - Copy folder : **Data** into the build folder\ 

    You should have a folder tree like that :
   
    ```
    YourProject
      | Includes
      | build
         | Data
         | IO.SDK.dll
         | IO.SDK.lib
      | IO.SDK.dll
      | IO.SDK.lib
    ```

## Option 2 - Build and install from source code
    
```bash
#Clone project    
git clone https://github.com/IO-Aerospace-software-engineering/SDK.git

#Go into directory
cd SDK

#Create build directory    
mkdir build_release

#Go into build directory
cd build_release

#Configure Cmake project
cmake -DCMAKE_BUILD_TYPE=Release ..

#Build project
#-j 4 option is used to define how many threads could be used to compile project, is this example will use 4 threads
cmake --build . --config Release --target IO.SDK -j 4

#Install libraries and includes
#This command must be executed with admin rights
cmake --install IO.SDK

#When you create a project that will use the SDK, don't forget to import **Data** into your build directory.
#We suggest you to use Data from IO.SDK.Tests project because these data have been used to approved software
#You could use one of these scripts to copy data folder:
#========Windows user======== : 
cp ../IO.SDK.Tests/Data/Windows/ <your build path>

#========Linux user======== : 
cp ../IO.SDK.Tests/Data/Linux/ <your build path>

```

:warning: Windows users :warning:

Due to heterogeneous Windows development environments, once you've proceeded cmake install you must copy headers and libraries into your project.

Windows users should have a folder tree like that for their project :
```
 YourProject
   | Includes
   | build_release
      | Data
      | IO.SDK.dll
      | IO.SDK.lib
   | IO.SDK.dll
   | IO.SDK.lib
```

Linux users should have a folder tree like that :
```
YourProject
   | build_release
      | Data
```
    
## Use the SDK
Before use the SDK, you must install it.

It can be installed from binaries or cmake, these procedures are described above. 

In this example we will create a small program based on cmake to compute maneuvers required to join another spacecraft from earth surface and evaluate some constraints during the flight.

1. Ensure your CMake project contains at least these parameters :
    ```CMAKE
    cmake_minimum_required(VERSION 3.18.0)
    project(MyApp VERSION 0.1.0)

    project (MyApp C CXX)

    set(CMAKE_C_STANDARD 99)
    set(CMAKE_CXX_STANDARD 17)
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

    add_executable(MyApp main.cpp)

    if (MSVC)
        include_directories(${CMAKE_SOURCE_DIR}/Includes)
        target_link_libraries(MyApp IO.SDK.dll)
    elseif(UNIX)
        include_directories(/usr/local/include/IO)
        find_library(IO_SDK_LIB NAMES libIO.SDK.so)
        target_link_libraries(IOSDKTEST ${IO_SDK_LIB})
    endif ()
    ```

2. You can create a scenario based on this [Example](https://github.com/IO-Aerospace-software-engineering/SDK/tree/develop/IO.SDK.Scenarios/Program.cpp)

3. When you execute it, you should have this output :
    ```CMD
    ========================================Launch Window 0 ========================================
    Launch epoch :2021-03-03 23:09:15.829809 (UTC)
    Inertial azimuth :47.0059 °
    Non inertial azimuth :45.1252 °
    Inertial insertion velocity :8794.34 m/s
    Non inertial insertion velocity :8499.73 m/s

    ========================================Launch Window 1 ========================================
    Launch epoch :2021-03-04 23:05:20.139985 (UTC)
    Inertial azimuth :47.0059 °
    Non inertial azimuth :45.1252 °
    Inertial insertion velocity :8794.34 m/s
    Non inertial insertion velocity :8499.73 m/s

    ======================================== Plane alignment ========================================
    Maneuver window : 2021-03-04 00:33:28.947415 (TDB) => 2021-03-04 00:33:37.083057 (TDB)
    Thrust window : 2021-03-04 00:33:28.947415 (TDB) => 2021-03-04 00:33:37.083057 (TDB)
    Thrust duration : 8.13564 s
    Delta V : 182.335 m/s
    Spacecraft orientation : X : -0.516358 Y : 0.573323 Z : -0.636141 ( ICRF )
    Fuel burned :406.782 kg

    ======================================== Aspidal alignment ========================================
    Maneuver window : 2021-03-04 01:18:24.928793 (TDB) => 2021-03-04 01:18:43.237321 (TDB)
    Thrust window : 2021-03-04 01:18:24.928793 (TDB) => 2021-03-04 01:18:43.237321 (TDB)
    Thrust duration : 18.3085 s
    Delta V : 440.163 m/s
    Spacecraft orientation : X : -0.844401 Y : -0.286639 Z : 0.452575 ( ICRF )
    Fuel burned :915.426 kg

    ======================================== Phasing ========================================
    Maneuver window : 2021-03-04 04:34:57.320071 (TDB) => 2021-03-04 08:18:28.240580 (TDB)
    Thrust window : 2021-03-04 04:34:57.320071 (TDB) => 2021-03-04 04:35:07.154572 (TDB)
    Thrust duration : 9.8345 s
    Delta V : 255.907 m/s
    Spacecraft orientation : X : -0.549214 Y : 0.335374 Z : 0.765434 ( ICRF )
    Fuel burned :491.725 kg

    ======================================== Apogee height changing ========================================
    Maneuver window : 2021-03-04 08:43:16.504286 (TDB) => 2021-03-04 08:43:25.804857 (TDB)
    Thrust window : 2021-03-04 08:43:16.504286 (TDB) => 2021-03-04 08:43:25.804857 (TDB)
    Thrust duration : 9.30057 s
    Delta V : 256.479 m/s
    Spacecraft orientation : X : 0.549319 Y : -0.335252 Z : -0.765412 ( ICRF )
    Fuel burned :465.029 kg

    ======================================== Sun occultations from dragonfly spacecraft ========================================
    Occulation start at :2021-03-03 23:20:25.015236 (TDB)
    Occulation end at :2021-03-03 23:25:08.727103 (TDB)

    ======================================== Windows when the moon is in camera's field of view ========================================
    Opportunity start at :2021-03-03 23:20:25.187133 (TDB)
    Opportunity end at :2021-03-04 01:15:44.489464 (TDB)

    Opportunity start at :2021-03-04 01:21:23.355170 (TDB)
    Opportunity end at :2021-03-04 04:33:14.824913 (TDB)
    ```
    Remark : If unspecified, all values are expressed in international system of units (meter, second, radian, m/s, ...)
