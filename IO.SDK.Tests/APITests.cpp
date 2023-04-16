#include<gtest/gtest.h>
#include<ScenarioDTO.h>
#include "TestParameters.h"
#include "Proxy.h"

TEST(API, DTOSize)
{
    auto size2 = sizeof(IO::SDK::API::DTO::ScenarioDTO);
    ASSERT_EQ(18816, size2);
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