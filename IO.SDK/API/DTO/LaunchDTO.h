//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_LAUNCHDTO_H
#define IOSDK_LAUNCHDTO_H
#include <WindowDTO.h>
#include <StateVectorDTO.h>

namespace IO::SDK::API::DTO
{
    struct LaunchDTO
    {
        int launchSiteId{0};
        int recoverySiteId{0};
        bool launchByDay;
        double initialStepSize{0.0};
        IO::SDK::API::DTO::StateVectorDTO targetOrbit;
        double inertialAzimuth{0.0};
        double nonInertialAzimuth{0.0};

        double nonInertialInsertionVelocity{0.0};
        double inertialInsertionVelocity{0.0};

        IO::SDK::API::DTO::WindowDTO windows[100];
    };
}
#endif //IOSDK_LAUNCHDTO_H
