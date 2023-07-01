/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_BYDAYCONSTRAINTDTO_H
#define IOSDK_BYDAYCONSTRAINTDTO_H
#include <WindowDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct ByDayConstraintDTO
    {
        double twilightDefinition{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]{};
    };
}
#endif //IOSDK_BYDAYCONSTRAINTDTO_H
