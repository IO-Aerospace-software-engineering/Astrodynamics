/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_ORBITALPLANECHANGINGMANEUVERDTO_H
#define IOSDK_ORBITALPLANECHANGINGMANEUVERDTO_H

#include <WindowDTO.h>
#include <StateVectorDTO.h>

namespace IO::Astrodynamics::API::DTO
{
    struct OrbitalPlaneChangingManeuverDTO
    {
        int maneuverOrder{-1};
        const char* engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        StateVectorDTO targetOrbit;

        WindowDTO maneuverWindow{};
        WindowDTO thrustWindow{};
        WindowDTO attitudeWindow{};
        Vector3DDTO deltaV{};
        double FuelBurned{0.0};
    };
}

#endif //IOSDK_ORBITALPLANECHANGINGMANEUVERDTO_H