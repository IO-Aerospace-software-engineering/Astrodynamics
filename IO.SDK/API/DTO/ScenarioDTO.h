#pragma once

#include <SpacecraftDTO.h>
#include <InFieldOfViewConstraintDTO.h>
#include <SiteDTO.h>
#include <OccultationConstraintDTO.h>
#include <BodyVisibilityFromSiteConstraintDTO.h>
#include <ByDayConstraintDTO.h>
#include <ByNightConstraintDTO.h>
#include <DistanceConstraintDTO.h>
#include <LaunchDTO.h>
#include <AttitudeDTO.h>
#include <InstrumentPointingToAttitudeDTO.h>

namespace IO::SDK::API::DTO
{
    struct ScenarioDTO
    {
        char *Name;
        WindowDTO Window;
        SpacecraftDTO spacecraft;
        SiteDTO sites[2];

        DistanceConstraintDTO distances[5];
        OccultationConstraintDTO occultations[5];

        CelestialBodyDTO celestialBodies[10];
    };
}
