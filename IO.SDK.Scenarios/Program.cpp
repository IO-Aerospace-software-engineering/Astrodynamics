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

    //Define parking orbit
    auto parkingOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                           6700000.0,
                                                                                           0.3,
                                                                                           50.0 * IO::SDK::Constants::DEG_RAD,
                                                                                           53.90 * IO::SDK::Constants::DEG_RAD,
                                                                                           92.34 * IO::SDK::Constants::DEG_RAD,
                                                                                           0.0,
                                                                                           IO::SDK::Time::TDB("2021-06-02T00:00:00"),
                                                                                           IO::SDK::Frames::InertialFrames::GetICRF());
    //Define target orbit
    auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                          6720000.0,
                                                                                          0.3,
                                                                                          50.3 * IO::SDK::Constants::DEG_RAD,
                                                                                          50.0 * IO::SDK::Constants::DEG_RAD,
                                                                                          90.0 * IO::SDK::Constants::DEG_RAD,
                                                                                          0.0,
                                                                                          IO::SDK::Time::TDB("2021-06-02T00:00:00"),
                                                                                          IO::SDK::Frames::InertialFrames::GetICRF());
    //Compute launch windows
    IO::SDK::Maneuvers::Launch launch(ls, ls, true, *parkingOrbit);
    auto windows = launch.GetLaunchWindows(IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC("2021-06-02T00:00:00"), IO::SDK::Time::UTC("2021-06-03T00:00:00")));

    //Display launch window results
    std::cout << "========== LAUNCH ==========" << std::endl;

    std::cout << "Launch epoch :" << windows[0].GetWindow().GetStartDate().ToString().c_str() << std::endl;
    std::cout << "Inertial azimuth :" << windows[0].GetInertialAzimuth() * IO::SDK::Constants::RAD_DEG << " °" << std::endl;
    std::cout << "Non inertial azimuth :" << windows[0].GetNonInertialAzimuth() * IO::SDK::Constants::RAD_DEG << " °" << std::endl;
    std::cout << "Inertial insertion velocity :" << windows[0].GetInertialInsertionVelocity() << " m/s" << std::endl;
    std::cout << "Non inertial insertion velocity :" << windows[0].GetNonInertialInsertionVelocity() << " m/s" << std::endl;

    //===================Compute maneuvers to reach target body================================

    //Configure spacecraft
    IO::SDK::Body::Spacecraft::Spacecraft spacecraft{-1, "MySpacecraft", 1000.0, 3000.0, "mission01", std::make_unique<IO::SDK::OrbitalParameters::ConicOrbitalElements>(*parkingOrbit)};
    spacecraft.AddFuelTank("fuelTank1", 2000.0, 1000.0);
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
    IO::SDK::Time::UTC startEpoch("2021-06-02T00:00:00");
    IO::SDK::Time::UTC endEpoch("2021-06-02T13:00:00");
    IO::SDK::Propagators::Propagator propagator(spacecraft, integrator, IO::SDK::Time::Window(startEpoch.ToTDB(), endEpoch.ToTDB()));

    //Configure maneuvers

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
    planeAlignment.SetNextManeuver(apogeeChange).SetNextManeuver(apsidalAlignment);
    // planeAlignment.SetNextManeuver(apogeeChange).SetNextManeuver(apsidalAlignment).SetNextManeuver(phasing).SetNextManeuver(finalApogeeChanging);

    //We define the first maneuver in standby
    propagator.SetStandbyManeuver(&planeAlignment);

    propagator.Propagate();

    auto e = targetOrbit->GetStateVector().GetEccentricity();

    auto ti = targetOrbit->GetInclination() * IO::SDK::Constants::RAD_DEG;
    auto si = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch.ToTDB(), *earth).GetInclination() * IO::SDK::Constants::RAD_DEG;

    auto tom = targetOrbit->GetRightAscendingNodeLongitude() * IO::SDK::Constants::RAD_DEG;
    auto som = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch.ToTDB(), *earth).GetRightAscendingNodeLongitude() * IO::SDK::Constants::RAD_DEG;

    auto tecc = targetOrbit->GetEccentricity();
    auto secc = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch.ToTDB(), *earth).GetEccentricity();

    auto tw = targetOrbit->GetPeriapsisArgument() * IO::SDK::Constants::RAD_DEG;
    auto sw = spacecraft.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, endEpoch.ToTDB(), *earth).GetPeriapsisArgument() * IO::SDK::Constants::RAD_DEG;
    //     2459368.250000000 = A.D. 2021-Jun-02 18:00:00.0000 TDB
    //  EC= 1.645965536521140E-03 QR= 6.781987029515979E+03 IN= 5.172225994694696E+01
    //  OM= 5.390581000057843E+01 W = 9.234298392810511E+01 Tp=  2459368.280228322838
    //  N = 6.460750212192097E-02 MA= 1.912628368599871E+02 TA= 1.912260728883235E+02
    //  A = 6.793168350504695E+03 AD= 6.804349671493411E+03 PR= 5.572108318328778E+03

    //first launch window
    // ASSERT_STREQ("2021-06-02 18:08:00.980377 (UTC)", windows[0].GetWindow().GetStartDate().ToString().c_str());
    // ASSERT_EQ(3, windows[0].GetLaunchSite()->GetId());
    // ASSERT_DOUBLE_EQ(44.906290078823638, windows[0].GetInertialAzimuth() * IO::SDK::Constants::RAD_DEG);
    // ASSERT_DOUBLE_EQ(42.657119977138009, windows[0].GetNonInertialAzimuth() * IO::SDK::Constants::RAD_DEG);
    // ASSERT_DOUBLE_EQ(7665.2355903714715, windows[0].GetInertialInsertionVelocity());
    // ASSERT_DOUBLE_EQ(7382.1537527826185, windows[0].GetNonInertialInsertionVelocity());
}