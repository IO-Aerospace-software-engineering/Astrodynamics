/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_CELESTIALBODYDTO_H
#define IOSDK_CELESTIALBODYDTO_H
namespace IO::SDK::API::DTO
{
    struct CelestialBodyDTO
    {
        int Id{-1};
        int centerOfMotionId{-1};
        const char *Name{};
        IO::SDK::API::DTO::Vector3DDTO Radii{};
        double GM{};
        const char *FrameName{};
        int FrameId{};
        const char * Error{};
    };
}
#endif //IOSDK_CELESTIALBODYDTO_H
