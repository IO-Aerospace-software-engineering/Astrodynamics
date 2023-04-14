#include<gtest/gtest.h>
#include<ScenarioDTO.h>
#include <Proxy.h>
#include "TestParameters.h"

TEST(API, DTOSize)
{
//    auto size= sizeof(IO::SDK::API::DTO::ApogeeHeightChangingManeuverDTO);
//    ASSERT_EQ(2160664,size);
    auto size2 = sizeof(IO::SDK::API::DTO::ScenarioDTO);
    ASSERT_EQ(810776, size2);
}

TEST(API, BuildScenarioAttitutesWithoutException)
{
    IO::SDK::API::DTO::ScenarioDTO scenario;
    scenario.CelestialBodies[0].id = 399;
    scenario.CelestialBodies[0].centerOfMotionId = 10;
    scenario.CelestialBodies[1].id = 10;
    scenario.Name = std::string(SpacecraftPath).data();
    scenario.Window.start = 10.0;
    scenario.Window.end = 20.0;
    scenario.Spacecraft.id = -1111;
    scenario.Spacecraft.name = "spc1";
    scenario.Spacecraft.dryOperatingMass = 1000.0;
    scenario.Spacecraft.maximumOperatingMass = 3000.0;
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.id = 399;
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.initialOrbitalParameter.epoch = 15.0;
    scenario.Spacecraft.initialOrbitalParameter.inertialFrame = "J2000";
    scenario.Spacecraft.initialOrbitalParameter.position.x = 6800.0;
    scenario.Spacecraft.initialOrbitalParameter.position.y = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.position.z = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.x = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.y = 8.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.z = 0.0;
    scenario.Spacecraft.instruments[0].name = "inst1";
    scenario.Spacecraft.instruments[0].id = 200;
    scenario.Spacecraft.instruments[0].boresight.x = 1.0;
    scenario.Spacecraft.instruments[0].boresight.y = 0.0;
    scenario.Spacecraft.instruments[0].boresight.z = 0.0;
    scenario.Spacecraft.instruments[0].fovRefVector.x = 0.0;
    scenario.Spacecraft.instruments[0].fovRefVector.y = 1.0;
    scenario.Spacecraft.instruments[0].fovRefVector.z = 0.0;
    scenario.Spacecraft.instruments[0].fieldOfView = 3.14;
    scenario.Spacecraft.instruments[0].shape = "circular";
    scenario.Spacecraft.fuelTank[0].id = 1;
    scenario.Spacecraft.fuelTank[0].capacity = 1000.0;
    scenario.Spacecraft.fuelTank[0].quantity = 1000.0;
    scenario.Spacecraft.fuelTank[0].serialNumber = "ft1";
    scenario.Spacecraft.engines[0].id = 1;
    scenario.Spacecraft.engines[0].serialNumber = "eng1";
    scenario.Spacecraft.engines[0].fuelTankSerialNumber = "ft1";
    scenario.Spacecraft.engines[0].fuelflow = 50;
    scenario.Spacecraft.engines[0].isp = 400;
    scenario.Spacecraft.engines[0].name = "engine1";
    scenario.Spacecraft.progradeAttitudes[0].engines[0] = "eng1";
    scenario.Spacecraft.progradeAttitudes[0].maneuverOrder = 0;
    scenario.Spacecraft.retrogradeAttitudes[0].engines[0] = "eng1";
    scenario.Spacecraft.retrogradeAttitudes[0].maneuverOrder = 1;
    scenario.Spacecraft.nadirAttitudes[0].engines[0] = "eng1";
    scenario.Spacecraft.nadirAttitudes[0].maneuverOrder = 2;
    scenario.Spacecraft.zenithAttitudes[0].engines[0] = "eng1";
    scenario.Spacecraft.zenithAttitudes[0].maneuverOrder = 3;
    scenario.Spacecraft.pointingToAttitudes[0].engines[0] = "eng1";
    scenario.Spacecraft.pointingToAttitudes[0].maneuverOrder = 4;
    scenario.Spacecraft.pointingToAttitudes[0].targetBodyId = 399;
    scenario.Spacecraft.pointingToAttitudes[0].instrumentId = 200;


    PropagateProxy(scenario);
}

