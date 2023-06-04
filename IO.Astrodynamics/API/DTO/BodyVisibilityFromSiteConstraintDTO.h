/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_BODYVISIBILITYFROMSITECONSTRAINTDTO_H
#define IOSDK_BODYVISIBILITYFROMSITECONSTRAINTDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct BodyVisibilityFromSiteConstraintDTO
    {
        int TargetBodyId{0};
        const char* aberration{};
        IO::SDK::API::DTO::WindowDTO windows[1000]{};
    };
}
#endif //IOSDK_BODYVISIBILITYFROMSITECONSTRAINTDTO_H
