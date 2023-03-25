#pragma once

#include <Vector3DDTO.h>
namespace IO::SDK::API::DTO
{
    struct StateVectorDTO
    {
        IO::SDK::API::DTO::Vector3DDTO position;
        IO::SDK::API::DTO::Vector3DDTO velocity;
    };
}
