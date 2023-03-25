#pragma once
#include <SpacecraftDTO.h>
#include <InFieldOfViewDTO.h>
#include <SiteDTO.h>
#include <OccultationDTO.h>

namespace IO::SDK::API::DTO
{
    struct ScenarioRequestDTO
    {
        IO::SDK::API::DTO::SpacecraftDTO spacecrafts[10];
        IO::SDK::API::DTO::SiteDTO sites[10];
        IO::SDK::API::DTO::OccultationDTO occultations[10];
        IO::SDK::API::DTO::InFieldOfViewDTO fovs[10];
        int involvedCelestialBodies[4];
    };
}
