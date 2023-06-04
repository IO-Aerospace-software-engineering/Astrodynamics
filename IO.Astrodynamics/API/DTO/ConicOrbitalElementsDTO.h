/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_CONICORBITALELEMENTSDTO_H
#define IOSDK_CONICORBITALELEMENTSDTO_H

namespace IO::Astrodynamics::API::DTO
{

    struct ConicOrbitalElementsDTO
    {
        int centerOfMotionId{};
        double epoch{};
        double perifocalDistance{};
        double eccentricity{};
        double inclination{};
        double ascendingNodeLongitude{};
        double periapsisArgument{};
        double meanAnomaly{};
        double trueAnomaly{};
        double orbitalPeriod{};
        double semiMajorAxis{};
        const char* frame{};

    };
}


#endif //IOSDK_CONICORBITALELEMENTSDTO_H
