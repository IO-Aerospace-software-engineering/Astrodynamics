#pragma once

namespace IO::SDK::API::DTO
{
    struct FuelTankDTO
    {
        int id;
        const char * serialNumber;

        double capacity;
        double quantity;
    };
}
