#pragma once

#include <SpacecraftDTO.h>
#include <SiteDTO.h>
#include <OccultationConstraintDTO.h>
#include <DistanceConstraintDTO.h>

namespace IO::SDK::API::DTO
{
    struct ScenarioDTO
    {
        char *Name{};
        WindowDTO Window{};
        SpacecraftDTO Spacecraft;
        SiteDTO Sites[10];

//        DistanceConstraintDTO distances[5];
//        OccultationConstraintDTO occultations[5];

        CelestialBodyDTO CelestialBodies[10];
    };
}
