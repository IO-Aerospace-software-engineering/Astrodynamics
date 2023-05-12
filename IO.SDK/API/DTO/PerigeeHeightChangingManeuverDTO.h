//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_PERIGEEHEIGHTCHANGINGMANEUVERDTO_H
#define IOSDK_PERIGEEHEIGHTCHANGINGMANEUVERDTO_H
#include <WindowDTO.h>
#include <Vector3DDTO.h>
namespace IO::SDK::API::DTO
{
    struct PerigeeHeightChangingManeuverDTO
    {
        int maneuverOrder{-1};
        char* engines[5]{};
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

#endif //IOSDK_PERIGEEHEIGHTCHANGINGMANEUVERDTO_H
