/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_LAUNCHDTO_H
#define IOSDK_LAUNCHDTO_H
#include <StateVectorDTO.h>
#include "SiteDTO.h"

namespace IO::SDK::API::DTO
{
    struct LaunchDTO
    {
        IO::SDK::API::DTO::WindowDTO window{};
        SiteDTO launchSite;
        SiteDTO recoverySite;
        bool launchByDay{};
        double initialStepSize{1.0};
        IO::SDK::API::DTO::StateVectorDTO targetOrbit;
        double inertialAzimuth{0.0};
        double nonInertialAzimuth{0.0};

        double nonInertialInsertionVelocity{0.0};
        double inertialInsertionVelocity{0.0};

        IO::SDK::API::DTO::WindowDTO windows[100]{};
    };
}
#endif //IOSDK_LAUNCHDTO_H
