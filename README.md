# SDK
Welcome!

Astrodynamics toolkit can be seen as an extension and a C++ wrapper of cspice toolkit(N 67) developped by the JPL.

This project has been initiated to make life easier for people who don't know cspice or need an object-oriented approach.

The goal of this project is to :
- Allow object oriented development and provide high level objects
- Abstract kernels and frames files management
- Provide a body integrator
- Simulate Spacecraft and impulsive maneuvers
- Evaluate constraints like occultations, body in instrument field of view, ...
    
## Project status

[![IO SDK Integration](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml)

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

In this example we will create a small program based on cmake to compute maneuvers required to join another Spacecraft from earth surface and evaluate some constraints during the flight.

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
Maneuver window : 2021-03-04 00:31:35.852043 (TDB) => 2021-03-04 00:31:44.178428 (TDB)
Thrust window : 2021-03-04 00:31:35.852043 (TDB) => 2021-03-04 00:31:44.178428 (TDB)
Thrust duration : 8.32638 s
Delta V - X : -96.3101 m/s
Delta V - Y : 106.947 m/s
Delta V - Z : -118.929 m/s
Delta V Magnitude : 186.702 m/s
Spacecraft orientation : X : -0.515851 Y : 0.572824 Z : -0.637002 ( ICRF )
Fuel burned :416.319 kg

======================================== Aspidal alignment ========================================
Maneuver window : 2021-03-04 01:14:35.908714 (TDB) => 2021-03-04 01:14:58.448142 (TDB)
Thrust window : 2021-03-04 01:14:35.908714 (TDB) => 2021-03-04 01:14:58.448142 (TDB)
Thrust duration : 22.5394 s
Delta V - X : -465.432 m/s
Delta V - Y : -170.795 m/s
Delta V - Z : 235.852 m/s
Delta V Magnitude : 549.021 m/s
Spacecraft orientation : X : -0.847749 Y : -0.311089 Z : 0.429586 ( ICRF )
Fuel burned :1126.97 kg

======================================== Phasing ========================================
Maneuver window : 2021-03-04 01:15:06.675929 (TDB) => 2021-03-04 04:58:19.564056 (TDB)
Thrust window : 2021-03-04 01:15:06.675929 (TDB) => 2021-03-04 01:15:16.220356 (TDB)
Thrust duration : 9.54443 s
Delta V - X : -140.066 m/s
Delta V - Y : 85.2926 m/s
Delta V - Z : 194.988 m/s
Delta V Magnitude : 254.782 m/s
Spacecraft orientation : X : -0.549749 Y : 0.334768 Z : 0.765315 ( ICRF )
Fuel burned :477.221 kg

======================================== Apogee height changing ========================================
Maneuver window : 2021-03-04 05:23:34.930488 (TDB) => 2021-03-04 05:23:43.510224 (TDB)
Thrust window : 2021-03-04 05:23:34.930488 (TDB) => 2021-03-04 05:23:43.510224 (TDB)
Thrust duration : 8.57974 s
Delta V - X : 134.75 m/s
Delta V - Y : -81.2458 m/s
Delta V - Z : -184.26 m/s
Delta V Magnitude : 242.302 m/s
Spacecraft orientation : X : 0.556124 Y : -0.335308 Z : -0.760457 ( ICRF )
Fuel burned :428.987 kg

======================================== Sun occultations from chaser Spacecraft ========================================
Occulation start at :2021-03-03 23:20:25.015236 (TDB)
Occulation end at :2021-03-03 23:25:08.814461 (TDB)

======================================== Windows when the moon is in camera's field of view ========================================
```
    Remark : If unspecified, all values are expressed in international system of units (meter, second, radian, m/s, ...)
