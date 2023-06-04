/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_APOGEEHEIGHTCHANGINGMANEUVERDTO_H
#define IOSDK_APOGEEHEIGHTCHANGINGMANEUVERDTO_H

#include <WindowDTO.h>
#include <Vector3DDTO.h>

namespace IO::SDK::API::DTO {
    struct ApogeeHeightChangingManeuverDTO {
        int maneuverOrder{-1};
        char *engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        double targetHeight{0.0};

        IO::SDK::API::DTO::WindowDTO maneuverWindow{};
        IO::SDK::API::DTO::WindowDTO thrustWindow{};
        IO::SDK::API::DTO::WindowDTO attitudeWindow{};
        IO::SDK::API::DTO::Vector3DDTO deltaV{};
        double FuelBurned{0.0};
    };
}

#endif //IOSDK_APOGEEHEIGHTCHANGINGMANEUVERDTO_H
