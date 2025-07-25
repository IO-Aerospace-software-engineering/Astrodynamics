# IO.Astrodynamics
## Presentation
[![IO Astrodynamics deployment](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/ci.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/ci.yml)
[![IO Astrodynamics deployment](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cd.yml/badge.svg)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cd.yml)

IO.Astrodynamics based on cspice toolkit(N 67) developped by the JPL and IO.Astrodynamics native library, it provides the best of both worlds : C++ Velocity + .Net productivity

This framework provides the following features :

* Work with [JPL Spice kernels](https://naif.jpl.nasa.gov/naif/data.html) and stars
* Export simulation to [Cosmographia](https://naif.jpl.nasa.gov/naif/cosmographia.html)
* PDS Archive management
  * Generate archive from object
  * Generate object from archive
  * Validate archive from Xml schemas
* Orbit prediction from observation
* Spacecraft propagator
  * Geopotentials model (Earth's Geopotentials is embedded but others can be provided)
  * Simplified atmospheric model (Earth and Mars built-in)
  * Solar radiation pressure
  * N-body perturbation
  * Impulsive maneuvers
  * Fuel balance
* Small body propagator
  * Geopotentials model (Earth only)
  * Simplified atmospheric model (Earth and Mars only)
  * Solar radiation pressure
  * N-body perturbation
* Compute and convert orbital parameters
  * State vector
  * Two lines elements
  * Equinoctial
  * Keplerian elements
* Compute and convert coordinates system
  * Equatorial
  * Horizontal
  * Planetodetic
  * Planetographic
* Frame transformation
  * ICRF / J2000
  * Ecliptic J2000
  * Ecliptic B1950
  * True Equator Mean Equinox
  * Galactic
  * B1950
  * FK4
  * Body fixed frames and ITRF93 (High accuracy earth fixed frame)
* Configure spacecraft
  * Clock
  * Fuel tank
  * Engines
  * Instrument
* Impulse maneuvers :
  * Lambert transfer 
  * Apogee height
  * Perigee height
  * Plane alignment
  * Combined maneuver
  * Apsidal alignment
  * Phasing
* Attitudes
  * Instrument pointing toward an object
  * Nadir
  * Zenith
  * Prograde
  * Retrograde
* Surface site on any celestial body
* Evaluate launch opportunities
* Use or convert different time referential (Calendar, Julian, seconds from J2000, TDB, UTC, Local)
* Get celestial body information based on Naif kernels
* Find time windows based on distance constraints from spacecraft, celestial body or ground site
* Find time windows based on occultation constraints from spacecraft, celestial body or ground site
* Find time windows based on coordinate constraints from spacecraft, celestial body or ground site
* Find time windows based on illumination constraints from ground site.
* Find time windows when an object is in instrument field of view.
* Manipulate kernel files
* Math tools
  * Vector
  * Matrix
  * Plane
  * Quaternion
  * Lagrange interpolation
  * Legendre polynomials
  * Jacobian
  * SLERP & LERP
* Use the Framework through CLI (Command line interface)
  * Propagate small body and visualize it with Cosmographia
  * Sub observer point
  * Angular separation
  * Orientation
  * Orbital parameters converter
  * Frame converter
  * Time converter
  * Celestial body information
  * Find time windows from coordinate constraint
  * Find time windows from distance constraint
  * Find time windows from occultation constraint
  * Find time windows from field of view constraint
  * Find time windows from illumination constraint

## Concept
IO.Astrodynamics is based on [Spice concept](https://naif.jpl.nasa.gov/naif/spiceconcept.html)

To work, this framework needs data (Ephemeris, Planetary constants, leap seconds, mission data, ...) these data can be found [here](https://naif.jpl.nasa.gov/naif/data.html)

To use these data in the framework, just call this function :

```C#
//Load required kernels for computation
API.Instance.LoadKernels(new DirectoryInfo("<your path containing data>"));
```

When you use the propagation functionalities, you will have to provide an output path to reuse, if necessary, the data generated in another software.

## Installation
This package is hosted by Nuget [here](https://www.nuget.org/packages/IO.Astrodynamics/).
You can install it in your project with this command :
```
dotnet add package IO.Astrodynamics
```
## Quick start
```C#
//In this example we want the ephemeris of the moon at epoch (2000-01-01T12:00Z) in ICRF frame with earth at center
API.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));//replace Data/SolarSytem by your kernel path
var earth = new CelestialBody(PlanetsAndMoons.EARTH);
var moon = new CelestialBody(PlanetsAndMoons.MOON);
var ephemerid = moon.GetEphemeris(new TimeSystem.Time(new DateTime(2000, 1, 1, 12, 0, 0), TimeFrame.UTCFrame), earth, Frames.Frame.ICRF, Aberration.None);

```

You can find more advanced examples [here](https://github.com/IO-Aerospace-software-engineering/Astrodynamics.Net/wiki/Examples)

## Documentation
For more information you can read the [wiki](https://github.com/IO-Aerospace-software-engineering/Astrodynamics.Net/wiki)


# IO.Astrodynamics C++

This project aims to simplify the use of cspice for those who are unfamiliar with it or prefer an object-oriented approach.

With this project, you can:

- Develop high-level objects using object-oriented programming
- Handle files (kernels, frames, ...) in an abstract way
- Integrate bodies and simulate spacecrafts and impulsive maneuvers
- Check constraints such as occultations, body in instrument field of view, etc.

This framework is written in C++ for optimal performance, but if you want a more productive approach, you can switch to the .Net version of this project [here](https://github.com/IO-Aerospace-software-engineering/Astrodynamics.Net), which offers the best of both worlds.C++ velocity + .Net productivity = ❤️
    
## Project status

[![IO Astrodynamics Integration](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cmake.yml/badge.svg?branch=develop)](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/actions/workflows/cmake.yml)

[![IO Astrodynamics Code coverage](https://img.shields.io/badge/Code%20coverage-Passing-Green.svg)](https://htmlpreview.github.io/?https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/develop/coverage/index.html)

# Quick start
## Download Astrodynamics framework
Download the latest Linux or Windows release :
[Releases](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/releases)

At this stage we assume that you have mastered your development environment but if you need some advises for yours developments we suggest you this approach :
- [Cross plateform development](https://code.visualstudio.com/docs/cpp/cmake-linux)
- [Linux development](https://code.visualstudio.com/docs/cpp/config-linux)
- [Windows development](https://code.visualstudio.com/docs/cpp/config-mingw)

In this quick start you have 2 options to install the framework, one from binaries another from cmake.

## Option 1 - Install from binaries
### On Linux

1. Create your C/C++ project folder, in this example we assume your output path will be called "build" but you can use the name of your choice. 

2. Extract **Includes** folder from archive IO-Toolkit-Linux-vx.x.xx-x to folder /usr/local/include/IO/.
3. Copy **libIO.Astrodynamics.so** to /usr/local/lib/
4. Extract **Data** folder from archive IO-Toolkit-Linux-vx.x.xx-x into your computer. This data folder contains main solar system kernels.
5. You will need to load these data in your program with the following recursive function
```C++
IO::Astrodynamics::Kernels::KernelsLoader::Load("Data/SolarSystem");
```

### On Windows

1. Create your C/C++ project folder, in this example we assume your output path will be called "build" but you can use the name of your choice.

2. From the dll package you just downloaded
   - Copy **Includes** folder at the root of the project
   - Copy **IO.Astrodynamics.dll** and **IO.Astrodynamics.lib** in the root folder(used to link libraries) and in the build folder(used at runtime).
   - Copy **Data** folder into your computer. This data folder contains main solar system kernels.
3. You will need to load these data in your program with the following recursive function
```C++
IO::Astrodynamics::Kernels::KernelsLoader::Load("Data/SolarSystem");
```

You should have a folder tree like that :

```
YourProject
  | Includes
  | build         
     | IO.Astrodynamics.dll
     | IO.Astrodynamics.lib
  | IO.Astrodynamics.dll
  | IO.Astrodynamics.lib
```

## Option 2 - Build and install from source code
    
```bash
#Clone project    
git clone https://github.com/IO-Aerospace-software-engineering/Astrodynamics.git

#Go into directory
cd Astrodynamics

#Create build directory    
mkdir build_release

#Go into build directory
cd build_release

#Configure Cmake project
cmake -DCMAKE_BUILD_TYPE=Release ..

#Build project
#-j 4 option is used to define how many threads could be used to compile project, is this example will use 4 threads
cmake --build . --config Release --target IO.Astrodynamics -j 4

#Install libraries and includes
#This command must be executed with admin rights
cmake --install IO.Astrodynamics

```

:warning: Windows users :warning:

Due to heterogeneous Windows development environments, once you've proceeded cmake install you must copy headers and libraries into your project.

Windows users should have a folder tree like that for their project :
```
 YourProject
   | Includes
   | build_release
      | IO.Astrodynamics.dll
      | IO.Astrodynamics.lib
   | IO.Astrodynamics.dll
   | IO.Astrodynamics.lib
```

Linux users should have a folder tree like that :
```
YourProject
   | build_release
```
    
## Use the framework
Before use the framework, you must install it.

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
        target_link_libraries(MyApp IO.Astrodynamics.dll)
    elseif(UNIX)
        include_directories(/usr/local/include/IO)
        find_library(IO_Astrodynamics_LIB NAMES libIO.Astrodynamics.so)
        target_link_libraries(IOAstrodynamicsTEST ${IO_Astrodynamics_LIB})
    endif ()
    ```

2. You can create a scenario based on this [Example](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/IO.Astrodynamics.Scenarios/Program.cpp)

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
Opportunity start at :2021-03-04 04:02:39.570545 (TDB)
Opportunity end at :2021-03-04 12:28:59.250151 (TDB)

Opportunity start at :2021-03-04 13:44:25.914095 (TDB)
Opportunity end at :2021-03-04 15:35:27.446243 (TDB)

Opportunity start at :2021-03-04 17:49:59.706025 (TDB)
Opportunity end at :2021-03-04 18:38:37.723206 (TDB)

```
    Remark : If unspecified, all values are expressed in international system of units (meter, second, radian, m/s, ...)
