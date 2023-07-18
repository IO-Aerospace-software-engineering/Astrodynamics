//
// Created by s.guillet on 07/07/2023.
//

#ifndef IO_BARYCENTER_H
#define IO_BARYCENTER_H

#include "CelestialItem.h"

namespace IO::Astrodynamics::Body
{
    class Barycenter final : public IO::Astrodynamics::Body::CelestialItem
    {
    public:
        explicit Barycenter(int id);
    };
}


#endif //IO_BARYCENTER_H
