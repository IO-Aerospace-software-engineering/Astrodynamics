/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#pragma once

namespace IO::SDK::API::DTO
{
    struct EngineDTO
    {
        int id;
        const char * serialNumber;
        const char * fuelTankSerialNumber;
        const char * name;
        double isp;
        double fuelFlow;
    };
    
}