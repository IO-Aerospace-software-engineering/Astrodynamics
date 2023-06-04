//
// Created by s.guillet on 10/02/2023.
//

#ifndef TEST_VECTORS_H
#define TEST_VECTORS_H

#include <Vector3D.h>

namespace IO::Astrodynamics::Tests
{
    inline const IO::Astrodynamics::Math::Vector3D VectorX{1.0, 0.0, 0.0};
    inline const IO::Astrodynamics::Math::Vector3D VectorY{0.0, 1.0, 0.0};
    inline const IO::Astrodynamics::Math::Vector3D VectorZ{0.0, 0.0, 1.0};
    inline const IO::Astrodynamics::Math::Vector3D Zero{0.0, 0.0, 0.0};
}
#endif //TEST_VECTORS_H
