/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once

#include <ostream>

namespace IO::SDK::API::DTO
{
    struct Vector3DDTO
    {
        friend std::ostream &operator<<(std::ostream &os, const Vector3DDTO &ddto)
        {
            os << "x: " << ddto.x << " y: " << ddto.y << " z: " << ddto.z;
            return os;
        }

        double x, y, z;
    };
}
