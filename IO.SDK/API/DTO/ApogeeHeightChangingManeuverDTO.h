//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_APOGEEHEIGHTCHANGINGMANEUVERDTO_H
#define IOSDK_APOGEEHEIGHTCHANGINGMANEUVERDTO_H

#include <WindowDTO.h>
#include <Vector3DDTO.h>

namespace IO::SDK::API::DTO {
    struct ApogeeHeightChangingManeuverDTO {
        int maneuverOrder{0};
        char *engines[10]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        double targetHeight{0.0};

        IO::SDK::API::DTO::WindowDTO maneuverWindow{};
        IO::SDK::API::DTO::WindowDTO thrustWindow{};
        IO::SDK::API::DTO::WindowDTO attitudeWindow{};
        IO::SDK::API::DTO::Vector3DDTO deltaV{};
    };
}

#endif //IOSDK_APOGEEHEIGHTCHANGINGMANEUVERDTO_H
