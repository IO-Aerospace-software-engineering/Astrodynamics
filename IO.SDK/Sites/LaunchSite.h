/**
 * @file LaunchSite.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-26
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef LAUNCH_SITE_H
#define LAUNCH_SITE_H

#include <Site.h>
#include <AzimuthRange.h>

namespace IO::SDK::Sites
{
    /**
     * @brief Lauch site class
     * 
     */
    class LaunchSite final : public IO::SDK::Sites::Site
    {
    private:
        std::vector<IO::SDK::Coordinates::AzimuthRange> m_azimuthRanges;

    public:
        /**
         * @brief Construct a new Launch Site object
         * 
         * @param id 
         * @param name 
         * @param coordinates 
         * @param body 
         */
        LaunchSite(int id, const std::string& name, const IO::SDK::Coordinates::Geodetic& coordinates,
                   std::shared_ptr<IO::SDK::Body::CelestialBody> body);

        /**
         * @brief Add an azimuth range
         * 
         * @param azimuthRange 
         */
        void AddAzimuthLaunchRange(IO::SDK::Coordinates::AzimuthRange &azimuthRange);

        /**
         * @brief Clear all azimuth ranges
         * 
         */
        void ClearAzimuthLaunchRanges();

        /**
         * @brief Know if an azimuth launch is allowed
         * 
         * @param azimuth 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsAzimuthLaunchAllowed(double azimuth) const;
    };

} // namespace IO::SDK::Sites

#endif