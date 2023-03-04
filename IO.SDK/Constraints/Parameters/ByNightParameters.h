//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_BYNIGHTPARAMETERS_H
#define IOSDK_BYNIGHTPARAMETERS_H

#include <UTC.h>
#include <Window.h>
#include <Site.h>

namespace IO::SDK::Constraints::Parameters
{

    class ByNightParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_window;
        const double m_twilightDefinition;
        const IO::SDK::Sites::Site& m_site;
    public :
        ByNightParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const IO::SDK::Sites::Site &site, const double twilightDefinition);
    };

} // IO

#endif //IOSDK_BYNIGHTPARAMETERS_H
