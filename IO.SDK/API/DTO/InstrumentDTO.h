//
// Created by spacer on 3/24/23.
//

#ifndef IOSDK_INSTRUMENTDTO_H
#define IOSDK_INSTRUMENTDTO_H
#include <Vector3DDTO.h>
#include "InFieldOfViewConstraintDTO.h"

namespace IO::SDK::API::DTO
{
    struct InstrumentDTO
    {
        int id{};
        const char* name{};
        const char* shape{};
        Vector3DDTO orientation{};
        Vector3DDTO boresight{};
        Vector3DDTO fovRefVector{};
        double fieldOfView{};
        double crossAngle{};
//        IO::SDK::API::DTO::InFieldOfViewConstraintDTO inFieldOfViews[10];
    };

}
#endif //IOSDK_INSTRUMENTDTO_H
