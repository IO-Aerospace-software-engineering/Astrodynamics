#pragma once
#include <WindowDTO.h>

namespace IO::SDK::API::DTO
{
    struct InFieldOfViewConstraintDTO
    {
        int targetId{0};
        const char* aberration{};
        double initialStepSize{0.0};
        IO::SDK::API::DTO::WindowDTO windows[1000]{};
    };
}
