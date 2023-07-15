/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <LaunchSite.h>
#include <algorithm>

IO::Astrodynamics::Sites::LaunchSite::LaunchSite(const int id, const std::string &name, const IO::Astrodynamics::Coordinates::Planetodetic &coordinates,
                                       std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> body, std::string directoryPath) : Site(id, name, coordinates, std::move(body), std::move(directoryPath))
{
}

void IO::Astrodynamics::Sites::LaunchSite::AddAzimuthLaunchRange(IO::Astrodynamics::Coordinates::AzimuthRange &azimuthRange)
{
    for (auto &&az: m_azimuthRanges)
    {
        if (az.IsIntersected(azimuthRange))
        {
            throw IO::Astrodynamics::Exception::SDKException("Azimuth range overlap an existing azimuth range for this site");
        }
    }

    m_azimuthRanges.push_back(azimuthRange);

}

void IO::Astrodynamics::Sites::LaunchSite::ClearAzimuthLaunchRanges()
{
    m_azimuthRanges.clear();
}

bool IO::Astrodynamics::Sites::LaunchSite::IsAzimuthLaunchAllowed(const double azimuth) const
{
    return std::any_of(m_azimuthRanges.cbegin(), m_azimuthRanges.cend(), [&azimuth](IO::Astrodynamics::Coordinates::AzimuthRange az) { return az.IsInRange(azimuth); });
}
