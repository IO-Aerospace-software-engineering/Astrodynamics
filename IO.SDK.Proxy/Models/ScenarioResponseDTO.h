#pragma once
#include <Models/StateVectorDTO.h>

namespace IO::SDK::Proxy::Models
{
    struct ScenarioResponseDTO
    {   
        IO::SDK::Proxy::Models::StateVectorDTO sv[10];
    };
}
