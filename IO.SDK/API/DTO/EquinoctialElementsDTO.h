//
// Created by spacer on 5/31/23.
//

#ifndef IOSDK_EQUINOCTIALELEMENTSDTO_H
#define IOSDK_EQUINOCTIALELEMENTSDTO_H


namespace IO::SDK::API::DTO
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
        double period;
    };
}

#endif //IOSDK_EQUINOCTIALELEMENTSDTO_H
