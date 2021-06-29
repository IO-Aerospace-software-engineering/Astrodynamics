/**
 * @file Launch.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-26
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef LAUNCH_MANEUVER_H
#define LAUNCH_MANEUVER_H

#include <vector>
#include <memory>
#include <limits>

#include <ManeuverBase.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <Propagator.h>
#include <TDB.h>
#include <UTC.h>
#include <LaunchSite.h>
#include <OrbitalParameters.h>
#include <StateOrientation.h>
#include <Window.h>
#include <LaunchWindow.h>

namespace IO::SDK::Maneuvers
{
    class Launch
    {
    private:
        
        const std::shared_ptr<IO::SDK::Sites::LaunchSite> m_launchSite;
        const std::shared_ptr<IO::SDK::Sites::LaunchSite> m_recoverySite{nullptr};
        const bool m_launchByDay;
        const IO::SDK::OrbitalParameters::OrbitalParameters &m_targetOrbit;
        double m_nonInertialAscendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_inertialAscendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_inertialDescendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_nonInertialDescendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_nonInertialInsertionVelocity{std::numeric_limits<double>::quiet_NaN()};
        double m_inertialInsertionVelocity{std::numeric_limits<double>::quiet_NaN()};

        const double m_deltaL{std::numeric_limits<double>::quiet_NaN()};
        const double m_inclination{std::numeric_limits<double>::quiet_NaN()};

        std::vector<LaunchWindow> FindLaunchWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windowToSearch);

    public:
        /**
         * @brief Construct a new Launch object
         * 
         * @param launchSite 
         * @param recoverySite 
         * @param launchByDay 
         * @param targetOrbit 
         * @param spacecraft 
         */
        Launch(const std::shared_ptr<IO::SDK::Sites::LaunchSite> launchSite, const std::shared_ptr<IO::SDK::Sites::LaunchSite> recoverySite, bool launchByDay, const IO::SDK::OrbitalParameters::OrbitalParameters &targetOrbit);

        ~Launch() = default;

        /**
         * @brief Get the Non Inertial Ascending Azimuth Launch
         * 
         * @return double 
         */
        double GetNonInertialAscendingAzimuthLaunch();

        /**
         * @brief Get the Non Inertial Descending Azimuth Launch
         * 
         * @return double 
         */
        double GetNonInertialDescendingAzimuthLaunch();

        /**
         * @brief Get the Inertial Ascending Azimuth Launch
         * 
         * @return double 
         */
        double GetInertialAscendingAzimuthLaunch();

        /**
         * @brief Get the Inertial Descending Azimuth Launch
         * 
         * @return double 
         */
        double GetInertialDescendingAzimuthLaunch();

        /**
         * @brief Get the Non Inertial Insertion Velocity
         * 
         * @return double 
         */
        double GetNonInertialInsertionVelocity();

        /**
         * @brief Get the Inertial Insertion Velocity
         * 
         * @return double 
         */
        double GetInertialInsertionVelocity();

        /**
         * @brief Get the Launch Windows
         * 
         * @param searchWindow 
         * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> 
         */
        std::vector<LaunchWindow> GetLaunchWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow);
    };

} // namespace IO::SDK::Maneuvers

#endif // LAUNCH_MANEUVER_H
