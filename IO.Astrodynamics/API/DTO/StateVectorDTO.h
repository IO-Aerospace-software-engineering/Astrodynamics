/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once

#include <Vector3DDTO.h>
#include <CelestialBodyDTO.h>
#include <cstring>
namespace IO::Astrodynamics::API::DTO
{
    struct StateVectorDTO
    {
        double epoch{0.0};
        IO::Astrodynamics::API::DTO::Vector3DDTO position{};
        IO::Astrodynamics::API::DTO::Vector3DDTO velocity{};
        int centerOfMotionId;
        char inertialFrame[32];

        void SetFrame(const char* frame)
        {
            std::strncpy(inertialFrame, frame, sizeof(inertialFrame) - 1);
            inertialFrame[sizeof(inertialFrame) - 1] = '\0';
        }
    };
}
