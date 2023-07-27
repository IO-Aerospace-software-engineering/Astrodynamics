/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <iostream>
#include <TLE.h>
#include <Launch.h>
#include <OrbitalPlaneChangingManeuver.h>
#include <ApogeeHeightChangingManeuver.h>
#include <ApsidalAlignmentManeuver.h>
#include <PhasingManeuver.h>
#include <Scenario.h>
#include "InertialFrames.h"
#include <KernelsLoader.h>

using namespace std::literals::chrono_literals;

inline constexpr std::string_view SpacecraftPath = "Data/User/Spacecrafts";
inline constexpr std::string_view SolarSystemKernelPath = "Data/SolarSystem";
inline constexpr std::string_view SitePath = "Data/User/Sites";

void DisplayManeuverSummary(IO::Astrodynamics::Maneuvers::ManeuverBase *maneuver, const std::string &title)
{
    std::cout << "======================================== " << title << " ========================================" << std::endl;
    std::cout << "Maneuver window : " << maneuver->GetManeuverWindow()->GetStartDate().ToString() << " => " << maneuver->GetManeuverWindow()->GetEndDate().ToString() << std::endl;
    std::cout << "Thrust window : " << maneuver->GetThrustWindow()->GetStartDate().ToString() << " => " << maneuver->GetThrustWindow()->GetEndDate().ToString() << std::endl;
    std::cout << "Thrust duration : " << maneuver->GetThrustWindow()->GetLength().GetSeconds().count() << " s" << std::endl;
    std::cout << "Delta V - X : " << maneuver->GetDeltaV().GetX() << " m/s" << std::endl;
    std::cout << "Delta V - Y : " << maneuver->GetDeltaV().GetY() << " m/s" << std::endl;
    std::cout << "Delta V - Z : " << maneuver->GetDeltaV().GetZ() << " m/s" << std::endl;
    std::cout << "Delta V Magnitude : " << maneuver->GetDeltaV().Magnitude() << " m/s" << std::endl;
    auto v = maneuver->GetDeltaV().Normalize();
    std::cout << "Spacecraft orientation : X : " << v.GetX() << " Y : " << v.GetY() << " Z : " << v.GetZ() << " ( ICRF )" << std::endl;
    std::cout << "Fuel burned :" << maneuver->GetFuelBurned() << " kg" << std::endl;
    std::cout << std::endl;
}

void DisplayLaunchWindowsSummary(const std::vector<IO::Astrodynamics::Maneuvers::LaunchWindow> &launchWindows)
{
    for (size_t i = 0; i < launchWindows.size(); i++)
    {
        std::cout << "========================================"
                  << "Launch Window " << i << " ========================================" << std::endl;
        std::cout << "Launch epoch :" << launchWindows[i].GetWindow().GetStartDate().ToString().c_str() << std::endl;
        std::cout << "Inertial azimuth :" << launchWindows[i].GetInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG << " °" << std::endl;
        std::cout << "Non inertial azimuth :" << launchWindows[i].GetNonInertialAzimuth() * IO::Astrodynamics::Constants::RAD_DEG << " °" << std::endl;
        std::cout << "Inertial insertion velocity :" << launchWindows[i].GetInertialInsertionVelocity() << " m/s" << std::endl;
        std::cout << "Non inertial insertion velocity :" << launchWindows[i].GetNonInertialInsertionVelocity() << " m/s" << std::endl;
        std::cout << std::endl;
    }
}

void DisplayOccultations(const std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> &occultations)
{
    std::cout << "========================================"
              << " Sun occultations from chaser Spacecraft"
              << " ========================================" << std::endl;
    for (const auto &occultation: occultations)
    {
        std::cout << "Occulation start at :" << occultation.GetStartDate().ToString().c_str() << std::endl;
        std::cout << "Occulation end at :" << occultation.GetEndDate().ToString().c_str() << std::endl;
        std::cout << std::endl;
    }
}

void DisplayInsight(const std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> &windows)
{
    std::cout << "========================================"
              << " Windows when the moon is in camera's field of view"
              << " ========================================" << std::endl;
    for (const auto &window: windows)
    {
        std::cout << "Opportunity start at :" << window.GetStartDate().ToString().c_str() << std::endl;
        std::cout << "Opportunity end at :" << window.GetEndDate().ToString().c_str() << std::endl;
        std::cout << std::endl;
    }
}

