//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_BYDAYPARAMETERS_H
#define IOSDK_BYDAYPARAMETERS_H

#include <Window.h>
#include <UTC.h>
#include <Site.h>

namespace IO::SDK::Constraints::Parameters
{

    class ByDayParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_window;
        const double m_twilightDefinition;
        const IO::SDK::Sites::Site &m_site;
    public :
        ByDayParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC>& window,const IO::SDK::Sites::Site& site, const double twilightDefinition);
    };

} // IO

#endif //IOSDK_BYDAYPARAMETERS_H
