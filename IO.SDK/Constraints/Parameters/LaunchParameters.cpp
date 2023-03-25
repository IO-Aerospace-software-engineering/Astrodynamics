//
// Created by spacer on 3/23/23.
//

#include "LaunchParameters.h"

IO::SDK::Constraints::Parameters::LaunchParameters::LaunchParameters(const IO::SDK::Sites::LaunchSite& launchSiteId, const IO::SDK::Sites::Site& recoverySiteId, bool launchByDay,
                                                                     const IO::SDK::OrbitalParameters::OrbitalParameters& targetOrbit)
        : m_launchSite(launchSiteId), m_recoverySite(recoverySiteId), m_launchByDay(launchByDay), m_targetOrbit(targetOrbit)
{}
