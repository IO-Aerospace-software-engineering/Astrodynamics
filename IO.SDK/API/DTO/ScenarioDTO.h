#pragma once

#include <SpacecraftDTO.h>
#include <InFieldOfViewDTO.h>
#include <SiteDTO.h>
#include <OccultationDTO.h>
#include <BodyVisibilityFromSiteDTO.h>
#include <ByDayDTO.h>
#include <ByNightDTO.h>
#include <DistanceDTO.h>
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

        IO::SDK::API::DTO::DistanceDTO distances[5];
        IO::SDK::API::DTO::OccultationDTO occultations[5];
    };
}
