
/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_APSIDALALIGNMENTMANEUVERDTO_H
#define IOSDK_APSIDALALIGNMENTMANEUVERDTO_H

#include <WindowDTO.h>
#include <StateVectorDTO.h>

namespace IO::Astrodynamics::API::DTO
{
    struct ApsidalAlignmentManeuverDTO
    {
        int maneuverOrder{-1};
        char *engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        StateVectorDTO targetOrbit;

        WindowDTO maneuverWindow{};
        WindowDTO thrustWindow{};
        WindowDTO attitudeWindow{};
        Vector3DDTO deltaV{};
        double FuelBurned{0.0};
        double theta{};
    };
}

#endif //IOSDK_APSIDALALIGNMENTMANEUVERDTO_H
