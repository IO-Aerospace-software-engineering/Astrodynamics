/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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

namespace IO::Astrodynamics::Maneuvers
{
    class Launch final
    {
    private:
        
        const IO::Astrodynamics::Sites::LaunchSite& m_launchSite;
        const IO::Astrodynamics::Sites::Site& m_recoverySite;
        const bool m_launchByDay;
        const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &m_targetOrbit;
        double m_nonInertialAscendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_inertialAscendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_inertialDescendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_nonInertialDescendingAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
        double m_nonInertialInsertionVelocity{std::numeric_limits<double>::quiet_NaN()};

        const double m_deltaL{std::numeric_limits<double>::quiet_NaN()};
        const double m_inclination{std::numeric_limits<double>::quiet_NaN()};

        std::vector<LaunchWindow> FindLaunchWindows(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &windowToSearch);

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
        Launch(const IO::Astrodynamics::Sites::LaunchSite& launchSite, const IO::Astrodynamics::Sites::Site& recoverySite, bool launchByDay, const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &targetOrbit);

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
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
         */
        std::vector<LaunchWindow> GetLaunchWindows(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow);
    };

} // namespace IO::Astrodynamics::Maneuvers

#endif // LAUNCH_MANEUVER_H
