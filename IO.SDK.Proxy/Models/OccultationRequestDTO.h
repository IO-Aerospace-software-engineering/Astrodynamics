#pragma once
#include <Models/WindowDTO.h>

namespace IO::SDK::Proxy::Models
{
struct OccultationRequestDTO
{
    int occultationType=0;
    int bodyId=0;
    int backBodyId=0;
    int frontBodyId=0;    
    IO::SDK::Proxy::Models::WindowDTO window;
    int aberrationId=0;
};
}
