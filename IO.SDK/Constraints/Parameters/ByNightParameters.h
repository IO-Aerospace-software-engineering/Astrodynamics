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
        const double m_twilightDefinition;
        const IO::SDK::Sites::Site &m_site;
    public :
        ByNightParameters(const IO::SDK::Sites::Site &site, const double twilightDefinition);

        inline double GetTwilightDefinition() const
        { return m_twilightDefinition; }

        inline const IO::SDK::Sites::Site &GetSite() const
        { return m_site; }
    };

} // IO

#endif //IOSDK_BYNIGHTPARAMETERS_H
