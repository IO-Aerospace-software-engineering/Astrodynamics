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
            std::strcpy(FrameName,frame);
            FrameName[31]='\0';
        }

        void SetName(const char* name)
        {
            std::strcpy(Name,name);
            Name[31]='\0';
        }
    };
}
#endif //IOSDK_CELESTIALBODYDTO_H
