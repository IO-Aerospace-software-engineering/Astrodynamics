//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_DISTANCECONSTRAINTDTO_H
#define IOSDK_DISTANCECONSTRAINTDTO_H

#include <WindowDTO.h>

namespace IO::SDK::API::DTO
{
    struct DistanceConstraintDTO
    {
        int observerId{0};
        int targetId{0};
        const char *constraint{};
        double value{0.0};
        const char *aberration{};
        double initialStepSize{0.0};
        IO::SDK::API::DTO::WindowDTO windows[1000]{};
    };
}
#endif //IOSDK_DISTANCECONSTRAINTDTO_H
