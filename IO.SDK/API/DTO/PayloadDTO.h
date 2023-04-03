//
// Created by spacer on 3/25/23.
//

#ifndef IOSDK_PAYLOADDTO_H
#define IOSDK_PAYLOADDTO_H
namespace IO::SDK::API::DTO
{
    struct PayloadDTO
    {
        int serialNumber{0};
        const char * name{};
        double mass{};
    };
}
#endif //IOSDK_PAYLOADDTO_H
