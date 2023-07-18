//
// Created by s.guillet on 17/07/2023.
//

#ifndef IO_LAGRANGEPOINT_H
#define IO_LAGRANGEPOINT_H

#include "CelestialItem.h"

namespace IO::Astrodynamics::Body
{
    class LagrangePoint final : public IO::Astrodynamics::Body::CelestialItem
    {
    public:
        LagrangePoint(int id, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> centerOfMotion);
    };
}


#endif //IO_LAGRANGEPOINT_H
