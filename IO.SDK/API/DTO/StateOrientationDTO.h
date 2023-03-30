//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_STATEORIENTATION_H
#define IOSDK_STATEORIENTATION_H
#include <QuaternionDTO.h>
#include <Vector3DDTO.h>
namespace IO::SDK::API::DTO
{
    struct StateOrientationDTO
    {
        QuaternionDTO orientation;
        Vector3DDTO angularVelocity;
        double epoch;
        const char* frame;
    };
}
#endif //IOSDK_STATEORIENTATION_H
