#pragma once
#include <string>
#include <FuelTankDTO.h>
#include <EngineDTO.h>
#include <InstrumentDTO.h>

namespace IO::SDK::API::DTO
{
    struct SpacecraftDTO
    {
        IO::SDK::API::DTO::FuelTankDTO fuelTank[10];
        IO::SDK::API::DTO::EngineDTO engines[10];
        IO::SDK::API::DTO::InstrumentDTO instruments[10];
    };
}
