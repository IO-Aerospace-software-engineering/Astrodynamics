//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_BYDAYCONSTRAINTDTO_H
#define IOSDK_BYDAYCONSTRAINTDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct ByDayConstraintDTO
    {
        double twilightDefinition{0.0};
        IO::SDK::API::DTO::WindowDTO windows[1000];
    };
}
#endif //IOSDK_BYDAYCONSTRAINTDTO_H
