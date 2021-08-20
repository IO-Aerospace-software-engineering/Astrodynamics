# SDK
Astrodynamics toolkit

[![IO SDK Integration](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml)

[![IO SDK Deployment](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml)


# Quick start
## Download SDK
Download the latest Linux or Windows release :
[Releases](https://github.com/IO-Aerospace-software-engineering/SDK/releases)

## Install the SDK
1. Create a project folder
2. Extract folders (Data, Includes and Templates) from archive IO-Toolkit-OS-vx.x.xx-x to the root of your project folder.
3. You should have :
    - ProjectFolder
        - Data
        - Includes
        - Templates
4. Copy libIO.SDK.so to /usr/lib/


## Use SDK
At this stage we assume that you have mastered your development environment but if you need some advises we can suggest you these approches :
- [Cross plateform development](https://code.visualstudio.com/docs/cpp/cmake-linux)
- [Linux development](https://code.visualstudio.com/docs/cpp/config-linux)
- [Windows development](https://code.visualstudio.com/docs/cpp/config-mingw)

In this quick start we encourage you to use [cross plateform](https://code.visualstudio.com/docs/cpp/cmake-linux) approach.

In this example we will create a small program that will compute ISS orbital period from TLE(two lines elements), earth Hill sphere and angle between two vectors. 

1. Configure CMake like this example :
    ```CMAKE
    cmake_minimum_required(VERSION 3.18.0)

    project(MyApp VERSION 0.1.0)

    project (${This} C CXX)

    set(CMAKE_C_STANDARD 99)
    set(CMAKE_CXX_STANDARD 17)
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

    include(CTest)
    enable_testing()

    add_executable(MyApp main.cpp)

    include_directories(${CMAKE_SOURCE_DIR}/Includes)
    target_link_libraries(MyApp libIO.SDK.so)

    set(CPACK_PROJECT_NAME ${PROJECT_NAME})
    set(CPACK_PROJECT_VERSION ${PROJECT_VERSION})
    include(CPack)
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
    Remark : All values are expressed in SI (meter, second, radian, m/s, ...)