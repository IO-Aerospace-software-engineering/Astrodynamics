#pragma once
#include <WindowsDTO.h>

namespace IO::SDK::API::DTO
{
    struct OccultationDTO
    {
        int bodyId{0};
        int backBodyId{0};
        int frontId{0};
        int type{0};
        IO::SDK::API::DTO::WindowsDTO window;
        int aberrationId = 0;
    };
}
