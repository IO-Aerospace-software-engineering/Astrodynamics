//
// Created by spacer on 3/23/23.
//

#ifndef IOSDK_LAUNCHPARAMETERS_H
#define IOSDK_LAUNCHPARAMETERS_H

#include <OrbitalParameters.h>
#include <LaunchSite.h>

namespace IO::SDK::Constraints::Parameters
{
    class LaunchParameters
    {
    private:
        const IO::SDK::Sites::LaunchSite &m_launchSite;
        const IO::SDK::Sites::Site &m_recoverySite;
        const bool m_launchByDay{false};
        const IO::SDK::OrbitalParameters::OrbitalParameters &m_targetOrbit;

    public:
        LaunchParameters(const IO::SDK::Sites::LaunchSite &launchSite, const IO::SDK::Sites::Site &recoverySite, bool launchByDay,
                         const IO::SDK::OrbitalParameters::OrbitalParameters &targetOrbit);

        [[nodiscard]] inline const IO::SDK::Sites::LaunchSite &GetLaunchSite() const
        { return m_launchSite; }

        [[nodiscard]] inline const IO::SDK::Sites::Site &GetRecoverySite() const
        { return m_recoverySite; }

        [[nodiscard]] inline bool GetLaunchByDay() const
        { return m_launchByDay; }

        [[nodiscard]] inline const IO::SDK::OrbitalParameters::OrbitalParameters &GetTargetOrbit() const
        { return m_targetOrbit; }
    };
}


#endif //IOSDK_LAUNCHPARAMETERS_H
