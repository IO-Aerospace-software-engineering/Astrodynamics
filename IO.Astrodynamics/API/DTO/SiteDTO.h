/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/23/23.
//

#include <PlanetodeticDTO.h>
#include <AzimuthRangeDTO.h>
#include "BodyVisibilityFromSiteConstraintDTO.h"
#include "ByDayConstraintDTO.h"
#include "ByNightConstraintDTO.h"

#ifndef IOSDK_SITEDTO_H
#define IOSDK_SITEDTO_H
namespace IO::Astrodynamics::API::DTO
{
    struct SiteDTO
    {
        int id{0};
        const char *name{};
        int bodyId{0};
        IO::Astrodynamics::API::DTO::PlanetodeticDTO coordinates{};
        IO::Astrodynamics::API::DTO::AzimuthRangeDTO ranges[10]{};
        const char *directoryPath{};
    };
}
#endif //IOSDK_SITEDTO_H
