/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once

#include <Vector3DDTO.h>
#include <CelestialBodyDTO.h>
namespace IO::SDK::API::DTO
{
    struct StateVectorDTO
    {
        double epoch{0.0};
        IO::SDK::API::DTO::Vector3DDTO position{};
        IO::SDK::API::DTO::Vector3DDTO velocity{};
        int centerOfMotionId;
        const char* inertialFrame{};
    };
}
