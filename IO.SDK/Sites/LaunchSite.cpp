/**
 * @file LaunchSite.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#include <iterator>
#include <LaunchSite.h>
#include <algorithm>

IO::SDK::Sites::LaunchSite::LaunchSite(const int id, const std::string &name, const IO::SDK::Coordinates::Geodetic& coordinates,
                                       std::shared_ptr<IO::SDK::Body::CelestialBody> body) : Site(id, name, coordinates, body)
{
}

void IO::SDK::Sites::LaunchSite::AddAzimuthLaunchRange(IO::SDK::Coordinates::AzimuthRange &azimuthRange)
{
    for (auto &&az: m_azimuthRanges)
    {
        if (az.IsIntersected(azimuthRange))
        {
            throw IO::SDK::Exception::SDKException("Azimuth range overlap an existing azimuth range for this site");
        }
    }

    m_azimuthRanges.push_back(azimuthRange);

}

void IO::SDK::Sites::LaunchSite::ClearAzimuthLaunchRanges()
{
    m_azimuthRanges.clear();
}

bool IO::SDK::Sites::LaunchSite::IsAzimuthLaunchAllowed(const double azimuth) const
{
    return std::any_of(m_azimuthRanges.cbegin(), m_azimuthRanges.cend(), [&azimuth](IO::SDK::Coordinates::AzimuthRange az) { return az.IsInRange(azimuth); });
}
