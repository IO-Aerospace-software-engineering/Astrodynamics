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

TEST(API, BuildScenarioAttitutesWithoutException)
{
    IO::SDK::API::DTO::ScenarioDTO scenario;
    scenario.celestialBodies[0].id = 399;
    scenario.celestialBodies[0].centerOfMotionId = 10;
    scenario.celestialBodies[1].id = 10;
    scenario.Name = "titi";
    scenario.Window.start = 10.0;
    scenario.Window.end = 20.0;
    scenario.spacecraft.instruments[0].name = "inst1";
    scenario.spacecraft.instruments[0].id = 200;
    scenario.spacecraft.instruments[0].boresight.x = 1.0;
    scenario.spacecraft.instruments[0].boresight.y = 0.0;
    scenario.spacecraft.instruments[0].boresight.z = 0.0;
    scenario.spacecraft.instruments[0].fovRefVector.x = 0.0;
    scenario.spacecraft.instruments[0].fovRefVector.y = 1.0;
    scenario.spacecraft.instruments[0].fovRefVector.z = 0.0;
    scenario.spacecraft.instruments[0].fieldOfView = 3.14;
    scenario.spacecraft.instruments[0].shape = "circular";
    scenario.spacecraft.fuelTank[0].id = 1;
    scenario.spacecraft.fuelTank[0].capacity = 1000.0;
    scenario.spacecraft.fuelTank[0].quantity = 1000.0;
    scenario.spacecraft.fuelTank[0].serialNumber = "ft1";
    scenario.spacecraft.engines[0].id = 1;
    scenario.spacecraft.engines[0].serialNumber = "eng1";
    scenario.spacecraft.engines[0].fuelTankSerialNumber = "ft1";
    scenario.spacecraft.engines[0].fuelflow = 50;
    scenario.spacecraft.engines[0].isp = 400;
    scenario.spacecraft.engines[0].name = "engine1";
    scenario.spacecraft.progradeAttitudes[0].engines[0] = "eng1";
    scenario.spacecraft.progradeAttitudes[0].maneuverOrder = 0;
    scenario.spacecraft.retrogradeAttitudes[0].engines[0] = "eng1";
    scenario.spacecraft.retrogradeAttitudes[0].maneuverOrder = 1;
    scenario.spacecraft.nadirAttitudes[0].engines[0] = "eng1";
    scenario.spacecraft.nadirAttitudes[0].maneuverOrder = 2;
    scenario.spacecraft.zenithAttitudes[0].engines[0] = "eng1";
    scenario.spacecraft.zenithAttitudes[0].maneuverOrder = 3;
    scenario.spacecraft.pointingToAttitudes[0].engines[0] = "eng1";
    scenario.spacecraft.pointingToAttitudes[0].maneuverOrder = 4;
    scenario.spacecraft.pointingToAttitudes[0].targetBodyId = 399;
    scenario.spacecraft.pointingToAttitudes[0].instrumentId = 200;
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

TEST(API, BuildScenarioManeuverWithoutException)
{
    IO::SDK::API::DTO::ScenarioDTO scenario;
    scenario.celestialBodies[0].id = 399;
    scenario.celestialBodies[0].centerOfMotionId = 10;
    scenario.celestialBodies[1].id = 10;
    scenario.Name = "titi";
    scenario.Window.start = 10.0;
    scenario.Window.end = 20.0;
    scenario.spacecraft.id = -1111;
    scenario.spacecraft.name = "spc1";
    scenario.spacecraft.dryOperatingMass = 1000.0;
    scenario.spacecraft.maximumOperatingMass = 3000.0;
    scenario.spacecraft.instruments[0].name = "inst1";
    scenario.spacecraft.instruments[0].id = 200;
    scenario.spacecraft.instruments[0].boresight.x = 1.0;
    scenario.spacecraft.instruments[0].boresight.y = 0.0;
    scenario.spacecraft.instruments[0].boresight.z = 0.0;
    scenario.spacecraft.instruments[0].fovRefVector.x = 0.0;
    scenario.spacecraft.instruments[0].fovRefVector.y = 1.0;
    scenario.spacecraft.instruments[0].fovRefVector.z = 0.0;
    scenario.spacecraft.instruments[0].fieldOfView = 3.14;
    scenario.spacecraft.instruments[0].shape = "circular";
    scenario.spacecraft.fuelTank[0].id = 1;
    scenario.spacecraft.fuelTank[0].capacity = 1000.0;
    scenario.spacecraft.fuelTank[0].quantity = 1000.0;
    scenario.spacecraft.fuelTank[0].serialNumber = "ft1";
    scenario.spacecraft.engines[0].id = 1;
    scenario.spacecraft.engines[0].serialNumber = "eng1";
    scenario.spacecraft.engines[0].fuelTankSerialNumber = "ft1";
    scenario.spacecraft.engines[0].fuelflow = 50;
    scenario.spacecraft.engines[0].isp = 400;
    scenario.spacecraft.engines[0].name = "engine1";
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
    scenario.spacecraft.apogeeHeightChangingManeuvers[0].maneuverOrder = 0;
    scenario.spacecraft.apogeeHeightChangingManeuvers[0].engines[0] = "eng1";
    scenario.spacecraft.apogeeHeightChangingManeuvers[0].targetHeight = 7000000.0;
    scenario.spacecraft.perigeeHeightChangingManeuvers[0].maneuverOrder = 1;
    scenario.spacecraft.perigeeHeightChangingManeuvers[0].engines[0] = "eng1";
    scenario.spacecraft.perigeeHeightChangingManeuvers[0].targetHeight = 6600000.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].maneuverOrder = 2;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].engines[0] = "eng1";
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.x = 6800.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.y = 0.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.position.z = 0.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.x = 0.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.y = 8.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.velocity.z = 0.0;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.centerOfMotion.id = 399;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.spacecraft.orbitalPlaneChangingManeuvers[0].targetOrbit.inertialFrame = "J2000";
    scenario.spacecraft.combinedManeuvers[0].maneuverOrder = 3;
    scenario.spacecraft.combinedManeuvers[0].engines[0] = "eng1";
    scenario.spacecraft.combinedManeuvers[0].targetHeight = 6800.0;
    scenario.spacecraft.combinedManeuvers[0].targetInclination = 1.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].maneuverOrder = 4;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].engines[0] = "eng1";
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.position.x = 6800.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.position.y = 0.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.position.z = 0.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.velocity.x = 0.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.velocity.y = 8.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.velocity.z = 0.0;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.centerOfMotion.id = 399;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.spacecraft.apsidalAlignmentManeuvers[0].targetOrbit.inertialFrame = "J2000";
    scenario.spacecraft.phasingManeuverDto[0].maneuverOrder = 5;
    scenario.spacecraft.phasingManeuverDto[0].engines[0] = "eng1";
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.position.x = 6800.0;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.position.y = 0.0;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.position.z = 0.0;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.velocity.x = 0.0;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.velocity.y = 8.0;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.velocity.z = 0.0;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.centerOfMotion.id = 399;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.centerOfMotion.centerOfMotionId = 10;
    scenario.spacecraft.phasingManeuverDto[0].targetOrbit.inertialFrame = "J2000";
    scenario.spacecraft.phasingManeuverDto[0].numberRevolutions = 2;

    PropagateProxy(scenario);

}