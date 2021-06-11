#include <InvalidArgumentException.h>
#include<TimeSpan.h>
#include<chrono>

#ifndef DYNAMIC_FUELTANK_H
#define DYNAMIC_FUELTANK_H

namespace IO::SDK::Maneuvers
{
    struct DynamicFuelTank
    {
        // double RemainingFuel{};
        double EquivalentFuelFlow{};

        DynamicFuelTank()
        {
        }

        DynamicFuelTank(const double equivalentFuelFlow)
        {
            EquivalentFuelFlow = equivalentFuelFlow;
        }

        IO::SDK::Time::TimeSpan GetRemainingT(const double remainingFuel) const
        {
            return IO::SDK::Time::TimeSpan(std::chrono::duration<double>(remainingFuel / EquivalentFuelFlow));
        }
        
    };

}

#endif