/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_INSTRUMENTPOINTINGTOATTITUDEDTO_H
#define IOSDK_INSTRUMENTPOINTINGTOATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct InstrumentPointingToAttitudeDTO
    {
        int instrumentId{0};
        int targetId{-1};
        int maneuverOrder{-1};
        char* engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO window{};
    };
}
#endif //IOSDK_INSTRUMENTPOINTINGTOATTITUDEDTO_H