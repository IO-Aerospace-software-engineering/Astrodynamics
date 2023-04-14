//
// Created by spacer on 3/28/23.
//

#ifndef IOSDK_COMBINEDMANEUVERDTO_H
#define IOSDK_COMBINEDMANEUVERDTO_H

#include <WindowDTO.h>
#include <Vector3DDTO.h>

namespace IO::SDK::API::DTO
{
    struct CombinedManeuverDTO
    {
        int maneuverOrder{-1};
        char *engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        double targetHeight{0.0};
        double targetInclination{0.0};

        WindowDTO maneuverWindow{};
        WindowDTO thrustWindow{};
        WindowDTO attitudeWindow{};
        Vector3DDTO deltaV{};
    };
}

#endif //IOSDK_COMBINEDMANEUVERDTO_H
