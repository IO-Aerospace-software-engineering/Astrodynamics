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

namespace IO::SDK::API::DTO
{
    struct ScenarioRequestDTO
    {
        IO::SDK::API::DTO::SpacecraftDTO spacecrafts[10];
        IO::SDK::API::DTO::SiteDTO sites[10];
        IO::SDK::API::DTO::BodyVisibilityFromSiteDTO bodyVisibilityFromSites[10];
        IO::SDK::API::DTO::ByDayDTO byDays[10];
        IO::SDK::API::DTO::ByNightDTO byNights[10];
        IO::SDK::API::DTO::DistanceDTO distances[10];
        IO::SDK::API::DTO::InFieldOfViewDTO fovs[10];
        IO::SDK::API::DTO::LaunchDTO launches[10];
        IO::SDK::API::DTO::OccultationDTO occultations[10];
    };
}
