/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_EQUINOCTIALELEMENTSDTO_H
#define IOSDK_EQUINOCTIALELEMENTSDTO_H


namespace IO::Astrodynamics::API::DTO
{
    struct EquinoctialElementsDTO
    {
        double epoch{};
        int centerOfMotionId{};
        const char* frame{};
        double semiMajorAxis{};
        double h{};
        double k{};
        double p{};
        double q{};
        double L{};
        double periapsisLongitudeRate{};
        double rightAscensionOfThePole{};
        double declinationOfThePole{};
        double ascendingNodeLongitudeRate{};
        double period{};
    };
}

#endif //IOSDK_EQUINOCTIALELEMENTSDTO_H