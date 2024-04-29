/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <cstring>

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
        char frame[32];

        void SetFrame(const char* inputFrame)
        {
            std::strcpy(frame,inputFrame);
            frame[31]='\0';
        }

    };
}


#endif //IOSDK_CONICORBITALELEMENTSDTO_H
