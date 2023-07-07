//
// Created by s.guillet on 07/07/2023.
//

#ifndef IO_BARYCENTER_H
#define IO_BARYCENTER_H

#include "Body.h"

namespace IO::Astrodynamics::Body
{
    class Barycenter final : public IO::Astrodynamics::Body::Body
    {
    public:
        Barycenter(int id);
    };
}


#endif //IO_BARYCENTER_H
