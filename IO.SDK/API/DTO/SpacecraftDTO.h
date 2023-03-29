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
        FuelTankDTO fuelTank[10];
        EngineDTO engines[10];
        InstrumentDTO instruments[10];
        PayloadDTO payloads[10];

        //Spacecraft attitudes
        AttitudeDTO attitudes[100];
        InstrumentPointingToAttitudeDTO pointingToAttitudes[100];

        //Spacecraft maneuvers
        PerigeeHeightChangingManeuverDTO perigeeHeightChangingManeuvers[100];
        ApogeeHeightChangingManeuverDTO apogeeHeightChangingManeuvers[100];
        OrbitalPlaneChangingManeuverDTO orbitalPlaneChangingManeuvers[100];
        CombinedManeuverDTO combinedManeuvers[100];
        ApsidalAlignmentManeuverDTO apsidalAlignmentManeuvers[100];
        PhasingManeuverDTO phasingManeuverDto[100];
        LaunchDTO launches[10];

        //Spacecraft states
        StateVectorDTO stateVectors[1000];
        StateOrientationDTO stateOrientations[1000];
    };
}
