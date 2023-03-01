//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_BYDAYPARAMETERS_H
#define IOSDK_BYDAYPARAMETERS_H

#include <Window.h>
#include <UTC.h>

namespace IO::SDK::Constraints::Parameters
{

    class ByDayParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_window;
        const double m_twilightDefinition;
    public :
        size_t Order{};

        ByDayParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC>& window, const double twilightDefinition);
        inline bool operator<(const ByDayParameters &rhs) const
        { return Order < rhs.Order; }
    };

} // IO

#endif //IOSDK_BYDAYPARAMETERS_H
