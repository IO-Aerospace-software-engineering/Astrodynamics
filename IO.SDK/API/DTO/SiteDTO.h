//
// Created by spacer on 3/23/23.
//
#include <string>

#include <GeodeticDTO.h>
#include <AzimuthRangeDTO.h>

#ifndef IOSDK_SITEDTO_H
#define IOSDK_SITEDTO_H
namespace IO::SDK::API::DTO
{
    struct SiteDTO
    {
        int id{0};
        const char *name;
        int bodyId{0};
        IO::SDK::API::DTO::GeodeticDTO coordinates;
        IO::SDK::API::DTO::AzimuthRangeDDTO ranges[10];
    };
}
#endif //IOSDK_SITEDTO_H
