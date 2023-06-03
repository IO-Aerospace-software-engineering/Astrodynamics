#pragma once

#include <SpacecraftDTO.h>
#include <SiteDTO.h>
#include <OccultationConstraintDTO.h>
#include <DistanceConstraintDTO.h>

namespace IO::SDK::API::DTO
{
    struct ScenarioDTO
    {
        const char *Name{};
        WindowDTO Window{};
        SpacecraftDTO Spacecraft;
        SiteDTO Sites[10];

        CelestialBodyDTO CelestialBodies[10];
    };
}
