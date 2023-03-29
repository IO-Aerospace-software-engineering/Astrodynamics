//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_BYDAYDTO_H
#define IOSDK_BYDAYDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct ByDayDTO
    {
        double twilightDefinition{0.0};
        IO::SDK::API::DTO::WindowDTO windows[100];
    };
}
#endif //IOSDK_BYDAYDTO_H
