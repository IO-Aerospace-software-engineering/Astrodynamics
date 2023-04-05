//
// Created by sylvain guillet on 3/27/23.
//

#ifndef IOSDK_ZENITHATTITUDEDTO_H
#define IOSDK_ZENITHATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct ZenithAttitudeDTO
    {
        int maneuverOrder{0};
        char* engines[10]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::SDK::API::DTO::WindowDTO window{};
    };
}

#endif //IOSDK_ZENITHATTITUDEDTO_H
