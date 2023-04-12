#include<gtest/gtest.h>
#include<ScenarioDTO.h>
#include <Proxy.h>

TEST(API, DTOSize)
{
//    auto size= sizeof(IO::SDK::API::DTO::ApogeeHeightChangingManeuverDTO);
//    ASSERT_EQ(2160664,size);
    auto size2 = sizeof(IO::SDK::API::DTO::ScenarioDTO);
    ASSERT_EQ(814712, size2);
}

TEST(API, Propagate)
{
    IO::SDK::API::DTO::ScenarioDTO scenario;
    scenario.celestialBodies[0].id = 399;
    scenario.celestialBodies[0].centerOfMotionId = 10;
    scenario.celestialBodies[1].id = 10;
    scenario.Name = "titi";
    scenario.Window.start = 10.0;
    scenario.Window.end = 20.0;
    scenario.spacecraft.fuelTank[0].id = 1;
    scenario.spacecraft.fuelTank[0].capacity = 1000.0;
    scenario.spacecraft.fuelTank[0].quantity = 1000.0;
    scenario.spacecraft.fuelTank[0].serialNumber = "ft1";
    scenario.spacecraft.engines[0].id=1;
    scenario.spacecraft.engines[0].serialNumber="eng1";
    scenario.spacecraft.engines[0].fuelTankSerialNumber="ft1";
    scenario.spacecraft.engines[0].fuelflow=50;
    scenario.spacecraft.engines[0].isp=400;
    scenario.spacecraft.engines[0].name="engine1";
    scenario.spacecraft.progradeAttitudes[0].engines[0] = "eng1";
    scenario.spacecraft.progradeAttitudes[0].maneuverOrder = 0;
    scenario.spacecraft.initialOrbitalParameter.centerOfMotion.id = 399;
    scenario.spacecraft.initialOrbitalParameter.centerOfMotion.centerOfMotionId = 10;
    scenario.spacecraft.initialOrbitalParameter.epoch = 15.0;
    scenario.spacecraft.initialOrbitalParameter.inertialFrame = "J2000";
    scenario.spacecraft.initialOrbitalParameter.position.x = 6800.0;
    scenario.spacecraft.initialOrbitalParameter.position.y = 0.0;
    scenario.spacecraft.initialOrbitalParameter.position.z = 0.0;
    scenario.spacecraft.initialOrbitalParameter.velocity.x = 0.0;
    scenario.spacecraft.initialOrbitalParameter.velocity.y = 8.0;
    scenario.spacecraft.initialOrbitalParameter.velocity.z = 0.0;
    scenario.spacecraft.id = -1111;
    scenario.spacecraft.name = "spc1";
    scenario.spacecraft.dryOperatingMass = 1000.0;
    scenario.spacecraft.maximumOperatingMass = 3000.0;
    PropagateProxy(scenario);
}