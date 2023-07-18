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
       explicit LagrangePoint(int id);
    };
}


#endif //IO_LAGRANGEPOINT_H
