#pragma once

namespace IO::SDK::API::DTO
{
    struct EngineDTO
    {
        int id;
        char * serialNumber;
        char * fuelTankSerialNumber;
        char * name;
        double isp;
        double fuelflow;
    };
    
}