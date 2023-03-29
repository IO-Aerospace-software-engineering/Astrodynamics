//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_BODYVISIBILITYFROMSITEDTO_H
#define IOSDK_BODYVISIBILITYFROMSITEDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct BodyVisibilityFromSiteDTO
    {
        int TargetBodyId{0};
        const char* aberration;
        IO::SDK::API::DTO::WindowDTO windows[1000];
    };
}
#endif //IOSDK_BODYVISIBILITYFROMSITEDTO_H
