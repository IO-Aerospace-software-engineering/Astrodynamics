/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_LAUNCHDTO_H
#define IOSDK_LAUNCHDTO_H

#include <StateVectorDTO.h>
#include "SiteDTO.h"

namespace IO::Astrodynamics::API::DTO
{
    struct LaunchDTO
    {
        IO::Astrodynamics::API::DTO::WindowDTO window{};
        SiteDTO launchSite;
        SiteDTO recoverySite;
        bool launchByDay{};
        double initialStepSize{1.0};
        IO::Astrodynamics::API::DTO::StateVectorDTO targetOrbit;
        double inertialAzimuth[100]{};
        double nonInertialAzimuth[100]{};

        double nonInertialInsertionVelocity[100]{};
        double inertialInsertionVelocity[100]{};

        IO::Astrodynamics::API::DTO::WindowDTO windows[100]{};
    };
}
#endif //IOSDK_LAUNCHDTO_H
