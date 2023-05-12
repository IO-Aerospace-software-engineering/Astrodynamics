#include<gtest/gtest.h>
#include<ScenarioDTO.h>
#include "TestParameters.h"
#include "Proxy.h"
#include "Constants.h"
#include "DataPoolMonitoring.h"
#include "InertialFrames.h"
#include <KernelsLoader.h>
#include <Converters.cpp>

TEST(API, DTOSize)
{
    auto size2 = sizeof(IO::SDK::API::DTO::ScenarioDTO);
    ASSERT_EQ(18816, size2);
}

TEST(API, TDBToString)
{
    auto res = TDBToStringProxy(0.0);
    ASSERT_STREQ("2000-01-01 12:00:00.000000 (TDB)", res);
}

TEST(API, UTCToString)
{
    auto res = UTCToStringProxy(0.0);
    ASSERT_STREQ("2000-01-01 12:00:00.000000 (UTC)", res);
}

TEST(API, SitePropagation)
{
    IO::SDK::API::DTO::ScenarioDTO scenario{};
    scenario.Name = "scenatiosites";
    scenario.Window.start = 668085625.01523638;
    scenario.Window.end = 668174330.814560;
    scenario.CelestialBodies[0].id = 399;
    scenario.CelestialBodies[0].centerOfMotionId = 10;
    scenario.CelestialBodies[1].id = 10;
    scenario.Sites[0].id = 399033;
    scenario.Sites[0].name = "S33";
    std::string sitePath(SitePath);
    scenario.Sites[0].directoryPath = sitePath.c_str();
    scenario.Sites[0].bodyId = 399;
    scenario.Sites[0].coordinates.longitude = -1.4137166941154069;
    scenario.Sites[0].coordinates.latitude = 0.49741883681838395;
    scenario.Sites[0].coordinates.altitude = 0.0;
    scenario.Spacecraft.id = -1111;
    scenario.Spacecraft.name = "spc1";
    scenario.Spacecraft.dryOperatingMass = 1000.0;
    scenario.Spacecraft.maximumOperatingMass = 3000.0;
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.id = 399;
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.initialOrbitalParameter.epoch = 668085625.01523638;
    scenario.Spacecraft.initialOrbitalParameter.inertialFrame = "J2000";
    scenario.Spacecraft.initialOrbitalParameter.position.x = 6800.0;
    scenario.Spacecraft.initialOrbitalParameter.position.y = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.position.z = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.x = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.y = 8.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.z = 0.0;

    std::string spacecraftPath(SpacecraftPath);
    scenario.Spacecraft.directoryPath = spacecraftPath.c_str();

    PropagateProxy(scenario);
}

