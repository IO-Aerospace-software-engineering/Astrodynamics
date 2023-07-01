/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_NADIRATTITUDEDTO_H
#define IOSDK_NADIRATTITUDEDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct NadirAttitudeDTO
    {
        int maneuverOrder{-1};
        char* engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO window{};
    };
}

#endif //IOSDK_NADIRATTITUDEDTO_H
