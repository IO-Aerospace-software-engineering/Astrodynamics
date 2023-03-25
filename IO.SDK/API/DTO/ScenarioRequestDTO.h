#pragma once
#include <SpacecraftDTO.h>
#include <OccultationRequestDTO.h>
#include <InFieldOfViewRequestDTO.h>
#include <SiteDTO.h>

namespace IO::SDK::API::DTO
{
    struct ScenarioRequestDTO
    {
        IO::SDK::API::DTO::SpacecraftDTO spacecrafts[10];
        IO::SDK::API::DTO::SiteDTO sites[10];
        IO::SDK::API::DTO::OccultationRequestDTO occultations[10];
        IO::SDK::API::DTO::InFieldOfViewRequestDTO fovs[10];
        int involvedCelestialBodies[4];
    };
}
