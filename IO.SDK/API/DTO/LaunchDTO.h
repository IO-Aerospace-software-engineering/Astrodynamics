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

        IO::SDK::API::DTO::WindowDTO windows[1000];
    };
}
#endif //IOSDK_LAUNCHDTO_H
