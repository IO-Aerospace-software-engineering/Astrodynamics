/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_PAYLOADDTO_H
#define IOSDK_PAYLOADDTO_H
namespace IO::Astrodynamics::API::DTO
{
    struct PayloadDTO
    {
        const char * serialNumber{};
        const char * name{};
        double mass{};
    };
}
#endif //IOSDK_PAYLOADDTO_H