int main()
{
    /*========================== Scenario Description =====================================
    We are at Cap canaveral and we have to join another Spacecraft in orbit.
    The launch must occurs by day at launch site and recovery site
    To realize this operation, we'll show you how to use IO SDK to find launch windows then maneuvers sequence to reach our objective.
    For each maneuver you will obtain the maneuver window, the thrust window, Delta V, Spacecraft or satellite orientation and mass of fuel burned.
    We also get sun occultations and windows when the moon will be in camera's field of view
    */

    //Load generic kernel (leap second, barycenters, major bodies,...)
    IO::Astrodynamics::Kernels::KernelsLoader::Load(std::string(SolarSystemKernelPath));

    IO::Astrodynamics::Time::TDB startEpoch("2021-03-02T00:00:00");
    IO::Astrodynamics::Time::TDB endEpoch("2021-03-05T00:00:00");
    IO::Astrodynamics::Scenario scenario("scenario1", IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(startEpoch.ToUTC(), endEpoch.ToUTC()));
    //=======================Configure universe topology======================================
    //Bodies id are defined here https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/naif_ids.html#NAIF%20Object%20ID%20numbers
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399,sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301,earth);

    //========================Compute launch parameters=======================================

    //Define launch site and recovery site
    auto launchSite = std::make_shared<IO::Astrodynamics::Sites::LaunchSite>(399003, "S3",
                                                                   IO::Astrodynamics::Coordinates::Planetodetic(-81.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                                   earth, std::string(SitePath));
    auto recoverySite = std::make_shared<IO::Astrodynamics::Sites::Site>(399004, "S4",
                                                               IO::Astrodynamics::Coordinates::Planetodetic(-80.0 * IO::Astrodynamics::Constants::DEG_RAD, 28.5 * IO::Astrodynamics::Constants::DEG_RAD, 0.0),
                                                               earth, std::string(SitePath));

    //Define simulation window. (Warning : When Spacecraft is involved, dates must be greater than 2021-01-01 to be compliant with Spacecraft clock)


    //Define parking orbit
    auto parkingOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                           6700000.0,
                                                                                           0.3,
                                                                                           50.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                           41.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                           0.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                           0.0,
                                                                                           startEpoch,
                                                                                                     IO::Astrodynamics::Frames::InertialFrames::ICRF());

    //Define orbit of the target
    auto targetOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                          6800000.0,
                                                                                          0.4,
                                                                                          51.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                          43.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                          10.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                          0.0,
                                                                                          startEpoch,
                                                                                                    IO::Astrodynamics::Frames::InertialFrames::ICRF());
    auto pk = parkingOrbit->ToStateVector();
    auto tb = targetOrbit->ToStateVector();
    //Compute launch windows, to launch by day on launch site and recovery site when the launch site crosses the parking orbital plane
    IO::Astrodynamics::Maneuvers::Launch launch(*launchSite, *recoverySite, true, *parkingOrbit);
    auto launchWindows = launch.GetLaunchWindows(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(startEpoch.ToUTC(), endEpoch.ToUTC()));

    //Display launch window results (this is not necessary)
    DisplayLaunchWindowsSummary(launchWindows);

    //===================Compute maneuvers to reach target body================================

    //Configure Spacecraft at insertion orbit
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spacecraft{-178, "DRAGONFLY", 1000.0, 10000.0, "MIS01",
                                                     std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(*parkingOrbit)};
    spacecraft.AddFuelTank("fuelTank1", 9000.0, 9000.0);                                                          // Add fuel tank
    spacecraft.AddEngine("serialNumber1", "engine1", "fuelTank1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0); //Add engine and link with fuel tank
    spacecraft.AddPayload("PAY01", "Payload 01", 50.0);                                                           //We add a 50 kg payload to the Spacecraft

    //We add an instrument with a circular field of view  aligned with the Spacecraft Z axis
    IO::Astrodynamics::Math::Vector3D orientation{IO::Astrodynamics::Constants::PI, 0.0, 0.0};
    IO::Astrodynamics::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::Astrodynamics::Math::Vector3D fovvector{1.0, 0.0, 0.0};
    spacecraft.AddCircularFOVInstrument(-178600, "CAM600", orientation, boresight, fovvector, 20.0 * IO::Astrodynamics::Constants::DEG_RAD);

    //Target
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spacecraftTarget{-179, "TARGET", 1000.0, 10000.0, "MIS01",
                                                           std::make_unique<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(*targetOrbit)};
    spacecraftTarget.AddFuelTank("fuelTank2", 9000.0, 9000.0);                                                          // Add fuel tank
    spacecraftTarget.AddEngine("serialNumber2", "engine2", "fuelTank2", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0); //Add engine and link with fuel tank

    //Configure propagator
    auto step{IO::Astrodynamics::Time::TimeSpan(1.0s)};

    //Add gravity to forces model
    //You can add your own force model
    std::vector<IO::Astrodynamics::Integrators::Forces::Force *> forces{};
    IO::Astrodynamics::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);

    //Initialize an integrator
    IO::Astrodynamics::Integrators::VVIntegrator integrator(step, forces);

    //We assume the ship will be in orbit 10 minutes after launch.
    IO::Astrodynamics::Time::TDB startDatePropagator = launchWindows[0].GetWindow().GetStartDate().ToTDB().Add(IO::Astrodynamics::Time::TimeSpan(600.0s));

    //Initialize propagator for dragonfly Spacecraft
    IO::Astrodynamics::Propagators::Propagator propagator(spacecraft, integrator, IO::Astrodynamics::Time::Window(startDatePropagator, endEpoch));

    //Initialize an integrator
    IO::Astrodynamics::Integrators::VVIntegrator targetIntegrator(step, forces);
    //Intialize propagator for target Spacecraft
    IO::Astrodynamics::Propagators::Propagator targetPropagator(spacecraftTarget, targetIntegrator, IO::Astrodynamics::Time::Window(startDatePropagator, endEpoch));
    targetPropagator.Propagate();

    //We define which engines can be used to realize maneuvers
    auto engine1 = spacecraft.GetEngine("serialNumber1");
    std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines;
    engines.push_back(const_cast<IO::Astrodynamics::Body::Spacecraft::Engine *>(engine1));

    //We configure each maneuver
    IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver planeAlignment(engines, propagator, targetOrbit,
                                                                    startDatePropagator); //The first maneuver must not start until the launch is complete
    IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver apsidalAlignment(engines, propagator, targetOrbit);
    IO::Astrodynamics::Maneuvers::PhasingManeuver phasing(engines, propagator, 1, targetOrbit);
    IO::Astrodynamics::Maneuvers::ApogeeHeightChangingManeuver finalApogeeChanging(engines, propagator, targetOrbit->GetApogeeVector().Magnitude());

    //We order maneuvers
    planeAlignment.SetNextManeuver(apsidalAlignment).SetNextManeuver(phasing).SetNextManeuver(finalApogeeChanging);

    //We set the first maneuver in standby
    propagator.SetStandbyManeuver(&planeAlignment);

    //We execute the propagator
    propagator.Propagate();

    //Find sun occultation
    auto occultationWindows = spacecraft.FindWindowsOnOccultationConstraint(IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(startDatePropagator, endEpoch), *sun, *earth,
                                                                            IO::Astrodynamics::OccultationType::Any(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TimeSpan(30s));

    //Find when moon will be in instrument field of view
    auto fovWindows = spacecraft.GetInstrument(-178600)->FindWindowsWhereInFieldOfView(
            IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(startDatePropagator, spacecraft.GetOrientationsCoverageWindow().GetEndDate()), *moon, IO::Astrodynamics::AberrationsEnum::LT,
            IO::Astrodynamics::Time::TimeSpan(300s));

    //From here Only for data vizualization
//    auto epoch = finalApogeeChanging.GetManeuverWindow()->GetEndDate();
//
//    auto ephemeris = Spacecraft.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::AberrationsEnum::None, epoch, *earth);
//    auto e = targetOrbit->ToStateVector().GetEccentricity();
//    auto ti = targetOrbit->GetInclination() * IO::Astrodynamics::Constants::RAD_DEG;
//    auto si = ephemeris.GetInclination() * IO::Astrodynamics::Constants::RAD_DEG;
//
//    auto tom = targetOrbit->GetRightAscendingNodeLongitude() * IO::Astrodynamics::Constants::RAD_DEG;
//    auto som = ephemeris.GetRightAscendingNodeLongitude() * IO::Astrodynamics::Constants::RAD_DEG;
//
//    auto tecc = targetOrbit->GetEccentricity();
//    auto secc = ephemeris.GetEccentricity();
//
//    auto tw = targetOrbit->ToStateVector(epoch).GetPeriapsisArgument() * IO::Astrodynamics::Constants::RAD_DEG;
//    auto sw = ephemeris.GetPeriapsisArgument() * IO::Astrodynamics::Constants::RAD_DEG;
//
//    auto tq = targetOrbit->ToStateVector(epoch).GetPerigeeVector().Magnitude();
//    auto sq = ephemeris.GetPerigeeVector().Magnitude();
//
//    auto tm = targetOrbit->ToStateVector(epoch).GetMeanLongitude() * IO::Astrodynamics::Constants::RAD_DEG;
//    auto sm = ephemeris.GetMeanLongitude() * IO::Astrodynamics::Constants::RAD_DEG;
//
//    auto tv = targetOrbit->ToStateVector(epoch).GetTrueLongitude() * IO::Astrodynamics::Constants::RAD_DEG;
//    auto sv = ephemeris.GetTrueLongitude() * IO::Astrodynamics::Constants::RAD_DEG;
//
//    auto tp = targetOrbit->ToStateVector(epoch).GetPeriod().GetHours().count();
//    auto sp = ephemeris.GetPeriod().GetHours().count();
//
//    auto ta = targetOrbit->ToStateVector(epoch).GetSemiMajorAxis();
//    auto sa = ephemeris.GetSemiMajorAxis();
//
//    double dpos = (ephemeris.GetPosition() - targetOrbit->ToStateVector(epoch).GetPosition()).Magnitude();
//    double period = ephemeris.GetPeriod().GetHours().count();

    DisplayManeuverSummary(&planeAlignment, "Plane alignment");
    DisplayManeuverSummary(&apsidalAlignment, "Aspidal alignment");
    DisplayManeuverSummary(&phasing, "Phasing");
    DisplayManeuverSummary(&finalApogeeChanging, "Apogee height changing");
    DisplayOccultations(occultationWindows);
    DisplayInsight(fovWindows);
}