#include<gtest/gtest.h>
#include<ScenarioDTO.h>
#include "TestParameters.h"
#include "Proxy.h"
#include <GenericKernelsLoader.h>

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
    std::string s(SolarSystemKernelPath);
    LoadGenericKernelsProxy(s.c_str());
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
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].maneuverOrder= 0;

    PropagateProxy(scenario);

    IO::SDK::Time::TDB tdbStart(std::chrono::duration<double>(scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].thrustWindow.start));
    IO::SDK::Time::TDB tdbEnd(std::chrono::duration<double>(scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].thrustWindow.end));
    ASSERT_STREQ("2021-03-04 00:31:35.852044 (TDB)",tdbStart.ToString().c_str());
    ASSERT_STREQ("2021-03-04 00:31:44.178429 (TDB)",tdbEnd.ToString().c_str());
}