#pragma once
#include <Models/WindowsDTO.h>

namespace IO::SDK::Proxy::Models
{
    struct OccultationDTO
    {
        int bodyId{0};
        int backBodyId{0};
        int frontId{0};
        int type{0};
        IO::SDK::Proxy::Models::WindowsDTO window;
    };
}
