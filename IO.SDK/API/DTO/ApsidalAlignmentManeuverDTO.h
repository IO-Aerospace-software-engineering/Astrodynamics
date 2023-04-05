//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_APSIDALALIGNMENTMANEUVERDTO_H
#define IOSDK_APSIDALALIGNMENTMANEUVERDTO_H

#include <WindowDTO.h>
#include <Vector3DDTO.h>
#include <StateVectorDTO.h>

namespace IO::SDK::API::DTO
{
    struct ApsidalAlignmentManeuverDTO
    {
        int maneuverOrder{0};
        char *engines[10]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        StateVectorDTO targetOrbit;

        WindowDTO maneuverWindow{};
        WindowDTO thrustWindow{};
        WindowDTO attitudeWindow{};
        Vector3DDTO deltaV{};
        double theta{};
    };
}

#endif //IOSDK_APSIDALALIGNMENTMANEUVERDTO_H
