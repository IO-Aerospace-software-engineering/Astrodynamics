/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_CELESTIALBODYDTO_H
#define IOSDK_CELESTIALBODYDTO_H
namespace IO::Astrodynamics::API::DTO
{
    struct CelestialBodyDTO
    {
        int Id{-1};
        int CenterOfMotionId{-1};
        int BarycenterOfMotionId{-1};
        const char *Name{};
        IO::Astrodynamics::API::DTO::Vector3DDTO Radii{};
        double GM{};
        const char *FrameName{};
        int FrameId{};
        const char *Error{};
    };
}
#endif //IOSDK_CELESTIALBODYDTO_H