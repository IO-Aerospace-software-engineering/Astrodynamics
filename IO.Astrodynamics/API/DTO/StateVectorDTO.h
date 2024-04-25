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
        const char* Error{};

        void SetFrame(const char* frame)
        {
            std::strcpy(inertialFrame,frame);
            inertialFrame[31]='\0';
        }
    };
}
