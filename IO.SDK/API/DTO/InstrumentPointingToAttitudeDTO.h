//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_INSTRUMENTPOINTINGTOATTITUDEDTO_H
#define IOSDK_INSTRUMENTPOINTINGTOATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct InstrumentPointingToAttitudeDTO
    {
        int instrumentId{0};
        int targetBodyId{0};
        int targetSiteId{0};
        int maneuverOrder{0};
        int engines[10];
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::SDK::API::DTO::WindowDTO window;
    };
}
#endif //IOSDK_INSTRUMENTPOINTINGTOATTITUDEDTO_H
