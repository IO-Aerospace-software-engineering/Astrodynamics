/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 10/02/2023.
//

#ifndef TEST_PLANE_H
#define TEST_PLANE_H

#include <Plane.h>

namespace IO::Astrodynamics::Tests
{
    inline const IO::Astrodynamics::Math::Plane PlaneX{{1.0, 0.0, 0.0},0.0};
    inline const IO::Astrodynamics::Math::Plane PlaneY{{0.0, 1.0, 0.0},0.0};
    inline const IO::Astrodynamics::Math::Plane PlaneZ{{0.0, 0.0, 1.0},0.0};
}
#endif //TEST_PLANE_H
