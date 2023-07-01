/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once
#include <WindowDTO.h>

namespace IO::Astrodynamics::API::DTO
{
    struct InFieldOfViewConstraintDTO
    {
        int targetId{0};
        const char* aberration{};
        double initialStepSize{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]{};
    };
}