TEST(API, SpacecraftPropagation)
{
    IO::SDK::API::DTO::ScenarioDTO scenario{};
    scenario.Name = "scenatiosites";
    scenario.Window.start = 668085555.829810;
    scenario.Window.end = 668174400.000000;
    scenario.CelestialBodies[0].id = 10;
    scenario.CelestialBodies[1].id = 399;
    scenario.CelestialBodies[1].centerOfMotionId = 10;
    scenario.CelestialBodies[2].id = 301;
    scenario.CelestialBodies[2].centerOfMotionId = 399;
    scenario.Spacecraft.id = -1111;
    scenario.Spacecraft.name = "spc1";
    scenario.Spacecraft.dryOperatingMass = 1000.0;
    scenario.Spacecraft.maximumOperatingMass = 10000.0;
    std::string spacecraftPath(SpacecraftPath);
    scenario.Spacecraft.directoryPath = spacecraftPath.c_str();
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.id = 399;
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.initialOrbitalParameter.epoch = 667915269.18539762;
    scenario.Spacecraft.initialOrbitalParameter.inertialFrame = "J2000";
    scenario.Spacecraft.initialOrbitalParameter.position.x = 5056554.1874925727;
    scenario.Spacecraft.initialOrbitalParameter.position.y = 4395595.4942363985;
    scenario.Spacecraft.initialOrbitalParameter.position.z = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.x = -3708.6305608890916;
    scenario.Spacecraft.initialOrbitalParameter.velocity.y = 4266.2914313011433;
    scenario.Spacecraft.initialOrbitalParameter.velocity.z = 6736.8538488755494;

    scenario.Spacecraft.fuelTank[0].id = 1;
    scenario.Spacecraft.fuelTank[0].serialNumber = "ft1";
    scenario.Spacecraft.fuelTank[0].capacity = 9000;
    scenario.Spacecraft.fuelTank[0].quantity = 9000;

    scenario.Spacecraft.engines[0].id = 1;
    scenario.Spacecraft.engines[0].name = "eng1";
    scenario.Spacecraft.engines[0].serialNumber = "eng1";
    scenario.Spacecraft.engines[0].fuelTankSerialNumber = "ft1";
    scenario.Spacecraft.engines[0].fuelflow = 50.0;
    scenario.Spacecraft.engines[0].isp = 450.0;

    scenario.Spacecraft.payloads[0].serialNumber = "pl1";
    scenario.Spacecraft.payloads[0].name = "pl1";
    scenario.Spacecraft.payloads[0].mass = 50;

    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.x = 4390853.7278876612;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.y = 5110607.0005866792;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.z = 917659.86391987884;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.x = -4979.4693432656513;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.y = 3033.2639866911495;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.z = 6933.1803797017265;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.centerOfMotion.id = 399;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.epoch = 667915269.18539762;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.inertialFrame = "J2000";
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].engines[0] = "eng1";
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].maneuverOrder = 0;

    PropagateProxy(scenario);

    IO::SDK::Time::TDB tdbStart(std::chrono::duration<double>(scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].thrustWindow.start));
    IO::SDK::Time::TDB tdbEnd(std::chrono::duration<double>(scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].thrustWindow.end));
    ASSERT_STREQ("2021-03-04 00:31:35.852044 (TDB)", tdbStart.ToString().c_str());
    ASSERT_STREQ("2021-03-04 00:31:44.178429 (TDB)", tdbEnd.ToString().c_str());
}

TEST(API, FindWindowsOnCoordinateConstraintProxy)
{
    IO::SDK::API::DTO::WindowDTO windows[1000];
    IO::SDK::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 730036800.0;
    searchWindow.end = 730123200;
    FindWindowsOnCoordinateConstraintProxy(searchWindow, 399013, 301, "DSS-13_TOPO", "LATITUDINAL", "LATITUDE", ">", 0.0, 0.0, "NONE", 60.0,
                                           windows);

    ASSERT_STREQ("2023-02-19 14:33:08.918098 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2023-02-19 23:58:50.814787 (UTC)", ToTDBWindow(windows[0]).GetEndDate().ToUTC().ToString().c_str());
}

