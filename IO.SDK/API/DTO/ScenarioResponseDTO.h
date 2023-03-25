#pragma once
#include <StateVectorDTO.h>

namespace IO::SDK::API::DTO
{
    struct ScenarioResponseDTO
    {   
        IO::SDK::API::DTO::StateVectorDTO sv[10];
    };
}
