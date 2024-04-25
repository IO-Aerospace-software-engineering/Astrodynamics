/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_STATEORIENTATION_H
#define IOSDK_STATEORIENTATION_H
#include <QuaternionDTO.h>
#include <Vector3DDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct StateOrientationDTO
    {
        QuaternionDTO orientation;
        Vector3DDTO angularVelocity{};
        double epoch{};
        char frame[32];

        void SetFrame(const char* data)
        {
            std::strcpy(frame,data);
            frame[31]='\0';
        }
    };
}
#endif //IOSDK_STATEORIENTATION_H
