/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_FRAMETRANSFORMATION_H
#define IOSDK_FRAMETRANSFORMATION_H

#include <Vector3DDTO.h>
#include <QuaternionDTO.h>

namespace IO::SDK::API::DTO
{
    struct FrameTransformationDTO
    {
        IO::SDK::API::DTO::QuaternionDTO Rotation{};
        IO::SDK::API::DTO::Vector3DDTO AngularVelocity{};
    };
}

#endif //IOSDK_FRAMETRANSFORMATION_H
