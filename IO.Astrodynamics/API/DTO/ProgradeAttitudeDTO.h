/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by sylvain guillet on 3/27/23.
//

#ifndef IOSDK_PROGRADEATTITUDEDTO_H
#define IOSDK_PROGRADEATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct ProgradeAttitudeDTO
    {
        int maneuverOrder{-1};
        char* engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO window{};
    };
}

#endif //IOSDK_PROGRADEATTITUDEDTO_H
