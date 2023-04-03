//
// Created by sylvain guillet on 3/27/23.
//

#ifndef IOSDK_ATTITUDEDTO_H
#define IOSDK_ATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct AttitudeDTO
    {
        const char *name{};
        int maneuverOrder{0};
        int engines[10]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::SDK::API::DTO::WindowDTO window{};
    };
}

#endif //IOSDK_ATTITUDEDTO_H
