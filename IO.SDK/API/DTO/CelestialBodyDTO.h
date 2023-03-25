//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_CELESTIALBODYDTO_H
#define IOSDK_CELESTIALBODYDTO_H
namespace IO::SDK::API::DTO
{
    struct CelestialBodyDTO
    {
        int id{0};
        const char* name;
        int centerOfMotionId{0};
    };
}
#endif //IOSDK_CELESTIALBODYDTO_H
