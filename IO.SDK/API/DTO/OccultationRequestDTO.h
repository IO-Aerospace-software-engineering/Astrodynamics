#pragma once

#include <WindowDTO.h>

namespace IO::SDK::API::DTO
{
    struct OccultationRequestDTO
    {
        int occultationType = 0;
        int bodyId = 0;
        int backBodyId = 0;
        int frontBodyId = 0;
        IO::SDK::API::DTO::WindowDTO window;
        int aberrationId = 0;
    };
}
