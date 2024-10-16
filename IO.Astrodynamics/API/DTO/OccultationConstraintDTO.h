/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once
#include <WindowDTO.h>

namespace IO::Astrodynamics::API::DTO
{
    struct OccultationConstraintDTO
    {
        int observerId{0};
        int backBodyId{0};
        int frontId{0};
        const char* type{};
        const char* aberrationId{};
        double initialStepSize{0.0};
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]{};
    };
}
