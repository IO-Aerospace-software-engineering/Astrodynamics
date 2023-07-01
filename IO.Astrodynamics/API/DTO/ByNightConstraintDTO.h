/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_BYNIGHTCONSTRAINTDTO_H
#define IOSDK_BYNIGHTCONSTRAINTDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct ByNightConstraintDTO
    {
        double twilightDefinition{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]{};
    };
}
#endif //IOSDK_BYNIGHTCONSTRAINTDTO_H
