#pragma once
#include <string>
#include <FuelTankDTO.h>
#include <EngineDTO.h>
#include <InstrumentDTO.h>
#include <PayloadDTO.h>
#include <StateVectorDTO.h>
#include <StateOrientationDTO.h>
#include <AttitudeDTO.h>
#include <InstrumentPointingToAttitudeDTO.h>
#include <ApogeeHeightChangingManeuverDTO.h>
#include <PerigeeHeightChangingManeuverDTO.h>
#include <CombinedManeuverDTO.h>
#include <OrbitalPlaneChangingManeuverDTO.h>
#include <PhasingManeuverDTO.h>
#include <ApsidalAlignmentManeuverDTO.h>
#include "LaunchDTO.h"

namespace IO::SDK::API::DTO
{
    struct SpacecraftDTO
    {
        //Spacecraft structure
        int id{0};
        const char* name;
        double dryOperatingMass;
        double maximumOperatingMass;
        StateVectorDTO initialOrbitalParameter;
        FuelTankDTO fuelTank[5];
        EngineDTO engines[5];
        InstrumentDTO instruments[5];
        PayloadDTO payloads[5];

        //Spacecraft attitudes
        AttitudeDTO attitudes[50];
        InstrumentPointingToAttitudeDTO pointingToAttitudes[10];

        //Spacecraft maneuvers
        PerigeeHeightChangingManeuverDTO perigeeHeightChangingManeuvers[10];
        ApogeeHeightChangingManeuverDTO apogeeHeightChangingManeuvers[10];
        OrbitalPlaneChangingManeuverDTO orbitalPlaneChangingManeuvers[10];
        CombinedManeuverDTO combinedManeuvers[10];
        ApsidalAlignmentManeuverDTO apsidalAlignmentManeuvers[10];
        PhasingManeuverDTO phasingManeuverDto[10];
        LaunchDTO launches;

        //Spacecraft states
        StateVectorDTO stateVectors[10000];
        StateOrientationDTO stateOrientations[1000];
    };
}
