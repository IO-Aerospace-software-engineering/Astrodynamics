//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_PHASINGMANEUVERDTO_H
#define IOSDK_PHASINGMANEUVERDTO_H

#include <WindowDTO.h>
#include <Vector3DDTO.h>
#include <StateVectorDTO.h>

namespace IO::SDK::API::DTO
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
    };
}

#endif //IOSDK_PHASINGMANEUVERDTO_H
