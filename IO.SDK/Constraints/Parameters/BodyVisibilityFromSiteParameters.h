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
        const IO::SDK::Sites::Site &m_site;
        const IO::SDK::Body::Body &m_target;
        const IO::SDK::AberrationsEnum m_aberration;

    public:
        BodyVisibilityFromSiteParameters(const IO::SDK::Sites::Site &site, const IO::SDK::Body::Body &target,
                                         IO::SDK::AberrationsEnum aberration);

        [[nodiscard]] inline const IO::SDK::Sites::Site &GetSite() const
        { return m_site; }

        [[nodiscard]] inline const IO::SDK::Body::Body &GetTarget() const
        { return m_target; }

        [[nodiscard]] inline IO::SDK::AberrationsEnum GetAberration() const
        { return m_aberration; }
    };


} // IO

#endif //IOSDK_BODYVISIBILITYFROMSITEPARAMETERS_H
