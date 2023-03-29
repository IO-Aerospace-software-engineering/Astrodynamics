//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_BYNIGHTDTO_H
#define IOSDK_BYNIGHTDTO_H
#include <WindowDTO.h>
namespace IO::SDK::API::DTO
{
    struct ByNightDTO
    {
        double twilightDefinition{0.0};
        IO::SDK::API::DTO::WindowDTO windows[1000];
    };
}
#endif //IOSDK_BYNIGHTDTO_H
