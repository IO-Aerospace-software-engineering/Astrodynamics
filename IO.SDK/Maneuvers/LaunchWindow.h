/**
 * @file LaunchWindow.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef LAUNCH_WINDOW_H
#define LAUNCH_WINDOW_H

#include<memory>

#include <Window.h>
#include <UTC.h>
#include <LaunchSite.h>

namespace IO::SDK::Maneuvers
{
    class LaunchWindow final
    {
    private:
        const std::shared_ptr<IO::SDK::Sites::LaunchSite> m_launchSite;
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_window;
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
        LaunchWindow(const std::shared_ptr<IO::SDK::Sites::LaunchSite> &launchSite, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const double inertialAzimuth, const double nonInertialAzimuth, const double inertialInsertionVelocity, const double nonInertialInsertionVelocity);

        LaunchWindow& operator=(const LaunchWindow& lw);

        /**
         * @brief Get the Launch Site
         * 
         * @return const std::shared_ptr<IO::SDK::Sites::LaunchSite>& 
         */
        const std::shared_ptr<IO::SDK::Sites::LaunchSite>& GetLaunchSite() const;
        
        /**
         * @brief Get the Window
         * 
         * @return const IO::SDK::Time::Window<IO::SDK::Time::UTC>& 
         */
        const IO::SDK::Time::Window<IO::SDK::Time::UTC>& GetWindow() const;
        
        /**
         * @brief Get the Inertial Azimuth
         * 
         * @return double 
         */
        double GetInertialAzimuth() const;

        /**
         * @brief Get the Non Inertial Azimuth
         * 
         * @return double 
         */
        double GetNonInertialAzimuth() const;

        /**
         * @brief Get the Inertial Insertion Velocity
         * 
         * @return double 
         */
        double GetInertialInsertionVelocity() const;

        /**
         * @brief Get the Non Inertial Insertion Velocity
         * 
         * @return double 
         */
        double GetNonInertialInsertionVelocity() const;

    };

} // namespace IO::SDK::Maneuvers

#endif