#pragma once
#include <string>
#include <FuelTankDTO.h>
#include <EngineDTO.h>
#include <InstrumentDTO.h>
#include <PayloadDTO.h>
#include <StateVectorDTO.h>

namespace IO::SDK::API::DTO
{
    struct SpacecraftDTO
    {
        int id{0};
        const char* name;
        double dryOperatingMass;
        double maximumOperatingMass;
        IO::SDK::API::DTO::StateVectorDTO initialOrbitalParameter;
        IO::SDK::API::DTO::FuelTankDTO fuelTank[10];
        IO::SDK::API::DTO::EngineDTO engines[10];
        IO::SDK::API::DTO::InstrumentDTO instruments[10];
        IO::SDK::API::DTO::PayloadDTO payloads[10];
    };
}
