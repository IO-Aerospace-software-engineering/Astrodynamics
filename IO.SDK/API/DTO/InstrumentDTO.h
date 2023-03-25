//
// Created by spacer on 3/24/23.
//

#ifndef IOSDK_INSTRUMENTDTO_H
#define IOSDK_INSTRUMENTDTO_H
#include <Vector3DDTO.h>

namespace IO::SDK::API::DTO
{
    struct InstrumentDTO
    {
        int id;
        const char* shape;
        Vector3DDTO orientation;
        Vector3DDTO boresight;
        Vector3DDTO fovRefVector;
        double fov;
    };

}
#endif //IOSDK_INSTRUMENTDTO_H
