/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_BODYVISIBILITYFROMSITECONSTRAINTDTO_H
#define IOSDK_BODYVISIBILITYFROMSITECONSTRAINTDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct BodyVisibilityFromSiteConstraintDTO
    {
        int TargetBodyId{0};
        const char* aberration{};
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]{};
    };
}
#endif //IOSDK_BODYVISIBILITYFROMSITECONSTRAINTDTO_H
