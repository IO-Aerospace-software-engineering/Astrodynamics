#pragma once

#include <Models/Vector3DDTO.h>
namespace IO::SDK::Proxy::Models
{
    struct StateVectorDTO
    {
        IO::SDK::Proxy::Models::Vector3DDTO position;
        IO::SDK::Proxy::Models::Vector3DDTO velocity;
    };
}
