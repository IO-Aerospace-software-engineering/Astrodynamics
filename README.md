# SDK
Astrodynamics toolkit

[![IO SDK Integration](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml)

[![IO SDK Deployment](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml)


# Quick start
## Download SDK
Download the latest Linux or Windows release :
[Releases](https://github.com/IO-Aerospace-software-engineering/SDK/releases)

## Install the SDK

At this stage we assume that you have mastered your development environment but if you need some advises we can suggest you these approches :
- [Cross plateform development](https://code.visualstudio.com/docs/cpp/cmake-linux)
- [Linux development](https://code.visualstudio.com/docs/cpp/config-linux)
- [Windows development](https://code.visualstudio.com/docs/cpp/config-mingw)

In this quick start we suggest you to use [cross plateform approach](https://code.visualstudio.com/docs/cpp/cmake-linux) with CMake.

1. Create a cmake project

2. Extract Includes folder from archive IO-Toolkit-OS-vx.x.xx-x to the root folder.

3. Extract Data and Templates folders from archive IO-Toolkit-OS-vx.x.xx-x to your executable build folder.

4. You should have :
    - ProjectFolder
        - Includes
        - build
            - Data
            - Templates

5a. For Linux copy libIO.SDK.so to /usr/lib/

5b. For Windows copy IO.SDK.dll and IO.SDK.lib in build directory and in the same place of your executable.


## Use SDK

In this example we will create a small program that will compute ISS orbital period from TLE(two lines elements), earth Hill sphere and angle between two vectors. 

1. Ensure your CMake projet contains at least these parameters :
    ```CMAKE
    cmake_minimum_required(VERSION 3.18.0)
    project(MyApp VERSION 0.1.0)

    project (MyApp C CXX)

    set(CMAKE_C_STANDARD 99)
    set(CMAKE_CXX_STANDARD 17)
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

    add_executable(MyApp main.cpp)

    include_directories(${CMAKE_SOURCE_DIR}/Includes)

    if (MSVC)
        target_link_libraries(MyApp IO.SDK.dll)
    elseif(UNIX)
        target_link_libraries(MyApp libIO.SDK.so)
    endif ()
    ```

2. Create a main.cpp file in project root folder.

3. Create program in main.cpp file :
    ```C++
    #include <memory>
    #include <iostream>
    #include <string>

    //IO SDK headers to include
    #include <TLE.h>
    #include <Vector3D.h>
    #include <CelestialBody.h>    

    int main(int, char **)
    {
        //1. Compute ISS orbital period from two lines elements
        auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
        auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
        std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
        IO::SDK::OrbitalParameters::TLE tle(earth, lines);

        double period = tle.GetPeriod().GetSeconds().count();
        std::cout << "ISS orbital period is :" << period << std::endl;

        //2. Get the Earth Hill sphere
        double hillSphereRadius = earth->GetHillSphere();
        std::cout << "the earth Hill sphere radius is :" << hillSphereRadius << std::endl;

        //3. Compute angle between two vectors
        IO::SDK::Math::Vector3D v1(1.0, 0.0, 0.0);
        IO::SDK::Math::Vector3D v2(0.0, 1.0, 0.0);
        std::cout << "Angle between v1 and v2 is :" << v1.GetAngle(v2) << std::endl;
    }
    ```
4. Run your application, you should have these outputs :
    ```CMD
    ISS orbital period is :5576.68
    the earth's Hill sphere radius is :1.4716e+09
    Angle between v1 and v2 is :1.5708
    ```
    Remark : All values are expressed in international system of units (meter, second, radian, m/s, ...)
