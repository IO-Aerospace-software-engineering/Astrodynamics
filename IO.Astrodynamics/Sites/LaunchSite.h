/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef LAUNCH_SITE_H
#define LAUNCH_SITE_H

#include <Site.h>
#include <AzimuthRange.h>

namespace IO::Astrodynamics::Sites
{
    /**
     * @brief Lauch site class
     * 
     */
    class LaunchSite final : public IO::Astrodynamics::Sites::Site
    {
    private:
        std::vector<IO::Astrodynamics::Coordinates::AzimuthRange> m_azimuthRanges;

    public:
        /**
         * @brief Construct a new Launch Site object
         * 
         * @param id 
         * @param name 
         * @param coordinates 
         * @param body 
         */
        LaunchSite(int id, const std::string& name, const IO::Astrodynamics::Coordinates::Planetodetic& coordinates,
                   std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> body,std::string directoryPath);

        /**
         * @brief Add an azimuth range
         * 
         * @param azimuthRange 
         */
        void AddAzimuthLaunchRange(IO::Astrodynamics::Coordinates::AzimuthRange &azimuthRange);

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

} // namespace IO::Astrodynamics::Sites

#endif