#pragma once
#include <string>
#include <Models/FuelTankDTO.h>
#include <Models/EngineDTO.h>

namespace IO::SDK::Proxy::Models
{
    struct SpacecraftDTO
    {
        IO::SDK::Proxy::Models::FuelTankDTO fuelTank[10];
        IO::SDK::Proxy::Models::EngineDTO engines[20];
    };
}
