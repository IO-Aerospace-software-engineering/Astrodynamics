/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once

#include <SpacecraftDTO.h>
#include <SiteDTO.h>
#include <OccultationConstraintDTO.h>
#include <DistanceConstraintDTO.h>

namespace IO::Astrodynamics::API::DTO
{
    struct ScenarioDTO
    {
        const char *Name{};
        WindowDTO Window{};
        SpacecraftDTO Spacecraft;
        SiteDTO Sites[10];

        int CelestialBodiesId[10]{-1,-1,-1,-1,-1,-1,-1,-1,-1,-1};
        const char *Error{};
    };
}
