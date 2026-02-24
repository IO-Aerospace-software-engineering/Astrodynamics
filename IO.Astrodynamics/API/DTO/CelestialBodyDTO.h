/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_CELESTIALBODYDTO_H
#define IOSDK_CELESTIALBODYDTO_H
#include <cstring>
namespace IO::Astrodynamics::API::DTO
{
    struct CelestialBodyDTO
    {
        int Id{-1};
        int CenterOfMotionId{-1};
        int BarycenterOfMotionId{-1};
        char Name[32];
        IO::Astrodynamics::API::DTO::Vector3DDTO Radii{};
        double GM{};
        char FrameName[32];
        int FrameId{0};
        double J2{0.0};
        double J3{0.0};
        double J4{0.0};

        void SetFrame(const char* frame)
        {
            std::strncpy(FrameName, frame, sizeof(FrameName) - 1);
            FrameName[sizeof(FrameName) - 1] = '\0';
        }

        void SetName(const char* name)
        {
            std::strncpy(Name, name, sizeof(Name) - 1);
            Name[sizeof(Name) - 1] = '\0';
        }
    };
}
#endif //IOSDK_CELESTIALBODYDTO_H
