//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_BYNIGHTPARAMETERS_H
#define IOSDK_BYNIGHTPARAMETERS_H

#include <UTC.h>
#include <Window.h>

namespace IO::SDK::Constraints::Parameters
{

    class ByNightParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_window;
        const double m_twilightDefinition;
    public :
        size_t Order{};
        ByNightParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC>& window, const double twilightDefinition);
        inline bool operator<(const ByNightParameters &rhs) const
        { return Order < rhs.Order; }
    };

} // IO

#endif //IOSDK_BYNIGHTPARAMETERS_H
