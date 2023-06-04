/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef LAUNCH_WINDOW_H
#define LAUNCH_WINDOW_H

#include <LaunchSite.h>

namespace IO::Astrodynamics::Maneuvers
{
    class LaunchWindow final
    {
    private:
        const IO::Astrodynamics::Sites::LaunchSite& m_launchSite;
        const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> m_window;
        const double m_inertialAzimuth;
        const double m_nonInertialAzimuth;
        
        const double m_nonInertialInsertionVelocity;
        const double m_inertialInsertionVelocity;

    public:
        /**
         * @brief Construct a new Launch Window object
         * 
         * @param launchSite 
         * @param window 
         * @param inertialAzimuth 
         * @param nonInertialAzimuth 
         * @param inertialInsertionVelocity 
         * @param nonInertialInsertionVelocity 
         */
        LaunchWindow(const IO::Astrodynamics::Sites::LaunchSite &launchSite, const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &window, double inertialAzimuth, double nonInertialAzimuth, double inertialInsertionVelocity, double nonInertialInsertionVelocity);

//        LaunchWindow& operator=(const LaunchWindow& lw);

        /**
         * @brief Get the Launch Site
         * 
         * @return const std::shared_ptr<IO::Astrodynamics::Sites::LaunchSite>&
         */
        [[nodiscard]] const IO::Astrodynamics::Sites::LaunchSite& GetLaunchSite() const;
        
        /**
         * @brief Get the Window
         * 
         * @return const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>&
         */
        [[nodiscard]] const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>& GetWindow() const;
        
        /**
         * @brief Get the Inertial Azimuth
         * 
         * @return double 
         */
        [[nodiscard]] double GetInertialAzimuth() const;

        /**
         * @brief Get the Non Inertial Azimuth
         * 
         * @return double 
         */
        [[nodiscard]] double GetNonInertialAzimuth() const;

        /**
         * @brief Get the Inertial Insertion Velocity
         * 
         * @return double 
         */
        [[nodiscard]] double GetInertialInsertionVelocity() const;

        /**
         * @brief Get the Non Inertial Insertion Velocity
         * 
         * @return double 
         */
        [[nodiscard]] double GetNonInertialInsertionVelocity() const;

    };

} // namespace IO::Astrodynamics::Maneuvers

#endif