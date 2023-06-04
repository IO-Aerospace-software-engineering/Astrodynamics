/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by sylvain guillet on 3/27/23.
//

#ifndef IOSDK_ZENITHATTITUDEDTO_H
#define IOSDK_ZENITHATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct ZenithAttitudeDTO
    {
        int maneuverOrder{-1};
        char* engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO window{};
    };
}

#endif //IOSDK_ZENITHATTITUDEDTO_H
