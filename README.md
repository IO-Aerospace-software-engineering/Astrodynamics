# SDK
Welcome!

Astrodynamics toolkit can be seen as an extension and a C++ wrapper of cspice toolkit(N 67) developped by the JPL.

This project has been initiated to make life easier for people who don't know cspice or need an object-oriented approach.

The goal of this project is to :
- Allow object oriented development and provide high level objects
- Abstract kernels and frames files management
- Provides a body integrator
- Simulate spacecraft and impulsive maneuvers
    
## Project status

[![IO SDK Integration](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/cmake.yml)

[![IO SDK Deployment](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/SDK/actions/workflows/deployment.yml)

[![IO SDK Code coverage](https://img.shields.io/badge/Code%20coverage-Passing-Green.svg)](https://htmlpreview.github.io/?https://github.com/IO-Aerospace-software-engineering/SDK/blob/develop/coverage/index.html)

# Quick start
## Download SDK
Download the latest Linux or Windows release :
[Releases](https://github.com/IO-Aerospace-software-engineering/SDK/releases)

At this stage we assume that you have mastered your development environment but if you need some advises we can suggest you these approches :
- [Cross plateform development](https://code.visualstudio.com/docs/cpp/cmake-linux)
- [Linux development](https://code.visualstudio.com/docs/cpp/config-linux)
- [Windows development](https://code.visualstudio.com/docs/cpp/config-mingw)

In this quick start we suggest you to use [cross plateform approach](https://code.visualstudio.com/docs/cpp/cmake-linux) with CMake.

## Install from binaries
### On Linux

1. Create a cmake project

2. Extract **Includes** folder from archive IO-Toolkit-Linux-vx.x.xx-x to folder /usr/local/include/IO/.

3. Extract **Data** folder from archive IO-Toolkit-Linux-vx.x.xx-x to your executable build folder.

4. You should have :
    ```
    YourProject
        | build
           | Data
    ```
5. Copy **libIO.SDK.so<span>** to /usr/local/lib/

### On Windows

1. Create a cmake project

2. From the dll package you just downloaded
   - Copy **Includes** folder at the root of the project
   - Copy **IO.SDK.dll** and **IO.SDK.lib** in the build folder and in the Debug folder. You can also copy the library in parent folder and configure your linker to use the relative path of the library
   - Copy folder : **Data** in the Debug folder\ 

    You should have a folder tree like below

    ```
    YourProject
        | Includes
        | build
            | IO.SDK.dll
            | IO.SDK.lib
            | Debug (generated after the first compile and run)
                | Data
                | IO.SDK.dll
                | IO.SDK.lib
    ```

## Build and install from source code
    
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

#Build porject
#-j 4 option is used to define how many threads could be used to compile project, is this example will use 4 threads
cmake --build . --config Release --target IO.SDK -j 4

#Install libraries and includes
#This command must be executed with admin rights
cmake --install IO.SDK
```

    
## Use the SDK

In this example we will create a small program to compute maneuvers required to join another spacecraft from earth surface

1. (Execute this step only with binary installation) Ensure your CMake projet contains at least these parameters :
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
        target_link_libraries(MyApp libIO.SDK.so)
    endif ()
    ```

2. Create a main.cpp file your project folder.

3. Create program in main.cpp file :
    ```C++
    #include <memory>
    #include <string>
    #include <iostream>
    #include <chrono>
    #include <vector>

    #include <CelestialBody.h>
    #include <LaunchSite.h>
    #include <OrbitalParameters.h>
    #include <Spacecraft.h>
    #include <StateVector.h>
    #include <TLE.h>
    #include <Launch.h>
    #include <LaunchWindow.h>
    #include <Window.h>
    #include <UTC.h>
    #include <TDB.h>
    #include <Propagator.h>
    #include <VVIntegrator.h>
    #include <OrbitalPlaneChangingManeuver.h>
    #include <ApogeeHeightChangingManeuver.h>
    #include <ApsidalAlignmentManeuver.h>
    #include <PhasingManeuver.h>
    #include <DataPoolMonitoring.h> 

   int main()
    {
        /*========================== Scenario Description =====================================
        We are at Cap canaveral and we have to join another spacecraft in orbit.
        The launch must occurs by day at launch site and recovery site
        To realize this operation, we'll show you how to use IO SDK to find launch windows then maneuvers sequence to reach our objective.
        For each maneuver you will obtain the maneuver window, the thrust window, Delta V, Spacecraft or satellite orientation and mass of fuel burned.
        We also get sun occultations and windows when the moon will be in camera's field of view
        */

        //=======================Configure universe topology======================================
        //Bodies id are defined here https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/naif_ids.html#NAIF%20Object%20ID%20numbers
        auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
        auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
        auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon");

        //========================Compute launch parameters=======================================

        //Define launch site and recovery site
        auto launchSite = std::make_shared<IO::SDK::Sites::LaunchSite>(3, "S3", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);
        auto recoverySite = std::make_shared<IO::SDK::Sites::LaunchSite>(4, "S4", IO::SDK::Coordinates::Geodetic(-80.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);

        //Define simulation window. (Warning : When spacecraft is involved, dates must be greater than 2021-01-01 to be compliant with spacecraft clock)
        IO::SDK::Time::TDB startEpoch("2021-03-02T00:00:00");
        IO::SDK::Time::TDB endEpoch("2021-03-05T00:00:00");

        //Define parking orbit
        auto parkingOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                               6700000.0,
                                                                                               0.3,
                                                                                               50.0 * IO::SDK::Constants::DEG_RAD,
                                                                                               41.0 * IO::SDK::Constants::DEG_RAD,
                                                                                               0.0 * IO::SDK::Constants::DEG_RAD,
                                                                                               0.0,
                                                                                               startEpoch,
                                                                                               IO::SDK::Frames::InertialFrames::GetICRF());
        //Define orbit of the target
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                              6800000.0,
                                                                                              0.4,
                                                                                              51.0 * IO::SDK::Constants::DEG_RAD,
                                                                                              43.0 * IO::SDK::Constants::DEG_RAD,
                                                                                              10.0 * IO::SDK::Constants::DEG_RAD,
                                                                                              0.0,
                                                                                              startEpoch,
                                                                                              IO::SDK::Frames::InertialFrames::GetICRF());

        //Compute launch windows, to launch by day on launch site and recovery site when the launch site crosses the parking orbital plane
        IO::SDK::Maneuvers::Launch launch(launchSite, recoverySite, true, *parkingOrbit);
        auto launchWindows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(startEpoch.ToUTC(), endEpoch.ToUTC()));

        //Display launch window results (this is not necessary)
        DisplayLaunchWindowsSummary(launchWindows);

        //===================Compute maneuvers to reach target body================================

        //Configure spacecraft at insertion orbit
        IO::SDK::Body::Spacecraft::Spacecraft spacecraft{-178, "DRAGONFLY", 1000.0, 10000.0, "MIS01", std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(*parkingOrbit)};
        spacecraft.AddFuelTank("fuelTank1", 9000.0, 9000.0);                                                          // Add fuel tank
        spacecraft.AddEngine("serialNumber1", "engine1", "fuelTank1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0); //Add engine and link with fuel tank
        spacecraft.AddPayload("PAY01", "Payload 01", 50.0);                                                           //We add a 50 kg payload to the spacecraft

        //We add an instrument with a circular field of view  aligned with the spacecraft Z axis
        IO::SDK::Math::Vector3D orientation{1.0, 0.0, 0.0};
        IO::SDK::Math::Vector3D boresight{0.0, 0.0, 1.0};
        IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};
        spacecraft.AddCircularFOVInstrument(600, "CAM600", orientation, boresight, fovvector, 80.0 * IO::SDK::Constants::DEG_RAD);

        //Target
        IO::SDK::Body::Spacecraft::Spacecraft spacecraftTarget{-179, "TARGET", 1000.0, 10000.0, "MIS01", std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(*targetOrbit)};
        spacecraftTarget.AddFuelTank("fuelTank2", 9000.0, 9000.0);                                                          // Add fuel tank
        spacecraftTarget.AddEngine("serialNumber2", "engine2", "fuelTank2", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0); //Add engine and link with fuel tank

        //Configure propagator
        auto step{IO::SDK::Time::TimeSpan(1.0s)};

        //Add gravity to forces model
        //You can add your own force model
        std::vector<IO::SDK::Integrators::Forces::Force *> forces{};
        IO::SDK::Integrators::Forces::GravityForce gravityForce;
        forces.push_back(&gravityForce);

        //Initialize an integrator
        IO::SDK::Integrators::VVIntegrator integrator(step, forces);

        //We assume the ship will be in orbit 10 minutes after launch.
        IO::SDK::Time::TDB startDatePropagator = launchWindows[0].GetWindow().GetStartDate().ToTDB().Add(IO::SDK::Time::TimeSpan(600.0s));

        //Initialize propagator for dragonfly spacecraft
        IO::SDK::Propagators::Propagator propagator(spacecraft, integrator, IO::SDK::Time::Window(startDatePropagator, endEpoch));

        //Intialize propagator for target spacecraft
        IO::SDK::Propagators::Propagator targetPropagator(spacecraftTarget, integrator, IO::SDK::Time::Window(startDatePropagator, endEpoch));
        targetPropagator.Propagate();

        //We define which engines can be used to realize maneuvers
        auto engine1 = spacecraft.GetEngine("serialNumber1");
        std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
        engines.push_back(*engine1);

        //We configure each maneuver
        IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver planeAlignment(engines, propagator, targetOrbit.get(), startDatePropagator); //The first maneuver must not start until the launch is complete
        IO::SDK::Maneuvers::ApsidalAlignmentManeuver apsidalAlignment(engines, propagator, targetOrbit.get());
        IO::SDK::Maneuvers::PhasingManeuver phasing(engines, propagator, 1, targetOrbit.get());
        IO::SDK::Maneuvers::ApogeeHeightChangingManeuver finalApogeeChanging(engines, propagator, targetOrbit->GetApogeeVector().Magnitude());

        //We order maneuvers
        planeAlignment.SetNextManeuver(apsidalAlignment).SetNextManeuver(phasing).SetNextManeuver(finalApogeeChanging);

        //We set the first maneuver in standby
        propagator.SetStandbyManeuver(&planeAlignment);

        //We execute the propagator
        propagator.Propagate();

        //Find sun occultation
        auto occultationWindows = spacecraft.FindWindowsOnOccultationConstraint(IO::SDK::Time::Window<IO::SDK::Time::TDB>(startDatePropagator, endEpoch), *sun, *earth, IO::SDK::OccultationType::Any(), IO::SDK::AberrationsEnum::None, IO::SDK::Time::TimeSpan(30s));

        //Find when moon will be in instrument field of view
        auto fovWindows = spacecraft.GetInstrument(600)->FindWindowsWhereInFieldOfView(IO::SDK::Time::Window<IO::SDK::Time::TDB>(startDatePropagator, spacecraft.GetOrientationsCoverageWindow().GetEndDate()), *moon, IO::SDK::Time::TimeSpan(300s), IO::SDK::AberrationsEnum::LT);

    }
    ```
4. Run your application, if you read launch windows, maneuvers, occultations and fov windows you will retrieve these informations :
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
