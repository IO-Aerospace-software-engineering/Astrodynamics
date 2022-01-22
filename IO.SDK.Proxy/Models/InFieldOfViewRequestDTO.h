#pragma once
#include <Models/WindowDTO.h>

namespace IO::SDK::Proxy::Models
{
    struct InFieldOfViewRequestDTO
    {
        int instrumentId{0};
        int targetId{0};
        IO::SDK::Proxy::Models::WindowDTO window;
    };
}
