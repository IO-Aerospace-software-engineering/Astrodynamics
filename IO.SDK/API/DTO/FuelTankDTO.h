#pragma once

namespace IO::SDK::API::DTO
{
    struct FuelTankDTO
    {
        int id;
        char * serialNumber;

        double capacity;
        double quantity;
    };
}
