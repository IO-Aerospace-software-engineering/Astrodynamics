#pragma once
#include <Models/SpacecraftDTO.h>
#include <Models/OccultationRequestDTO.h>
#include <Models/InFieldOfViewRequestDTO.h>

namespace IO::SDK::Proxy::Models
{
    struct ScenarioRequestDTO
    {
        IO::SDK::Proxy::Models::SpacecraftDTO spacecraft;
        IO::SDK::Proxy::Models::OccultationRequestDTO occultations[10];
        IO::SDK::Proxy::Models::InFieldOfViewRequestDTO fovs[10];
        int involvedCelestialBodies[4];
    };
}