TEST(API, FindWindowsOnDistanceConstraintProxy)
{
    IO::SDK::API::DTO::WindowDTO windows[1000];
    IO::SDK::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::SDK::Time::TDB("2007 JAN 1").GetSecondsFromJ2000().count();
    searchWindow.end = IO::SDK::Time::TDB("2007 APR 1").GetSecondsFromJ2000().count();;
    FindWindowsOnDistanceConstraintProxy(searchWindow, 399, 301, ">", 400000000, "NONE", 86400.0, windows);

    ASSERT_STREQ("2007-01-08 00:11:07.628591 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-01-13 06:37:47.948144 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());
    ASSERT_STREQ("2007-03-29 22:53:58.151896 (TDB)", ToTDBWindow(windows[3]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-04-01 00:01:05.185654 (TDB)", ToTDBWindow(windows[3]).GetEndDate().ToString().c_str());
}

TEST(API, FindWindowsOnIlluminationConstraintProxy)
{
    IO::SDK::API::DTO::WindowDTO windows[1000];
    IO::SDK::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::SDK::Time::TDB("2021-05-17 12:00:00 TDB").GetSecondsFromJ2000().count();
    searchWindow.end = IO::SDK::Time::TDB("2021-05-18 12:00:00 TDB").GetSecondsFromJ2000().count();
    IO::SDK::API::DTO::GeodeticDTO geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0);
    FindWindowsOnIlluminationConstraintProxy(searchWindow, 10, "SUN", 399, "IAU_EARTH",
                                             geodetic, "INCIDENCE", "<",
                                             IO::SDK::Constants::PI2 - IO::SDK::Constants::OfficialTwilight, 0.0, "CN+S", 4.5 * 60 * 60, "Ellipsoid", windows);

    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", ToTDBWindow(windows[0]).GetEndDate().ToUTC().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", ToTDBWindow(windows[1]).GetStartDate().ToUTC().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", ToTDBWindow(windows[1]).GetEndDate().ToString().c_str());
}

TEST(API, FindWindowsOnOccultationConstraintProxy)
{
    IO::SDK::API::DTO::WindowDTO windows[1000];
    IO::SDK::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::SDK::Time::TDB("2001 DEC 13").GetSecondsFromJ2000().count();
    searchWindow.end = IO::SDK::Time::TDB("2001 DEC 15").GetSecondsFromJ2000().count();
    FindWindowsOnOccultationConstraintProxy(searchWindow,399,10,"IAU_SUN","ELLIPSOID",301,"IAU_MOON","ELLIPSOID","ANY","LT",3600.0,windows);

    ASSERT_STREQ("2001-12-14 20:10:15.410588 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2001-12-14 21:35:49.100520 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());
}



TEST(API, FindWindowsInFieldOfViewConstraintProxy)
{
    IO::SDK::Math::Vector3D orientation{1.0, 0.0, 0.0};
    IO::SDK::Math::Vector3D boresight{0.0, 0.0, 1.0};
    IO::SDK::Math::Vector3D fovvector{1.0, 0.0, 0.0};

    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399,sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301,earth);
    double a = 6800000.0;
    auto v = std::sqrt(earth->GetMu() / a);
    IO::SDK::Time::TDB epoch("2021-JUN-10 00:00:00.0000 TDB");

    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::SDK::Math::Vector3D(a, 0.0, 0.0),
                                                                                                                                             IO::SDK::Math::Vector3D(0.0, v, 0.0),
                                                                                                                                             epoch,
                                                                                                                                             IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-179, "SC179", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    s.AddCircularFOVInstrument(789, "CAMERA789", orientation, boresight, fovvector, 1.5);
    const IO::SDK::Instruments::Instrument *instrument{s.GetInstrument(789)};

    //==========PROPAGATOR====================
    auto step{IO::SDK::Time::TimeSpan(1.0s)};
    IO::SDK::Time::TimeSpan duration(6447.0s);

    std::vector<IO::SDK::Integrators::Forces::Force *> forces{};

    IO::SDK::Integrators::Forces::GravityForce gravityForce;
    forces.push_back(&gravityForce);
    IO::SDK::Integrators::VVIntegrator integrator(step, forces);

    IO::SDK::Propagators::Propagator pro(s, integrator, IO::SDK::Time::Window(epoch, epoch + duration));

    pro.Propagate();

    IO::SDK::API::DTO::WindowDTO windows[1000];
    IO::SDK::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::SDK::Time::TDB("2021-JUN-10 00:00:00.0000 TDB").GetSecondsFromJ2000().count();
    searchWindow.end = IO::SDK::Time::TDB("2021-JUN-10 01:47:27.0000 TDB").GetSecondsFromJ2000().count();
    FindWindowsInFieldOfViewConstraintProxy(searchWindow,-179,-179789,399,"IAU_EARTH","ELLIPSOID","LT",3600,windows);

    ASSERT_STREQ("2021-06-10 00:00:00.000000 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-06-10 00:30:12.460950 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());

    ASSERT_STREQ("2021-06-10 01:02:53.845054 (TDB)", ToTDBWindow(windows[1]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-06-10 01:47:27.000000 (TDB)", ToTDBWindow(windows[1]).GetEndDate().ToString().c_str());
}