TEST(API, BuildScenarioManeuverWithoutException)
{
    IO::SDK::API::DTO::ScenarioDTO scenario;
    scenario.CelestialBodies[0].id = 399;
    scenario.CelestialBodies[0].centerOfMotionId = 10;
    scenario.CelestialBodies[1].id = 10;
    scenario.Name = std::string(SpacecraftPath).data();
    scenario.Window.start = 10.0;
    scenario.Window.end = 20.0;
    scenario.Spacecraft.id = -1111;
    scenario.Spacecraft.name = "spc1";
    scenario.Spacecraft.dryOperatingMass = 1000.0;
    scenario.Spacecraft.maximumOperatingMass = 3000.0;
    scenario.Spacecraft.instruments[0].name = "inst1";
    scenario.Spacecraft.instruments[0].id = 200;
    scenario.Spacecraft.instruments[0].boresight.x = 1.0;
    scenario.Spacecraft.instruments[0].boresight.y = 0.0;
    scenario.Spacecraft.instruments[0].boresight.z = 0.0;
    scenario.Spacecraft.instruments[0].fovRefVector.x = 0.0;
    scenario.Spacecraft.instruments[0].fovRefVector.y = 1.0;
    scenario.Spacecraft.instruments[0].fovRefVector.z = 0.0;
    scenario.Spacecraft.instruments[0].fieldOfView = 3.14;
    scenario.Spacecraft.instruments[0].shape = "circular";
    scenario.Spacecraft.fuelTank[0].id = 1;
    scenario.Spacecraft.fuelTank[0].capacity = 1000.0;
    scenario.Spacecraft.fuelTank[0].quantity = 1000.0;
    scenario.Spacecraft.fuelTank[0].serialNumber = "ft1";
    scenario.Spacecraft.engines[0].id = 1;
    scenario.Spacecraft.engines[0].serialNumber = "eng1";
    scenario.Spacecraft.engines[0].fuelTankSerialNumber = "ft1";
    scenario.Spacecraft.engines[0].fuelflow = 50;
    scenario.Spacecraft.engines[0].isp = 400;
    scenario.Spacecraft.engines[0].name = "engine1";
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.id = 399;
    scenario.Spacecraft.initialOrbitalParameter.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.initialOrbitalParameter.epoch = 15.0;
    scenario.Spacecraft.initialOrbitalParameter.inertialFrame = "J2000";
    scenario.Spacecraft.initialOrbitalParameter.position.x = 6800.0;
    scenario.Spacecraft.initialOrbitalParameter.position.y = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.position.z = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.x = 0.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.y = 8.0;
    scenario.Spacecraft.initialOrbitalParameter.velocity.z = 0.0;
    scenario.Spacecraft.apogeeHeightChangingManeuvers[0].maneuverOrder = 0;
    scenario.Spacecraft.apogeeHeightChangingManeuvers[0].engines[0] = "eng1";
    scenario.Spacecraft.apogeeHeightChangingManeuvers[0].targetHeight = 7000000.0;
    scenario.Spacecraft.perigeeHeightChangingManeuvers[0].maneuverOrder = 1;
    scenario.Spacecraft.perigeeHeightChangingManeuvers[0].engines[0] = "eng1";
    scenario.Spacecraft.perigeeHeightChangingManeuvers[0].targetHeight = 6600000.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].maneuverOrder = 2;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].engines[0] = "eng1";
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.x = 6800.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.y = 0.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.z = 0.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.x = 0.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.y = 8.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.z = 0.0;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.centerOfMotion.id = 399;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.inertialFrame = "J2000";
    scenario.Spacecraft.combinedManeuvers[0].maneuverOrder = 3;
    scenario.Spacecraft.combinedManeuvers[0].engines[0] = "eng1";
    scenario.Spacecraft.combinedManeuvers[0].targetHeight = 6800.0;
    scenario.Spacecraft.combinedManeuvers[0].targetInclination = 1.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].maneuverOrder = 4;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].engines[0] = "eng1";
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.position.x = 6800.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.position.y = 0.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.position.z = 0.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.velocity.x = 0.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.velocity.y = 8.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.velocity.z = 0.0;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.centerOfMotion.id = 399;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.inertialFrame = "J2000";
    scenario.Spacecraft.phasingManeuverDto[0].maneuverOrder = 5;
    scenario.Spacecraft.phasingManeuverDto[0].engines[0] = "eng1";
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.position.x = 6800.0;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.position.y = 0.0;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.position.z = 0.0;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.velocity.x = 0.0;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.velocity.y = 8.0;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.velocity.z = 0.0;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.centerOfMotion.id = 399;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.Spacecraft.phasingManeuverDto[0].targetOrbit.inertialFrame = "J2000";
    scenario.Spacecraft.phasingManeuverDto[0].numberRevolutions = 2;

    PropagateProxy(scenario);

}