/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/23/23.
//

#include <GeodeticDTO.h>
#include <AzimuthRangeDTO.h>
#include "BodyVisibilityFromSiteConstraintDTO.h"
#include "ByDayConstraintDTO.h"
#include "ByNightConstraintDTO.h"

#ifndef IOSDK_SITEDTO_H
#define IOSDK_SITEDTO_H
namespace IO::SDK::API::DTO
{
    struct SiteDTO
    {
        int id{0};
        const char *name{};
        int bodyId{0};
        IO::SDK::API::DTO::GeodeticDTO coordinates{};
        IO::SDK::API::DTO::AzimuthRangeDTO ranges[10]{};
        const char *directoryPath{};
//        IO::SDK::API::DTO::BodyVisibilityFromSiteConstraintDTO bodyVisibilityFromSites[10];
//        IO::SDK::API::DTO::ByDayConstraintDTO byDay;
//        IO::SDK::API::DTO::ByNightConstraintDTO byNight;
    };
}
#endif //IOSDK_SITEDTO_H
