//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_BODYVISIBILITYFROMSITEPARAMETERS_H
#define IOSDK_BODYVISIBILITYFROMSITEPARAMETERS_H

#include <TDB.h>
#include <Window.h>
#include <Aberrations.h>
#include <Body.h>
#include <Site.h>

namespace IO::SDK::Constraints::Parameters
{

    class BodyVisibilityFromSiteParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> &m_window;
        const IO::SDK::Sites::Site &m_site;
        const IO::SDK::Body::Body &m_target;
        const IO::SDK::AberrationsEnum m_aberration;

    public:
        size_t Order{};

        BodyVisibilityFromSiteParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const IO::SDK::Sites::Site &site, const IO::SDK::Body::Body& target,
                                         const IO::SDK::AberrationsEnum aberration);

        inline bool operator<(const BodyVisibilityFromSiteParameters &rhs) const
        { return Order < rhs.Order; }
    };

} // IO

#endif //IOSDK_BODYVISIBILITYFROMSITEPARAMETERS_H
