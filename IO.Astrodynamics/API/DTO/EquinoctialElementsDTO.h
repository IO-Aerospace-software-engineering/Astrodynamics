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
        char inertialFrame[32];
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

        void SetFrame(const char* inputFrame)
        {
            std::strcpy(inertialFrame,inputFrame);
            inertialFrame[31]='\0';
        }
    };
}

#endif //IOSDK_EQUINOCTIALELEMENTSDTO_H
