/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_PHASINGMANEUVERDTO_H
#define IOSDK_PHASINGMANEUVERDTO_H

#include <WindowDTO.h>
#include <StateVectorDTO.h>

namespace IO::Astrodynamics::API::DTO
{
    struct PhasingManeuverDTO
    {
        int maneuverOrder{-1};
        char *engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        int numberRevolutions{};
        StateVectorDTO targetOrbit;

        WindowDTO maneuverWindow{};
        WindowDTO thrustWindow{};
        WindowDTO attitudeWindow{};
        Vector3DDTO deltaV{};
        double FuelBurned{0.0};
    };
}

#endif //IOSDK_PHASINGMANEUVERDTO_H
