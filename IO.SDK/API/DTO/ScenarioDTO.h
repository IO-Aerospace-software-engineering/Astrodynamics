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
        IO::SDK::API::DTO::SpacecraftDTO spacecraft;
        IO::SDK::API::DTO::SiteDTO sites[2];

        IO::SDK::API::DTO::DistanceConstraintDTO distances[5];
        IO::SDK::API::DTO::OccultationConstraintDTO occultations[5];
    };
}
