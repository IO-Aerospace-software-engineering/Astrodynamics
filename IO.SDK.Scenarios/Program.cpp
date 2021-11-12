#include <memory>
#include <string>
#include <iostream>

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

using namespace std::literals::chrono_literals;

int main()
{
    //=======================Configure universe topology======================================
    //auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    //auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon",earth);

    //========================Compute launch parameters=======================================

    //Define launch site
    auto ls = std::make_shared<IO::SDK::Sites::LaunchSite>(3, "S3", IO::SDK::Coordinates::Geodetic(-81.0 * IO::SDK::Constants::DEG_RAD, 28.5 * IO::SDK::Constants::DEG_RAD, 0.0), earth);

    IO::SDK::Time::TDB startEpoch("2021-06-02T00:00:00");
    IO::SDK::Time::TDB endEpoch("2021-06-03T00:00:00");
    //Define parking orbit
    auto parkingOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                           6700000.0,
                                                                                           0.3,
                                                                                           50.0 * IO::SDK::Constants::DEG_RAD,
                                                                                           40.0 * IO::SDK::Constants::DEG_RAD,
                                                                                           0.0 * IO::SDK::Constants::DEG_RAD,
                                                                                           0.0,
                                                                                           startEpoch,
                                                                                           IO::SDK::Frames::InertialFrames::GetICRF());
    //Define target orbit
    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                          6700000.0,
                                                                                          0.4,
                                                                                          50.5 * IO::SDK::Constants::DEG_RAD,
                                                                                          42.0 * IO::SDK::Constants::DEG_RAD,
                                                                                          10.0 * IO::SDK::Constants::DEG_RAD,
                                                                                          0.0,
                                                                                          startEpoch,
                                                                                          IO::SDK::Frames::InertialFrames::GetICRF());
    //Compute launch windows
    IO::SDK::Maneuvers::Launch launch(ls, ls, true, *parkingOrbit);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(startEpoch.ToUTC(), endEpoch.ToUTC()));

    //Display launch window results
    std::cout << "========== LAUNCH ==========" << std::endl;

    std::cout << "Launch epoch :" << windows[0].GetWindow().GetStartDate().ToString().c_str() << std::endl;
    std::cout << "Inertial azimuth :" << windows[0].GetInertialAzimuth() * IO::SDK::Constants::RAD_DEG << " °" << std::endl;
    std::cout << "Non inertial azimuth :" << windows[0].GetNonInertialAzimuth() * IO::SDK::Constants::RAD_DEG << " °" << std::endl;
    std::cout << "Inertial insertion velocity :" << windows[0].GetInertialInsertionVelocity() << " m/s" << std::endl;
    std::cout << "Non inertial insertion velocity :" << windows[0].GetNonInertialInsertionVelocity() << " m/s" << std::endl;

    //===================Compute maneuvers to reach target body================================

    //Configure spacecraft
    IO::SDK::Body::Spacecraft::Spacecraft spacecraft{-1, "MySpacecraft", 1000.0, 10000.0, "mission01", std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(*parkingOrbit)};
    spacecraft.AddFuelTank("fuelTank1", 9000.0, 9000.0);
    spacecraft.AddEngine("serialNumber1", "engine1", "fuelTank1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);

    //Configure propagator
    auto step{IO::SDK::Time::TimeSpan(1.0s)};

    //Add gravity to forces model
    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};
    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);

    //Initialize integrator
    IO::SDK::Integrators::VVIntegrator integrator(step, forces);

    //Configure propagator
    IO::SDK::Propagators::Propagator propagator(spacecraft, integrator, IO::SDK::Time::Window(startEpoch, endEpoch));

    //We define which engines can be used to realize maneuvers
    auto engine1 = spacecraft.GetEngine("serialNumber1");
    std::vector<IO::SDK::Body::Spacecraft::Engine> engines;
    engines.push_back(*engine1);

    //We configre each maneuver
    IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver planeAlignment(engines, propagator, targetOrbit.get());
    IO::SDK::Maneuvers::ApogeeHeightChangingManeuver apogeeChange(engines, propagator, targetOrbit->GetApogeeVector().Magnitude());
    IO::SDK::Maneuvers::ApsidalAlignmentManeuver apsidalAlignment(engines, propagator, targetOrbit.get());
    IO::SDK::Maneuvers::PhasingManeuver phasing(engines, propagator, 2, targetOrbit.get());
    IO::SDK::Maneuvers::ApogeeHeightChangingManeuver finalApogeeChanging(engines, propagator, targetOrbit->GetApogeeVector().Magnitude());

    //We link maneuvers
    planeAlignment.SetNextManeuver(apsidalAlignment).SetNextManeuver(phasing).SetNextManeuver(apogeeChange);

    //We define the first maneuver in standby
    propagator.SetStandbyManeuver(&planeAlignment);

    propagator.Propagate();

    auto e = targetOrbit->GetStateVector().GetEccentricity();

    auto ti = targetOrbit->GetInclination() * IO::SDK::Constants::RAD_DEG;
    auto si = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetInclination() * IO::SDK::Constants::RAD_DEG;

    auto tom = targetOrbit->GetRightAscendingNodeLongitude() * IO::SDK::Constants::RAD_DEG;
    auto som = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetRightAscendingNodeLongitude() * IO::SDK::Constants::RAD_DEG;

    auto tecc = targetOrbit->GetEccentricity();
    auto secc = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetEccentricity();

    auto tw = targetOrbit->GetPeriapsisArgument() * IO::SDK::Constants::RAD_DEG;
    auto sw = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetPeriapsisArgument() * IO::SDK::Constants::RAD_DEG;

    auto tq = targetOrbit->GetPerigeeVector().Magnitude();
    auto sq = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetPerigeeVector().Magnitude();
    
    auto tm = targetOrbit->GetMeanAnomaly(endEpoch) * IO::SDK::Constants::RAD_DEG;
    auto sm = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetMeanAnomaly() * IO::SDK::Constants::RAD_DEG;

    auto tv = targetOrbit->GetTrueAnomaly(endEpoch) * IO::SDK::Constants::RAD_DEG;
    auto sv = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch, *earth).GetTrueAnomaly() * IO::SDK::Constants::RAD_DEG;
    
}