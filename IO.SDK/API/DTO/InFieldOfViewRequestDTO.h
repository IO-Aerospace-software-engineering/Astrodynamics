#pragma once
#include <WindowDTO.h>

namespace IO::SDK::API::DTO
{
    struct InFieldOfViewRequestDTO
    {
        int instrumentId{0};
        int targetId{0};
        IO::SDK::API::DTO::WindowDTO window;
    };
}
