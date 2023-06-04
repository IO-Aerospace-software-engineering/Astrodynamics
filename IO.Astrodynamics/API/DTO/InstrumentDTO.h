/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_INSTRUMENTDTO_H
#define IOSDK_INSTRUMENTDTO_H
#include <Vector3DDTO.h>

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

        friend std::ostream &operator<<(std::ostream &os, const InstrumentDTO &dto)
        {
            os << "id: " << dto.id << " name: " << dto.name << " shape: " << dto.shape << " orientation: " << dto.orientation << " boresight: " << dto.boresight
               << " fovRefVector: " << dto.fovRefVector << " fieldOfView: " << dto.fieldOfView << " crossAngle: " << dto.crossAngle;
            return os;
        }

        double fieldOfView{};
        double crossAngle{};
//        IO::SDK::API::DTO::InFieldOfViewConstraintDTO inFieldOfViews[10];
    };

}
#endif //IOSDK_INSTRUMENTDTO_H
