//
// Created by sylvain guillet on 3/27/23.
//

#ifndef IOSDK_RETROGRADEATTITUDEDTO_H
#define IOSDK_RETROGRADEATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct RetrogradeAttitudeDTO
    {
        int maneuverOrder{-1};
        char* engines[10]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::SDK::API::DTO::WindowDTO window{};
    };
}

#endif //IOSDK_RETROGRADEATTITUDEDTO_H