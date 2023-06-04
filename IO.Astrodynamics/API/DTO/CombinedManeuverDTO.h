
/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_COMBINEDMANEUVERDTO_H
#define IOSDK_COMBINEDMANEUVERDTO_H

#include <WindowDTO.h>
#include <Vector3DDTO.h>

namespace IO::Astrodynamics::API::DTO
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
        double FuelBurned{0.0};
    };
}

#endif //IOSDK_COMBINEDMANEUVERDTO_H
